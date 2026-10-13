using System.Collections;
using BinakayanRising.Gameplay;
using BinakayanRising.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BinakayanRising.UI.Screens
{
    public sealed partial class BattleHud
    {
        private const float BannerWidth = 760f;
        private const float BannerHeight = 132f;
        private const float BannerDelay = 0.3f;
        private const float BannerFadeIn = 0.5f;
        private const float BannerHold = 2.8f;
        private const float BannerFadeOut = 0.8f;

        private CanvasGroup banner;

        /// <summary>The opening banner, while it is showing. For screenshots and tests.</summary>
        public RectTransform Banner
        {
            get { return banner != null && banner.gameObject.activeSelf ? (RectTransform)banner.transform : null; }
        }

        /// <summary>
        /// A campaign battle opens on where and when it is fought, as a title card under the top
        /// bar that fades after a few seconds; the top bar keeps the same line for the rest of the
        /// battle (panel feedback: "location name based to story, every battle").
        /// </summary>
        /// <remarks>
        /// Nothing on it takes a click, so a player who starts deploying at once is never blocked by
        /// it. It sits under the top bar rather than at the centre of the screen, clear of the
        /// tutorial's first card and of the deployment zone.
        /// </remarks>
        private void BuildBanner(Transform parent)
        {
            MissionSetup mission = battle.Mission;
            if (mission == null || string.IsNullOrEmpty(mission.Place))
            {
                return;
            }

            RectTransform root = UiKit.NewRect(parent, "Location Banner");
            Pin(root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -(TopBarHeight + Theme.Space.Loose)), new Vector2(BannerWidth, BannerHeight));
            banner = UiKit.Group(root.gameObject);
            banner.alpha = 0f;
            banner.interactable = false;
            banner.blocksRaycasts = false;

            // An ink band, so parchment type reads over grass, sea and smoke alike.
            Image band = root.gameObject.AddComponent<Image>();
            band.sprite = Theme.BarFill;
            band.type = Image.Type.Sliced;
            band.color = new Color(Theme.Ink.r, Theme.Ink.g, Theme.Ink.b, 0.86f);
            band.raycastTarget = false;

            RectTransform column = UiKit.Column(root, "Column", 0f, Theme.Space.Snug, TextAnchor.MiddleCenter);
            UiKit.Stretch(column);
            ExpandChildren(column);

            Image top = UiKit.Divider(column);
            top.raycastTarget = false;
            FixHeight(top.rectTransform, 14f);

            TextMeshProUGUI place = UiKit.Display(column, mission.Place, Theme.Type.Title + 6f, TextAlignmentOptions.Center);
            place.color = Theme.GoldBright;
            place.fontStyle = FontStyles.UpperCase;
            place.characterSpacing = 6f;
            place.raycastTarget = false;
            FitLine(place, Theme.Type.Title + 6f);
            FixHeight(place.rectTransform, 52f);

            TextMeshProUGUI when = UiKit.Body(column, mission.When ?? string.Empty, Theme.Type.Body + 2f, TextAlignmentOptions.Center);
            when.color = Theme.Parchment;
            when.fontStyle = FontStyles.Italic;
            when.raycastTarget = false;
            FitLine(when, Theme.Type.Body + 2f);
            FixHeight(when.rectTransform, 28f);

            Image bottom = UiKit.Divider(column);
            bottom.raycastTarget = false;
            FixHeight(bottom.rectTransform, 14f);

            Register("banner.location", root);
        }

        /// <summary>Fades the banner in, holds it, fades it out, then retires it.</summary>
        private IEnumerator PlayBanner()
        {
            if (banner == null)
            {
                yield break;
            }

            yield return new WaitForSecondsRealtime(BannerDelay);
            yield return UiTween.Fade(banner, 1f, BannerFadeIn);

            // Fade leaves a finished group taking clicks; this one never should.
            banner.blocksRaycasts = false;
            yield return new WaitForSecondsRealtime(BannerHold);
            yield return UiTween.Fade(banner, 0f, BannerFadeOut);
            banner.gameObject.SetActive(false);
        }
    }
}
