// Small dependency-free visual helper. Does not implement gameplay, navigation or task completion.
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EarlyRush.UIKit
{
    [DisallowMultipleComponent]
    public sealed class EarlyRushUITween : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public enum ClockMode { Scaled, Unscaled, Manual }
        private enum Ease { OutCubic, OutBack }

        [Tooltip("Animate a visual child, not a LayoutGroup-controlled slot.")]
        [SerializeField] private RectTransform visual;
        [SerializeField] private CanvasGroup fadeGroup;
        [SerializeField] private Selectable selectable;
        [SerializeField] private ClockMode clock = ClockMode.Unscaled;
        [SerializeField] private bool pointerFeedback = true;

        private Vector3 baseScale;
        private float baseAlpha;
        private Vector3 scaleFrom, scaleTo;
        private float scaleTime, scaleDuration, alphaFrom, alphaTo, alphaTime, alphaDuration;
        private Ease scaleEase;
        private bool cached, scaleRunning, alphaRunning, paused, pressed;

        public ClockMode Clock { get => clock; set => clock = value; }
        public bool IsAnimating => scaleRunning || alphaRunning;

        private void Awake() { CaptureRestPose(); }
        private void OnDisable() { CancelAndRestore(); }

        public void CaptureRestPose()
        {
            scaleRunning = alphaRunning = false;
            if (!visual) visual = transform as RectTransform;
            if (!fadeGroup) fadeGroup = GetComponent<CanvasGroup>();
            if (!selectable) selectable = GetComponent<Selectable>();
            baseScale = visual ? visual.localScale : Vector3.one;
            baseAlpha = fadeGroup ? fadeGroup.alpha : 1f;
            cached = true;
        }

        public void SetPaused(bool value) { paused = value; }

        public void SetVisualRoot(RectTransform target)
        {
            CancelAndRestore();
            visual = target;
            CaptureRestPose();
        }

        public void Pop(float duration = 0.24f)
        {
            EnsureCached();
            if (!visual || !isActiveAndEnabled) return;
            visual.localScale = baseScale * 0.86f;
            StartScale(baseScale, duration, Ease.OutBack);
        }

        public void Press()
        {
            EnsureCached();
            if (!CanInteract() || !visual) return;
            pressed = true;
            StartScale(baseScale * 0.94f, 0.08f, Ease.OutCubic);
        }

        public void Release()
        {
            EnsureCached();
            pressed = false;
            if (visual && isActiveAndEnabled) StartScale(baseScale, 0.14f, Ease.OutBack);
        }

        public void FadeIn(float duration = 0.18f)
        {
            EnsureCached();
            if (!fadeGroup || !isActiveAndEnabled) return;
            fadeGroup.alpha = 0;
            StartAlpha(baseAlpha, duration);
        }

        public void FadeOut(float duration = 0.14f)
        {
            EnsureCached();
            if (fadeGroup && isActiveAndEnabled) StartAlpha(0, duration);
        }

        public void CancelAndRestore()
        {
            scaleRunning = alphaRunning = pressed = paused = false;
            if (!cached) return;
            if (visual) visual.localScale = baseScale;
            if (fadeGroup) fadeGroup.alpha = baseAlpha;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (pointerFeedback && eventData.button == PointerEventData.InputButton.Left) Press();
        }
        public void OnPointerUp(PointerEventData eventData)
        {
            if (pointerFeedback && pressed) Release();
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            if (pointerFeedback && pressed) Release();
        }

        private void Update()
        {
            // If a button becomes disabled mid-press, restore its visual state.
            if (pressed && !CanInteract()) Release();
            if (clock == ClockMode.Manual) return;
            Step(clock == ClockMode.Unscaled ? Time.unscaledDeltaTime : Time.deltaTime);
        }

        public void Advance(float gameDeltaSeconds)
        {
            if (clock == ClockMode.Manual) Step(gameDeltaSeconds);
        }

        private void Step(float delta)
        {
            if (paused || !isActiveAndEnabled || delta <= 0 || float.IsNaN(delta) || float.IsInfinity(delta)) return;
            if (scaleRunning && visual)
            {
                scaleTime += delta;
                float t = Mathf.Clamp01(scaleTime / scaleDuration);
                visual.localScale = Vector3.LerpUnclamped(scaleFrom, scaleTo, Evaluate(t, scaleEase));
                if (t >= 1) { visual.localScale = scaleTo; scaleRunning = false; }
            }
            if (alphaRunning && fadeGroup)
            {
                alphaTime += delta;
                float t = Mathf.Clamp01(alphaTime / alphaDuration);
                fadeGroup.alpha = Mathf.Lerp(alphaFrom, alphaTo, Evaluate(t, Ease.OutCubic));
                if (t >= 1) { fadeGroup.alpha = alphaTo; alphaRunning = false; }
            }
        }

        private void StartScale(Vector3 targetScale, float duration, Ease ease)
        {
            scaleFrom = visual.localScale;
            scaleTo = targetScale;
            scaleTime = 0;
            scaleDuration = SafeDuration(duration);
            scaleEase = ease;
            scaleRunning = true;
        }

        private void StartAlpha(float targetAlpha, float duration)
        {
            alphaFrom = fadeGroup.alpha;
            alphaTo = Mathf.Clamp01(targetAlpha);
            alphaTime = 0;
            alphaDuration = SafeDuration(duration);
            alphaRunning = true;
        }

        private bool CanInteract() => isActiveAndEnabled && (!selectable || selectable.IsInteractable());
        private void EnsureCached() { if (!cached) CaptureRestPose(); }
        private static float SafeDuration(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0.001f : Mathf.Max(0.001f, value);
        }
        private static float Evaluate(float t, Ease ease)
        {
            if (ease == Ease.OutCubic) return 1f - Mathf.Pow(1f - t, 3);
            const float c = 1.70158f;
            float x = t - 1f;
            return 1f + (c + 1f) * x * x * x + c * x * x;
        }
    }
}
