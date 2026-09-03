using System;
using System.Collections.Generic;
using UnityEngine;

namespace BinakayanRising.Data
{
    /// <summary>
    /// One rarity tier of the Hero Summoning gacha and the relative weight with which it is drawn.
    /// </summary>
    /// <remarks>
    /// TODO(design): the capstone document names the gacha ("randomized gacha hero acquisition",
    /// "Reales ... used for the Hero Summoning gacha") but publishes no rarity ladder and no pull
    /// rates. Tier names and weights are therefore authored entirely in the Inspector, which keeps
    /// the rates data-driven once the team decides them.
    /// </remarks>
    [Serializable]
    public sealed class GachaRarityTier
    {
        [Tooltip("Display name of the tier, e.g. \"Common\" or \"Heroic\". TODO(design): the ladder is unspecified.")]
        [SerializeField] private string tierName = string.Empty;

        [Tooltip("Relative draw weight. Rates are weight / sum of all weights, so weights need not " +
                 "sum to 1 or 100. TODO(design): no pull rates are given in the document.")]
        [Min(0f)]
        [SerializeField] private float weight = 0f;

        /// <summary>Display name of the tier.</summary>
        public string TierName => tierName;

        /// <summary>
        /// Relative draw weight. The effective rate is this weight divided by
        /// <see cref="EconomyConfig.TotalRarityWeight"/>.
        /// </summary>
        public float Weight => weight;
    }

    /// <summary>
    /// A single Synthesis facility recipe cost, in the currencies it consumes.
    /// </summary>
    /// <remarks>
    /// TODO(design): the document describes the Synthesis facility ("Combining scrap metal rewards
    /// the player with upgraded equipment ... transitioning a unit from wielding a standard bolo to
    /// a captured Mauser rifle") but gives no recipe, no cost, and no output stat gain.
    /// </remarks>
    [Serializable]
    public sealed class SynthesisRecipeCost
    {
        [Tooltip("Identifier of the equipment this recipe produces. TODO(design): recipe list unspecified.")]
        [SerializeField] private string recipeId = string.Empty;

        [Tooltip("Scrap Metal consumed. TODO(design): unspecified in the capstone document.")]
        [Min(0)]
        [SerializeField] private int scrapMetalCost = 0;

        [Tooltip("Reales consumed alongside the Scrap Metal, if any. TODO(design): unspecified.")]
        [Min(0)]
        [SerializeField] private int realesCost = 0;

        /// <summary>Identifier of the equipment this recipe produces.</summary>
        public string RecipeId => recipeId;

        /// <summary>Scrap Metal consumed by the recipe.</summary>
        public int ScrapMetalCost => scrapMetalCost;

        /// <summary>Reales consumed by the recipe, if any.</summary>
        public int RealesCost => realesCost;
    }

    /// <summary>
    /// Campaign-wide tuning for the three player currencies: Reales, Rations and Scrap Metal.
    /// A single asset is authored for the whole game.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The three currencies come from the Player Properties section of the capstone document:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>
    ///     <b>Reales (Virtual Currency)</b> — "Used for the Hero Summoning gacha."
    ///   </description></item>
    ///   <item><description>
    ///     <b>Rations (Energy)</b> — "Required to deploy units into the Mission Portal. ... Depletes
    ///     upon entering a stage."
    ///   </description></item>
    ///   <item><description>
    ///     <b>Scrap Metal (Crafting)</b> — "Used in the Synthesis facility. ... Gathered over time
    ///     from the Mines."
    ///   </description></item>
    /// </list>
    /// <para>
    /// Exactly ONE number in this file comes from the document: the +50 Reales paid for a correct
    /// quiz answer, which Capstone Table 4 awards in all four sample rows. Every other field is a
    /// TODO(design) placeholder at a neutral default. The document explicitly rules out real-money
    /// microtransactions but says nothing about pull cost, rarity ladders, pull rates, the Rations
    /// cap, Rations regeneration, or synthesis costs.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(fileName = "EconomyConfig", menuName = "Binakayan Rising/Economy Config", order = 6)]
    public sealed class EconomyConfig : ScriptableObject
    {
        [Header("Reales (Gacha Currency)")]
        [Tooltip("Reales awarded for a correct pop-up quiz answer. Capstone Table 4 awards +50 in " +
                 "every sample row. Individual QuizQuestionData assets carry their own value; this " +
                 "is the campaign-wide default used when authoring new questions.")]
        [Min(0)]
        [SerializeField] private int quizCorrectAnswerRewardReales = 50;

        [Tooltip("Reales spent on one Hero Summoning pull. TODO(design): unspecified in the document.")]
        [Min(0)]
        [SerializeField] private int singlePullCostReales = 0;

        [Tooltip("How many heroes a multi-pull draws. TODO(design): the document never mentions multi-pulls.")]
        [Min(0)]
        [SerializeField] private int multiPullCount = 0;

        [Tooltip("Reales spent on one multi-pull. TODO(design): unspecified in the document.")]
        [Min(0)]
        [SerializeField] private int multiPullCostReales = 0;

        [Tooltip("Starting Reales balance for a new campaign save. TODO(design): unspecified.")]
        [Min(0)]
        [SerializeField] private int startingReales = 0;

        [Header("Gacha Rarity Tiers")]
        // TODO(design): not specified in capstone document. No rarity ladder and no pull rates are
        // published, so this list ships empty; rates are weight / TotalRarityWeight once authored.
        [Tooltip("Rarity ladder and relative draw weights. TODO(design): no rates are given in the document.")]
        [SerializeField] private List<GachaRarityTier> rarityTiers = new List<GachaRarityTier>();

