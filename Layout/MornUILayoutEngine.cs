using TMPro;
using UnityEngine;

namespace MornLib
{
    internal sealed class MornUILayoutEngine
    {
        public TMP_FontAsset DefaultFont { get; set; }

        public void Calculate(MornUILayoutNode root, float canvasWidth, float canvasHeight)
        {
            CalculateNode(root, canvasWidth, canvasHeight);
        }

        private void CalculateNode(MornUILayoutNode node, float availableWidth, float availableHeight)
        {
            var style = node.ComputedStyle;

            if (style.Display == MornUIDisplay.None)
            {
                node.LayoutRect = new MornUILayoutRect(0, 0, 0, 0, style);
                return;
            }

            // content-box: width/heightはcontent領域のサイズ
            // autoの場合は利用可能幅からborder/padding/marginを引いた値
            var contentWidth = ResolveSize(style.Width, availableWidth,
                availableWidth - style.BorderLeftWidth - style.BorderRightWidth
                - style.PaddingLeft - style.PaddingRight - style.MarginLeft - style.MarginRight);
            var contentHeight = ResolveSize(style.Height, availableHeight, 0f);

            contentWidth = ApplyMinMax(contentWidth, style.MinWidth, style.MaxWidth, availableWidth);
            contentHeight = ApplyMinMax(contentHeight, style.MinHeight, style.MaxHeight, availableHeight);

            // content-box: paddingBoxWidth = contentWidth + padding
            var innerWidth = Mathf.Max(contentWidth, 0f);
            var innerHeight = Mathf.Max(contentHeight, 0f);

            if (style.Display == MornUIDisplay.Flex)
            {
                LayoutFlex(node, innerWidth, innerHeight);
            }
            else
            {
                LayoutBlock(node, innerWidth, innerHeight);
            }

            // Auto height: contentHeight = content部分のサイズ (content-box)
            if (style.Height.Unit == MornUICssValue.ValueUnit.Auto)
            {
                if (node.Children.Count > 0)
                {
                    var childrenExtent = ComputeChildrenExtent(node, style);
                    contentHeight = childrenExtent;
                    innerHeight = contentHeight;

                    if (style.Display == MornUIDisplay.Flex)
                    {
                        LayoutFlex(node, innerWidth, innerHeight);
                    }
                }
                else if (!string.IsNullOrEmpty(node.TextContent))
                {
                    var textHeight = MornUITextPainter.MeasureMultiLineHeight(
                        node.TextContent, DefaultFont, style.FontSize,
                        innerWidth, style.WhiteSpace, style.LineHeightMultiplier);
                    contentHeight = textHeight;
                    innerHeight = textHeight;
                }
            }

            node.LayoutRect = new MornUILayoutRect(0, 0, contentWidth, contentHeight, style);
        }

        private void LayoutBlock(MornUILayoutNode node, float innerWidth, float innerHeight)
        {
            var childY = 0f;
            foreach (var child in node.Children)
            {
                if (child.ComputedStyle.Display == MornUIDisplay.None) continue;
                CalculateNode(child, innerWidth, innerHeight);
                var childRect = child.LayoutRect;
                childRect.X = 0;
                childRect.Y = childY;
                child.LayoutRect = childRect;
                childY += childRect.MarginBoxHeight;
            }
        }

