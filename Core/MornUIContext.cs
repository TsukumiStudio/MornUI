using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace MornLib
{
    public sealed class MornUIContext
    {
        private readonly MornUICssParser _cssParser = new();
        private readonly MornUIStyleResolver _styleResolver = new();
        private readonly MornUILayoutEngine _layoutEngine = new();
        private readonly MornUIRenderer _renderer = new();
        private readonly MornUIJsEngine _jsEngine = new();
        private readonly MornUIEventSystem _eventSystem = new();
        private readonly MornUIAnimationEngine _animationEngine = new();
        private readonly MornUITransitionEngine _transitionEngine = new();

        private MornUILayoutNode _root;
        private List<MornUICssRule> _rules;
        private int _width;
        private int _height;
        private bool _dirty;
        private MornUILayoutNode _focusedNode;

        public TMP_FontAsset DefaultFont
        {
            get => _renderer.DefaultFont;
            set => _renderer.DefaultFont = value;
        }

        public bool HasRunningAnimations => _animationEngine.HasRunning || _transitionEngine.HasRunning;

        public Texture Render(string html, string css, int width, int height)
        {
            MornUITextPainter.Cleanup();
            _eventSystem.Clear();
            _animationEngine.Clear();
            _transitionEngine.Clear();

            _root = MornUIDocument.Parse(html);
            _width = width;
            _height = height;

            _rules = string.IsNullOrEmpty(css)
                ? new List<MornUICssRule>()
                : _cssParser.Parse(css);

            // Register @keyframes rules
            foreach (var kf in _cssParser.KeyframeRules)
                _animationEngine.RegisterKeyframes(kf);

            var font = DefaultFont;
            if (font == null && MornUIGlobal.I != null)
                font = MornUIGlobal.I.DefaultFont;

            if (font != null)
            {
                PrepopulateFontAtlas(_root, font);
            }

            _styleResolver.Resolve(_root, _rules, width, height);
            _renderer.DefaultFont = font;
            _layoutEngine.DefaultFont = font;
            _layoutEngine.Calculate(_root, width, height);

            // Setup animations after style resolution
            _animationEngine.SetupAnimations(_root);

            // Apply initial animation frame (t=0) before first render
            // to avoid a flash of unanimated state
            if (_animationEngine.HasRunning)
            {
                _animationEngine.Update(0f);
                _layoutEngine.Calculate(_root, width, height);
            }

            // Setup JS engine after layout is calculated
            _jsEngine.Setup(_root, _eventSystem);

            _dirty = false;
            return _renderer.Render(_root, width, height);
        }

        public void ExecuteJS(string js)
        {
            _jsEngine.Execute(js);
        }

        public void RegisterMethod(string name, Action<object[]> handler)
        {
            _jsEngine.RegisterMethod(name, jsArgs =>
            {
                var args = MornUIJsEngine.ConvertArgs(jsArgs);
                handler(args);
            });
        }

        public bool HandleClick(float x, float y)
        {
            if (_root == null) return false;
            var hit = MornUIHitTester.HitTest(_root, x, y);
            if (hit == null) return false;

            // Set focus to the clicked focusable element (or its nearest focusable ancestor)
            var focusTarget = FindFocusableAncestor(hit);
            SetFocus(focusTarget);

            _jsEngine.DispatchClickEvent(hit);
            return true;
        }

        /// <summary>
        /// Move focus to next (forward=true) or previous (forward=false) focusable element.
        /// Returns true if focus changed.
        /// </summary>
        public bool MoveFocus(bool forward)
        {
            if (_root == null) return false;

            var focusables = new List<MornUILayoutNode>();
            CollectFocusableNodes(_root, focusables);
            if (focusables.Count == 0) return false;

            var currentIndex = _focusedNode != null ? focusables.IndexOf(_focusedNode) : -1;
            int nextIndex;
            if (forward)
                nextIndex = currentIndex < 0 ? 0 : (currentIndex + 1) % focusables.Count;
            else
                nextIndex = currentIndex < 0
                    ? focusables.Count - 1
                    : (currentIndex - 1 + focusables.Count) % focusables.Count;

            SetFocus(focusables[nextIndex]);
            return true;
        }

        private void SetFocus(MornUILayoutNode node)
        {
            if (_focusedNode == node) return;

            if (_focusedNode != null)
            {
                _focusedNode.IsFocused = false;
                _eventSystem.DispatchEvent(_focusedNode, "blur");
            }

            _focusedNode = node;

            if (_focusedNode != null)
            {
                _focusedNode.IsFocused = true;
                _eventSystem.DispatchEvent(_focusedNode, "focus");
            }
        }

        private static MornUILayoutNode FindFocusableAncestor(MornUILayoutNode node)
        {
            var current = node;
            while (current != null)
            {
                if (IsFocusable(current)) return current;
                current = current.Parent;
            }

            return null;
        }

        private static bool IsFocusable(MornUILayoutNode node)
        {
            // Elements with cursor: pointer, buttons, inputs, or elements with onclick are focusable
            if (node.TagName is "button" or "input" or "select" or "a")
                return true;
            if (!string.IsNullOrEmpty(node.OnClick))
                return true;
            if (node.ComputedStyle?.Cursor == "pointer")
                return true;
            if (node.Attributes != null && node.Attributes.ContainsKey("tabindex"))
                return true;
            return false;
        }

        private static void CollectFocusableNodes(MornUILayoutNode node, List<MornUILayoutNode> result)
        {
            if (node.ComputedStyle?.Display == MornUIDisplay.None) return;
            if (IsFocusable(node))
                result.Add(node);
            foreach (var child in node.Children)
                CollectFocusableNodes(child, result);
        }

        public bool IsDirty => _dirty;

        /// <summary>
        /// Lightweight: resolve styles and detect transitions, but skip the expensive render.
        /// The actual render will happen on the next UpdateAnimations call.
        /// </summary>
        public void ApplyStyleChanges()
        {
            if (_root == null) return;

            _transitionEngine.SaveSnapshots(_root);
            _styleResolver.Resolve(_root, _rules, _width, _height);
            _transitionEngine.DetectChanges(_root);
            _dirty = true;
        }

        public Texture ReRender()
        {
            if (_root == null) return null;

            var font = DefaultFont;
            if (font == null && MornUIGlobal.I != null)
                font = MornUIGlobal.I.DefaultFont;

            _transitionEngine.SaveSnapshots(_root);
            _styleResolver.Resolve(_root, _rules, _width, _height);
            _transitionEngine.DetectChanges(_root);

            if (_transitionEngine.HasRunning)
                _transitionEngine.Update(1f / 60f);

            _renderer.DefaultFont = font;
            _layoutEngine.DefaultFont = font;
            _layoutEngine.Calculate(_root, _width, _height);
            _dirty = false;
            return _renderer.Render(_root, _width, _height);
        }

        public Texture UpdateAnimations(float deltaTime)
        {
            if (_root == null) return null;

            // If dirty (e.g. after click/focus change), resolve styles and detect transitions
            var wasDirty = _dirty;
            if (wasDirty)
            {
                _dirty = false;
                _transitionEngine.SaveSnapshots(_root);
                _styleResolver.Resolve(_root, _rules, _width, _height);
                _transitionEngine.DetectChanges(_root);
            }

            _animationEngine.Update(deltaTime);
            _transitionEngine.Update(deltaTime);

            if (!wasDirty && !_animationEngine.IsDirty && !_transitionEngine.IsDirty) return null;

            var font = DefaultFont;
            if (font == null && MornUIGlobal.I != null)
                font = MornUIGlobal.I.DefaultFont;

            _renderer.DefaultFont = font;
            _layoutEngine.DefaultFont = font;
            _layoutEngine.Calculate(_root, _width, _height);
            return _renderer.Render(_root, _width, _height);
        }

        public void MarkDirty()
        {
            _dirty = true;
        }

        public void Cleanup()
        {
            MornUITextPainter.Cleanup();
            _renderer.Cleanup();
            _eventSystem.Clear();
            _animationEngine.Clear();
            _transitionEngine.Clear();
        }

        private static void PrepopulateFontAtlas(MornUILayoutNode node, TMP_FontAsset font)
        {
            var sb = new StringBuilder();
            CollectAllText(node, sb);
            if (sb.Length == 0) return;

            font.TryAddCharacters(sb.ToString(), out _);

            if (font.atlasTexture is Texture2D atlas)
            {
                atlas.Apply(false);
            }
        }

        private static void CollectAllText(MornUILayoutNode node, StringBuilder sb)
        {
            if (!string.IsNullOrEmpty(node.TextContent))
            {
                sb.Append(node.TextContent);
            }

            foreach (var child in node.Children)
            {
                CollectAllText(child, sb);
            }
        }
    }
}
