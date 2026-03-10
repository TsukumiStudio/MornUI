using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MornLib
{
    internal enum MornUIAnimationDirection
    {
        Normal,
        Reverse,
        Alternate,
    }

    internal enum MornUIAnimationFillMode
    {
        None,
        Forwards,
        Backwards,
        Both,
    }

    internal sealed class MornUIRunningAnimation
    {
        public MornUILayoutNode Node;
        public MornUIKeyframeRule Keyframes;
        public float Duration;
        public float Delay;
        public MornUIEasingType Easing;
        public int IterationCount; // -1 = infinite
        public MornUIAnimationDirection Direction;
        public MornUIAnimationFillMode FillMode;
        public float StartTime;
        public bool Finished;
    }

    internal sealed class MornUIAnimationEngine
    {
        private readonly List<MornUIRunningAnimation> _animations = new();
        private readonly Dictionary<string, MornUIKeyframeRule> _keyframeRules = new();
        private float _time;
        private bool _dirty;

        public bool IsDirty => _dirty;
        public bool HasRunning => _animations.Count > 0;

        public void Clear()
        {
            _animations.Clear();
            _keyframeRules.Clear();
            _time = 0f;
            _dirty = false;
        }

        public void RegisterKeyframes(MornUIKeyframeRule rule)
        {
            _keyframeRules[rule.Name] = rule;
        }

        public void SetupAnimations(MornUILayoutNode root)
        {
            _animations.Clear();
            _time = 0f;
            CollectAnimations(root);
        }

        public void Update(float deltaTime)
        {
            if (_animations.Count == 0) return;
            _time += deltaTime;
            _dirty = false;

            foreach (var anim in _animations)
            {
                if (anim.Finished) continue;

                var elapsed = _time - anim.StartTime - anim.Delay;
                if (elapsed < 0f)
                {
                    // Before delay: apply backwards fill
                    if (anim.FillMode == MornUIAnimationFillMode.Backwards ||
                        anim.FillMode == MornUIAnimationFillMode.Both)
                    {
                        ApplyKeyframe(anim, 0f);
                        _dirty = true;
                    }
                    continue;
                }

                if (anim.Duration <= 0f)
                {
                    ApplyKeyframe(anim, 1f);
                    anim.Finished = true;
                    _dirty = true;
                    continue;
                }

                var rawProgress = elapsed / anim.Duration;
                var iteration = Mathf.FloorToInt(rawProgress);

                if (anim.IterationCount >= 0 && iteration >= anim.IterationCount)
                {
                    // Animation finished
                    anim.Finished = true;
                    if (anim.FillMode == MornUIAnimationFillMode.Forwards ||
                        anim.FillMode == MornUIAnimationFillMode.Both)
                    {
                        var finalT = (anim.IterationCount % 2 == 0 && anim.Direction == MornUIAnimationDirection.Alternate)
                            ? 0f : 1f;
                        ApplyKeyframe(anim, finalT);
                    }
                    _dirty = true;
                    continue;
                }

                var t = rawProgress - iteration; // 0..1 within iteration
                if (anim.Direction == MornUIAnimationDirection.Reverse)
                    t = 1f - t;
                else if (anim.Direction == MornUIAnimationDirection.Alternate)
                    t = (iteration % 2 == 0) ? t : 1f - t;

                t = MornUIEasing.Evaluate(anim.Easing, t);
                ApplyKeyframe(anim, t);
                _dirty = true;
            }

            // Remove finished animations
            _animations.RemoveAll(a => a.Finished);
        }

        private void ApplyKeyframe(MornUIRunningAnimation anim, float t)
        {
            var stops = anim.Keyframes.Stops;
            if (stops.Count == 0) return;

            // Find surrounding keyframe stops
            MornUIKeyframeStop from = stops[0];
            MornUIKeyframeStop to = stops[stops.Count - 1];

            for (var i = 0; i < stops.Count - 1; i++)
            {
                if (t >= stops[i].Percentage && t <= stops[i + 1].Percentage)
                {
                    from = stops[i];
                    to = stops[i + 1];
                    break;
                }
            }

            var range = to.Percentage - from.Percentage;
            var localT = range > 0f ? (t - from.Percentage) / range : 1f;

            var style = anim.Node.ComputedStyle;

            // Interpolate each property
            foreach (var kvp in to.Properties)
            {
                var prop = kvp.Key;
                var toVal = kvp.Value;
                from.Properties.TryGetValue(prop, out var fromVal);

                if (prop == "transform")
                {
                    var fromTf = MornUITransform.Parse(fromVal);
                    var toTf = MornUITransform.Parse(toVal);
                    style.Transform = MornUITransform.Lerp(fromTf, toTf, localT);
                }
                else if (prop == "opacity")
                {
                    var f = ParseFloat(fromVal, style.Opacity);
                    var tov = ParseFloat(toVal, style.Opacity);
                    style.Opacity = Mathf.Lerp(f, tov, localT);
                }
                else if (prop == "background-color")
                {
                    var fc = ParseColor(fromVal, style.BackgroundColor);
                    var tc = ParseColor(toVal, style.BackgroundColor);
                    style.BackgroundColor = LerpColor(fc, tc, localT);
                }
                else if (prop == "color")
                {
                    var fc = ParseColor(fromVal, style.TextColor);
                    var tc = ParseColor(toVal, style.TextColor);
                    style.TextColor = LerpColor(fc, tc, localT);
                }
                else if (prop == "width")
                {
                    var f = ParseFloat(fromVal, 0f);
                    var tov = ParseFloat(toVal, 0f);
                    style.Width = MornUICssValue.Px(Mathf.Lerp(f, tov, localT));
                }
                else if (prop == "height")
                {
                    var f = ParseFloat(fromVal, 0f);
                    var tov = ParseFloat(toVal, 0f);
                    style.Height = MornUICssValue.Px(Mathf.Lerp(f, tov, localT));
                }
            }
        }

        private void CollectAnimations(MornUILayoutNode node)
        {
            var style = node.ComputedStyle;
            if (!string.IsNullOrEmpty(style.AnimationName) && style.AnimationName != "none")
            {
                if (_keyframeRules.TryGetValue(style.AnimationName, out var rule))
                {
                    _animations.Add(new MornUIRunningAnimation
                    {
                        Node = node,
                        Keyframes = rule,
                        Duration = style.AnimationDuration,
                        Delay = style.AnimationDelay,
                        Easing = style.AnimationEasing,
                        IterationCount = style.AnimationIterationCount,
                        Direction = style.AnimationDirection,
                        FillMode = style.AnimationFillMode,
                        StartTime = 0f,
                        Finished = false,
                    });
                }
            }

            foreach (var child in node.Children)
                CollectAnimations(child);
        }

        private static float ParseFloat(string s, float fallback)
        {
            if (string.IsNullOrEmpty(s)) return fallback;
            s = s.Trim().Replace("px", "").Replace("%", "");
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;
        }

        private static Color32 ParseColor(string s, Color32 fallback)
        {
            if (string.IsNullOrEmpty(s)) return fallback;
            var v = MornUICssValue.Parse(s.Trim());
            return v.Unit == MornUICssValue.ValueUnit.Color ? v.ColorVal : fallback;
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
