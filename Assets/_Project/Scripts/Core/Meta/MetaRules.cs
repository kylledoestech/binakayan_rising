namespace BinakayanRising.Core.Meta
{
    /// <summary>How fast a facility produces, and how much it holds before it stops.</summary>
    public sealed class FacilityRule
    {
        /// <summary>What the facility produces.</summary>
        public readonly Currency Produces;

        /// <summary>Real seconds to produce one unit.</summary>
        public readonly int SecondsPerUnit;

        /// <summary>Uncollected output stops growing at this amount until the player harvests.</summary>
        public readonly int StorageCap;

        public FacilityRule(Currency produces, int secondsPerUnit, int storageCap)
        {
            Produces = produces;
            SecondsPerUnit = secondsPerUnit < 1 ? 1 : secondsPerUnit;
            StorageCap = storageCap < 1 ? 1 : storageCap;
        }
    }

    /// <summary>One line of the Exchange's price board: a lot of goods for a number of Reales.</summary>
    public sealed class ExchangeRate
    {
        /// <summary>The goods sold. Never <see cref="Currency.Reales"/>.</summary>
        public readonly Currency Sells;

        /// <summary>How many goods one trade takes.</summary>
        public readonly int LotSize;

        /// <summary>Reales paid for one lot.</summary>
        public readonly int RealesPerLot;

        public ExchangeRate(Currency sells, int lotSize, int realesPerLot)
        {
            Sells = sells;
            LotSize = lotSize < 1 ? 1 : lotSize;
            RealesPerLot = realesPerLot < 0 ? 0 : realesPerLot;
        }
    }

    /// <summary>
    /// Every number the encampment, progression, recruiting and assessment systems run on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The capstone document fixes only one of these — +50 Reales per correct quiz answer
    /// (Table 4). Everything else is a placeholder tuned so the whole loop can be played through
    /// in a panel demo without grinding, and is listed in <c>Docs/DESIGN-DECISIONS.md</c>.
    /// </para>
    /// <para>
    /// A plain object rather than a ScriptableObject so the rules can be unit-tested with the
    /// editor closed. Nothing reads a number from anywhere but here.
    /// </para>
    /// </remarks>
    public sealed class MetaRules
    {
        // ------------------------------------------------------------------ new game

        // TODO(design): not specified in capstone document — starting purse.
        public int StartingReales = 300;
        public int StartingRations = 20;
        public int StartingScrap = 10;

        // ------------------------------------------------------------------ farm, mine, exchange

        // TODO(design): not specified in capstone document — the proposal names Farms and Mines
        // but gives no rates. Fast enough that a demo sees both fill within a few minutes.
        public FacilityRule Farm = new FacilityRule(Currency.Rations, 20, 30);
        public FacilityRule Mine = new FacilityRule(Currency.Scrap, 40, 20);

        // TODO(design): not specified in capstone document — the Exchange is new (panel
        // feedback: "harvest purpose — conversion to coins"). Scrap is worth more because it is
        // slower to dig and is also needed for training and synthesis.
        public ExchangeRate[] ExchangeRates =
        {
            new ExchangeRate(Currency.Rations, 10, 15),
            new ExchangeRate(Currency.Scrap, 10, 25)
        };

        // ------------------------------------------------------------------ unit progression

        // TODO(design): not specified in capstone document — level cap, curve and growth (#15).
        public int LevelCap = 10;

        /// <summary>XP to go from level L to L+1 is <c>XpPerLevel × L</c>.</summary>
        public int XpPerLevel = 100;

        /// <summary>Fractional growth per level above 1, applied to the archetype's base stat.</summary>
        public float HpGrowthPerLevel = 0.08f;
        public float AttackGrowthPerLevel = 0.05f;
        public float DefenseGrowthPerLevel = 0.04f;

        /// <summary>XP every deployed unit earns from a battle.</summary>
        public int VictoryXp = 60;
        public int DefeatXp = 20;

        /// <summary>One paid drill at the Training Grounds.</summary>
        public int DrillXp = 100;
        public Cost DrillCost = new Cost(80, 0, 5);

        // ------------------------------------------------------------------ recruiting (gacha)

        // TODO(design): not specified in capstone document — tiers, rates, costs, pity (#7).
        // Published in-game and kept deliberately modest: this is an offline educational game.
        public int SinglePullCost = 100;
        public int MultiPullCount = 10;
        public int MultiPullCost = 900;

        /// <summary>Relative weights for Common, Rare and Hero, in that order.</summary>
        public int[] RarityWeights = { 70, 25, 5 };

        /// <summary>The pull that brings the Hero counter to this number is a guaranteed Hero.</summary>
        public int PityThreshold = 10;

        /// <summary>XP a unique Hero receives when recruited again.</summary>
        public int DuplicateHeroXp = 150;

        // ------------------------------------------------------------------ learning

        /// <summary>Table 4: +50 Reales per correct answer. The only number here the document fixes.</summary>
        public int QuizCorrectReales = 50;

        // TODO(design): not specified in capstone document — assessment length and pass mark.
        public int AssessmentQuestions = 5;
        public int AssessmentPassPercent = 60;
        public int AssessmentFirstPassReales = 150;

        public static MetaRules Default()
        {
            return new MetaRules();
        }

        /// <summary>The facility rule for <paramref name="facility"/>.</summary>
        public FacilityRule For(Facility facility)
        {
            return facility == Facility.Mine ? Mine : Farm;
        }

        /// <summary>XP needed to go from <paramref name="level"/> to the next.</summary>
        public int XpToNext(int level)
        {
            return XpPerLevel * (level < 1 ? 1 : level);
        }

        /// <summary>The Exchange line that sells <paramref name="goods"/>, or null.</summary>
        public ExchangeRate RateFor(Currency goods)
        {
            for (int i = 0; i < ExchangeRates.Length; i++)
            {
                if (ExchangeRates[i].Sells == goods)
                {
                    return ExchangeRates[i];
                }
            }

            return null;
        }
    }

    /// <summary>The two producing facilities of the encampment.</summary>
    public enum Facility
    {
        Farm = 0,
        Mine = 1
    }
}
