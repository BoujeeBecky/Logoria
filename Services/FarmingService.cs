using System;
using System.Collections.Generic;
using System.Linq;
using Logoria.Data;

namespace Logoria.Services
{
    /// <summary>How much of one mneme the whole farm list needs.</summary>
    public sealed record MnemeNeed(uint ItemId, int Needed, int Have)
    {
        public int Short => System.Math.Max(0, Needed - Have);
        public bool Satisfied => Short == 0;
        public float Progress => Needed <= 0 ? 1f : System.Math.Min(1f, Have / (float)Needed);
    }

    /// <summary>Everything a single logogram is being farmed for.</summary>
    public sealed record LogogramNeed(Logogram Source, IReadOnlyList<MnemeNeed> Mnemes)
    {
        public int TotalShort => Mnemes.Sum(m => m.Short);
        public bool Satisfied => TotalShort == 0;
    }

    /// <summary>
    /// Turns the farm list into a shopping list.
    /// <para>
    /// Requirements are summed across every action you are farming, then grouped by
    /// the logogram that yields each mneme, because that is the unit you actually go
    /// out and collect.
    /// </para>
    /// </summary>
    public class FarmingService
    {
        private readonly Configuration configuration;
        private readonly MnemeInventoryService inventory;

        public FarmingService(Configuration configuration, MnemeInventoryService inventory)
        {
            this.configuration = configuration;
            this.inventory = inventory;
        }

        public List<LogosAction> FarmedActions()
        {
            var result = new List<LogosAction>();
            foreach (var action in LogosDatabase.Actions)
                if (configuration.IsFarming(action.ActionId)) result.Add(action);
            return result;
        }

        /// <summary>
        /// The recipe we assume you will use for an action: whichever you are
        /// closest to finishing.
        /// <para>
        /// This used to prefer any recipe you could already make and otherwise the
        /// cheapest. That made the plan unstable. Picking up a mneme could flip an
        /// action onto a different combination, so the shopping list rewrote itself
        /// and overall progress could drop even though you had just gained
        /// something. Worse, progress toward the combination you were part-way
        /// through counted for nothing the moment the plan jumped elsewhere.
        /// </para>
        /// <para>
        /// Ranking by shortfall fixes that because it only ever moves one way:
        /// gathering toward a recipe reduces its shortfall, so the recipe you are
        /// investing in stays the plan. It changes only when another combination is
        /// genuinely closer, which is information worth acting on. Ties break toward
        /// fewer mnemes, which is also the better success rate.
        /// </para>
        /// </summary>
        public LogosRecipe? PlannedRecipe(LogosAction action)
        {
            if (!configuration.PlanWithCheapestRecipe)
                return action.Recipes.FirstOrDefault();

            LogosRecipe? best = null;
            var bestShort = int.MaxValue;
            var bestTotal = int.MaxValue;

            foreach (var recipe in action.Recipes)
            {
                var shortfall = ShortfallOf(recipe);
                var total = recipe.TotalMnemes;

                if (shortfall > bestShort) continue;
                if (shortfall == bestShort && total >= bestTotal) continue;

                best = recipe;
                bestShort = shortfall;
                bestTotal = total;
            }

            return best;
        }

        /// <summary>How many individual mnemes you still lack for a recipe.</summary>
        public int ShortfallOf(LogosRecipe recipe)
        {
            var missing = 0;
            foreach (var slot in recipe.Slots)
                missing += Math.Max(0, slot.Count - inventory.CountOf(slot.ItemId));
            return missing;
        }

        /// <summary>Total mneme requirements across the whole farm list.</summary>
        public List<MnemeNeed> TotalNeeds()
        {
            var totals = new Dictionary<uint, int>();

            foreach (var action in FarmedActions())
            {
                var recipe = PlannedRecipe(action);
                if (recipe == null) continue;

                foreach (var slot in recipe.Slots)
                {
                    totals.TryGetValue(slot.ItemId, out var n);
                    totals[slot.ItemId] = n + slot.Count;
                }
            }

            return totals
                .Select(kv => new MnemeNeed(kv.Key, kv.Value, inventory.CountOf(kv.Key)))
                .OrderByDescending(n => n.Short)
                .ThenBy(n => n.ItemId)
                .ToList();
        }

        /// <summary>Requirements grouped by the logogram you would go and farm.</summary>
        public List<LogogramNeed> NeedsByLogogram()
        {
            var groups = new Dictionary<int, List<MnemeNeed>>();

            foreach (var need in TotalNeeds())
            {
                var mneme = MnemeDatabase.ById(need.ItemId);
                var key = mneme?.LogogramIndex ?? 0;

                if (!groups.TryGetValue(key, out var list))
                {
                    list = new List<MnemeNeed>();
                    groups[key] = list;
                }

                list.Add(need);
            }

            var result = new List<LogogramNeed>();
            foreach (var (index, needs) in groups)
            {
                var source = MnemeDatabase.LogogramAt(index);
                if (source == null) continue;
                result.Add(new LogogramNeed(source, needs));
            }

            return result
                .OrderByDescending(g => g.TotalShort)
                .ThenBy(g => g.Source.Index)
                .ToList();
        }

        /// <summary>Requirements for one action on its own.</summary>
        public List<MnemeNeed> NeedsFor(LogosAction action)
        {
            var recipe = PlannedRecipe(action);
            if (recipe == null) return new List<MnemeNeed>();

            return recipe.Slots
                .Select(s => new MnemeNeed(s.ItemId, s.Count, inventory.CountOf(s.ItemId)))
                .ToList();
        }

        /// <summary>Fraction of the whole farm list's requirements already in hand.</summary>
        public float OverallProgress()
        {
            var needs = TotalNeeds();
            if (needs.Count == 0) return 0f;

            var needed = needs.Sum(n => n.Needed);
            if (needed <= 0) return 1f;

            var have = needs.Sum(n => System.Math.Min(n.Have, n.Needed));
            return have / (float)needed;
        }

        public bool IsReady(LogosAction action)
        {
            var recipe = inventory.BestAvailableRecipe(action);
            return recipe != null;
        }
    }
}
