using System;
using BinakayanRising.Core.Content;

namespace BinakayanRising.Core.Meta
{
    /// <summary>
    /// The rules of everything outside a battle, acting on one <see cref="SaveData"/>: the purse,
    /// the Farm and Mine, the Exchange, the roster, the armoury, recruiting, the campaign and the
    /// learning record.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every change to the save goes through here and ends in <see cref="Changed"/>, which is what
    /// the save store listens to. A screen never edits <see cref="Data"/> directly.
    /// </para>
    /// <para>
    /// Pure C#, with the clock injected, so offline accrual, rollback of the system clock and
    /// every price can be tested without the editor.
    /// </para>
    /// </remarks>
    public sealed partial class MetaGame
    {
        private readonly Func<DateTime> utcNow;

        public MetaGame(SaveData data, MetaRules rules, Func<DateTime> utcNow)
        {
            if (data == null) throw new ArgumentNullException("data");
            if (rules == null) throw new ArgumentNullException("rules");
            if (utcNow == null) throw new ArgumentNullException("utcNow");

            Data = data;
            Rules = rules;
            this.utcNow = utcNow;
            Data.Repair(rules);
        }

        public SaveData Data { get; private set; }

        public MetaRules Rules { get; private set; }

        /// <summary>Raised after every change to <see cref="Data"/>.</summary>
        public event Action Changed;

        /// <summary>Raised when a hub-task sub-quest completes itself because its last step was done.</summary>
        public event Action<QuestReward> QuestCompleted;

        /// <summary>
        /// A fresh campaign: the starting purse, the five units Table 3 names for the opening
        /// battles, three bolos, and both facilities starting to produce now.
        /// </summary>
        public static SaveData NewGame(MetaRules rules, DateTime utcNow, int seed)
        {
            var data = new SaveData
            {
                createdUtcTicks = utcNow.Ticks,
                savedUtcTicks = utcNow.Ticks,
                reales = rules.StartingReales,
                rations = rules.StartingRations,
                scrap = rules.StartingScrap,
                gachaSeed = seed
            };

            data.farm.sinceUtcTicks = utcNow.Ticks;
            data.mine.sinceUtcTicks = utcNow.Ticks;

            string[] starters =
            {
                UnitCatalog.Evangelista, UnitCatalog.Aguinaldo, UnitCatalog.Marksman,
                UnitCatalog.Engineer, UnitCatalog.Vanguard
            };

            for (int i = 0; i < starters.Length; i++)
            {
                data.units.Add(new OwnedUnit { id = data.nextUnitId++, archetype = starters[i], level = 1 });
            }

            for (int i = 0; i < 3; i++)
            {
                data.weapons.Add(new OwnedWeapon { id = data.nextWeaponId++, weapon = WeaponCatalog.Bolo });
            }

            // The Vanguard starts armed so the armoury has something to show; the other two
            // bolos wait on the rack for the player's first equip.
            data.units[4].weaponId = data.weapons[0].id;

            return data;
        }

        public DateTime Now
        {
            get { return utcNow(); }
        }

        private void RaiseChanged()
        {
            Action handler = Changed;
            if (handler != null)
            {
                handler();
            }
        }

        // ------------------------------------------------------------------ purse

        public int Balance(Currency currency)
        {
            switch (currency)
            {
                case Currency.Rations:
                    return Data.rations;
                case Currency.Scrap:
                    return Data.scrap;
                default:
                    return Data.reales;
            }
        }

        public bool CanAfford(Cost cost)
        {
            return Data.reales >= cost.Reales && Data.rations >= cost.Rations && Data.scrap >= cost.Scrap;
        }

        /// <summary>Spends <paramref name="cost"/> in full, or nothing at all.</summary>
        public bool TrySpend(Cost cost)
        {
            if (!CanAfford(cost))
            {
                return false;
            }

            Data.reales -= cost.Reales;
            Data.rations -= cost.Rations;
            Data.scrap -= cost.Scrap;
            RaiseChanged();
            return true;
        }

        public void Earn(Currency currency, int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            AddSilently(currency, amount);
            RaiseChanged();
        }

        private void AddSilently(Currency currency, int amount)
        {
            switch (currency)
            {
                case Currency.Rations:
                    Data.rations = SafeAdd(Data.rations, amount);
                    break;
                case Currency.Scrap:
                    Data.scrap = SafeAdd(Data.scrap, amount);
                    break;
                default:
                    Data.reales = SafeAdd(Data.reales, amount);
                    break;
            }
        }

        private static int SafeAdd(int a, int b)
        {
            long sum = (long)a + b;
            return sum > int.MaxValue ? int.MaxValue : (int)sum;
        }

        // ------------------------------------------------------------------ flags

        public bool HasFlag(string flag)
        {
            return Data.HasFlag(flag);
        }

        /// <summary>Sets a one-way progress flag.</summary>
        /// <returns>True when the flag was new.</returns>
        public bool SetFlag(string flag)
        {
            if (string.IsNullOrEmpty(flag) || Data.flags.Contains(flag))
            {
                return false;
            }

            Data.flags.Add(flag);
            RaiseChanged();
            return true;
        }

