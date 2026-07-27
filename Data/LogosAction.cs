using System.Collections.Generic;

namespace Logoria.Data
{
    /// <summary>
    /// A single Logos Action in the dex.
    /// <para>
    /// Only stable, hand-verified facts live here: the game's Action row id, the
    /// synthesis recipes, and the job list. Display name and description are NOT
    /// stored, they are read live from the Action / ActionTransient sheets so the
    /// dex follows the client's language automatically. <see cref="FallbackName"/>
    /// is only used if that lookup fails.
    /// </para>
    /// </summary>
    public sealed class LogosAction
    {
        public LogosAction(
            uint actionId,
            uint magiaIndex,
            string fallbackName,
            ushort iconId,
            LogosActionKind kind,
            int uses,
            float cast,
            float recast,
            IReadOnlyList<string> jobs,
            IReadOnlyList<string> tags,
            IReadOnlyList<LogosRecipe> recipes)
        {
            ActionId = actionId;
            MagiaIndex = magiaIndex;
            FallbackName = fallbackName;
            IconId = iconId;
            Kind = kind;
            Uses = uses;
            Cast = cast;
            Recast = recast;
            Jobs = jobs;
            Tags = tags;
            Recipes = recipes;
        }

        /// <summary>Real Action sheet row id (12958-13007, 14476-14481).</summary>
        public uint ActionId { get; }

        /// <summary>
        /// Row id in the EurekaMagiaAction sheet, 1-56. This is the index the
        /// in-game Logos Action Log uses, so it is what a 56-entry unlock bitfield
        /// would be keyed by, rather than <see cref="ActionId"/>.
        /// </summary>
        public uint MagiaIndex { get; }

        /// <summary>English name, used only when the live sheet lookup fails.</summary>
        public string FallbackName { get; }

        /// <summary>Real game icon id (64601-64656).</summary>
        public ushort IconId { get; }

        public LogosActionKind Kind { get; }

        /// <summary>Charges granted per synthesis.</summary>
        public int Uses { get; }

        public float Cast { get; }
        public float Recast { get; }

        /// <summary>Lowercase job abbreviations, e.g. "pld", "whm".</summary>
        public IReadOnlyList<string> Jobs { get; }

        /// <summary>Effect tags for filtering, e.g. "dmg", "buff", "raise".</summary>
        public IReadOnlyList<string> Tags { get; }

        /// <summary>
        /// Every known way to synthesise this action, cheapest first. Recipes using
        /// fewer mnemes have a higher success chance in the manipulator.
        /// </summary>
        public IReadOnlyList<LogosRecipe> Recipes { get; }

        /// <summary>The fewest-mneme recipe, which is always the best odds.</summary>
        public LogosRecipe? CheapestRecipe
        {
            get
            {
                LogosRecipe? best = null;
                foreach (var r in Recipes)
                    if (best == null || r.TotalMnemes < best.TotalMnemes) best = r;
                return best;
            }
        }
    }

    /// <summary>Broad role buckets derived from a Logos Action's job list.</summary>
    public static class LogosRoles
    {
        private static readonly HashSet<string> Tanks = new() { "pld", "war", "drk", "gnb" };
        private static readonly HashSet<string> Healers = new() { "whm", "sch", "ast", "sge" };
        private static readonly HashSet<string> Melee = new() { "mnk", "drg", "nin", "sam", "rpr", "vpr" };
        private static readonly HashSet<string> Ranged = new() { "brd", "mch", "dnc" };
        private static readonly HashSet<string> Casters = new() { "blm", "smn", "rdm", "pct" };

        /// <summary>
        /// All 20-ish jobs means "everyone", which reads better than listing them.
        /// <para>
        /// The generated database writes the sentinel <c>"all"</c> for the seventeen
        /// universal actions rather than spelling out every job, so a count test
        /// alone reported them as one job and fell through to "Unknown".
        /// </para>
        /// </summary>
        public static bool IsUniversal(LogosAction action) =>
            action.Jobs.Count >= 16
            || (action.Jobs.Count == 1 && action.Jobs[0] == "all");

        public static string Describe(LogosAction action)
        {
            if (IsUniversal(action)) return "All Roles";

            var roles = new List<string>(4);
            if (HasAny(action, Tanks)) roles.Add("Tank");
            if (HasAny(action, Healers)) roles.Add("Healer");
            if (HasAny(action, Melee)) roles.Add("Melee");
            if (HasAny(action, Ranged)) roles.Add("Ranged");
            if (HasAny(action, Casters)) roles.Add("Caster");

            return roles.Count == 0 ? "Unknown" : string.Join(", ", roles);
        }

        private static bool HasAny(LogosAction action, HashSet<string> role)
        {
            foreach (var j in action.Jobs)
                if (role.Contains(j)) return true;
            return false;
        }
    }
}
