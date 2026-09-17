using System.Collections.Generic;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Grid;
using BinakayanRising.Gameplay;
using BinakayanRising.UI.Kit;
using UnityEngine;

namespace BinakayanRising.UI.Board
{
    /// <summary>
    /// Paints the isometric board: terrain tiles, unit tokens, deployment markers and shadows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These are drawn in code rather than imported, for the same reason the sun-of-liberty sigil
    /// is: no CC0 pack ships isometric tiles for a Philippine trench line, and recolouring a
    /// European fantasy tileset to stand in for one reads as exactly that. Painting them means the
    /// board shares a palette with the interface by construction.
    /// </para>
    /// <para>
    /// Each tile is a taxicab diamond — inside when <c>|nx| + |ny| &lt;= 1</c> — matching the
    /// geometry <see cref="PlaceholderArt"/> already establishes, so tiles keep lining up with
    /// <c>IsoGridLayout</c> exactly as before. On top of that mask sit three things that turn a
    /// flat colour into ground: a directional bevel, value noise, and per-terrain detail.
    /// </para>
    /// </remarks>
    public static class BoardArtLibrary
    {
        private const int TileWidth = 128;
        private const int TileHeight = 64;
        private const int TokenSize = 128;
        private const int ShadowWidth = 128;
        private const int ShadowHeight = 44;

        /// <summary>Tiles are authored at the scale the board layout already assumes.</summary>
        private const float TilePixelsPerUnit = 128f;

        /// <summary>
        /// Tokens are authored denser than tiles so a unit occupies about two thirds of a cell.
        /// </summary>
        /// <remarks>
        /// At the tile's own density a 128px token would be a full world unit across — as wide as
        /// the cell is long — and a deployed line becomes a row of overlapping discs with the board
        /// invisible behind them.
        /// </remarks>
        private const float TokenPixelsPerUnit = 192f;

        /// <summary>
        /// Shadows are authored wider than the token they sit under.
        /// </summary>
        /// <remarks>
        /// A disc seen almost head-on covers anything its own size, so a shadow matched to the
        /// token is invisible in play — the piece looks pasted onto the tile. Spreading it past the
        /// token's silhouette is what actually plants the unit on the ground.
        /// </remarks>
        private const float ShadowPixelsPerUnit = 142f;

        private static readonly Dictionary<TerrainType, Sprite> tiles = new Dictionary<TerrainType, Sprite>();
        private static readonly Dictionary<Team, Sprite> tokens = new Dictionary<Team, Sprite>();

        private static Sprite deployMarker;
        private static Sprite shadow;

        // ------------------------------------------------------------------ palette

        // Board colours are earth, not parchment: the interface is a document laid over the world,
        // and the world underneath has to look like ground rather than more paper.
        private static readonly Color32 Earth = new Color32(0x7E, 0x82, 0x4C, 0xFF);
        private static readonly Color32 EarthDark = new Color32(0x55, 0x59, 0x33, 0xFF);
        private static readonly Color32 EarthWarm = new Color32(0x93, 0x88, 0x4E, 0xFF);
        private static readonly Color32 TrenchSoil = new Color32(0x6A, 0x4F, 0x30, 0xFF);
        private static readonly Color32 TrenchCut = new Color32(0x2E, 0x21, 0x13, 0xFF);
        private static readonly Color32 Timber = new Color32(0xA8, 0x86, 0x4E, 0xFF);
        private static readonly Color32 TimberDark = new Color32(0x6E, 0x54, 0x2E, 0xFF);
        private static readonly Color32 Water = new Color32(0x25, 0x5C, 0x70, 0xFF);
        private static readonly Color32 WaterLight = new Color32(0x4E, 0x8C, 0x9B, 0xFF);
        private static readonly Color32 Foam = new Color32(0xC7, 0xDD, 0xDF, 0xFF);
        private static readonly Color32 Bamboo = new Color32(0x3B, 0x4A, 0x28, 0xFF);
        private static readonly Color32 BambooStalk = new Color32(0x9A, 0xAC, 0x5C, 0xFF);
        private static readonly Color32 BambooNode = new Color32(0x6B, 0x77, 0x38, 0xFF);
        private static readonly Color32 Canvas = new Color32(0xD3, 0xBE, 0x92, 0xFF);
        private static readonly Color32 CanvasShade = new Color32(0x8A, 0x77, 0x50, 0xFF);
        private static readonly Color32 TentMouth = new Color32(0x3A, 0x2E, 0x1E, 0xFF);

