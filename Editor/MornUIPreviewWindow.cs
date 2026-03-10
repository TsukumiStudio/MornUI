using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MornLib
{
    public sealed class MornUIPreviewWindow : EditorWindow
    {
        private string _htmlPath = "";
        private string _cssPath = "";
        private string _htmlContent = "";
        private string _cssContent = "";
        private int _width = 800;
        private int _height = 600;
        private Texture _texture;
        private bool _dirty = true;
        private string _prevHtml;
        private string _prevCss;
        private int _prevWidth;
        private int _prevHeight;
        private int _prevGlobalHash;
        private double _lastUpdateTime;

        private Vector2 _htmlScroll;
        private Vector2 _cssScroll;
        private Vector2 _previewScroll;
        private MornUIContext _context;

        private string _outputDir;
        private bool _autoReload;
        private FileSystemWatcher _htmlWatcher;
        private FileSystemWatcher _cssWatcher;
        private bool _fileChanged;

        [MenuItem("Tools/MornUI/Preview")]
        private static void Open()
        {
            var window = GetWindow<MornUIPreviewWindow>("MornUI Preview");
            window.LoadDefaultsIfEmpty();
        }

        private void LoadDefaultsIfEmpty()
        {
            if (!string.IsNullOrEmpty(_htmlPath) || !string.IsNullOrEmpty(_cssPath)) return;

            // MornUIPreviewWindow.cs → Editor/ → MornUI/ → Samples~/
            var scriptPath = AssetDatabase.FindAssets($"t:MonoScript {nameof(MornUIPreviewWindow)}");
            if (scriptPath.Length == 0) return;
            var editorDir = Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(scriptPath[0]));
            var samplesDir = Path.GetFullPath(Path.Combine(editorDir!, "..", "Samples~"));
            var htmlFile = Path.Combine(samplesDir, "sample.html");
            var cssFile = Path.Combine(samplesDir, "sample.css");
            if (File.Exists(htmlFile)) _htmlPath = htmlFile;
            if (File.Exists(cssFile)) _cssPath = cssFile;
            LoadFiles();
        }

        private void OnEnable()
        {
            LoadDefaultsIfEmpty();

            if (string.IsNullOrEmpty(_htmlContent) && !string.IsNullOrEmpty(_htmlPath) && File.Exists(_htmlPath)
                || string.IsNullOrEmpty(_cssContent) && !string.IsNullOrEmpty(_cssPath) && File.Exists(_cssPath))
            {
                LoadFiles();
            }

            _context = null;
            _texture = null;
            _dirty = true;

            if (_autoReload)
                SetupWatchers();
        }

        private void OnGUI()
        {
            // File selectors
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("HTML File", GUILayout.Width(60));
            _htmlPath = EditorGUILayout.TextField(_htmlPath);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                var path = EditorUtility.OpenFilePanel("Select HTML", Application.dataPath, "html");
                if (!string.IsNullOrEmpty(path)) _htmlPath = path;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("CSS File", GUILayout.Width(60));
            _cssPath = EditorGUILayout.TextField(_cssPath);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                var path = EditorUtility.OpenFilePanel("Select CSS", Application.dataPath, "css");
                if (!string.IsNullOrEmpty(path)) _cssPath = path;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Load Files"))
            {
                LoadFiles();
                ForceRender();
            }

            var newAutoReload = GUILayout.Toggle(_autoReload, "Auto Reload", "Button", GUILayout.Width(100));
            if (newAutoReload != _autoReload)
            {
                _autoReload = newAutoReload;
                if (_autoReload)
                    SetupWatchers();
                else
                    DisposeWatchers();
            }

            EditorGUILayout.EndHorizontal();

            // Content preview
            EditorGUILayout.LabelField("HTML", EditorStyles.boldLabel);
            _htmlScroll = EditorGUILayout.BeginScrollView(_htmlScroll, GUILayout.Height(120));
            _htmlContent = EditorGUILayout.TextArea(_htmlContent, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.LabelField("CSS", EditorStyles.boldLabel);
            _cssScroll = EditorGUILayout.BeginScrollView(_cssScroll, GUILayout.Height(80));
            _cssContent = EditorGUILayout.TextArea(_cssContent, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            _width = EditorGUILayout.IntField("Width", _width);
            _height = EditorGUILayout.IntField("Height", _height);
            EditorGUILayout.EndHorizontal();

            // Action buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save PNG")) SavePng();
            if (GUILayout.Button("Open in Browser")) OpenInBrowser();
            if (GUILayout.Button("Browser Screenshot")) TakeBrowserScreenshot();
            if (GUILayout.Button("Open Folder"))
            {
                EnsureOutputDir();
                RevealInFinder(_outputDir);
            }

            EditorGUILayout.EndHorizontal();

            // Preview
            if (_texture != null)
            {
                EditorGUILayout.Space();
                _previewScroll = EditorGUILayout.BeginScrollView(_previewScroll);
                var rect = GUILayoutUtility.GetRect(
                    _texture.width, _texture.height,
                    GUILayout.MinWidth(_texture.width), GUILayout.MaxWidth(_texture.width),
                    GUILayout.MinHeight(_texture.height), GUILayout.MaxHeight(_texture.height));
                EditorGUI.DrawPreviewTexture(rect, _texture);

                // Click handling
                if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                {
                    var localPos = Event.current.mousePosition - rect.position;
                    var layoutX = localPos.x;
                    var layoutY = localPos.y;
                    if (_context != null && _context.HandleClick(layoutX, layoutY))
                    {
                        _context.MarkDirty();
                        Repaint();
                    }
                    Event.current.Use();
                }

                // Tab / Shift+Tab focus navigation
                if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Tab && _context != null)
                {
                    var forward = !Event.current.shift;
                    if (_context.MoveFocus(forward))
                    {
                        _context.MarkDirty();
                        Repaint();
                    }
                    Event.current.Use();
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void LoadFiles()
        {
            _htmlContent = !string.IsNullOrEmpty(_htmlPath) && File.Exists(_htmlPath)
                ? File.ReadAllText(_htmlPath)
                : "";
            _cssContent = !string.IsNullOrEmpty(_cssPath) && File.Exists(_cssPath)
                ? File.ReadAllText(_cssPath)
                : "";
        }

        private void ForceRender()
        {
            _context = null;
            _prevHtml = null;
            _prevCss = null;
            _dirty = true;
            _lastUpdateTime = EditorApplication.timeSinceStartup;
        }

        private void Render()
        {
            _context ??= new MornUIContext();

            _width = Mathf.Clamp(_width, 1, 4096);
            _height = Mathf.Clamp(_height, 1, 4096);

            _texture = _context.Render(_htmlContent, _cssContent, _width, _height);
        }

        private void SavePng()
        {
            if (_texture == null)
            {
                EditorUtility.DisplayDialog("MornUI", "先にRenderしてください。", "OK");
                return;
            }

            EnsureOutputDir();
            var path = Path.Combine(_outputDir, $"{ComputeHash()}_MornUI.png");
            File.WriteAllBytes(path, TextureToPng(_texture));
            Debug.Log($"MornUI: PNG saved to {path}");
            RevealInFinder(path);
        }

        private static byte[] TextureToPng(Texture texture)
        {
            if (texture is Texture2D tex2D)
                return tex2D.EncodeToPNG();

            if (texture is RenderTexture rt)
            {
                // Linear RT contains linear values; blit to sRGB RT for correct PNG output
                var srgbRT = RenderTexture.GetTemporary(rt.width, rt.height, 0,
                    RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(rt, srgbRT);

                var prev = RenderTexture.active;
                RenderTexture.active = srgbRT;
                var temp = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
                temp.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                temp.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(srgbRT);

                var png = temp.EncodeToPNG();
                DestroyImmediate(temp);
                return png;
            }

            return null;
        }

        private string BuildBrowserHtml()
        {
            var cssBlock = string.IsNullOrEmpty(_cssContent) ? "" : $"<style>{_cssContent}</style>\n";
            return "<!DOCTYPE html>\n<html>\n<head>\n<meta charset=\"utf-8\">\n"
                   + "<link rel=\"preconnect\" href=\"https://fonts.googleapis.com\">\n"
                   + "<link rel=\"preconnect\" href=\"https://fonts.gstatic.com\" crossorigin>\n"
                   + "<link href=\"https://fonts.googleapis.com/css2?family=Noto+Sans+JP:wght@400;700&display=swap\" rel=\"stylesheet\">\n"
                   + "<style>* { font-family: 'Noto Sans JP', sans-serif; }</style>\n"
                   + cssBlock
                   + "</head>\n<body style=\"margin:0;background:#222;\">\n"
                   + _htmlContent
                   + "\n</body>\n</html>";
        }

        private void OpenInBrowser()
        {
            var tmpPath = Path.Combine(Path.GetTempPath(), "mornui_preview.html");
            File.WriteAllText(tmpPath, BuildBrowserHtml());
            Application.OpenURL("file://" + tmpPath);
        }

        private void TakeBrowserScreenshot()
        {
            var homePath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            var scriptPath = Path.Combine(homePath, ".mornui", "mornui_screenshot.mjs");
            if (!File.Exists(scriptPath))
            {
                EditorUtility.DisplayDialog("MornUI",
                    $"スクリーンショット用スクリプトが見つかりません。\n{scriptPath}\n\n以下を実行してセットアップしてください:\nmkdir -p ~/.mornui && cd ~/.mornui && npm init -y && npm install puppeteer",
                    "OK");
                return;
            }

            EnsureOutputDir();
            var screenshotPath = Path.Combine(_outputDir, $"{ComputeHash()}_Browser.png");

            var htmlPath = Path.Combine(Path.GetTempPath(), "mornui_preview.html");
            File.WriteAllText(htmlPath, BuildBrowserHtml());

            if (File.Exists(screenshotPath))
                File.Delete(screenshotPath);

            var w = Mathf.Clamp(_width, 1, 4096);
            var h = Mathf.Clamp(_height, 1, 4096);

            var nodePath = ResolveNodePath();
            if (nodePath == null)
            {
                EditorUtility.DisplayDialog("MornUI", "node が見つかりません。Node.js をインストールしてください。", "OK");
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = nodePath,
                Arguments = $"\"{scriptPath}\" \"{htmlPath}\" \"{screenshotPath}\" {w} {h}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            try
            {
                var process = Process.Start(psi);
                process?.WaitForExit(30000);

                if (!File.Exists(screenshotPath))
                {
                    var err = process?.StandardError.ReadToEnd();
                    EditorUtility.DisplayDialog("MornUI", $"スクリーンショット取得に失敗しました。\n{err}", "OK");
                    return;
                }

                Debug.Log($"MornUI: Browser screenshot saved to {screenshotPath}");
                RevealInFinder(_outputDir);
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("MornUI", $"エラー:\n{e.Message}", "OK");
            }
        }

        private string ComputeHash()
        {
            var combined = (_htmlContent ?? "") + "|||" + (_cssContent ?? "");
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(combined));
            return System.BitConverter.ToString(bytes, 0, 8).Replace("-", "").ToLowerInvariant();
        }

        private void EnsureOutputDir()
        {
            if (string.IsNullOrEmpty(_outputDir) || !Directory.Exists(_outputDir))
            {
                _outputDir = Path.Combine(Path.GetDirectoryName(Application.dataPath)!, "Screenshots");
                Directory.CreateDirectory(_outputDir);
            }
        }

        private static void RevealInFinder(string path)
        {
#if UNITY_EDITOR_OSX
            Process.Start("open", $"\"{path}\"");
#elif UNITY_EDITOR_WIN
            Process.Start("explorer.exe", $"\"{path}\"");
#endif
        }

        private static string ResolveNodePath()
        {
            string[] candidates =
            {
                "/usr/local/bin/node",
                "/opt/homebrew/bin/node",
            };
            foreach (var c in candidates)
                if (File.Exists(c)) return c;

            var homePath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            var nvmDir = Path.Combine(homePath, ".nvm", "versions", "node");
            if (Directory.Exists(nvmDir))
            {
                var dirs = Directory.GetDirectories(nvmDir);
                if (dirs.Length > 0)
                {
                    System.Array.Sort(dirs);
                    var nodePath = Path.Combine(dirs[^1], "bin", "node");
                    if (File.Exists(nodePath)) return nodePath;
                }
            }

            return null;
        }

        private void Update()
        {
            if (_fileChanged)
            {
                _fileChanged = false;
                LoadFiles();
                ForceRender();
            }

            // Full re-render when content/settings changed
            var globalHash = ComputeGlobalHash();
            if (_texture == null || _dirty || _htmlContent != _prevHtml || _cssContent != _prevCss
                || _width != _prevWidth || _height != _prevHeight
                || globalHash != _prevGlobalHash)
            {
                _dirty = false;
                _prevHtml = _htmlContent;
                _prevCss = _cssContent;
                _prevWidth = _width;
                _prevHeight = _height;
                _prevGlobalHash = globalHash;
                _lastUpdateTime = EditorApplication.timeSinceStartup;
                Render();
                Repaint();
                return;
            }

            // Every frame: advance animations or re-render
            var now = EditorApplication.timeSinceStartup;
            var dt = (float)(now - _lastUpdateTime);
            _lastUpdateTime = now;

            if (_context == null) return;

            if (_context.HasRunningAnimations || _context.IsDirty)
            {
                var newTex = _context.UpdateAnimations(dt);
                if (newTex != null)
                    _texture = newTex;
                Repaint();
            }
        }

        private static int ComputeGlobalHash()
        {
            var g = MornUIGlobal.I;
            if (g == null) return 0;
            var hash = g.ClearColor.GetHashCode();
            hash = hash * 397 ^ (g.DefaultFont != null ? g.DefaultFont.GetInstanceID() : 0);
            hash = hash * 397 ^ (int)g.FilterMode;
            hash = hash * 397 ^ g.TextSupersample;
            hash = hash * 397 ^ g.TextGamma.GetHashCode();
            hash = hash * 397 ^ g.TextRenderPadding;
            hash = hash * 397 ^ g.TextRenderLayer;
            return hash;
        }

        private void SetupWatchers()
        {
            DisposeWatchers();
            _htmlWatcher = CreateWatcher(_htmlPath);
            _cssWatcher = CreateWatcher(_cssPath);
        }

        private FileSystemWatcher CreateWatcher(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return null;
            var dir = Path.GetDirectoryName(filePath);
            var name = Path.GetFileName(filePath);
            if (string.IsNullOrEmpty(dir)) return null;
            var watcher = new FileSystemWatcher(dir, name)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };
            watcher.Changed += OnFileChanged;
            return watcher;
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            _fileChanged = true;
        }

        private void DisposeWatchers()
        {
            if (_htmlWatcher != null)
            {
                _htmlWatcher.EnableRaisingEvents = false;
                _htmlWatcher.Dispose();
                _htmlWatcher = null;
            }

            if (_cssWatcher != null)
            {
                _cssWatcher.EnableRaisingEvents = false;
                _cssWatcher.Dispose();
                _cssWatcher = null;
            }
        }

        private void OnDisable()
        {
            DisposeWatchers();
            _texture = null;
            _context?.Cleanup();
        }
    }
}
