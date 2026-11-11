// Requires Unity UI (com.unity.ugui). Visual-only: never writes gameplay state.
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace EarlyRush.UIKit
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class EarlyRushUISequence : MonoBehaviour
    {
        public enum ClockMode { Scaled, Unscaled, Manual }

        [SerializeField] private Image target;
        [SerializeField] private Sprite[] frames;
        [SerializeField, Min(0.01f)] private float framesPerSecond = 18f;
        [SerializeField] private bool loop;
        [SerializeField] private bool playOnEnable;
        [SerializeField] private bool restoreOnComplete;
        [SerializeField] private ClockMode clock = ClockMode.Manual;
        [SerializeField] private UnityEvent onCompleted = new UnityEvent();

        private Sprite baseSprite;
        private float elapsed;
        private bool cached;
        private bool playing;
        private bool paused;

        public bool IsPlaying => playing;
        public bool IsPaused => paused;
        public ClockMode Clock { get => clock; set => clock = value; }
        public event Action Completed;

        private void Awake() { CacheBaseSprite(); }
        private void OnEnable() { if (playOnEnable) Play(); }
        private void OnDisable() { Cancel(true); }

        public void CacheBaseSprite()
        {
            if (playing) throw new InvalidOperationException("Cancel playback before changing its base sprite.");
            if (!target) target = GetComponent<Image>();
            baseSprite = target.sprite;
            cached = true;
        }

        public void Play(Sprite[] clip, float fps, bool shouldLoop = false)
        {
            Cancel(false);
            frames = clip;
            framesPerSecond = fps;
            loop = shouldLoop;
            Play();
        }

        public void Play()
        {
            if (!cached) CacheBaseSprite();
            Cancel(false);
            if (!isActiveAndEnabled || !target || frames == null || frames.Length == 0 ||
                framesPerSecond <= 0 || float.IsNaN(framesPerSecond) || float.IsInfinity(framesPerSecond))
                return;
            for (int i = 0; i < frames.Length; i++)
                if (!frames[i]) { Debug.LogWarning("Sequence contains an empty frame slot.", this); return; }
            elapsed = 0;
            paused = false;
            playing = true;
            target.sprite = frames[0];
        }

        public void SetPaused(bool value) { paused = value; }

        public void Cancel(bool restoreBase = true)
        {
            playing = false;
            paused = false;
            elapsed = 0;
            if (restoreBase && cached && target) target.sprite = baseSprite;
            // Cancellation intentionally does NOT invoke completion callbacks.
        }

        private void Update()
        {
            if (clock == ClockMode.Manual) return;
            Step(clock == ClockMode.Unscaled ? Time.unscaledDeltaTime : Time.deltaTime);
        }

        // For gameplay effects: call once per rendered frame with max(0, run.elapsed - previousElapsed).
        // Pass 0 during Ready/Paused/Results. Do not also use Update-based clocks for the same clip.
        public void Advance(float gameDeltaSeconds)
        {
            if (clock == ClockMode.Manual) Step(gameDeltaSeconds);
        }

        private void Step(float delta)
        {
            if (!playing || paused || !isActiveAndEnabled || delta <= 0 || float.IsNaN(delta) || float.IsInfinity(delta)) return;
            elapsed += delta;
            double framePosition = (double)elapsed * framesPerSecond;
            if (!loop && framePosition >= frames.Length)
            {
                playing = false; // Set before callbacks so callbacks may start a new clip safely.
                target.sprite = restoreOnComplete ? baseSprite : frames[frames.Length - 1];
                onCompleted.Invoke();
                Completed?.Invoke();
                return;
            }
            if (loop)
            {
                float duration = frames.Length / framesPerSecond;
                elapsed %= duration;
                target.sprite = frames[Mathf.Min(frames.Length - 1, Mathf.FloorToInt(elapsed * framesPerSecond))];
            }
            else target.sprite = frames[Mathf.Min(frames.Length - 1, (int)framePosition)];
        }
    }
}
