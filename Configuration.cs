using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;

namespace Logoria
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        /// <summary>
        /// Required by IPluginConfiguration. Note this is NOT a reliable migration
        /// signal: it is only ever the value that was first written, since a loaded
        /// config overwrites the default and nothing bumps it afterwards. Migrations
        /// live in <see cref="Migrate"/> and key off the values themselves.
        /// </summary>
        public int Version { get; set; } = 3;

        /// <summary>
        /// Per-character dex: PlayerState.ContentId -> action ids ever obtained.
        /// Write-once, never cleared automatically.
        /// </summary>
        public Dictionary<ulong, HashSet<uint>> ObtainedActions { get; set; } = new();

        // ---- Auto-detection ----
        public bool EnableAutoDetect { get; set; } = true;

        /// <summary>Seconds between dex scans. Cheap, but no reason to run per-frame.</summary>
        public float AutoDetectIntervalSeconds { get; set; } = 2f;

        public bool AnnounceDiscoveriesInChat { get; set; } = true;

        /// <summary>
        /// Sync the dex automatically whenever Drake's Logos Action Log is opened.
        /// This is the authoritative source, so it is on by default.
        /// </summary>
        public bool AutoSyncFromLog { get; set; } = true;

        /// <summary>
        /// Actions you are actively farming toward. The floater and the farming
        /// planner both work from this list.
        /// </summary>
        public HashSet<uint> FarmActionIds { get; set; } = new();

        /// <summary>
        /// Which farmed action is expanded for detail. 0 means show the list.
        /// </summary>
        public uint TargetActionId { get; set; }

        /// <summary>Use the best-odds recipe when totalling what to farm.</summary>
        public bool PlanWithCheapestRecipe { get; set; } = true;

        public bool IsFarming(uint actionId) => FarmActionIds.Contains(actionId);

        public void ToggleFarming(uint actionId)
        {
            if (!FarmActionIds.Remove(actionId)) FarmActionIds.Add(actionId);
            if (TargetActionId == actionId && !FarmActionIds.Contains(actionId)) TargetActionId = 0;
            Save();
        }

        /// <summary>
        /// Text shadow style: 0 none, 1 drop, 2 outline. Stored as an int so the
        /// config does not depend on the UI kit's enum.
        /// </summary>
        public int TextShadowStyle { get; set; } = 1;

        /// <summary>
        /// Plain-ImGui mode: no theme, effects or animation. A performance option
        /// for weaker machines, deliberately off by default.
        /// </summary>
        public bool VanillaMode { get; set; }

        /// <summary>Name of the active UI kit theme preset.</summary>
        public string ThemeName { get; set; } = "Aetherial";

        /// <summary>Vertical lighting on panels. 0 is flat.</summary>
        public float SurfaceGradient { get; set; } = 0.045f;

        /// <summary>Edge highlight and shadow strength on raised surfaces.</summary>
        public float BevelStrength { get; set; } = 0.55f;

        /// <summary>Glassy shine across the top of raised elements.</summary>
        public float GlossStrength { get; set; } = 0.8f;

        /// <summary>
        /// Pills and the progress-bar fill grade edge-to-centre (a convex token)
        /// rather than top-to-bottom (a flat plane). Panels are unaffected either
        /// way. Purely taste, so it is a switch.
        /// </summary>
        public bool DomedTokens { get; set; } = true;

        /// <summary>
        /// Size for a custom font from assets/fonts. Ignored when none is present.
        /// Changing it needs a plugin reload, since the atlas is built once.
        /// </summary>
        public float FontSizePx { get; set; } = 17f;

        /// <summary>Fades and eased transitions. Off makes everything snap.</summary>
        public bool EnableAnimation { get; set; } = true;

        /// <summary>Multiplier on every animation. Higher is snappier.</summary>
        public float AnimationSpeed { get; set; } = 1f;

        /// <summary>Film grain over surfaces. 0 disables it.</summary>
        public float NoiseStrength { get; set; } = 0.7f;

        /// <summary>Frosted-glass surfaces instead of solid panels.</summary>
        public bool GlassMode { get; set; }

        /// <summary>How much surface colour survives in glass mode. Lower is clearer.</summary>
        public float GlassOpacity { get; set; } = 0.30f;

        // ---- Main window ----
        public bool HighlightReadyToDiscover { get; set; } = true;

        // ---- Floating window ----
        public float FloatingWindowOpacity { get; set; } = 0.85f;
        public bool FloatingWindowLock { get; set; } = false;
        public bool FloatingWindowHideObtained { get; set; } = false;

        // ---- Manipulator ----
        public bool AutoOpenOnManipulator { get; set; } = true;

        /// <summary>
        /// Addon name of the Logos Manipulator window. Overridable because it is the
        /// kind of thing a patch can rename; the diagnostics window can set it.
        /// </summary>
        public string ManipulatorAddonName { get; set; } =
            Services.ManipulatorService.DefaultAddonName;

        /// <summary>
        /// Index of the UI number array holding mneme stock. Discoverable in-game
        /// via the diagnostics window, so a wrong default is not a code change.
        /// Confirmed as 137 on 2026-07-25.
        /// </summary>
        public int ManipulatorStockNumberArray { get; set; } =
            Services.MnemeInventoryService.DefaultStockNumberArray;

        /// <summary>
        /// Index of the number array backing the held Logos Actions list
        /// (EurekaMagiciteItemAtherList). -1 means not discovered yet, in which case
        /// the dex falls back to reading only the actions you have slotted.
        /// </summary>
        public int HeldActionsNumberArray { get; set; } = -1;

