using BinakayanRising.Core.Grid;
using BinakayanRising.Core.Localization;
using BinakayanRising.Gameplay;
using BinakayanRising.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BinakayanRising.UI.Screens
{
    /// <summary>
    /// The rules of the battle as a short deck of cards, opened from the <c>?</c> button.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A reference, not a lesson: every page can be read in any order and nothing on the board
    /// changes while it is open. The replay pauses and board and camera input are ignored, then all
    /// three go back to exactly how they were, so opening help mid-tutorial or mid-replay costs the
    /// player nothing.
    /// </para>
    /// <para>
    /// The keyboard is read by <see cref="BattleHud"/>, which sends Esc and the arrow keys here
    /// while the deck is open and nowhere else.
    /// </para>
    /// </remarks>
    [AddComponentMenu("")]
    public sealed class HowToPlayDeck : MonoBehaviour
    {
        private const float CardWidth = 1040f;
        private const float CardHeight = 700f;
        private const float TileSize = 72f;

        private static readonly TextKey[] Titles =
        {
            TextKey.DeckBattleTitle, TextKey.DeckDeployTitle, TextKey.DeckUnitsTitle, TextKey.DeckTerrainTitle,
            TextKey.DeckBondsTitle, TextKey.DeckCombatTitle, TextKey.DeckControlsTitle,
        };

        private static readonly TextKey[] Bodies =
        {
            TextKey.DeckBattleBody, TextKey.DeckDeployBody, TextKey.DeckUnitsBody, TextKey.DeckTerrainBody,
            TextKey.DeckBondsBody, TextKey.DeckCombatBody, TextKey.DeckControlsBody,
        };

        private const int TerrainPage = 3;

        private BattleHud hud;
        private BattlePlaytest battle;
        private RectTransform root;
        private RectTransform card;

        private TextMeshProUGUI pageTitle;
        private TextMeshProUGUI pageBody;
        private TextMeshProUGUI pageCounter;
        private RectTransform terrainStrip;
        private Image[] dots;
        private Button backButton;
        private Button nextButton;

        private int page;
        private int renderedPage = -1;
        private int renderedVersion = -1;

        private bool restorePaused;
        private bool restoreBoardLock;
        private bool restoreCameraLock;

        /// <summary>Whether the deck is showing.</summary>
        public bool IsOpen => root != null && root.gameObject.activeSelf;

        /// <summary>Zero-based index of the page showing.</summary>
        public int Page => page;

        /// <summary>How many pages there are.</summary>
        public int PageCount => Titles.Length;

        /// <summary>The card, for measuring.</summary>
        public RectTransform Card => card;

        /// <summary>Creates a closed deck on its own canvas above the HUD and tutorial.</summary>
        public static HowToPlayDeck Create(BattleHud owner)
        {
            Canvas canvas = UiKit.Screen("How To Play", Theme.Layer.Deck);
            canvas.transform.SetParent(owner.transform, worldPositionStays: false);

            var deck = canvas.gameObject.AddComponent<HowToPlayDeck>();
            deck.hud = owner;
            deck.battle = owner.Battle;
            deck.Build(canvas.transform);
            deck.root.gameObject.SetActive(false);
            return deck;
        }

        /// <summary>Opens on the first page.</summary>
        public void Open()
        {
            if (IsOpen)
            {
                return;
            }

            restorePaused = battle.Paused;
            restoreBoardLock = battle.BoardInputLocked;
            restoreCameraLock = battle.CameraInputLocked;
            battle.SetPaused(true);
            battle.SetBoardInputLocked(true);
            battle.SetCameraInputLocked(true);

            page = 0;
            root.gameObject.SetActive(true);
            Render();
            UiSfx.Play(UiSfx.Cue.Open);
            CoroutineHost.Run(UiTween.Punch(card));
        }

        /// <summary>Closes, putting the battle back as it was.</summary>
        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            root.gameObject.SetActive(false);
            battle.SetPaused(restorePaused);
            battle.SetBoardInputLocked(restoreBoardLock);
            battle.SetCameraInputLocked(restoreCameraLock);
            UiSfx.Play(UiSfx.Cue.Close);
        }

        /// <summary>Moves by <paramref name="delta"/> pages, stopping at either end.</summary>
        public void Turn(int delta)
        {
            int target = Mathf.Clamp(page + delta, 0, Titles.Length - 1);
            if (target == page)
            {
                return;
            }

            page = target;
            Render();
            UiSfx.Play(UiSfx.Cue.Click);
        }

        /// <summary>Jumps to a page, for tests and screenshots.</summary>
        public void ShowPage(int index)
        {
            page = Mathf.Clamp(index, 0, Titles.Length - 1);
            Render();
        }

        private void ReplayTutorial()
        {
            Close();
            if (hud.Tutorial != null)
            {
                hud.Tutorial.Begin();
            }
        }

        private void LateUpdate()
        {
            // A language switch while open re-renders the page; nothing else changes under it.
            if (IsOpen && renderedVersion != Loc.Version)
            {
                Render();
            }
        }

        private void Render()
        {
            if (renderedPage != page || renderedVersion != Loc.Version)
            {
                pageTitle.text = Loc.Get(Titles[page]);
                pageBody.text = Loc.Get(Bodies[page]);
                renderedPage = page;
                renderedVersion = Loc.Version;
            }

            pageCounter.SetText("{0} / {1}", page + 1, Titles.Length);
            terrainStrip.gameObject.SetActive(page == TerrainPage);

            for (int i = 0; i < dots.Length; i++)
            {
                bool current = i == page;
                dots[i].color = current ? Theme.Revolution : new Color(Theme.InkSoft.r, Theme.InkSoft.g, Theme.InkSoft.b, 0.45f);
                dots[i].rectTransform.localScale = Vector3.one * (current ? 0.75f : 0.5f);
            }

            backButton.interactable = page > 0;
            nextButton.interactable = page < Titles.Length - 1;
        }

        // ------------------------------------------------------------------ build

        private void Build(Transform canvas)
        {
            root = UiKit.NewRect(canvas, "Deck");
            UiKit.Stretch(root);
            UiKit.Scrim(root);

            card = UiKit.Panel(root, "Card");
            UiKit.Anchor(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardWidth, CardHeight));

            RectTransform column = UiKit.Column(card, "Body", Theme.Space.Snug, Theme.Space.FramePadding + 8f, TextAnchor.UpperLeft);
            UiKit.Stretch(column);
            column.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = true;

            // Header: deck title on the left, page counter on the right.
            RectTransform header = UiKit.Row(column, "Header", Theme.Space.Snug, 0f, TextAnchor.MiddleLeft);
            Fix(header, 0f, 48f);
            UiKit.Sigil(header, 40f, Theme.Gold);
            TextMeshProUGUI deckTitle = UiKit.Display(header, Loc.Get(TextKey.DeckTitle), Theme.Type.Title, TextAlignmentOptions.Left);
            UiKit.Localize(deckTitle, TextKey.DeckTitle);
            OneLine(deckTitle, Theme.Type.Title);
            Flexible(deckTitle.rectTransform);
            pageCounter = UiKit.Caption(header, string.Empty, TextAlignmentOptions.Right);
            pageCounter.fontStyle = FontStyles.Bold;
            Fix(pageCounter.rectTransform, 80f, 48f);

            Image rule = UiKit.Divider(column);
            Fix(rule.rectTransform, 0f, 20f);

            pageTitle = UiKit.Display(column, string.Empty, Theme.Type.Heading, TextAlignmentOptions.Left);
            pageTitle.color = Theme.Revolution;
            OneLine(pageTitle, Theme.Type.Heading);
            Fix(pageTitle.rectTransform, 0f, 36f);

            terrainStrip = UiKit.Row(column, "Terrain", Theme.Space.Wide, 0f, TextAnchor.MiddleLeft);
            Fix(terrainStrip, 0f, TileSize + 26f);
            AddTile(terrainStrip, TerrainType.Trench, TextKey.TerrainTrench);
            AddTile(terrainStrip, TerrainType.EncampmentTent, TextKey.TerrainTent);
            AddTile(terrainStrip, TerrainType.CoastalShallows, TextKey.TerrainShallows);
            AddTile(terrainStrip, TerrainType.BambooBarricade, TextKey.TerrainBamboo);

            // Takes whatever height is left and shrinks its type to fit, so the longest Filipino
            // page stays inside the card instead of running under the buttons.
            pageBody = UiKit.Body(column, string.Empty, Theme.Type.Body + 2f, TextAlignmentOptions.TopLeft);
            pageBody.lineSpacing = 8f;
            pageBody.enableAutoSizing = true;
            pageBody.fontSizeMax = Theme.Type.Body + 2f;
            pageBody.fontSizeMin = Theme.Type.Small;
            pageBody.overflowMode = TextOverflowModes.Truncate;
            LayoutElement bodyElement = Element(pageBody.rectTransform);
            bodyElement.minHeight = 120f;
            bodyElement.preferredHeight = 120f;
            bodyElement.flexibleHeight = 1f;

            // Footer: replay on the left, page dots in the middle, navigation on the right.
            RectTransform footer = UiKit.Row(column, "Footer", Theme.Space.Snug, 0f, TextAnchor.MiddleLeft);
            Fix(footer, 0f, 60f);

            UiKit.SealButton(footer, TextKey.DeckReplay, ReplayTutorial, 250f, 52f, Theme.Type.Small, "Button Replay Tutorial");

            RectTransform dotRow = UiKit.Row(footer, "Dots", Theme.Space.Tight, 0f, TextAnchor.MiddleCenter);
            Flexible(dotRow);
            dots = new Image[Titles.Length];
            for (int i = 0; i < dots.Length; i++)
            {
                // A plain quad turned 45 degrees stays a crisp diamond at pager size; the soft glow
                // sprite fades out to a speck this small.
                dots[i] = UiKit.Icon(dotRow, null, 16f, Theme.InkSoft);
                dots[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }

            backButton = UiKit.SealButton(footer, TextKey.DeckBack, () => Turn(-1), 150f, 52f, Theme.Type.Small, "Button Back");
            nextButton = UiKit.SealButton(footer, TextKey.DeckNext, () => Turn(1), 150f, 52f, Theme.Type.Small, "Button Next");
            UiKit.SealButton(footer, TextKey.DeckClose, Close, 150f, 52f, Theme.Type.Small, "Button Close");
        }

        private static void AddTile(RectTransform strip, TerrainType terrain, TextKey name)
        {
            RectTransform cell = UiKit.Column(strip, "Tile " + terrain, Theme.Space.Hair, 0f, TextAnchor.UpperCenter);
            Fix(cell, 130f, TileSize + 26f);

            Image tile = UiKit.Icon(cell, BoardArt.Tile(terrain), TileSize, Color.white);
            Fix(tile.rectTransform, TileSize, TileSize);

            TextMeshProUGUI label = UiKit.Caption(cell, Loc.Get(name), TextAlignmentOptions.Center);
            label.fontStyle = FontStyles.Bold;
            UiKit.Localize(label, name);
            OneLine(label, Theme.Type.Small);
            Fix(label.rectTransform, 130f, 22f);
        }

        private static LayoutElement Element(RectTransform rect)
        {
            LayoutElement element = rect.GetComponent<LayoutElement>();
            return element != null ? element : rect.gameObject.AddComponent<LayoutElement>();
        }

        /// <summary>Fixes a height, and a width when non-zero.</summary>
        private static void Fix(RectTransform rect, float width, float height)
        {
            LayoutElement element = Element(rect);
            element.minHeight = height;
            element.preferredHeight = height;
            element.flexibleHeight = 0f;
            if (width > 0f)
            {
                element.minWidth = width;
                element.preferredWidth = width;
                element.flexibleWidth = 0f;
            }
        }

        private static void Flexible(RectTransform rect)
        {
            LayoutElement element = Element(rect);
            element.minWidth = 0f;
            element.preferredWidth = 0f;
            element.flexibleWidth = 1f;
        }

        private static void OneLine(TextMeshProUGUI label, float maxSize)
        {
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMax = maxSize;
            label.fontSizeMin = Mathf.Max(10f, maxSize * 0.6f);
            label.overflowMode = TextOverflowModes.Ellipsis;
        }
    }
}