        // ------------------------------------------------------------------ registration

        /// <summary>
        /// Registers this library as the board's art source.
        /// </summary>
        /// <remarks>
        /// Runs before the first scene loads so the providers are in place by the time
        /// <c>BattlePlaytest</c> builds its board, which happens in <c>Awake</c>.
        /// </remarks>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            tiles.Clear();
            tokens.Clear();
            deployMarker = null;
            shadow = null;

            BoardArt.TileProvider = TileFor;
            BoardArt.TokenProvider = TokenFor;
            BoardArt.DeployMarkerProvider = DeployMarkerSprite;
            BoardArt.ShadowProvider = ShadowSprite;
        }

        private static Sprite TileFor(TerrainType terrain)
        {
            if (tiles.TryGetValue(terrain, out Sprite cached) && cached != null)
            {
                return cached;
            }

            Sprite built = BuildTile(terrain);
            tiles[terrain] = built;
            return built;
        }

        private static Sprite TokenFor(Team team)
        {
            if (tokens.TryGetValue(team, out Sprite cached) && cached != null)
            {
                return cached;
            }

            Sprite built = BuildToken(team);
            tokens[team] = built;
            return built;
        }

        private static Sprite DeployMarkerSprite()
        {
            if (deployMarker == null)
            {
                deployMarker = BuildDeployMarker();
            }

            return deployMarker;
        }

        private static Sprite ShadowSprite()
        {
            if (shadow == null)
            {
                shadow = BuildShadow();
            }

            return shadow;
        }

        // ------------------------------------------------------------------ tiles

        private static Sprite BuildTile(TerrainType terrain)
        {
            var pixels = new Color[TileWidth * TileHeight];

            float halfWidth = TileWidth * 0.5f;
            float halfHeight = TileHeight * 0.5f;
            float feather = 2f / TileWidth;

            // Each terrain gets its own noise offset so neighbouring tile types do not share an
            // identical grain pattern, which would read as tiling rather than as ground.
            float noiseOffset = (int)terrain * 37.5f;

            for (int y = 0; y < TileHeight; y++)
            {
                for (int x = 0; x < TileWidth; x++)
                {
                    float nx = ((x + 0.5f) - halfWidth) / halfWidth;
                    float ny = ((y + 0.5f) - halfHeight) / halfHeight;
                    float distance = Mathf.Abs(nx) + Mathf.Abs(ny);

                    float alpha = Mathf.InverseLerp(1f, 1f - feather, distance);
                    if (alpha <= 0f)
                    {
                        pixels[(y * TileWidth) + x] = Color.clear;
                        continue;
                    }

                    Color color = Paint(terrain, nx, ny, x, y, noiseOffset);

                    // Directional bevel: the far edge catches light, the near edge falls into
                    // shadow. Without it every tile is a flat lozenge and the grid reads as a
                    // spreadsheet rather than as ground seen at an angle.
                    float lift = Mathf.Clamp01((ny + 1f) * 0.5f);
                    color = Multiply(color, Mathf.Lerp(0.80f, 1.14f, lift));

                    // Darkened rim, so adjacent tiles stay individually readable.
                    float rim = Mathf.InverseLerp(0.86f, 1f, distance);
                    color = Multiply(color, Mathf.Lerp(1f, 0.62f, rim));

                    color.a = alpha;
                    pixels[(y * TileWidth) + x] = color;
                }
            }

            return Finish(pixels, TileWidth, TileHeight, TilePixelsPerUnit);
        }

