using System.Collections.Generic;

namespace MornLib
{
    internal sealed class MornUICssRule
    {
        public MornUICssSelector Selector { get; }
        public Dictionary<MornUICssPropertyId, MornUICssValue> Declarations { get; }
        public Dictionary<MornUICssPropertyId, string> RawDeclarations { get; }

        public MornUICssRule(MornUICssSelector selector,
            Dictionary<MornUICssPropertyId, MornUICssValue> declarations,
            Dictionary<MornUICssPropertyId, string> rawDeclarations = null)
        {
            Selector = selector;
            Declarations = declarations;
            RawDeclarations = rawDeclarations ?? new Dictionary<MornUICssPropertyId, string>();
        }
    }
}
