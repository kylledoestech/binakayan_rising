using System.Collections;
using BinakayanRising.Core.Content;
using BinakayanRising.Core.Localization;
using BinakayanRising.Core.Meta;
using BinakayanRising.UI.Kit;
using BinakayanRising.UI.Screens;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BinakayanRising.UI.Shell
{
    public sealed partial class ShotAutopilot
    {
        /// <summary>
        /// The capstone panel's feedback: Act 1 animated, the Journal's Characters and Ranks tabs
        /// before and after the people in them are met, and a campaign battle's location banner,
        /// top bar and the hero's gold plate, with the moving parts measured as well as shot.
        /// </summary>
        private IEnumerator PanelRoute()
        {
            Shell.Session.DeleteSave();
            Shell.StartNewCampaign();
            yield return Wait(0.6f);
            Hub().Dialogue.Finish();
            MetaGame game = Shell.Session.Game;

            CutscenePlayer.Play(Cutscenes.Act1, null);
            yield return StageShot("pf_01_act1_walkin", 0.9f);
            yield return StageShot("pf_02_act1_ghent", 2.2f);
            NextSlide();
            yield return StageShot("pf_03_act1_night", 2.6f);
            NextSlide();
            NextSlide();
            yield return StageShot("pf_04_act1_sea", 1f);
            yield return StageShot("pf_05_act1_sea_later", 6f);
            NextSlide();
            yield return StageShot("pf_06_act1_camp", 2.6f);
            CutscenePlayer.Current?.Skip();
            yield return Wait(0.4f);

            yield return JournalShots("pf_07", "start");

            foreach (string id in new[] { "q01", "q02", "q03", "q04", "q05", "q06", "q07" })
            {
                game.Data.clearedQuests.Add(id);
            }

            game.Earn(Currency.Rations, 60);
            yield return DrainRankCards();
            yield return JournalShots("pf_08", "q08");

            UserPrefs.ChooseLanguage(Language.Filipino);
            yield return Wait(0.3f);
            yield return JournalShots("pf_09", "fil");
            UserPrefs.ChooseLanguage(Language.English);
            yield return Wait(0.3f);

            // q08, the battle of November 9: its banner, then the bar and the hero once it fades.
            Shell.Camp.ClickSite(Places.MissionTent);
            yield return WaitWhile(() => Shell.Camp.IsWalking, 8f);
            Hub().Dialogue.Finish();
            yield return WaitWhile(() => !(Shell.Router.Current is MissionMapScreen), 4f);
            Shell.LaunchQuest(Campaign.Find("q08"));
            yield return Wait(0.5f);
            CutscenePlayer.Current?.Skip();
            yield return WaitWhile(() => Shell.Battle == null, 4f);
            yield return Shot("pf_10_banner", 0.9f);
            MeasureBanner("banner showing");
            yield return Shot("pf_11_banner_gone", 4.2f);
            MeasureBanner("banner gone");
            if (Shell.Battle != null)
            {
                Shell.Battle.RequestAutoDeploy();
                yield return Shot("pf_12_hero", 0.8f);
                MeasureHero("deployed");
            }
        }

        /// <summary>
        /// Shoots the cutscene <paramref name="settle"/> seconds on, measuring the stage in the
        /// frame before the capture: encoding a capture takes a second or two, and the movers do
        /// not wait for it.
        /// </summary>
        private IEnumerator StageShot(string name, float settle)
        {
            yield return Wait(settle);
            MeasureStage(name);
            yield return Shot(name, 0f);
        }

        /// <summary>The Journal's Characters and Ranks tabs, on a met and an unmet entry each.</summary>
        private IEnumerator JournalShots(string prefix, string when)
        {
            LibraryPanel.Open(Shell);
            yield return Wait(0.4f);
            LibraryPanel journal = LibraryPanel.Current;
            if (journal == null)
            {
                Note("journal", "did not open (" + when + ")");
                yield break;
            }

            journal.ShowCharacters();
            journal.SelectFigure(0, false);
            yield return Shot(prefix + "a_characters");
            MeasureCard("journal " + when, journal.Card);
            journal.SelectFigure(IndexOfFigure(Figures.Blanco), false);
            yield return Shot(prefix + "b_blanco");
            journal.ShowRanks();
            yield return Shot(prefix + "c_ranks");
            journal.SelectRank(PlayerRanks.Count - 1, false);
            yield return Shot(prefix + "d_rank_last");
            journal.Close();
            yield return Wait(0.3f);
        }

        private static int IndexOfFigure(string id)
        {
            for (int i = 0; i < Figures.All.Count; i++)
            {
                if (Figures.All[i].Id == id)
                {
                    return i;
                }
            }

            return 0;
        }

        /// <summary>Moves the cutscene one slide on, finishing the line first if it is still typing.</summary>
        private static void NextSlide()
        {
            CutscenePlayer player = CutscenePlayer.Current;
            if (player == null)
            {
                return;
            }

            int at = player.SlideIndex;
            player.Advance();
            if (player.SlideIndex == at)
            {
                player.Advance();
            }
        }

        /// <summary>
        /// Records where every mover on the cutscene's stage is, in canvas units, and checks the
        /// cast stand with their feet behind the narration box, most of them showing, inside the
        /// screen.
        /// </summary>
        private void MeasureStage(string what)
        {
            CutscenePlayer player = CutscenePlayer.Current;
            CutsceneStage stage = player != null ? player.Stage : null;
            if (stage == null)
            {
                Note(what, "no cutscene playing");
                return;
            }

            Canvas canvas = player.GetComponent<Canvas>();
            float scale = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            Transform box = player.transform.Find("Narration");
            Rect narration = box != null ? ScreenRectOf((RectTransform)box, canvas) : Rect.zero;
            Rect picture = ScreenRectOf((RectTransform)stage.transform, canvas);
            audit.AppendFormat("\n-- stage {0}: t={1:0.00}s, slide {2}, {3} movers, picture {4} scale {5:0.000}, narration top {6:0}\n",
                what, stage.Elapsed, player.SlideIndex + 1, stage.ActorCount, Describe(picture, scale),
                stage.transform.localScale.x, narration.yMax / scale);

            foreach (Transform child in stage.transform)
            {
                Rect bounds = ScreenRectOf((RectTransform)child, canvas);
                Graphic graphic = child.GetComponent<Graphic>();
                audit.AppendFormat("   {0} {1} alpha {2:0.00}\n", child.name, Describe(bounds, scale), graphic != null ? graphic.color.a : 1f);
                if (!child.name.StartsWith("Cast "))
                {
                    continue;
                }

                if (bounds.yMin > narration.yMax)
                {
                    Note(what, child.name + "'s feet show above the narration box, so they float: " + Describe(bounds, scale));
                }

                if (bounds.yMax - narration.yMax < bounds.height * 0.6f || bounds.yMax > Screen.height)
                {
                    Note(what, child.name + " is cut off: " + Describe(bounds, scale));
                }

                if (bounds.xMin < 0f || bounds.xMax > Screen.width)
                {
                    Note(what, child.name + " is off the screen's side: " + Describe(bounds, scale));
                }
            }
        }

        /// <summary>The location banner's place and opacity, and the top bar's place line.</summary>
        private void MeasureBanner(string what)
        {
            var hud = Object.FindAnyObjectByType<BattleHud>();
            if (hud == null)
            {
                Note(what, "no battle HUD");
                return;
            }

            RectTransform banner = hud.Banner;
            if (banner == null)
            {
                audit.AppendFormat("\n-- {0}: banner retired\n", what);
            }
            else
            {
                var canvas = banner.GetComponentInParent<Canvas>().rootCanvas;
                float scale = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
                CanvasGroup group = banner.GetComponent<CanvasGroup>();
                audit.AppendFormat("\n-- {0}: banner {1} alpha {2:0.00} raycasts {3}\n", what,
                    Describe(ScreenRectOf(banner, canvas), scale), group.alpha, group.blocksRaycasts);
                foreach (TextMeshProUGUI text in banner.GetComponentsInChildren<TextMeshProUGUI>())
                {
                    audit.AppendFormat("   \"{0}\" size {1:0}\n", text.text, text.fontSize);
                }
            }

            RectTransform line;
            if (hud.TryGetAnchor("top.rank", out line))
            {
                TextMeshProUGUI text = line.GetComponent<TextMeshProUGUI>();
                audit.AppendFormat("   top bar: \"{0}\" ({1:0} wide, text {2:0})\n", text != null ? text.text : "?", line.rect.width,
                    text != null ? text.preferredWidth : 0f);
            }
        }

        /// <summary>
        /// Finds the hero's gold plate among the name plates and checks the star sits on it, left
        /// of the name, with nothing else wearing gold.
        /// </summary>
        private void MeasureHero(string what)
        {
            GameObject labels = GameObject.Find("World Labels");
            if (labels == null)
            {
                Note(what, "no world labels");
                return;
            }

            Canvas canvas = labels.GetComponent<Canvas>();
            float scale = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            Transform star = labels.transform.Find("Hero Star");
            Transform plates = labels.transform.Find("Unit Labels Plates");
            Transform names = labels.transform.Find("Unit Labels");
            if (star == null || plates == null || names == null)
            {
                Note(what, "label layer is missing a part");
                return;
            }

            int gold = 0;
            Rect plate = Rect.zero;
            foreach (Transform child in plates)
            {
                Image image = child.GetComponent<Image>();
                if (child.gameObject.activeSelf && image != null && image.color == Theme.GoldBright)
                {
                    gold++;
                    plate = ScreenRectOf((RectTransform)child, canvas);
                }
            }

            Rect name = Rect.zero;
            foreach (TextMeshProUGUI label in names.GetComponentsInChildren<TextMeshProUGUI>(false))
            {
                if (label.text == "EVANGELISTA")
                {
                    name = ScreenRectOf(label.rectTransform, canvas);
                    name = new Rect(name.center.x - (label.preferredWidth * scale * 0.5f), name.y, label.preferredWidth * scale, name.height);
                }
            }

            Image glyph = star.GetComponentInChildren<Image>();
            Rect starRect = glyph != null ? ScreenRectOf(glyph.rectTransform, canvas) : Rect.zero;
            audit.AppendFormat("\n-- hero {0}: {1} gold plate(s), plate {2}, name {3}, star {4} active {5}\n", what, gold,
                Describe(plate, scale), Describe(name, scale), Describe(starRect, scale), star.gameObject.activeSelf);
            if (gold != 1)
            {
                Note(what, gold + " gold plates; the hero's should be the only one");
            }

            if (!star.gameObject.activeSelf || starRect.xMin < plate.xMin - 1f || starRect.xMax > name.xMin + 1f
                || starRect.center.y < plate.yMin || starRect.center.y > plate.yMax)
            {
                Note(what, "star is not on the plate left of the name");
            }

            if (name.xMax > plate.xMax + 1f)
            {
                Note(what, "name runs off the gold plate");
            }
        }

        private static Rect ScreenRectOf(RectTransform rect, Canvas canvas)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return ScreenRect(corners, canvas);
        }
    }
}