        /// <summary>Returns the untinted surface colour for one point inside a tile.</summary>
        private static Color Paint(TerrainType terrain, float nx, float ny, int x, int y, float noiseOffset)
        {
            float grain = Noise(x, y, noiseOffset, 0.16f);

            switch (terrain)
            {
                case TerrainType.Trench:
                {
                    // A cut driven across the tile's width, dark at the bottom and revetted with
                    // timber boards along its near lip.
                    float depth = Mathf.InverseLerp(0.44f, 0.06f, Mathf.Abs(ny));
                    Color soil = Blend(TrenchSoil, EarthDark, grain * 0.4f);
                    Color cut = Blend(soil, TrenchCut, depth * depth);

                    bool onLip = ny < -0.14f && ny > -0.50f;
                    float board = onLip ? Mathf.Repeat(nx * 7f, 1f) : 0f;
                    float plank = board > 0.16f ? 1f : 0f;
                    Color revetted = onLip
                        ? Blend(Blend(TimberDark, Timber, board), cut, 0.15f)
                        : cut;

                    return Shade(onLip ? Blend(TimberDark, revetted, plank) : cut, grain);
                }

                case TerrainType.CoastalShallows:
                {
                    // Banded ripples plus a foam line where the water meets the tile edge.
                    float ripple = (Mathf.Sin((nx * 9f) + (ny * 4f) + (noiseOffset * 0.1f)) * 0.5f) + 0.5f;
                    Color water = Blend(Water, WaterLight, ripple * 0.6f);

                    float distance = Mathf.Abs(nx) + Mathf.Abs(ny);
                    float foam = Mathf.InverseLerp(0.70f, 0.92f, distance)
                                 * Mathf.InverseLerp(1f, 0.90f, distance);

                    return Shade(Blend(water, Foam, foam * 0.75f), grain * 0.5f);
                }

                case TerrainType.BambooBarricade:
                {
                    // Stakes driven upright into the ground. The phase depends on nx alone: add
                    // any ny term and the poles shear into diagonal stripes, which reads as a
                    // hatched pattern rather than as a fence standing on the tile.
                    float across = Mathf.Repeat(nx * 7f, 1f);
                    float stalk = across > 0.30f && across < 0.86f ? 1f : 0f;

                    // Round each stake so it reads as a pole rather than a painted stripe.
                    float round = Mathf.Sin(Mathf.Clamp01((across - 0.30f) / 0.56f) * Mathf.PI);
                    Color pole = Blend(BambooNode, BambooStalk, round);

                    // Two horizontal lashings tying the stakes into one barricade.
                    float lash = Mathf.Abs(Mathf.Abs(ny) - 0.34f) < 0.07f ? 1f : 0f;
                    pole = Blend(pole, BambooNode, lash * 0.8f);

                    Color ground = Blend(Bamboo, EarthDark, grain * 0.5f);
                    return Shade(Blend(ground, pole, Mathf.Max(stalk, lash * 0.9f)), grain * 0.6f);
                }

                case TerrainType.EncampmentTent:
                {
                    // A hipped canvas roof. The tile's four triangular faces are shaded as the
                    // four slopes of a pyramid, which is what makes a flat lozenge read as a
                    // shelter with a peak; a single bright band down the middle does not.
                    float face = nx < 0f
                        ? (ny > 0f ? 1.12f : 0.80f)
                        : (ny > 0f ? 0.98f : 0.68f);

                    Color cloth = Multiply(Canvas, face);

                    // Hips: the four creases running from the peak down to the corners.
                    float hip = Mathf.Min(Mathf.Abs(nx), Mathf.Abs(ny) * 2f);
                    cloth = Blend(CanvasShade, cloth, Mathf.InverseLerp(0.02f, 0.10f, hip));

                    // The doorway, cut into the near slope as a triangle rather than smeared in
                    // as a soft blob.
                    bool nearFace = ny < -0.10f && ny > -0.62f;
                    float door = nearFace && Mathf.Abs(nx) < (0.30f + (ny * 0.36f)) ? 1f : 0f;
                    cloth = Blend(cloth, TentMouth, door);

                    return Shade(cloth, grain * 0.5f);
                }

                default:
                {
                    // Trampled earth: broken up by noise at two scales so no obvious repeat shows
                    // across a board of identical tiles.
                    float coarse = Noise(x, y, noiseOffset + 11f, 0.05f);
                    Color ground = Blend(EarthDark, Earth, 0.30f + (coarse * 0.70f));
                    ground = Blend(ground, EarthWarm, grain * 0.35f);
                    return Shade(ground, grain);
                }
            }
        }

