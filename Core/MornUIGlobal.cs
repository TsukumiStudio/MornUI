using TMPro;
using UnityEngine;

namespace MornLib
{
    [CreateAssetMenu(fileName = nameof(MornUIGlobal), menuName = "Morn/" + nameof(MornUIGlobal))]
    public sealed class MornUIGlobal : MornGlobalBase<MornUIGlobal>
    {
        [Header("Font")]
        [SerializeField] private TMP_FontAsset _defaultFont;

        [Header("Renderer")]
        [SerializeField] private Color _clearColor = new Color32(34, 34, 34, 255);
        [SerializeField] private FilterMode _filterMode = FilterMode.Bilinear;

        [Header("Text")]
        [Tooltip("Supersample scale for text rendering (1 = normal, 2 = 2x AA)")]
        [SerializeField] [Range(1, 4)] private int _textSupersample = 2;
        [Tooltip("Gamma correction for text weight (lower = bolder, 1.0 = no correction)")]
        [SerializeField] [Range(0.3f, 1.0f)] private float _textGamma = 0.7f;
        [Tooltip("Extra pixels around text render target to prevent SDF edge clipping")]
        [SerializeField] [Range(0, 16)] private int _textRenderPadding = 4;
        [Tooltip("Layer used for off-screen TMP text rendering")]
        [SerializeField] [Range(0, 31)] private int _textRenderLayer = 31;

        protected override string ModuleName => "MornUI";

        public TMP_FontAsset DefaultFont => _defaultFont;
        public Color32 ClearColor => _clearColor;
        public FilterMode FilterMode => _filterMode;
        public int TextSupersample => _textSupersample;
        public float TextGamma => _textGamma;
        public int TextRenderPadding => _textRenderPadding;
        public int TextRenderLayer => _textRenderLayer;
    }
}
