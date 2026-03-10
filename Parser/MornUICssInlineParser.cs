using System.Collections.Generic;

namespace MornLib
{
    internal static class MornUICssInlineParser
    {
        private static readonly HashSet<MornUICssPropertyId> RawProperties = new()
        {
            MornUICssPropertyId.Transform,
            MornUICssPropertyId.AnimationName,
            MornUICssPropertyId.AnimationDuration,
            MornUICssPropertyId.AnimationDelay,
            MornUICssPropertyId.AnimationTimingFunction,
            MornUICssPropertyId.AnimationIterationCount,
            MornUICssPropertyId.AnimationDirection,
            MornUICssPropertyId.AnimationFillMode,
            MornUICssPropertyId.TransitionProperty,
            MornUICssPropertyId.TransitionDuration,
            MornUICssPropertyId.TransitionDelay,
            MornUICssPropertyId.TransitionTimingFunction,
            MornUICssPropertyId.Cursor,
        };

        public static Dictionary<MornUICssPropertyId, MornUICssValue> Parse(string styleAttr)
        {
            return Parse(styleAttr, out _);
        }

        public static Dictionary<MornUICssPropertyId, MornUICssValue> Parse(string styleAttr,
            out Dictionary<MornUICssPropertyId, string> rawDeclarations)
        {
            var result = new Dictionary<MornUICssPropertyId, MornUICssValue>();
            rawDeclarations = new Dictionary<MornUICssPropertyId, string>();
            if (string.IsNullOrWhiteSpace(styleAttr))
                return result;

            var declarations = styleAttr.Split(';');
            foreach (var decl in declarations)
            {
                var trimmed = decl.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                var colonIdx = trimmed.IndexOf(':');
                if (colonIdx < 0)
                    continue;

                var propName = trimmed.Substring(0, colonIdx).Trim();
                var valueStr = trimmed.Substring(colonIdx + 1).Trim();

                if (TryExpandShorthand(propName, valueStr, result, rawDeclarations))
                    continue;

                if (MornUICssPropertyMap.TryGetPropertyId(propName, out var propId))
                {
                    if (RawProperties.Contains(propId))
                        rawDeclarations[propId] = valueStr;
                    else
                        result[propId] = MornUICssPropertyMap.ParseValue(propId, valueStr);
                }
            }

            return result;
        }

        private static bool TryExpandShorthand(string propName, string valueStr,
            Dictionary<MornUICssPropertyId, MornUICssValue> result,
            Dictionary<MornUICssPropertyId, string> rawDeclarations)
        {
            switch (propName)
            {
                case "margin":
                    ExpandBoxShorthand(valueStr,
                        MornUICssPropertyId.MarginTop, MornUICssPropertyId.MarginRight,
                        MornUICssPropertyId.MarginBottom, MornUICssPropertyId.MarginLeft, result);
                    return true;
                case "padding":
                    ExpandBoxShorthand(valueStr,
                        MornUICssPropertyId.PaddingTop, MornUICssPropertyId.PaddingRight,
                        MornUICssPropertyId.PaddingBottom, MornUICssPropertyId.PaddingLeft, result);
                    return true;
                case "border":
                    ExpandBorderShorthand(valueStr, result);
                    return true;
                case "border-width":
                    ExpandBoxShorthand(valueStr,
                        MornUICssPropertyId.BorderTopWidth, MornUICssPropertyId.BorderRightWidth,
                        MornUICssPropertyId.BorderBottomWidth, MornUICssPropertyId.BorderLeftWidth, result);
                    return true;
                case "border-color":
                    ExpandBoxShorthand(valueStr,
                        MornUICssPropertyId.BorderTopColor, MornUICssPropertyId.BorderRightColor,
                        MornUICssPropertyId.BorderBottomColor, MornUICssPropertyId.BorderLeftColor, result);
                    return true;
                default:
                    return false;
            }
        }

        private static void ExpandBoxShorthand(string valueStr,
            MornUICssPropertyId top, MornUICssPropertyId right,
            MornUICssPropertyId bottom, MornUICssPropertyId left,
            Dictionary<MornUICssPropertyId, MornUICssValue> result)
        {
            var parts = valueStr.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            MornUICssValue t, r, b, l;
            switch (parts.Length)
            {
                case 1:
                    t = r = b = l = MornUICssValue.Parse(parts[0]);
                    break;
                case 2:
                    t = b = MornUICssValue.Parse(parts[0]);
                    r = l = MornUICssValue.Parse(parts[1]);
                    break;
                case 3:
                    t = MornUICssValue.Parse(parts[0]);
                    r = l = MornUICssValue.Parse(parts[1]);
                    b = MornUICssValue.Parse(parts[2]);
                    break;
                default:
                    t = MornUICssValue.Parse(parts[0]);
                    r = MornUICssValue.Parse(parts[1]);
                    b = MornUICssValue.Parse(parts[2]);
                    l = MornUICssValue.Parse(parts[3]);
                    break;
            }

            result[top] = t;
            result[right] = r;
            result[bottom] = b;
            result[left] = l;
        }

        private static void ExpandBorderShorthand(string valueStr,
            Dictionary<MornUICssPropertyId, MornUICssValue> result)
        {
            var parts = valueStr.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            MornUICssValue widthVal = MornUICssValue.NoneValue;
            MornUICssValue colorVal = MornUICssValue.NoneValue;

            foreach (var part in parts)
            {
                if (part is "solid" or "dashed" or "dotted" or "double" or "none")
                    continue;
                var parsed = MornUICssValue.Parse(part);
                if (parsed.Unit == MornUICssValue.ValueUnit.Color)
                    colorVal = parsed;
                else if (parsed.Unit is MornUICssValue.ValueUnit.Px or MornUICssValue.ValueUnit.Percent)
                    widthVal = parsed;
            }

            if (widthVal.Unit != MornUICssValue.ValueUnit.None)
            {
                result[MornUICssPropertyId.BorderTopWidth] = widthVal;
                result[MornUICssPropertyId.BorderRightWidth] = widthVal;
                result[MornUICssPropertyId.BorderBottomWidth] = widthVal;
                result[MornUICssPropertyId.BorderLeftWidth] = widthVal;
            }

            if (colorVal.Unit != MornUICssValue.ValueUnit.None)
            {
                result[MornUICssPropertyId.BorderTopColor] = colorVal;
                result[MornUICssPropertyId.BorderRightColor] = colorVal;
                result[MornUICssPropertyId.BorderBottomColor] = colorVal;
                result[MornUICssPropertyId.BorderLeftColor] = colorVal;
            }
        }
    }
}
