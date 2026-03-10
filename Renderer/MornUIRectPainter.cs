using UnityEngine;

namespace MornLib
{
    internal static class MornUIRectPainter
    {
        public static void FillRect(Color32[] pixels, int textureWidth, int textureHeight,
            int x, int y, int width, int height, Color32 color)
        {
            FillRoundedRect(pixels, textureWidth, textureHeight, x, y, width, height, color, 0, 0, 0, 0);
        }

        public static void FillRoundedRect(Color32[] pixels, int textureWidth, int textureHeight,
            int x, int y, int width, int height, Color32 color,
            float radiusTL, float radiusTR, float radiusBR, float radiusBL)
        {
            if (color.a == 0 || width <= 0 || height <= 0) return;

            var maxRadius = Mathf.Min(width, height) / 2f;
            radiusTL = Mathf.Min(radiusTL, maxRadius);
            radiusTR = Mathf.Min(radiusTR, maxRadius);
            radiusBR = Mathf.Min(radiusBR, maxRadius);
            radiusBL = Mathf.Min(radiusBL, maxRadius);
            var hasRadius = radiusTL > 0 || radiusTR > 0 || radiusBR > 0 || radiusBL > 0;

            var x0 = Mathf.Max(x, 0);
            var y0 = Mathf.Max(y, 0);
            var x1 = Mathf.Min(x + width, textureWidth);
            var y1 = Mathf.Min(y + height, textureHeight);

            for (var py = y0; py < y1; py++)
            {
                var flippedY = textureHeight - 1 - py;
                var rowStart = flippedY * textureWidth;
                var ly = py - y;

                for (var px = x0; px < x1; px++)
                {
                    var coverage = 1f;
                    if (hasRadius)
                    {
                        var lx = px - x;
                        coverage = RoundedRectCoverage(lx, ly, width, height, radiusTL, radiusTR, radiusBR, radiusBL);
                        if (coverage <= 0f) continue;
                    }

                    var idx = rowStart + px;
                    if (coverage >= 1f && color.a == 255)
                        pixels[idx] = color;
                    else
                    {
                        var a = (byte)(color.a * coverage);
                        pixels[idx] = AlphaBlend(pixels[idx], new Color32(color.r, color.g, color.b, a));
                    }
                }
            }
        }

        public static void DrawBorder(Color32[] pixels, int textureWidth, int textureHeight,
            int x, int y, int width, int height,
            float topW, float rightW, float bottomW, float leftW,
            Color32 topColor, Color32 rightColor, Color32 bottomColor, Color32 leftColor,
            float radiusTL, float radiusTR, float radiusBR, float radiusBL)
        {
            if (width <= 0 || height <= 0) return;

            var maxRadius = Mathf.Min(width, height) / 2f;
            radiusTL = Mathf.Min(radiusTL, maxRadius);
            radiusTR = Mathf.Min(radiusTR, maxRadius);
            radiusBR = Mathf.Min(radiusBR, maxRadius);
            radiusBL = Mathf.Min(radiusBL, maxRadius);

            var x0 = Mathf.Max(x, 0);
            var y0 = Mathf.Max(y, 0);
            var x1 = Mathf.Min(x + width, textureWidth);
            var y1 = Mathf.Min(y + height, textureHeight);

            var topWi = Mathf.RoundToInt(topW);
            var rightWi = Mathf.RoundToInt(rightW);
            var bottomWi = Mathf.RoundToInt(bottomW);
            var leftWi = Mathf.RoundToInt(leftW);

            for (var py = y0; py < y1; py++)
            {
                var flippedY = textureHeight - 1 - py;
                var rowStart = flippedY * textureWidth;
                var ly = py - y;

                for (var px = x0; px < x1; px++)
                {
                    var lx = px - x;

                    // Outer coverage (with AA)
                    var outerCoverage = RoundedRectCoverage(lx, ly, width, height,
                        radiusTL, radiusTR, radiusBR, radiusBL);
                    if (outerCoverage <= 0f) continue;

                    // Inner coverage
                    var innerX = lx - leftWi;
                    var innerY = ly - topWi;
                    var innerW = width - leftWi - rightWi;
                    var innerH = height - topWi - bottomWi;

                    var innerCoverage = 0f;
                    if (innerW > 0 && innerH > 0)
                    {
                        var innerRadTL = Mathf.Max(radiusTL - Mathf.Max(topW, leftW), 0);
                        var innerRadTR = Mathf.Max(radiusTR - Mathf.Max(topW, rightW), 0);
                        var innerRadBR = Mathf.Max(radiusBR - Mathf.Max(bottomW, rightW), 0);
                        var innerRadBL = Mathf.Max(radiusBL - Mathf.Max(bottomW, leftW), 0);

                        innerCoverage = RoundedRectCoverage(innerX, innerY, innerW, innerH,
                            innerRadTL, innerRadTR, innerRadBR, innerRadBL);
                    }

                    // Border coverage = outer minus inner
                    var borderCoverage = outerCoverage - innerCoverage;
                    if (borderCoverage <= 0f) continue;

                    // Determine which border edge this pixel belongs to
                    var isTop = ly < topWi;
                    var isBottom = ly >= height - bottomWi;
                    var isLeft = lx < leftWi;
                    var isRight = lx >= width - rightWi;

                    Color32 borderColor;
                    if (isTop && !isLeft && !isRight) borderColor = topColor;
                    else if (isBottom && !isLeft && !isRight) borderColor = bottomColor;
                    else if (isLeft) borderColor = leftColor;
                    else if (isRight) borderColor = rightColor;
                    else if (isTop) borderColor = topColor;
                    else borderColor = bottomColor;

                    if (borderColor.a == 0) continue;

                    var idx = rowStart + px;
                    var a = (byte)(borderColor.a * borderCoverage);
                    pixels[idx] = AlphaBlend(pixels[idx], new Color32(borderColor.r, borderColor.g, borderColor.b, a));
                }
            }
        }

