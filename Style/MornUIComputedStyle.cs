using UnityEngine;

namespace MornLib
{
    internal sealed class MornUIComputedStyle
    {
        public MornUICssValue Width = MornUICssValue.AutoValue;
        public MornUICssValue Height = MornUICssValue.AutoValue;
        public MornUICssValue MinWidth = MornUICssValue.NoneValue;
        public MornUICssValue MinHeight = MornUICssValue.NoneValue;
        public MornUICssValue MaxWidth = MornUICssValue.NoneValue;
        public MornUICssValue MaxHeight = MornUICssValue.NoneValue;

        public float MarginTop;
        public float MarginRight;
        public float MarginBottom;
        public float MarginLeft;

        public float PaddingTop;
        public float PaddingRight;
        public float PaddingBottom;
        public float PaddingLeft;

        public Color32 BackgroundColor = new(0, 0, 0, 0);

        public MornUIDisplay Display = MornUIDisplay.Block;
        public MornUIFlexDirection FlexDirection = MornUIFlexDirection.Row;
        public MornUIJustifyContent JustifyContent = MornUIJustifyContent.FlexStart;
        public MornUIAlignItems AlignItems = MornUIAlignItems.Stretch;
        public float FlexGrow;
        public float FlexShrink = 1f;
        public MornUICssValue FlexBasis = MornUICssValue.AutoValue;
        public float Gap;

        public float BorderTopWidth;
        public float BorderRightWidth;
        public float BorderBottomWidth;
        public float BorderLeftWidth;
        public Color32 BorderTopColor = new(0, 0, 0, 255);
        public Color32 BorderRightColor = new(0, 0, 0, 255);
        public Color32 BorderBottomColor = new(0, 0, 0, 255);
        public Color32 BorderLeftColor = new(0, 0, 0, 255);
        public float BorderTopLeftRadius;
        public float BorderTopRightRadius;
        public float BorderBottomRightRadius;
        public float BorderBottomLeftRadius;
        public float Opacity = 1f;

        public Color32 TextColor = new(0, 0, 0, 255);
        public float FontSize = 16f;
        public MornUITextAlign TextAlign = MornUITextAlign.Left;
        public float LineHeightMultiplier = -1f; // -1 = auto, >0 = multiplier of font-size
        public MornUIWhiteSpace WhiteSpace = MornUIWhiteSpace.Normal;
        public MornUITextOverflow TextOverflow = MornUITextOverflow.Clip;
        public MornUIOverflow Overflow = MornUIOverflow.Visible;

        // Transform
        public MornUITransform Transform = MornUITransform.Identity;

        // Animation
        public string AnimationName = "";
        public float AnimationDuration;
        public float AnimationDelay;
        public MornUIEasingType AnimationEasing = MornUIEasingType.Ease;
        public int AnimationIterationCount = 1;
        public MornUIAnimationDirection AnimationDirection = MornUIAnimationDirection.Normal;
        public MornUIAnimationFillMode AnimationFillMode = MornUIAnimationFillMode.None;

        // Transition
        public string TransitionProperty = "";
        public float TransitionDuration;
        public float TransitionDelay;
        public MornUIEasingType TransitionEasing = MornUIEasingType.Ease;

        // Cursor
        public string Cursor = "";

