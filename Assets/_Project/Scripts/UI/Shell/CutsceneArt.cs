using System.Collections.Generic;
using BinakayanRising.Core.Content;
using UnityEngine;

namespace BinakayanRising.UI.Shell
{
    /// <summary>
    /// Paints a cutscene slide's picture when no illustration was drawn for it: a small
    /// pixel-art landscape for the slide's <see cref="SceneMood"/>, scaled up with point filtering
    /// so it sits with the game's sprites.
    /// </summary>
    /// <remarks>
    /// An illustration under <c>Resources/Cutscenes/</c> named for the slide always wins, so the
    /// team can replace these one at a time without touching code.
    /// </remarks>
    public static class CutsceneArt
    {
        public const int Width = 320;
        public const int Height = 140;

        private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            cache.Clear();
        }

        /// <summary>The slide's illustration, or its painted landscape.</summary>
        public static Texture2D For(CutsceneSlide slide)
        {
            if (slide == null)
            {
                return null;
            }

            Texture2D drawn = string.IsNullOrEmpty(slide.Image) ? null : Resources.Load<Texture2D>("Cutscenes/" + slide.Image);
            if (drawn != null)
            {
                return drawn;
            }

            string key = slide.Mood + "/" + slide.Image;
            Texture2D painted;
            if (!cache.TryGetValue(key, out painted) || painted == null)
            {
                painted = Paint(slide.Mood, (slide.Image ?? string.Empty).GetHashCode());
                cache[key] = painted;
            }

            return painted;
        }