        /// <summary>
        /// Returns 0.0 (outside) to 1.0 (fully inside) with smooth anti-aliasing at rounded corners.
        /// </summary>
        private static float RoundedRectCoverage(float lx, float ly, float w, float h,
            float rTL, float rTR, float rBR, float rBL)
        {
            if (lx < -0.5f || lx >= w + 0.5f || ly < -0.5f || ly >= h + 0.5f)
                return 0f;

            // Pixel center offset (+0.5 for center of pixel)
            var cx = lx + 0.5f;
            var cy = ly + 0.5f;

            // Check each corner — compute signed distance to circle edge
            float dist;

            // Top-left corner
            if (cx < rTL && cy < rTL)
            {
                var dx = cx - rTL;
                var dy = cy - rTL;
                dist = rTL - Mathf.Sqrt(dx * dx + dy * dy);
            }
            // Top-right corner
            else if (cx > w - rTR && cy < rTR)
            {
                var dx = cx - (w - rTR);
                var dy = cy - rTR;
                dist = rTR - Mathf.Sqrt(dx * dx + dy * dy);
            }
            // Bottom-right corner
            else if (cx > w - rBR && cy > h - rBR)
            {
                var dx = cx - (w - rBR);
                var dy = cy - (h - rBR);
                dist = rBR - Mathf.Sqrt(dx * dx + dy * dy);
            }
            // Bottom-left corner
            else if (cx < rBL && cy > h - rBL)
            {
                var dx = cx - rBL;
                var dy = cy - (h - rBL);
                dist = rBL - Mathf.Sqrt(dx * dx + dy * dy);
            }
            else
            {
                // Not in a corner — check rect edges
                dist = Mathf.Min(Mathf.Min(cx, w - cx), Mathf.Min(cy, h - cy));
            }

            // Smooth step: 1px transition band
            return Mathf.Clamp01(dist + 0.5f);
        }

        public static Color32 AlphaBlend(Color32 dst, Color32 src)
        {
            var sa = src.a / 255f;
            var da = dst.a / 255f;
            var outA = sa + da * (1f - sa);
            if (outA < 0.001f) return new Color32(0, 0, 0, 0);
            var r = (byte)((src.r * sa + dst.r * da * (1f - sa)) / outA);
            var g = (byte)((src.g * sa + dst.g * da * (1f - sa)) / outA);
            var b = (byte)((src.b * sa + dst.b * da * (1f - sa)) / outA);
            return new Color32(r, g, b, (byte)(outA * 255));
        }
    }
}
