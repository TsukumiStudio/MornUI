using TMPro;
using UnityEngine;

namespace MornLib
{
    internal sealed class MornUIRenderer
    {
        public TMP_FontAsset DefaultFont { get; set; }

        private RenderTexture _rtA;
        private RenderTexture _rtB;
        private RenderTexture _currentRT;
        private RenderTexture _tempRT;
        private Material _rectMat;
        private Material _textMat;
        private Material _opacityMat;
        private int _texWidth;
        private int _texHeight;

        public Texture Render(MornUILayoutNode root, int width, int height)
        {
            _texWidth = width;
            _texHeight = height;
            EnsureResources(width, height);

            var g = MornUIGlobal.I;
            var cc = g != null ? g.ClearColor : new Color32(34, 34, 34, 255);
            var clearColor = new Color(cc.r / 255f, cc.g / 255f, cc.b / 255f, cc.a / 255f).linear;
            var filterMode = g != null ? g.FilterMode : FilterMode.Bilinear;

            var prev = RenderTexture.active;
            RenderTexture.active = _rtA;
            GL.Clear(true, true, clearColor);
            RenderTexture.active = prev;

            _currentRT = _rtA;
            _tempRT = _rtB;

            RenderNode(root, 0f, 0f);

            _currentRT.filterMode = filterMode;
            return _currentRT;
        }

        private void EnsureResources(int width, int height)
        {
            EnsureRT(ref _rtA, width, height);
            EnsureRT(ref _rtB, width, height);

            if (_rectMat == null)
            {
                var s = Shader.Find("Hidden/MornUI/Rect");
                if (s != null)
                    _rectMat = new Material(s) { hideFlags = HideFlags.HideAndDontSave };
            }

            if (_textMat == null)
            {
                var s = Shader.Find("Hidden/MornUI/Text");
                if (s != null)
                    _textMat = new Material(s) { hideFlags = HideFlags.HideAndDontSave };
            }

            if (_opacityMat == null)
            {
                var s = Shader.Find("Hidden/MornUI/Opacity");
                if (s != null)
                    _opacityMat = new Material(s) { hideFlags = HideFlags.HideAndDontSave };
            }
        }

        private static void EnsureRT(ref RenderTexture rt, int width, int height)
        {
            if (rt != null && rt.width == width && rt.height == height) return;
            if (rt != null)
            {
                rt.Release();
                Object.DestroyImmediate(rt);
            }

            // ARGBHalf: 16-bit float avoids banding on dark colors in linear space
            rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            rt.Create();
        }

        private void SwapRT()
        {
            (_currentRT, _tempRT) = (_tempRT, _currentRT);
        }

        private static int SnapSize(float pos, float size)
        {
            return Mathf.RoundToInt(pos + size) - Mathf.RoundToInt(pos);
        }

