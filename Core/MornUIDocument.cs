using System.Collections.Generic;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace MornLib
{
    internal static class MornUIDocument
    {
        public static MornUILayoutNode Parse(string html)
        {
            var parser = new HtmlParser();
            var document = parser.ParseDocument(html);
            var body = document.Body;
            if (body == null)
                return new MornUILayoutNode("body", null, null, null);
            return ConvertNode(body);
        }

        private static MornUILayoutNode ConvertNode(IElement element)
        {
            var classList = new List<string>();
            foreach (var cls in element.ClassList)
            {
                classList.Add(cls);
            }

            var node = new MornUILayoutNode(
                element.LocalName,
                element.Id,
                classList,
                element.GetAttribute("style")
            );

            node.TextContent = element.OwnText()?.Trim();
            node.OnClick = element.GetAttribute("onclick");

            // Store all attributes for JS access
            var attrs = new Dictionary<string, string>();
            foreach (var attr in element.Attributes)
            {
                attrs[attr.Name] = attr.Value;
            }
            node.Attributes = attrs;

            foreach (var child in element.Children)
            {
                node.AddChild(ConvertNode(child));
            }

            return node;
        }

        private static string OwnText(this IElement element)
        {
            foreach (var node in element.ChildNodes)
            {
                if (node is IText textNode)
                    return textNode.Data;
            }

            return null;
        }
    }
}
