using System;
using System.Collections;
using System.Collections.Generic;
using BinakayanRising.Core.Content;
using BinakayanRising.Core.Localization;
using BinakayanRising.Core.Meta;
using BinakayanRising.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BinakayanRising.UI.Shell
{
    /// <summary>
    /// Shows what a recruit brought: a face-down card per recruit, turned over one after another,
    /// each framed in its tier's colour.
    /// </summary>
    /// <remarks>
    /// The reveal only presents a result already decided and saved by
    /// <see cref="MetaGame.TryPull"/>; closing it early loses nothing. The first press of the
    /// button turns every card still face down; the next one closes.
    /// </remarks>
    public sealed class RecruitReveal : MonoBehaviour
    {
        private const float TileWidth = 200f;
        private const float TileHeight = 280f;
        private const int PerRow = 5;

        /// <summary>A lone recruit is shown half again as large: three screen pixels per art pixel.</summary>
        private const float SingleScale = 1.5f;

        /// <summary>Bodies are 48x64 art pixels, drawn at two screen pixels each on a tile.</summary>
        private const float BodyWidth = 96f;
        private const float BodyHeight = 128f;
        private const float BodyTop = 22f;

        /// <summary>A Hero's turning sun: its rays reach the edge of a circle this wide, which clears the card's top.</summary>
        private const float GlowSize = 160f;

        private const float TitleHeight = 56f;
        private const float ButtonHeight = 64f;
        private const float ButtonBottom = 72f;

        private const float FirstDelay = 0.35f;
        private const float Step = 0.28f;
        private const float HalfTurn = 0.11f;
        private const float GlowTurnDegreesPerSecond = 20f;

        private readonly List<Tile> tiles = new List<Tile>();
        private Action onClosed;
        private Button button;
        private TextMeshProUGUI buttonLabel;
        private int openedFrame;
        private bool allShown;

        /// <summary>The reveal showing now, or null.</summary>
        public static RecruitReveal Current { get; private set; }

        /// <summary>True once every card is face up.</summary>
        public bool AllShown
        {
            get { return allShown; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = null;
        }

        private sealed class Tile
        {
            public RectTransform Root;
            public GameObject Back;
            public GameObject Front;
            public RectTransform Glow;
            public PullResult Result;
            public bool Shown;
        }

        public static RecruitReveal Show(IList<PullResult> results, Action closed)
        {
            Canvas canvas = UiKit.Screen("Recruit Reveal", Theme.Layer.Modal);
            UiKit.EnsureEventSystem();
            var view = canvas.gameObject.AddComponent<RecruitReveal>();
            view.onClosed = closed;
            view.Build(canvas.transform, results);
            Current = view;
            view.StartCoroutine(view.Play());
            return view;
        }

        private void Build(Transform root, IList<PullResult> results)
        {
            openedFrame = Time.frameCount;
            // Its own screen, on the dusk backdrop. The tiles float without a card behind them,
            // and through any scrim the panel underneath still read between them and under the
            // title (UI blends in linear space, so even a dark scrim lets a light card show).
            UiKit.Scrim(root).color = Theme.Backdrop;

            // The title and the tiles are one block, centred in the band above the button, with
            // as much room over it as the button has under it.
            RectTransform band = UiKit.NewRect(root, "Band");
            UiKit.Stretch(band);
            band.offsetMin = new Vector2(0f, ButtonBottom + ButtonHeight);
            band.offsetMax = new Vector2(0f, -ButtonBottom);

            int rows = (results.Count + PerRow - 1) / PerRow;
            float gap = Theme.Space.Base;
            float scale = results.Count == 1 ? SingleScale : 1f;
            float gridHeight = ((rows * TileHeight) + ((rows - 1) * gap)) * scale;
            float blockTop = (TitleHeight + gap + gridHeight) * 0.5f;
            float gridTop = blockTop - TitleHeight - gap;

            TextMeshProUGUI title = UiKit.Display(band, Loc.Get(TextKey.RecResults), Theme.Type.Title);
            title.color = Theme.GoldBright;
            UiKit.Localize(title, TextKey.RecResults);
            UiKit.Anchor(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, blockTop), new Vector2(900f, TitleHeight));
            UiLayout.OneLine(title, Theme.Type.Title);

            for (int i = 0; i < results.Count; i++)
            {
                int row = i / PerRow;
                int inRow = Mathf.Min(PerRow, results.Count - (row * PerRow));
                int column = i % PerRow;
                float rowWidth = (inRow * TileWidth) + ((inRow - 1) * gap);
                float x = (-(rowWidth * 0.5f) + (TileWidth * 0.5f) + (column * (TileWidth + gap))) * scale;
                float y = gridTop - (((TileHeight * 0.5f) + (row * (TileHeight + gap))) * scale);

                Tile tile = BuildTile(band, results[i], i);
                UiKit.Anchor(tile.Root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(TileWidth, TileHeight));
                tile.Root.localScale = new Vector3(scale, scale, 1f);
                tiles.Add(tile);
            }

            button = UiKit.SealButton(root, Loc.Get(TextKey.RecRevealAll), Press, 300f, ButtonHeight, 0f, "Button Reveal");
            UiKit.Anchor((RectTransform)button.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, ButtonBottom), new Vector2(300f, ButtonHeight));
            buttonLabel = button.GetComponentInChildren<TextMeshProUGUI>();
        }

        private Tile BuildTile(Transform parent, PullResult result, int index)
        {
            var tile = new Tile { Result = result };
            tile.Root = UiKit.NewRect(parent, "Recruit " + index);

            // Face down: the seal's red with the sun on it.
            RectTransform back = UiKit.Panel(tile.Root, "Back");
            UiKit.Stretch(back);
            back.Find("Fill").GetComponent<Image>().color = Theme.Revolution;
            Image sun = UiKit.Sigil(back, 120f, Theme.GoldBright);
            UiKit.Anchor((RectTransform)sun.transform.parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 120f));
            tile.Back = back.gameObject;

            // Face up: the recruit, framed in the tier's colour.
            UnitArchetype archetype = UnitCatalog.Find(result.Archetype);
            Color tier = Theme.Rarity.Of(result.Rarity);
            RectTransform front = UiKit.Panel(tile.Root, "Front");
            UiKit.Stretch(front);
            RectTransform rim = UiKit.Frame(front, "Rim", tier);
            UiKit.Stretch(rim, -Theme.Space.Hair);

            if (result.Rarity == UnitRarity.Hero)
            {
                // Centred on the figure and turned about its own middle, so it stays inside the card.
                Image glow = UiKit.Sigil(front, GlowSize, Theme.GoldBright);
                tile.Glow = (RectTransform)glow.transform.parent;
                UiKit.Anchor(tile.Glow, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -(BodyTop + (BodyHeight * 0.5f))), new Vector2(GlowSize, GlowSize));
                Color soft = Theme.GoldBright;
                soft.a = 0.5f;
                glow.color = soft;
            }

            Sprite figure = Theme.Assets != null ? Theme.Assets.UnitBody(result.Archetype) : null;
            Image body = UiKit.Icon(front, figure, BodyWidth, Color.white);
            body.enabled = figure != null;
            UiKit.Anchor((RectTransform)body.transform.parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -BodyTop), new Vector2(BodyWidth, BodyHeight));

            TextMeshProUGUI name = UiKit.Body(front, archetype != null ? archetype.Name.Get() : result.Archetype, Theme.Type.Body, TextAlignmentOptions.Center);
            name.fontStyle = FontStyles.Bold;
            name.textWrappingMode = TextWrappingModes.Normal;
            name.enableAutoSizing = true;
            name.fontSizeMin = Theme.Type.Small;
            name.fontSizeMax = Theme.Type.Body;
            UiKit.Anchor(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -156f), new Vector2(TileWidth - 24f, 48f));

            TextMeshProUGUI rarity = UiKit.Caption(front, UnitCatalog.RarityLabel(result.Rarity).Get(), TextAlignmentOptions.Center);
            rarity.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            rarity.color = tier;
            UiLayout.OneLine(rarity, Theme.Type.Small);
            UiKit.Anchor(rarity.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 42f), new Vector2(TileWidth - 24f, 22f));

            TextMeshProUGUI note = UiKit.Caption(front, NoteFor(result), TextAlignmentOptions.Center);
            note.fontStyle = FontStyles.Bold;
            note.color = result.NewUnit ? Theme.Success : Theme.Revolution;
            UiLayout.OneLine(note, Theme.Type.Small);
            UiKit.Anchor(note.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(TileWidth - 24f, 22f));

            tile.Front = front.gameObject;
            tile.Front.SetActive(false);

            Button turn = tile.Root.gameObject.AddComponent<Button>();
            turn.transition = Selectable.Transition.None;
            Image hit = tile.Root.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            turn.targetGraphic = hit;
            turn.onClick.AddListener(() => Reveal(tile, true));
            return tile;
        }

        private static string NoteFor(PullResult result)
        {
            string note = result.NewUnit ? Loc.Get(TextKey.RecNew) : Loc.Format(TextKey.RecDupXp, result.DuplicateXp);
            return result.FromPity ? note + " · " + Loc.Get(TextKey.RecGuaranteed) : note;
        }

        // ------------------------------------------------------------------ turning

        private IEnumerator Play()
        {
            yield return new WaitForSecondsRealtime(FirstDelay);
            for (int i = 0; i < tiles.Count; i++)
            {
                if (!tiles[i].Shown)
                {
                    Reveal(tiles[i], true);
                    yield return new WaitForSecondsRealtime(tiles[i].Result.Rarity == UnitRarity.Hero ? Step * 2f : Step);
                }
            }
        }

        private void Reveal(Tile tile, bool animate)
        {
            if (tile.Shown)
            {
                return;
            }

            tile.Shown = true;
            if (animate)
            {
                StartCoroutine(Turn(tile));
            }
            else
            {
                tile.Back.SetActive(false);
                tile.Front.SetActive(true);
            }

            CheckAllShown();
        }

        private IEnumerator Turn(Tile tile)
        {
            Vector3 rest = tile.Root.localScale;
            yield return Squash(tile.Root, rest, 1f, 0f);
            tile.Back.SetActive(false);
            tile.Front.SetActive(true);
            UiSfx.Play(tile.Result.Rarity == UnitRarity.Hero ? UiSfx.Cue.Victory
                : tile.Result.Rarity == UnitRarity.Rare ? UiSfx.Cue.Confirm : UiSfx.Cue.Place);
            yield return Squash(tile.Root, rest, 0f, 1f);
            if (tile.Result.Rarity != UnitRarity.Common)
            {
                yield return UiTween.Punch(tile.Root, tile.Result.Rarity == UnitRarity.Hero ? 0.16f : 0.08f, 0.24f);
            }
        }

        private static IEnumerator Squash(RectTransform target, Vector3 rest, float from, float to)
        {
            float elapsed = 0f;
            while (elapsed < HalfTurn && target != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / HalfTurn);
                target.localScale = new Vector3(rest.x * Mathf.Lerp(from, to, t), rest.y, 1f);
                yield return null;
            }

            if (target != null)
            {
                target.localScale = new Vector3(rest.x * to, rest.y, 1f);
            }
        }

        private void CheckAllShown()
        {
            for (int i = 0; i < tiles.Count; i++)
            {
                if (!tiles[i].Shown)
                {
                    return;
                }
            }

            allShown = true;
            buttonLabel.text = Loc.Get(TextKey.MenuContinue);
            button.name = "Button Continue";
        }

        /// <summary>Turns every card still face down; once all are up, closes.</summary>
        public void Press()
        {
            if (!allShown)
            {
                StopAllCoroutines();
                for (int i = 0; i < tiles.Count; i++)
                {
                    // Finish any card caught mid-turn, too.
                    tiles[i].Shown = true;
                    tiles[i].Back.SetActive(false);
                    tiles[i].Front.SetActive(true);
                    float scale = tiles.Count == 1 ? SingleScale : 1f;
                    tiles[i].Root.localScale = new Vector3(scale, scale, 1f);
                }

                UiSfx.Play(UiSfx.Cue.Place);
                CheckAllShown();
                return;
            }

            UiSfx.Play(UiSfx.Cue.Click);
            Current = null;
            Action closed = onClosed;
            onClosed = null;
            Destroy(gameObject);
            if (closed != null)
            {
                closed();
            }
        }

        private void Update()
        {
            for (int i = 0; i < tiles.Count; i++)
            {
                if (tiles[i].Glow != null)
                {
                    tiles[i].Glow.localRotation = Quaternion.Euler(0f, 0f, -Time.unscaledTime * GlowTurnDegreesPerSecond);
                }
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && Time.frameCount != openedFrame
                && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
            {
                Press();
            }
        }

        private void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }
        }
    }
}
