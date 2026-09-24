using System.Collections.Generic;
using BinakayanRising.Core.Content;
using BinakayanRising.Core.Localization;
using BinakayanRising.Core.Meta;
using BinakayanRising.Gameplay.Flow;
using BinakayanRising.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BinakayanRising.UI.Shell
{
    /// <summary>
    /// The Mission Tent: the campaign map of Cavite with the ten sub-quests on it, and the chosen
    /// quest's briefing beside it with the way into its battle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nodes sit at <see cref="Quest.MapX"/>/<see cref="Quest.MapY"/>, joined in campaign order.
    /// A cleared quest's sun is gold, the current one red and pulsing, a locked one faded ink.
    /// </para>
    /// <para>
    /// Only battle quests deploy from here. A hub-task quest lists its steps, which are done at
    /// the buildings in the camp (the navigation guide points at them).
    /// </para>
    /// </remarks>
    public sealed class MissionMapScreen : CampPanelScreen
    {
        private const float MapWidth = 820f;
        private const float MapHeight = 520f;
        private const float MapInset = 44f;
        private const float NodeSize = 54f;
        private const float PathWidth = 5f;
        private const float DetailWidth = 420f;
        private const int PaperWidth = 205;
        private const int PaperHeight = 135;

        private static Texture2D paper;

        private readonly List<Node> nodes = new List<Node>();
        private readonly List<Image> paths = new List<Image>();

        private Quest selected;
        private TextMeshProUGUI levelLabel;
        private TextMeshProUGUI questTitle;
        private TextMeshProUGUI tagLabel;
        private TextMeshProUGUI briefing;
        private TextMeshProUGUI facts;
        private TextMeshProUGUI status;
        private Button deploy;
        private TextMeshProUGUI deployLabel;
        private int renderedVersion = -1;
        private int renderedChange = -1;
        private bool flashPending;

        public override GameState State
        {
            get { return GameState.MissionPortal; }
        }

        /// <summary>The quest whose briefing is showing.</summary>
        public Quest Selected
        {
            get { return selected; }
        }

        /// <summary>The Deploy button, for the screenshot autopilot.</summary>
        public Button DeployButton
        {
            get { return deploy; }
        }

        protected override void BuildTabs(RectTransform row)
        {
        }

        protected override void BuildBody(RectTransform area)
        {
            RectTransform row = UiKit.Row(area, "Row", Theme.Space.Huge, 0f, TextAnchor.UpperLeft);
            UiKit.Stretch(row);

            BuildMap(row);
            BuildDetail(row);
        }

        private void BuildMap(RectTransform parent)
        {
            RectTransform well = UiKit.Well(parent, "Map");
            UiLayout.Fix(well, MapWidth, MapHeight);

            RectTransform canvasRect = UiKit.NewRect(well, "Paper");
            UiKit.Stretch(canvasRect, 6f);
            RawImage image = canvasRect.gameObject.AddComponent<RawImage>();
            image.texture = Paper();
            image.raycastTarget = false;

            IReadOnlyList<Quest> quests = Campaign.Quests;

            // The road first, so the suns draw over it.
            for (int i = 1; i < quests.Count; i++)
            {
                RectTransform segment = UiKit.NewRect(canvasRect, "Path " + i);
                Image line = segment.gameObject.AddComponent<Image>();
                line.raycastTarget = false;
                PlaceSegment(segment, NodePosition(quests[i - 1]), NodePosition(quests[i]));
                paths.Add(line);
            }

            for (int i = 0; i < quests.Count; i++)
            {
                nodes.Add(BuildNode(canvasRect, quests[i]));
            }
        }

        /// <summary>Where a quest's node sits inside the paper, from its centre.</summary>
        private static Vector2 NodePosition(Quest quest)
        {
            float width = MapWidth - 12f - (2f * MapInset);
            float height = MapHeight - 12f - (2f * MapInset);
            return new Vector2((quest.MapX - 0.5f) * width, (quest.MapY - 0.5f) * height);
        }

        private static void PlaceSegment(RectTransform segment, Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            segment.anchorMin = segment.anchorMax = new Vector2(0.5f, 0.5f);
            segment.pivot = new Vector2(0.5f, 0.5f);
            segment.sizeDelta = new Vector2(delta.magnitude, PathWidth);
            segment.anchoredPosition = (from + to) * 0.5f;
            segment.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        private Node BuildNode(RectTransform parent, Quest quest)
        {
            var node = new Node { Quest = quest };
            node.Root = UiKit.NewRect(parent, "Node " + quest.Id);
            UiKit.Anchor(node.Root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), NodePosition(quest), new Vector2(NodeSize + 20f, NodeSize + 20f));

            // A clear hit area a little larger than the sun.
            Image hit = node.Root.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            node.Button = node.Root.gameObject.AddComponent<Button>();
            node.Button.targetGraphic = hit;
            node.Button.onClick.AddListener(() => Select(quest, true));

            node.Ring = UiKit.Sigil(node.Root, NodeSize + 18f, Theme.GoldBright);
            UiKit.Anchor((RectTransform)node.Ring.transform.parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(NodeSize + 18f, NodeSize + 18f));

            node.Sun = UiKit.Sigil(node.Root, NodeSize, Theme.Gold);
            UiKit.Anchor((RectTransform)node.Sun.transform.parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(NodeSize, NodeSize));

            node.Number = UiKit.Display(node.Root, quest.Number.ToString(), Theme.Type.Heading, TextAlignmentOptions.Center);
            node.Number.color = Theme.Ink;
            node.Number.raycastTarget = false;
            UiKit.Anchor(node.Number.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(NodeSize, NodeSize));
            return node;
        }

        private void BuildDetail(RectTransform parent)
        {
            RectTransform column = UiKit.Column(parent, "Briefing", Theme.Space.Tight, 0f, TextAnchor.UpperLeft);
            UiLayout.Fix(column, DetailWidth, MapHeight);
            UiLayout.FillWidth(column);

            levelLabel = UiKit.Caption(column, string.Empty, TextAlignmentOptions.Left);
            levelLabel.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            UiLayout.OneLine(levelLabel, Theme.Type.Small);
            UiLayout.Fix(levelLabel.rectTransform, DetailWidth, 24f);

            questTitle = UiKit.Display(column, string.Empty, Theme.Type.Heading, TextAlignmentOptions.Left);
            questTitle.color = Theme.Revolution;
            UiLayout.OneLine(questTitle, Theme.Type.Heading);
            UiLayout.Fix(questTitle.rectTransform, DetailWidth, 36f);

            tagLabel = UiKit.Caption(column, string.Empty, TextAlignmentOptions.Left);
            tagLabel.fontStyle = FontStyles.Italic;
            UiLayout.OneLine(tagLabel, Theme.Type.Body);
            UiLayout.Fix(tagLabel.rectTransform, DetailWidth, 28f);

            briefing = UiKit.Body(column, string.Empty, Theme.Type.Body + 2f, TextAlignmentOptions.TopLeft);
            briefing.textWrappingMode = TextWrappingModes.Normal;
            UiLayout.Fix(briefing.rectTransform, DetailWidth, 90f);

            RectTransform factWell = UiKit.Well(column, "Facts");
            UiLayout.Fix(factWell, DetailWidth, 196f);
            facts = UiKit.Body(factWell, string.Empty, Theme.Type.Body, TextAlignmentOptions.TopLeft);
            facts.textWrappingMode = TextWrappingModes.Normal;
            facts.lineSpacing = 0f;
            UiKit.Stretch(facts.rectTransform, Theme.Space.Base);

            status = UiKit.Caption(column, string.Empty, TextAlignmentOptions.Left);
            status.fontStyle = FontStyles.Italic;
            status.textWrappingMode = TextWrappingModes.Normal;
            UiLayout.Fix(status.rectTransform, DetailWidth, 28f);

            deploy = UiKit.SealButton(column, Loc.Get(TextKey.MapDeploy), Deploy, DetailWidth, 60f, 0f, "Button Deploy");
            UiLayout.Fix((RectTransform)deploy.transform, DetailWidth, 60f);
            deployLabel = deploy.GetComponentInChildren<TextMeshProUGUI>();

            // A disabled Button swallows its click silently; say why when it is Rations.
            deploy.gameObject.AddComponent<DisabledPress>().Pressed = DeployRefused;
        }

        // ------------------------------------------------------------------ showing

        protected override void Refresh()
        {
            base.Refresh();
            MetaGame game = Game;
            Quest start = game != null ? game.CurrentQuest : null;
            if (start == null)
            {
                IReadOnlyList<Quest> quests = Campaign.Quests;
                start = quests[quests.Count - 1];
            }

            SelectTab(0);
            Select(start, false);
        }

        protected override void ShowTab(int index)
        {
            SetTitle(Loc.Get(TextKey.MapTitle));
        }

        private void Select(Quest quest, bool byClick)
        {
            if (quest == null)
            {
                return;
            }

            if (byClick)
            {
                UiSfx.Play(UiSfx.Cue.Click);
            }

            selected = quest;
            renderedVersion = -1;
            Redraw();

            if (RationsShort(Game, quest))
            {
                FlashRations();
            }
        }

        /// <summary>A battle the player could deploy to, except that they cannot pay for it.</summary>
        private static bool RationsShort(MetaGame game, Quest quest)
        {
            return game != null && quest != null && quest.Kind == QuestKind.Battle
                && game.IsUnlocked(quest) && game.Data.rations < quest.RationsCost;
        }

        /// <summary>
        /// Asks for the Rations chip to flash. Held until the screen is up: Refresh runs while
        /// the tent is still hidden, and a hidden bar can't run the flash.
        /// </summary>
        private void FlashRations()
        {
            flashPending = true;
        }

        private void DeployRefused()
        {
            if (RationsShort(Game, selected))
            {
                UiSfx.Play(UiSfx.Cue.Error);
                FlashRations();
            }
        }

        protected override void Update()
        {
            base.Update();
            if (!IsVisible || Game == null)
            {
                return;
            }

            if (renderedVersion != Loc.Version || renderedChange != Game.Data.rations)
            {
                Redraw();
            }

            if (flashPending && Bar != null && Bar.isActiveAndEnabled)
            {
                flashPending = false;
                Bar.FlashShort(Currency.Rations);
            }

            // The current quest's sun breathes, and the chosen one turns slowly.
            Quest current = Game.CurrentQuest;
            for (int i = 0; i < nodes.Count; i++)
            {
                Node node = nodes[i];
                float scale = node.Quest == current ? 1f + (0.07f * Mathf.Sin(Time.unscaledTime * 4f)) : 1f;
                node.Sun.transform.parent.localScale = Vector3.one * scale;
                node.Ring.transform.parent.localRotation = Quaternion.Euler(0f, 0f, -Time.unscaledTime * 20f);
            }
        }

        private void Redraw()
        {
            MetaGame game = Game;
            if (game == null || selected == null)
            {
                return;
            }

            renderedVersion = Loc.Version;
            renderedChange = game.Data.rations;
            SetTitle(Loc.Get(TextKey.MapTitle));

            Quest current = game.CurrentQuest;
            for (int i = 0; i < nodes.Count; i++)
            {
                Node node = nodes[i];
                bool cleared = game.IsCleared(node.Quest);
                bool open = game.IsUnlocked(node.Quest);
                node.Sun.color = cleared ? Theme.Gold : (node.Quest == current ? Theme.Revolution : Theme.InkSoft);
                Color faded = node.Sun.color;
                faded.a = open ? 1f : 0.45f;
                node.Sun.color = faded;
                node.Number.color = node.Quest == current ? Theme.Parchment : Theme.Ink;
                node.Ring.enabled = node.Quest == selected;
            }

            IReadOnlyList<Quest> quests = Campaign.Quests;
            for (int i = 0; i < paths.Count; i++)
            {
                paths[i].color = game.IsCleared(quests[i]) ? Theme.Revolution : new Color(Theme.InkSoft.r, Theme.InkSoft.g, Theme.InkSoft.b, 0.45f);
            }

            Quest quest = selected;
            CampaignLevel level = Campaign.Level(quest.Level);
            levelLabel.text = Loc.Format(TextKey.MapLevel, quest.Level, level != null ? level.Title.Get() : string.Empty);
            questTitle.text = quest.Number + ". " + quest.Title.Get();
            tagLabel.text = "[" + quest.Tag.Get() + "]  " + quest.Place.Get();
            briefing.text = quest.Briefing.Get();
            facts.text = Facts(game, quest);

            bool battleQuest = quest.Kind == QuestKind.Battle;
            deploy.gameObject.SetActive(battleQuest);
            deploy.interactable = game.CanLaunch(quest);
            deployLabel.text = Loc.Get(game.IsCleared(quest) ? TextKey.MapReplay : TextKey.MapDeploy);
            status.text = StatusOf(game, quest);
        }

        private static string Facts(MetaGame game, Quest quest)
        {
            var text = new System.Text.StringBuilder();
            if (quest.Kind == QuestKind.Battle)
            {
                QuestBattle rules = quest.Battle;
                text.Append("▸ ").Append(Loc.Format(TextKey.MapEnemies, rules.EnemyCount)).Append('\n');
                text.Append("▸ ").Append(Loc.Format(TextKey.MapSquad, rules.SquadCap)).Append('\n');
                text.Append("▸ ").Append(rules.WinRule == WinRule.Hold
                    ? Loc.Format(TextKey.MapWinHold, rules.TurnCap)
                    : Loc.Get(TextKey.MapWinRout)).Append('\n');
                text.Append("▸ ").Append(Loc.Format(TextKey.MapCost, quest.RationsCost)).Append('\n');
            }
            else
            {
                text.Append(Loc.Get(TextKey.MapInCamp)).Append('\n');
                for (int i = 0; i < quest.Tasks.Length; i++)
                {
                    HubTask task = Campaign.Task(quest.Tasks[i]);
                    if (task != null)
                    {
                        text.Append(game.IsTaskDone(quest, task.Id) ? "✓ " : "▸ ").Append(task.Text.Get()).Append('\n');
                    }
                }
            }

            text.Append("▸ ").Append(Loc.Format(TextKey.MapReward, quest.RewardReales));
            if (!string.IsNullOrEmpty(quest.RewardLesson))
            {
                text.Append('\n').Append("▸ ").Append(Loc.Get(TextKey.MapRewardLesson));
            }

            return text.ToString();
        }

        private static string StatusOf(MetaGame game, Quest quest)
        {
            if (!game.IsUnlocked(quest))
            {
                return Loc.Get(TextKey.MapLocked);
            }

            if (game.CampaignComplete)
            {
                return Loc.Get(TextKey.MapComplete);
            }

            if (game.IsCleared(quest))
            {
                return "✓ " + Loc.Get(TextKey.MapCleared);
            }

            if (quest.Kind == QuestKind.Battle && game.Data.rations < quest.RationsCost)
            {
                return Loc.Get(TextKey.MapNoRations);
            }

            return string.Empty;
        }

        private void Deploy()
        {
            if (selected != null)
            {
                Shell.LaunchQuest(selected);
            }
        }

        // ------------------------------------------------------------------ the paper

        /// <summary>
        /// The map's paper: the coast of Cavite on Manila Bay, painted once in the pixel style of
        /// the board and the slides.
        /// </summary>
        private static Texture2D Paper()
        {
            if (paper != null)
            {
                return paper;
            }

            var pixels = new Color32[PaperWidth * PaperHeight];
            Color32 land = new Color32(0xD9, 0xC8, 0x9C, 0xFF);
            Color32 landDark = new Color32(0xC8, 0xB4, 0x84, 0xFF);
            Color32 field = new Color32(0xB8, 0xB0, 0x74, 0xFF);
            Color32 sea = new Color32(0x8C, 0xA4, 0xA0, 0xFF);
            Color32 seaLight = new Color32(0x9E, 0xB4, 0xAC, 0xFF);
            Color32 shore = new Color32(0xE8, 0xDA, 0xB0, 0xFF);
            Color32 tree = new Color32(0x6E, 0x7A, 0x48, 0xFF);

            for (int y = 0; y < PaperHeight; y++)
            {
                for (int x = 0; x < PaperWidth; x++)
                {
                    // The bay fills the top and left; the coast wanders down and across.
                    float coast = (PaperHeight * 0.62f) - (x * 0.28f)
                        + (6f * Mathf.Sin(x * 0.09f)) + (3f * Mathf.Sin(x * 0.31f + 1.3f));
                    float noise = Mathf.PerlinNoise(x * 0.08f, y * 0.08f);
                    Color32 c;
                    if (y > coast + 3f)
                    {
                        c = ((x + (y * 3)) % 11 == 0 || noise > 0.7f) ? seaLight : sea;
                    }
                    else if (y > coast)
                    {
                        c = shore;
                    }
                    else if (noise > 0.66f && ((x * 7) + (y * 13)) % 5 == 0)
                    {
                        c = tree;
                    }
                    else if (noise < 0.3f && y % 3 == 0)
                    {
                        c = field;
                    }
                    else
                    {
                        c = noise > 0.5f ? landDark : land;
                    }

                    pixels[(y * PaperWidth) + x] = c;
                }
            }

            paper = new Texture2D(PaperWidth, PaperHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Mission Map Paper"
            };
            paper.SetPixels32(pixels);
            paper.Apply(false, true);
            return paper;
        }

        private sealed class Node
        {
            public Quest Quest;
            public RectTransform Root;
            public Button Button;
            public Image Sun;
            public Image Ring;
            public TextMeshProUGUI Number;
        }
    }

    /// <summary>
    /// Reports a press on a <see cref="Button"/> that is not interactable, which the button
    /// itself ignores.
    /// </summary>
    internal sealed class DisabledPress : MonoBehaviour, IPointerClickHandler
    {
        public System.Action Pressed;

        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (button != null && !button.interactable && eventData.button == PointerEventData.InputButton.Left && Pressed != null)
            {
                Pressed();
            }
        }
    }
}