        /// <summary>Paints <paramref name="mood"/>; <paramref name="seed"/> varies the details.</summary>
        public static Texture2D Paint(SceneMood mood, int seed)
        {
            var pixels = new Color32[Width * Height];
            var random = new System.Random(seed);
            Palette p = PaletteFor(mood);

            int horizon = mood == SceneMood.City ? 46 : 52;

            // Sky, banded rather than smooth: pixel art steps its gradients.
            for (int y = horizon; y < Height; y++)
            {
                float t = (float)(y - horizon) / (Height - horizon);
                int band = Mathf.FloorToInt(t * 6f);
                Color32 sky = Color32.Lerp(p.SkyLow, p.SkyHigh, band / 5f);
                for (int x = 0; x < Width; x++)
                {
                    pixels[(y * Width) + x] = sky;
                }
            }

            if (mood == SceneMood.Night)
            {
                for (int i = 0; i < 60; i++)
                {
                    Set(pixels, random.Next(Width), horizon + 20 + random.Next(Height - horizon - 20), new Color32(0xE9, 0xE2, 0xD0, 0xFF));
                }
            }

            // Sun or moon.
            int sunX = 60 + random.Next(200);
            int sunY = mood == SceneMood.Dawn ? horizon + 12 : horizon + 50 + random.Next(20);
            Disc(pixels, sunX, sunY, mood == SceneMood.Night ? 7 : 11, p.Sun);

            // Far hills: a sum of slow waves.
            float phase = random.Next(1000) * 0.01f;
            for (int x = 0; x < Width; x++)
            {
                int top = horizon + 6 + Mathf.RoundToInt((5f * Mathf.Sin((x * 0.03f) + phase)) + (3f * Mathf.Sin((x * 0.071f) + (phase * 2f))));
                for (int y = horizon; y < top; y++)
                {
                    Set(pixels, x, y, p.Hills);
                }
            }

            // Ground.
            for (int y = 0; y < horizon; y++)
            {
                int band = (horizon - y) / 12;
                Color32 ground = band % 2 == 0 ? p.Ground : p.GroundDark;
                for (int x = 0; x < Width; x++)
                {
                    pixels[(y * Width) + x] = ground;
                }
            }

            switch (mood)
            {
                case SceneMood.City:
                    City(pixels, random, horizon, p);
                    break;
                case SceneMood.Camp:
                    Palms(pixels, random, horizon, p, 3);
                    Tents(pixels, random, horizon, p);
                    break;
                case SceneMood.Sea:
                    Sea(pixels, random, horizon, p);
                    break;
                case SceneMood.Trench:
                    Palms(pixels, random, horizon, p, 2);
                    Trench(pixels, horizon, p);
                    break;
                case SceneMood.Battle:
                    Trench(pixels, horizon, p);
                    Smoke(pixels, random, horizon, p);
                    break;
                case SceneMood.Dawn:
                    RiceRows(pixels, horizon, p);
                    Palms(pixels, random, horizon, p, 4);
                    break;
                case SceneMood.Night:
                    Tents(pixels, random, horizon, p);
                    Disc(pixels, 160, horizon - 20, 3, new Color32(0xF0, 0xD2, 0x64, 0xFF));
                    break;
            }

            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        // ------------------------------------------------------------------ features

        private static void City(Color32[] pixels, System.Random random, int horizon, Palette p)
        {
            int x = 0;
            while (x < Width)
            {
                int w = 12 + random.Next(18);
                int h = 18 + random.Next(34);
                Rect(pixels, x, horizon - 2, w, h, p.Shape);
                // A pitched roof, and now and then a church spire.
                for (int i = 0; i < w / 2; i++)
                {
                    Rect(pixels, x + i, horizon - 2 + h + i, w - (2 * i), 1, p.Shape);
                }

                if (random.Next(4) == 0)
                {
                    Rect(pixels, x + (w / 2) - 1, horizon + h + (w / 2), 3, 18, p.Shape);
                }

                // Lit windows.
                for (int wy = horizon + 4; wy < horizon + h - 4; wy += 6)
                {
                    for (int wx = x + 3; wx < x + w - 3; wx += 5)
                    {
                        if (random.Next(3) == 0)
                        {
                            Rect(pixels, wx, wy, 2, 3, p.Accent);
                        }
                    }
                }

                x += w + random.Next(4);
            }
        }

        private static void Tents(Color32[] pixels, System.Random random, int horizon, Palette p)
        {
            for (int n = 0; n < 4; n++)
            {
                int cx = 30 + (n * 75) + random.Next(20);
                int baseY = horizon - 18 - random.Next(10);
                int half = 16 + random.Next(6);
                for (int row = 0; row < half; row++)
                {
                    Rect(pixels, cx - half + row, baseY + row, 2 * (half - row), 1, row % 5 == 0 ? p.ShapeLight : p.Shape);
                }

                Rect(pixels, cx - 2, baseY, 4, half / 2, p.GroundDark);
            }
        }

        private static void Palms(Color32[] pixels, System.Random random, int horizon, Palette p, int count)
        {
            for (int n = 0; n < count; n++)
            {
                int x = random.Next(Width);
                int baseY = horizon - 4 - random.Next(20);
                int height = 34 + random.Next(20);
                int lean = random.Next(2) == 0 ? -1 : 1;
                int topX = x;
                for (int y = 0; y < height; y++)
                {
                    topX = x + (lean * (y * y) / (height * 4));
                    Rect(pixels, topX, baseY + y, 2, 1, p.Trunk);
                }

                int top = baseY + height;
                for (int f = 0; f < 6; f++)
                {
                    float angle = (f / 6f) * Mathf.PI * 2f;
                    for (int r = 0; r < 12; r++)
                    {
                        int fx = topX + Mathf.RoundToInt(Mathf.Cos(angle) * r);
                        int fy = top + Mathf.RoundToInt((Mathf.Sin(angle) * r * 0.5f) - (r * r * 0.04f));
                        Rect(pixels, fx, fy, 2, 1, p.Leaf);
                    }
                }
            }
        }

        private static void Sea(Color32[] pixels, System.Random random, int horizon, Palette p)
        {
            // The whole ground band is water, with a strip of beach at the bottom.
            for (int y = 10; y < horizon; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    bool crest = ((x + (y * 7)) % 23) < 3 && y % 4 == 0;
                    Set(pixels, x, y, crest ? p.ShapeLight : (y % 8 < 4 ? p.Water : p.WaterDark));
                }
            }

            Rect(pixels, 0, 0, Width, 10, p.Ground);

            // A warship on the horizon: hull, masts, smoke.
            int shipX = 40 + random.Next(200);
            int shipY = horizon - 8;
            Rect(pixels, shipX, shipY, 44, 5, p.Shape);
            Rect(pixels, shipX + 4, shipY + 5, 34, 4, p.Shape);
            Rect(pixels, shipX + 12, shipY + 9, 2, 16, p.Shape);
            Rect(pixels, shipX + 26, shipY + 9, 2, 13, p.Shape);
            Rect(pixels, shipX + 18, shipY + 9, 4, 7, p.Shape);
            for (int i = 0; i < 5; i++)
            {
                Disc(pixels, shipX + 20 + (i * 5), shipY + 18 + (i * 3), 2 + (i / 2), p.Smoke);
            }
        }

        private static void Trench(Color32[] pixels, int horizon, Palette p)
        {
            // A long earthwork across the foreground, with sharpened stakes along its lip.
            int lip = horizon - 26;
            for (int x = 0; x < Width; x++)
            {
                int top = lip + Mathf.RoundToInt(2f * Mathf.Sin(x * 0.2f));
                for (int y = lip - 14; y < top; y++)
                {
                    Set(pixels, x, y, y > top - 3 ? p.ShapeLight : p.Earth);
                }

                if (x % 9 == 0)
                {
                    for (int s = 0; s < 7; s++)
                    {
                        Set(pixels, x + (s / 3), top + s, p.Trunk);
                    }
                }
            }
        }