        private void LayoutFlex(MornUILayoutNode node, float innerWidth, float innerHeight)
        {
            var style = node.ComputedStyle;
            var isRow = style.FlexDirection == MornUIFlexDirection.Row;
            var mainSize = isRow ? innerWidth : innerHeight;
            var crossSize = isRow ? innerHeight : innerWidth;
            var gap = style.Gap;

            // Phase 1: Calculate base sizes of children
            var childCount = 0;
            foreach (var child in node.Children)
            {
                if (child.ComputedStyle.Display != MornUIDisplay.None) childCount++;
            }

            if (childCount == 0) return;

            var baseSizes = new float[node.Children.Count];
            var grows = new float[node.Children.Count];
            var shrinks = new float[node.Children.Count];
            var totalBase = 0f;
            var totalGaps = gap * (childCount - 1);
            var visibleIdx = 0;

            for (var i = 0; i < node.Children.Count; i++)
            {
                var child = node.Children[i];
                if (child.ComputedStyle.Display == MornUIDisplay.None) continue;

                var cs = child.ComputedStyle;
                grows[i] = cs.FlexGrow;
                shrinks[i] = cs.FlexShrink;

                // Calculate child to get base main size
                CalculateNode(child, isRow ? innerWidth : crossSize, isRow ? crossSize : innerHeight);

                var childRect = child.LayoutRect;
                float childMainMarginBox;

                if (cs.FlexBasis.Unit != MornUICssValue.ValueUnit.Auto)
                {
                    // flex-basis overrides
                    childMainMarginBox = cs.FlexBasis.Resolve(mainSize)
                                         + (isRow ? cs.MarginLeft + cs.MarginRight : cs.MarginTop + cs.MarginBottom);
                }
                else if (isRow && cs.Width.Unit == MornUICssValue.ValueUnit.Auto)
                {
                    // Row flex: auto-width child uses intrinsic width
                    var intrinsic = ComputeIntrinsicWidth(child);
                    childMainMarginBox = intrinsic + cs.PaddingLeft + cs.PaddingRight
                                         + cs.BorderLeftWidth + cs.BorderRightWidth
                                         + cs.MarginLeft + cs.MarginRight;
                }
                else if (!isRow && cs.Height.Unit == MornUICssValue.ValueUnit.Auto)
                {
                    // Column flex: auto-height child uses intrinsic height
                    var intrinsic = ComputeIntrinsicHeight(child);
                    childMainMarginBox = intrinsic + cs.PaddingTop + cs.PaddingBottom
                                         + cs.BorderTopWidth + cs.BorderBottomWidth
                                         + cs.MarginTop + cs.MarginBottom;
                }
                else
                {
                    childMainMarginBox = isRow ? childRect.MarginBoxWidth : childRect.MarginBoxHeight;
                }

                baseSizes[i] = childMainMarginBox;
                totalBase += childMainMarginBox;
                visibleIdx++;
            }

            // Phase 2: Distribute free space
            var freeSpace = mainSize - totalBase - totalGaps;
            if (freeSpace > 0)
            {
                var totalGrow = 0f;
                for (var i = 0; i < node.Children.Count; i++)
                {
                    if (node.Children[i].ComputedStyle.Display == MornUIDisplay.None) continue;
                    totalGrow += grows[i];
                }

                if (totalGrow > 0)
                {
                    for (var i = 0; i < node.Children.Count; i++)
                    {
                        if (node.Children[i].ComputedStyle.Display == MornUIDisplay.None) continue;
                        baseSizes[i] += freeSpace * (grows[i] / totalGrow);
                    }

                    freeSpace = 0;
                }
            }
            else if (freeSpace < 0)
            {
                var totalShrink = 0f;
                for (var i = 0; i < node.Children.Count; i++)
                {
                    if (node.Children[i].ComputedStyle.Display == MornUIDisplay.None) continue;
                    totalShrink += shrinks[i];
                }

                if (totalShrink > 0)
                {
                    // Compute min-content sizes (min-width: auto)
                    var minSizes = new float[node.Children.Count];
                    for (var i = 0; i < node.Children.Count; i++)
                    {
                        if (node.Children[i].ComputedStyle.Display == MornUIDisplay.None) continue;
                        minSizes[i] = ComputeMinContentSize(node.Children[i], isRow);
                    }

                    // CSS spec: iterative freezing algorithm
                    // When an item is clamped to min-content, freeze it and redistribute
                    var frozen = new bool[node.Children.Count];
                    var remaining = -freeSpace;

                    for (var iter = 0; iter < childCount && remaining > 0.01f; iter++)
                    {
                        var totalWeightedShrink = 0f;
                        for (var i = 0; i < node.Children.Count; i++)
                        {
                            if (node.Children[i].ComputedStyle.Display == MornUIDisplay.None || frozen[i]) continue;
                            totalWeightedShrink += shrinks[i] * baseSizes[i];
                        }

                        if (totalWeightedShrink <= 0) break;

                        var anyFrozen = false;
                        for (var i = 0; i < node.Children.Count; i++)
                        {
                            if (node.Children[i].ComputedStyle.Display == MornUIDisplay.None || frozen[i]) continue;
                            var shrinkBy = remaining * (shrinks[i] * baseSizes[i] / totalWeightedShrink);
                            var newSize = baseSizes[i] - shrinkBy;
                            if (newSize < minSizes[i])
                            {
                                // Freeze at min-content, reclaim unused shrinkage
                                remaining -= (baseSizes[i] - minSizes[i]);
                                baseSizes[i] = minSizes[i];
                                frozen[i] = true;
                                anyFrozen = true;
                            }
                        }

                        if (!anyFrozen)
                        {
                            // No items frozen, apply final shrink
                            for (var i = 0; i < node.Children.Count; i++)
                            {
                                if (node.Children[i].ComputedStyle.Display == MornUIDisplay.None || frozen[i]) continue;
                                baseSizes[i] -= remaining * (shrinks[i] * baseSizes[i] / totalWeightedShrink);
                            }

                            remaining = 0;
                        }
                    }

                    freeSpace = 0;
                }
            }

            // Phase 3: Re-calculate children with final main sizes, then position
            // Compute main-axis start offset based on justify-content
            var actualTotal = totalGaps;
            for (var i = 0; i < node.Children.Count; i++)
            {
                if (node.Children[i].ComputedStyle.Display == MornUIDisplay.None) continue;
                actualTotal += baseSizes[i];
            }

            var remainingSpace = Mathf.Max(mainSize - actualTotal, 0f);
            float mainOffset;
            float spaceBetween;
            ComputeJustify(style.JustifyContent, remainingSpace, childCount, out mainOffset, out spaceBetween);

            visibleIdx = 0;
            for (var i = 0; i < node.Children.Count; i++)
            {
                var child = node.Children[i];
                if (child.ComputedStyle.Display == MornUIDisplay.None) continue;

                var cs = child.ComputedStyle;
                // baseSizes[i] is margin-box size; CalculateNode expects available width
                // which should produce the correct content width via auto-width formula
                var childMainAvail = baseSizes[i];
                if (childMainAvail < 0) childMainAvail = 0;

                float childCrossContent;

                if (isRow)
                {
                    // Main axis (width) = flex-distributed size (margin-box)
                    var availW = childMainAvail;
                    float availH;

                    if (cs.Height.Unit != MornUICssValue.ValueUnit.Auto)
                    {
                        availH = ResolveSize(cs.Height, crossSize, 0f)
                                 + cs.PaddingTop + cs.PaddingBottom
                                 + cs.BorderTopWidth + cs.BorderBottomWidth
                                 + cs.MarginTop + cs.MarginBottom;
                    }
                    else if (style.AlignItems == MornUIAlignItems.Stretch)
                    {
                        availH = crossSize;
                    }
                    else
                    {
                        CalculateNode(child, availW, Mathf.Max(crossSize, 0f));
                        var intrinsicH = ComputeIntrinsicHeight(child);
                        availH = intrinsicH + cs.PaddingTop + cs.PaddingBottom
                                 + cs.BorderTopWidth + cs.BorderBottomWidth
                                 + cs.MarginTop + cs.MarginBottom;
                    }

                    CalculateNode(child, availW, Mathf.Max(availH, 0f));
                    childCrossContent = child.LayoutRect.MarginBoxHeight;
                }
                else
                {
                    // Main axis (height) = flex-distributed size (margin-box)
                    var availH = childMainAvail;
                    float availW;

                    if (cs.Width.Unit != MornUICssValue.ValueUnit.Auto)
                    {
                        availW = ResolveSize(cs.Width, crossSize, 0f)
                                 + cs.PaddingLeft + cs.PaddingRight
                                 + cs.BorderLeftWidth + cs.BorderRightWidth
                                 + cs.MarginLeft + cs.MarginRight;
                    }
                    else if (style.AlignItems == MornUIAlignItems.Stretch)
                    {
                        availW = crossSize;
                    }
                    else
                    {
                        CalculateNode(child, Mathf.Max(crossSize, 0f), availH);
                        var intrinsicW = ComputeIntrinsicWidth(child);
                        availW = intrinsicW + cs.PaddingLeft + cs.PaddingRight
                                 + cs.BorderLeftWidth + cs.BorderRightWidth
                                 + cs.MarginLeft + cs.MarginRight;
                    }

                    CalculateNode(child, Mathf.Max(availW, 0f), availH);
                    childCrossContent = child.LayoutRect.MarginBoxWidth;
                }

                // Position on main axis
                var childRect = child.LayoutRect;
                var mainPos = mainOffset;

                // Position on cross axis
                var crossPos = ComputeCrossOffset(style.AlignItems, crossSize,
                    isRow ? childRect.MarginBoxHeight : childRect.MarginBoxWidth);

                if (isRow)
                {
                    childRect.X = mainPos;
                    childRect.Y = crossPos;
                }
                else
                {
                    childRect.X = crossPos;
                    childRect.Y = mainPos;
                }

                child.LayoutRect = childRect;

                mainOffset += (isRow ? childRect.MarginBoxWidth : childRect.MarginBoxHeight) + gap + spaceBetween;
                visibleIdx++;
            }
        }

