using System;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Logoria.Data;

namespace Logoria.Services
{
    /// <summary>What the dex knows about one action, for the current character.</summary>
    public enum DexState
    {
        /// <summary>Never obtained, and you cannot make it right now.</summary>
        Unknown,

        /// <summary>Never obtained, but you are holding everything needed. The good one.</summary>
        Ready,

        /// <summary>Obtained at some point. Permanent.</summary>
        Obtained,
    }

    /// <summary>
    /// The persistent "pokedex" of Logos Actions.
    /// <para>
    /// FFXIV does not record which actions you have ever synthesised. The Eureka
    /// save state is 18 bytes (experience, elemental levels, and a single magicite
    /// counter) with no per-action flags, and there is no Eureka inventory type.
    /// So the dex is built by observation: anything we ever see you holding is
    /// recorded permanently and never un-recorded, even after you use it up.
    /// </para>
    /// </summary>
    public unsafe class LogosDexService
    {
        private readonly Configuration configuration;
        private readonly MnemeInventoryService inventory;

        /// <summary>Fires when auto-detection records an action for the first time.</summary>
        public event Action<LogosAction>? ActionDiscovered;

        public LogosDexService(Configuration configuration, MnemeInventoryService inventory)
        {
            this.configuration = configuration;
            this.inventory = inventory;
        }

        /// <summary>Stable per-character key. Zero when not logged in.</summary>
        public static ulong CurrentContentId
        {
            get
            {
                var state = PlayerState.Instance();
                return state == null ? 0UL : state->ContentId;
            }
        }

        public bool IsLoggedIn => CurrentContentId != 0;

        /// <summary>Display name of the character the dex is currently showing.</summary>
        public static string CurrentCharacterName
        {
            get
            {
                try
                {
                    var state = PlayerState.Instance();
                    return state == null ? string.Empty : state->CharacterNameString;
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        /// <summary>
        /// True when this character's dex is empty but another character has entries.
        /// The dex is per-character, so an empty page is otherwise indistinguishable
        /// from data loss.
        /// </summary>
        public bool OtherCharactersHaveData()
        {
            var current = CurrentContentId;
            foreach (var (contentId, actions) in configuration.ObtainedActions)
                if (contentId != current && actions.Count > 0) return true;
            return false;
        }

        public bool HasObtained(uint actionId) =>
            configuration.IsObtained(CurrentContentId, actionId);

        public void SetObtained(uint actionId, bool obtained) =>
            configuration.SetObtained(CurrentContentId, actionId, obtained);

        public DexState StateOf(LogosAction action)
        {
            if (HasObtained(action.ActionId)) return DexState.Obtained;
            return inventory.BestAvailableRecipe(action) != null ? DexState.Ready : DexState.Unknown;
        }

        /// <summary>
        /// Actions you could synthesise right now but have never had. This is the
        /// headline number: "you can make 4 new ones".
        /// </summary>
        public List<LogosAction> ReadyToDiscover()
        {
            var result = new List<LogosAction>();
            foreach (var action in LogosDatabase.Actions)
                if (StateOf(action) == DexState.Ready) result.Add(action);
            return result;
        }

        public int ObtainedCount()
        {
            var cid = CurrentContentId;
            var n = 0;
            foreach (var action in LogosDatabase.Actions)
                if (configuration.IsObtained(cid, action.ActionId)) n++;
            return n;
        }

        /// <summary>
        /// Polls what the game says you currently have and folds it into the dex.
        /// Safe and cheap to call on a timer from the framework thread.
        /// </summary>
        public void Scan()
        {
            if (!IsLoggedIn) return;

            var discovered = new List<LogosAction>();
            ScanEquippedDutyActions(discovered);
            ScanHeldActionsArray(discovered);

            if (discovered.Count == 0) return;

            configuration.Save();
            foreach (var action in discovered)
            {
                Service.Log.Information($"Logoria: recorded '{action.FallbackName}' in the dex.");
                ActionDiscovered?.Invoke(action);
            }
        }

        /// <summary>
        /// Reads the Logos Actions you currently have slotted. This is the source
        /// we can rely on today: <see cref="DutyActionManager"/> is populated
        /// whenever duty actions are active, and its ids are plain Action row ids.
        /// </summary>
        private void ScanEquippedDutyActions(List<LogosAction> discovered)
        {
            try
            {
                var manager = DutyActionManager.GetInstanceIfReady();
                if (manager == null) return;

                var slots = manager->ActionId;
                var valid = Math.Min((int)manager->NumValidSlots, slots.Length);

                for (var i = 0; i < valid; i++)
                {
                    var actionId = slots[i];
                    if (actionId == 0) continue;
                    if (!LogosDatabase.TryGet(actionId, out var action)) continue;
                    if (HasObtained(actionId)) continue;

                    configuration.MarkObtained(CurrentContentId, actionId);
                    discovered.Add(action);
                }
            }
            catch (Exception ex)
            {
                Service.Log.Error(ex, "Logoria: duty action scan failed.");
            }
        }

        /// <summary>
        /// Reads the whole held Logos Actions list, not just the slots you have
        /// equipped. Only runs once the array index has been discovered via the
        /// diagnostics window; until then <see cref="ScanEquippedDutyActions"/>
        /// carries the load on its own.
        /// </summary>
        private void ScanHeldActionsArray(List<LogosAction> discovered)
        {
            var index = configuration.HeldActionsNumberArray;
            if (index < 0) return;

            try
            {
                var framework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance();
                if (framework == null) return;

                var uiModule = framework->GetUIModule();
                if (uiModule == null) return;

                var rapture = uiModule->GetRaptureAtkModule();
                if (rapture == null) return;

                var holder = rapture->AtkModule.AtkArrayDataHolder;
                if (index >= holder.NumberArrayCount) return;

                var array = holder.GetNumberArrayData(index);
                if (array == null || array->IntArray == null) return;

                // Scan the whole array rather than assuming a stride: this list's
                // layout is not confirmed, and a plain id sweep is robust to that.
                for (var i = 0; i < array->Size; i++)
                {
                    var value = array->IntArray[i];
                    if (value <= 0) continue;
                    if (!LogosDatabase.TryGet((uint)value, out var action)) continue;
                    if (HasObtained(action.ActionId)) continue;

                    configuration.MarkObtained(CurrentContentId, action.ActionId);
                    discovered.Add(action);
                }
            }
            catch (Exception ex)
            {
                Service.Log.Error(ex, "Logoria: held actions array scan failed.");
            }
        }

        /// <summary>
        /// Seeds the dex from everything currently synthesisable. Explicit user
        /// action only, since "I can make it" is not the same as "I have made it".
        /// </summary>
        public int MarkAllReadyAsObtained()
        {
            var cid = CurrentContentId;
            var n = 0;
            foreach (var action in ReadyToDiscover())
            {
                configuration.MarkObtained(cid, action.ActionId);
                n++;
            }
            if (n > 0) configuration.Save();
            return n;
        }
    }
}