        public void Apply(MornUICssPropertyId propId, MornUICssValue value, float parentWidth, float parentHeight)
        {
            switch (propId)
            {
                case MornUICssPropertyId.Width:
                    Width = value;
                    break;
                case MornUICssPropertyId.Height:
                    Height = value;
                    break;
                case MornUICssPropertyId.MinWidth:
                    MinWidth = value;
                    break;
                case MornUICssPropertyId.MinHeight:
                    MinHeight = value;
                    break;
                case MornUICssPropertyId.MaxWidth:
                    MaxWidth = value;
                    break;
                case MornUICssPropertyId.MaxHeight:
                    MaxHeight = value;
                    break;
                case MornUICssPropertyId.MarginTop:
                    MarginTop = value.Resolve(parentHeight);
                    break;
                case MornUICssPropertyId.MarginRight:
                    MarginRight = value.Resolve(parentWidth);
                    break;
                case MornUICssPropertyId.MarginBottom:
                    MarginBottom = value.Resolve(parentHeight);
                    break;
                case MornUICssPropertyId.MarginLeft:
                    MarginLeft = value.Resolve(parentWidth);
                    break;
                case MornUICssPropertyId.PaddingTop:
                    PaddingTop = value.Resolve(parentHeight);
                    break;
                case MornUICssPropertyId.PaddingRight:
                    PaddingRight = value.Resolve(parentWidth);
                    break;
                case MornUICssPropertyId.PaddingBottom:
                    PaddingBottom = value.Resolve(parentHeight);
                    break;
                case MornUICssPropertyId.PaddingLeft:
                    PaddingLeft = value.Resolve(parentWidth);
                    break;
                case MornUICssPropertyId.BackgroundColor:
                    if (value.Unit == MornUICssValue.ValueUnit.Color)
                        BackgroundColor = value.ColorVal;
                    break;
                case MornUICssPropertyId.Display:
                    if (value.Unit == MornUICssValue.ValueUnit.Keyword)
                        Display = (MornUIDisplay)(int)value.Number;
                    break;
                case MornUICssPropertyId.FlexDirection:
                    if (value.Unit == MornUICssValue.ValueUnit.Keyword)
                        FlexDirection = (MornUIFlexDirection)(int)value.Number;
                    break;
                case MornUICssPropertyId.JustifyContent:
                    if (value.Unit == MornUICssValue.ValueUnit.Keyword)
                        JustifyContent = (MornUIJustifyContent)(int)value.Number;
                    break;
                case MornUICssPropertyId.AlignItems:
                    if (value.Unit == MornUICssValue.ValueUnit.Keyword)
                        AlignItems = (MornUIAlignItems)(int)value.Number;
                    break;
                case MornUICssPropertyId.FlexGrow:
                    FlexGrow = value.Number;
                    break;
                case MornUICssPropertyId.FlexShrink:
                    FlexShrink = value.Number;
                    break;
                case MornUICssPropertyId.FlexBasis:
                    FlexBasis = value;
                    break;
                case MornUICssPropertyId.Gap:
                    Gap = value.Resolve(parentWidth);
                    break;
                case MornUICssPropertyId.Color:
                    if (value.Unit == MornUICssValue.ValueUnit.Color)
                        TextColor = value.ColorVal;
                    break;
                case MornUICssPropertyId.FontSize:
                    FontSize = value.Resolve(parentHeight);
                    if (FontSize < 1f) FontSize = 1f;
                    break;
                case MornUICssPropertyId.TextAlign:
                    if (value.Unit == MornUICssValue.ValueUnit.Keyword)
                        TextAlign = (MornUITextAlign)(int)value.Number;
                    break;
                case MornUICssPropertyId.LineHeight:
                    if (value.Unit == MornUICssValue.ValueUnit.Px)
                        LineHeightMultiplier = value.Number;
                    break;
                case MornUICssPropertyId.BorderTopWidth:
                    BorderTopWidth = value.Resolve(parentWidth);
                    break;
                case MornUICssPropertyId.BorderRightWidth:
                    BorderRightWidth = value.Resolve(parentWidth);
                    break;
                case MornUICssPropertyId.BorderBottomWidth:
                    BorderBottomWidth = value.Resolve(parentWidth);
                    break;
                case MornUICssPropertyId.BorderLeftWidth:
                    BorderLeftWidth = value.Resolve(parentWidth);
                    break;
                case MornUICssPropertyId.BorderTopColor:
                    if (value.Unit == MornUICssValue.ValueUnit.Color)
                        BorderTopColor = value.ColorVal;
                    break;
                case MornUICssPropertyId.BorderRightColor:
                    if (value.Unit == MornUICssValue.ValueUnit.Color)
                        BorderRightColor = value.ColorVal;
                    break;
                case MornUICssPropertyId.BorderBottomColor:
                    if (value.Unit == MornUICssValue.ValueUnit.Color)
                        BorderBottomColor = value.ColorVal;
                    break;
                case MornUICssPropertyId.BorderLeftColor:
                    if (value.Unit == MornUICssValue.ValueUnit.Color)
                        BorderLeftColor = value.ColorVal;
                    break;
                case MornUICssPropertyId.BorderRadius:
                    var rad = value.Resolve(parentWidth);
                    BorderTopLeftRadius = BorderTopRightRadius = BorderBottomRightRadius = BorderBottomLeftRadius = rad;
                    break;
                case MornUICssPropertyId.BorderTopLeftRadius:
                    BorderTopLeftRadius = value.Resolve(parentWidth);
                    break;
                case MornUICssPropertyId.BorderTopRightRadius:
                    BorderTopRightRadius = value.Resolve(parentWidth);
                    break;
                case MornUICssPropertyId.BorderBottomRightRadius:
                    BorderBottomRightRadius = value.Resolve(parentWidth);
                    break;
                case MornUICssPropertyId.BorderBottomLeftRadius:
                    BorderBottomLeftRadius = value.Resolve(parentWidth);
                    break;
                case MornUICssPropertyId.Opacity:
                    Opacity = Mathf.Clamp01(value.Number);
                    break;
                case MornUICssPropertyId.WhiteSpace:
                    if (value.Unit == MornUICssValue.ValueUnit.Keyword)
                        WhiteSpace = (MornUIWhiteSpace)(int)value.Number;
                    break;
                case MornUICssPropertyId.TextOverflow:
                    if (value.Unit == MornUICssValue.ValueUnit.Keyword)
                        TextOverflow = (MornUITextOverflow)(int)value.Number;
                    break;
                case MornUICssPropertyId.Overflow:
                    if (value.Unit == MornUICssValue.ValueUnit.Keyword)
                        Overflow = (MornUIOverflow)(int)value.Number;
                    break;
            }
        }

