using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MornLib
{
    [RequireComponent(typeof(RawImage))]
    public sealed class MornUIMono : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField]
        [TextArea(10, 30)]
        private string _html = "<div style=\"width:400px;height:300px;background-color:#333333;padding:20px;\">\n  <div style=\"width:100px;height:100px;background-color:#e94560;margin-bottom:10px;\"></div>\n  <div style=\"width:200px;height:80px;background-color:#0f3460;\"></div>\n</div>";

        [SerializeField]
        [TextArea(5, 20)]
        private string _css = "";

        [SerializeField]
        [TextArea(5, 20)]
        private string _js = "";

        [SerializeField]
        private int _width = 800;

        [SerializeField]
        private int _height = 600;

        [SerializeField]
        private TMP_FontAsset _font;

        private MornUIContext _context;
        private RawImage _rawImage;
        private Texture _texture;

        /// <summary>
        /// Register a C# method callable from JS via MornUI.call(name, ...args).
        /// </summary>
        public void RegisterMethod(string name, Action<object[]> handler)
        {
            _context?.RegisterMethod(name, handler);
        }

        /// <summary>
        /// Execute JavaScript in the UI context.
        /// </summary>
        public void ExecuteJS(string js)
        {
            _context?.ExecuteJS(js);
        }

        private void Start()
        {
            _rawImage = GetComponent<RawImage>();
            _context = new MornUIContext();
            _context.DefaultFont = _font;
            RenderUI();

            // Execute initial JS if provided
            if (!string.IsNullOrEmpty(_js))
            {
                _context.ExecuteJS(_js);
            }
        }

        public void RenderUI()
        {
            _texture = _context.Render(_html, _css, _width, _height);
            _rawImage.texture = _texture;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_context == null || _rawImage == null) return;

            // Convert screen position to local RawImage coordinates
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rawImage.rectTransform, eventData.position, eventData.pressEventCamera, out var localPoint))
                return;

            var rectTransform = _rawImage.rectTransform;
            var rect = rectTransform.rect;

            // Normalize to 0-1 UV space
            var uvX = (localPoint.x - rect.x) / rect.width;
            var uvY = (localPoint.y - rect.y) / rect.height;

            // Convert to layout coordinates (top-left origin, Y down)
            var layoutX = uvX * _width;
            var layoutY = (1f - uvY) * _height;

            if (_context.HandleClick(layoutX, layoutY))
            {
                _context.MarkDirty();
            }
        }

        private void Update()
        {
            if (_context == null) return;

            // Tab / Shift+Tab focus navigation
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                var forward = !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift);
                if (_context.MoveFocus(forward))
                    _context.MarkDirty();
            }

            if (!_context.HasRunningAnimations && !_context.IsDirty) return;

            var newTexture = _context.UpdateAnimations(Time.deltaTime);
            if (newTexture != null)
            {
                _texture = newTexture;
                _rawImage.texture = _texture;
            }
        }

        private void OnDestroy()
        {
            _texture = null;
            _context?.Cleanup();
        }
    }
}