        private static void ComputeJustify(MornUIJustifyContent justify, float freeSpace, int itemCount,
            out float startOffset, out float spaceBetween)
        {
            startOffset = 0;
            spaceBetween = 0;
            if (freeSpace <= 0 || itemCount == 0) return;

            switch (justify)
            {
                case MornUIJustifyContent.FlexStart:
                    break;
                case MornUIJustifyContent.FlexEnd:
                    startOffset = freeSpace;
                    break;
                case MornUIJustifyContent.Center:
                    startOffset = freeSpace / 2f;
                    break;
                case MornUIJustifyContent.SpaceBetween:
                    if (itemCount > 1)
                        spaceBetween = freeSpace / (itemCount - 1);
                    break;
                case MornUIJustifyContent.SpaceAround:
                    var each = freeSpace / itemCount;
                    startOffset = each / 2f;
                    spaceBetween = each;
                    break;
            }
        }

        private static float ComputeCrossOffset(MornUIAlignItems align, float crossSize, float childCrossSize)
        {
            switch (align)
            {
                case MornUIAlignItems.FlexStart:
                case MornUIAlignItems.Stretch:
                    return 0;
                case MornUIAlignItems.FlexEnd:
                    return crossSize - childCrossSize;
                case MornUIAlignItems.Center:
                    return (crossSize - childCrossSize) / 2f;
                default:
                    return 0;
            }
        }

