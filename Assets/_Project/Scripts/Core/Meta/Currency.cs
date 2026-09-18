namespace BinakayanRising.Core.Meta
{
    /// <summary>The three currencies of Appendix F.</summary>
    public enum Currency
    {
        /// <summary>Coins. Earned from quizzes, missions and the Exchange; spent on recruiting and training.</summary>
        Reales = 0,

        /// <summary>Food. Grown on the Farm; spent to enter a mission; sold at the Exchange.</summary>
        Rations = 1,

        /// <summary>Metal. Dug from the Mine; spent on training and synthesis; sold at the Exchange.</summary>
        Scrap = 2
    }

    /// <summary>A price in any mix of the three currencies.</summary>
    public struct Cost
    {
        public readonly int Reales;
        public readonly int Rations;
        public readonly int Scrap;

        public Cost(int reales, int rations, int scrap)
        {
            Reales = reales < 0 ? 0 : reales;
            Rations = rations < 0 ? 0 : rations;
            Scrap = scrap < 0 ? 0 : scrap;
        }

        /// <summary>A price in a single currency.</summary>
        public static Cost Of(Currency currency, int amount)
        {
            switch (currency)
            {
                case Currency.Rations:
                    return new Cost(0, amount, 0);
                case Currency.Scrap:
                    return new Cost(0, 0, amount);
                default:
                    return new Cost(amount, 0, 0);
            }
        }

        public bool IsFree
        {
            get { return Reales == 0 && Rations == 0 && Scrap == 0; }
        }

        public int Amount(Currency currency)
        {
            switch (currency)
            {
                case Currency.Rations:
                    return Rations;
                case Currency.Scrap:
                    return Scrap;
                default:
                    return Reales;
            }
        }
    }
}
