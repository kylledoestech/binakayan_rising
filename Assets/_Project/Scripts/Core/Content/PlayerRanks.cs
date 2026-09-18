using BinakayanRising.Core.Localization;

namespace BinakayanRising.Core.Content
{
    /// <summary>One step of the player's rank ladder.</summary>
    public sealed class PlayerRank
    {
        public readonly int Index;

        /// <summary>The Filipino rank title, the same in both languages.</summary>
        public readonly string Title;

        /// <summary>What the title means.</summary>
        public readonly LocString Gloss;

        public PlayerRank(int index, string title, LocString gloss)
        {
            Index = index;
            Title = title;
            Gloss = gloss;
        }
    }

    /// <summary>
    /// The Tactician's rank: one title per campaign level cleared (panel feedback: "name
    /// identification per level"). Revolutionary-army titles, in the Filipino forms of the
    /// Spanish ranks the Katipunan adopted.
    /// </summary>
    public static class PlayerRanks
    {
        private static readonly PlayerRank[] ladder =
        {
            new PlayerRank(0, "Kawal", new LocString("Private", "Sundalo")),
            new PlayerRank(1, "Kabo", new LocString("Corporal", "Pinuno ng pulutong")),
            new PlayerRank(2, "Kapitan", new LocString("Captain", "Pinuno ng kompanya")),
            new PlayerRank(3, "Heneral", new LocString("General", "Pinuno ng hukbo"))
        };

        public static int Count
        {
            get { return ladder.Length; }
        }

        /// <summary>The rank for <paramref name="levelsCleared"/> campaign levels, clamped to the ladder.</summary>
        public static PlayerRank ForLevelsCleared(int levelsCleared)
        {
            int index = levelsCleared < 0 ? 0 : (levelsCleared >= ladder.Length ? ladder.Length - 1 : levelsCleared);
            return ladder[index];
        }

        public static PlayerRank At(int index)
        {
            return ForLevelsCleared(index);
        }
    }
}
