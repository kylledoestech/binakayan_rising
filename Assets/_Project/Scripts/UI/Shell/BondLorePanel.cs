using System;
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
    /// The Lore list (#20): the four Kapatiran lore dialogues in one place, each re-playable once
    /// its pair has reached rank C, and each still-locked one saying how to open it.
    /// </summary>
    /// <remarks>
    /// Opened from the Training Grounds, where the soldiers and their bond line already are. A row
    /// shows the pair, its rank and support toward the next rank, and marks a dialogue the player
    /// has opened but not yet heard.
    /// </remarks>
    public sealed class BondLorePanel : MonoBehaviour
    {
        private const float CardWidth = 1120f;
        private const float CardHeight = 760f;
        private const float RowHeight = 118f;
        private const float PortraitSize = 72f;

        private readonly List<Button> listens = new List<Button>();
        private readonly List<TextMeshProUGUI> titles = new List<TextMeshProUGUI>();
        private readonly List<TextMeshProUGUI> details = new List<TextMeshProUGUI>();
        private readonly List<TextMeshProUGUI> tags = new List<TextMeshProUGUI>();
        private readonly List<Image> rims = new List<Image>();

        private MetaGame game;
        private Action onClosed;
        private RectTransform card;
        private CanvasGroup cardGroup;
        private int openedFrame;
        private int renderedVersion = -1;
        private bool stale = true;

        /// <summary>The list open now, or null. For screenshots and tests.</summary>
        public static BondLorePanel Current { get; private set; }

        /// <summary>The card's frame, for layout checks.</summary>
        public RectTransform Card
        {
            get { return card; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = null;
        }

        /// <summary>Opens the list for <paramref name="meta"/>; <paramref name="closed"/> runs on Close.</summary>
        public static BondLorePanel Show(MetaGame meta, Action closed)
        {
            if (meta == null || Current != null)
            {
                return Current;
            }

            Canvas canvas = UiKit.Screen("Lore List", Theme.Layer.Modal);
            UiKit.EnsureEventSystem();
            var view = canvas.gameObject.AddComponent<BondLorePanel>();
            view.game = meta;
            view.onClosed = closed;
            view.openedFrame = Time.frameCount;
            view.Build(canvas.transform);
            meta.Changed += view.MarkStale;
            Current = view;
            view.Render();
            view.StartCoroutine(UiTween.Enter(view.card, view.cardGroup, new Vector2(0f, -24f), UiTween.PanelDuration));
            UiSfx.Play(UiSfx.Cue.Open);
            return view;
        }

        private void Build(Transform root)
        {
            UiKit.Scrim(root);

            card = UiKit.Panel(root, "Card");
            UiKit.Anchor(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardWidth, CardHeight));
            cardGroup = UiKit.Group(card.gameObject);

            RectTransform column = UiKit.Column(card, "Column", Theme.Space.Snug, Theme.Space.FramePadding + 8f, TextAnchor.UpperLeft);
            UiKit.Stretch(column);
            UiLayout.FillWidth(column);

            RectTransform header = UiKit.Row(column, "Header", Theme.Space.Base, 0f, TextAnchor.MiddleLeft);
            UiLayout.Fix(header, 0f, 48f);
            Image sigil = UiKit.Sigil(header, 40f, Theme.Gold);
            UiLayout.Fix((RectTransform)sigil.transform.parent, 40f, 40f);
            TextMeshProUGUI title = UiKit.Display(header, Loc.Get(TextKey.LoreTitle), Theme.Type.Title, TextAlignmentOptions.Left);
            title.color = Theme.Revolution;
            UiKit.Localize(title, TextKey.LoreTitle);
            UiLayout.OneLine(title, Theme.Type.Title);
            UiLayout.Flexible(title.rectTransform);
            UiLayout.Fix(title.rectTransform, 0f, 48f);

            Image rule = UiKit.Divider(column);
            UiLayout.Fix(rule.rectTransform, 0f, 16f);

            IReadOnlyList<LoreDialogue> all = BondLore.All;
            for (int i = 0; i < all.Count; i++)
            {
                BuildRow(column, all[i], i);
            }

            RectTransform footer = UiKit.Row(column, "Footer", 0f, 0f, TextAnchor.MiddleCenter);
            UiLayout.Fix(footer, 0f, 68f);
            UiKit.SealButton(footer, TextKey.LoreClose, Close, 260f, 60f, 0f, "Button Close");
        }

        private void BuildRow(RectTransform column, LoreDialogue lore, int index)
        {
            RectTransform row = UiKit.Well(column, "Lore " + lore.Number);
            UiLayout.Fix(row, 0f, RowHeight);
            RectTransform rim = UiKit.Frame(row, "Rim", Theme.ParchmentDeep);
            UiKit.Stretch(rim);
            rims.Add(rim.GetComponentInChildren<Image>());

            RectTransform content = UiKit.Row(row, "Content", Theme.Space.Base, 0f, TextAnchor.MiddleLeft);
            UiKit.Stretch(content, Theme.Space.Snug);

            BondPair pair = BondCatalog.Find(lore.BondId);
            AddFace(content, pair != null ? pair.ArchetypeA : null);
            AddFace(content, pair != null ? pair.ArchetypeB : null);

            RectTransform words = UiKit.Column(content, "Words", Theme.Space.Hair, 0f, TextAnchor.MiddleLeft);
            UiLayout.Flexible(words);
            UiLayout.Fix(words, 0f, RowHeight - 24f);
            UiLayout.FillWidth(words);

            RectTransform titleRow = UiKit.Row(words, "Title", Theme.Space.Tight, 0f, TextAnchor.MiddleLeft);
            UiLayout.Fix(titleRow, 0f, 34f);
            TextMeshProUGUI name = UiKit.Body(titleRow, string.Empty, Theme.Type.Heading, TextAlignmentOptions.Left);
            name.fontStyle = FontStyles.Bold;
            UiLayout.OneLine(name, Theme.Type.Heading);
            UiLayout.Flexible(name.rectTransform);
            UiLayout.Fix(name.rectTransform, 0f, 34f);
            titles.Add(name);

            TextMeshProUGUI tag = UiKit.Caption(titleRow, string.Empty, TextAlignmentOptions.Right);
            tag.fontStyle = FontStyles.Bold;
            tag.color = Theme.Revolution;
            UiLayout.OneLine(tag, Theme.Type.Small);
            UiLayout.Fix(tag.rectTransform, 90f, 34f);
            tags.Add(tag);

            TextMeshProUGUI detail = UiKit.Caption(words, string.Empty, TextAlignmentOptions.Left);
            // Two lines when it needs them: the Filipino pair names and lock hint run long.
            detail.textWrappingMode = TextWrappingModes.Normal;
            detail.enableAutoSizing = true;
            detail.fontSizeMax = Theme.Type.Body;
            detail.fontSizeMin = Theme.Type.Small;
            detail.overflowMode = TextOverflowModes.Ellipsis;
            UiLayout.Fix(detail.rectTransform, 0f, 54f);
            details.Add(detail);

            int which = index;
            Button play = UiKit.SealButton(content, TextKey.BondListen, () => Listen(which), 200f, 56f, 0f, "Button Listen " + lore.Number);
            UiLayout.Fix((RectTransform)play.transform, 200f, 56f);
            listens.Add(play);
        }

        private static void AddFace(RectTransform parent, string archetypeId)
        {
            RectTransform frame = UiKit.Well(parent, "Portrait");
            UiLayout.Fix(frame, PortraitSize + 8f, PortraitSize + 8f);
            Sprite sprite = Theme.Assets != null && archetypeId != null ? Theme.Assets.UnitPortrait(archetypeId) : null;
            Image face = UiKit.Icon(frame, sprite, PortraitSize, Color.white);
            UiKit.Anchor((RectTransform)face.transform.parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PortraitSize, PortraitSize));
            face.enabled = sprite != null;
        }

        private void MarkStale()
        {
            stale = true;
        }

        private void Render()
        {
            IReadOnlyList<LoreDialogue> all = BondLore.All;
            for (int i = 0; i < all.Count; i++)
            {
                LoreDialogue lore = all[i];
                BondPair pair = BondCatalog.Find(lore.BondId);
                BondRank rank = game.BondRankOf(lore.BondId);
                bool open = game.IsLoreUnlocked(lore.BondId);
                bool heard = game.IsLoreHeard(lore.BondId);

                titles[i].text = lore.Number + ".  " + (open ? lore.Title.Get() : "? ? ?");
                titles[i].color = open ? Theme.Ink : Theme.InkSoft;
                tags[i].text = open && !heard ? Loc.Get(TextKey.LoreNew) : string.Empty;

                string pairName = pair != null ? KapatiranText.PairName(pair) : lore.BondId;
                int next = game.BondSupportForNext(lore.BondId);
                string progress = next <= 0
                    ? BondCatalog.Label(rank)
                    : rank == BondRank.None
                        ? Loc.Format(TextKey.LoreSupportUnranked, game.BondSupport(lore.BondId), next)
                        : Loc.Format(TextKey.LoreSupport, BondCatalog.Label(rank), game.BondSupport(lore.BondId), next);
                details[i].text = open
                    ? pairName + "  ·  " + lore.Setting.Get() + "  ·  " + progress
                    : pairName + "  ·  " + Loc.Get(TextKey.LoreLocked) + "  ·  " + progress;

                listens[i].interactable = open;
                rims[i].color = open && !heard ? Theme.Gold : Theme.ParchmentDeep;
            }

            renderedVersion = Loc.Version;
            stale = false;
        }

        /// <summary>Plays dialogue <paramref name="index"/> in Table 3's order, if it is open.</summary>
        public void Listen(int index)
        {
            IReadOnlyList<LoreDialogue> all = BondLore.All;
            if (index < 0 || index >= all.Count || !game.IsLoreUnlocked(all[index].BondId) || LorePlayer.Current != null)
            {
                return;
            }

            UiSfx.Play(UiSfx.Cue.Click);
            LorePlayer.Play(all[index], game, () => openedFrame = Time.frameCount);
        }

        /// <summary>Closes the list.</summary>
        public void Close()
        {
            if (LorePlayer.Current != null)
            {
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
            if (stale || renderedVersion != Loc.Version)
            {
                Render();
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && LorePlayer.Current == null && Time.frameCount != openedFrame
                && keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
        }

        private void OnDestroy()
        {
            if (game != null)
            {
                game.Changed -= MarkStale;
            }

            if (Current == this)
            {
                Current = null;
            }
        }
    }
}
