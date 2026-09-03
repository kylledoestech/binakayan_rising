namespace BinakayanRising.Core.Grid
{
    /// <summary>
    /// The kinds of ground a battle cell can be made of, as specified by Table 2 "Environmental
    /// Terrain Modifiers" in the capstone design document. Drawn from the terrain of the Battle of
    /// Binakayan-Dalahican (November 1896): Evangelista's trench line, the tidal shallows along the
    /// Dalahican shore, bamboo barricades, and the encampments at the rear.
    /// </summary>
    /// <remarks>
    /// <b>No modifier values here.</b> The percentages quoted in each member's summary are
    /// documentation of the design intent only and must NOT be hardcoded against this enum. They
    /// live in ScriptableObject assets in the <c>BinakayanRising.Data</c> assembly so designers can
    /// tune them without a recompile. This enum carries identity only.
    /// <para>
    /// Values are assigned explicitly because this enum will be serialised into saved battles and
    /// authored map assets; never renumber an existing member.
    /// </para>
    /// </remarks>
    public enum TerrainType
    {
        /// <summary>
        /// Flat grass, dirt paths, or clearing. No modifiers; units use their base statistics.
        /// This is the default fill for a new grid.
        /// </summary>
        StandardGrid = 0,

        /// <summary>
        /// Evangelista's trench: dug-out earthworks reinforced with bamboo.
        /// Design intent: +20% Defense, +15% Evasion.
        /// </summary>
        Trench = 1,

        /// <summary>
        /// Water tiles near the Dalahican shore.
        /// Design intent: -15% Movement Speed, -10% Defense.
        /// </summary>
        CoastalShallows = 2,

        /// <summary>
        /// Sharp wooden defensive structures. Impassable; blocks pathfinding.
        /// </summary>
        BambooBarricade = 3,

        /// <summary>
        /// Safe zones at the rear of the grid.
        /// Design intent: +5% HP regeneration per AI turn.
        /// </summary>
        EncampmentTent = 4
    }
}
