using BinakayanRising.Core.Grid;
using BinakayanRising.Gameplay.Adapters;
using UnityEngine;

namespace BinakayanRising.Gameplay.Presentation
{
    /// <summary>
    /// Projects simulation cells to Unity world positions and back.
    /// </summary>
    public interface IBattleWorldSpace
    {
        /// <summary>The world-space centre of a cell.</summary>
        /// <param name="cell">Simulation cell.</param>
        Vector3 CellToWorld(GridCoord cell);

        /// <summary>The cell containing a world-space point.</summary>
        /// <param name="world">World-space point.</param>
        GridCoord WorldToCell(Vector3 world);
    }

    /// <summary>
    /// Cell-to-world projection backed by Core's <see cref="IsoGridLayout"/>, for scenes with no
    /// Tilemap.
    /// </summary>
    public sealed class IsoLayoutWorldSpace : IBattleWorldSpace
    {
        private readonly IsoGridLayout layout;
        private readonly Vector3 origin;

        /// <summary>Creates a projection.</summary>
        /// <param name="layout">Layout to project with. Null falls back to <see cref="IsoGridLayout.Default"/>.</param>
        /// <param name="origin">World position of cell <c>(0, 0)</c>.</param>
        public IsoLayoutWorldSpace(IsoGridLayout layout = null, Vector3 origin = default)
        {
            this.layout = layout ?? IsoGridLayout.Default;
            this.origin = origin;
        }

        /// <inheritdoc />
        public Vector3 CellToWorld(GridCoord cell)
        {
            return origin + layout.CellToWorldPosition(cell);
        }

        /// <inheritdoc />
        public GridCoord WorldToCell(Vector3 world)
        {
            return layout.WorldPositionToCell(world - origin);
        }
    }
}
