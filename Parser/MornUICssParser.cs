using System.Collections.Generic;

namespace MornLib
{
    internal sealed class MornUICssParser
    {
        public List<MornUIKeyframeRule> KeyframeRules { get; private set; } = new();

        public List<MornUICssRule> Parse(string css)
        {
            var tokenizer = new MornUICssTokenizer(css);
            var tokens = tokenizer.Tokenize();
            var rules = new List<MornUICssRule>();
            KeyframeRules = new List<MornUIKeyframeRule>();
            var pos = 0;

            while (pos < tokens.Count && tokens[pos].Type != MornUICssTokenType.Eof)
            {
                SkipWhitespace(tokens, ref pos);
                if (pos >= tokens.Count || tokens[pos].Type == MornUICssTokenType.Eof)
                    break;

                // Check for @keyframes
                if (tokens[pos].Type == MornUICssTokenType.AtKeyword && tokens[pos].Value == "keyframes")
                {
                    pos++;
                    SkipWhitespace(tokens, ref pos);
                    var keyframeRule = ParseKeyframes(tokens, ref pos);
                    if (keyframeRule != null)
                        KeyframeRules.Add(keyframeRule);
                    continue;
                }

                var selector = MornUICssSelector.Parse(tokens, ref pos);
                SkipWhitespace(tokens, ref pos);

                if (pos < tokens.Count && tokens[pos].Type == MornUICssTokenType.LBrace)
                {
                    pos++;
                    var (declarations, rawDeclarations) = ParseDeclarationsWithRaw(tokens, ref pos);
                    if (pos < tokens.Count && tokens[pos].Type == MornUICssTokenType.RBrace)
                        pos++;
                    rules.Add(new MornUICssRule(selector, declarations, rawDeclarations));
                }
            }

            return rules;
        }

        private static MornUIKeyframeRule ParseKeyframes(List<MornUICssToken> tokens, ref int pos)
        {
            if (pos >= tokens.Count) return null;
            var name = tokens[pos].Value;
            pos++;
            SkipWhitespace(tokens, ref pos);

            if (pos >= tokens.Count || tokens[pos].Type != MornUICssTokenType.LBrace) return null;
            pos++; // skip {

            var rule = new MornUIKeyframeRule { Name = name };

            while (pos < tokens.Count && tokens[pos].Type != MornUICssTokenType.RBrace &&
                   tokens[pos].Type != MornUICssTokenType.Eof)
            {
                SkipWhitespace(tokens, ref pos);
                if (pos >= tokens.Count || tokens[pos].Type == MornUICssTokenType.RBrace) break;

                // Read stop selector: "from", "to", or percentage like "50%"
                var stopStr = "";
                while (pos < tokens.Count && tokens[pos].Type != MornUICssTokenType.LBrace &&
                       tokens[pos].Type != MornUICssTokenType.RBrace &&
                       tokens[pos].Type != MornUICssTokenType.Eof)
                {
                    stopStr += tokens[pos].Value;
                    pos++;
                }

                stopStr = stopStr.Trim();
                float percentage;
                if (stopStr == "from") percentage = 0f;
                else if (stopStr == "to") percentage = 1f;
                else
                {
                    stopStr = stopStr.Replace("%", "");
                    float.TryParse(stopStr, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out percentage);
                    percentage /= 100f;
                }

                if (pos < tokens.Count && tokens[pos].Type == MornUICssTokenType.LBrace)
                {
                    pos++;
                    var props = ParseRawDeclarations(tokens, ref pos);
                    if (pos < tokens.Count && tokens[pos].Type == MornUICssTokenType.RBrace)
                        pos++;

                    rule.Stops.Add(new MornUIKeyframeStop
                    {
                        Percentage = percentage,
                        Properties = props,
                    });
                }
            }

            if (pos < tokens.Count && tokens[pos].Type == MornUICssTokenType.RBrace)
                pos++;

            // Sort stops by percentage
            rule.Stops.Sort((a, b) => a.Percentage.CompareTo(b.Percentage));
            return rule;
        }

