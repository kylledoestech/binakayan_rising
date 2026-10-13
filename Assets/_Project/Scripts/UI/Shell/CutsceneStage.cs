using System;
using System.Collections.Generic;
using BinakayanRising.Core.Content;
using BinakayanRising.UI.Kit;
using UnityEngine;
using UnityEngine.UI;

namespace BinakayanRising.UI.Shell
{
    /// <summary>
    /// The moving half of an animated cutscene slide: clouds drifting, a ship under way, smoke
    /// rising, lamplight flickering, the slide's people walking in, and a slow push-in on the whole
    /// picture (panel feedback: "cut scenes - 1st mission, animation").
    /// </summary>
    /// <remarks>
    /// <para>
    /// Lives on the picture itself, so its movers scale and fade with the painting. Everything is
    /// placed in the painting's own pixels (<see cref="CutsceneArt.Width"/> by
    /// <see cref="CutsceneArt.Height"/>) and snapped to them, so the movers stay as crisp as the
    /// landscape they cross.
    /// </para>
    /// <para>
    /// Every position is a function of the time since the slide opened, not a sum of per-frame
    /// steps, so a frame rate dip never puts a cloud somewhere else and a screenshot taken at a
    /// given second always shows the same picture. Nothing here takes a click.
    /// </para>
    /// </remarks>
    public sealed class CutsceneStage : MonoBehaviour
    {
        /// <summary>How far the picture pushes in over <see cref="PushSeconds"/>.</summary>
        private const float PushIn = 0.06f;
        private const float PushSeconds = 14f;

        /// <summary>When the cast starts in, and how long they take to reach their marks.</summary>
        private const float WalkDelay = 0.4f;
        private const float WalkSeconds = 1.6f;
        private const float WalkDistance = 40f;

        /// <summary>Where the cast stand, in painting pixels: one at the left third, two at either third.</summary>
        private static readonly float[] Marks = { 96f, 224f };

        /// <summary>
        /// Where the cast's feet rest: behind the narration box, which hides the painting's bottom
        /// thirty rows on a 16:9 screen. They stand nearer the viewer than anything painted, so
        /// they are cut at the shins as in a stage play's front row, rather than stood on ground
        /// a painted tent's height away and seeming to float over it.
        /// </summary>
        private const float FeetY = 20f;

        /// <summary>The camp's cook fire, between where the cast stand.</summary>
        private static readonly Vector2 Campfire = new Vector2(160f, 40f);

        private sealed class Actor
        {
            public RectTransform Rect;
            public Graphic Graphic;

            /// <summary>Size in painting pixels.</summary>
            public Vector2 Size;

            /// <summary>Where the actor's pivot is at a time, in painting pixels.</summary>
            public Func<float, Vector2> Path;

            /// <summary>Its opacity at a time; null for always opaque.</summary>
            public Func<float, float> Alpha;
        }

        private static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

        private readonly List<Actor> actors = new List<Actor>();
        private RectTransform frame;
        private float startedAt;
        private bool playing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            textures.Clear();
        }

        /// <summary>Seconds since the slide opened.</summary>
        public float Elapsed
        {
            get { return Time.unscaledTime - startedAt; }
        }

        /// <summary>How many things are moving on this slide. For tests.</summary>
        public int ActorCount
        {
            get { return actors.Count; }
        }

        /// <summary>Clears the stage and sets <paramref name="slide"/>'s movers on it.</summary>
        public void Play(CutsceneSlide slide)
        {
            Clear();
            frame = (RectTransform)transform;
            startedAt = Time.unscaledTime;
            playing = slide != null;
            if (slide == null)
            {
                return;
            }

            int horizon = CutsceneArt.HorizonOf(slide.Mood);
            var random = new System.Random((slide.Image ?? string.Empty).GetHashCode());
            switch (slide.Mood)
            {
                case SceneMood.City:
                    Clouds(random, horizon, 4, new Color32(0x9A, 0x98, 0x94, 0xFF), 2.5f);
                    break;
                case SceneMood.Camp:
                    Clouds(random, horizon, 3, new Color32(0xF4, 0xEE, 0xE0, 0xFF), 3f);
                    Fire(Campfire);
                    Smoke(Campfire + new Vector2(0f, 4f), 6, 0.9f, new Color32(0x8A, 0x84, 0x7C, 0xFF));
                    break;
                case SceneMood.Sea:
                    Clouds(random, horizon, 3, new Color32(0xF4, 0xEE, 0xE0, 0xFF), 4f);
                    Ship(horizon);
                    break;
                case SceneMood.Dawn:
                    Clouds(random, horizon, 3, new Color32(0xF8, 0xE0, 0xB8, 0xFF), 3f);
                    break;
                case SceneMood.Night:
                    Stars(random, horizon);
                    Lamplight(CutsceneArt.LampOf(slide.Mood));
                    break;
                case SceneMood.Battle:
                case SceneMood.Trench:
                    Smoke(new Vector2(120f, horizon - 10f), 8, 0.7f, new Color32(0x6E, 0x68, 0x62, 0xFF));
                    break;
            }

            for (int i = 0; i < slide.Cast.Length && i < Marks.Length; i++)
            {
                // A pair takes the two thirds; one person alone takes the left.
                Walker(slide.Cast[i], Marks[i], fromRight: i > 0);
            }

            LateUpdate();
        }

