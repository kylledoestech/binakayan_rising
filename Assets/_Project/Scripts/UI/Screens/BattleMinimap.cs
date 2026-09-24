using System.Collections.Generic;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Grid;
using BinakayanRising.Core.Localization;
using BinakayanRising.Gameplay;
using BinakayanRising.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BinakayanRising.UI.Screens
{
    /// <summary>
    /// The battle's minimap: the whole board as the camera sees it, a dot per living soldier, and
    /// a frame around what is on screen. Click or drag on it to move the camera there.
    /// </summary>
    /// <remarks>
    /// Drawn in world space, not grid space, so its diamond matches the isometric board the
    /// player is looking at. The tiles are painted once into a texture; the dots and the camera
    /// frame move on top every frame.
    /// </remarks>
    public sealed class BattleMinimap : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public const float Width = 300f;
        public const float Height = 200f;

        private const float Inset = 10f;
        private const float HeaderHeight = 22f;
        private const float DotSize = 9f;
        private const float FrameLine = 2f;
        private const int TexturePixelsPerUnit = 24;

        private BattlePlaytest battle;
        private RectTransform root;
        private RectTransform field;
        private RawImage tiles;
        private readonly RectTransform[] frame = new RectTransform[4];
        private readonly List<Image> dots = new List<Image>();
        private readonly List<BattlePlaytest.UnitSnapshot> units = new List<BattlePlaytest.UnitSnapshot>();
        private Texture2D painted;
        private Rect paintedWorld;

        /// <summary>The panel, for the HUD's anchors and layout checks.</summary>
        public RectTransform Root
        {
            get { return root; }
        }

        public static BattleMinimap Create(Transform parent, BattlePlaytest battle)
        {
            RectTransform panel = UiKit.Well(parent, "Minimap", blocksClicks: true);
            var map = panel.gameObject.AddComponent<BattleMinimap>();
            map.battle = battle;
            map.root = panel;
            map.Build();
            return map;
        }

        private void Build()
        {
            TextMeshProUGUI heading = UiKit.Caption(root, Loc.Get(TextKey.HudMinimap), TextAlignmentOptions.TopLeft);
            heading.color = Theme.RevolutionDark;
            heading.fontStyle = FontStyles.UpperCase | FontStyles.Bold;
            heading.raycastTarget = false;
            UiKit.Localize(heading, TextKey.HudMinimap);
            UiKit.Anchor(heading.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Inset + 4f, -6f), new Vector2(Width - (2f * Inset), HeaderHeight));

            field = UiKit.NewRect(root, "Field");
            field.anchorMin = Vector2.zero;
            field.anchorMax = Vector2.one;
            field.offsetMin = new Vector2(Inset, Inset);
            field.offsetMax = new Vector2(-Inset, -(Inset + HeaderHeight));
            field.gameObject.AddComponent<RectMask2D>();

            tiles = field.gameObject.AddComponent<RawImage>();
            tiles.color = Color.white;
            tiles.raycastTarget = true;

            for (int i = 0; i < frame.Length; i++)
            {
                RectTransform line = UiKit.NewRect(field, "Camera " + i);
                Image image = line.gameObject.AddComponent<Image>();
                image.color = Theme.GoldBright;
                image.raycastTarget = false;
                frame[i] = line;
            }
        }

        private void LateUpdate()
        {
            if (battle == null || battle.Grid == null || field.rect.width <= 0f)
            {
                return;
            }

            Rect world = FittedWorld();
            if (painted == null || world != paintedWorld)
            {
                Paint(world);
            }

            DrawUnits(world);
            DrawCamera(world);
        }

        /// <summary>
        /// The board's world rectangle, widened to the field's shape so tiles are not stretched.
        /// </summary>
        private Rect FittedWorld()
        {
            Rect board = battle.BoardWorldRect;
            float fieldAspect = field.rect.width / field.rect.height;
            float boardAspect = board.width / Mathf.Max(0.001f, board.height);
            if (boardAspect > fieldAspect)
            {
                float height = board.width / fieldAspect;
                return new Rect(board.xMin, board.center.y - (height * 0.5f), board.width, height);
            }

            float width = board.height * fieldAspect;
            return new Rect(board.center.x - (width * 0.5f), board.yMin, width, board.height);
        }

        private void Paint(Rect world)
        {
            IBattleGrid grid = battle.Grid;
            int w = Mathf.Clamp(Mathf.CeilToInt(world.width * TexturePixelsPerUnit), 32, 512);
            int h = Mathf.Clamp(Mathf.CeilToInt(world.height * TexturePixelsPerUnit), 32, 512);
            var pixels = new Color32[w * h];
            Color32 empty = new Color32(0x24, 0x1D, 0x18, 0xFF);
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = empty;
            }

            // Each cell as a small diamond around its centre.
            float stepX = w / world.width;
            float stepY = h / world.height;
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var cell = new GridCoord(x, y);
                    Vector3 centre;
                    if (!battle.TryGetCellWorld(cell, out centre))
                    {
                        continue;
                    }

                    Color32 colour = ColourOf(grid.GetTerrain(cell), grid.IsDeployable(cell));
                    int cx = Mathf.RoundToInt((centre.x - world.xMin) * stepX);
                    int cy = Mathf.RoundToInt((centre.y - world.yMin) * stepY);
                    int rx = Mathf.Max(2, Mathf.RoundToInt(HalfTileWidth(world, grid) * stepX));
                    int ry = Mathf.Max(1, rx / 2);
                    for (int dy = -ry; dy <= ry; dy++)
                    {
                        int span = rx - Mathf.Abs(dy) * rx / Mathf.Max(1, ry);
                        for (int dx = -span; dx <= span; dx++)
                        {
                            int px = cx + dx;
                            int py = cy + dy;
                            if (px >= 0 && px < w && py >= 0 && py < h)
                            {
                                pixels[(py * w) + px] = colour;
                            }
                        }
                    }
                }
            }

            if (painted != null)
            {
                Destroy(painted);
            }

            painted = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Minimap"
            };
            painted.SetPixels32(pixels);
            painted.Apply(false, false);
            tiles.texture = painted;
            paintedWorld = world;
        }

        /// <summary>Half a tile's width in world units, from two neighbouring cells.</summary>
        private float HalfTileWidth(Rect world, IBattleGrid grid)
        {
            Vector3 a;
            Vector3 b;
            if (grid.Width > 1 && battle.TryGetCellWorld(new GridCoord(0, 0), out a) && battle.TryGetCellWorld(new GridCoord(1, 0), out b))
            {
                return Mathf.Abs(b.x - a.x);
            }

            return world.width / Mathf.Max(1, grid.Width + grid.Height);
        }

        private static Color32 ColourOf(TerrainType terrain, bool deployable)
        {
            switch (terrain)
            {
                case TerrainType.Trench:
                    return Theme.Camp.PathDark;
                case TerrainType.CoastalShallows:
                    return new Color32(0x4A, 0x6E, 0x80, 0xFF);
                case TerrainType.BambooBarricade:
                    return new Color32(0x9C, 0x86, 0x3E, 0xFF);
                case TerrainType.EncampmentTent:
                    return new Color32(0xC8, 0xB4, 0x84, 0xFF);
                default:
                    return deployable ? Theme.Camp.ClearingLight : Theme.Camp.Jungle;
            }
        }

        private Vector2 ToField(Rect world, Vector3 position)
        {
            Rect r = field.rect;
            float u = (position.x - world.xMin) / world.width;
            float v = (position.y - world.yMin) / world.height;
            return new Vector2(r.xMin + (u * r.width), r.yMin + (v * r.height));
        }

        private void DrawUnits(Rect world)
        {
            battle.GetUnits(units);
            int shown = 0;
            for (int i = 0; i < units.Count; i++)
            {
                BattlePlaytest.UnitSnapshot unit = units[i];
                if (!unit.Alive)
                {
                    continue;
                }

                if (shown == dots.Count)
                {
                    RectTransform rect = UiKit.NewRect(field, "Dot");
                    Image image = rect.gameObject.AddComponent<Image>();
                    image.raycastTarget = false;
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.sizeDelta = new Vector2(DotSize, DotSize);
                    dots.Add(image);
                }

                Image dot = dots[shown++];
                dot.gameObject.SetActive(true);
                dot.color = unit.Team == Team.Katipunan ? Theme.Revolution : Theme.ColonialLight;
                dot.rectTransform.anchoredPosition = ToField(world, unit.World);
            }

            for (int i = shown; i < dots.Count; i++)
            {
                dots[i].gameObject.SetActive(false);
            }

            // The camera frame draws over the dots.
            for (int i = 0; i < frame.Length; i++)
            {
                frame[i].SetAsLastSibling();
            }
        }

        private void DrawCamera(Rect world)
        {
            Camera camera = battle.BoardCamera;
            if (camera == null || !camera.orthographic)
            {
                return;
            }

            float halfH = camera.orthographicSize;
            float halfW = halfH * camera.aspect;
            Vector3 c = camera.transform.position;
            Vector2 min = ToField(world, new Vector3(c.x - halfW, c.y - halfH, 0f));
            Vector2 max = ToField(world, new Vector3(c.x + halfW, c.y + halfH, 0f));
            Rect r = field.rect;
            min = Vector2.Max(min, r.min);
            max = Vector2.Min(max, r.max);
            if (max.x <= min.x || max.y <= min.y)
            {
                return;
            }

            Line(frame[0], new Vector2(min.x, max.y - FrameLine), new Vector2(max.x, max.y));
            Line(frame[1], new Vector2(min.x, min.y), new Vector2(max.x, min.y + FrameLine));
            Line(frame[2], new Vector2(min.x, min.y), new Vector2(min.x + FrameLine, max.y));
            Line(frame[3], new Vector2(max.x - FrameLine, min.y), new Vector2(max.x, max.y));
        }

        private static void Line(RectTransform line, Vector2 min, Vector2 max)
        {
            line.anchorMin = line.anchorMax = new Vector2(0.5f, 0.5f);
            line.pivot = new Vector2(0.5f, 0.5f);
            line.sizeDelta = max - min;
            line.anchoredPosition = (min + max) * 0.5f;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Pan(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Pan(eventData);
        }

        private void Pan(PointerEventData eventData)
        {
            if (battle == null || battle.Grid == null)
            {
                return;
            }

            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(field, eventData.position, eventData.pressEventCamera, out local))
            {
                return;
            }

            Rect r = field.rect;
            Rect world = FittedWorld();
            float u = Mathf.Clamp01((local.x - r.xMin) / r.width);
            float v = Mathf.Clamp01((local.y - r.yMin) / r.height);
            battle.LookAt(new Vector3(world.xMin + (u * world.width), world.yMin + (v * world.height), 0f));
        }

        private void OnDestroy()
        {
            if (painted != null)
            {
                Destroy(painted);
            }
        }
    }
}
