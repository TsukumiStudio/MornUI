using System;
using System.Globalization;
using UnityEngine;

namespace MornLib
{
    internal readonly struct MornUICssValue : IEquatable<MornUICssValue>
    {
        public enum ValueUnit
        {
            None,
            Px,
            Percent,
            Auto,
            Color,
            Keyword,
        }

        public readonly ValueUnit Unit;
        public readonly float Number;
        public readonly Color32 ColorVal;

        private MornUICssValue(ValueUnit unit, float number, Color32 color)
        {
            Unit = unit;
            Number = number;
            ColorVal = color;
        }

        public static MornUICssValue Px(float value) => new(ValueUnit.Px, value, default);
        public static MornUICssValue Percent(float value) => new(ValueUnit.Percent, value, default);
        public static readonly MornUICssValue AutoValue = new(ValueUnit.Auto, 0, default);
        public static readonly MornUICssValue NoneValue = new(ValueUnit.None, 0, default);

        public static MornUICssValue FromColor(Color32 color) => new(ValueUnit.Color, 0, color);
        public static MornUICssValue Keyword(float encodedValue) => new(ValueUnit.Keyword, encodedValue, default);

        public float Resolve(float parentSize)
        {
            return Unit switch
            {
                ValueUnit.Px => Number,
                ValueUnit.Percent => parentSize * Number / 100f,
                _ => 0f,
            };
        }

        public static MornUICssValue Parse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return NoneValue;

            raw = raw.Trim();

            if (raw == "auto")
                return AutoValue;

            if (raw.StartsWith("#"))
                return FromColor(ParseHexColor(raw));

            if (raw.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
                return FromColor(ParseRgbColor(raw));

            if (TryParseNamedColor(raw, out var namedColor))
                return FromColor(namedColor);

            if (raw.EndsWith("px", StringComparison.OrdinalIgnoreCase))
            {
                if (float.TryParse(raw.AsSpan(0, raw.Length - 2), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var px))
                    return Px(px);
            }

            if (raw.EndsWith("%"))
            {
                if (float.TryParse(raw.AsSpan(0, raw.Length - 1), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var pct))
                    return Percent(pct);
            }

            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
                return Px(num);

            return NoneValue;
        }

        private static Color32 ParseHexColor(string hex)
        {
            hex = hex.TrimStart('#');
            byte r, g, b, a = 255;
            switch (hex.Length)
            {
                case 3:
                    r = (byte)(Convert.ToByte(hex.Substring(0, 1), 16) * 17);
                    g = (byte)(Convert.ToByte(hex.Substring(1, 1), 16) * 17);
                    b = (byte)(Convert.ToByte(hex.Substring(2, 1), 16) * 17);
                    break;
                case 6:
                    r = Convert.ToByte(hex.Substring(0, 2), 16);
                    g = Convert.ToByte(hex.Substring(2, 2), 16);
                    b = Convert.ToByte(hex.Substring(4, 2), 16);
                    break;
                case 8:
                    r = Convert.ToByte(hex.Substring(0, 2), 16);
                    g = Convert.ToByte(hex.Substring(2, 2), 16);
                    b = Convert.ToByte(hex.Substring(4, 2), 16);
                    a = Convert.ToByte(hex.Substring(6, 2), 16);
                    break;
                default:
                    return new Color32(0, 0, 0, 255);
            }

            return new Color32(r, g, b, a);
        }

        private static Color32 ParseRgbColor(string raw)
        {
            var start = raw.IndexOf('(');
            var end = raw.IndexOf(')');
            if (start < 0 || end < 0) return new Color32(0, 0, 0, 255);
            var inner = raw.Substring(start + 1, end - start - 1);
            var parts = inner.Split(',');
            if (parts.Length < 3) return new Color32(0, 0, 0, 255);
            byte.TryParse(parts[0].Trim(), out var r);
            byte.TryParse(parts[1].Trim(), out var g);
            byte.TryParse(parts[2].Trim(), out var b);
            byte a = 255;
            if (parts.Length >= 4)
            {
                if (float.TryParse(parts[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var af))
                    a = (byte)(Mathf.Clamp01(af) * 255);
            }

            return new Color32(r, g, b, a);
        }

        private static bool TryParseNamedColor(string name, out Color32 color)
        {
            color = name.ToLowerInvariant() switch
            {
                "transparent" => new Color32(0, 0, 0, 0),
                "black" => new Color32(0, 0, 0, 255),
                "white" => new Color32(255, 255, 255, 255),
                "red" => new Color32(255, 0, 0, 255),
                "green" => new Color32(0, 128, 0, 255),
                "blue" => new Color32(0, 0, 255, 255),
                "yellow" => new Color32(255, 255, 0, 255),
                "gray" or "grey" => new Color32(128, 128, 128, 255),
                _ => default,
            };
            return name.ToLowerInvariant() is "transparent" or "black" or "white" or "red"
                or "green" or "blue" or "yellow" or "gray" or "grey";
        }

        public bool Equals(MornUICssValue other) =>
            Unit == other.Unit && Math.Abs(Number - other.Number) < 0.001f &&
            ColorVal.r == other.ColorVal.r && ColorVal.g == other.ColorVal.g &&
            ColorVal.b == other.ColorVal.b && ColorVal.a == other.ColorVal.a;

        public override bool Equals(object obj) => obj is MornUICssValue other && Equals(other);
        public override int GetHashCode() => HashCode.Combine((int)Unit, Number, ColorVal);
    }
}
