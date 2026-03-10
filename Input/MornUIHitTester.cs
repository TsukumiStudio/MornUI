using UnityEngine;

namespace MornLib
{
    internal static class MornUIHitTester
    {
        /// <summary>
        /// Find the deepest node whose border-box contains the point (x, y).
        /// Coordinates are in layout space (top-left origin, Y down).
        /// </summary>
        public static MornUILayoutNode HitTest(MornUILayoutNode root, float x, float y)
        {
            return HitTestNode(root, x, y, 0f, 0f);
        }

        private static MornUILayoutNode HitTestNode(MornUILayoutNode node, float x, float y,
            float parentGlobalX, float parentGlobalY)
        {
            if (node.ComputedStyle.Display == MornUIDisplay.None)
                return null;

            var rect = node.LayoutRect;
            var bbX = rect.GlobalBorderBoxX(parentGlobalX);
            var bbY = rect.GlobalBorderBoxY(parentGlobalY);
            var bbW = rect.BorderBoxWidth;
            var bbH = rect.BorderBoxHeight;

            // Check if point is inside this node's border-box
            if (x < bbX || x >= bbX + bbW || y < bbY || y >= bbY + bbH)
                return null;

            var contentX = rect.GlobalContentX(parentGlobalX);
            var contentY = rect.GlobalContentY(parentGlobalY);

            // Check children in reverse order (later children are on top)
            for (var i = node.Children.Count - 1; i >= 0; i--)
            {
                var hit = HitTestNode(node.Children[i], x, y, contentX, contentY);
                if (hit != null)
                    return hit;
            }

            // No child was hit, return this node
            return node;
        }
    }
}
