using System;
using BinakayanRising.Core.Content;
using BinakayanRising.Core.Localization;
using BinakayanRising.Core.Meta;
using BinakayanRising.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BinakayanRising.UI.Shell
{
    /// <summary>
    /// Plays one Kapatiran lore dialogue (#20) through the ordinary <see cref="DialoguePanel"/>,
    /// over a dimmed screen with the dialogue's title and setting above it.
    /// </summary>
    /// <remarks>
    /// Sits one step over the other modals, so it can open from the bond rank card or from the
    /// Lore list and come back to either. Finishing or skipping it records it as heard.
    /// </remarks>
    public sealed class LorePlayer : MonoBehaviour
    {
        private LoreDialogue lore;
        private MetaGame game;
        private Action onClosed;
        private TextMeshProUGUI title;
        private TextMeshProUGUI setting;
        private int renderedVersion = -1;

        /// <summary>The dialogue playing now, or null. For screenshots and tests.</summary>
        public static LorePlayer Current { get; private set; }

        /// <summary>The dialogue box, for screenshots and tests.</summary>
        public DialoguePanel Panel { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = null;
        }

        /// <summary>
        /// Plays <paramref name="dialogue"/>, marks it heard in <paramref name="meta"/>, then calls
        /// <paramref name="closed"/>.
        /// </summary>
        public static LorePlayer Play(LoreDialogue dialogue, MetaGame meta, Action closed)
        {
            if (dialogue == null || Current != null)
            {
                if (closed != null)
                {
                    closed();
                }

                return null;
            }

            Canvas canvas = UiKit.Screen("Kapatiran Lore", Theme.Layer.Modal + 1);
            UiKit.EnsureEventSystem();
            var view = canvas.gameObject.AddComponent<LorePlayer>();
            view.lore = dialogue;
            view.game = meta;
            view.onClosed = closed;
            view.Build((RectTransform)canvas.transform);
            Current = view;
            view.Panel.Play(dialogue.Lines, view.Close);
            return view;
        }

        private void Build(RectTransform root)
        {
            UiKit.Scrim(root);

            RectTransform banner = UiKit.Panel(root, "Banner");
            UiKit.Anchor(banner, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -Theme.Space.Loose), new Vector2(DialoguePanel.Width, 132f));
            RectTransform column = UiKit.Column(banner, "Column", Theme.Space.Hair, Theme.Space.FramePadding, TextAnchor.MiddleCenter);
            UiKit.Stretch(column);
            UiLayout.FillWidth(column);

            TextMeshProUGUI kicker = UiKit.Caption(column, Loc.Get(TextKey.LoreTitle), TextAlignmentOptions.Center);
            kicker.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            UiKit.Localize(kicker, TextKey.LoreTitle);
            UiLayout.OneLine(kicker, Theme.Type.Small);
            UiLayout.Fix(kicker.rectTransform, 0f, 22f);

            title = UiKit.Display(column, string.Empty, Theme.Type.Title, TextAlignmentOptions.Center);
            title.color = Theme.Revolution;
            UiLayout.OneLine(title, Theme.Type.Title);
            UiLayout.Fix(title.rectTransform, 0f, 46f);

            setting = UiKit.Caption(column, string.Empty, TextAlignmentOptions.Center);
            setting.fontStyle = FontStyles.Italic;
            UiLayout.OneLine(setting, Theme.Type.Body);
            UiLayout.Fix(setting.rectTransform, 0f, 26f);

            Panel = DialoguePanel.Create(root);
            Render();
        }

        private void Render()
        {
            title.text = lore.Number + ".  " + lore.Title.Get();
            setting.text = lore.Setting.Get();
            renderedVersion = Loc.Version;
        }

        private void Close()
        {
            if (game != null)
            {
                game.MarkLoreHeard(lore.BondId);
            }

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
            if (renderedVersion != Loc.Version)
            {
                Render();
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
