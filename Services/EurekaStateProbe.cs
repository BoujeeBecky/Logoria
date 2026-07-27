using System;
using System.Collections.Generic;
using System.Text;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using Logoria.Data;

namespace Logoria.Services
{
    /// <summary>One byte that changed between two snapshots.</summary>
    public sealed record ByteDelta(int Offset, byte Before, byte After)
    {
        public IEnumerable<int> ChangedBits()
        {
            var diff = Before ^ After;
            for (var bit = 0; bit < 8; bit++)
                if ((diff & (1 << bit)) != 0) yield return bit;
        }

        public bool WasSet(int bit) => (After & (1 << bit)) != 0;
    }

    /// <summary>
    /// Locates the Logos Action Log by watching Eureka's save block change.
    /// <para>
    /// The game clearly does persist which actions you have registered, because the
    /// Hydatos armour augmentation unlocks at 56 unique entries. It is not in
    /// PlayerState, so the most likely home is Eureka's own per-character save
    /// blob. Rather than guess an offset, snapshot the block, synthesise one new
    /// action, and diff: the bit that flips is the log.
    /// </para>
    /// </summary>
    public unsafe class EurekaStateProbe
    {
        private byte[]? snapshot;
        private DateTime snapshotAt;

        public bool HasSnapshot => snapshot != null;
        public DateTime SnapshotAt => snapshotAt;
        public int SnapshotLength => snapshot?.Length ?? 0;

        /// <summary>The Eureka director, or null when not in Eureka.</summary>
        private static PublicContentEureka* GetEurekaDirector()
        {
            // Ask for Eureka specifically. GetPublicContentDirector() returns
            // whatever public content you happen to be in - Bozja, Island
            // Sanctuary, Occult Crescent - and blindly casting that to
            // PublicContentEureka would read another content's save block as if it
            // were Eureka's, and report "in Eureka" while standing in none of it.
            var director = EventFramework.GetPublicContentDirectorByType(
                PublicContentDirectorType.Eureka);

            return director == null ? null : (PublicContentEureka*)director;
        }

        public bool IsInEureka() => GetEurekaDirector() != null;

        /// <summary>
        /// Reads the director's union data. Using the bounded span rather than a raw
        /// pointer walk keeps this read strictly inside the struct.
        /// </summary>
        public byte[]? ReadCurrent()
        {
            try
            {
                var eureka = GetEurekaDirector();
                if (eureka == null) return null;

                var union = eureka->UnionData;
                var buffer = new byte[union.Length];
                union.CopyTo(buffer);
                return buffer;
            }
            catch (Exception ex)
            {
                Service.Log.Error(ex, "Eureka state read failed.");
                return null;
            }
        }

        public bool TakeSnapshot()
        {
            var current = ReadCurrent();
            if (current == null) return false;

            snapshot = current;
            snapshotAt = DateTime.Now;
            Service.Log.Information($"Eureka state snapshot taken: {current.Length} bytes.");
            return true;
        }

        public void ClearSnapshot() => snapshot = null;

        /// <summary>Bytes that differ between the snapshot and now.</summary>
        public List<ByteDelta> Diff()
        {
            var deltas = new List<ByteDelta>();
            if (snapshot == null) return deltas;

            var current = ReadCurrent();
            if (current == null) return deltas;

            var length = Math.Min(snapshot.Length, current.Length);
            for (var i = 0; i < length; i++)
                if (snapshot[i] != current[i])
                    deltas.Add(new ByteDelta(i, snapshot[i], current[i]));

            return deltas;
        }

        /// <summary>
        /// For a bit that just turned on, works out which Logos Action it would mean
        /// under each plausible bitfield convention. If one of these names matches
        /// what you just synthesised, that is the log and that is its base offset.
        /// </summary>
        public List<string> InterpretBit(int byteOffset, int bit, int assumedBaseOffset)
        {
            var readings = new List<string>();
            var bitIndex = ((byteOffset - assumedBaseOffset) * 8) + bit;

            // Convention A: bit 0 == MagiaIndex 1, least significant bit first.
            Describe(readings, "LSB-first, 1-based", bitIndex + 1);

            // Convention B: bit 0 == MagiaIndex 0 (i.e. index 1 lives at bit 1).
            Describe(readings, "LSB-first, 0-based", bitIndex);

            // Convention C: most significant bit first within each byte.
            var msbIndex = ((byteOffset - assumedBaseOffset) * 8) + (7 - bit);
            Describe(readings, "MSB-first, 1-based", msbIndex + 1);

            return readings;
        }

        private static void Describe(List<string> into, string convention, int magiaIndex)
        {
            if (magiaIndex is < 1 or > 56)
            {
                into.Add($"{convention}: index {magiaIndex} (out of the 1-56 range)");
                return;
            }

            var action = LogosDatabase.ByMagiaIndex((uint)magiaIndex);
            into.Add(action == null
                ? $"{convention}: index {magiaIndex} (no action)"
                : $"{convention}: index {magiaIndex} = {action.FallbackName}");
        }

        /// <summary>
        /// Scans the whole block for a run of bytes whose set-bit count equals the
        /// number of actions the dex already knows about. A 56-entry log is 7 bytes,
        /// so a matching popcount over any 7-byte window is a strong lead.
        /// </summary>
        public List<(int Offset, int PopCount)> FindCandidateBitfields(int expectedCount)
        {
            var hits = new List<(int, int)>();
            var current = ReadCurrent();
            if (current == null || expectedCount <= 0) return hits;

            const int windowBytes = 7; // 56 bits
            for (var offset = 0; offset + windowBytes <= current.Length; offset++)
            {
                var pop = 0;
                for (var i = 0; i < windowBytes; i++) pop += System.Numerics.BitOperations.PopCount(current[offset + i]);
                if (pop == expectedCount) hits.Add((offset, pop));
            }

            return hits;
        }

        public string HexDump(int bytesPerRow = 16)
        {
            var current = ReadCurrent();
            if (current == null) return "(not in Eureka)";

            var sb = new StringBuilder();
            for (var i = 0; i < current.Length; i += bytesPerRow)
            {
                sb.Append($"{i:X4}  ");
                for (var j = 0; j < bytesPerRow && i + j < current.Length; j++)
                    sb.Append($"{current[i + j]:X2} ");
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
