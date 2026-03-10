using System.Collections.Generic;
using UnityEngine;

namespace MornLib
{
    internal enum MornUITransitionValueType
    {
        Float,
        Color,
        Transform,
    }

    internal sealed class MornUIRunningTransition
    {
        public MornUILayoutNode Node;
        public string Property;
        public float Duration;
        public float Delay;
        public MornUIEasingType Easing;
        public float ElapsedTime;
        public bool Finished;

        public MornUITransitionValueType ValueType;
        public float FromFloat, ToFloat;
        public Color32 FromColor, ToColor;
        public MornUITransform FromTransform, ToTransform;
    }

    internal sealed class MornUITransitionSnapshot
    {
        public float Opacity;
        public Color32 BackgroundColor;
        public Color32 TextColor;
        public Color32 BorderTopColor;
        public Color32 BorderRightColor;
        public Color32 BorderBottomColor;
        public Color32 BorderLeftColor;
        public float BorderTopLeftRadius;
        public float BorderTopRightRadius;
        public float BorderBottomRightRadius;
        public float BorderBottomLeftRadius;
        public MornUITransform Transform;

        public static MornUITransitionSnapshot Capture(MornUIComputedStyle style)
        {
            return new MornUITransitionSnapshot
            {
                Opacity = style.Opacity,
                BackgroundColor = style.BackgroundColor,
                TextColor = style.TextColor,
                BorderTopColor = style.BorderTopColor,
                BorderRightColor = style.BorderRightColor,
                BorderBottomColor = style.BorderBottomColor,
                BorderLeftColor = style.BorderLeftColor,
                BorderTopLeftRadius = style.BorderTopLeftRadius,
                BorderTopRightRadius = style.BorderTopRightRadius,
                BorderBottomRightRadius = style.BorderBottomRightRadius,
                BorderBottomLeftRadius = style.BorderBottomLeftRadius,
                Transform = style.Transform,
            };
        }
    }

    internal sealed class MornUITransitionEngine
    {
        private readonly List<MornUIRunningTransition> _transitions = new();
        private readonly Dictionary<MornUILayoutNode, MornUITransitionSnapshot> _snapshots = new();
        private bool _dirty;

        public bool IsDirty => _dirty;
        public bool HasRunning => _transitions.Count > 0;

        public void Clear()
        {
            _transitions.Clear();
            _snapshots.Clear();
            _dirty = false;
        }

        /// <summary>
        /// Save snapshots of all nodes' transitionable properties before style re-resolution.
        /// </summary>
        public void SaveSnapshots(MornUILayoutNode root)
        {
            _snapshots.Clear();
            SaveSnapshotRecursive(root);
        }

        private void SaveSnapshotRecursive(MornUILayoutNode node)
        {
            if (node.ComputedStyle != null)
            {
                // Also capture any currently-transitioning values as the "current" snapshot
                _snapshots[node] = MornUITransitionSnapshot.Capture(node.ComputedStyle);
            }

            foreach (var child in node.Children)
                SaveSnapshotRecursive(child);
        }

        /// <summary>
        /// After style re-resolution, detect changed properties and start transitions.
        /// </summary>
        public void DetectChanges(MornUILayoutNode root)
        {
            DetectChangesRecursive(root);
        }