        // ------------------------------------------------------------------ tokens

        private static Sprite BuildToken(Team team)
        {
            bool katipunan = team == Team.Katipunan;
            Color face = katipunan ? Theme.Revolution : Theme.Colonial;
            Color faceDark = katipunan ? Theme.RevolutionDark : new Color32(0x1B, 0x2C, 0x41, 0xFF);
            Color gold = Theme.Gold;
            Color goldBright = Theme.GoldBright;
            Color parchment = Theme.Parchment;

            var pixels = new Color[TokenSize * TokenSize];
            float half = TokenSize * 0.5f;

            for (int y = 0; y < TokenSize; y++)
            {
                for (int x = 0; x < TokenSize; x++)
                {
                    float nx = ((x + 0.5f) - half) / half;
                    float ny = ((y + 0.5f) - half) / half;
                    float radius = Mathf.Sqrt((nx * nx) + (ny * ny));

                    if (radius > 1f)
                    {
                        pixels[(y * TokenSize) + x] = Color.clear;
                        continue;
                    }

                    float lift = Mathf.Clamp01((ny + 1f) * 0.5f);
                    Color color;

                    if (radius > 0.93f)
                    {
                        color = faceDark;                                       // outer edge
                    }
                    else if (radius > 0.78f)
                    {
                        // Rank ring, lit from above so the rim has a metal roll to it.
                        color = Blend(gold, goldBright, lift);
                    }
                    else if (radius > 0.68f)
                    {
                        color = faceDark;
                    }
                    else
                    {
                        // Team-colour face. Lit from above so the disc reads as a standing piece
                        // rather than as a flat circle painted on the ground.
                        color = Blend(face, parchment, lift * 0.20f);
                        color = Multiply(color, Mathf.Lerp(0.78f, 1.10f, lift));
                        color = Blend(color, Motif(katipunan, nx, ny, radius, gold, parchment),
                            MotifStrength(katipunan, nx, ny, radius));
                    }

                    // Feather the silhouette so the token does not crawl with aliasing as it moves.
                    color.a = Mathf.InverseLerp(1f, 0.97f, radius);
                    pixels[(y * TokenSize) + x] = color;
                }
            }

            return Finish(pixels, TokenSize, TokenSize, TokenPixelsPerUnit);
        }

        /// <summary>
        /// How strongly the team's device shows at a point on the token face.
        /// </summary>
        /// <remarks>
        /// A plain coloured disc with a ring on it is a board-game counter, not a soldier. The
        /// Katipunan carry the eight-rayed sun of the flag; the colonial force carry the saltire of
        /// the Cross of Burgundy, which is what actually flew over Spanish colonial units.
        /// </remarks>
        private static float MotifStrength(bool katipunan, float nx, float ny, float radius)
        {
            // Everything lives in an annulus just inside the rank ring. The middle of the face is
            // left clear on purpose: the battle labels every token with the unit's three-letter
            // code, and a device drawn through that turns the name into noise.
            if (radius < 0.40f || radius > 0.66f)
            {
                return 0f;
            }

            float angle = Mathf.Atan2(ny, nx);

            if (katipunan)
            {
                // The eight rays of the sun of liberty, tapering outward.
                float ray = Mathf.Abs(Mathf.Cos(angle * 4f));
                float taper = Mathf.InverseLerp(0.66f, 0.40f, radius);
                return ray > Mathf.Lerp(0.90f, 0.55f, taper) ? 1f : 0f;
            }

            // Four spokes on the diagonals — the arms of the Cross of Burgundy, which is what
            // actually flew over Spanish colonial units.
            float spoke = Mathf.Abs(Mathf.Cos((angle * 2f) - (Mathf.PI * 0.5f)));
            return spoke > 0.86f ? 1f : 0f;
        }

