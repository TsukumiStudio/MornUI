using System.Collections.Generic;

namespace MornLib
{
    internal sealed class MornUILayoutNode
    {
        public string TagName { get; }
        public string Id { get; }
        public List<string> ClassList { get; }
        public string InlineStyle { get; }
        public string TextContent { get; set; }
        public string OnClick { get; set; }
        public Dictionary<string, string> Attributes { get; set; }

        public MornUILayoutNode Parent { get; set; }
        public List<MornUILayoutNode> Children { get; } = new();

        public bool IsFocused { get; set; }

        public MornUIComputedStyle ComputedStyle { get; set; } = new();
        public MornUILayoutRect LayoutRect { get; set; }

        public MornUILayoutNode(string tagName, string id, List<string> classList, string inlineStyle)
        {
            TagName = tagName;
            Id = id;
            ClassList = classList ?? new List<string>();
            InlineStyle = inlineStyle;
        }

        public void AddChild(MornUILayoutNode child)
        {
            child.Parent = this;
            Children.Add(child);
        }
    }
}
