using System;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Logoria.Data;

namespace Logoria.Services
{
    /// <summary>
    /// Talks to the in-game Logos Manipulator window.
    /// <para>
    /// The callback ids used by <see cref="RequestAutoFill"/> were captured from the
    /// live game and cross-checked against the resulting Astral Array, so they are
    /// observed rather than guessed. Auto-fill still stops short of synthesising:
    /// it loads the array and leaves the final click to the player, so the plugin
    /// can never consume materials on its own.
    /// </para>
    /// </summary>
    public unsafe class ManipulatorService
    {
        /// <summary>
        /// The synthesis window itself: the panel with the mix slots and the
        /// Synthesize button. Confirmed in-game 2026-07-25.
        /// </summary>
        public const string DefaultAddonName = "EurekaMagiciteItemSynthesis";

        /// <summary>Your mneme stock list. Feeds number array 137.</summary>
        public const string ShardListAddonName = "EurekaMagiciteItemShardList";

        /// <summary>
        /// The list of Logos Actions you are holding. Note the game's own spelling,
        /// "Ather" rather than "Aether" - it is not a typo on our side.
        /// </summary>
        public const string AetherListAddonName = "EurekaMagiciteItemAtherList";

        public bool IsAddonOpen(string name)
        {
            try
            {
                var wrapper = Service.GameGui.GetAddonByName(name);
                return !wrapper.IsNull && wrapper.IsReady && wrapper.IsVisible;
            }
            catch
            {
                return false;
            }
        }

        private readonly Configuration configuration;

        public ManipulatorService(Configuration configuration)
        {
            this.configuration = configuration;
        }

        /// <summary>
        /// Every addon Logoria is ever allowed to touch.
        /// <para>
        /// The addon name is configurable so a patch that renames the window can be
        /// fixed without a plugin update. That flexibility is also a hole:
        /// <see cref="TryAutoFill"/> sends UI callbacks to whatever this names, so an
        /// arbitrary value turns the Fill button into "fire callback 32 into some
        /// other game window". Nobody would choose that on purpose, but a config file
        /// is a text file, and "paste this to fix your plugin" is advice people
        /// follow. A closed list keeps the flexibility pointed at the three windows
        /// that exist while making the dangerous case unreachable.
        /// </para>
        /// </summary>
        public static readonly string[] KnownAddons =
        {
            DefaultAddonName,
            ShardListAddonName,
            AetherListAddonName,
        };

        public static bool IsKnownAddon(string? name) =>
            name != null && Array.IndexOf(KnownAddons, name) >= 0;

        /// <summary>
        /// The configured addon, validated on every read rather than only when it is
        /// set. Validating at assignment alone would miss a hand-edited config, which
        /// is the case that matters.
        /// </summary>
        public string AddonName =>
            IsKnownAddon(configuration.ManipulatorAddonName)
                ? configuration.ManipulatorAddonName
                : DefaultAddonName;

        // AutoFillVerified lived here as a gate while the callback ids were still
        // guesses. Removed once they were confirmed: TryAutoFill never consulted it
        // after that, so a flag that looked like a safety switch actually controlled
        // nothing, which is worse than having no switch at all.

        /// <summary>Shard list: place the mneme whose row base is the second value.</summary>
        private const int PlaceMnemeCallback = 14;

        /// <summary>Synthesis window: empty the array slot given by the second value.</summary>
        private const int ClearSlotCallback = 32;

        /// <summary>The Astral Array holds at most three mnemes.</summary>
        private const int ArraySlots = 3;

        private bool wasOpen;

        /// <summary>A fill asked for from the UI, waiting for the game thread.</summary>
        private sealed record FillRequest(
            LogosAction Action,
            LogosRecipe Recipe,
            MnemeInventoryService Inventory,
            GameTextService Text);

        private readonly object pendingGate = new();
        private FillRequest? pending;

        /// <summary>
        /// Queues a fill instead of performing one.
        /// <para>
        /// The Fill button is drawn on the render thread, but <c>FireCallback</c>
        /// drives the game's UI, which lives on the main thread. Calling it straight
        /// from Draw races the game: the addon can be torn down between the pointer
        /// being resolved and the callback firing, and that is a client crash rather
        /// than an exception we could catch. <see cref="ProcessPending"/> runs it on
        /// the right thread instead.
        /// </para>
        /// <para>
        /// Only the most recent request survives. Clicking Fill on three actions in
        /// quick succession should load the last one, not replay all three into the
        /// same three-slot array.
        /// </para>
        /// </summary>
        public void RequestAutoFill(
            LogosAction action,
            LogosRecipe recipe,
            MnemeInventoryService inventory,
            GameTextService text)
        {
            lock (pendingGate)
                pending = new FillRequest(action, recipe, inventory, text);
        }

