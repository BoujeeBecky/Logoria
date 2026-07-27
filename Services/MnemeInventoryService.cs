using System;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Logoria.Data;

namespace Logoria.Services
{
    /// <summary>
    /// Live mneme stock, which is what recipes actually consume.
    /// <para>
    /// Deciphered mnemes do not sit in your normal bags, so
    /// <see cref="InventoryManager"/> alone reports zero for all of them. The
    /// authoritative source is the Logos Manipulator's own stock list, exposed
    /// through the UI number arrays. We read that when it is populated and fall
    /// back to a normal inventory count otherwise (raw, unidentified logograms
    /// <i>are</i> ordinary tradeable items).
    /// </para>
    /// </summary>
    public unsafe class MnemeInventoryService
    {
        /// <summary>
        /// Default index of the manipulator stock number array. Layout is
        /// <c>IntArray[0]</c> = entry count, then for entry i:
        /// <c>IntArray[4*i]</c> = stock and <c>IntArray[4*i + 1]</c> = mneme item id.
        /// The live value comes from config so the diagnostics window can correct it
        /// without a rebuild.
        /// </summary>
        public const int DefaultStockNumberArray = 137;

        private readonly Configuration configuration;
        private readonly Dictionary<uint, int> stock = new();
        private readonly Dictionary<uint, int> rowIndex = new();

        public MnemeInventoryService(Configuration configuration)
        {
            this.configuration = configuration;
        }

        public int StockNumberArrayIndex => configuration.ManipulatorStockNumberArray;

        /// <summary>True when the last refresh came from the manipulator arrays.</summary>
        public bool HasLiveStock { get; private set; }


        /// <summary>
        /// Re-reads mneme stock. Cheap enough to call once per frame, but the UI
        /// throttles it so a 56-row table is not doing 100+ lookups per draw.
        /// </summary>
        public void Refresh()
        {
            stock.Clear();
            rowIndex.Clear();
            HasLiveStock = false;

            if (TryReadManipulatorStock())
            {
                HasLiveStock = true;
                return;
            }

            ReadNormalInventoryFallback();
        }

        private bool TryReadManipulatorStock()
        {
            try
            {
                var framework = Framework.Instance();
                if (framework == null) return false;

                var uiModule = framework->GetUIModule();
                if (uiModule == null) return false;

                var raptureAtk = uiModule->GetRaptureAtkModule();
                if (raptureAtk == null) return false;

                var arrays = raptureAtk->AtkModule.AtkArrayDataHolder;
                var index = StockNumberArrayIndex;
                if (index < 0 || index >= arrays.NumberArrayCount) return false;

                var numbers = arrays.GetNumberArrayData(index);
                if (numbers == null || numbers->IntArray == null) return false;

                // Check the length before reading element 0. A zero-length array is
                // legal and would otherwise be a one-int read past the end.
                if (numbers->Size < 1) return false;

                var count = numbers->IntArray[0];
                if (count <= 0 || count > 128) return false;

                // Row layout, decoded from a live FireCallback capture on 2026-07-25:
                // rows start at index 3 with stride 4, and each row is
                // [icon, stock, itemId, ownIndex]. The trailing ownIndex is the value
                // the shard list passes back when you click that row, so it is what
                // auto-fill needs in order to select a specific mneme.
                var read = false;
                for (var i = 1; i <= count; i++)
                {
                    var rowBase = (4 * i) - 1;
                    if (rowBase + 3 >= numbers->Size) break;

                    var quantity = numbers->IntArray[rowBase + 1];
                    var itemId = numbers->IntArray[rowBase + 2];
                    var ownIndex = numbers->IntArray[rowBase + 3];
                    if (itemId <= 0) continue;

                    stock[(uint)itemId] = quantity;

                    // The row should report its own base index; if it does not, the
                    // layout has changed and the click index cannot be trusted.
                    rowIndex[(uint)itemId] = ownIndex == rowBase ? ownIndex : -1;
                    read = true;
                }

                return read;
            }
            catch (Exception ex)
            {
                Service.Log.Debug(ex, "Manipulator stock array unavailable, using inventory fallback.");
                return false;
            }
        }

        private void ReadNormalInventoryFallback()
        {
            try
            {
                var inventory = InventoryManager.Instance();
                if (inventory == null) return;

                foreach (var mneme in MnemeDatabase.All)
                    stock[mneme.ItemId] = inventory->GetInventoryItemCount(mneme.ItemId);
            }
            catch (Exception ex)
            {
                Service.Log.Error(ex, "Failed to read mneme counts from inventory.");
            }
        }

        public int CountOf(uint mnemeItemId) => stock.TryGetValue(mnemeItemId, out var n) ? n : 0;

        /// <summary>
        /// The value the shard list expects when selecting this mneme, or -1 if it is
        /// not currently listed or the row layout did not validate. Only meaningful
        /// while the manipulator is open.
        /// </summary>
        public int RowIndexOf(uint mnemeItemId) => rowIndex.TryGetValue(mnemeItemId, out var n) ? n : -1;

        /// <summary>True when every slot of <paramref name="recipe"/> is covered.</summary>
        public bool CanSynthesise(LogosRecipe recipe)
        {
            foreach (var slot in recipe.Slots)
                if (CountOf(slot.ItemId) < slot.Count) return false;
            return true;
        }

        /// <summary>The best-odds recipe you can actually make right now, if any.</summary>
        public LogosRecipe? BestAvailableRecipe(LogosAction action)
        {
            LogosRecipe? best = null;
            foreach (var recipe in action.Recipes)
            {
                if (!CanSynthesise(recipe)) continue;
                if (best == null || recipe.TotalMnemes < best.TotalMnemes) best = recipe;
            }
            return best;
        }

    }
}
