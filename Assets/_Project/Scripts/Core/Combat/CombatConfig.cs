using System;

namespace BinakayanRising.Core.Combat
{
    /// <summary>
    /// How the damage formula turns raw Attack Damage and Defense into a mitigated number.
    /// </summary>
    /// <remarks>
    /// TODO(design): not specified in capstone document. The document names Attack Damage and
    /// Defense as statistics and states that terrain and bonds modify them by percentages, but it
    /// never writes down a damage equation of any kind. Both members below are defensible readings
    /// of the same two stats and they are NOT interchangeable — they imply completely different
    /// stat scales. The team must pick one before any balancing work happens.
    /// </remarks>
    public enum DamageMitigationMode
    {
        /// <summary>
        /// Defense is a flat shield: <c>damage = attack - defense</c>, floored at
        /// <see cref="CombatConfig.MinimumDamage"/>. Implies Defense is measured in the same units
        /// as Attack Damage.
        /// </summary>
        Subtractive = 0,

        /// <summary>
        /// Defense is a damage-reduction fraction: <c>damage = attack * (1 - defense)</c> with
        /// Defense clamped to <c>[0, 1]</c>, floored at <see cref="CombatConfig.MinimumDamage"/>.
        /// Implies Defense is a 0..1 percentage, like Evasion and Critical Hit Chance.
        /// </summary>
        Multiplicative = 1
    }

    /// <summary>
    /// Every tunable number the combat resolver needs, in one plain-C# object.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Read this before touching any value below.</b> The capstone document specifies NONE of
    /// these. It describes a state machine in which <c>AI_Pathfinding</c> and
    /// <c>DamageCalculation</c> alternate "until stage clear or defeat", it names eight statistics,
    /// and it gives percentage modifiers for terrain and for Kapatiran bonds — and that is the
    /// entire specification. There is no damage formula, no crit multiplier, no turn duration, no
    /// accuracy mechanic, no draw condition and no base stat block anywhere in the manuscript.
    /// </para>
    /// <para>
    /// Consequently every field here ships with a placeholder default chosen only so that the
    /// simulation runs at all, and every one is marked TODO(design). Nothing in
    /// <see cref="BattleSimulator"/> or <see cref="StandardDamageFormula"/> hardcodes a balance
    /// number; they read it from here. Changing a value must never require touching resolver logic.
    /// </para>
    /// <para>
    /// The simulator snapshots this object at construction, so mutating a config after a battle has
    /// started cannot change that battle's outcome and cannot break its reproducibility.
    /// </para>
    /// </remarks>
    public sealed class CombatConfig
    {
        private int maxTurns = 100;
        private float criticalHitMultiplier = 2f;
        private float minimumDamage = 1f;
        private ModifierStackingPolicy stackingPolicy = ModifierStackingPolicy.AdditivePercent;
        private bool allowDiagonalMovement;
        private int randomSeed;
        private DamageMitigationMode mitigationMode = DamageMitigationMode.Subtractive;
        private bool applyEvasionRoll = true;
        private bool applyAccuracyRoll = true;
        private BattleOutcome mutualAnnihilationOutcome = BattleOutcome.Draw;
        private bool logModifierEvents = true;
        private bool spanishReceivesTerrainBonuses = true;
        private float healBelowFraction = 0.75f;

        /// <summary>
        /// Hard cap on AI turns. When a battle reaches it, the outcome is
        /// <see cref="BattleOutcome.Draw"/>.
        /// </summary>
        /// <remarks>
        /// TODO(design): not specified in capstone document. The document's state machine loops
        /// "until stage clear or defeat" and offers no third exit, which as written is an infinite
        /// loop whenever neither side can reach the other — two ranged units separated by a bamboo
        /// barricade, for instance. A turn cap is the minimum safe interpretation; whether the game
        /// should surface it to the player as a draw, a defeat, or a "the enemy withdrew" beat is a
        /// design decision. Default 100.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set to zero or a negative value.</exception>
        public int MaxTurns
        {
            get { return maxTurns; }
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "MaxTurns must be greater than zero.");
                }

