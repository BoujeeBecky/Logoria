using System;
using System.Collections.Generic;
using Lumina.Excel.Sheets;
using Logoria.Data;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace Logoria.Services
{
    /// <summary>
    /// Which jobs a Logos Action is usable by, read from the game rather than
    /// bundled.
    /// <para>
    /// The generated database carries a job list scraped alongside the recipes, but
    /// that snapshot predates Dawntrail and is missing Reaper, Viper and Pictomancer.
    /// The game's own <c>Action.ClassJobCategory</c> is always current and always
    /// correct, so it wins; the bundled list is only a fallback for when the sheet
    /// cannot be read.
    /// </para>
    /// </summary>
    public class JobAffinityService
    {
        private readonly Dictionary<uint, string> roleCache = new();
        private readonly Dictionary<uint, string> jobCache = new();

        /// <summary>
        /// Combat jobs grouped by role, in the game's own column order. Crafters and
        /// gatherers are deliberately absent: no Logos Action is usable by them, and
        /// listing them would only add noise.
        /// </summary>
        private static readonly (string Role, string[] Jobs)[] Roles =
        {
            ("Tank", new[] { "PLD", "WAR", "DRK", "GNB" }),
            ("Healer", new[] { "WHM", "SCH", "AST", "SGE" }),
            ("Melee", new[] { "MNK", "DRG", "NIN", "SAM", "RPR", "VPR" }),
            ("Ranged", new[] { "BRD", "MCH", "DNC" }),
            ("Caster", new[] { "BLM", "SMN", "RDM", "PCT" }),
        };

        /// <summary>
        /// Role summary for an action, e.g. "Tank, Healer" or "All Roles".
        /// Cached: this walks a sheet row per call and the dex draws 56 of them.
        /// </summary>
        public string Describe(LogosAction action)
        {
            if (roleCache.TryGetValue(action.ActionId, out var cached)) return cached;

            var jobs = JobsFor(action);
            var described = jobs.Count == 0
                ? LogosRoles.Describe(action)   // sheet gave nothing usable
                : Summarise(jobs);

            roleCache[action.ActionId] = described;
            return described;
        }

        /// <summary>
        /// The jobs themselves, comma separated, for a tooltip behind the summary.
        /// A summary like "Tank, Healer" is short enough for a table cell but hides
        /// which jobs it actually means, and for the partial categories that
        /// difference matters.
        /// </summary>
        public string DescribeJobs(LogosAction action)
        {
            if (jobCache.TryGetValue(action.ActionId, out var cached)) return cached;

            var jobs = JobsFor(action);
            var described = jobs.Count == 0
                ? string.Join(", ", ActionJobs(action))
                : string.Join(", ", jobs);

            jobCache[action.ActionId] = described;
            return described;
        }

        /// <summary>
        /// Jobs the game says can use this action, in role order. Empty when the
        /// sheet cannot be read or the row carries no category, which is the signal
        /// for callers to fall back to the bundled list.
        /// </summary>
        private List<string> JobsFor(LogosAction action)
        {
            var jobs = new List<string>();

            try
            {
                var row = Service.DataManager.GetExcelSheet<LuminaAction>()?
                    .GetRowOrDefault(action.ActionId);

                if (!row.HasValue) return jobs;

                var category = row.Value.ClassJobCategory.ValueNullable;
                if (!category.HasValue) return jobs;

                foreach (var (_, roleJobs) in Roles)
                    foreach (var job in roleJobs)
                        if (IsJobSet(category.Value, job))
                            jobs.Add(job);
            }
            catch (Exception ex)
            {
                Service.Log.Debug(ex, $"Job affinity lookup failed for {action.ActionId}.");
                jobs.Clear();
            }

            return jobs;
        }

        /// <summary>
        /// Bundled fallback list, uppercased to match the sheet-derived one. The
        /// database's <c>"all"</c> sentinel is expanded, since a tooltip reading
        /// "ALL" tells the reader less than the roster does.
        /// </summary>
        private static IEnumerable<string> ActionJobs(LogosAction action)
        {
            if (LogosRoles.IsUniversal(action))
            {
                foreach (var (_, jobs) in Roles)
                    foreach (var job in jobs)
                        yield return job;
                yield break;
            }

            foreach (var job in action.Jobs) yield return job.ToUpperInvariant();
        }

        /// <summary>
        /// Collapses a job list to role names, without over-claiming.
        /// <para>
        /// A role is only named once every job in it is present. Stealth L is the
        /// case that forces this: it is usable by all twenty-one jobs except Ninja,
        /// and simply listing all five role names would read as "All Roles" while
        /// quietly being wrong for the one job most likely to try it.
        /// </para>
        /// </summary>
        private static string Summarise(List<string> jobs)
        {
            var set = new HashSet<string>(jobs);

            var full = new List<string>(Roles.Length);
            var partial = new List<string>();
            var missing = new List<string>();
            var total = 0;

            foreach (var (role, roleJobs) in Roles)
            {
                total += roleJobs.Length;

                var have = 0;
                foreach (var job in roleJobs)
                {
                    if (set.Contains(job)) have++;
                    else missing.Add(job);
                }

                if (have == roleJobs.Length) full.Add(role);
                else if (have > 0) partial.Add(role);
            }

            if (set.Count == total) return "All Roles";

            // Nearly everything: naming the exceptions is shorter and more useful
            // than naming five roles that are not quite all there.
            if (missing.Count <= 2 && partial.Count > 0)
                return $"All Roles except {string.Join(", ", missing)}";

            var parts = new List<string>(full);
            foreach (var role in partial)
            {
                var roleJobs = Array.Find(Roles, r => r.Role == role).Jobs;
                var have = new List<string>();
                foreach (var job in roleJobs)
                    if (set.Contains(job)) have.Add(job);

                // Few enough to name outright; otherwise say the role is partial
                // rather than pretending it is whole.
                parts.Add(have.Count <= 2
                    ? string.Join(", ", have)
                    : $"{role} ({have.Count}/{roleJobs.Length})");
            }

            return parts.Count == 0 ? "Unknown" : string.Join(", ", parts);
        }

        /// <summary>
        /// Reads one job column. ClassJobCategory exposes a bool per job rather than
        /// anything indexable, so the mapping has to be spelled out.
        /// </summary>
        private static bool IsJobSet(ClassJobCategory c, string job) => job switch
        {
            "PLD" => c.PLD,
            "WAR" => c.WAR,
            "DRK" => c.DRK,
            "GNB" => c.GNB,
            "WHM" => c.WHM,
            "SCH" => c.SCH,
            "AST" => c.AST,
            "SGE" => c.SGE,
            "MNK" => c.MNK,
            "DRG" => c.DRG,
            "NIN" => c.NIN,
            "SAM" => c.SAM,
            "RPR" => c.RPR,
            "VPR" => c.VPR,
            "BRD" => c.BRD,
            "MCH" => c.MCH,
            "DNC" => c.DNC,
            "BLM" => c.BLM,
            "SMN" => c.SMN,
            "RDM" => c.RDM,
            "PCT" => c.PCT,
            _ => false,
        };
    }
}
