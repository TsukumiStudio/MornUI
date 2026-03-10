using System;
using System.Collections.Generic;
using Jint;
using Jint.Constraints;
using Jint.Native;
using UnityEngine;

namespace MornLib
{
    internal sealed class MornUIJsEngine
    {
        private Engine _engine;
        private MornUILayoutNode _root;
        private MornUIEventSystem _eventSystem;
        private readonly Dictionary<string, Action<JsValue[]>> _csharpMethods = new();

        public void Setup(MornUILayoutNode root, MornUIEventSystem eventSystem)
        {
            _root = root;
            _eventSystem = eventSystem;

            _engine = new Engine(cfg => cfg
                .LimitRecursion(64)
            );

            // document object
            _engine.SetValue("document", new DocumentProxy(this));

            // console object
            _engine.SetValue("console", new ConsoleProxy());

            // MornUI bridge object
            _engine.SetValue("MornUI", new MornUIBridge(this));
        }

        public void Execute(string js)
        {
            if (_engine == null) return;
            try
            {
                _engine.Execute(js);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MornUI JS] {e.Message}");
            }
        }

        public void DispatchClickEvent(MornUILayoutNode target)
        {
            if (_engine == null) return;

            // Execute inline onclick attribute (with bubbling)
            var node = target;
            while (node != null)
            {
                if (!string.IsNullOrEmpty(node.OnClick))
                {
                    Execute(node.OnClick);
                }

                node = node.Parent;
            }

            // Dispatch to addEventListener handlers
            _eventSystem?.DispatchEvent(target, "click");
        }

        public void RegisterMethod(string name, Action<JsValue[]> handler)
        {
            _csharpMethods[name] = handler;
        }

        internal void InvokeCSharpMethod(string name, JsValue[] args)
        {
            if (_csharpMethods.TryGetValue(name, out var handler))
            {
                handler.Invoke(args);
            }
            else
            {
                Debug.LogWarning($"[MornUI] C# method '{name}' not registered.");
            }
        }

        /// <summary>
        /// Convert JsValue[] to object[] for C# bridge.
        /// </summary>
        internal static object[] ConvertArgs(JsValue[] jsArgs)
        {
            var args = new object[jsArgs.Length];
            for (var i = 0; i < jsArgs.Length; i++)
            {
                var v = jsArgs[i];
                if (v.IsNull() || v.IsUndefined()) args[i] = null;
                else if (v.IsString()) args[i] = v.AsString();
                else if (v.IsNumber()) args[i] = v.AsNumber();
                else if (v.IsBoolean()) args[i] = v.AsBoolean();
                else args[i] = v.ToString();
            }

            return args;
        }

        internal MornUILayoutNode FindById(string id)
        {
            return FindByIdRecursive(_root, id);
        }

        internal MornUILayoutNode FindBySelector(string selector)
        {
            if (selector.StartsWith("#"))
                return FindById(selector.Substring(1));
            if (selector.StartsWith("."))
                return FindByClassRecursive(_root, selector.Substring(1));
            return FindByTagRecursive(_root, selector);
        }

        internal List<MornUILayoutNode> FindAllBySelector(string selector)
        {
            var results = new List<MornUILayoutNode>();
            if (selector.StartsWith("#"))
            {
                var node = FindById(selector.Substring(1));
                if (node != null) results.Add(node);
            }
            else if (selector.StartsWith("."))
            {
                FindAllByClassRecursive(_root, selector.Substring(1), results);
            }
            else
            {
                FindAllByTagRecursive(_root, selector, results);
            }

            return results;
        }

        internal void AddEventListenerFromJs(MornUILayoutNode node, string eventType, JsValue callback)
        {
            if (_eventSystem == null) return;
            if (!callback.IsObject()) return;

            var engine = _engine;
            _eventSystem.AddEventListener(node, eventType, () =>
            {
                try
                {
                    engine.Invoke(callback, new object[0]);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[MornUI JS] Event handler error: {e.Message}");
                }
            });
        }

        private static MornUILayoutNode FindByIdRecursive(MornUILayoutNode node, string id)
        {
            if (node.Id == id) return node;
            foreach (var child in node.Children)
            {
                var result = FindByIdRecursive(child, id);
                if (result != null) return result;
            }

            return null;
        }

        private static MornUILayoutNode FindByClassRecursive(MornUILayoutNode node, string className)
        {
            if (node.ClassList.Contains(className)) return node;
            foreach (var child in node.Children)
            {
                var result = FindByClassRecursive(child, className);
                if (result != null) return result;
            }

            return null;
        }

        private static MornUILayoutNode FindByTagRecursive(MornUILayoutNode node, string tagName)
        {
            if (node.TagName == tagName) return node;
            foreach (var child in node.Children)
            {
                var result = FindByTagRecursive(child, tagName);
                if (result != null) return result;
            }

            return null;
        }

        private static void FindAllByClassRecursive(MornUILayoutNode node, string className,
            List<MornUILayoutNode> results)
        {
            if (node.ClassList.Contains(className)) results.Add(node);
            foreach (var child in node.Children)
                FindAllByClassRecursive(child, className, results);
        }

        private static void FindAllByTagRecursive(MornUILayoutNode node, string tagName,
            List<MornUILayoutNode> results)
        {
            if (node.TagName == tagName) results.Add(node);
            foreach (var child in node.Children)
                FindAllByTagRecursive(child, tagName, results);
        }

        // --- Proxy classes for JS interop ---

