using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Grid;
using UnityEngine;

namespace BinakayanRising.Gameplay
{
    /// <summary>
    /// Where the board gets its sprites from, with procedural placeholders as the floor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The themed art lives in the UI assembly, because that is where the palette lives, and this
    /// assembly must not reference it — simulation code that can name a widget eventually reaches
    /// into one. So the UI registers providers here at startup and this class simply asks.
    /// </para>
    /// <para>
    /// Every provider is optional. With none registered the board still draws, using
    /// <see cref="PlaceholderArt"/>'s generated shapes, which is what keeps the project runnable
    /// on a fresh clone and what the offline <c>Tools/battle-sim</c> path depends on.
    /// </para>
    /// </remarks>
    public static class BoardArt
    {
        /// <summary>Supplies a finished tile sprite for a terrain type.</summary>
        public static System.Func<TerrainType, Sprite> TileProvider;

        /// <summary>Supplies a unit token sprite for a side.</summary>
        public static System.Func<Team, Sprite> TokenProvider;

        /// <summary>Supplies the marker drawn over a deployable cell.</summary>
        public static System.Func<Sprite> DeployMarkerProvider;

        /// <summary>Supplies the shadow drawn under a unit.</summary>
        public static System.Func<Sprite> ShadowProvider;

        /// <summary>
        /// True when tiles come with their own colour baked in.
        /// </summary>
        /// <remarks>
        /// Callers must not tint a themed tile. The placeholder tile is a white diamond that only
        /// becomes terrain once a colour is multiplied over it, but a painted tile already carries
        /// its earth, water and timber tones — multiplying a terrain colour over that a second time
        /// turns the whole board to mud.
        /// </remarks>
        public static bool TilesAreThemed => TileProvider != null;

        /// <summary>True when tokens come with their own colour baked in.</summary>
        public static bool TokensAreThemed => TokenProvider != null;

        /// <summary>True when the deployment marker is drawn rather than a tinted placeholder.</summary>
        public static bool DeployMarkerIsThemed => DeployMarkerProvider != null;

        /// <summary>True when the shadow is drawn rather than a tinted placeholder ring.</summary>
        public static bool ShadowIsThemed => ShadowProvider != null;

        /// <summary>The tile sprite for a terrain type.</summary>
        public static Sprite Tile(TerrainType terrain)
        {
            Sprite themed = TileProvider?.Invoke(terrain);
            return themed != null ? themed : PlaceholderArt.Tile;
        }

        /// <summary>The token sprite for a side.</summary>
        public static Sprite Token(Team team)
        {
            Sprite themed = TokenProvider?.Invoke(team);
            return themed != null ? themed : PlaceholderArt.Token;
        }

        /// <summary>The marker for a deployable cell.</summary>
        public static Sprite DeployMarker()
        {
            Sprite themed = DeployMarkerProvider?.Invoke();
            return themed != null ? themed : PlaceholderArt.Tile;
        }

        /// <summary>The shadow blob drawn beneath a unit.</summary>
        public static Sprite Shadow()
        {
            Sprite themed = ShadowProvider?.Invoke();
            return themed != null ? themed : PlaceholderArt.Ring;
        }

        /// <summary>
        /// Clears the registered providers so a new play session re-registers them.
        /// </summary>
        /// <remarks>
        /// Domain reload is disabled in this project, so without this the second play session
        /// holds delegates closing over textures destroyed at the end of the first, and the board
        /// draws with pink "missing texture" tiles.
        /// </remarks>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetStatics()
        {
            TileProvider = null;
            TokenProvider = null;
            DeployMarkerProvider = null;
            ShadowProvider = null;
        }
    }
}