        private static float ComputeChildrenExtent(MornUILayoutNode node, MornUIComputedStyle style)
        {
            var isRow = style.Display == MornUIDisplay.Flex && style.FlexDirection == MornUIFlexDirection.Row;
            var extent = 0f;
            var gap = style.Gap;
            var visibleCount = 0;

            foreach (var child in node.Children)
            {
                if (child.ComputedStyle.Display == MornUIDisplay.None) continue;
                var childRect = child.LayoutRect;

                if (isRow)
                {
                    var childBottom = childRect.Y + childRect.MarginBoxHeight;
                    if (childBottom > extent) extent = childBottom;
                }
                else
                {
                    extent += childRect.MarginBoxHeight;
                    visibleCount++;
                }
            }

            if (!isRow && visibleCount > 1)
                extent += gap * (visibleCount - 1);

            return extent;
        }

        private float ComputeIntrinsicWidth(MornUILayoutNode node)
        {
            var cs = node.ComputedStyle;
            // Explicit width
            if (cs.Width.Unit != MornUICssValue.ValueUnit.Auto)
                return node.LayoutRect.ContentWidth;

            // Text node: measure with font metrics
            if (node.Children.Count == 0 && !string.IsNullOrEmpty(node.TextContent))
            {
                var w = MornUITextPainter.MeasureWidth(node.TextContent, DefaultFont, cs.FontSize);
                return w > 0 ? w : MornUITextPainter.MeasureWidthFallback(node.TextContent, cs.FontSize);
            }

            // Container: find max child extent
            var maxWidth = 0f;
            var isFlex = cs.Display == MornUIDisplay.Flex;
            var isRow = isFlex && cs.FlexDirection == MornUIFlexDirection.Row;

            if (isRow)
            {
                // Row: sum of children widths + gaps
                var sum = 0f;
                var count = 0;
                foreach (var child in node.Children)
                {
                    if (child.ComputedStyle.Display == MornUIDisplay.None) continue;
                    sum += child.LayoutRect.MarginBoxWidth;
                    count++;
                }

                if (count > 1) sum += cs.Gap * (count - 1);
                maxWidth = sum;
            }
            else
            {
                // Column or block: max child width
                foreach (var child in node.Children)
                {
                    if (child.ComputedStyle.Display == MornUIDisplay.None) continue;
                    var w = child.LayoutRect.MarginBoxWidth;
                    if (w > maxWidth) maxWidth = w;
                }
            }

            return maxWidth;
        }

        private float ComputeIntrinsicHeight(MornUILayoutNode node)
        {
            var cs = node.ComputedStyle;
            if (cs.Height.Unit != MornUICssValue.ValueUnit.Auto)
                return node.LayoutRect.ContentHeight;

            if (node.Children.Count == 0 && !string.IsNullOrEmpty(node.TextContent))
                return MornUITextPainter.MeasureHeight(DefaultFont, cs.FontSize, cs.LineHeightMultiplier);

            var isFlex = cs.Display == MornUIDisplay.Flex;
            var isRow = isFlex && cs.FlexDirection == MornUIFlexDirection.Row;

            if (isRow)
            {
                var maxH = 0f;
                foreach (var child in node.Children)
                {
                    if (child.ComputedStyle.Display == MornUIDisplay.None) continue;
                    var h = child.LayoutRect.MarginBoxHeight;
                    if (h > maxH) maxH = h;
                }

                return maxH;
            }
            else
            {
                var sum = 0f;
                var count = 0;
                foreach (var child in node.Children)
                {
                    if (child.ComputedStyle.Display == MornUIDisplay.None) continue;
                    sum += child.LayoutRect.MarginBoxHeight;
                    count++;
                }

                if (count > 1) sum += cs.Gap * (count - 1);
                return sum;
            }
        }

