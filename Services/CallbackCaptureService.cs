using System;
using System.Collections.Generic;
using System.Text;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Logoria.Services
{
    public sealed record CapturedCallback(
        DateTime At,
        string AddonName,
        string Values,
        bool ClosesAddon = false,
        bool IsMarker = false);

    /// <summary>
    /// Records the <c>FireCallback</c> payloads an addon sends to its agent.
    /// <para>
    /// This is the piece the passive AddonLifecycle listener cannot see. ATK events
    /// like ListItemClick always report param 0 because the selected row lives in
    /// the list component, so replaying events cannot drive auto-fill. The actual
    /// "insert this mneme" instruction is in the AtkValue array passed to
    /// FireCallback, which needs a hook to observe.
    /// </para>
    /// <para>
    /// The hook is strictly read-only: it logs and always calls the original. It is
    /// only installed when explicitly enabled, and by default records just the Eureka
    /// windows plus the confirmation dialogs.
    /// </para>
    /// </summary>
    public unsafe class CallbackCaptureService : IDisposable
    {
        private delegate bool FireCallbackDelegate(
            AtkUnitBase* addon, uint valueCount, AtkValue* values, bool close);

        private Hook<FireCallbackDelegate>? hook;
        private readonly List<CapturedCallback> captured = new();

        // The hook runs on the game thread while the UI reads on the render thread,
        // so every touch of the list is guarded. Without this, drawing the table
        // mid-capture can throw "collection was modified" and take the window down.
        private readonly object gate = new();

        /// <summary>A point-in-time copy, safe to enumerate while capturing.</summary>
        public CapturedCallback[] Snapshot()
        {
            lock (gate) return captured.ToArray();
        }

        public int CapturedCount
        {
            get { lock (gate) return captured.Count; }
        }
        public bool IsEnabled { get; private set; }
        public string? LastError { get; private set; }

        /// <summary>
        /// Capture every addon rather than just the Eureka windows. Needed to see the
        /// SelectYesno confirmation, which is what pins down the synthesise callback.
        /// </summary>
        public bool CaptureAllAddons { get; set; }

        /// <summary>
        /// Drops a labelled line into the capture so the stream documents itself.
        /// Without this, a list of callbacks is very hard to line up with what you
        /// actually clicked.
        /// </summary>
        public void AddMarker(string label)
        {
            lock (gate) captured.Add(new CapturedCallback(DateTime.Now, "----", label, IsMarker: true));
            Service.Log.Information($"Logoria capture marker: {label}");
        }

        public void Enable()
        {
            if (IsEnabled) return;

            try
            {
                if (hook == null)
                {
                    var address = (nint)AtkUnitBase.MemberFunctionPointers.FireCallback;
                    if (address == 0)
                    {
                        LastError = "Could not resolve the FireCallback address.";
                        Service.Log.Error(LastError);
                        return;
                    }

                    hook = Service.GameInterop.HookFromAddress<FireCallbackDelegate>(address, Detour);
                }

                hook.Enable();
                IsEnabled = true;
                LastError = null;
                Service.Log.Information("Logoria: FireCallback capture enabled.");
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Service.Log.Error(ex, "Failed to install the FireCallback hook.");
            }
        }

        public void Disable()
        {
            if (!IsEnabled) return;

            try
            {
                hook?.Disable();
            }
            catch (Exception ex)
            {
                Service.Log.Error(ex, "Failed to disable the FireCallback hook.");
            }

            IsEnabled = false;
            Service.Log.Information("Logoria: FireCallback capture disabled.");
        }

        public void Clear()
        {
            lock (gate) captured.Clear();
        }

        private bool Detour(AtkUnitBase* addon, uint valueCount, AtkValue* values, bool close)
        {
            // Never let diagnostics break the game: record defensively, always
            // hand off to the original no matter what happens here.
            try
            {
                if (addon != null)
                {
                    var name = addon->NameString ?? string.Empty;
                    if (IsInteresting(name))
                    {
                        var entry = new CapturedCallback(
                            DateTime.Now,
                            name,
                            $"n={valueCount}  {FormatValues(valueCount, values)}",
                            close);

                        lock (gate)
                        {
                            captured.Add(entry);
                            if (captured.Count > 600) captured.RemoveAt(0);
                        }
                    }
                }
            }
            catch
            {
                // Swallow: a diagnostics failure must not affect the callback.
            }

            return hook!.Original(addon, valueCount, values, close);
        }

        private bool IsInteresting(string name)
        {
            if (CaptureAllAddons) return true;
            if (name.StartsWith("Eureka", StringComparison.Ordinal)) return true;

            // The confirmation dialogs are the giveaway for which callback actually
            // commits a synthesis.
            return name is "SelectYesno" or "SelectString" or "SelectIconString";
        }

        private static string FormatValues(uint valueCount, AtkValue* values)
        {
            if (values == null || valueCount == 0) return "(none)";

            var count = Math.Min(valueCount, 16u);
            var sb = new StringBuilder();

            for (var i = 0u; i < count; i++)
            {
                if (i > 0) sb.Append(", ");
                var v = values[i];

                sb.Append(v.Type switch
                {
                    AtkValueType.Int => $"Int:{v.Int}",
                    AtkValueType.UInt => $"UInt:{v.UInt}",
                    AtkValueType.Bool => $"Bool:{v.Byte != 0}",
                    AtkValueType.Float => $"Float:{v.Float}",
                    AtkValueType.String or AtkValueType.ManagedString or AtkValueType.ConstString => "String",
                    AtkValueType.Undefined => "-",
                    _ => v.Type.ToString(),
                });
            }

            if (valueCount > count) sb.Append(", ...");
            return sb.ToString();
        }

        public void Dispose()
        {
            try
            {
                hook?.Disable();
                hook?.Dispose();
            }
            catch (Exception ex)
            {
                Service.Log.Error(ex, "Failed to dispose the FireCallback hook.");
            }

            hook = null;
            IsEnabled = false;
        }
    }
}