        public void ApplyRaw(MornUICssPropertyId propId, string rawValue)
        {
            switch (propId)
            {
                case MornUICssPropertyId.Transform:
                    Transform = MornUITransform.Parse(rawValue);
                    break;
                case MornUICssPropertyId.AnimationName:
                    AnimationName = rawValue;
                    break;
                case MornUICssPropertyId.AnimationDuration:
                    AnimationDuration = ParseTime(rawValue);
                    break;
                case MornUICssPropertyId.AnimationDelay:
                    AnimationDelay = ParseTime(rawValue);
                    break;
                case MornUICssPropertyId.AnimationTimingFunction:
                    AnimationEasing = MornUIEasing.Parse(rawValue);
                    break;
                case MornUICssPropertyId.AnimationIterationCount:
                    AnimationIterationCount = rawValue.Trim() == "infinite"
                        ? -1
                        : int.TryParse(rawValue.Trim(), out var n) ? n : 1;
                    break;
                case MornUICssPropertyId.AnimationDirection:
                    AnimationDirection = rawValue.Trim() switch
                    {
                        "reverse" => MornUIAnimationDirection.Reverse,
                        "alternate" => MornUIAnimationDirection.Alternate,
                        _ => MornUIAnimationDirection.Normal,
                    };
                    break;
                case MornUICssPropertyId.AnimationFillMode:
                    AnimationFillMode = rawValue.Trim() switch
                    {
                        "forwards" => MornUIAnimationFillMode.Forwards,
                        "backwards" => MornUIAnimationFillMode.Backwards,
                        "both" => MornUIAnimationFillMode.Both,
                        _ => MornUIAnimationFillMode.None,
                    };
                    break;
                case MornUICssPropertyId.TransitionProperty:
                    TransitionProperty = rawValue.Trim();
                    break;
                case MornUICssPropertyId.TransitionDuration:
                    TransitionDuration = ParseTime(rawValue);
                    break;
                case MornUICssPropertyId.TransitionDelay:
                    TransitionDelay = ParseTime(rawValue);
                    break;
                case MornUICssPropertyId.TransitionTimingFunction:
                    TransitionEasing = MornUIEasing.Parse(rawValue);
                    break;
                case MornUICssPropertyId.Cursor:
                    Cursor = rawValue.Trim();
                    break;
            }
        }

        private static float ParseTime(string s)
        {
            s = s.Trim();
            if (s.EndsWith("ms"))
            {
                if (float.TryParse(s.Replace("ms", ""), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var ms))
                    return ms / 1000f;
            }
            else if (s.EndsWith("s"))
            {
                if (float.TryParse(s.Replace("s", ""), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var sec))
                    return sec;
            }
            return 0f;
        }
    }
}