        private float ComputeMinContentSize(MornUILayoutNode node, bool isRow)
        {
            var cs = node.ComputedStyle;

            // If explicit min-width/min-height is set, use that instead
            var minProp = isRow ? cs.MinWidth : cs.MinHeight;
            if (minProp.Unit == MornUICssValue.ValueUnit.Px)
            {
                return minProp.Number
                       + (isRow ? cs.PaddingLeft + cs.PaddingRight + cs.BorderLeftWidth + cs.BorderRightWidth
                           + cs.MarginLeft + cs.MarginRight
                           : cs.PaddingTop + cs.PaddingBottom + cs.BorderTopWidth + cs.BorderBottomWidth
                           + cs.MarginTop + cs.MarginBottom);
            }

            // Text node: min-content width
            if (node.Children.Count == 0 && !string.IsNullOrEmpty(node.TextContent))
            {
                float textMin;
                if (isRow)
                {
                    if (cs.WhiteSpace == MornUIWhiteSpace.Nowrap)
                    {
                        // nowrap: full text width is the minimum
                        textMin = MornUITextPainter.MeasureWidth(node.TextContent, DefaultFont, cs.FontSize);
                    }
                    else
                    {
                        // normal: longest single word
                        textMin = 0f;
                        var words = node.TextContent.Split(' ');
                        foreach (var word in words)
                        {
                            var w = MornUITextPainter.MeasureWidth(word, DefaultFont, cs.FontSize);
                            if (w > textMin) textMin = w;
                        }
                    }

                    return textMin + cs.PaddingLeft + cs.PaddingRight
                           + cs.BorderLeftWidth + cs.BorderRightWidth
                           + cs.MarginLeft + cs.MarginRight;
                }

                // Column direction: single line height is the minimum
                var h = MornUITextPainter.MeasureHeight(DefaultFont, cs.FontSize, cs.LineHeightMultiplier);
                return h + cs.PaddingTop + cs.PaddingBottom
                       + cs.BorderTopWidth + cs.BorderBottomWidth
                       + cs.MarginTop + cs.MarginBottom;
            }

            // Container with children: sum min-content of children (simplified)
            if (node.Children.Count > 0)
            {
                var isFlex = cs.Display == MornUIDisplay.Flex;
                var childIsRow = isFlex && cs.FlexDirection == MornUIFlexDirection.Row;

                if (isRow)
                {
                    // For row direction, min is the largest child if column, or sum if row
                    if (childIsRow)
                    {
                        var sum = 0f;
                        var count = 0;
                        foreach (var child in node.Children)
                        {
                            if (child.ComputedStyle.Display == MornUIDisplay.None) continue;
                            sum += ComputeMinContentSize(child, true);
                            count++;
                        }
                        if (count > 1) sum += cs.Gap * (count - 1);
                        return sum + cs.PaddingLeft + cs.PaddingRight
                               + cs.BorderLeftWidth + cs.BorderRightWidth
                               + cs.MarginLeft + cs.MarginRight;
                    }
                    else
                    {
                        var maxW = 0f;
                        foreach (var child in node.Children)
                        {
                            if (child.ComputedStyle.Display == MornUIDisplay.None) continue;
                            var w = ComputeMinContentSize(child, true);
                            if (w > maxW) maxW = w;
                        }
                        return maxW + cs.PaddingLeft + cs.PaddingRight
                               + cs.BorderLeftWidth + cs.BorderRightWidth
                               + cs.MarginLeft + cs.MarginRight;
                    }
                }
            }

            return 0f;
        }

        private static float ResolveSize(MornUICssValue sizeValue, float parentSize, float defaultSize)
        {
            return sizeValue.Unit switch
            {
                MornUICssValue.ValueUnit.Px => sizeValue.Number,
                MornUICssValue.ValueUnit.Percent => parentSize * sizeValue.Number / 100f,
                _ => defaultSize,
            };
        }

        private static float ApplyMinMax(float size, MornUICssValue min, MornUICssValue max, float parentSize)
        {
            if (min.Unit != MornUICssValue.ValueUnit.None)
            {
                var minVal = min.Resolve(parentSize);
                if (size < minVal) size = minVal;
            }

            if (max.Unit != MornUICssValue.ValueUnit.None)
            {
                var maxVal = max.Resolve(parentSize);
                if (size > maxVal) size = maxVal;
            }

            return Mathf.Max(size, 0);
        }
    }
}