        /// <summary>
        /// Records that the player did a hub-task step (harvested, traded, drilled...). The step
        /// counts toward the current sub-quest only if that sub-quest asks for it, so a step done
        /// early is asked for again when its sub-quest comes up — the navigation guide stays a
        /// guide rather than a list of things that silently already happened.
        /// </summary>
        public void MarkTask(string task)
        {
            MarkTaskSilently(task);
            RaiseChanged();
        }

        // ------------------------------------------------------------------ farm and mine

        private FacilityState StateOf(Facility facility)
        {
            return facility == Facility.Mine ? Data.mine : Data.farm;
        }

        /// <summary>
        /// Seconds of production banked since the facility was last emptied. A clock that has run
        /// backwards counts as no time at all rather than as negative production.
        /// </summary>
        private double ElapsedSeconds(Facility facility)
        {
            long ticks = Now.Ticks - StateOf(facility).sinceUtcTicks;
            return ticks <= 0 ? 0.0 : ticks / (double)TimeSpan.TicksPerSecond;
        }

        /// <summary>Goods waiting to be harvested.</summary>
        public int Stored(Facility facility)
        {
            FacilityRule rule = Rules.For(facility);
            double produced = Math.Floor(ElapsedSeconds(facility) / rule.SecondsPerUnit);
            return produced >= rule.StorageCap ? rule.StorageCap : (int)produced;
        }

        public bool IsFull(Facility facility)
        {
            return Stored(facility) >= Rules.For(facility).StorageCap;
        }

        /// <summary>Progress towards the next unit of goods, 0..1. Exactly 1 when full.</summary>
        public float Progress(Facility facility)
        {
            if (IsFull(facility))
            {
                return 1f;
            }

            FacilityRule rule = Rules.For(facility);
            double partial = ElapsedSeconds(facility) % rule.SecondsPerUnit;
            return (float)(partial / rule.SecondsPerUnit);
        }

        /// <summary>Seconds until the next unit of goods, or 0 when full.</summary>
        public int SecondsToNext(Facility facility)
        {
            if (IsFull(facility))
            {
                return 0;
            }

            FacilityRule rule = Rules.For(facility);
            double partial = ElapsedSeconds(facility) % rule.SecondsPerUnit;
            return (int)Math.Ceiling(rule.SecondsPerUnit - partial);
        }

        /// <summary>Moves the stored goods into the purse.</summary>
        /// <returns>How much was harvested.</returns>
        public int Harvest(Facility facility)
        {
            FacilityRule rule = Rules.For(facility);
            FacilityState state = StateOf(facility);
            int amount = Stored(facility);
            long now = Now.Ticks;

            if (amount >= rule.StorageCap || state.sinceUtcTicks > now)
            {
                // Production stopped at the cap, so nothing past it is owed; and a clock that ran
                // backwards restarts the count from the present.
                state.sinceUtcTicks = now;
            }
            else
            {
                // Keep the partial progress towards the next unit.
                state.sinceUtcTicks += amount * rule.SecondsPerUnit * TimeSpan.TicksPerSecond;
            }

            if (amount > 0)
            {
                AddSilently(rule.Produces, amount);
                MarkTaskSilently(facility == Facility.Mine ? Campaign.TaskHarvestMine : Campaign.TaskHarvestFarm);
            }

            RaiseChanged();
            return amount;
        }

        // ------------------------------------------------------------------ exchange

        /// <summary>How many whole lots of <paramref name="goods"/> the purse can sell.</summary>
        public int SellableLots(Currency goods)
        {
            ExchangeRate rate = Rules.RateFor(goods);
            return rate == null ? 0 : Balance(goods) / rate.LotSize;
        }

        /// <summary>Sells <paramref name="lots"/> lots of <paramref name="goods"/> for Reales.</summary>
        /// <returns>Reales received, or 0 when the trade was refused.</returns>
        public int Exchange(Currency goods, int lots)
        {
            ExchangeRate rate = Rules.RateFor(goods);
            if (rate == null || lots <= 0 || SellableLots(goods) < lots)
            {
                return 0;
            }

            int reales = rate.RealesPerLot * lots;
            Cost price = Cost.Of(goods, rate.LotSize * lots);
            Data.rations -= price.Rations;
            Data.scrap -= price.Scrap;
            AddSilently(Currency.Reales, reales);
            MarkTaskSilently(Campaign.TaskExchange);
            RaiseChanged();
            return reales;
        }

        // ------------------------------------------------------------------ play time

        public void AddPlayTime(double seconds)
        {
            if (seconds > 0 && !double.IsNaN(seconds) && !double.IsInfinity(seconds))
            {
                Data.playSeconds += seconds;
            }
        }

        private void MarkTaskSilently(string task)
        {
            Quest quest = CurrentQuest;
            if (quest == null || quest.Kind != QuestKind.HubTask || System.Array.IndexOf(quest.Tasks, task) < 0)
            {
                return;
            }

            string scoped = TaskFlag(quest, task);
            if (!Data.flags.Contains(scoped))
            {
                Data.flags.Add(scoped);
                CompleteHubTaskIfDone();
            }
        }

        private static string TaskFlag(Quest quest, string task)
        {
            return "task." + quest.Id + "." + task;
        }
    }
}
