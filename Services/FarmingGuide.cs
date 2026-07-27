using System.Collections.Generic;

namespace Logoria.Services
{
    /// <summary>
    /// Turns the terse source tags on a logogram into something readable.
    /// <para>
    /// The tags come from the public Eureka tracker's dataset. They describe how a
    /// logogram drops, not a precise map location, so the wording here stays at the
    /// level the data actually supports rather than inventing coordinates.
    /// </para>
    /// </summary>
    public static class FarmingGuide
    {
        /// <summary>
        /// Named sources, taken verbatim from the tracker's own tooltips rather than
        /// paraphrased. An earlier version said things like "Protective NMs", which
        /// is true but useless: you cannot go and kill a category.
        /// </summary>
        private static readonly Dictionary<string, string> Descriptions = new()
        {
            ["NM-protective"] =
                "NMs: Flauros, Askalaphos, Lesath, Lamebrix Strikebocks, Glaukopis, Skoll",
            ["NM-curative"] =
                "NMs: Leucosia, Graffiacane, Aetolus, Iris, Lumber Jack, Penthesilea",
            ["NM-inimical"] =
                "NMs: The Sophist, Grand Duke Batym, Eldthurs, Dux, Ying-Yang",
            ["NM-obscure"] =
                "NMs: Khalamari, Stegodon, Molech, and NM fodder",

            ["sprite-conceptual"] = "Sprites at your level or above",
            ["sprite-fundamental"] = "Lv.41 Thunderstorm Sprite",
            ["sprite-mitigative"] = "Lv.46 Thunderstorm Sprite",
            ["sprite-adaptation-mitigative"] =
                "Sprite Adaptation: Lv.43 Ember, Lv.46 Thunderstorm",
            ["sprite-adaptation-inimical"] =
                "Sprite Adaptation: Lv.52 Snowstorm, Lv.54 Thunderstorm, Lv.55 Typhoon",
            ["undead-adaptation"] = "Undead during Adaptation weather",

            ["coffer-bronze"] = "Bronze Coffer",
            ["coffer-silver"] = "Silver Coffer",
            ["coffer-gold"] = "Gold Coffer",
            ["heatbox"] = "Heat-warped Lockbox",
            ["chain-30"] = "Chain 30 bonus",
        };

        public static string Describe(string tag) =>
            Descriptions.TryGetValue(tag, out var text) ? text : tag;

        public static string DescribeAll(IReadOnlyList<string> tags)
        {
            if (tags.Count == 0) return "Source unknown";

            var parts = new List<string>(tags.Count);
            foreach (var tag in tags) parts.Add(Describe(tag));
            return string.Join(", ", parts);
        }
    }
}
