using BinakayanRising.Core.Content;
using BinakayanRising.Core.Localization;
using BinakayanRising.Core.Meta;
using BinakayanRising.Gameplay.Flow;
using BinakayanRising.UI.Camp;
using BinakayanRising.UI.Kit;
using TMPro;
using UnityEngine;

namespace BinakayanRising.UI.Shell
{
    /// <summary>
    /// The encampment hub: the resting state between missions, and where every facility is
    /// reached from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The camp itself is a world drawn by <see cref="CampWorld"/> under this screen; the screen
    /// adds what sits over it: the top bar with the objective, a name plate over whatever the
    /// pointer is on, and the speech box for the aide and the keepers.
    /// </para>
    /// <para>
    /// The navigation guide is the objective line in the bar plus the gold arrow the camp draws
    /// over the place that objective names. Both follow <see cref="MetaGame.Changed"/>, so
    /// finishing a task moves the arrow on at once.
    /// </para>
    /// </remarks>
    public sealed class EncampmentScreen : ShellScreen
    {
        private const float PlateWidth = 420f;
        private const float PlateHeight = 78f;

        private CampaignBar bar;
        private DialoguePanel dialogue;
        private RectTransform plate;
        private TextMeshProUGUI plateName;
        private TextMeshProUGUI plateNote;
        private MetaGame bound;
        private bool pointerStale = true;

        public override GameState State
        {
            get { return GameState.BaseHub; }
        }

        /// <summary>The top bar, for the navigation guide and screenshots.</summary>
        public CampaignBar Bar
        {
            get { return bar; }
        }

        /// <summary>The speech box, for screenshots and tests.</summary>
        public DialoguePanel Dialogue
        {
            get { return dialogue; }
        }

        /// <summary>The hover name plate, for screenshots and layout checks.</summary>
        public RectTransform Plate
        {
            get { return plate; }
        }

        private CampWorld World
        {
            get { return Shell.Camp; }
        }

        protected override void Build()
        {
            // Plate first, so the bar and the speech box draw over it.
            plate = UiKit.Well(Root, "Name Plate");
            UiKit.Anchor(plate, Vector2.zero, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(PlateWidth, PlateHeight));
            RectTransform plateColumn = UiKit.Column(plate, "Text", 0f, 0f, TextAnchor.MiddleCenter);
            UiKit.Stretch(plateColumn, Theme.Space.Tight);
            UiLayout.FillWidth(plateColumn);

            plateName = UiKit.Body(plateColumn, string.Empty, Theme.Type.Heading, TextAlignmentOptions.Center);
            plateName.fontStyle = FontStyles.Bold;
            plateName.color = Theme.Revolution;
            UiLayout.OneLine(plateName, Theme.Type.Heading);
            UiLayout.Fix(plateName.rectTransform, 0f, 32f);

            plateNote = UiKit.Caption(plateColumn, string.Empty, TextAlignmentOptions.Center);
            UiLayout.OneLine(plateNote, Theme.Type.Small);
            UiLayout.Fix(plateNote.rectTransform, 0f, 24f);
            plate.gameObject.SetActive(false);

            bar = CampaignBar.Create(Root, Shell);
            dialogue = DialoguePanel.Create(Root);
        }

        protected override void Refresh()
        {
            bar.Bind(Game);
            BindGame(Game);
            World.SiteReached -= OnSiteReached;
            World.FigureReached -= OnFigureReached;
            World.SiteReached += OnSiteReached;
            World.FigureReached += OnFigureReached;
            pointerStale = true;
        }

        protected override void OnHidden()
        {
            bar.Bind(null);
            BindGame(null);
            dialogue.Cancel();
            plate.gameObject.SetActive(false);
            if (Shell != null && Shell.Camp != null)
            {
                Shell.Camp.SiteReached -= OnSiteReached;
                Shell.Camp.FigureReached -= OnFigureReached;
            }
        }

        private void OnDestroy()
        {
            BindGame(null);
        }

        private void BindGame(MetaGame game)
        {
            if (bound != null)
            {
                bound.Changed -= MarkPointerStale;
            }

            bound = game;
            if (bound != null)
            {
                bound.Changed += MarkPointerStale;
            }

            pointerStale = true;
        }

        private void MarkPointerStale()
        {
            pointerStale = true;
        }

