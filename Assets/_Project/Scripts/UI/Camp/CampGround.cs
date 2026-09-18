using System.Collections.Generic;
using BinakayanRising.Core.Content;
using BinakayanRising.Core.Grid;
using BinakayanRising.UI.Kit;
using UnityEngine;

namespace BinakayanRising.UI.Camp
{
    /// <summary>
    /// Paints the encampment's ground as one pixel-art texture, from the layout itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The trodden paths are the walks the pathfinder would take from where the player appears to
    /// every door and every keeper, so they cannot drift away from the buildings they lead to: move
    /// a door in <see cref="Encampment"/> and the path follows it on the next run.
    /// </para>
    /// <para>
    /// Every pixel is projected back onto the grid to decide what it is: clearing inside the
    /// camp's twelve cells, jungle floor outside, path along the walks. The edges between them are
    /// pushed around by value noise so nothing reads as a row of diamonds.
    /// </para>
    /// </remarks>
    public static class CampGround
    {
        /// <summary>Samples per cell in the precomputed distance-to-path field.</summary>
        private const int FieldResolution = 8;

        /// <summary>The field covers the grid plus this many cells on each side.</summary>
        private const int FieldMargin = 2;

        private const float PathHalfWidth = 0.34f;
        private const float PlazaRadius = 1.7f;