        private void DetectChangesRecursive(MornUILayoutNode node)
        {
            var style = node.ComputedStyle;
            if (style == null || string.IsNullOrEmpty(style.TransitionProperty) || style.TransitionDuration <= 0f)
            {
                foreach (var child in node.Children)
                    DetectChangesRecursive(child);
                return;
            }

            if (!_snapshots.TryGetValue(node, out var snapshot))
            {
                foreach (var child in node.Children)
                    DetectChangesRecursive(child);
                return;
            }

            var props = style.TransitionProperty;
            var isAll = props == "all";

            // Check each transitionable property
            if (isAll || ContainsProperty(props, "opacity"))
                TryStartFloat(node, "opacity", snapshot.Opacity, style.Opacity, style);

            if (isAll || ContainsProperty(props, "background-color"))
                TryStartColor(node, "background-color", snapshot.BackgroundColor, style.BackgroundColor, style);

            if (isAll || ContainsProperty(props, "color"))
                TryStartColor(node, "color", snapshot.TextColor, style.TextColor, style);

            if (isAll || ContainsProperty(props, "border-color"))
            {
                TryStartColor(node, "border-top-color", snapshot.BorderTopColor, style.BorderTopColor, style);
                TryStartColor(node, "border-right-color", snapshot.BorderRightColor, style.BorderRightColor, style);
                TryStartColor(node, "border-bottom-color", snapshot.BorderBottomColor, style.BorderBottomColor, style);
                TryStartColor(node, "border-left-color", snapshot.BorderLeftColor, style.BorderLeftColor, style);
            }

            if (isAll || ContainsProperty(props, "border-radius"))
            {
                TryStartFloat(node, "border-top-left-radius", snapshot.BorderTopLeftRadius, style.BorderTopLeftRadius, style);
                TryStartFloat(node, "border-top-right-radius", snapshot.BorderTopRightRadius, style.BorderTopRightRadius, style);
                TryStartFloat(node, "border-bottom-right-radius", snapshot.BorderBottomRightRadius, style.BorderBottomRightRadius, style);
                TryStartFloat(node, "border-bottom-left-radius", snapshot.BorderBottomLeftRadius, style.BorderBottomLeftRadius, style);
            }

            if (isAll || ContainsProperty(props, "transform"))
                TryStartTransform(node, snapshot.Transform, style.Transform, style);

            foreach (var child in node.Children)
                DetectChangesRecursive(child);
        }

        private static bool ContainsProperty(string transitionProperty, string property)
        {
            if (transitionProperty == property) return true;
            // Handle comma-separated list: "background-color, transform"
            foreach (var part in transitionProperty.Split(','))
            {
                if (part.Trim() == property) return true;
            }
            return false;
        }

        private void TryStartFloat(MornUILayoutNode node, string property,
            float from, float to, MornUIComputedStyle style)
        {
            if (Mathf.Approximately(from, to)) return;

            // Cancel existing transition for this property on this node
            CancelTransition(node, property);

            _transitions.Add(new MornUIRunningTransition
            {
                Node = node,
                Property = property,
                Duration = style.TransitionDuration,
                Delay = style.TransitionDelay,
                Easing = style.TransitionEasing,
                ElapsedTime = 0f,
                Finished = false,
                ValueType = MornUITransitionValueType.Float,
                FromFloat = from,
                ToFloat = to,
            });
        }

        private void TryStartColor(MornUILayoutNode node, string property,
            Color32 from, Color32 to, MornUIComputedStyle style)
        {
            if (from.r == to.r && from.g == to.g && from.b == to.b && from.a == to.a) return;

            CancelTransition(node, property);

            _transitions.Add(new MornUIRunningTransition
            {
                Node = node,
                Property = property,
                Duration = style.TransitionDuration,
                Delay = style.TransitionDelay,
                Easing = style.TransitionEasing,
                ElapsedTime = 0f,
                Finished = false,
                ValueType = MornUITransitionValueType.Color,
                FromColor = from,
                ToColor = to,
            });
        }

        private void TryStartTransform(MornUILayoutNode node,
            MornUITransform from, MornUITransform to, MornUIComputedStyle style)
        {
            if (from == to) return;
            // Compare by values
            if (from != null && to != null &&
                Mathf.Approximately(from.GetTranslateX(), to.GetTranslateX()) &&
                Mathf.Approximately(from.GetTranslateY(), to.GetTranslateY()) &&
                Mathf.Approximately(from.GetScaleX(), to.GetScaleX()) &&
                Mathf.Approximately(from.GetScaleY(), to.GetScaleY()) &&
                Mathf.Approximately(from.GetRotation(), to.GetRotation()))
                return;

            CancelTransition(node, "transform");

            _transitions.Add(new MornUIRunningTransition
            {
                Node = node,
                Property = "transform",
                Duration = style.TransitionDuration,
                Delay = style.TransitionDelay,
                Easing = style.TransitionEasing,
                ElapsedTime = 0f,
                Finished = false,
                ValueType = MornUITransitionValueType.Transform,
                FromTransform = from ?? MornUITransform.Identity,
                ToTransform = to ?? MornUITransform.Identity,
            });
        }