        private static Dictionary<string, string> ParseRawDeclarations(List<MornUICssToken> tokens, ref int pos)
        {
            var result = new Dictionary<string, string>();

            while (pos < tokens.Count && tokens[pos].Type != MornUICssTokenType.RBrace &&
                   tokens[pos].Type != MornUICssTokenType.Eof)
            {
                SkipWhitespace(tokens, ref pos);
                if (pos >= tokens.Count || tokens[pos].Type == MornUICssTokenType.RBrace) break;

                if (tokens[pos].Type == MornUICssTokenType.Ident)
                {
                    var propName = tokens[pos].Value;
                    pos++;
                    SkipWhitespace(tokens, ref pos);

                    if (pos < tokens.Count && tokens[pos].Type == MornUICssTokenType.Colon)
                    {
                        pos++;
                        SkipWhitespace(tokens, ref pos);
                        var valueStr = ReadValueUntilSemicolonOrBrace(tokens, ref pos);
                        result[propName] = valueStr;
                        if (pos < tokens.Count && tokens[pos].Type == MornUICssTokenType.Semicolon)
                            pos++;
                    }
                }
                else
                {
                    pos++;
                }
            }

            return result;
        }

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

        private static (Dictionary<MornUICssPropertyId, MornUICssValue>, Dictionary<MornUICssPropertyId, string>)
            ParseDeclarationsWithRaw(List<MornUICssToken> tokens, ref int pos)
        {
            var declarations = new Dictionary<MornUICssPropertyId, MornUICssValue>();
            var rawDeclarations = new Dictionary<MornUICssPropertyId, string>();

            while (pos < tokens.Count && tokens[pos].Type != MornUICssTokenType.RBrace &&
                   tokens[pos].Type != MornUICssTokenType.Eof)
            {
                SkipWhitespace(tokens, ref pos);
                if (pos >= tokens.Count || tokens[pos].Type == MornUICssTokenType.RBrace)
                    break;

                if (tokens[pos].Type == MornUICssTokenType.Ident)
                {
                    var propName = tokens[pos].Value;
                    pos++;
                    SkipWhitespace(tokens, ref pos);

                    if (pos < tokens.Count && tokens[pos].Type == MornUICssTokenType.Colon)
                    {
                        pos++;
                        SkipWhitespace(tokens, ref pos);
                        var valueStr = ReadValueUntilSemicolonOrBrace(tokens, ref pos);

                        if (TryExpandShorthand(propName, valueStr, declarations, rawDeclarations))
                        {
                            // Shorthand handled
                        }
                        else if (MornUICssPropertyMap.TryGetPropertyId(propName, out var propId))
                        {
                            if (RawProperties.Contains(propId))
                                rawDeclarations[propId] = valueStr;
                            else
                                declarations[propId] = MornUICssPropertyMap.ParseValue(propId, valueStr);
                        }

                        if (pos < tokens.Count && tokens[pos].Type == MornUICssTokenType.Semicolon)
                            pos++;
                    }
                }
                else
                {
                    pos++;
                }
            }

            return (declarations, rawDeclarations);
        }

        private static string ReadValueUntilSemicolonOrBrace(List<MornUICssToken> tokens, ref int pos)
        {
            var parts = new List<string>();
            while (pos < tokens.Count &&
                   tokens[pos].Type != MornUICssTokenType.Semicolon &&
                   tokens[pos].Type != MornUICssTokenType.RBrace &&
                   tokens[pos].Type != MornUICssTokenType.Eof)
            {
                var t = tokens[pos];
                // Hash token stores value without '#', restore it
                if (t.Type == MornUICssTokenType.Hash)
                    parts.Add("#" + t.Value);
                else
                    parts.Add(t.Value);
                pos++;
            }

            return string.Join("", parts).Trim();
        }