        private void LateUpdate()
        {
            if (!IsVisible || Shell == null || World == null)
            {
                return;
            }

            World.InputEnabled = !dialogue.IsOpen && !Shell.SettingsOpen && !Shell.ModalOpen && Machine != null && Machine.CurrentState == State;

            if (pointerStale && bound != null)
            {
                pointerStale = false;
                Objective objective = bound.CurrentObjective;
                World.PointAt(objective != null ? objective.Place : null);
            }

            UpdatePlate();
        }

        // ------------------------------------------------------------------ name plate

        private void UpdatePlate()
        {
            CampSite site = World.HoveredSite;
            CampFigure figure = World.HoveredFigure;
            if (site == null && figure == null)
            {
                plate.gameObject.SetActive(false);
                return;
            }

            if (site != null)
            {
                plateName.text = site.Name.Get();
                plateNote.text = site.Purpose.Get();
            }
            else
            {
                Character who = Characters.Find(figure.Character);
                plateName.text = who != null ? who.Name.Get() : figure.Character;
                plateNote.text = who != null ? who.Role.Get() : string.Empty;
            }

            Vector3 screen = World.View.WorldToScreenPoint(World.HoverAnchor);
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, screen, null, out local);

            // Anchored at the root's bottom-left, so the local point is shifted to that corner.
            Rect area = Root.rect;
            float x = local.x - area.xMin;
            float y = local.y - area.yMin + Theme.Space.Tight;
            float half = PlateWidth * 0.5f;
            x = Mathf.Clamp(x, half + Theme.Space.Base, area.width - half - Theme.Space.Base);
            y = Mathf.Min(y, area.height - CampaignBar.CoveredHeight - PlateHeight - Theme.Space.Tight);
            plate.anchoredPosition = new Vector2(Mathf.Round(x), Mathf.Round(y));
            plate.gameObject.SetActive(true);
        }

        // ------------------------------------------------------------------ arriving

        private void OnFigureReached(CampFigure figure)
        {
            if (Game == null)
            {
                return;
            }

            if (figure.Character == Characters.Tomas)
            {
                TalkToAide();
                return;
            }

            // A keeper speaks for their building.
            foreach (CampSite site in Encampment.Sites)
            {
                if (site.Keeper == figure.Character)
                {
                    Visit(site);
                    return;
                }
            }
        }

        private void OnSiteReached(CampSite site)
        {
            if (Game != null)
            {
                Visit(site);
            }
        }

        private void TalkToAide()
        {
            Objective objective = Game.CurrentObjective;
            if (objective != null && objective.Place == Places.Aide)
            {
                dialogue.Play(Core.Content.Dialogue.AideWelcome, () => Game.MarkTask(Campaign.TaskTalkAide));
                return;
            }

            LocString reminder = Core.Content.Dialogue.AideReminder;
            LocString line = objective != null
                ? new LocString(string.Format(reminder.English, objective.Text.English), string.Format(reminder.Filipino, objective.Text.Filipino))
                : Core.Content.Dialogue.AideWelcome[Core.Content.Dialogue.AideWelcome.Count - 1].Text;
            dialogue.Say(Characters.Tomas, line, null);
        }

        /// <summary>
        /// The keeper explains the building the first time; after that, or once they have, the
        /// building opens.
        /// </summary>
        private void Visit(CampSite site)
        {
            string flag = Core.Content.Dialogue.MetFlag(site.Place);
            var greeting = Core.Content.Dialogue.Greeting(site.Place);
            if (greeting != null && !Game.HasFlag(flag))
            {
                dialogue.Play(greeting, () =>
                {
                    Game.SetFlag(flag);
                    Open(site);
                });
                return;
            }

            Open(site);
        }

        private void Open(CampSite site)
        {
            if (!IsVisible)
            {
                return;
            }

            switch (site.Place)
            {
                case Places.Farm:
                case Places.Mine:
                case Places.Exchange:
                    Shell.VisitingPlace = site.Place;
                    GoTo(GameState.ResourceManagement);
                    break;

                case Places.Armory:
                    GoTo(GameState.Inventory);
                    break;

                case Places.Training:
                    GoTo(GameState.RosterTraining);
                    break;

                case Places.Recruitment:
                    GoTo(GameState.HeroSummoning);
                    break;

                case Places.MissionTent:
                    GoTo(GameState.MissionPortal);
                    break;

                case Places.Library:
                    LibraryPanel.Open(Shell);
                    break;

                default:
                    UiSfx.Play(UiSfx.Cue.Error);
                    UiControls.Toast(Loc.Format(TextKey.HubComingSoon, site.Name.Get()));
                    break;
            }
        }
    }
}
