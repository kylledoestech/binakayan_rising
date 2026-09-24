using System.Collections;
using BinakayanRising.Core.Localization;
using BinakayanRising.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BinakayanRising.UI.Shell
{
    /// <summary>
    /// The title splash: Katipunan soldiers in a Cavite trench at dusk, the game's name, and
    /// "Press any key". Any key or click fades it away to the main menu under it (#46).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The picture is rendered by <c>Tools/splash/splash.py</c> in Blender and loaded from
    /// <c>Resources/Splash/splash</c>. Only the picture is baked: the title and the prompt are
    /// drawn here, so they follow the language setting. With no picture (a clone that has not
    /// imported it yet) the splash shows the menu's plain backdrop instead of failing.
    /// </para>
    /// <para>
    /// It sits over the main menu rather than being a state of its own, so the state machine and
    /// its Figure 2 diagram are untouched: the menu is already live underneath when it fades.
    /// </para>
    /// </remarks>
    public sealed class SplashScreen : MonoBehaviour
    {
        /// <summary>Where the picture lives under Resources.</summary>
        public const string ArtPath = "Splash/splash";

        /// <summary>Over the shell and its bars, under Settings' modal cards and toasts.</summary>
        private const int Layer = Theme.Layer.Settings + 2;

        /// <summary>Input is ignored this long after the splash appears, so a held key does not skip it.</summary>
        private const float InputGrace = 0.4f;

        private const float FadeSeconds = 0.45f;

        private CanvasGroup group;
        private TextMeshProUGUI prompt;
        private float shownAt;
        private bool leaving;

        /// <summary>The splash on screen, or null.</summary>
        public static SplashScreen Current { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = null;
        }

        /// <summary>Loads the splash picture, point-filtered, or null when it is missing.</summary>
        public static Texture2D LoadArt()
        {
            Texture2D art = Resources.Load<Texture2D>(ArtPath);
            if (art != null)
            {
                art.filterMode = FilterMode.Point;
            }

            return art;
        }

        /// <summary>Shows the splash over whatever is on screen.</summary>
        public static SplashScreen Show()
        {
            if (Current != null)
            {
                return Current;
            }

            Canvas canvas = UiKit.Screen("Splash", Layer);
            UiKit.EnsureEventSystem();
            var view = canvas.gameObject.AddComponent<SplashScreen>();
            view.Build(canvas.transform);
            view.shownAt = Time.unscaledTime;
            Current = view;
            return view;
        }

        private void Build(Transform root)
        {
            group = UiKit.Group(gameObject);

            Image backdrop = UiKit.NewRect(root, "Backdrop").gameObject.AddComponent<Image>();
            backdrop.color = Theme.Backdrop;
            UiKit.Stretch(backdrop.rectTransform);

            Texture2D art = LoadArt();
            if (art != null)
            {
                RawImage picture = UiKit.NewRect(root, "Picture").gameObject.AddComponent<RawImage>();
                picture.texture = art;
                picture.raycastTarget = false;
                UiKit.Stretch(picture.rectTransform);

                // Fill the screen, cropping the long side, rather than letterboxing a 4:3 window.
                AspectRatioFitter fit = picture.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = (float)art.width / art.height;
            }

            // The title sits in the clear dusk sky at the upper left, where the picture leaves room.
            RectTransform titleBlock = UiKit.Column(root, "Title", Theme.Space.Tight, 0f, TextAnchor.UpperLeft);
            UiKit.Anchor(titleBlock, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(110f, -90f), new Vector2(900f, 260f));
            titleBlock.pivot = new Vector2(0f, 1f);
            titleBlock.anchoredPosition = new Vector2(110f, -90f);
            UiLayout.FillWidth(titleBlock);

            Image sigil = UiKit.Sigil(titleBlock, 84f, Theme.Gold);
            UiLayout.Fix((RectTransform)sigil.transform.parent, 84f, 84f);

            TextMeshProUGUI title = UiKit.Display(titleBlock, "Binakayan Rising", 88f, TextAlignmentOptions.Left);
            title.color = Theme.Parchment;
            UiLayout.OneLine(title, 88f);
            UiLayout.Fix(title.rectTransform, 0f, 104f);

            TextMeshProUGUI tagline = UiKit.Body(titleBlock, Loc.Get(TextKey.MenuTagline), Theme.Type.Heading, TextAlignmentOptions.Left);
            tagline.color = Theme.Gold;
            tagline.fontStyle = FontStyles.Italic;
            UiKit.Localize(tagline, TextKey.MenuTagline);
            UiLayout.Fix(tagline.rectTransform, 0f, 40f);

            // The prompt, over the dark near bank of the trench at the foot of the picture.
            prompt = UiKit.Display(root, Loc.Get(TextKey.SplashPressAnyKey), Theme.Type.Title, TextAlignmentOptions.Center);
            prompt.color = Theme.Parchment;
            UiKit.Localize(prompt, TextKey.SplashPressAnyKey);
            UiLayout.OneLine(prompt, Theme.Type.Title);
            UiKit.Anchor(prompt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(1200f, 52f));
        }

        private void Update()
        {
            if (leaving)
            {
                return;
            }

            // A slow breath on the prompt, so the screen reads as waiting rather than frozen.
            float breath = 0.55f + (0.45f * Mathf.Cos((Time.unscaledTime - shownAt) * 2.6f));
            prompt.alpha = breath;

            if (Time.unscaledTime - shownAt < InputGrace)
            {
                return;
            }

            bool pressed = (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                || (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame))
                || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
            if (pressed)
            {
                Dismiss(false);
            }
        }

        /// <summary>Fades the splash out to the menu under it; at once when <paramref name="immediate"/>.</summary>
        public void Dismiss(bool immediate)
        {
            if (leaving)
            {
                return;
            }

            leaving = true;
            if (Current == this)
            {
                Current = null;
            }

            if (immediate)
            {
                Destroy(gameObject);
                return;
            }

            UiSfx.Play(UiSfx.Cue.Confirm);
            StartCoroutine(FadeOut());
        }

        private IEnumerator FadeOut()
        {
            group.blocksRaycasts = false;
            float start = Time.unscaledTime;
            while (Time.unscaledTime - start < FadeSeconds)
            {
                group.alpha = 1f - ((Time.unscaledTime - start) / FadeSeconds);
                yield return null;
            }

            Destroy(gameObject);
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