        private sealed class DocumentProxy
        {
            private readonly MornUIJsEngine _engine;

            public DocumentProxy(MornUIJsEngine engine)
            {
                _engine = engine;
            }

            public ElementProxy getElementById(string id)
            {
                var node = _engine.FindById(id);
                return node != null ? new ElementProxy(node, _engine) : null;
            }

            public ElementProxy querySelector(string selector)
            {
                var node = _engine.FindBySelector(selector);
                return node != null ? new ElementProxy(node, _engine) : null;
            }

            public ElementProxy[] querySelectorAll(string selector)
            {
                var nodes = _engine.FindAllBySelector(selector);
                var proxies = new ElementProxy[nodes.Count];
                for (var i = 0; i < nodes.Count; i++)
                    proxies[i] = new ElementProxy(nodes[i], _engine);
                return proxies;
            }
        }

        private sealed class ElementProxy
        {
            private readonly MornUILayoutNode _node;
            private readonly MornUIJsEngine _engine;

            public ElementProxy(MornUILayoutNode node, MornUIJsEngine engine)
            {
                _node = node;
                _engine = engine;
            }

            public string id => _node.Id ?? "";
            public string tagName => _node.TagName?.ToUpperInvariant() ?? "";

            public string textContent
            {
                get => _node.TextContent ?? "";
                set => _node.TextContent = value;
            }

            public StyleProxy style => new(_node);

            public ClassListProxy classList => new(_node);

            public void addEventListener(string type, JsValue callback)
            {
                _engine.AddEventListenerFromJs(_node, type, callback);
            }

            public string getAttribute(string name)
            {
                if (_node.Attributes != null && _node.Attributes.TryGetValue(name, out var val))
                    return val;
                return null;
            }

            public void setAttribute(string name, string value)
            {
                _node.Attributes ??= new Dictionary<string, string>();
                _node.Attributes[name] = value;
            }
        }

        private sealed class StyleProxy
        {
            private readonly MornUILayoutNode _node;

            public StyleProxy(MornUILayoutNode node)
            {
                _node = node;
            }

            public string backgroundColor
            {
                get => GetProp("background-color");
                set => SetProp("background-color", value);
            }

            public string color
            {
                get => GetProp("color");
                set => SetProp("color", value);
            }

            public string width
            {
                get => GetProp("width");
                set => SetProp("width", value);
            }

            public string height
            {
                get => GetProp("height");
                set => SetProp("height", value);
            }

            public string opacity
            {
                get => GetProp("opacity");
                set => SetProp("opacity", value);
            }

            public string display
            {
                get => GetProp("display");
                set => SetProp("display", value);
            }

            public string borderColor
            {
                get => GetProp("border-color");
                set => SetProp("border-color", value);
            }

            public string transform
            {
                get => GetProp("transform");
                set => SetProp("transform", value);
            }

            private string GetProp(string name)
            {
                if (_node.Attributes != null && _node.Attributes.TryGetValue("style", out var s))
                {
                    foreach (var part in s.Split(';'))
                    {
                        var trimmed = part.Trim();
                        var colon = trimmed.IndexOf(':');
                        if (colon < 0) continue;
                        if (trimmed.Substring(0, colon).Trim() == name)
                            return trimmed.Substring(colon + 1).Trim();
                    }
                }

                return "";
            }

            private void SetProp(string name, string value)
            {
                var existing = _node.Attributes != null && _node.Attributes.TryGetValue("style", out var s) ? s : "";
                var props = new Dictionary<string, string>();
                foreach (var part in existing.Split(';'))
                {
                    var trimmed = part.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;
                    var colon = trimmed.IndexOf(':');
                    if (colon < 0) continue;
                    props[trimmed.Substring(0, colon).Trim()] = trimmed.Substring(colon + 1).Trim();
                }

                props[name] = value;

                var sb = new System.Text.StringBuilder();
                foreach (var kv in props)
                {
                    if (sb.Length > 0) sb.Append("; ");
                    sb.Append(kv.Key).Append(": ").Append(kv.Value);
                }

                _node.Attributes ??= new Dictionary<string, string>();
                _node.Attributes["style"] = sb.ToString();
            }
        }

        private sealed class ClassListProxy
        {
            private readonly MornUILayoutNode _node;

            public ClassListProxy(MornUILayoutNode node)
            {
                _node = node;
            }

            public void add(string className)
            {
                if (!_node.ClassList.Contains(className))
                    _node.ClassList.Add(className);
            }

            public void remove(string className)
            {
                _node.ClassList.Remove(className);
            }

            public bool toggle(string className)
            {
                if (_node.ClassList.Contains(className))
                {
                    _node.ClassList.Remove(className);
                    return false;
                }

                _node.ClassList.Add(className);
                return true;
            }

            public bool contains(string className)
            {
                return _node.ClassList.Contains(className);
            }
        }

        private sealed class ConsoleProxy
        {
            public void log(params object[] args) => Debug.Log("[MornUI JS] " + string.Join(" ", args));
            public void warn(params object[] args) => Debug.LogWarning("[MornUI JS] " + string.Join(" ", args));
            public void error(params object[] args) => Debug.LogError("[MornUI JS] " + string.Join(" ", args));
        }

        private sealed class MornUIBridge
        {
            private readonly MornUIJsEngine _engine;

            public MornUIBridge(MornUIJsEngine engine)
            {
                _engine = engine;
            }

            public void call(string methodName, params JsValue[] args)
            {
                _engine.InvokeCSharpMethod(methodName, args);
            }
        }
    }
}
