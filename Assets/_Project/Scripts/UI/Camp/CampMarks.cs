using BinakayanRising.UI.Kit;
using UnityEngine;

namespace BinakayanRising.UI.Camp
{
    /// <summary>
    /// The small pixel-art markers the camp draws over its ground: the objective arrow, footprint
    /// outlines, the walk target and the figures' contact shadows. Drawn in code at the camp's
    /// pixel density, so they sit on the same pixel grid as the rendered buildings.
    /// </summary>
    public static class CampMarks
    {
        /// <summary>A downward arrow with an ink rim, lit from the left. Pivot on its tip.</summary>
        public static Sprite Arrow()
        {
            const int width = 21;
            const int height = 19;
            const int centre = 10;
            var mask = new bool[width, height];

            // Rows counted from the top: a shaft seven wide, then a head narrowing to one pixel.
            for (int row = 0; row < 17; row++)
            {
                int half = row < 7 ? 3 : 9 - (row - 7);
                for (int x = centre - half; x <= centre + half; x++)
                {
                    mask[x, height - 2 - row] = true;
                }
            }

            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (mask[x, y])
                    {
                        pixels[(y * width) + x] = x <= centre ? Theme.Camp.Marker : Theme.Camp.MarkerShade;
                    }
                    else if (Touches(mask, x, y, width, height))
                    {
                        pixels[(y * width) + x] = Theme.Camp.MarkerInk;
                    }
                }
            }

            return Make("Camp Arrow", pixels, width, height, new Vector2((centre + 0.5f) / width, 0f));
        }

        /// <summary>
        /// The outline of a footprint of <paramref name="cellsX"/> by <paramref name="cellsY"/> cells,
        /// with a faint fill. Pivot on the footprint's centre.
        /// </summary>
        public static Sprite Footprint(int cellsX, int cellsY, Color32 rim, byte fillAlpha)
        {
            int width = ((cellsX + cellsY) * 30) + 2;
            int height = ((cellsX + cellsY) * 15) + 2;
            var inside = new bool[width, height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var local = new Vector2((x + 0.5f - (width * 0.5f)) / CampIso.PixelsPerUnit, (y + 0.5f - (height * 0.5f)) / CampIso.PixelsPerUnit);
                    Vector2 grid = CampIso.ToGrid(local);
                    inside[x, y] = Mathf.Abs(grid.x) <= cellsX * 0.5f && Mathf.Abs(grid.y) <= cellsY * 0.5f;
                }
            }

            var fill = new Color32(rim.r, rim.g, rim.b, fillAlpha);
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!inside[x, y])
                    {
                        continue;
                    }

                    bool edge = x == 0 || y == 0 || x == width - 1 || y == height - 1
                        || !inside[x - 1, y] || !inside[x + 1, y] || !inside[x, y - 1] || !inside[x, y + 1];
                    pixels[(y * width) + x] = edge ? rim : fill;
                }
            }

            return Make("Camp Footprint", pixels, width, height, new Vector2(0.5f, 0.5f));
        }

        /// <summary>A soft oval under a figure's feet.</summary>
        public static Sprite Shadow()
        {
            const int width = 16;
            const int height = 6;
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = (x + 0.5f - (width * 0.5f)) / (width * 0.5f);
                    float dy = (y + 0.5f - (height * 0.5f)) / (height * 0.5f);
                    if ((dx * dx) + (dy * dy) <= 1f)
                    {
                        pixels[(y * width) + x] = Theme.Camp.Shadow;
                    }
                }
            }

            return Make("Camp Shadow", pixels, width, height, new Vector2(0.5f, 0.5f));
        }

        private static bool Touches(bool[,] mask, int x, int y, int width, int height)
        {
            return (x > 0 && mask[x - 1, y]) || (x < width - 1 && mask[x + 1, y])
                || (y > 0 && mask[x, y - 1]) || (y < height - 1 && mask[x, y + 1]);
        }

        private static Sprite Make(string name, Color32[] pixels, int width, int height, Vector2 pivot)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), pivot, CampIso.PixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            return sprite;
        }
    }
}
