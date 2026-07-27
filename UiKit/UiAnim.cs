using System;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;

namespace UiKit
{
    /// <summary>
    /// Per-widget animation state for an immediate-mode UI.
    /// <para>
    /// ImGui redraws from scratch every frame and keeps nothing, so a widget that
    /// wants to fade or slide has to store its own value somewhere. This keeps one
    /// float per key and eases it toward whatever target the caller passes this
    /// frame. Callers stay stateless; they just say where they want to be.
    /// </para>
    /// <para>
    /// Everything here runs on the render thread inside Draw, so a plain dictionary
    /// is safe. Do not call it from the framework thread.
    /// </para>
    /// </summary>
    public static class UiAnim
    {
        private struct Entry
        {
            public float Value;
            public int LastFrame;
        }

        private static readonly Dictionary<uint, Entry> States = new();
        private static int lastSweep;

        /// <summary>Master switch. When off, every call snaps straight to target.</summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>Scales every animation. 1 is the tuned default; 2 is twice as fast.</summary>
        public static float SpeedScale { get; set; } = 1f;

        /// <summary>
        /// Eases a stored value toward <paramref name="target"/> and returns it.
        /// <para>
        /// Framerate independent: the step uses an exponential decay against delta
        /// time, so the animation takes the same wall-clock time at 30fps and 144fps.
        /// A linear step would run at whatever speed the machine happens to render.
        /// </para>
        /// </summary>
        /// <param name="speed">Higher converges faster. 12 is a brisk UI fade.</param>
        public static float Approach(uint key, float target, float speed = 12f)
        {
            if (!Enabled || UiPalette.IsVanilla) return target;

            var frame = ImGui.GetFrameCount();
            Sweep(frame);

            var delta = ImGui.GetIO().DeltaTime;

            // A hitch or a paused game can deliver an enormous delta; clamping stops
            // the animation from jumping the whole way in one frame.
            delta = Math.Clamp(delta, 0f, 0.1f);

            if (!States.TryGetValue(key, out var entry))
            {
                // First sight of this widget: start at the target so it does not
                // visibly animate in from zero the moment a window opens.
                States[key] = new Entry { Value = target, LastFrame = frame };
                return target;
            }

            var t = 1f - MathF.Exp(-Math.Max(0.01f, speed * SpeedScale) * delta);
            entry.Value += (target - entry.Value) * t;

            // Settle exactly, so a value never creeps forever a hair away.
            if (MathF.Abs(target - entry.Value) < 0.001f) entry.Value = target;

            entry.LastFrame = frame;
            States[key] = entry;
            return entry.Value;
        }

        /// <summary>Eases toward 1 when <paramref name="on"/>, back to 0 otherwise.</summary>
        public static float Toggle(uint key, bool on, float speed = 12f) =>
            Approach(key, on ? 1f : 0f, speed);

        /// <summary>
        /// A 0..1 triangle wave from wall-clock time. Stateless, so it needs no key,
        /// and every caller with the same period stays in phase, which looks
        /// deliberate rather than chaotic.
        /// </summary>
        public static float Pulse(float period = 2f)
        {
            if (!Enabled || UiPalette.IsVanilla || period <= 0f) return 1f;

            var phase = (float)(ImGui.GetTime() % period) / period;
            return phase < 0.5f ? phase * 2f : 2f - (phase * 2f);
        }

        /// <summary>Smoothstep, for easing a linear 0..1 into something less mechanical.</summary>
        public static float Smooth(float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return t * t * (3f - (2f * t));
        }

        /// <summary>Stable key for a string, so callers do not have to invent ids.</summary>
        public static uint Key(string id) => ImGui.GetID(id);

        public static void Clear() => States.Clear();

        /// <summary>
        /// Drops entries nothing has touched recently. Without this the dictionary
        /// grows for every widget ever drawn, including ones scrolled out of a long
        /// list and never seen again.
        /// </summary>
        private static void Sweep(int frame)
        {
            if (frame - lastSweep < 600) return;
            lastSweep = frame;

            var stale = new List<uint>();
            foreach (var (key, entry) in States)
                if (frame - entry.LastFrame > 600) stale.Add(key);

            foreach (var key in stale) States.Remove(key);
        }
    }
}
