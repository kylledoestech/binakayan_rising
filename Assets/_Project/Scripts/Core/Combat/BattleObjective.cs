using BinakayanRising.Core.Grid;

namespace BinakayanRising.Core.Combat
{
    /// <summary>What a battle is won by. DESIGN-DECISIONS #21.</summary>
    public enum ObjectiveKind
    {
        /// <summary>Rout every enemy; lose when every Katipunan unit falls. The document's rule.</summary>
        Rout = 0,

        /// <summary>
        /// Keep one Katipunan unit — the escorted one — alive until the turn cap. Losing it loses
        /// the battle at once; reaching the cap with it standing, or routing the enemy, wins.
        /// </summary>
        Escort = 1,

        /// <summary>
        /// End a turn with any Katipunan unit on the target cell. The squad marches on it, fighting
        /// only what stands in reach. Reaching the turn cap, or losing the squad, loses.
        /// </summary>
        Sabotage = 2
    }

    /// <summary>
    /// The win rule a <see cref="BattleSimulator"/> evaluates at the end of every turn, plus what
    /// the rule needs to know: which unit is escorted, which cell is the target.
    /// </summary>
    /// <remarks>
    /// Immutable, so <see cref="CombatConfig.Clone"/> can share it. The rule is part of the
    /// simulation rather than a presentation-layer reinterpretation of a draw, so a battle's event
    /// log ends with the outcome the player actually got.
    /// </remarks>
    public sealed class BattleObjective
    {
        /// <summary>The default: rout every enemy.</summary>
        public static readonly BattleObjective RoutAll = new BattleObjective(ObjectiveKind.Rout, -1, GridCoord.Zero, 0f);

        private readonly ObjectiveKind kind;
        private readonly int escortUnitId;
        private readonly GridCoord targetCell;
        private readonly float seekerSpeed;

        private BattleObjective(ObjectiveKind kind, int escortUnitId, GridCoord targetCell, float seekerSpeed)
        {
            this.kind = kind;
            this.escortUnitId = escortUnitId;
            this.targetCell = targetCell;
            this.seekerSpeed = seekerSpeed > 0f ? seekerSpeed : 0f;
        }

        /// <summary>Keep the unit with <paramref name="unitId"/> alive until the turn cap.</summary>
        public static BattleObjective Escort(int unitId)
        {
            return new BattleObjective(ObjectiveKind.Escort, unitId, GridCoord.Zero, 0f);
        }

        /// <summary>
        /// Reach <paramref name="cell"/>. Katipunan units slower than <paramref name="seekerSpeed"/>
        /// are brought up to it for this battle: a trench line that never moves cannot infiltrate.
        /// </summary>
        public static BattleObjective Sabotage(GridCoord cell, float seekerSpeed = 1f)
        {
            return new BattleObjective(ObjectiveKind.Sabotage, -1, cell, seekerSpeed);
        }

        /// <summary>Which rule this is.</summary>
        public ObjectiveKind Kind
        {
            get { return kind; }
        }

        /// <summary>The escorted unit's id under <see cref="ObjectiveKind.Escort"/>; otherwise -1.</summary>
        public int EscortUnitId
        {
            get { return escortUnitId; }
        }

        /// <summary>The cell to reach under <see cref="ObjectiveKind.Sabotage"/>.</summary>
        public GridCoord TargetCell
        {
            get { return targetCell; }
        }

        /// <summary>The least Movement Speed a Katipunan unit has under <see cref="ObjectiveKind.Sabotage"/>.</summary>
        public float SeekerSpeed
        {
            get { return seekerSpeed; }
        }
    }
}
