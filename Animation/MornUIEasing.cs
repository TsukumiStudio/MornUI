using UnityEngine;

namespace MornLib
{
    internal enum MornUIEasingType
    {
        Linear,
        Ease,
        EaseIn,
        EaseOut,
        EaseInOut,
    }

    internal static class MornUIEasing
    {
        public static float Evaluate(MornUIEasingType type, float t)
        {
            t = Mathf.Clamp01(t);
            switch (type)
            {
                case MornUIEasingType.Linear:
                    return t;
                case MornUIEasingType.Ease:
                    // CSS ease = cubic-bezier(0.25, 0.1, 0.25, 1.0)
                    return CubicBezier(0.25f, 0.1f, 0.25f, 1.0f, t);
                case MornUIEasingType.EaseIn:
                    // cubic-bezier(0.42, 0, 1.0, 1.0)
                    return CubicBezier(0.42f, 0f, 1.0f, 1.0f, t);
                case MornUIEasingType.EaseOut:
                    // cubic-bezier(0, 0, 0.58, 1.0)
                    return CubicBezier(0f, 0f, 0.58f, 1.0f, t);
                case MornUIEasingType.EaseInOut:
                    // cubic-bezier(0.42, 0, 0.58, 1.0)
                    return CubicBezier(0.42f, 0f, 0.58f, 1.0f, t);
                default:
                    return t;
            }
        }

        public static MornUIEasingType Parse(string value)
        {
            switch (value?.Trim())
            {
                case "linear": return MornUIEasingType.Linear;
                case "ease": return MornUIEasingType.Ease;
                case "ease-in": return MornUIEasingType.EaseIn;
                case "ease-out": return MornUIEasingType.EaseOut;
                case "ease-in-out": return MornUIEasingType.EaseInOut;
                default: return MornUIEasingType.Ease;
            }
        }

        private static float CubicBezier(float x1, float y1, float x2, float y2, float t)
        {
            // Solve for parameter u where B_x(u) = t, then return B_y(u)
            // Newton's method
            var u = t;
            for (var i = 0; i < 8; i++)
            {
                var bx = BezierComponent(x1, x2, u);
                var diff = bx - t;
                if (Mathf.Abs(diff) < 1e-6f) break;
                var dx = BezierDerivative(x1, x2, u);
                if (Mathf.Abs(dx) < 1e-6f) break;
                u -= diff / dx;
            }

            u = Mathf.Clamp01(u);
            return BezierComponent(y1, y2, u);
        }

        private static float BezierComponent(float p1, float p2, float t)
        {
            // B(t) = 3(1-t)^2*t*p1 + 3(1-t)*t^2*p2 + t^3
            var mt = 1f - t;
            return 3f * mt * mt * t * p1 + 3f * mt * t * t * p2 + t * t * t;
        }

        private static float BezierDerivative(float p1, float p2, float t)
        {
            var mt = 1f - t;
            return 3f * mt * mt * p1 + 6f * mt * t * (p2 - p1) + 3f * t * t * (1f - p2);
        }
    }
}