        /// <summary>
        /// Runs any queued fill. Must be called from the game's main thread, i.e.
        /// from IFramework.Update, never from Draw.
        /// </summary>
        public void ProcessPending()
        {
            FillRequest? request;
            lock (pendingGate)
            {
                request = pending;
                pending = null;
            }

            if (request == null) return;

            TryAutoFill(request.Action, request.Recipe, request.Inventory, request.Text);
        }

        public AtkUnitBase* GetAddon()
        {
            try
            {
                var wrapper = Service.GameGui.GetAddonByName(AddonName);
                return wrapper.IsNull ? null : (AtkUnitBase*)wrapper.Address;
            }
            catch
            {
                return null;
            }
        }

        public bool IsManipulatorOpen()
        {
            try
            {
                var wrapper = Service.GameGui.GetAddonByName(AddonName);
                return !wrapper.IsNull && wrapper.IsReady && wrapper.IsVisible;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>True on the single frame the manipulator transitions to open.</summary>
        public bool JustOpened()
        {
            var open = IsManipulatorOpen();
            var opened = open && !wasOpen;
            wasOpen = open;
            return opened;
        }

        /// <summary>
        /// Clears the Astral Array and loads <paramref name="recipe"/> into it.
        /// <para>
        /// Deliberately stops short of synthesising: it fills the array and leaves
        /// the Extract Mneme button to you, so nothing is ever consumed without a
        /// deliberate click.
        /// </para>
        /// </summary>
        private bool TryAutoFill(
            LogosAction action,
            LogosRecipe recipe,
            MnemeInventoryService inventory,
            GameTextService text)
        {
            if (!IsManipulatorOpen())
            {
                Service.Log.Warning("Logos Manipulator is not open.");
                return false;
            }

            // Belt and braces: AddonName already falls back to the default when the
            // config holds something unknown, but this is the line that fires
            // callbacks into whatever it resolves, so it re-checks rather than
            // trusting a property further up the file.
            var target = AddonName;
            if (!IsKnownAddon(target))
            {
                Service.Log.Warning($"Auto-fill refused: '{target}' is not a Logos Manipulator window.");
                return false;
            }

            var shardList = GetAddonPointer(ShardListAddonName);
            var synthesis = GetAddonPointer(target);

            if (shardList == null || synthesis == null)
            {
                Service.Log.Warning("Manipulator panels are not both available yet.");
                return false;
            }

            // Resolve every mneme to its shard-list row before touching the game, so
            // a partial fill cannot happen because one lookup failed halfway through.
            var placements = new List<int>();
            foreach (var slot in recipe.Slots)
            {
                var rowIndex = inventory.RowIndexOf(slot.ItemId);
                if (rowIndex < 0)
                {
                    Service.Log.Warning(
                        $"Auto-fill aborted: '{text.MnemeName(slot.ItemId)}' is not in the shard list.");
                    return false;
                }

                for (var i = 0; i < slot.Count; i++) placements.Add(rowIndex);
            }

            if (placements.Count is 0 or > ArraySlots)
            {
                Service.Log.Warning($"Auto-fill aborted: recipe needs {placements.Count} mnemes.");
                return false;
            }

            try
            {
                var values = stackalloc AtkValue[2];

                // Empty the array first, highest slot down, so placements land in a
                // known order rather than appending to whatever was already there.
                for (var slot = ArraySlots - 1; slot >= 0; slot--)
                {
                    values[0].Type = AtkValueType.Int;
                    values[0].Int = ClearSlotCallback;
                    values[1].Type = AtkValueType.UInt;
                    values[1].UInt = (uint)slot;
                    synthesis->FireCallback(2, values);
                }

                foreach (var rowIndex in placements)
                {
                    values[0].Type = AtkValueType.Int;
                    values[0].Int = PlaceMnemeCallback;
                    values[1].Type = AtkValueType.UInt;
                    values[1].UInt = (uint)rowIndex;
                    shardList->FireCallback(2, values);
                }

                Service.Log.Information(
                    $"Auto-fill loaded {placements.Count} mneme(s) for '{action.FallbackName}'. " +
                    "Press Extract Mneme when you are ready.");
                return true;
            }
            catch (Exception ex)
            {
                Service.Log.Error(ex, $"Auto-fill failed for '{action.FallbackName}'.");
                return false;
            }
        }

        private AtkUnitBase* GetAddonPointer(string name)
        {
            try
            {
                var wrapper = Service.GameGui.GetAddonByName(name);
                if (wrapper.IsNull || !wrapper.IsReady || !wrapper.IsVisible) return null;
                return (AtkUnitBase*)wrapper.Address;
            }
            catch
            {
                return null;
            }
        }
    }
}