        private static void Smoke(Color32[] pixels, System.Random random, int horizon, Palette p)
        {
            for (int i = 0; i < 26; i++)
            {
                int x = random.Next(Width);
                int y = horizon - 10 + random.Next(40);
                Disc(pixels, x, y, 5 + random.Next(9), i % 3 == 0 ? p.Accent : p.Smoke);
            }
        }

        private static void RiceRows(Color32[] pixels, int horizon, Palette p)
        {
            for (int y = 0; y < horizon - 4; y++)
            {
                if ((horizon - y) % 5 != 0)
                {
                    continue;
                }

                for (int x = 0; x < Width; x++)
                {
                    if ((x + y) % 4 < 2)
                    {
                        Set(pixels, x, y, p.Leaf);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ pixels

        private static void Set(Color32[] pixels, int x, int y, Color32 color)
        {
            if (x >= 0 && x < Width && y >= 0 && y < Height)
            {
                pixels[(y * Width) + x] = color;
            }
        }

        private static void Rect(Color32[] pixels, int x, int y, int w, int h, Color32 color)
        {
            for (int yy = y; yy < y + h; yy++)
            {
                for (int xx = x; xx < x + w; xx++)
                {
                    Set(pixels, xx, yy, color);
                }
            }
        }

        private static void Disc(Color32[] pixels, int cx, int cy, int r, Color32 color)
        {
            for (int y = -r; y <= r; y++)
            {
                for (int x = -r; x <= r; x++)
                {
                    if ((x * x) + (y * y) <= r * r)
                    {
                        Set(pixels, cx + x, cy + y, color);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ palettes

        private struct Palette
        {
            public Color32 SkyLow, SkyHigh, Sun, Hills, Ground, GroundDark, Earth;
            public Color32 Shape, ShapeLight, Accent, Trunk, Leaf, Water, WaterDark, Smoke;
        }

        private static Color32 Hex(uint rgb)
        {
            return new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 0xFF);
        }

        private static Palette PaletteFor(SceneMood mood)
        {
            var p = new Palette
            {
                SkyLow = Hex(0xE8C98A), SkyHigh = Hex(0x8FB3C9), Sun = Hex(0xF0D264),
                Hills = Hex(0x5C7A4A), Ground = Hex(0x788A44), GroundDark = Hex(0x6A7C3C), Earth = Hex(0x8E764E),
                Shape = Hex(0x3A2E2A), ShapeLight = Hex(0xE9E2D0), Accent = Hex(0xF0D264),
                Trunk = Hex(0x5B3A22), Leaf = Hex(0x405C2A), Water = Hex(0x3E6A8A), WaterDark = Hex(0x2E4A6B),
                Smoke = Hex(0x6E6862)
            };

            switch (mood)
            {
                case SceneMood.City:
                    p.SkyLow = Hex(0xB8B2A8); p.SkyHigh = Hex(0x6E7A86); p.Sun = Hex(0xD6D0C4);
                    p.Hills = Hex(0x55606A); p.Ground = Hex(0x4A4640); p.GroundDark = Hex(0x3E3A34);
                    p.Shape = Hex(0x2B2B30);
                    break;
                case SceneMood.Sea:
                    p.SkyLow = Hex(0xD9C9A0); p.SkyHigh = Hex(0x7FA6C4); p.Ground = Hex(0xC8B488);
                    p.Hills = Hex(0x6A8458);
                    break;
                case SceneMood.Trench:
                    p.SkyLow = Hex(0xE0A060); p.SkyHigh = Hex(0x6A5A7A); p.Sun = Hex(0xF08A40);
                    p.Hills = Hex(0x3E4A36); p.Ground = Hex(0x5A5A34); p.GroundDark = Hex(0x4E4E2E);
                    break;
                case SceneMood.Battle:
                    p.SkyLow = Hex(0xC06A3A); p.SkyHigh = Hex(0x4A3A3A); p.Sun = Hex(0xE0503A);
                    p.Hills = Hex(0x3A3230); p.Ground = Hex(0x4E4A34); p.GroundDark = Hex(0x423E2C);
                    p.Accent = Hex(0xF08A40); p.Smoke = Hex(0x5A5450);
                    break;
                case SceneMood.Dawn:
                    p.SkyLow = Hex(0xF0B878); p.SkyHigh = Hex(0x9AB0C8); p.Sun = Hex(0xF8D878);
                    p.Ground = Hex(0x86964C); p.GroundDark = Hex(0x788A44); p.Leaf = Hex(0x4C6A30);
                    break;
                case SceneMood.Night:
                    p.SkyLow = Hex(0x2E3A5A); p.SkyHigh = Hex(0x141A2E); p.Sun = Hex(0xE9E2D0);
                    p.Hills = Hex(0x1E2636); p.Ground = Hex(0x2A2E26); p.GroundDark = Hex(0x24281F);
                    p.Shape = Hex(0x4A4034); p.ShapeLight = Hex(0x6A5A44); p.Trunk = Hex(0x2A1E14);
                    break;
            }

            return p;
        }
    }
}