        /// <summary>Removes every mover and lets the picture rest at full frame.</summary>
        public void Clear()
        {
            for (int i = 0; i < actors.Count; i++)
            {
                if (actors[i].Rect != null)
                {
                    Destroy(actors[i].Rect.gameObject);
                }
            }

            actors.Clear();
            playing = false;
            transform.localScale = Vector3.one;
        }

        private void LateUpdate()
        {
            if (!playing || frame == null)
            {
                return;
            }

            float t = Elapsed;
            float push = 1f + (PushIn * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / PushSeconds)));
            transform.localScale = new Vector3(push, push, 1f);

            // One painting pixel in canvas units. The picture keeps the painting's shape, so width
            // and height agree.
            float px = frame.rect.width / CutsceneArt.Width;
            for (int i = 0; i < actors.Count; i++)
            {
                Actor actor = actors[i];
                Vector2 at = actor.Path(t);

                // Snap the corner, not the pivot: a centred disc five pixels wide would otherwise
                // straddle the painting's pixels by half of one.
                Vector2 offset = Vector2.Scale(actor.Rect.pivot, actor.Size);
                Vector2 corner = new Vector2(Mathf.Round(at.x - offset.x), Mathf.Round(at.y - offset.y));
                actor.Rect.anchoredPosition = (corner + offset) * px;
                actor.Rect.sizeDelta = actor.Size * px;
                if (actor.Alpha != null)
                {
                    Color color = actor.Graphic.color;
                    color.a = Mathf.Clamp01(actor.Alpha(t));
                    actor.Graphic.color = color;
                }
            }
        }

        // ------------------------------------------------------------------ movers

        /// <summary>Clouds crossing the sky left to right at <paramref name="speed"/> pixels a second, wrapping round.</summary>
        private void Clouds(System.Random random, int horizon, int count, Color32 tint, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                int width = 26 + random.Next(20);
                int height = 7 + random.Next(5);
                Texture2D cloud = Cached("cloud" + width + "x" + height + "/" + i, () => PaintCloud(width, height, i));
                float x0 = random.Next(CutsceneArt.Width);
                float y = horizon + 34 + random.Next(CutsceneArt.Height - horizon - 46);
                float rate = speed * (0.7f + (0.15f * i));
                float span = CutsceneArt.Width + width + 20f;
                Add("Cloud", cloud, tint, new Vector2(width, height), new Vector2(0.5f, 0.5f),
                    t => new Vector2(Mathf.Repeat(x0 + (rate * t) + width, span) - width, y), null);
            }
        }

        /// <summary>The steamer home, crossing the bay with its smoke trailing behind.</summary>
        private void Ship(int horizon)
        {
            const float Speed = 6f;
            const float StartX = 70f;
            float deck = horizon - 8f;
            Texture2D hull = Cached("ship", PaintShip);

            // A slow roll: a pixel up and down, never more, or it reads as a toy.
            Add("Ship", hull, Color.white, new Vector2(hull.width, hull.height), new Vector2(0f, 0f),
                t => new Vector2(StartX + (Speed * t), deck + (Mathf.Sin(t * 1.3f) > 0.6f ? 1f : 0f)), null);
            Smoke(t => new Vector2(StartX + (Speed * t) + 21f, deck + 18f), 7, 0.6f, new Color32(0x6E, 0x68, 0x62, 0xFF));
        }

        private void Smoke(Vector2 source, int puffs, float every, Color32 tint)
        {
            Smoke(t => source, puffs, every, tint);
        }

        /// <summary>
        /// Puffs rising from <paramref name="source"/> one every <paramref name="every"/> seconds,
        /// drifting left on the wind, swelling and thinning as they go.
        /// </summary>
        private void Smoke(Func<float, Vector2> source, int puffs, float every, Color32 tint)
        {
            float life = puffs * every;
            for (int i = 0; i < puffs; i++)
            {
                float offset = i * every;
                Add("Puff", Puff(1), tint, new Vector2(3f, 3f), new Vector2(0.5f, 0.5f),
                    t =>
                    {
                        float age = Mathf.Repeat(t + offset, life);
                        Vector2 from = source(t - age);
                        return from + new Vector2(-age * 3f, age * 5f);
                    },
                    t =>
                    {
                        float age = Mathf.Repeat(t + offset, life);
                        return Mathf.Min(1f, t * 2f) * 0.85f * (1f - (age / life));
                    });

                // Swelling is a change of disc, so it rides on the path: one painted at each radius,
                // so a puff's edge always falls on the painting's own pixels.
                Actor actor = actors[actors.Count - 1];
                var image = (RawImage)actor.Graphic;
                Func<float, Vector2> path = actor.Path;
                actor.Path = t =>
                {
                    float age = Mathf.Repeat(t + offset, life);
                    int radius = 1 + Mathf.FloorToInt(age / life * 5f);
                    image.texture = Puff(radius);
                    actor.Size = new Vector2((radius * 2) + 1, (radius * 2) + 1);
                    return path(t);
                };
            }
        }

        /// <summary>A few stars that come and go over the painted ones.</summary>
        private void Stars(System.Random random, int horizon)
        {
            Texture2D star = Cached("star", () => PaintDisc(1, false));
            for (int i = 0; i < 10; i++)
            {
                float x = random.Next(CutsceneArt.Width);
                float y = horizon + 24 + random.Next(CutsceneArt.Height - horizon - 26);
                float rate = 1.2f + ((float)random.NextDouble() * 1.8f);
                float phase = (float)random.NextDouble() * 6.28f;
                Add("Star", star, new Color32(0xF0, 0xE8, 0xD0, 0xFF), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                    t => new Vector2(x, y), t => 0.25f + (0.75f * Mathf.Abs(Mathf.Sin((t * rate) + phase))));
            }
        }

        /// <summary>A warm glow round the painted lamp, flickering as a flame does.</summary>
        private void Lamplight(Vector2Int lamp)
        {
            Texture2D glow = Cached("glow", () => PaintDisc(32, true));
            Add("Lamplight", glow, new Color32(0xF0, 0xC0, 0x60, 0xFF), new Vector2(64f, 64f), new Vector2(0.5f, 0.5f),
                t => lamp, t => 0.45f + (0.2f * Mathf.PerlinNoise(t * 3.1f, 0.37f)) - (0.1f * Mathf.PerlinNoise(t * 9.7f, 1.3f)));
        }

        /// <summary>A small fire: a flame that never holds still, and the light it throws.</summary>
        private void Fire(Vector2 at)
        {
            Texture2D glow = Cached("glow", () => PaintDisc(32, true));
            Add("Firelight", glow, new Color32(0xF0, 0xA0, 0x50, 0xFF), new Vector2(40f, 40f), new Vector2(0.5f, 0.5f),
                t => at, t => 0.35f + (0.2f * Mathf.PerlinNoise(t * 4.3f, 0.71f)));
            Texture2D flame = Cached("flame", () => PaintDisc(9, false));
            Add("Flame", flame, new Color32(0xF0, 0x8A, 0x40, 0xFF), new Vector2(5f, 6f), new Vector2(0.5f, 0f),
                t => at + new Vector2(0f, -2f), null);
            Add("Flame Core", flame, new Color32(0xF8, 0xD8, 0x78, 0xFF), new Vector2(3f, 4f), new Vector2(0.5f, 0f),
                t => at + new Vector2(Mathf.PerlinNoise(t * 7f, 0.2f) > 0.5f ? 1f : 0f, -2f), null);

            // The outer flame leaps a pixel or two, a few times a second.
            Actor outer = actors[actors.Count - 2];
            Func<float, Vector2> path = outer.Path;
            outer.Path = t =>
            {
                outer.Size = new Vector2(5f, 5f + Mathf.Round(Mathf.PerlinNoise(t * 6f, 0.9f) * 3f));
                return path(t);
            };
        }

        /// <summary>
        /// Someone walking in to <paramref name="mark"/> from just off it, fading in as they come,
        /// with a step's bob while they move. Someone coming from the right turns to face left.
        /// </summary>
        private void Walker(string artId, float mark, bool fromRight)
        {
            Sprite body = Theme.Assets != null ? Theme.Assets.UnitBody(artId) : null;
            if (body == null)
            {
                return;
            }

            RectTransform rect = UiKit.NewRect(transform, "Cast " + artId);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = body;
            image.raycastTarget = false;
            image.preserveAspect = true;
            if (fromRight)
            {
                rect.localScale = new Vector3(-1f, 1f, 1f);
            }

            float from = mark + (fromRight ? WalkDistance : -WalkDistance);
            AddActor(rect, image, new Vector2(body.rect.width, body.rect.height), new Vector2(0.5f, 0f),
                t =>
                {
                    float k = Mathf.Clamp01((t - WalkDelay) / WalkSeconds);
                    float eased = 1f - ((1f - k) * (1f - k));
                    float bob = k > 0f && k < 1f ? Mathf.Round(Mathf.Abs(Mathf.Sin(t * 11f)) * 1.5f) : 0f;
                    return new Vector2(Mathf.Lerp(from, mark, eased), FeetY + bob);
                },
                t => Mathf.Clamp01((t - WalkDelay) / (WalkSeconds * 0.5f)));
        }

        private static Texture2D Puff(int radius)
        {
            return Cached("puff" + radius, () => PaintDisc(radius, false));
        }

        private void Add(string name, Texture2D texture, Color tint, Vector2 size, Vector2 pivot, Func<float, Vector2> path, Func<float, float> alpha)
        {
            RectTransform rect = UiKit.NewRect(transform, name);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.color = tint;
            image.raycastTarget = false;
            AddActor(rect, image, size, pivot, path, alpha);
        }

        private void AddActor(RectTransform rect, Graphic graphic, Vector2 size, Vector2 pivot, Func<float, Vector2> path, Func<float, float> alpha)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = pivot;
            actors.Add(new Actor { Rect = rect, Graphic = graphic, Size = size, Path = path, Alpha = alpha });
        }

        // ------------------------------------------------------------------ painting

        private static Texture2D Cached(string key, Func<Texture2D> paint)
        {
            Texture2D texture;
            if (!textures.TryGetValue(key, out texture) || texture == null)
            {
                texture = paint();
                textures[key] = texture;
            }

            return texture;
        }

        private static Texture2D NewTexture(int width, int height, FilterMode filter)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = filter;
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        /// <summary>A flat-bottomed cloud of overlapping lumps, white, for tinting.</summary>
        private static Texture2D PaintCloud(int width, int height, int seed)
        {
            var random = new System.Random(seed * 7919 + width);
            var pixels = new Color32[width * height];
            int lumps = 3 + random.Next(3);
            for (int n = 0; n < lumps; n++)
            {
                int cx = (width / (lumps + 1)) * (n + 1) + random.Next(5) - 2;
                int r = 3 + random.Next(Mathf.Max(1, height - 3));
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int dx = x - cx;
                        int dy = y - 1;
                        if ((dx * dx) + (dy * dy * 2) <= r * r)
                        {
                            // The underside a shade darker, the way the painted clouds of the period sit on the air.
                            pixels[(y * width) + x] = y < 2 ? new Color32(0xE0, 0xE0, 0xE0, 0xFF) : new Color32(0xFF, 0xFF, 0xFF, 0xFF);
                        }
                    }
                }
            }

            Texture2D texture = NewTexture(width, height, FilterMode.Point);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        /// <summary>The steamer: hull, deckhouse, two masts and a funnel, in ink.</summary>
        private static Texture2D PaintShip()
        {
            const int W = 46;
            const int H = 26;
            var pixels = new Color32[W * H];
            var ink = new Color32(0x3A, 0x2E, 0x2A, 0xFF);
            var trim = new Color32(0xE9, 0xE2, 0xD0, 0xFF);
            Fill(pixels, W, 1, 0, 44, 5, ink);
            Fill(pixels, W, 0, 3, 46, 2, ink);
            Fill(pixels, W, 2, 4, 42, 1, trim);
            Fill(pixels, W, 6, 5, 32, 4, ink);
            Fill(pixels, W, 13, 9, 2, 16, ink);
            Fill(pixels, W, 31, 9, 2, 13, ink);
            Fill(pixels, W, 19, 9, 4, 8, ink);
            Fill(pixels, W, 19, 16, 4, 1, new Color32(0x8C, 0x2E, 0x22, 0xFF));
            Texture2D texture = NewTexture(W, H, FilterMode.Point);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        /// <summary>A disc of <paramref name="radius"/>: hard-edged, or a soft falloff for a glow.</summary>
        private static Texture2D PaintDisc(int radius, bool soft)
        {
            int size = (radius * 2) + 1;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt(((x - radius) * (x - radius)) + ((y - radius) * (y - radius))) / Mathf.Max(1f, radius);
                    byte a = soft ? (byte)(255f * Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d)) : (byte)(d <= 1f ? 255 : 0);
                    pixels[(y * size) + x] = new Color32(0xFF, 0xFF, 0xFF, a);
                }
            }

            Texture2D texture = NewTexture(size, size, soft ? FilterMode.Bilinear : FilterMode.Point);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static void Fill(Color32[] pixels, int width, int x, int y, int w, int h, Color32 color)
        {
            int height = pixels.Length / width;
            for (int yy = y; yy < y + h; yy++)
            {
                for (int xx = x; xx < x + w; xx++)
                {
                    if (xx >= 0 && xx < width && yy >= 0 && yy < height)
                    {
                        pixels[(yy * width) + xx] = color;
                    }
                }
            }
        }
    }
}
