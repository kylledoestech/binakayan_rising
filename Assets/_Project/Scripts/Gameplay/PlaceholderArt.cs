using UnityEngine;

namespace BinakayanRising.Gameplay
{
    /// <summary>
    /// Generates the prototype's art at runtime: isometric tile diamonds, unit tokens and a plain
    /// pixel for bars.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every sprite here is procedural and white, so callers tint it with
    /// <see cref="SpriteRenderer.color"/>. That keeps the playable slice completely free of imported
    /// assets: no PNGs, no atlases, no import settings, nothing to break when the project moves
    /// between machines.
    /// </para>
    /// <para>
    /// This is scaffolding. When real art lands, delete this class and assign authored sprites; no
    /// other code needs to change, because everything downstream only ever sees a
    /// <see cref="Sprite"/>.
    /// </para>
    /// </remarks>
    public static class PlaceholderArt
    {
        /// <summary>Texture pixels per world unit. A 128px-wide diamond is therefore one cell wide.</summary>
        public const float PixelsPerUnit = 128f;

        private static Sprite tileSprite;
        private static Sprite tokenSprite;
        private static Sprite ringSprite;
        private static Sprite pixelSprite;
        private static Sprite starSprite;

        /// <summary>A 2:1 isometric diamond with a subtly darker rim, sized to exactly one grid cell.</summary>
        public static Sprite Tile
        {
            get
            {
                if (tileSprite == null)
                {
                    tileSprite = BuildDiamond(128, 64);
                }

                return tileSprite;
            }
        }

        /// <summary>A filled disc used as a unit token.</summary>
        public static Sprite Token
        {
            get
            {
                if (tokenSprite == null)
                {
                    tokenSprite = BuildDisc(72, 0f);
                }

                return tokenSprite;
            }
        }

        /// <summary>A hollow ring used for selection and hover highlights.</summary>
        public static Sprite Ring
        {
            get
            {
                if (ringSprite == null)
                {
                    ringSprite = BuildDisc(88, 0.80f);
                }

                return ringSprite;
            }
        }

        /// <summary>A single white pixel, stretched for health bars and panels.</summary>
        public static Sprite Pixel
        {
            get
            {
                if (pixelSprite == null)
                {
                    Texture2D texture = NewTexture(1, 1);
                    texture.SetPixel(0, 0, Color.white);
                    texture.Apply();
                    pixelSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
                }

                return pixelSprite;
            }
        }

        /// <summary>
        /// A five-pointed star with a dark rim: the Sabotage objective's powder-magazine marker
        /// (#38). Drawn rather than typed, because the baked fonts carry no star glyph.
        /// </summary>
        public static Sprite Star
        {
            get
            {
                if (starSprite == null)
                {
                    starSprite = BuildStar(96);
                }

                return starSprite;
            }
        }

        private static Texture2D NewTexture(int width, int height)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }

        /// <summary>
        /// Rasterises a diamond using the taxicab metric: a point is inside when
        /// <c>|nx| + |ny| &lt;= 1</c> in coordinates normalised to the half-extents. The last few
        /// percent of that range is darkened to give the tile a readable edge, and the very edge is
        /// feathered so the grid does not shimmer when the camera sits on a half-pixel.
        /// </summary>
        private static Sprite BuildDiamond(int width, int height)
        {
            Texture2D texture = NewTexture(width, height);
            Color32[] pixels = new Color32[width * height];

            float halfWidth = width * 0.5f;
            float halfHeight = height * 0.5f;
            float feather = 2f / width;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = ((x + 0.5f) - halfWidth) / halfWidth;
                    float ny = ((y + 0.5f) - halfHeight) / halfHeight;
                    float distance = Mathf.Abs(nx) + Mathf.Abs(ny);

                    float alpha = Mathf.InverseLerp(1f, 1f - feather, distance);
                    float rim = Mathf.InverseLerp(0.86f, 1f, distance);
                    float shade = Mathf.Lerp(1f, 0.68f, rim);

                    pixels[(y * width) + x] = new Color(shade, shade, shade, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            return Sprite.Create(
                texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        }

        /// <summary>
        /// Rasterises a disc. <paramref name="innerRadius"/> above zero carves the middle out to
        /// produce a ring instead.
        /// </summary>
        private static Sprite BuildDisc(int size, float innerRadius)
        {
            Texture2D texture = NewTexture(size, size);
            Color32[] pixels = new Color32[size * size];

            float half = size * 0.5f;
            float feather = 2f / size;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = ((x + 0.5f) - half) / half;
                    float ny = ((y + 0.5f) - half) / half;
                    float radius = Mathf.Sqrt((nx * nx) + (ny * ny));

                    float alpha = Mathf.InverseLerp(1f, 1f - feather, radius);
                    if (innerRadius > 0f)
                    {
                        alpha *= Mathf.InverseLerp(innerRadius - feather, innerRadius, radius);
                    }

                    // A soft top-down gradient keeps a flat disc from reading as a sticker.
                    float shade = innerRadius > 0f ? 1f : Mathf.Lerp(1f, 0.78f, Mathf.Clamp01((-ny + 1f) * 0.5f));
                    pixels[(y * size) + x] = new Color(shade, shade, shade, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            return Sprite.Create(
                texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        }

        /// <summary>
        /// Rasterises an upright five-pointed star: a point is inside when its radius is under the
        /// star's outline at that angle, which alternates linearly between the outer and inner
        /// radius every 36 degrees. The outermost band is darkened into a rim so it reads on sand.
        /// </summary>
        private static Sprite BuildStar(int size)
        {
            Texture2D texture = NewTexture(size, size);
            Color32[] pixels = new Color32[size * size];

            const float outer = 0.96f;
            const float inner = 0.42f;
            float half = size * 0.5f;
            float feather = 2.5f / size;
            float sector = Mathf.PI / 5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = ((x + 0.5f) - half) / half;
                    float ny = ((y + 0.5f) - half) / half;
                    float radius = Mathf.Sqrt((nx * nx) + (ny * ny));

                    // Angular distance from the nearest tip; tips sit every two sectors from straight up.
                    float angle = Mathf.Atan2(nx, ny);
                    float phi = Mathf.Abs(Mathf.Repeat(angle + sector, sector * 2f) - sector);

                    // The outline is the straight edge from a tip (outer, 0) to a notch (inner, sector).
                    float edge = (outer * inner * Mathf.Sin(sector))
                        / ((outer * Mathf.Sin(phi)) + (inner * Mathf.Sin(sector - phi)));
                    float alpha = Mathf.InverseLerp(edge, edge - feather, radius);
                    float rim = Mathf.InverseLerp(edge - 0.16f, edge - 0.08f, radius);
                    float shade = Mathf.Lerp(1f, 0.35f, rim);

                    pixels[(y * size) + x] = new Color(shade, shade, shade, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            return Sprite.Create(
                texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        }

        /// <summary>
        /// Drops the cached sprites so the next play session rebuilds them.
        /// </summary>
        /// <remarks>
        /// The project runs with domain reload disabled, so these statics outlive a play session.
        /// The textures behind them do not: Unity destroys them when play stops, leaving the cached
        /// <see cref="Sprite"/> references pointing at dead textures. The result is UI and tiles
        /// that render correctly the first time Play is pressed and turn invisible the second time.
        /// </remarks>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetStatics()
        {
            tileSprite = null;
            tokenSprite = null;
            ringSprite = null;
            pixelSprite = null;
            starSprite = null;
        }
    }
}