        /// <summary>Paints the ground over <paramref name="world"/>, which must sit on whole pixels.</summary>
        public static Sprite Paint(Rect world)
        {
            int width = Mathf.RoundToInt(world.width * CampIso.PixelsPerUnit);
            int height = Mathf.RoundToInt(world.height * CampIso.PixelsPerUnit);
            float[] field = PathField(out int fieldSize);

            var pixels = new Color32[width * height];
            for (int py = 0; py < height; py++)
            {
                for (int px = 0; px < width; px++)
                {
                    var point = new Vector2(world.xMin + ((px + 0.5f) / CampIso.PixelsPerUnit), world.yMin + ((py + 0.5f) / CampIso.PixelsPerUnit));
                    Vector2 grid = CampIso.ToGrid(point);
                    pixels[(py * width) + px] = Shade(px, py, grid, SampleField(field, fieldSize, grid));
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Camp Ground",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), Vector2.zero, CampIso.PixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        private static Color32 Shade(int px, int py, Vector2 grid, float pathDistance)
        {
            // Outside the camp's square, by how far, with a ragged edge.
            float outside = Mathf.Max(Mathf.Max(-0.5f - grid.x, grid.x - (Encampment.Width - 0.5f)),
                                      Mathf.Max(-0.5f - grid.y, grid.y - (Encampment.Height - 0.5f)));
            float edgeNoise = (ValueNoise(grid.x * 1.3f, grid.y * 1.3f, 11) - 0.5f) * 0.9f;
            bool jungle = outside > edgeNoise;

            float grain = Hash(px >> 1, py >> 1, 3);
            float patch = ValueNoise(px / 9f, py / 9f, 7);
            float tone = (patch * 0.65f) + (grain * 0.35f);

            float pathEdge = PathHalfWidth + ((ValueNoise(grid.x * 2.2f, grid.y * 2.2f, 5) - 0.5f) * 0.18f);
            if (!jungle && pathDistance < pathEdge)
            {
                if (pathDistance > pathEdge - 0.05f)
                {
                    return Theme.Camp.PathDark;
                }

                // Pebbles: single dark pixels, sparse.
                if (Hash(px, py, 17) < 0.012f)
                {
                    return Theme.Camp.PathDark;
                }

                return tone < 0.33f ? Theme.Camp.PathDark : tone > 0.68f ? Theme.Camp.PathLight : Theme.Camp.Path;
            }

            bool tuft = Tuft(px, py);
            if (jungle)
            {
                if (tuft)
                {
                    return Theme.Camp.JungleLight;
                }

                return tone < 0.36f ? Theme.Camp.JungleDark : tone > 0.7f ? Theme.Camp.JungleLight : Theme.Camp.Jungle;
            }

            if (tuft)
            {
                return Theme.Camp.ClearingDark;
            }

            return tone < 0.3f ? Theme.Camp.ClearingDark : tone > 0.7f ? Theme.Camp.ClearingLight : Theme.Camp.Clearing;
        }

        /// <summary>A few three-pixel grass tufts: a stem and two blades, seeded from a sparse hash.</summary>
        private static bool Tuft(int px, int py)
        {
            if (Hash(px, py, 23) < 0.006f)
            {
                return true;
            }

            // The stem's two blades, one pixel up and to either side of a seeded stem.
            return Hash(px - 1, py - 1, 23) < 0.006f || Hash(px + 1, py - 1, 23) < 0.006f;
        }

        // ------------------------------------------------------------------ paths

        /// <summary>
        /// Distance from each sample point to the nearest walk, in cells. Computed once on a
        /// coarse grid so the per-pixel pass only has to interpolate.
        /// </summary>
        private static float[] PathField(out int size)
        {
            var walks = new List<List<GridCoord>>();
            GridCoord spawn = Encampment.Spawn;
            foreach (CampSite site in Encampment.Sites)
            {
                AddWalk(walks, spawn, site.Door);
            }

            foreach (CampFigure figure in Encampment.Figures)
            {
                AddWalk(walks, spawn, figure.Talk);
            }

            int span = Mathf.Max(Encampment.Width, Encampment.Height) + (FieldMargin * 2);
            size = (span * FieldResolution) + 1;
            var field = new float[size * size];

            // The plaza round the aide, between the flag and where the player appears.
            GridCoord aide = Encampment.Figure(Characters.Tomas).Cell;
            var plaza = new Vector2(aide.X, aide.Y);

            for (int j = 0; j < size; j++)
            {
                for (int i = 0; i < size; i++)
                {
                    var point = new Vector2((i / (float)FieldResolution) - FieldMargin, (j / (float)FieldResolution) - FieldMargin);
                    float best = Vector2.Distance(point, plaza) - PlazaRadius + PathHalfWidth;
                    for (int w = 0; w < walks.Count; w++)
                    {
                        List<GridCoord> walk = walks[w];
                        for (int s = 1; s < walk.Count; s++)
                        {
                            float distance = SegmentDistance(point, walk[s - 1], walk[s]);
                            if (distance < best)
                            {
                                best = distance;
                            }
                        }
                    }

                    field[(j * size) + i] = best;
                }
            }

            return field;
        }

        private static void AddWalk(List<List<GridCoord>> walks, GridCoord from, GridCoord to)
        {
            List<GridCoord> walk = Encampment.Path(from, to);
            if (walk != null && walk.Count > 1)
            {
                walks.Add(walk);
            }
        }

        private static float SampleField(float[] field, int size, Vector2 grid)
        {
            float fx = (grid.x + FieldMargin) * FieldResolution;
            float fy = (grid.y + FieldMargin) * FieldResolution;
            if (fx < 0f || fy < 0f || fx >= size - 1 || fy >= size - 1)
            {
                return float.MaxValue;
            }

            int x0 = (int)fx;
            int y0 = (int)fy;
            float tx = fx - x0;
            float ty = fy - y0;
            float a = field[(y0 * size) + x0];
            float b = field[(y0 * size) + x0 + 1];
            float c = field[((y0 + 1) * size) + x0];
            float d = field[((y0 + 1) * size) + x0 + 1];
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        private static float SegmentDistance(Vector2 point, GridCoord from, GridCoord to)
        {
            var a = new Vector2(from.X, from.Y);
            Vector2 ab = new Vector2(to.X, to.Y) - a;
            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(point, a + (ab * t));
        }

        // ------------------------------------------------------------------ noise

        /// <summary>A repeatable 0..1 value per integer point.</summary>
        public static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)((x * 374761393) + (y * 668265263) + (seed * 144665));
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        /// <summary>Smoothly interpolated <see cref="Hash"/>: soft patches instead of speckle.</summary>
        public static float ValueNoise(float x, float y, int seed)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float tx = x - x0;
            float ty = y - y0;
            tx = tx * tx * (3f - (2f * tx));
            ty = ty * ty * (3f - (2f * ty));
            float a = Hash(x0, y0, seed);
            float b = Hash(x0 + 1, y0, seed);
            float c = Hash(x0, y0 + 1, seed);
            float d = Hash(x0 + 1, y0 + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }
    }
}