        private void CancelTransition(MornUILayoutNode node, string property)
        {
            _transitions.RemoveAll(t => t.Node == node && t.Property == property);
        }

        /// <summary>
        /// Advance all running transitions and apply interpolated values to ComputedStyles.
        /// </summary>
        public void Update(float deltaTime)
        {
            if (_transitions.Count == 0) return;
            _dirty = false;

            foreach (var tr in _transitions)
            {
                if (tr.Finished) continue;

                tr.ElapsedTime += deltaTime;
                var elapsed = tr.ElapsedTime - tr.Delay;

                if (elapsed < 0f)
                {
                    // Still in delay, apply from value
                    ApplyValue(tr, 0f);
                    _dirty = true;
                    continue;
                }

                if (tr.Duration <= 0f)
                {
                    ApplyValue(tr, 1f);
                    tr.Finished = true;
                    _dirty = true;
                    continue;
                }

                var progress = elapsed / tr.Duration;
                if (progress >= 1f)
                {
                    ApplyValue(tr, 1f);
                    tr.Finished = true;
                    _dirty = true;
                    continue;
                }

                var t = MornUIEasing.Evaluate(tr.Easing, progress);
                ApplyValue(tr, t);
                _dirty = true;
            }

            _transitions.RemoveAll(t => t.Finished);
        }

        private static void ApplyValue(MornUIRunningTransition tr, float t)
        {
            var style = tr.Node.ComputedStyle;
            if (style == null) return;

            switch (tr.ValueType)
            {
                case MornUITransitionValueType.Float:
                    var lerped = Mathf.Lerp(tr.FromFloat, tr.ToFloat, t);
                    switch (tr.Property)
                    {
                        case "opacity":
                            style.Opacity = lerped;
                            break;
                        case "border-top-left-radius":
                            style.BorderTopLeftRadius = lerped;
                            break;
                        case "border-top-right-radius":
                            style.BorderTopRightRadius = lerped;
                            break;
                        case "border-bottom-right-radius":
                            style.BorderBottomRightRadius = lerped;
                            break;
                        case "border-bottom-left-radius":
                            style.BorderBottomLeftRadius = lerped;
                            break;
                    }
                    break;

                case MornUITransitionValueType.Color:
                    var c = LerpColor(tr.FromColor, tr.ToColor, t);
                    switch (tr.Property)
                    {
                        case "background-color":
                            style.BackgroundColor = c;
                            break;
                        case "color":
                            style.TextColor = c;
                            break;
                        case "border-top-color":
                            style.BorderTopColor = c;
                            break;
                        case "border-right-color":
                            style.BorderRightColor = c;
                            break;
                        case "border-bottom-color":
                            style.BorderBottomColor = c;
                            break;
                        case "border-left-color":
                            style.BorderLeftColor = c;
                            break;
                    }
                    break;

                case MornUITransitionValueType.Transform:
                    style.Transform = MornUITransform.Lerp(tr.FromTransform, tr.ToTransform, t);
                    break;
            }
        }

        private static Color32 LerpColor(Color32 a, Color32 b, float t)
        {
            return new Color32(
                (byte)Mathf.RoundToInt(Mathf.Lerp(a.r, b.r, t)),
                (byte)Mathf.RoundToInt(Mathf.Lerp(a.g, b.g, t)),
                (byte)Mathf.RoundToInt(Mathf.Lerp(a.b, b.b, t)),
                (byte)Mathf.RoundToInt(Mathf.Lerp(a.a, b.a, t)));
        }
    }
}
