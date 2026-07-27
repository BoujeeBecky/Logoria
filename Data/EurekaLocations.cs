using System.Collections.Generic;
using System.Linq;

namespace Logoria.Data
{
    public enum EurekaZone
    {
        Anemos,
        Pagos,
        Pyros,
        Hydatos,
    }

    /// <summary>Territory and map ids, read from the game's own sheets.</summary>
    public sealed record EurekaZoneInfo(EurekaZone Zone, string Name, uint TerritoryId, uint MapId);

    /// <summary>
    /// How much to trust a coordinate. Surfaced in the UI rather than kept internal:
    /// a guess that looks identical to a fact is worse than no guess at all.
    /// </summary>
    public enum LocationConfidence
    {
        /// <summary>An explicit Locations row on the wiki named this enemy here.</summary>
        Confirmed,

        /// <summary>Close but not the enemy's own entry: a FATE marker, a landmark,
        /// or a spawn inferred from a game mechanic.</summary>
        Approximate,
    }

    public sealed record MobLocation(
        string Mob,
        EurekaZone Zone,
        float X,
        float Y,
        LocationConfidence Confidence,
        string? Note = null);

    /// <summary>
    /// Where to go for each logogram source.
    /// <para>
    /// Coordinates are community-sourced from the FFXIV wikis. They <i>should</i> be
    /// right rather than <i>are</i> right: the wikis are community maintained, several
    /// enemies spawn in more than one place, and Eureka's adaptation mechanic levels a
    /// mob up in place rather than spawning a separate one. Treat every pin as "start
    /// looking here", which is also how the UI words it.
    /// </para>
    /// </summary>
    public static class EurekaLocations
    {
        public static readonly IReadOnlyList<EurekaZoneInfo> Zones = new EurekaZoneInfo[]
        {
            new(EurekaZone.Anemos, "Eureka Anemos", 732, 414),
            new(EurekaZone.Pagos, "Eureka Pagos", 763, 467),
            new(EurekaZone.Pyros, "Eureka Pyros", 795, 484),
            new(EurekaZone.Hydatos, "Eureka Hydatos", 827, 515),
        };

        public static EurekaZoneInfo ZoneInfo(EurekaZone zone) =>
            Zones.First(z => z.Zone == zone);

        private const LocationConfidence Ok = LocationConfidence.Confirmed;
        private const LocationConfidence Approx = LocationConfidence.Approximate;

        /// <summary>
        /// Source tag to places. Several tags name multiple enemies, and several
        /// enemies have multiple spawns, so this is deliberately a list.
        /// </summary>
        private static readonly Dictionary<string, MobLocation[]> BySource = new()
        {
            ["NM-protective"] =
            [
                new("Flauros", EurekaZone.Pyros, 29.1f, 29.6f, Ok),
                new("Askalaphos", EurekaZone.Pyros, 19.3f, 28.1f, Ok),
                new("Lesath", EurekaZone.Pyros, 13.0f, 10.7f, Ok),
                new("Lamebrix Strikebocks", EurekaZone.Pyros, 21.4f, 8.3f, Ok),
                new("Glaukopis", EurekaZone.Pyros, 31.6f, 14.9f, Ok),
                new("Skoll", EurekaZone.Pyros, 23.3f, 30.2f, Ok),
            ],

            ["NM-curative"] =
            [
                new("Leucosia", EurekaZone.Pyros, 26.6f, 26.3f, Ok),
                new("Graffiacane", EurekaZone.Pyros, 23.7f, 37.3f, Ok),
                new("Aetolus", EurekaZone.Pyros, 10.2f, 14.0f, Ok),
                new("Iris", EurekaZone.Pyros, 21.5f, 11.9f, Ok),
                new("Lumber Jack", EurekaZone.Pyros, 30.0f, 11.6f, Approx, "FATE marker"),
                new("Penthesilea", EurekaZone.Pyros, 35.5f, 5.5f, Ok),
            ],

            ["NM-inimical"] =
            [
                new("The Sophist", EurekaZone.Pyros, 31.9f, 32.0f, Ok),
                new("Grand Duke Batym", EurekaZone.Pyros, 17.9f, 14.3f, Approx, "FATE marker"),
                new("Eldthurs", EurekaZone.Pyros, 14.5f, 6.1f, Ok),
                new("Dux", EurekaZone.Pyros, 27.1f, 9.0f, Ok),
                new("Ying-Yang", EurekaZone.Pyros, 11.9f, 33.8f, Ok),
            ],

            ["NM-obscure"] =
            [
                new("Khalamari", EurekaZone.Hydatos, 10.8f, 25.6f, Ok),
                new("Stegodon", EurekaZone.Hydatos, 10.4f, 17.2f, Ok),
                new("Molech", EurekaZone.Hydatos, 8.1f, 21.8f, Ok),
            ],

            ["sprite-conceptual"] =
            [
                new("Sprites at your level or above", EurekaZone.Pyros, 30.0f, 30.0f, Approx,
                    "any sprite of your level works"),
            ],

            // Adaptation levels a sprite up in place, so the higher bands are the same
            // spawn under different weather, not a separate mob to hunt elsewhere.
            ["sprite-fundamental"] =
            [
                new("Thunderstorm Sprite (Lv.41)", EurekaZone.Pyros, 30.0f, 30.0f, Ok,
                    "spawns in Thunder weather"),
            ],

            ["sprite-mitigative"] =
            [
                new("Thunderstorm Sprite (Lv.46)", EurekaZone.Pyros, 30.0f, 30.0f, Approx,
                    "same spawn as Lv.41, levelled by weather"),
            ],

            ["sprite-adaptation-mitigative"] =
            [
                new("Ember Sprite (Lv.43)", EurekaZone.Pyros, 19.2f, 34.5f, Ok),
                new("Thunderstorm Sprite (Lv.46)", EurekaZone.Pyros, 30.0f, 30.0f, Approx,
                    "same spawn as Lv.41, levelled by weather"),
            ],

            ["sprite-adaptation-inimical"] =
            [
                new("Snowstorm Sprite (Lv.52)", EurekaZone.Pyros, 22.6f, 8.6f, Ok),
                new("Thunderstorm Sprite (Lv.54)", EurekaZone.Pyros, 30.0f, 30.0f, Approx,
                    "same spawn as Lv.41, levelled by weather"),
                new("Typhoon Sprite (Lv.55)", EurekaZone.Pyros, 16.5f, 36.0f, Approx,
                    "unconfirmed: near Pyros Shucks in Umbral Wind"),
            ],
        };

        /// <summary>Places to try for a source tag. Empty when we have nothing useful.</summary>
        public static IReadOnlyList<MobLocation> ForSource(string tag) =>
            BySource.TryGetValue(tag, out var places) ? places : [];

        private static readonly Dictionary<int, MobLocation[]> ByLogogram = new();

        /// <summary>
        /// Every place for a logogram, across all of its source tags.
        /// <para>
        /// Cached and returned as a list rather than an iterator. The farming plan
        /// asks for this once per group per frame, plus once more to decide whether
        /// to show the accuracy note, and every caller wants a count. Yielding meant
        /// each of those allocated an enumerator and a fresh list.
        /// </para>
        /// </summary>
        public static IReadOnlyList<MobLocation> ForLogogram(Logogram logogram)
        {
            if (ByLogogram.TryGetValue(logogram.Index, out var cached)) return cached;

            var places = new List<MobLocation>();
            foreach (var tag in logogram.AcquiredBy)
                places.AddRange(ForSource(tag));

            var result = places.ToArray();
            ByLogogram[logogram.Index] = result;
            return result;
        }
    }
}
