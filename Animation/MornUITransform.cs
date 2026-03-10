using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MornLib
{
    internal enum MornUITransformOpType
    {
        TranslateX,
        TranslateY,
        ScaleX,
        ScaleY,
        Rotate,
    }

    internal struct MornUITransformOp
    {
        public MornUITransformOpType Type;
        public float Value;
    }

    internal sealed class MornUITransform
    {
        public static readonly MornUITransform Identity = new(new List<MornUITransformOp>());

        public readonly List<MornUITransformOp> Operations;

        public MornUITransform(List<MornUITransformOp> operations)
        {
            Operations = operations;
        }

        public float GetTranslateX()
        {
            var v = 0f;
            foreach (var op in Operations)
                if (op.Type == MornUITransformOpType.TranslateX) v += op.Value;
            return v;
        }

        public float GetTranslateY()
        {
            var v = 0f;
            foreach (var op in Operations)
                if (op.Type == MornUITransformOpType.TranslateY) v += op.Value;
            return v;
        }

        public float GetScaleX()
        {
            var v = 1f;
            foreach (var op in Operations)
                if (op.Type == MornUITransformOpType.ScaleX) v *= op.Value;
            return v;
        }

        public float GetScaleY()
        {
            var v = 1f;
            foreach (var op in Operations)
                if (op.Type == MornUITransformOpType.ScaleY) v *= op.Value;
            return v;
        }

        public float GetRotation()
        {
            var v = 0f;
            foreach (var op in Operations)
                if (op.Type == MornUITransformOpType.Rotate) v += op.Value;
            return v;
        }

        public bool IsIdentity =>
            Mathf.Approximately(GetTranslateX(), 0f) &&
            Mathf.Approximately(GetTranslateY(), 0f) &&
            Mathf.Approximately(GetScaleX(), 1f) &&
            Mathf.Approximately(GetScaleY(), 1f) &&
            Mathf.Approximately(GetRotation(), 0f);

        public static MornUITransform Parse(string value)
        {
            if (string.IsNullOrEmpty(value) || value == "none")
                return Identity;

            var ops = new List<MornUITransformOp>();
            var i = 0;
            while (i < value.Length)
            {
                // Skip whitespace
                while (i < value.Length && char.IsWhiteSpace(value[i])) i++;
                if (i >= value.Length) break;

                // Read function name
                var nameStart = i;
                while (i < value.Length && value[i] != '(') i++;
                if (i >= value.Length) break;
                var name = value.Substring(nameStart, i - nameStart).Trim();
                i++; // skip '('

                // Read args until ')'
                var argStart = i;
                while (i < value.Length && value[i] != ')') i++;
                if (i >= value.Length) break;
                var argsStr = value.Substring(argStart, i - argStart).Trim();
                i++; // skip ')'

                var args = argsStr.Split(',');

                switch (name)
                {
                    case "translateX":
                        ops.Add(new MornUITransformOp
                        {
                            Type = MornUITransformOpType.TranslateX,
                            Value = ParsePx(args[0]),
                        });
                        break;
                    case "translateY":
                        ops.Add(new MornUITransformOp
                        {
                            Type = MornUITransformOpType.TranslateY,
                            Value = ParsePx(args[0]),
                        });
                        break;
                    case "translate":
                        ops.Add(new MornUITransformOp
                        {
                            Type = MornUITransformOpType.TranslateX,
                            Value = ParsePx(args[0]),
                        });
                        if (args.Length > 1)
                            ops.Add(new MornUITransformOp
                            {
                                Type = MornUITransformOpType.TranslateY,
                                Value = ParsePx(args[1]),
                            });
                        break;
                    case "scale":
                        var sx = ParseFloat(args[0]);
                        var sy = args.Length > 1 ? ParseFloat(args[1]) : sx;
                        ops.Add(new MornUITransformOp { Type = MornUITransformOpType.ScaleX, Value = sx });
                        ops.Add(new MornUITransformOp { Type = MornUITransformOpType.ScaleY, Value = sy });
                        break;
                    case "scaleX":
                        ops.Add(new MornUITransformOp { Type = MornUITransformOpType.ScaleX, Value = ParseFloat(args[0]) });
                        break;
                    case "scaleY":
                        ops.Add(new MornUITransformOp { Type = MornUITransformOpType.ScaleY, Value = ParseFloat(args[0]) });
                        break;
                    case "rotate":
                        ops.Add(new MornUITransformOp { Type = MornUITransformOpType.Rotate, Value = ParseDeg(args[0]) });
                        break;
                }
            }

            return new MornUITransform(ops);
        }

        public static MornUITransform Lerp(MornUITransform a, MornUITransform b, float t)
        {
            var tx = Mathf.Lerp(a.GetTranslateX(), b.GetTranslateX(), t);
            var ty = Mathf.Lerp(a.GetTranslateY(), b.GetTranslateY(), t);
            var sx = Mathf.Lerp(a.GetScaleX(), b.GetScaleX(), t);
            var sy = Mathf.Lerp(a.GetScaleY(), b.GetScaleY(), t);
            var rot = Mathf.Lerp(a.GetRotation(), b.GetRotation(), t);

            var ops = new List<MornUITransformOp>();
            if (!Mathf.Approximately(tx, 0f))
                ops.Add(new MornUITransformOp { Type = MornUITransformOpType.TranslateX, Value = tx });
            if (!Mathf.Approximately(ty, 0f))
                ops.Add(new MornUITransformOp { Type = MornUITransformOpType.TranslateY, Value = ty });
            if (!Mathf.Approximately(sx, 1f))
                ops.Add(new MornUITransformOp { Type = MornUITransformOpType.ScaleX, Value = sx });
            if (!Mathf.Approximately(sy, 1f))
                ops.Add(new MornUITransformOp { Type = MornUITransformOpType.ScaleY, Value = sy });
            if (!Mathf.Approximately(rot, 0f))
                ops.Add(new MornUITransformOp { Type = MornUITransformOpType.Rotate, Value = rot });

            return new MornUITransform(ops);
        }

        private static float ParsePx(string s)
        {
            s = s.Trim().Replace("px", "");
            float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v);
            return v;
        }

        private static float ParseFloat(string s)
        {
            float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v);
            return v;
        }

        private static float ParseDeg(string s)
        {
            s = s.Trim().Replace("deg", "");
            float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v);
            return v;
        }
    }
}
