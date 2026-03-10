using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MornLib
{
    internal static class MornUITextPainter
    {
        private static int RenderLayer => MornUIGlobal.I != null ? MornUIGlobal.I.TextRenderLayer : 31;
        private static int RenderPadding => MornUIGlobal.I != null ? MornUIGlobal.I.TextRenderPadding : 4;

        private sealed class MeasurementContext
        {
            public GameObject Root;
            public TextMeshProUGUI Text;
        }

        private static MeasurementContext s_measurement;

        // Render infrastructure reuse
        private sealed class RenderContext
        {
            public GameObject CameraGo;
            public Camera Camera;
            public GameObject CanvasGo;
            public Canvas Canvas;
            public GameObject TextGo;
            public TextMeshProUGUI Tmp;
        }

        private static RenderContext s_render;

        // Text pixel cache: keyed by content hash, stores downsampled pixels
        private struct TextCacheKey
        {
            public string Text;
            public int FontId;
            public float FontSize;
            public int Width;
            public int Height;
            public MornUITextAlign Align;
            public MornUIWhiteSpace WhiteSpace;
            public MornUITextOverflow TextOverflow;
            public MornUIOverflow Overflow;
            public float LineHeight;

            public override int GetHashCode()
            {
                var h = Text?.GetHashCode() ?? 0;
                h = h * 397 ^ FontId;
                h = h * 397 ^ FontSize.GetHashCode();
                h = h * 397 ^ Width;
                h = h * 397 ^ Height;
                h = h * 397 ^ (int)Align;
                h = h * 397 ^ (int)WhiteSpace;
                h = h * 397 ^ (int)TextOverflow;
                h = h * 397 ^ (int)Overflow;
                h = h * 397 ^ LineHeight.GetHashCode();
                return h;
            }

            public override bool Equals(object obj)
            {
                if (obj is not TextCacheKey other) return false;
                return Text == other.Text && FontId == other.FontId &&
                       Mathf.Approximately(FontSize, other.FontSize) &&
                       Width == other.Width && Height == other.Height &&
                       Align == other.Align && WhiteSpace == other.WhiteSpace &&
                       TextOverflow == other.TextOverflow && Overflow == other.Overflow &&
                       Mathf.Approximately(LineHeight, other.LineHeight);
            }
        }

        public struct TextGPUInfo
        {
            public Texture2D Texture;
            public int SrcWidth;
            public int SrcHeight;
            public int RenderContentH;
            public int VerticalOffset;
            public int Padding;
        }

        private struct TextCacheEntry
        {
            public Texture2D GPUTexture;
            public int RenderContentH;
            public int VerticalOffset;
        }

        private static readonly Dictionary<TextCacheKey, TextCacheEntry> s_textCache = new();

        // Measurement cache to avoid repeated GetPreferredValues calls
        private struct MeasureCacheKey
        {
            public string Text;
            public int FontId;
            public float FontSize;
            public float MaxWidth;
            public MornUIWhiteSpace WhiteSpace;
            public float LineHeight;

            public override int GetHashCode()
            {
                var h = Text?.GetHashCode() ?? 0;
                h = h * 397 ^ FontId;
                h = h * 397 ^ FontSize.GetHashCode();
                h = h * 397 ^ MaxWidth.GetHashCode();
                h = h * 397 ^ (int)WhiteSpace;
                h = h * 397 ^ LineHeight.GetHashCode();
                return h;
            }

            public override bool Equals(object obj)
            {
                if (obj is not MeasureCacheKey other) return false;
                return Text == other.Text && FontId == other.FontId &&
                       Mathf.Approximately(FontSize, other.FontSize) &&
                       Mathf.Approximately(MaxWidth, other.MaxWidth) &&
                       WhiteSpace == other.WhiteSpace &&
                       Mathf.Approximately(LineHeight, other.LineHeight);
            }
        }

        private static readonly Dictionary<MeasureCacheKey, float> s_measureWidthCache = new();
        private static readonly Dictionary<MeasureCacheKey, float> s_measureHeightCache = new();
        private static readonly Dictionary<MeasureCacheKey, float> s_measureMultiLineCache = new();

        private static int Supersample => MornUIGlobal.I != null ? Mathf.Clamp(MornUIGlobal.I.TextSupersample, 1, 4) : 2;
        private static float TextGamma => MornUIGlobal.I != null ? MornUIGlobal.I.TextGamma : 0.7f;

        public static bool TryGetTextGPUInfo(string text, TMP_FontAsset font, float fontSize,
            int width, int height, MornUITextAlign align, MornUIWhiteSpace whiteSpace,
            MornUITextOverflow textOverflow, MornUIOverflow overflow, float lineHeightMultiplier,
            out TextGPUInfo info)
        {
            info = default;
            if (string.IsNullOrEmpty(text) || width <= 0 || height <= 0 || font == null)
                return false;

            var padding = RenderPadding;
            var cacheKey = new TextCacheKey
            {
                Text = text,
                FontId = font.GetInstanceID(),
                FontSize = fontSize,
                Width = width,
                Height = height,
                Align = align,
                WhiteSpace = whiteSpace,
                TextOverflow = textOverflow,
                Overflow = overflow,
                LineHeight = lineHeightMultiplier,
            };

            if (!s_textCache.TryGetValue(cacheKey, out var cached))
            {
                cached = RenderTextToCacheGPU(text, font, fontSize, width, height, align,
                    whiteSpace, textOverflow, overflow, lineHeightMultiplier);
                s_textCache[cacheKey] = cached;
            }

            var sw = width + padding * 2;
            var sh = cached.RenderContentH + padding * 2;
            info = new TextGPUInfo
            {
                Texture = cached.GPUTexture,
                SrcWidth = sw,
                SrcHeight = sh,
                RenderContentH = cached.RenderContentH,
                VerticalOffset = cached.VerticalOffset,
                Padding = padding,
            };
            return true;
        }

        /// <summary>GPU path: render TMP at high-res, then GPU Blit downsample. No CPU readback.</summary>
        private static TextCacheEntry RenderTextToCacheGPU(string text, TMP_FontAsset font, float fontSize,
            int width, int height, MornUITextAlign align, MornUIWhiteSpace whiteSpace,
            MornUITextOverflow textOverflow, MornUIOverflow overflow, float lineHeightMultiplier)
        {
            var ss = Supersample;
            var padding = RenderPadding;
            var naturalH = Mathf.CeilToInt(MeasureNaturalHeight(text, font, fontSize, width, whiteSpace));
            var renderContentH = Mathf.Max(height, naturalH);

            var faceInfo = font.faceInfo;
            var singleLineNaturalH = faceInfo.lineHeight / (float)faceInfo.pointSize * fontSize;
            var cssLineHeight = lineHeightMultiplier > 0f ? fontSize * lineHeightMultiplier : singleLineNaturalH;
            var rawOffset = Mathf.Max((singleLineNaturalH - cssLineHeight) / 2f, 0f);
            var verticalOffset = Mathf.RoundToInt(rawOffset + 0.6f);

            var renderWidth = width + padding * 2;
            var renderHeight = renderContentH + padding * 2;
            var rtWidth = renderWidth * ss;
            var rtHeight = renderHeight * ss;

            var ctx = EnsureRenderContext();
            var layer = RenderLayer;
            ctx.CameraGo.layer = layer;
            ctx.CanvasGo.layer = layer;
            ctx.TextGo.layer = layer;

            ctx.Camera.backgroundColor = Color.black;
            ctx.Camera.cullingMask = 1 << layer;
            ctx.Camera.transform.position = new Vector3(0f, 0f, -1f);

            // Ensure RenderTexture matches needed size
            if (ctx.Camera.targetTexture == null ||
                ctx.Camera.targetTexture.width != rtWidth ||
                ctx.Camera.targetTexture.height != rtHeight)
            {
                if (ctx.Camera.targetTexture != null)
                {
                    ctx.Camera.targetTexture.Release();
                    Object.DestroyImmediate(ctx.Camera.targetTexture);
                }

                var rt = new RenderTexture(rtWidth, rtHeight, 24, RenderTextureFormat.ARGB32)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    antiAliasing = 1,
                    filterMode = FilterMode.Bilinear,
                };
                rt.Create();
                ctx.Camera.targetTexture = rt;
            }

            var canvasRect = ctx.Canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(rtWidth, rtHeight);

            ConfigureText(ctx.Tmp, rtWidth, rtHeight, text, fontSize * ss,
                align, font, whiteSpace, textOverflow, overflow, lineHeightMultiplier, padding * ss);

            ctx.Camera.aspect = (float)rtWidth / rtHeight;
            ctx.Camera.orthographicSize = rtHeight * 0.5f;

            Canvas.ForceUpdateCanvases();
            ctx.Tmp.ForceMeshUpdate(true, true);
            ctx.Camera.Render();

            // GPU downsample via Blit, then ReadPixels at target resolution for stable cache
            var tempRT = RenderTexture.GetTemporary(renderWidth, renderHeight, 0, RenderTextureFormat.ARGB32);
            tempRT.filterMode = FilterMode.Bilinear;
            Graphics.Blit(ctx.Camera.targetTexture, tempRT);

            var prev = RenderTexture.active;
            RenderTexture.active = tempRT;
            var tex = new Texture2D(renderWidth, renderHeight, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.ReadPixels(new Rect(0, 0, renderWidth, renderHeight), 0, 0);
            tex.Apply(false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(tempRT);

            return new TextCacheEntry
            {
                GPUTexture = tex,
                RenderContentH = renderContentH,
                VerticalOffset = verticalOffset,
            };
        }

        private static RenderContext EnsureRenderContext()
        {
            if (s_render != null && s_render.CameraGo != null && s_render.Tmp != null)
                return s_render;

            // Cleanup stale
            if (s_render != null)
            {
                if (s_render.TextGo != null) Object.DestroyImmediate(s_render.TextGo);
                if (s_render.CanvasGo != null) Object.DestroyImmediate(s_render.CanvasGo);
                if (s_render.CameraGo != null) Object.DestroyImmediate(s_render.CameraGo);
            }

            var layer = RenderLayer;

            var cameraGo = new GameObject("MornUI TMP Camera")
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = layer,
            };
            var camera = cameraGo.AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.orthographic = true;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 10f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.forceIntoRenderTexture = true;

            var canvasGo = new GameObject("MornUI TMP Canvas")
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = layer,
            };
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.pixelPerfect = false;
            canvas.planeDistance = 1f;
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.None;
            canvasGo.AddComponent<GraphicRaycaster>();

            var canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.anchorMin = new Vector2(0.5f, 0.5f);
            canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
            canvasRect.pivot = new Vector2(0.5f, 0.5f);
            canvasRect.anchoredPosition = Vector2.zero;

            var textGo = new GameObject("MornUI TMP Text")
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = layer,
            };
            textGo.transform.SetParent(canvasGo.transform, false);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();

            s_render = new RenderContext
            {
                CameraGo = cameraGo,
                Camera = camera,
                CanvasGo = canvasGo,
                Canvas = canvas,
                TextGo = textGo,
                Tmp = tmp,
            };
            return s_render;
        }

        public static float MeasureWidth(string text, TMP_FontAsset font, float fontSize)
        {
            if (string.IsNullOrEmpty(text) || font == null)
                return 0f;

            var key = new MeasureCacheKey
            {
                Text = text, FontId = font.GetInstanceID(), FontSize = fontSize,
                MaxWidth = 0f, WhiteSpace = MornUIWhiteSpace.Nowrap, LineHeight = -1f,
            };
            if (s_measureWidthCache.TryGetValue(key, out var cached))
                return cached;

            var context = EnsureMeasurementContext();
            ConfigureMeasurementText(context.Text, font, fontSize, MornUIWhiteSpace.Nowrap, -1f);
            var preferred = context.Text.GetPreferredValues(text, 0f, 0f);
            s_measureWidthCache[key] = preferred.x;
            return preferred.x;
        }

        public static float MeasureWidthFallback(string text, float fontSize)
        {
            if (string.IsNullOrEmpty(text))
                return 0f;
            return text.Length * fontSize * 0.6f;
        }

        public static float MeasureHeight(TMP_FontAsset font, float fontSize, float lineHeightMultiplier = -1f)
        {
            if (font == null)
                return Mathf.Ceil(fontSize * 1.2f);

            if (lineHeightMultiplier > 0f)
                return fontSize * lineHeightMultiplier;

            var key = new MeasureCacheKey
            {
                Text = "Mg", FontId = font.GetInstanceID(), FontSize = fontSize,
                MaxWidth = 0f, WhiteSpace = MornUIWhiteSpace.Nowrap, LineHeight = -1f,
            };
            if (s_measureHeightCache.TryGetValue(key, out var cached))
                return cached;

            var context = EnsureMeasurementContext();
            ConfigureMeasurementText(context.Text, font, fontSize, MornUIWhiteSpace.Nowrap, -1f);
            var preferred = context.Text.GetPreferredValues("Mg", 0f, 0f);
            s_measureHeightCache[key] = preferred.y;
            return preferred.y;
        }

        public static float MeasureMultiLineHeight(string text, TMP_FontAsset font, float fontSize,
            float maxWidth, MornUIWhiteSpace whiteSpace, float lineHeightMultiplier = -1f)
        {
            if (string.IsNullOrEmpty(text) || font == null)
                return 0f;

            var key = new MeasureCacheKey
            {
                Text = text, FontId = font.GetInstanceID(), FontSize = fontSize,
                MaxWidth = maxWidth, WhiteSpace = whiteSpace, LineHeight = lineHeightMultiplier,
            };
            if (s_measureMultiLineCache.TryGetValue(key, out var cached))
                return cached;

            var context = EnsureMeasurementContext();
            float result;

            if (lineHeightMultiplier > 0f)
            {
                ConfigureMeasurementText(context.Text, font, fontSize, whiteSpace, -1f);
                var naturalH = whiteSpace == MornUIWhiteSpace.Nowrap
                    ? context.Text.GetPreferredValues(text, 0f, 0f).y
                    : context.Text.GetPreferredValues(text, maxWidth, 0f).y;

                ConfigureMeasurementText(context.Text, font, fontSize, MornUIWhiteSpace.Nowrap, -1f);
                var singleNaturalH = context.Text.GetPreferredValues("Mg", 0f, 0f).y;

                var lineCount = Mathf.Max(1, Mathf.RoundToInt(naturalH / singleNaturalH));
                result = lineCount * fontSize * lineHeightMultiplier;
            }
            else
            {
                ConfigureMeasurementText(context.Text, font, fontSize, whiteSpace, lineHeightMultiplier);
                var preferred = whiteSpace == MornUIWhiteSpace.Nowrap
                    ? context.Text.GetPreferredValues(text, 0f, 0f)
                    : context.Text.GetPreferredValues(text, maxWidth, 0f);
                result = preferred.y;
            }

            s_measureMultiLineCache[key] = result;
            return result;
        }

        /// <summary>TMP natural height for the given text (used for render target sizing).</summary>
        private static float MeasureNaturalHeight(string text, TMP_FontAsset font, float fontSize,
            float maxWidth, MornUIWhiteSpace whiteSpace)
        {
            if (string.IsNullOrEmpty(text) || font == null)
                return 0f;

            // Reuse multiline cache with lineHeight=-1
            var key = new MeasureCacheKey
            {
                Text = text, FontId = font.GetInstanceID(), FontSize = fontSize,
                MaxWidth = maxWidth, WhiteSpace = whiteSpace, LineHeight = -2f, // sentinel to distinguish from MeasureMultiLineHeight
            };
            if (s_measureMultiLineCache.TryGetValue(key, out var cached))
                return cached;

            var context = EnsureMeasurementContext();
            ConfigureMeasurementText(context.Text, font, fontSize, whiteSpace, -1f);
            var preferred = whiteSpace == MornUIWhiteSpace.Nowrap
                ? context.Text.GetPreferredValues(text, 0f, 0f)
                : context.Text.GetPreferredValues(text, maxWidth, 0f);
            s_measureMultiLineCache[key] = preferred.y;
            return preferred.y;
        }

        public static void Cleanup()
        {
            if (s_measurement?.Root != null)
                Object.DestroyImmediate(s_measurement.Root);
            s_measurement = null;

            if (s_render != null)
            {
                if (s_render.Camera != null && s_render.Camera.targetTexture != null)
                {
                    s_render.Camera.targetTexture.Release();
                    Object.DestroyImmediate(s_render.Camera.targetTexture);
                }
                if (s_render.TextGo != null) Object.DestroyImmediate(s_render.TextGo);
                if (s_render.CanvasGo != null) Object.DestroyImmediate(s_render.CanvasGo);
                if (s_render.CameraGo != null) Object.DestroyImmediate(s_render.CameraGo);
                s_render = null;
            }

            foreach (var entry in s_textCache.Values)
            {
                if (entry.GPUTexture != null)
                    Object.DestroyImmediate(entry.GPUTexture);
            }

            s_textCache.Clear();
            s_measureWidthCache.Clear();
            s_measureHeightCache.Clear();
            s_measureMultiLineCache.Clear();
        }

        private static MeasurementContext EnsureMeasurementContext()
        {
            if (s_measurement != null && s_measurement.Root != null && s_measurement.Text != null)
                return s_measurement;

            var root = new GameObject("MornUI TMP Measure")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var text = root.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.richText = false;
            text.enableAutoSizing = false;
            text.parseCtrlCharacters = true;
            text.isOrthographic = true;

            s_measurement = new MeasurementContext
            {
                Root = root,
                Text = text,
            };
            return s_measurement;
        }

        private static void ConfigureText(TextMeshProUGUI tmp, int width, int height, string text, float fontSize,
            MornUITextAlign align, TMP_FontAsset font, MornUIWhiteSpace whiteSpace,
            MornUITextOverflow textOverflow, MornUIOverflow overflow, float lineHeightMultiplier,
            int margin)
        {
            ConfigureMeasurementText(tmp, font, fontSize, whiteSpace, lineHeightMultiplier);

            tmp.text = text;
            tmp.color = Color.white;
            tmp.alignment = ToTmpAlignment(align);
            tmp.overflowMode = ToOverflowMode(whiteSpace, textOverflow, overflow);
            tmp.extraPadding = false;
            tmp.margin = new Vector4(margin, margin, margin, margin);

            var rect = tmp.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        private static void ConfigureMeasurementText(TextMeshProUGUI tmp, TMP_FontAsset font, float fontSize,
            MornUIWhiteSpace whiteSpace, float lineHeightMultiplier)
        {
            tmp.font = font;
            tmp.fontSharedMaterial = font.material;
            tmp.fontSize = fontSize;
#pragma warning disable CS0618
            tmp.enableKerning = true;
#pragma warning restore CS0618
            tmp.enableWordWrapping = whiteSpace != MornUIWhiteSpace.Nowrap;
            tmp.textWrappingMode = whiteSpace == MornUIWhiteSpace.Nowrap ? TextWrappingModes.NoWrap : TextWrappingModes.Normal;
            tmp.wordSpacing = 0f;
            tmp.characterSpacing = 0f;
            tmp.paragraphSpacing = 0f;
            tmp.margin = Vector4.zero;
            tmp.overflowMode = TextOverflowModes.Overflow;

            // TMP applies lineSpacing as: m_lineSpacing * currentEmScale where currentEmScale = fontSize * 0.01
            // TMP natural line height = faceInfo.lineHeight / pointSize * fontSize
            // Target CSS line-height = fontSize * lineHeightMultiplier
            // Delta needed = fontSize * lineHeightMultiplier - faceInfo.lineHeight / pointSize * fontSize
            // Since delta = lineSpacing * fontSize * 0.01:
            //   lineSpacing = (lineHeightMultiplier - faceInfo.lineHeight / pointSize) * 100
            tmp.lineSpacing = lineHeightMultiplier > 0f
                ? (lineHeightMultiplier - font.faceInfo.lineHeight / (float)font.faceInfo.pointSize) * 100f
                : 0f;
        }

        private static TextAlignmentOptions ToTmpAlignment(MornUITextAlign align)
        {
            return align switch
            {
                MornUITextAlign.Center => TextAlignmentOptions.Top,
                MornUITextAlign.Right => TextAlignmentOptions.TopRight,
                _ => TextAlignmentOptions.TopLeft,
            };
        }

        private static TextOverflowModes ToOverflowMode(MornUIWhiteSpace whiteSpace,
            MornUITextOverflow textOverflow, MornUIOverflow overflow)
        {
            if (textOverflow == MornUITextOverflow.Ellipsis && overflow == MornUIOverflow.Hidden)
                return TextOverflowModes.Ellipsis;

            if (overflow == MornUIOverflow.Hidden)
                return TextOverflowModes.Truncate;

            return whiteSpace == MornUIWhiteSpace.Nowrap
                ? TextOverflowModes.Overflow
                : TextOverflowModes.Overflow;
        }

    }
}
