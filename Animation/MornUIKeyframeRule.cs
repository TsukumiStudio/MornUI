using System.Collections.Generic;

namespace MornLib
{
    internal sealed class MornUIKeyframeStop
    {
        public float Percentage; // 0.0 ~ 1.0
        public Dictionary<string, string> Properties; // raw CSS property:value pairs
    }

    internal sealed class MornUIKeyframeRule
    {
        public string Name;
        public List<MornUIKeyframeStop> Stops = new();
    }
}