        private static bool TryExpandShorthand(string propName, string valueStr,
            Dictionary<MornUICssPropertyId, MornUICssValue> declarations,
            Dictionary<MornUICssPropertyId, string> rawDeclarations = null)
        {
            switch (propName)
            {
                case "margin":
                    ExpandBoxShorthand(valueStr,
                        MornUICssPropertyId.MarginTop, MornUICssPropertyId.MarginRight,
                        MornUICssPropertyId.MarginBottom, MornUICssPropertyId.MarginLeft, declarations);
                    return true;
                case "padding":
                    ExpandBoxShorthand(valueStr,
                        MornUICssPropertyId.PaddingTop, MornUICssPropertyId.PaddingRight,
                        MornUICssPropertyId.PaddingBottom, MornUICssPropertyId.PaddingLeft, declarations);
                    return true;
                case "border":
                    ExpandBorderShorthand(valueStr, declarations);
                    return true;
                case "border-width":
                    ExpandBoxShorthand(valueStr,
                        MornUICssPropertyId.BorderTopWidth, MornUICssPropertyId.BorderRightWidth,
                        MornUICssPropertyId.BorderBottomWidth, MornUICssPropertyId.BorderLeftWidth, declarations);
                    return true;
                case "border-color":
                    ExpandBoxShorthand(valueStr,
                        MornUICssPropertyId.BorderTopColor, MornUICssPropertyId.BorderRightColor,
                        MornUICssPropertyId.BorderBottomColor, MornUICssPropertyId.BorderLeftColor, declarations);
                    return true;
                case "animation":
                    ExpandAnimationShorthand(valueStr, declarations, rawDeclarations);
                    return true;
                case "transition":
                    ExpandTransitionShorthand(valueStr, declarations, rawDeclarations);
                    return true;
                default:
                    return false;
            }
        }

        private static void ExpandBoxShorthand(string valueStr,
            MornUICssPropertyId top, MornUICssPropertyId right,
            MornUICssPropertyId bottom, MornUICssPropertyId left,
            Dictionary<MornUICssPropertyId, MornUICssValue> declarations)
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

            declarations[top] = t;
            declarations[right] = r;
            declarations[bottom] = b;
            declarations[left] = l;
        }

        private static void ExpandBorderShorthand(string valueStr,
            Dictionary<MornUICssPropertyId, MornUICssValue> declarations)
        {
            // border: <width> <style> <color>
            var parts = valueStr.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            MornUICssValue widthVal = MornUICssValue.NoneValue;
            MornUICssValue colorVal = MornUICssValue.NoneValue;

            foreach (var part in parts)
            {
                if (part is "solid" or "dashed" or "dotted" or "double" or "none")
                    continue; // skip style (only solid is rendered)
                var parsed = MornUICssValue.Parse(part);
                if (parsed.Unit == MornUICssValue.ValueUnit.Color)
                    colorVal = parsed;
                else if (parsed.Unit is MornUICssValue.ValueUnit.Px or MornUICssValue.ValueUnit.Percent)
                    widthVal = parsed;
            }

            if (widthVal.Unit != MornUICssValue.ValueUnit.None)
            {
                declarations[MornUICssPropertyId.BorderTopWidth] = widthVal;
                declarations[MornUICssPropertyId.BorderRightWidth] = widthVal;
                declarations[MornUICssPropertyId.BorderBottomWidth] = widthVal;
                declarations[MornUICssPropertyId.BorderLeftWidth] = widthVal;
            }

            if (colorVal.Unit != MornUICssValue.ValueUnit.None)
            {
                declarations[MornUICssPropertyId.BorderTopColor] = colorVal;
                declarations[MornUICssPropertyId.BorderRightColor] = colorVal;
                declarations[MornUICssPropertyId.BorderBottomColor] = colorVal;
                declarations[MornUICssPropertyId.BorderLeftColor] = colorVal;
            }
        }

