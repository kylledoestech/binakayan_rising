using System.Collections.Generic;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Content;
using BinakayanRising.Core.Grid;

namespace BinakayanRising.Core.Meta
{
    /// <summary>A Kapatiran pair that reached a new rank after a battle.</summary>
    public sealed class BondRankUp
    {
        public BondPair Pair;
        public BondRank From;
        public BondRank To;

        /// <summary>True when this rank-up opened the pair's lore dialogue (rank C).</summary>
        public bool UnlockedLore
        {
            get { return From < BondRank.C && To >= BondRank.C; }
        }
    }

    /// <summary>
    /// Kapatiran support ranks (#19): how a pair earns C, B and A, and what a battle is fought with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>DESIGN-DECISIONS #10, resolved.</b> The GDD's "permanent" boosts and Table 3's proximity
    /// bonus are read as one system: the <em>rank</em> is permanent — it lives in the save and
    /// never drops — while the <em>bonus</em> of that rank applies in battle only while the pair
    /// stands side by side, exactly as the battle already resolved it.
    /// </para>
    /// <para>
    /// <b>#8, resolved.</b> Support is earned the way the proposal describes it, "placing specific
    /// historical units adjacent to each other on the deployment grid": each battle fought (won or
    /// lost, not retreated from) with both partners deployed side by side gives the pair
    /// <see cref="MetaRules.BondSupportPerBattle"/>. Adjacency is the battle's own rule
    /// (<see cref="KapatiranResolver.IsInProximity"/>), so the grid that shows the bond lit is the
    /// grid that earns it.
    /// </para>
    /// </remarks>
    public sealed partial class MetaGame
    {
        /// <summary>The adjacency rule the battle resolves bonds with.</summary>
        private static readonly KapatiranResolver Proximity = KapatiranResolver.CreateEmpty();

        /// <summary>Support points <paramref name="bondId"/> has earned.</summary>
        public int BondSupport(string bondId)
        {
            BondRecord record = FindBond(bondId);
            return record == null ? 0 : record.support;
        }

        /// <summary>The rank <paramref name="bondId"/> has reached.</summary>
        public BondRank BondRankOf(string bondId)
        {
            return RankFor(BondSupport(bondId));
        }

        /// <summary>Support points needed for the next rank, or 0 at rank A.</summary>
        public int BondSupportForNext(string bondId)
        {
            int rank = (int)BondRankOf(bondId);
            int[] steps = Rules.BondRankSupport;
            return rank < steps.Length ? steps[rank] : 0;
        }

        /// <summary>True once the pair's lore dialogue is open: rank C or better.</summary>
        public bool IsLoreUnlocked(string bondId)
        {
            return BondRankOf(bondId) >= BondRank.C;
        }

        /// <summary>True once the player has heard the pair's lore dialogue.</summary>
        public bool IsLoreHeard(string bondId)
        {
            return Data.HasFlag(LoreFlag(bondId));
        }

        /// <summary>Records the pair's lore dialogue as heard.</summary>
        public void MarkLoreHeard(string bondId)
        {
            string flag = LoreFlag(bondId);
            if (!Data.flags.Contains(flag))
            {
                Data.flags.Add(flag);
                RaiseChanged();
            }
        }

        /// <summary>The bonds a campaign battle is fought with: every pair at its earned rank.</summary>
        public List<KapatiranBond> BattleBonds()
        {
            return BondCatalog.AtRanks(BondRankOf);
        }

        /// <summary>
        /// Credits support to every bonded pair that fought a battle side by side, and returns the
        /// pairs that reached a new rank.
        /// </summary>
        /// <param name="placements">Where each deployed unit stood, by save unit id.</param>
        public List<BondRankUp> RecordBondSupport(IEnumerable<KeyValuePair<int, GridCoord>> placements)
        {
            var ups = new List<BondRankUp>();
            if (placements == null)
            {
                return ups;
            }

            var deployed = new List<KeyValuePair<string, GridCoord>>();
            foreach (KeyValuePair<int, GridCoord> placement in placements)
            {
                OwnedUnit unit = FindUnit(placement.Key);
                if (unit != null)
                {
                    deployed.Add(new KeyValuePair<string, GridCoord>(unit.archetype, placement.Value));
                }
            }

            IReadOnlyList<BondPair> pairs = BondCatalog.Pairs;
            for (int p = 0; p < pairs.Count; p++)
            {
                if (!FoughtSideBySide(pairs[p], deployed))
                {
                    continue;
                }

                BondRecord record = FindBond(pairs[p].Id);
                if (record == null)
                {
                    record = new BondRecord { bond = pairs[p].Id };
                    Data.bonds.Add(record);
                }

                BondRank before = RankFor(record.support);
                record.support += Rules.BondSupportPerBattle;
                BondRank after = RankFor(record.support);
                if (after > before)
                {
                    ups.Add(new BondRankUp { Pair = pairs[p], From = before, To = after });
                }
            }

            RaiseChanged();
            return ups;
        }

        /// <summary>Whether any unit of one half stood next to any unit of the other.</summary>
        private static bool FoughtSideBySide(BondPair pair, List<KeyValuePair<string, GridCoord>> deployed)
        {
            for (int i = 0; i < deployed.Count; i++)
            {
                for (int j = i + 1; j < deployed.Count; j++)
                {
                    if (pair.Matches(deployed[i].Key, deployed[j].Key)
                        && Proximity.IsInProximity(deployed[i].Value, deployed[j].Value))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private BondRank RankFor(int support)
        {
            int[] steps = Rules.BondRankSupport;
            int rank = 0;
            while (rank < steps.Length && rank < (int)BondRank.A && support >= steps[rank])
            {
                rank++;
            }

            return (BondRank)rank;
        }

        private BondRecord FindBond(string bondId)
        {
            for (int i = 0; i < Data.bonds.Count; i++)
            {
                if (Data.bonds[i].bond == bondId)
                {
                    return Data.bonds[i];
                }
            }

            return null;
        }

        private static string LoreFlag(string bondId)
        {
            return "lore." + bondId;
        }
    }
}
