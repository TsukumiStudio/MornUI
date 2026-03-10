using System.Collections.Generic;
using System.Linq;

namespace MornLib
{
    internal sealed class MornUIStyleResolver
    {
        public void Resolve(MornUILayoutNode root, List<MornUICssRule> rules, float canvasWidth, float canvasHeight)
        {
            var sortedRules = rules.OrderBy(r => r.Selector.Specificity).ToList();
            ResolveNode(root, sortedRules, canvasWidth, canvasHeight);
        }

        private void ResolveNode(MornUILayoutNode node, List<MornUICssRule> sortedRules,
            float parentWidth, float parentHeight)
        {
            node.ComputedStyle = new MornUIComputedStyle();

            // Inherit from parent
            if (node.Parent != null)
            {
                var parentStyle = node.Parent.ComputedStyle;
                node.ComputedStyle.TextColor = parentStyle.TextColor;
                node.ComputedStyle.FontSize = parentStyle.FontSize;
                node.ComputedStyle.TextAlign = parentStyle.TextAlign;
                node.ComputedStyle.LineHeightMultiplier = parentStyle.LineHeightMultiplier;
                node.ComputedStyle.WhiteSpace = parentStyle.WhiteSpace;
            }

            foreach (var rule in sortedRules)
            {
                if (!rule.Selector.Matches(node))
                    continue;
                foreach (var decl in rule.Declarations)
                {
                    node.ComputedStyle.Apply(decl.Key, decl.Value, parentWidth, parentHeight);
                }
                foreach (var raw in rule.RawDeclarations)
                {
                    node.ComputedStyle.ApplyRaw(raw.Key, raw.Value);
                }
            }

            // Use JS-modified Attributes["style"] if available, otherwise original InlineStyle
            var effectiveInlineStyle = node.Attributes != null &&
                                       node.Attributes.TryGetValue("style", out var attrStyle)
                ? attrStyle
                : node.InlineStyle;

            if (!string.IsNullOrEmpty(effectiveInlineStyle))
            {
                var inlineDeclarations = MornUICssInlineParser.Parse(effectiveInlineStyle, out var inlineRaw);
                foreach (var decl in inlineDeclarations)
                {
                    node.ComputedStyle.Apply(decl.Key, decl.Value, parentWidth, parentHeight);
                }
                foreach (var raw in inlineRaw)
                {
                    node.ComputedStyle.ApplyRaw(raw.Key, raw.Value);
                }
            }

            var nodeWidth = node.ComputedStyle.Width.Unit != MornUICssValue.ValueUnit.Auto
                ? node.ComputedStyle.Width.Resolve(parentWidth)
                : parentWidth;
            var nodeHeight = node.ComputedStyle.Height.Unit != MornUICssValue.ValueUnit.Auto
                ? node.ComputedStyle.Height.Resolve(parentHeight)
                : parentHeight;

            foreach (var child in node.Children)
            {
                ResolveNode(child, sortedRules, nodeWidth, nodeHeight);
            }
        }
    }
}