        // animation: name duration timing-function delay iteration-count direction fill-mode
        // e.g. "fadeSlideIn 0.3s ease forwards"
        private static void ExpandAnimationShorthand(string valueStr,
            Dictionary<MornUICssPropertyId, MornUICssValue> declarations,
            Dictionary<MornUICssPropertyId, string> rawDeclarations = null)
        {
            var parts = valueStr.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            string name = null;
            string duration = null;
            string timingFunc = null;
            string delay = null;
            string iterCount = null;
            string direction = null;
            string fillMode = null;

            foreach (var part in parts)
            {
                if (IsTimingFunction(part))
                    timingFunc ??= part;
                else if (IsTimeValue(part))
                {
                    if (duration == null) duration = part;
                    else delay ??= part;
                }
                else if (part is "infinite" || int.TryParse(part, out _))
                    iterCount ??= part;
                else if (part is "normal" or "reverse" or "alternate")
                    direction ??= part;
                else if (part is "none" or "forwards" or "backwards" or "both")
                    fillMode ??= part;
                else
                    name ??= part;
            }

            var raw = rawDeclarations ?? new Dictionary<MornUICssPropertyId, string>();
            if (name != null) raw[MornUICssPropertyId.AnimationName] = name;
            if (duration != null) raw[MornUICssPropertyId.AnimationDuration] = duration;
            if (timingFunc != null) raw[MornUICssPropertyId.AnimationTimingFunction] = timingFunc;
            if (delay != null) raw[MornUICssPropertyId.AnimationDelay] = delay;
            if (iterCount != null) raw[MornUICssPropertyId.AnimationIterationCount] = iterCount;
            if (direction != null) raw[MornUICssPropertyId.AnimationDirection] = direction;
            if (fillMode != null) raw[MornUICssPropertyId.AnimationFillMode] = fillMode;
        }

        // transition: property duration timing-function delay
        // e.g. "opacity 0.3s ease-out"
        private static void ExpandTransitionShorthand(string valueStr,
            Dictionary<MornUICssPropertyId, MornUICssValue> declarations,
            Dictionary<MornUICssPropertyId, string> rawDeclarations = null)
        {
            // Handle comma-separated multiple transitions:
            // "background-color 0.3s ease, border-color 0.3s ease, transform 0.3s ease"
            var segments = valueStr.Split(',');
            var props = new List<string>();
            string duration = null;
            string timingFunc = null;
            string delay = null;

            foreach (var segment in segments)
            {
                var parts = segment.Trim().Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    if (IsTimingFunction(part))
                        timingFunc ??= part;
                    else if (IsTimeValue(part))
                    {
                        if (duration == null) duration = part;
                        else delay ??= part;
                    }
                    else
                        props.Add(part);
                }
            }