        private void RenderNode(MornUILayoutNode node, float parentGlobalX, float parentGlobalY)
        {
            var rect = node.LayoutRect;
            var style = node.ComputedStyle;

            var globalBorderBoxX = rect.GlobalBorderBoxX(parentGlobalX);
            var globalBorderBoxY = rect.GlobalBorderBoxY(parentGlobalY);
            var globalPaddingBoxX = rect.GlobalPaddingBoxX(parentGlobalX);
            var globalPaddingBoxY = rect.GlobalPaddingBoxY(parentGlobalY);

            // Apply transform (translate + scale from center)
            var tfScaleX = 1f;
            var tfScaleY = 1f;
            var transform = style.Transform;
            if (transform != null && !transform.IsIdentity)
            {
                var tx = transform.GetTranslateX();
                var ty = transform.GetTranslateY();
                var sx = transform.GetScaleX();
                var sy = transform.GetScaleY();

                if (!Mathf.Approximately(sx, 1f) || !Mathf.Approximately(sy, 1f))
                {
                    var centerX = globalBorderBoxX + rect.BorderBoxWidth * 0.5f;
                    var centerY = globalBorderBoxY + rect.BorderBoxHeight * 0.5f;
                    globalBorderBoxX = centerX + (globalBorderBoxX - centerX) * sx;
                    globalBorderBoxY = centerY + (globalBorderBoxY - centerY) * sy;
                    globalPaddingBoxX = centerX + (globalPaddingBoxX - centerX) * sx;
                    globalPaddingBoxY = centerY + (globalPaddingBoxY - centerY) * sy;
                    tfScaleX = sx;
                    tfScaleY = sy;
                }

                globalBorderBoxX += tx;
                globalBorderBoxY += ty;
                globalPaddingBoxX += tx;
                globalPaddingBoxY += ty;
            }

            var bbX = Mathf.RoundToInt(globalBorderBoxX);
            var bbY = Mathf.RoundToInt(globalBorderBoxY);
            var bbW = SnapSize(globalBorderBoxX, rect.BorderBoxWidth * tfScaleX);
            var bbH = SnapSize(globalBorderBoxY, rect.BorderBoxHeight * tfScaleY);

            // CSS opacity: save current state before rendering subtree
            RenderTexture savedRT = null;
            var hasOpacity = style.Opacity < 1f;
            if (hasOpacity)
            {
                savedRT = RenderTexture.GetTemporary(_texWidth, _texHeight, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                Graphics.Blit(_currentRT, savedRT);
            }

            // Background + Border via GPU shader
            var bgColor = style.BackgroundColor;
            var hasBorder = style.BorderTopWidth > 0 || style.BorderRightWidth > 0 ||
                            style.BorderBottomWidth > 0 || style.BorderLeftWidth > 0;

            if ((bgColor.a > 0 || hasBorder) && _rectMat != null)
            {
                var maxR = Mathf.Min(bbW, bbH) / 2f;

                _rectMat.SetVector("_Rect", new Vector4(bbX, bbY, bbW, bbH));
                _rectMat.SetVector("_BgColor", C32ToVec4(bgColor));
                _rectMat.SetVector("_Radii", new Vector4(
                    Mathf.Min(style.BorderTopLeftRadius, maxR),
                    Mathf.Min(style.BorderTopRightRadius, maxR),
                    Mathf.Min(style.BorderBottomRightRadius, maxR),
                    Mathf.Min(style.BorderBottomLeftRadius, maxR)));
                _rectMat.SetVector("_BorderWidths", new Vector4(
                    style.BorderTopWidth, style.BorderRightWidth,
                    style.BorderBottomWidth, style.BorderLeftWidth));
                _rectMat.SetVector("_BorderColorTop", C32ToVec4(style.BorderTopColor));
                _rectMat.SetVector("_BorderColorRight", C32ToVec4(style.BorderRightColor));
                _rectMat.SetVector("_BorderColorBottom", C32ToVec4(style.BorderBottomColor));
                _rectMat.SetVector("_BorderColorLeft", C32ToVec4(style.BorderLeftColor));
                _rectMat.SetVector("_TexSize", new Vector2(_texWidth, _texHeight));

                Graphics.Blit(_currentRT, _tempRT, _rectMat);
                SwapRT();
            }

            // Text
            var globalContentX = globalPaddingBoxX + style.PaddingLeft * tfScaleX;
            var globalContentY = globalPaddingBoxY + style.PaddingTop * tfScaleY;

            if (!string.IsNullOrEmpty(node.TextContent) && node.Children.Count == 0 && _textMat != null)
            {
                var contentX = Mathf.RoundToInt(globalContentX);
                var contentY = Mathf.RoundToInt(globalContentY);
                var contentW = Mathf.CeilToInt(rect.ContentWidth);
                var contentH = Mathf.CeilToInt(rect.ContentHeight);

                BlitText(contentX, contentY, contentW, contentH,
                    node.TextContent, style.FontSize, style.TextColor, style.TextAlign,
                    DefaultFont, style.WhiteSpace, style.TextOverflow, style.Overflow,
                    style.LineHeightMultiplier);
            }

            // Children
            foreach (var child in node.Children)
            {
                RenderNode(child, globalContentX, globalContentY);
            }

            // Opacity composite
            if (hasOpacity && savedRT != null && _opacityMat != null)
            {
                _opacityMat.SetTexture("_OverTex", _currentRT);
                _opacityMat.SetFloat("_Opacity", style.Opacity);
                _opacityMat.SetVector("_OpacityRect", new Vector4(bbX, bbY, bbW, bbH));
                _opacityMat.SetVector("_TexSize", new Vector2(_texWidth, _texHeight));

                Graphics.Blit(savedRT, _tempRT, _opacityMat);
                SwapRT();
                RenderTexture.ReleaseTemporary(savedRT);
            }
        }

        private void BlitText(int dstX, int dstY, int width, int height,
            string text, float fontSize, Color32 color, MornUITextAlign align,
            TMP_FontAsset font, MornUIWhiteSpace whiteSpace, MornUITextOverflow textOverflow,
            MornUIOverflow overflow, float lineHeightMultiplier)
        {
            if (!MornUITextPainter.TryGetTextGPUInfo(text, font, fontSize, width, height,
                    align, whiteSpace, textOverflow, overflow, lineHeightMultiplier, out var info))
                return;

            var renderContentH = info.RenderContentH;
            var verticalOffset = info.VerticalOffset;
            var padding = info.Padding;
            var srcWidth = info.SrcWidth;
            var srcHeight = info.SrcHeight;

            // Destination rect in layout coordinates (top-left origin, matching shader after Y flip)
            _textMat.SetTexture("_TextTex", info.Texture);
            _textMat.SetVector("_TextRect", new Vector4(dstX, dstY - verticalOffset, width, renderContentH));
            _textMat.SetVector("_TextSrcRect", new Vector4(
                (float)padding / srcWidth,
                (float)padding / srcHeight,
                (float)width / srcWidth,
                (float)renderContentH / srcHeight));
            _textMat.SetVector("_TextColor", C32ToVec4(color));
            _textMat.SetVector("_TexSize", new Vector2(_texWidth, _texHeight));

            var g = MornUIGlobal.I;
            _textMat.SetFloat("_TextGamma", g != null ? g.TextGamma : 0.7f);
            _textMat.SetFloat("_LumThreshold", 10f / 255f);

            Graphics.Blit(_currentRT, _tempRT, _textMat);
            SwapRT();
        }

        private static Vector4 C32ToVec4(Color32 c)
        {
            // Convert sRGB CSS colors to linear space for correct shader blending
            var linear = new Color(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f).linear;
            return new Vector4(linear.r, linear.g, linear.b, linear.a);
        }

        public void Cleanup()
        {
            if (_rtA != null)
            {
                _rtA.Release();
                Object.DestroyImmediate(_rtA);
                _rtA = null;
            }

            if (_rtB != null)
            {
                _rtB.Release();
                Object.DestroyImmediate(_rtB);
                _rtB = null;
            }

            if (_rectMat != null)
            {
                Object.DestroyImmediate(_rectMat);
                _rectMat = null;
            }

            if (_textMat != null)
            {
                Object.DestroyImmediate(_textMat);
                _textMat = null;
            }

            if (_opacityMat != null)
            {
                Object.DestroyImmediate(_opacityMat);
                _opacityMat = null;
            }

            _currentRT = null;
            _tempRT = null;
        }
    }
}
