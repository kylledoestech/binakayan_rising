using System;
using System.Collections.Generic;
using BinakayanRising.Core.Content;
using BinakayanRising.Core.Grid;
using BinakayanRising.Gameplay;
using BinakayanRising.UI.Kit;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BinakayanRising.UI.Camp
{
    /// <summary>
    /// The walkable encampment: painted ground, the buildings, the keepers, the player's own
    /// figure, and the gold arrow over whatever the current objective asks for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything is built from <see cref="Encampment"/>: the layout is data, so this class only
    /// draws it and moves the player through it. Clicking the ground walks there; clicking a
    /// building or a keeper walks to its door and then raises <see cref="SiteReached"/> or
    /// <see cref="FigureReached"/> for the screen to act on.
    /// </para>
    /// <para>
    /// The camera is framed so art pixels land on whole screen pixels (two per art pixel at
    /// 1080p), and handed back as it was when the camp hides, so a battle finds it untouched.
    /// </para>
    /// </remarks>
    public sealed class CampWorld : MonoBehaviour
    {
        /// <summary>Walking pace, in cells per second.</summary>
        private const float WalkSpeed = 3.2f;

        /// <summary>How long each foot stays down in the walk bob.</summary>
        private const float StepSeconds = 0.14f;

        /// <summary>How far the arrow bobs, in art pixels, and how fast.</summary>
        private const float ArrowBobPixels = 3f;
        private const float ArrowBobSpeed = 4f;

        /// <summary>Space kept clear between the art and the screen's edges, in art pixels.</summary>
        private const float FrameMarginPixels = 6f;

        private const int GroundOrder = -30000;
        private const int MarkOrder = -29000;
        private const int ShadowOrder = -28000;
        private const int ArrowOrder = 30000;

        /// <summary>Scenery sprite names under Art/Encampment/.</summary>
        private static readonly string[] BackTrees = { "Palm", "Bamboo", "Banana", "Palm", "Bamboo" };

        private sealed class SiteView
        {
            public CampSite Site;
            public SpriteRenderer Renderer;
            public SpriteRenderer Outline;

            /// <summary>World height of the highest opaque pixel, for the arrow and the name plate.</summary>
            public float Top;
        }

        private sealed class FigureView
        {
            public CampFigure Figure;
            public SpriteRenderer Renderer;
            public float Phase;
        }

        private readonly List<SiteView> sites = new List<SiteView>();
        private readonly List<FigureView> figures = new List<FigureView>();

        private Camera view;
        private bool cameraSaved;
        private Vector3 savedPosition;
        private float savedSize;
        private bool savedOrthographic;
        private Color savedBackground;
        private CameraClearFlags savedClear;
        private Vector2Int framedFor;
        private float topInset;

        private Rect content;

        private Transform avatar;
        private SpriteRenderer avatarBody;
        private Vector2 avatarGrid;
        private List<GridCoord> route;
        private int routeIndex;
        private Action arrive;
        private float stepClock;

        private bool hoverForced;
        private SiteView forcedSite;
        private FigureView forcedFigure;

        private SpriteRenderer targetRing;
        private SpriteRenderer arrow;
        private SpriteRenderer arrowPad;
        private float arrowTop;
        private bool arrowShown;

        /// <summary>When false the camp ignores the pointer: a panel or dialogue is over it.</summary>
        public bool InputEnabled { get; set; }

        /// <summary>Raised when the player arrives at a building's door after clicking it.</summary>
        public event Action<CampSite> SiteReached;

        /// <summary>Raised when the player arrives beside a figure after clicking it.</summary>
        public event Action<CampFigure> FigureReached;

        /// <summary>The building under the pointer, or null.</summary>
        public CampSite HoveredSite { get; private set; }

        /// <summary>The figure under the pointer, or null.</summary>
        public CampFigure HoveredFigure { get; private set; }

        /// <summary>World point just above whatever is hovered, for its name plate.</summary>
        public Vector3 HoverAnchor { get; private set; }

        /// <summary>
        /// Screen pixels at the top covered by the interface. The camp is framed in the space
        /// under it, and reframed when it changes (the canvas scale settles a frame after start).
        /// </summary>
        public float TopInsetPixels
        {
            get
            {
                return topInset;
            }

            set
            {
                if (Mathf.Abs(value - topInset) < 0.5f)
                {
                    return;
                }

                topInset = value;
                if (view != null && cameraSaved)
                {
                    Frame();
                }
            }
        }

        /// <summary>The camera the camp is drawn with.</summary>
        public Camera View
        {
            get { return view; }
        }

        public bool IsWalking
        {
            get { return route != null; }
        }

        /// <summary>The cell the player's figure stands on, or is about to reach.</summary>
        public GridCoord AvatarCell
        {
            get
            {
                return route != null && routeIndex < route.Count
                    ? route[routeIndex]
                    : new GridCoord(Mathf.RoundToInt(avatarGrid.x), Mathf.RoundToInt(avatarGrid.y));
            }
        }

        /// <summary>Builds the camp, hidden. <see cref="Show"/> puts it on screen.</summary>
        public static CampWorld Create()
        {
            var root = new GameObject("Camp World");
            var world = root.AddComponent<CampWorld>();
            world.Build();
            root.SetActive(false);
            return world;
        }

        // ------------------------------------------------------------------ building

        private void Build()
        {
            Rect groundArea = new Rect(-9f, -3f, 18f, 11f);
            SpriteRenderer ground = NewSprite("Ground", CampGround.Paint(groundArea), GroundOrder);
            ground.transform.localPosition = new Vector3(groundArea.xMin, groundArea.yMin, 0f);

            BuildScenery(groundArea);

            Sprite shadow = CampMarks.Shadow();
            content = Rect.MinMaxRect(CampIso.ToWorld(-0.5f, Encampment.Height - 0.5f).x, CampIso.ToWorld(-0.5f, -0.5f).y,
                                      CampIso.ToWorld(Encampment.Width - 0.5f, -0.5f).x, CampIso.ToWorld(Encampment.Width - 0.5f, Encampment.Height - 0.5f).y);

            var outlines = new Dictionary<int, Sprite>();
            foreach (CampSite site in Encampment.Sites)
            {
                Vector2 centre = CampIso.ToWorld(site.CentreX, site.CentreY);
                var entry = new SiteView { Site = site, Top = centre.y + 0.6f };

                int key = (site.Width * 16) + site.Height;
                if (!outlines.TryGetValue(key, out Sprite outline))
                {
                    outline = CampMarks.Footprint(site.Width, site.Height, Theme.Camp.Hover, 40);
                    outlines[key] = outline;
                }

                entry.Outline = NewSprite("Outline " + site.Place, outline, MarkOrder);
                entry.Outline.transform.localPosition = centre;
                entry.Outline.enabled = false;

                Sprite art = CampArt(site.Art);
                if (art != null)
                {
                    entry.Renderer = NewSprite(site.Art, art, CampIso.SortOrder(centre.y));
                    entry.Renderer.transform.localPosition = centre;
                    entry.Top = centre.y + (OpaqueTop(art) / CampIso.PixelsPerUnit);
                    content.yMax = Mathf.Max(content.yMax, entry.Top);
                }

                sites.Add(entry);
            }

            foreach (CampProp prop in Encampment.Props)
            {
                Sprite art = CampArt(prop.Art);
                if (art != null)
                {
                    Vector2 at = CampIso.ToWorld(prop.Cell);
                    NewSprite(prop.Art, art, CampIso.SortOrder(at.y)).transform.localPosition = at;
                }
            }

            foreach (CampFigure figure in Encampment.Figures)
            {
                Vector2 at = CampIso.ToWorld(figure.Cell);
                NewSprite("Shadow " + figure.Character, shadow, ShadowOrder).transform.localPosition = at;

                var entry = new FigureView { Figure = figure, Phase = CampGround.Hash(figure.Cell.X, figure.Cell.Y, 29) * 2f };
                Sprite body = Theme.Assets != null ? Theme.Assets.UnitBody(figure.Character) : null;
                if (body != null)
                {
                    entry.Renderer = NewSprite(figure.Character, body, CampIso.SortOrder(at.y) + 1);
                    entry.Renderer.transform.localPosition = at;
                    entry.Renderer.flipX = figure.FacesLeft;
                }

                figures.Add(entry);
            }

            // The player: Evangelista, the engineer the camp is waiting for.
            avatar = new GameObject("Player").transform;
            avatar.SetParent(transform, false);
            SpriteRenderer avatarShadow = NewSprite("Shadow", shadow, ShadowOrder);
            avatarShadow.transform.SetParent(avatar, false);
            Sprite playerBody = Theme.Assets != null ? Theme.Assets.UnitBody(UnitCatalog.Evangelista) : null;
            avatarBody = NewSprite("Body", playerBody, 0);
            avatarBody.transform.SetParent(avatar, false);
            PlaceAvatar(Encampment.Spawn);

            targetRing = NewSprite("Walk Target", CampMarks.Footprint(1, 1, Theme.Camp.Target, 0), MarkOrder + 1);
            targetRing.enabled = false;

            arrowPad = NewSprite("Objective Pad", CampMarks.Footprint(1, 1, Theme.Camp.Marker, 70), MarkOrder + 2);
            arrow = NewSprite("Objective Arrow", CampMarks.Arrow(), ArrowOrder);
            arrow.enabled = false;
            arrowPad.enabled = false;
        }

        /// <summary>
        /// Woods round the clearing, placed from a hash of each cell so the camp looks the same on
        /// every visit. Only low bushes stand along the two near edges, where anything taller would
        /// hide the camp; the far edges and the two side corners get the trees.
        /// </summary>
        private void BuildScenery(Rect area)
        {
            for (int y = -14; y < Encampment.Height + 14; y++)
            {
                for (int x = -14; x < Encampment.Width + 14; x++)
                {
                    var cell = new GridCoord(x, y);
                    if (Encampment.Inside(cell))
                    {
                        continue;
                    }

                    Vector2 at = CampIso.ToWorld(cell);
                    if (!area.Contains(at))
                    {
                        continue;
                    }

                    bool behind = x >= Encampment.Width || y >= Encampment.Height;
                    bool side = Mathf.Abs(x - y) >= Encampment.Width - 1;
                    float roll = CampGround.Hash(x, y, 41);
                    string art;
                    if (behind || side)
                    {
                        if (roll > 0.62f)
                        {
                            continue;
                        }

                        art = roll < 0.12f ? "Bush" : BackTrees[(int)(CampGround.Hash(x, y, 43) * BackTrees.Length) % BackTrees.Length];
                    }
                    else
                    {
                        // The first ring stays open so the camp's edge reads; beyond it, a scatter.
                        bool firstRing = x >= -1 && y >= -1;
                        if (roll > (firstRing ? 0.12f : 0.3f))
                        {
                            continue;
                        }

                        art = "Bush";
                    }

                    Sprite sprite = CampArt(art);
                    if (sprite == null)
                    {
                        continue;
                    }

                    // A few pixels of jitter so the woods do not stand on the grid.
                    float jitterX = Mathf.Round((CampGround.Hash(x, y, 47) - 0.5f) * 16f) / CampIso.PixelsPerUnit;
                    float jitterY = Mathf.Round((CampGround.Hash(x, y, 53) - 0.5f) * 8f) / CampIso.PixelsPerUnit;
                    Vector2 position = at + new Vector2(jitterX, jitterY);
                    SpriteRenderer tree = NewSprite(art, sprite, CampIso.SortOrder(position.y));
                    tree.transform.localPosition = position;
                    tree.flipX = CampGround.Hash(x, y, 59) < 0.5f;
                }
            }
        }

        private SpriteRenderer NewSprite(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static Sprite CampArt(string name)
        {
            return Theme.Assets != null ? Theme.Assets.CampSprite(name) : null;
        }

        /// <summary>Pixels from the pivot up to the sprite's highest opaque row.</summary>
        private static float OpaqueTop(Sprite sprite)
        {
            Texture2D texture = sprite.texture;
            Rect rect = sprite.rect;
            if (!texture.isReadable)
            {
                return rect.height - sprite.pivot.y;
            }

            Color32[] pixels = texture.GetPixels32();
            for (int y = (int)rect.yMax - 1; y >= (int)rect.yMin; y--)
            {
                for (int x = (int)rect.xMin; x < (int)rect.xMax; x++)
                {
                    if (pixels[(y * texture.width) + x].a > 127)
                    {
                        return y - rect.yMin + 1 - sprite.pivot.y;
                    }
                }
            }

            return 0f;
        }

        // ------------------------------------------------------------------ showing

        /// <summary>
        /// Puts the camp on screen and frames the camera on it, leaving
        /// <paramref name="topInsetPixels"/> at the top for the interface that covers it.
        /// </summary>
        public void Show(float topInsetPixels)
        {
            topInset = topInsetPixels;
            gameObject.SetActive(true);
            TakeCamera();
            Frame();
        }

        /// <summary>Hides the camp and gives the camera back as it was.</summary>
        public void Hide()
        {
            InputEnabled = false;
            SetHover(null, null);
            if (view != null && cameraSaved)
            {
                view.transform.position = savedPosition;
                view.orthographicSize = savedSize;
                view.orthographic = savedOrthographic;
                view.backgroundColor = savedBackground;
                view.clearFlags = savedClear;
                cameraSaved = false;
            }

            gameObject.SetActive(false);
        }

        /// <summary>Sends the player's figure back to where it first appears, standing still.</summary>
        public void ResetAvatar()
        {
            route = null;
            arrive = null;
            targetRing.enabled = false;
            PlaceAvatar(Encampment.Spawn);
        }

        private void TakeCamera()
        {
            view = Camera.main;
            if (view == null)
            {
                view = new GameObject("Camp Camera").AddComponent<Camera>();
                view.tag = "MainCamera";
            }

            if (!cameraSaved)
            {
                savedPosition = view.transform.position;
                savedSize = view.orthographicSize;
                savedOrthographic = view.orthographic;
                savedBackground = view.backgroundColor;
                savedClear = view.clearFlags;
                cameraSaved = true;
            }

            view.orthographic = true;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = Theme.Camp.JungleDark;
        }

        /// <summary>
        /// The largest whole number of screen pixels per art pixel that fits the camp below the
        /// interface; fractional only on a screen too small for even one.
        /// </summary>
        private void Frame()
        {
            framedFor = new Vector2Int(Screen.width, Screen.height);
            float margin = FrameMarginPixels / CampIso.PixelsPerUnit;
            float needWidth = content.width + (margin * 2f);
            float needHeight = content.height + (margin * 2f);

            float pixelsPerUnit = 0f;
            for (int scale = 6; scale >= 1; scale--)
            {
                float candidate = CampIso.PixelsPerUnit * scale;
                if (needWidth * candidate <= Screen.width && (needHeight * candidate) + topInset <= Screen.height)
                {
                    pixelsPerUnit = candidate;
                    break;
                }
            }

            if (pixelsPerUnit <= 0f)
            {
                pixelsPerUnit = Mathf.Min(Screen.width / needWidth, (Screen.height - topInset) / needHeight);
            }

            view.orthographicSize = Screen.height / (2f * pixelsPerUnit);

            // Centre the camp in the space under the interface, then snap to a screen pixel.
            float insetWorld = topInset / pixelsPerUnit;
            float x = content.center.x;
            float y = content.center.y + (insetWorld * 0.5f);
            x = Mathf.Round(x * pixelsPerUnit) / pixelsPerUnit;
            y = Mathf.Round(y * pixelsPerUnit) / pixelsPerUnit;
            view.transform.position = new Vector3(x, y, -10f);
        }

        // ------------------------------------------------------------------ objective arrow

        /// <summary>
        /// Points the gold arrow at <paramref name="place"/> (a <see cref="Places"/> id) and marks
        /// the cell to stand on. Null, or a place the camp does not have, hides both.
        /// </summary>
        public void PointAt(string place)
        {
            GridCoord? stand = place != null ? Encampment.StandCell(place) : null;
            if (stand == null)
            {
                arrowShown = false;
                arrow.enabled = false;
                arrowPad.enabled = false;
                return;
            }

            float top;
            float x;
            if (place == Places.Aide)
            {
                FigureView aide = FindFigure(Characters.Tomas);
                Vector2 at = CampIso.ToWorld(aide.Figure.Cell);
                x = at.x;
                top = at.y + FigureHeight(aide.Renderer);
            }
            else
            {
                SiteView site = FindSite(place);
                x = CampIso.ToWorld(site.Site.CentreX, site.Site.CentreY).x;
                top = site.Top;
            }

            arrowTop = top + (4f / CampIso.PixelsPerUnit);
            arrow.transform.localPosition = CampIso.Snap(new Vector2(x, arrowTop));
            arrowPad.transform.localPosition = CampIso.ToWorld(stand.Value);
            arrowShown = true;
            arrow.enabled = true;
            arrowPad.enabled = true;
        }

        /// <summary>
        /// The highest the arrow's tip may rest and still bob clear of the interface over the top
        /// of the camp; a tall building like the Mission Tent would otherwise push it under the bar.
        /// </summary>
        private float ArrowCeiling()
        {
            float screenPerUnit = Screen.height / (2f * view.orthographicSize);
            float visibleTop = view.transform.position.y + view.orthographicSize - (topInset / screenPerUnit);
            float room = arrow.sprite.bounds.size.y + ((ArrowBobPixels + FrameMarginPixels) / CampIso.PixelsPerUnit);
            return Mathf.Floor((visibleTop - room) * CampIso.PixelsPerUnit) / CampIso.PixelsPerUnit;
        }

        /// <summary>World bounds of the arrow as drawn, for layout checks.</summary>
        public Bounds ArrowBounds
        {
            get { return arrow.bounds; }
        }

        /// <summary>World position of the arrow's tip, for tests and screenshots.</summary>
        public Vector3 ArrowTip
        {
            get { return arrow.transform.position; }
        }

        public bool ArrowShown
        {
            get { return arrowShown; }
        }

        // ------------------------------------------------------------------ frame loop

        private void Update()
        {
            if (view == null)
            {
                return;
            }

            if (framedFor.x != Screen.width || framedFor.y != Screen.height)
            {
                Frame();
            }

            ReadPointer();
            Walk(Time.deltaTime);
            Animate();
        }

        private void ReadPointer()
        {
            if (hoverForced)
            {
                SetHover(forcedSite, forcedFigure);
                return;
            }

            Pointer pointer = Pointer.current;
            if (!InputEnabled || pointer == null)
            {
                SetHover(null, null);
                return;
            }

            Vector2 screen = pointer.position.ReadValue();
            if (UiPointer.IsOverUi(screen))
            {
                SetHover(null, null);
                return;
            }

            Vector2 world = view.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));
            FigureView figure = FigureAt(world);
            SiteView site = figure == null ? SiteAt(world) : null;
            SetHover(site, figure);

            if (!pointer.press.wasPressedThisFrame)
            {
                return;
            }

            if (figure != null)
            {
                ClickFigure(figure.Figure.Character);
                return;
            }

            if (site != null)
            {
                ClickSite(site.Site.Place);
                return;
            }

            GridCoord cell = CampIso.Cell(world);
            if (Encampment.IsWalkable(cell))
            {
                WalkTo(cell, null);
            }
        }

        private static void Raise<T>(Action<T> handler, T value)
        {
            if (handler != null)
            {
                handler(value);
            }
        }

        private void SetHover(SiteView site, FigureView figure)
        {
            for (int i = 0; i < sites.Count; i++)
            {
                sites[i].Outline.enabled = sites[i] == site;
            }

            HoveredSite = site != null ? site.Site : null;
            HoveredFigure = figure != null ? figure.Figure : null;
            if (site != null)
            {
                HoverAnchor = new Vector3(CampIso.ToWorld(site.Site.CentreX, site.Site.CentreY).x, site.Top, 0f);
            }
            else if (figure != null)
            {
                Vector2 at = CampIso.ToWorld(figure.Figure.Cell);
                HoverAnchor = new Vector3(at.x, at.y + FigureHeight(figure.Renderer), 0f);
            }
        }

        /// <summary>The frontmost figure whose body covers the point.</summary>
        private FigureView FigureAt(Vector2 world)
        {
            FigureView best = null;
            for (int i = 0; i < figures.Count; i++)
            {
                SpriteRenderer renderer = figures[i].Renderer;
                if (renderer != null && Covers(renderer, world, false)
                    && (best == null || renderer.sortingOrder > best.Renderer.sortingOrder))
                {
                    best = figures[i];
                }
            }

            return best;
        }

        /// <summary>
        /// The frontmost building drawn under the point, by its opaque pixels; failing that, the
        /// building whose footprint the point is on.
        /// </summary>
        private SiteView SiteAt(Vector2 world)
        {
            SiteView best = null;
            for (int i = 0; i < sites.Count; i++)
            {
                SpriteRenderer renderer = sites[i].Renderer;
                if (renderer != null && Covers(renderer, world, true)
                    && (best == null || renderer.sortingOrder > best.Renderer.sortingOrder))
                {
                    best = sites[i];
                }
            }

            if (best != null)
            {
                return best;
            }

            CampSite under = Encampment.SiteAt(CampIso.Cell(world));
            return under != null ? FindSite(under.Place) : null;
        }

        /// <summary>
        /// True when a sprite draws over the point: by its alpha where the texture can be read,
        /// otherwise by the middle of its box, which is where a figure's body is.
        /// </summary>
        private static bool Covers(SpriteRenderer renderer, Vector2 world, bool byAlpha)
        {
            Sprite sprite = renderer.sprite;
            if (sprite == null || !renderer.enabled)
            {
                return false;
            }

            Vector2 local = world - (Vector2)renderer.transform.position;
            if (renderer.flipX)
            {
                local.x = -local.x;
            }

            Vector2 pixel = sprite.pivot + (local * sprite.pixelsPerUnit);
            Rect rect = sprite.rect;
            if (pixel.x < 0f || pixel.y < 0f || pixel.x >= rect.width || pixel.y >= rect.height)
            {
                return false;
            }

            if (!byAlpha || !sprite.texture.isReadable)
            {
                return pixel.x >= rect.width * 0.25f && pixel.x < rect.width * 0.75f && pixel.y < rect.height * 0.9f;
            }

            return sprite.texture.GetPixel((int)(rect.x + pixel.x), (int)(rect.y + pixel.y)).a > 0.5f;
        }

        private static float FigureHeight(SpriteRenderer renderer)
        {
            return renderer != null && renderer.sprite != null
                ? (renderer.sprite.rect.height - renderer.sprite.pivot.y) / renderer.sprite.pixelsPerUnit
                : 0.8f;
        }

        // ------------------------------------------------------------------ walking

        /// <summary>
        /// Walks the player's figure to <paramref name="goal"/> and calls
        /// <paramref name="onArrive"/> on arrival. False, and a refusal sound, when there is no way.
        /// </summary>
        public bool WalkTo(GridCoord goal, Action onArrive)
        {
            List<GridCoord> walk = Encampment.Path(AvatarCell, goal);
            if (walk == null)
            {
                UiSfx.Play(UiSfx.Cue.Error);
                return false;
            }

            route = walk;
            routeIndex = 0;
            arrive = onArrive;
            targetRing.transform.localPosition = CampIso.ToWorld(goal);
            targetRing.enabled = walk.Count > 1;
            UiSfx.Play(UiSfx.Cue.Click);
            return true;
        }

        /// <summary>Moves the player's figure to a cell at once, for screenshots and tests.</summary>
        public void PlaceAvatar(GridCoord cell)
        {
            avatarGrid = new Vector2(cell.X, cell.Y);
            route = null;
            UpdateAvatar(0f);
        }

        private void Walk(float deltaTime)
        {
            if (route == null)
            {
                stepClock = 0f;
                UpdateAvatar(0f);
                return;
            }

            float budget = WalkSpeed * deltaTime;
            float facing = 0f;
            while (route != null && budget > 0f)
            {
                var target = new Vector2(route[routeIndex].X, route[routeIndex].Y);
                Vector2 delta = target - avatarGrid;
                float distance = delta.magnitude;
                if (distance > 0.0001f)
                {
                    facing = delta.x - delta.y;
                }

                if (distance <= budget)
                {
                    avatarGrid = target;
                    budget -= distance;
                    routeIndex++;
                    if (routeIndex >= route.Count)
                    {
                        Arrive();
                    }
                }
                else
                {
                    avatarGrid += delta / distance * budget;
                    budget = 0f;
                }
            }

            if (facing < -0.01f)
            {
                avatarBody.flipX = true;
            }
            else if (facing > 0.01f)
            {
                avatarBody.flipX = false;
            }

            stepClock += deltaTime;
            UpdateAvatar(route != null ? 1f : 0f);
        }

        /// <summary>Turns the player's figure towards a world point: they talk to people face on.</summary>
        private void Face(Vector2 world)
        {
            float dx = world.x - avatar.localPosition.x;
            if (Mathf.Abs(dx) > 0.01f)
            {
                avatarBody.flipX = dx < 0f;
            }
        }

        private void Arrive()
        {
            route = null;
            targetRing.enabled = false;
            Action done = arrive;
            arrive = null;
            if (done != null)
            {
                done();
            }
        }

        private void UpdateAvatar(float walking)
        {
            Vector2 at = CampIso.Snap(CampIso.ToWorld(avatarGrid.x, avatarGrid.y));
            avatar.localPosition = at;
            avatarBody.sortingOrder = CampIso.SortOrder(at.y) + 2;

            // One art pixel up on every other step.
            bool up = walking > 0f && ((int)(stepClock / StepSeconds) & 1) == 1;
            avatarBody.transform.localPosition = new Vector3(0f, up ? 1f / CampIso.PixelsPerUnit : 0f, 0f);
        }

        private void Animate()
        {
            float time = Time.time;

            // Keepers breathe: one pixel, slowly, each out of step with the others.
            for (int i = 0; i < figures.Count; i++)
            {
                SpriteRenderer renderer = figures[i].Renderer;
                if (renderer == null)
                {
                    continue;
                }

                Vector2 at = CampIso.ToWorld(figures[i].Figure.Cell);
                bool up = Mathf.Repeat(time * 0.6f + figures[i].Phase, 2f) > 1.4f;
                renderer.transform.localPosition = new Vector3(at.x, at.y + (up ? 1f / CampIso.PixelsPerUnit : 0f), 0f);
            }

            if (arrowShown)
            {
                float bob = Mathf.Round((Mathf.Sin(time * ArrowBobSpeed) + 1f) * 0.5f * ArrowBobPixels) / CampIso.PixelsPerUnit;
                Vector3 position = arrow.transform.localPosition;
                arrow.transform.localPosition = new Vector3(position.x, Mathf.Min(arrowTop, ArrowCeiling()) + bob, 0f);

                Color pad = arrowPad.color;
                pad.a = 0.45f + (0.55f * (0.5f + (0.5f * Mathf.Sin(time * ArrowBobSpeed))));
                arrowPad.color = pad;
            }
        }

        // ------------------------------------------------------------------ scripted input

        /// <summary>
        /// Does what clicking a building does: walks to its door, then raises
        /// <see cref="SiteReached"/>. For the screenshot autopilot, which has no real pointer.
        /// </summary>
        public bool ClickSite(string place)
        {
            SiteView site = FindSite(place);
            if (site == null)
            {
                return false;
            }

            CampSite target = site.Site;
            return WalkTo(target.Door, () =>
            {
                Face(CampIso.ToWorld(target.CentreX, target.CentreY));
                Raise(SiteReached, target);
            });
        }

        /// <summary>Does what clicking a figure does. For the screenshot autopilot.</summary>
        public bool ClickFigure(string character)
        {
            FigureView figure = FindFigure(character);
            if (figure == null)
            {
                return false;
            }

            CampFigure target = figure.Figure;
            return WalkTo(target.Talk, () =>
            {
                Face(CampIso.ToWorld(target.Cell));
                Raise(FigureReached, target);
            });
        }

        /// <summary>
        /// Holds the hover on a building or a figure regardless of the pointer, for screenshots.
        /// Both null hands the hover back to the pointer.
        /// </summary>
        public void ForceHover(string place, string character)
        {
            forcedSite = place != null ? FindSite(place) : null;
            forcedFigure = character != null ? FindFigure(character) : null;
            hoverForced = forcedSite != null || forcedFigure != null;
        }

        // ------------------------------------------------------------------ lookup

        private SiteView FindSite(string place)
        {
            for (int i = 0; i < sites.Count; i++)
            {
                if (sites[i].Site.Place == place)
                {
                    return sites[i];
                }
            }

            return null;
        }

        private FigureView FindFigure(string character)
        {
            for (int i = 0; i < figures.Count; i++)
            {
                if (figures[i].Figure.Character == character)
                {
                    return figures[i];
                }
            }

            return null;
        }

        /// <summary>World position of a figure's feet, for screenshots and tests.</summary>
        public Vector3 FigurePosition(string character)
        {
            FigureView figure = FindFigure(character);
            return figure != null ? (Vector3)CampIso.ToWorld(figure.Figure.Cell) : Vector3.zero;
        }

        /// <summary>The renderer of a building's art, or null, for screenshots and tests.</summary>
        public SpriteRenderer SiteRenderer(string place)
        {
            SiteView site = FindSite(place);
            return site != null ? site.Renderer : null;
        }

        /// <summary>World bounds the camera frames: the clearing and the tallest roof.</summary>
        public Rect ContentBounds
        {
            get { return content; }
        }
    }
}