                maxTurns = value;
            }
        }

        /// <summary>
        /// Multiplier applied to mitigated damage on a critical hit.
        /// </summary>
        /// <remarks>
        /// TODO(design): not specified in capstone document. The document lists Critical Hit Chance
        /// as a statistic and lets Table 3 buff it, but never says what a critical hit actually does.
        /// Default 2.0 (double damage) purely because that is the genre convention.
        /// </remarks>
        public float CriticalHitMultiplier
        {
            get { return criticalHitMultiplier; }
            set { criticalHitMultiplier = value; }
        }

        /// <summary>
        /// Damage floor. A connecting attack always removes at least this much health, so a very
        /// high Defense can never make an attack heal its target or stall a battle forever.
        /// </summary>
        /// <remarks>
        /// TODO(design): not specified in capstone document. No floor, and no damage formula that
        /// would need one, appears anywhere. Default 1.0. Setting it to 0 is legal and makes a
        /// sufficiently armoured unit immune, which will deadlock a battle into the turn cap.
        /// </remarks>
        public float MinimumDamage
        {
            get { return minimumDamage; }
            set { minimumDamage = value < 0f ? 0f : value; }
        }

        /// <summary>
        /// How multiple percentage modifiers on one stat combine.
        /// </summary>
        /// <remarks>
        /// TODO(design): not specified in capstone document. See
        /// <see cref="ModifierStackingPolicy"/> for the full note. Default
        /// <see cref="ModifierStackingPolicy.AdditivePercent"/>.
        /// </remarks>
        public ModifierStackingPolicy StackingPolicy
        {
            get { return stackingPolicy; }
            set { stackingPolicy = value; }
        }

        /// <summary>
        /// When true, units may step diagonally and distances use Chebyshev; when false, movement is
        /// four-way and distances use Manhattan.
        /// </summary>
        /// <remarks>
        /// TODO(design): not specified in capstone document. The document describes an isometric
        /// grid and "autonomous pathfinding" without ever defining a step. Default false (four-way),
        /// because Manhattan distance makes Attack Range read the same way in every direction, which
        /// is easier for a player to judge from an isometric camera.
        /// </remarks>
        public bool AllowDiagonalMovement
        {
            get { return allowDiagonalMovement; }
            set { allowDiagonalMovement = value; }
        }

        /// <summary>
        /// Seed for the battle's <see cref="DeterministicRandom"/>. The same seed with the same
        /// deployment always replays the same battle, event for event.
        /// </summary>
        /// <remarks>
        /// TODO(design): not specified in capstone document. Default 0. Production code should
        /// derive this from something stable and recorded — a mission id combined with an attempt
        /// counter, say — so a battle can be replayed from a save file.
        /// </remarks>
        public int RandomSeed
        {
            get { return randomSeed; }
            set { randomSeed = value; }
        }

        /// <summary>
        /// How Defense mitigates Attack Damage.
        /// </summary>
        /// <remarks>
        /// TODO(design): not specified in capstone document. See <see cref="DamageMitigationMode"/>.
        /// Default <see cref="DamageMitigationMode.Subtractive"/>.
        /// </remarks>
        public DamageMitigationMode MitigationMode
        {
            get { return mitigationMode; }
            set { mitigationMode = value; }
        }

        /// <summary>
        /// When true, the defender rolls Evasion to avoid an attack outright.
        /// </summary>
        /// <remarks>
        /// TODO(design): not specified in capstone document. Evasion is named as a statistic and
        /// Table 2 grants "+15% Evasion" in a trench, but the document never says what Evasion does
        /// mechanically — dodge chance, damage reduction, or hit-chance subtraction are all
        /// consistent with the text. Default true, treating it as a dodge chance.
        /// </remarks>
        public bool ApplyEvasionRoll
        {
            get { return applyEvasionRoll; }
            set { applyEvasionRoll = value; }
        }

        /// <summary>
        /// When true, the attacker rolls Ranged Accuracy to connect.
        /// </summary>
        /// <remarks>
        /// TODO(design): not specified in capstone document. Ranged Accuracy is named as a statistic
        /// and Table 3 buffs it, but the document does not distinguish melee from ranged attacks
        /// anywhere, so there is no basis for applying the roll to only some attacks. Default true,
        /// applied to every attack. Note the consequence: a unit authored with Ranged Accuracy 0
        /// misses every attack. Set this false to disable hit rolls entirely.
        /// </remarks>
        public bool ApplyAccuracyRoll
        {
            get { return applyAccuracyRoll; }
            set { applyAccuracyRoll = value; }
        }

        /// <summary>
        /// Outcome reported when both sides are wiped out on the same turn.
        /// </summary>
        /// <remarks>
        /// TODO(design): not specified in capstone document. The document defines victory as all
        /// enemies routed and defeat as the player's roster completely defeated, and says both are
        /// checked from "remaining unit HP at the end of every AI turn" — so when both are true at
        /// once the two rules contradict each other. Default <see cref="BattleOutcome.Draw"/>;
        /// setting it to <see cref="BattleOutcome.Defeat"/> resolves the tie in the enemy's favour.
        /// Only <see cref="BattleOutcome.Victory"/>, <see cref="BattleOutcome.Defeat"/> and
        /// <see cref="BattleOutcome.Draw"/> are accepted.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set to <see cref="BattleOutcome.InProgress"/>.</exception>
        public BattleOutcome MutualAnnihilationOutcome
        {
            get { return mutualAnnihilationOutcome; }
            set
            {
                if (value == BattleOutcome.InProgress)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value), value, "MutualAnnihilationOutcome must be a terminal outcome.");
                }

                mutualAnnihilationOutcome = value;
            }
        }

        /// <summary>
        /// When true, every modifier applied to every unit is written to the event log each turn.
        /// </summary>
        /// <remarks>
        /// TODO(design): not specified in capstone document. Useful while balancing and for the
        /// determinism tests, noisy for a shipped replay. Default true.
        /// </remarks>
        public bool LogModifierEvents
        {
            get { return logModifierEvents; }
            set { logModifierEvents = value; }
        }

        /// <summary>
        /// When false, Spanish units standing on fortified terrain gain none of its benefits: no
        /// positive stat modifiers and no regeneration. Penalties such as the Coastal Shallows still
        /// apply to them.
        /// </summary>
        /// <remarks>
        /// TODO(design): not specified in capstone document. Table 2 lists terrain effects without
        /// saying who they apply to. Trenches and encampment tents are Katipunan works, so a Spanish
        /// regular climbing into an empty trench and inheriting its cover reads as a bug to players.
        /// Default true (terrain is neutral), which is what every existing test assumes; the playtest
        /// scenario turns it off.
        /// </remarks>
        public bool SpanishReceivesTerrainBonuses
        {
            get { return spanishReceivesTerrainBonuses; }
            set { spanishReceivesTerrainBonuses = value; }
        }

        /// <summary>Returns an independent copy, so a caller can tweak one battle without affecting others.</summary>
        /// <summary>
        /// A healer only spends its turn on an ally whose health is below this fraction of its
        /// effective Max HP; otherwise it fights. TODO(design): not specified in capstone
        /// document — Table 3 names the Field Medic's healing but not when it chooses to heal.
        /// </summary>
        public float HealBelowFraction
        {
            get { return healBelowFraction; }
            set { healBelowFraction = value < 0f ? 0f : (value > 1f ? 1f : value); }
        }

        public CombatConfig Clone()
        {
            return new CombatConfig
            {
                maxTurns = maxTurns,
                criticalHitMultiplier = criticalHitMultiplier,
                minimumDamage = minimumDamage,
                stackingPolicy = stackingPolicy,
                allowDiagonalMovement = allowDiagonalMovement,
                randomSeed = randomSeed,
                mitigationMode = mitigationMode,
                applyEvasionRoll = applyEvasionRoll,
                applyAccuracyRoll = applyAccuracyRoll,
                mutualAnnihilationOutcome = mutualAnnihilationOutcome,
                logModifierEvents = logModifierEvents,
                spanishReceivesTerrainBonuses = spanishReceivesTerrainBonuses,
                healBelowFraction = healBelowFraction
            };
        }
    }
}
