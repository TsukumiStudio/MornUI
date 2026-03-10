namespace MornLib
{
    internal struct MornUILayoutRect
    {
        public float X;
        public float Y;
        public float ContentWidth;
        public float ContentHeight;

        public float PaddingBoxWidth => ContentWidth + Style.PaddingLeft + Style.PaddingRight;
        public float PaddingBoxHeight => ContentHeight + Style.PaddingTop + Style.PaddingBottom;
        public float BorderBoxWidth => PaddingBoxWidth + Style.BorderLeftWidth + Style.BorderRightWidth;
        public float BorderBoxHeight => PaddingBoxHeight + Style.BorderTopWidth + Style.BorderBottomWidth;
        public float MarginBoxWidth => BorderBoxWidth + Style.MarginLeft + Style.MarginRight;
        public float MarginBoxHeight => BorderBoxHeight + Style.MarginTop + Style.MarginBottom;

        private MornUIComputedStyle Style;

        public MornUILayoutRect(float x, float y, float contentWidth, float contentHeight,
            MornUIComputedStyle style)
        {
            X = x;
            Y = y;
            ContentWidth = contentWidth;
            ContentHeight = contentHeight;
            Style = style;
        }

        public float GlobalBorderBoxX(float parentGlobalX) =>
            parentGlobalX + X + Style.MarginLeft;

        public float GlobalBorderBoxY(float parentGlobalY) =>
            parentGlobalY + Y + Style.MarginTop;

        public float GlobalPaddingBoxX(float parentGlobalX) =>
            GlobalBorderBoxX(parentGlobalX) + Style.BorderLeftWidth;

        public float GlobalPaddingBoxY(float parentGlobalY) =>
            GlobalBorderBoxY(parentGlobalY) + Style.BorderTopWidth;

        public float GlobalContentX(float parentGlobalX) =>
            GlobalPaddingBoxX(parentGlobalX) + Style.PaddingLeft;

        public float GlobalContentY(float parentGlobalY) =>
            GlobalPaddingBoxY(parentGlobalY) + Style.PaddingTop;
    }
}