        [Header("Rations (Stage Energy)")]
        // TODO(design): not specified in capstone document. The document says only that Rations are
        // required to enter a stage and deplete on entry; no cap, no regeneration rate, no refill.
        [Tooltip("Maximum Rations the player can hold. TODO(design): unspecified in the document.")]
        [Min(0)]
        [SerializeField] private int rationsCap = 0;

        [Tooltip("Rations restored per regeneration tick. TODO(design): unspecified in the document.")]
        [Min(0)]
        [SerializeField] private int rationsRegenAmount = 0;

        [Tooltip("Real-time minutes between regeneration ticks. TODO(design): unspecified in the document.")]
        [Min(0f)]
        [SerializeField] private float rationsRegenIntervalMinutes = 0f;

        [Tooltip("Rations charged when a mission asset does not override the cost. " +
                 "TODO(design): unspecified in the document.")]
        [Min(0)]
        [SerializeField] private int defaultMissionRationsCost = 0;

        [Header("Scrap Metal (Crafting / Synthesis)")]
        // TODO(design): not specified in capstone document. Mine yield and every synthesis recipe
        // cost are undefined, so the yield defaults to 0 and the recipe list ships empty.
        [Tooltip("Scrap Metal produced per Mine collection. TODO(design): unspecified in the document.")]
        [Min(0)]
        [SerializeField] private int scrapMetalPerMineCollection = 0;

        [Tooltip("Real-time minutes between Mine collections. TODO(design): unspecified in the document.")]
        [Min(0f)]
        [SerializeField] private float scrapMetalCollectionIntervalMinutes = 0f;

        [Tooltip("Synthesis facility recipes and their costs. TODO(design): no recipe costs are given.")]
        [SerializeField] private List<SynthesisRecipeCost> synthesisRecipes = new List<SynthesisRecipeCost>();

        /// <summary>
        /// Reales awarded for a correct pop-up quiz answer. Capstone Table 4 awards +50 in every
        /// sample row; a <see cref="QuizQuestionData"/> asset may carry its own value.
        /// </summary>
        public int QuizCorrectAnswerRewardReales => quizCorrectAnswerRewardReales;

        /// <summary>Reales spent on one Hero Summoning pull. TODO(design): unspecified.</summary>
        public int SinglePullCostReales => singlePullCostReales;

        /// <summary>How many heroes a multi-pull draws. TODO(design): unspecified.</summary>
        public int MultiPullCount => multiPullCount;

        /// <summary>Reales spent on one multi-pull. TODO(design): unspecified.</summary>
        public int MultiPullCostReales => multiPullCostReales;

        /// <summary>Starting Reales balance for a new campaign save. TODO(design): unspecified.</summary>
        public int StartingReales => startingReales;

        /// <summary>Rarity ladder and relative draw weights. TODO(design): unspecified.</summary>
        public IReadOnlyList<GachaRarityTier> RarityTiers => rarityTiers;

        /// <summary>Maximum Rations the player can hold. TODO(design): unspecified.</summary>
        public int RationsCap => rationsCap;

        /// <summary>Rations restored per regeneration tick. TODO(design): unspecified.</summary>
        public int RationsRegenAmount => rationsRegenAmount;

        /// <summary>Real-time minutes between regeneration ticks. TODO(design): unspecified.</summary>
        public float RationsRegenIntervalMinutes => rationsRegenIntervalMinutes;

        /// <summary>
        /// Rations charged when a <see cref="MissionData"/> asset does not override the cost.
        /// TODO(design): unspecified.
        /// </summary>
        public int DefaultMissionRationsCost => defaultMissionRationsCost;

        /// <summary>Scrap Metal produced per Mine collection. TODO(design): unspecified.</summary>
        public int ScrapMetalPerMineCollection => scrapMetalPerMineCollection;

        /// <summary>Real-time minutes between Mine collections. TODO(design): unspecified.</summary>
        public float ScrapMetalCollectionIntervalMinutes => scrapMetalCollectionIntervalMinutes;

        /// <summary>Synthesis facility recipes and their costs. TODO(design): unspecified.</summary>
        public IReadOnlyList<SynthesisRecipeCost> SynthesisRecipes => synthesisRecipes;

        /// <summary>
        /// Sum of every rarity tier weight, i.e. the denominator for turning a tier weight into a
        /// pull rate. Returns 0 while the ladder is unauthored, which callers must treat as
        /// "gacha not configured" rather than dividing by it.
        /// </summary>
        public float TotalRarityWeight
        {
            get
            {
                float total = 0f;

                for (int i = 0; i < rarityTiers.Count; i++)
                {
                    GachaRarityTier tier = rarityTiers[i];

                    if (tier != null)
                    {
                        total += tier.Weight;
                    }
                }

                return total;
            }
        }

        /// <summary>
        /// Normalised draw rate of a rarity tier as a 0..1 fraction, or 0 when the ladder is
        /// unauthored or the tier is not part of it.
        /// </summary>
        /// <param name="tier">Tier to measure.</param>
        public float GetPullRate(GachaRarityTier tier)
        {
            if (tier == null)
            {
                return 0f;
            }

            float total = TotalRarityWeight;

            if (total <= 0f)
            {
                return 0f;
            }

            return tier.Weight / total;
        }

        /// <summary>
        /// Returns the recipe with the given id, or <c>null</c> when the config has no such recipe.
        /// </summary>
        /// <param name="recipeId">Identifier to look up.</param>
        public SynthesisRecipeCost GetSynthesisRecipe(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId))
            {
                return null;
            }

            for (int i = 0; i < synthesisRecipes.Count; i++)
            {
                SynthesisRecipeCost recipe = synthesisRecipes[i];

                if (recipe != null && recipe.RecipeId == recipeId)
                {
                    return recipe;
                }
            }

            return null;
        }
    }
}
