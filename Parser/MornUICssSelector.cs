using System.Collections.Generic;

namespace MornLib
{
    internal sealed class MornUICssSelector
    {
        private readonly List<SelectorPart> _parts;

        public int Specificity { get; }

        private MornUICssSelector(List<SelectorPart> parts, int specificity)
        {
            _parts = parts;
            Specificity = specificity;
        }

        public bool Matches(MornUILayoutNode node)
        {
            var current = node;
            for (var i = _parts.Count - 1; i >= 0; i--)
            {
                var part = _parts[i];
                if (part.Type == SelectorPartType.Combinator)
                {
                    if (part.Value == ">")
                    {
                        current = current?.Parent;
                        continue;
                    }

                    if (part.Value == " ")
                    {
                        i--;
                        if (i < 0) return false;
                        var ancestorPart = _parts[i];
                        current = FindAncestor(current?.Parent, ancestorPart);
                        if (current == null) return false;
                        continue;
                    }
                }

                if (current == null || !MatchesPart(current, part))
                    return false;

                if (i > 0 && _parts[i - 1].Type != SelectorPartType.Combinator)
                    continue;
            }

            return true;
        }

        private static bool MatchesPart(MornUILayoutNode node, SelectorPart part)
        {
            return part.Type switch
            {
                SelectorPartType.Tag => string.Equals(node.TagName, part.Value,
                    System.StringComparison.OrdinalIgnoreCase),
                SelectorPartType.Id => node.Id == part.Value,
                SelectorPartType.Class => node.ClassList.Contains(part.Value),
                SelectorPartType.PseudoClass => MatchesPseudoClass(node, part.Value),
                _ => false,
            };
        }

        private static bool MatchesPseudoClass(MornUILayoutNode node, string pseudo)
        {
            if (pseudo == "focus")
                return node.IsFocused;

            // :nth-child(n)
            if (pseudo.StartsWith("nth-child(") && pseudo.EndsWith(")"))
            {
                var arg = pseudo.Substring(10, pseudo.Length - 11).Trim();
                if (!int.TryParse(arg, out var n)) return false;
                if (node.Parent == null) return n == 1;
                var index = node.Parent.Children.IndexOf(node) + 1;
                return index == n;
            }

            return false;
        }

        private static MornUILayoutNode FindAncestor(MornUILayoutNode node, SelectorPart part)
        {
            while (node != null)
            {
                if (MatchesPart(node, part))
                    return node;
                node = node.Parent;
            }

            return null;
        }

        public static MornUICssSelector Parse(List<MornUICssToken> tokens, ref int pos)
        {
            var parts = new List<SelectorPart>();
            var idCount = 0;
            var classCount = 0;
            var tagCount = 0;

            while (pos < tokens.Count)
            {
                var t = tokens[pos];
                if (t.Type == MornUICssTokenType.LBrace || t.Type == MornUICssTokenType.Comma ||
                    t.Type == MornUICssTokenType.Eof)
                    break;

                if (t.Type == MornUICssTokenType.Hash)
                {
                    parts.Add(new SelectorPart(SelectorPartType.Id, t.Value));
                    idCount++;
                    pos++;
                }
                else if (t.Type == MornUICssTokenType.Dot)
                {
                    pos++;
                    if (pos < tokens.Count && tokens[pos].Type == MornUICssTokenType.Ident)
                    {
                        parts.Add(new SelectorPart(SelectorPartType.Class, tokens[pos].Value));
                        classCount++;
                        pos++;
                    }
                }
                else if (t.Type == MornUICssTokenType.Colon)
                {
                    // Pseudo-class: :focus, :nth-child(n), etc.
                    pos++;
                    if (pos < tokens.Count && tokens[pos].Type == MornUICssTokenType.Ident)
                    {
                        var pseudoName = tokens[pos].Value;
                        pos++;

                        // Handle functional pseudo-class like :nth-child(2)
                        if (pos < tokens.Count && tokens[pos].Type == MornUICssTokenType.Ident &&
                            tokens[pos].Value == "(")
                        {
                            var sb = new System.Text.StringBuilder();
                            sb.Append(pseudoName).Append('(');
                            pos++;
                            while (pos < tokens.Count)
                            {
                                var pt = tokens[pos];
                                if (pt.Type == MornUICssTokenType.Ident && pt.Value == ")")
                                {
                                    sb.Append(')');
                                    pos++;
                                    break;
                                }

                                sb.Append(pt.Value);
                                pos++;
                            }

                            pseudoName = sb.ToString();
                        }

                        parts.Add(new SelectorPart(SelectorPartType.PseudoClass, pseudoName));
                        classCount++; // pseudo-classes have same specificity as classes
                    }
                }
                else if (t.Type == MornUICssTokenType.Ident)
                {
                    parts.Add(new SelectorPart(SelectorPartType.Tag, t.Value));
                    tagCount++;
                    pos++;
                }
                else if (t.Type == MornUICssTokenType.Gt)
                {
                    parts.Add(new SelectorPart(SelectorPartType.Combinator, ">"));
                    pos++;
                }
                else if (t.Type == MornUICssTokenType.Whitespace)
                {
                    pos++;
                    if (pos < tokens.Count)
                    {
                        var next = tokens[pos];
                        if (next.Type != MornUICssTokenType.LBrace && next.Type != MornUICssTokenType.Comma &&
                            next.Type != MornUICssTokenType.Eof && next.Type != MornUICssTokenType.Gt)
                        {
                            parts.Add(new SelectorPart(SelectorPartType.Combinator, " "));
                        }
                    }
                }
                else
                {
                    pos++;
                }
            }

            var specificity = idCount * 100 + classCount * 10 + tagCount;
            return new MornUICssSelector(parts, specificity);
        }

        private enum SelectorPartType
        {
            Tag,
            Id,
            Class,
            Combinator,
            PseudoClass,
        }

        private readonly struct SelectorPart
        {
            public readonly SelectorPartType Type;
            public readonly string Value;

            public SelectorPart(SelectorPartType type, string value)
            {
                Type = type;
                Value = value;
            }
        }
    }
}