        private static Color Motif(bool katipunan, float nx, float ny, float radius, Color gold, Color parchment)
        {
            return katipunan ? Blend(gold, Theme.GoldBright, Mathf.Clamp01(1f - (radius * 1.6f))) : parchment;
        }

        // ------------------------------------------------------------------ markers

        /// <summary>A dashed gold diamond outline marking a cell a unit may be placed on.</summary>
        private static Sprite BuildDeployMarker()
        {
            var pixels = new Color[TileWidth * TileHeight];

            float halfWidth = TileWidth * 0.5f;
            float halfHeight = TileHeight * 0.5f;
            Color gold = Theme.GoldBright;

            for (int y = 0; y < TileHeight; y++)
            {
                for (int x = 0; x < TileWidth; x++)
                {
                    float nx = ((x + 0.5f) - halfWidth) / halfWidth;
                    float ny = ((y + 0.5f) - halfHeight) / halfHeight;
                    float distance = Mathf.Abs(nx) + Mathf.Abs(ny);

                    if (distance > 1f)
                    {
                        pixels[(y * TileWidth) + x] = Color.clear;
                        continue;
                    }

                    // A band just inside the tile edge, broken into dashes along it. The dash
                    // phase runs on nx - ny on the two upper faces and nx + ny on the lower two,
                    // so the marks stay square to whichever edge they sit on.
                    float band = Mathf.InverseLerp(0.74f, 0.86f, distance)
                                 * Mathf.InverseLerp(1f, 0.94f, distance);

                    float along = ny > 0f ? nx - ny : nx + ny;
                    float dash = Mathf.Repeat(along * 4f, 1f) < 0.62f ? 1f : 0f;

                    // A wash inside the outline, so a deployable cell reads as a region and not
                    // only as an edge — it has to survive being drawn over pale trench and tent
                    // tiles, which is where this scenario's deployment zone happens to sit.
                    float wash = distance < 0.80f ? 0.13f : 0f;

                    Color color = gold;
                    color.a = Mathf.Clamp01((band * dash) + wash);
                    pixels[(y * TileWidth) + x] = color;
                }
            }

            return Finish(pixels, TileWidth, TileHeight, TilePixelsPerUnit);
        }

        /// <summary>A soft elliptical shadow, sat under a unit to plant it on the ground.</summary>
        private static Sprite BuildShadow()
        {
            var pixels = new Color[ShadowWidth * ShadowHeight];

            float halfWidth = ShadowWidth * 0.5f;
            float halfHeight = ShadowHeight * 0.5f;

            for (int y = 0; y < ShadowHeight; y++)
            {
                for (int x = 0; x < ShadowWidth; x++)
                {
                    float nx = ((x + 0.5f) - halfWidth) / halfWidth;
                    float ny = ((y + 0.5f) - halfHeight) / halfHeight;
                    float radius = Mathf.Sqrt((nx * nx) + (ny * ny));

                    float alpha = Mathf.InverseLerp(1f, 0.15f, radius);
                    pixels[(y * ShadowWidth) + x] = new Color(0f, 0f, 0f, alpha * alpha * 0.55f);
                }
            }

            return Finish(pixels, ShadowWidth, ShadowHeight, ShadowPixelsPerUnit);
        }

        // ------------------------------------------------------------------ helpers

        private static Sprite Finish(Color[] pixels, int width, int height, float pixelsPerUnit)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            texture.SetPixels(pixels);
            texture.Apply();

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        /// <summary>Value noise in 0..1 at a given frequency.</summary>
        private static float Noise(int x, int y, float offset, float frequency)
        {
            return Mathf.PerlinNoise((x * frequency) + offset, (y * frequency) + offset);
        }

        /// <summary>Applies a noise sample as a gentle light/dark variation.</summary>
        private static Color Shade(Color color, float grain)
        {
            return Multiply(color, Mathf.Lerp(0.88f, 1.12f, grain));
        }

        private static Color Multiply(Color color, float factor)
        {
            return new Color(color.r * factor, color.g * factor, color.b * factor, color.a);
        }

        private static Color Blend(Color a, Color b, float t)
        {
            return Color.Lerp(a, b, Mathf.Clamp01(t));
        }
    }
}