#if LOGORIA_DIAG
        /// <summary>
        /// Whether the diagnostics window is reachable. Development builds only:
        /// the released build does not contain the diagnostics code, so it does not
        /// carry the setting either. A config written by a dev build keeps the key
        /// harmlessly; the released build ignores keys it does not know.
        /// </summary>
        public bool ShowDiagnostics { get; set; }
#endif

        /// <summary>
        /// Replaces the character name in the UI with a neutral label.
        /// <para>
        /// For screenshots and streaming. The name is not decoration: it is what
        /// tells you the dex is per character, so hiding it swaps in wording that
        /// still says so rather than leaving a blank.
        /// </para>
        /// </summary>
        public bool HideCharacterName { get; set; }

        /// <summary>
        /// Bumped to throw away saved table column widths.
        /// <para>
        /// ImGui persists user-resized columns in its own ini, keyed by the table's
        /// id, and those saved widths win over whatever <c>TableSetupColumn</c> asks
        /// for. That is correct for a user drag and wrong after a layout change, and
        /// it is why saving was switched off in the first place: old widths were
        /// silently squeezing new columns. Appending this number to the table id
        /// means a layout change (or a reset button) starts from fresh defaults
        /// rather than inheriting them.
        /// </para>
        /// </summary>
        public int TableLayoutEpoch { get; set; }

        /// <summary>
        /// Fixes up settings saved before a default changed. Cheap, runs once at load.
        /// </summary>
        public void Migrate()
        {
            // The original guess for the manipulator addon never existed in game.
            // More importantly, this value decides where auto-fill sends UI
            // callbacks, so anything not on the known list is reset rather than
            // trusted. The config is a text file and this is the one field in it
            // that can point the plugin at another game window.
            if (!Services.ManipulatorService.IsKnownAddon(ManipulatorAddonName))
                ManipulatorAddonName = Services.ManipulatorService.DefaultAddonName;

            // Deserialised collections can come back null from an older or
            // hand-edited config, and every consumer assumes they are non-null.
            ObtainedActions ??= new Dictionary<ulong, HashSet<uint>>();
            FarmActionIds ??= new HashSet<uint>();
            ThemeName ??= "Aetherial";
        }

        [NonSerialized]
        private IDalamudPluginInterface? pluginInterface;

        public void Initialize(IDalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        public void Save() => pluginInterface?.SavePluginConfig(this);

        private HashSet<uint> DexFor(ulong contentId)
        {
            if (!ObtainedActions.TryGetValue(contentId, out var set))
            {
                set = new HashSet<uint>();
                ObtainedActions[contentId] = set;
            }
            return set;
        }

        public bool IsObtained(ulong contentId, uint actionId) =>
            ObtainedActions.TryGetValue(contentId, out var set) && set.Contains(actionId);

        /// <summary>Records without saving, so a batch scan writes the file once.</summary>
        public void MarkObtained(ulong contentId, uint actionId) => DexFor(contentId).Add(actionId);

        public void SetObtained(ulong contentId, uint actionId, bool obtained)
        {
            if (obtained) DexFor(contentId).Add(actionId);
            else DexFor(contentId).Remove(actionId);
            Save();
        }
    }
}