            var raw = rawDeclarations ?? new Dictionary<MornUICssPropertyId, string>();
            if (props.Count > 0) raw[MornUICssPropertyId.TransitionProperty] = string.Join(", ", props);
            if (duration != null) raw[MornUICssPropertyId.TransitionDuration] = duration;
            if (timingFunc != null) raw[MornUICssPropertyId.TransitionTimingFunction] = timingFunc;
            if (delay != null) raw[MornUICssPropertyId.TransitionDelay] = delay;
        }

        private static bool IsTimingFunction(string s)
        {
            return s is "ease" or "linear" or "ease-in" or "ease-out" or "ease-in-out";
        }

        private static bool IsTimeValue(string s)
        {
            return s.EndsWith("s") || s.EndsWith("ms");
        }

        private static void SkipWhitespace(List<MornUICssToken> tokens, ref int pos)
        {
            while (pos < tokens.Count && tokens[pos].Type == MornUICssTokenType.Whitespace)
                pos++;
        }
    }

    internal static class MornUICssPropertyMap
    {
        private static readonly Dictionary<string, MornUICssPropertyId> Map = new()
        {
            ["width"] = MornUICssPropertyId.Width,
            ["height"] = MornUICssPropertyId.Height,
            ["min-width"] = MornUICssPropertyId.MinWidth,
            ["min-height"] = MornUICssPropertyId.MinHeight,
            ["max-width"] = MornUICssPropertyId.MaxWidth,
            ["max-height"] = MornUICssPropertyId.MaxHeight,
            ["margin-top"] = MornUICssPropertyId.MarginTop,
            ["margin-right"] = MornUICssPropertyId.MarginRight,
            ["margin-bottom"] = MornUICssPropertyId.MarginBottom,
            ["margin-left"] = MornUICssPropertyId.MarginLeft,
            ["padding-top"] = MornUICssPropertyId.PaddingTop,
            ["padding-right"] = MornUICssPropertyId.PaddingRight,
            ["padding-bottom"] = MornUICssPropertyId.PaddingBottom,
            ["padding-left"] = MornUICssPropertyId.PaddingLeft,
            ["background-color"] = MornUICssPropertyId.BackgroundColor,
            ["display"] = MornUICssPropertyId.Display,
            ["flex-direction"] = MornUICssPropertyId.FlexDirection,
            ["justify-content"] = MornUICssPropertyId.JustifyContent,
            ["align-items"] = MornUICssPropertyId.AlignItems,
            ["flex-grow"] = MornUICssPropertyId.FlexGrow,
            ["flex-shrink"] = MornUICssPropertyId.FlexShrink,
            ["flex-basis"] = MornUICssPropertyId.FlexBasis,
            ["gap"] = MornUICssPropertyId.Gap,
            ["color"] = MornUICssPropertyId.Color,
            ["font-size"] = MornUICssPropertyId.FontSize,
            ["text-align"] = MornUICssPropertyId.TextAlign,
            ["line-height"] = MornUICssPropertyId.LineHeight,
            ["border-top-width"] = MornUICssPropertyId.BorderTopWidth,
            ["border-right-width"] = MornUICssPropertyId.BorderRightWidth,
            ["border-bottom-width"] = MornUICssPropertyId.BorderBottomWidth,
            ["border-left-width"] = MornUICssPropertyId.BorderLeftWidth,
            ["border-top-color"] = MornUICssPropertyId.BorderTopColor,
            ["border-right-color"] = MornUICssPropertyId.BorderRightColor,
            ["border-bottom-color"] = MornUICssPropertyId.BorderBottomColor,
            ["border-left-color"] = MornUICssPropertyId.BorderLeftColor,
            ["border-radius"] = MornUICssPropertyId.BorderRadius,
            ["border-top-left-radius"] = MornUICssPropertyId.BorderTopLeftRadius,
            ["border-top-right-radius"] = MornUICssPropertyId.BorderTopRightRadius,
            ["border-bottom-right-radius"] = MornUICssPropertyId.BorderBottomRightRadius,
            ["border-bottom-left-radius"] = MornUICssPropertyId.BorderBottomLeftRadius,
            ["opacity"] = MornUICssPropertyId.Opacity,
            ["white-space"] = MornUICssPropertyId.WhiteSpace,
            ["text-overflow"] = MornUICssPropertyId.TextOverflow,
            ["overflow"] = MornUICssPropertyId.Overflow,
            ["transform"] = MornUICssPropertyId.Transform,
            ["animation-name"] = MornUICssPropertyId.AnimationName,
            ["animation-duration"] = MornUICssPropertyId.AnimationDuration,
            ["animation-delay"] = MornUICssPropertyId.AnimationDelay,
            ["animation-timing-function"] = MornUICssPropertyId.AnimationTimingFunction,
            ["animation-iteration-count"] = MornUICssPropertyId.AnimationIterationCount,
            ["animation-direction"] = MornUICssPropertyId.AnimationDirection,
            ["animation-fill-mode"] = MornUICssPropertyId.AnimationFillMode,
            ["transition-property"] = MornUICssPropertyId.TransitionProperty,
            ["transition-duration"] = MornUICssPropertyId.TransitionDuration,
            ["transition-delay"] = MornUICssPropertyId.TransitionDelay,
            ["transition-timing-function"] = MornUICssPropertyId.TransitionTimingFunction,
            ["cursor"] = MornUICssPropertyId.Cursor,
        };

        public static bool TryGetPropertyId(string name, out MornUICssPropertyId id) => Map.TryGetValue(name, out id);

        private static readonly Dictionary<(MornUICssPropertyId, string), MornUICssValue> KeywordMap = new()
        {
            // display
            [(MornUICssPropertyId.Display, "flex")] = MornUICssValue.Keyword((float)MornUIDisplay.Flex),
            [(MornUICssPropertyId.Display, "block")] = MornUICssValue.Keyword((float)MornUIDisplay.Block),
            [(MornUICssPropertyId.Display, "none")] = MornUICssValue.Keyword((float)MornUIDisplay.None),
            // flex-direction
            [(MornUICssPropertyId.FlexDirection, "row")] = MornUICssValue.Keyword((float)MornUIFlexDirection.Row),
            [(MornUICssPropertyId.FlexDirection, "column")] = MornUICssValue.Keyword((float)MornUIFlexDirection.Column),
            // justify-content
            [(MornUICssPropertyId.JustifyContent, "flex-start")] = MornUICssValue.Keyword((float)MornUIJustifyContent.FlexStart),
            [(MornUICssPropertyId.JustifyContent, "flex-end")] = MornUICssValue.Keyword((float)MornUIJustifyContent.FlexEnd),
            [(MornUICssPropertyId.JustifyContent, "center")] = MornUICssValue.Keyword((float)MornUIJustifyContent.Center),
            [(MornUICssPropertyId.JustifyContent, "space-between")] = MornUICssValue.Keyword((float)MornUIJustifyContent.SpaceBetween),
            [(MornUICssPropertyId.JustifyContent, "space-around")] = MornUICssValue.Keyword((float)MornUIJustifyContent.SpaceAround),
            // align-items
            [(MornUICssPropertyId.AlignItems, "stretch")] = MornUICssValue.Keyword((float)MornUIAlignItems.Stretch),
            [(MornUICssPropertyId.AlignItems, "flex-start")] = MornUICssValue.Keyword((float)MornUIAlignItems.FlexStart),
            [(MornUICssPropertyId.AlignItems, "flex-end")] = MornUICssValue.Keyword((float)MornUIAlignItems.FlexEnd),
            [(MornUICssPropertyId.AlignItems, "center")] = MornUICssValue.Keyword((float)MornUIAlignItems.Center),
            // text-align
            [(MornUICssPropertyId.TextAlign, "left")] = MornUICssValue.Keyword((float)MornUITextAlign.Left),
            [(MornUICssPropertyId.TextAlign, "center")] = MornUICssValue.Keyword((float)MornUITextAlign.Center),
            [(MornUICssPropertyId.TextAlign, "right")] = MornUICssValue.Keyword((float)MornUITextAlign.Right),
            // white-space
            [(MornUICssPropertyId.WhiteSpace, "normal")] = MornUICssValue.Keyword((float)MornUIWhiteSpace.Normal),
            [(MornUICssPropertyId.WhiteSpace, "nowrap")] = MornUICssValue.Keyword((float)MornUIWhiteSpace.Nowrap),
            // text-overflow
            [(MornUICssPropertyId.TextOverflow, "clip")] = MornUICssValue.Keyword((float)MornUITextOverflow.Clip),
            [(MornUICssPropertyId.TextOverflow, "ellipsis")] = MornUICssValue.Keyword((float)MornUITextOverflow.Ellipsis),
            // overflow
            [(MornUICssPropertyId.Overflow, "visible")] = MornUICssValue.Keyword((float)MornUIOverflow.Visible),
            [(MornUICssPropertyId.Overflow, "hidden")] = MornUICssValue.Keyword((float)MornUIOverflow.Hidden),
        };

        public static MornUICssValue ParseValue(MornUICssPropertyId propId, string valueStr)
        {
            if (KeywordMap.TryGetValue((propId, valueStr), out var kw))
                return kw;
            return MornUICssValue.Parse(valueStr);
        }
    }
}
