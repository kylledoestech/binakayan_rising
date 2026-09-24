using System;
using BinakayanRising.Core.Content;
using BinakayanRising.Core.Localization;
using BinakayanRising.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BinakayanRising.UI.Shell
{
    /// <summary>
    /// Plays a story scene: a full-screen picture per slide, where and when over it, and the
    /// narrator's line typed out in a box at the foot of the screen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Click, Space or Enter finishes the line being typed, then moves on; Esc or Skip ends the
    /// scene. Every scene can be skipped, and each is short, because a story that keeps a
    /// player from the game is one they learn to skip.
    /// </para>
    /// <para>
    /// A scene id with no scene behind it (null, or not yet written) calls straight through, so
    /// callers never have to check first.
    /// </para>
    /// </remarks>
    public sealed class CutscenePlayer : MonoBehaviour, IPointerClickHandler
    {
        private const float BoxHeight = 230f;
        private const float PortraitSize = 120f;
        private const float FadeSeconds = 0.35f;

        private Cutscene scene;
        private int index;
        private Action onFinished;
        private RawImage picture;
        private CanvasGroup pictureGroup;
        private TextMeshProUGUI caption;
        private RectTransform captionBox;
        private Image portrait;
        private TextMeshProUGUI speaker;
        private TextMeshProUGUI speakerRole;
        private TextMeshProUGUI line;
        private TextMeshProUGUI progress;
        private TextMeshProUGUI nextLabel;
        private float typed;
        private int openedFrame;
        private int renderedVersion = -1;
        private Coroutine fading;

        /// <summary>The scene playing now, or null. For screenshots and tests.</summary>
        public static CutscenePlayer Current { get; private set; }

        /// <summary>The slide showing, from 0.</summary>
        public int SlideIndex
        {
            get { return index; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = null;
        }

        /// <summary>
        /// Plays the scene <paramref name="id"/>, then calls <paramref name="finished"/>. With no
        /// such scene, calls it at once.
        /// </summary>
        public static CutscenePlayer Play(string id, Action finished)
        {
            Cutscene found = Cutscenes.Find(id);
            if (found == null || found.Slides.Length == 0 || Current != null)
            {
                if (finished != null)
                {
                    finished();
                }

                return null;
            }

            Canvas canvas = UiKit.Screen("Cutscene", Theme.Layer.Modal);
            UiKit.EnsureEventSystem();
            var view = canvas.gameObject.AddComponent<CutscenePlayer>();
            view.scene = found;
            view.onFinished = finished;
            view.Build(canvas.transform);
            Current = view;
            view.Show(0);
            return view;
        }

        private void Build(Transform root)
        {
            // Black behind everything, so a picture of another shape letterboxes cleanly.
            Image backdrop = UiKit.NewRect(root, "Backdrop").gameObject.AddComponent<Image>();
            backdrop.color = Color.black;
            UiKit.Stretch(backdrop.rectTransform);

            // The picture fills the screen above the narration box, keeping its shape.
            RectTransform frame = UiKit.NewRect(root, "Picture");
            frame.anchorMin = Vector2.zero;
            frame.anchorMax = Vector2.one;
            frame.offsetMin = new Vector2(0f, BoxHeight - 20f);
            frame.offsetMax = Vector2.zero;
            picture = frame.gameObject.AddComponent<RawImage>();
            picture.raycastTarget = false;
            var fitter = frame.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = (float)CutsceneArt.Width / CutsceneArt.Height;
            pictureGroup = UiKit.Group(frame.gameObject);

            // The caption: a parchment label in the top-left corner.
            RectTransform label = UiKit.Panel(root, "Caption", blocksClicks: false);
            captionBox = label;
            UiKit.Anchor(label, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Theme.Space.Loose, -Theme.Space.Loose), new Vector2(620f, 64f));
            caption = UiKit.Display(label, string.Empty, Theme.Type.Heading, TextAlignmentOptions.Left);
            caption.color = Theme.Revolution;
            UiLayout.OneLine(caption, Theme.Type.Heading);
            UiKit.Stretch(caption.rectTransform, Theme.Space.Base);

            // The narration box along the bottom.
            RectTransform box = UiKit.Panel(root, "Narration", blocksClicks: false);
            box.anchorMin = new Vector2(0f, 0f);
            box.anchorMax = new Vector2(1f, 0f);
            box.pivot = new Vector2(0.5f, 0f);
            box.offsetMin = new Vector2(Theme.Space.Loose, Theme.Space.Base);
            box.offsetMax = new Vector2(-Theme.Space.Loose, BoxHeight);

            RectTransform row = UiKit.Row(box, "Row", Theme.Space.Wide, Theme.Space.FramePadding, TextAnchor.MiddleLeft);
            UiKit.Stretch(row);

            RectTransform well = UiKit.Well(row, "Portrait");
            UiLayout.Fix(well, PortraitSize + 16f, PortraitSize + 16f);
            portrait = UiKit.Icon(well, null, PortraitSize, Color.white);
            UiKit.Anchor((RectTransform)portrait.transform.parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PortraitSize, PortraitSize));

            RectTransform words = UiKit.Column(row, "Words", Theme.Space.Tight, 0f, TextAnchor.UpperLeft);
            UiLayout.Flexible(words);
            UiLayout.FlexibleHeight(words);
            UiLayout.FillWidth(words);

            RectTransform nameRow = UiKit.Row(words, "Speaker", Theme.Space.Base, 0f, TextAnchor.MiddleLeft);
            UiLayout.Fix(nameRow, 0f, 34f);
            speaker = UiKit.Body(nameRow, string.Empty, Theme.Type.Heading + 2f, TextAlignmentOptions.Left);
            speaker.fontStyle = FontStyles.Bold;
            speaker.color = Theme.Revolution;
            UiLayout.OneLine(speaker, Theme.Type.Heading + 2f);
            UiLayout.Fix(speaker.rectTransform, 120f, 34f);
            speakerRole = UiKit.Caption(nameRow, string.Empty, TextAlignmentOptions.Left);
            speakerRole.fontStyle = FontStyles.Italic;
            UiLayout.OneLine(speakerRole, Theme.Type.Body);
            UiLayout.Flexible(speakerRole.rectTransform);
            UiLayout.Fix(speakerRole.rectTransform, 0f, 34f);

            line = UiKit.Body(words, string.Empty, Theme.Type.Body + 4f, TextAlignmentOptions.TopLeft);
            line.textWrappingMode = TextWrappingModes.Normal;
            UiLayout.FlexibleHeight(line.rectTransform);

            RectTransform side = UiKit.Column(row, "Controls", Theme.Space.Tight, 0f, TextAnchor.MiddleCenter);
            UiLayout.Fix(side, 200f, 0f);
            UiLayout.FlexibleHeight(side);

            progress = UiKit.Caption(side, string.Empty, TextAlignmentOptions.Center);
            progress.fontStyle = FontStyles.Bold;
            UiLayout.OneLine(progress, Theme.Type.Small);
            UiLayout.Fix(progress.rectTransform, 200f, 24f);

            Button next = UiKit.SealButton(side, Loc.Get(TextKey.CutNext), Advance, 200f, 56f, 0f, "Button Cutscene Next");
            UiLayout.Fix((RectTransform)next.transform, 200f, 56f);
            nextLabel = next.GetComponentInChildren<TextMeshProUGUI>();

            Button skip = UiKit.SealButton(side, TextKey.CutSkip, Skip, 200f, 44f, Theme.Type.Small, "Button Cutscene Skip");
            UiLayout.Fix((RectTransform)skip.transform, 200f, 44f);
        }

        // ------------------------------------------------------------------ playing

        private void Show(int slide)
        {
            index = slide;
            openedFrame = Time.frameCount;
            Render();
            typed = UserPrefs.CharactersPerSecond(UserPrefs.DialogueSpeed) <= 0f ? line.textInfo.characterCount : 0f;
            line.maxVisibleCharacters = (int)typed;

            if (fading != null)
            {
                StopCoroutine(fading);
            }

            pictureGroup.alpha = 0f;
            fading = StartCoroutine(UiTween.Fade(pictureGroup, 1f, FadeSeconds));
            UiSfx.Play(UiSfx.Cue.Open);
        }

        private void Render()
        {
            CutsceneSlide slide = scene.Slides[index];
            picture.texture = CutsceneArt.For(slide);
            caption.text = slide.Caption.Get();
            FitCaption();

            Character who = Characters.Find(scene.Narrator);
            speaker.text = who != null ? who.Name.Get() : scene.Narrator;
            speakerRole.text = who != null ? who.Role.Get() : string.Empty;
            Sprite face = Theme.Assets != null ? Theme.Assets.UnitPortrait(scene.Narrator) : null;
            portrait.sprite = face;
            portrait.enabled = face != null;

            line.text = slide.Line.Get();
            line.ForceMeshUpdate();
            progress.text = (index + 1) + " / " + scene.Slides.Length;
            nextLabel.text = Loc.Get(index + 1 < scene.Slides.Length ? TextKey.CutNext : TextKey.CutDone);
            renderedVersion = Loc.Version;
        }

        private bool IsTyping
        {
            get { return line.maxVisibleCharacters < line.textInfo.characterCount; }
        }

        /// <summary>Finishes the line being typed, or moves to the next slide.</summary>
        public void Advance()
        {
            if (IsTyping)
            {
                typed = line.textInfo.characterCount;
                line.maxVisibleCharacters = line.textInfo.characterCount;
                return;
            }

            UiSfx.Play(UiSfx.Cue.Click);
            if (index + 1 < scene.Slides.Length)
            {
                Show(index + 1);
                return;
            }

            Finish();
        }

        /// <summary>Ends the scene at once.</summary>
        public void Skip()
        {
            UiSfx.Play(UiSfx.Cue.Close);
            Finish();
        }

        private void Finish()
        {
            if (Current == this)
            {
                Current = null;
            }

            if (fading != null)
            {
                StopCoroutine(fading);
                fading = null;
            }

            Action finished = onFinished;
            onFinished = null;
            Destroy(gameObject);
            if (finished != null)
            {
                finished();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Time.frameCount > openedFrame)
            {
                Advance();
            }
        }

        private void Update()
        {
            if (renderedVersion != Loc.Version)
            {
                Render();
                typed = line.textInfo.characterCount;
                line.maxVisibleCharacters = line.textInfo.characterCount;
            }

            if (IsTyping)
            {
                typed += UserPrefs.CharactersPerSecond(UserPrefs.DialogueSpeed) * Time.unscaledDeltaTime;
                line.maxVisibleCharacters = Mathf.Min((int)typed, line.textInfo.characterCount);
            }

            Keyboard keys = Keyboard.current;
            if (keys == null || Time.frameCount <= openedFrame)
            {
                return;
            }

            if (keys.escapeKey.wasPressedThisFrame)
            {
                Skip();
            }
            else if (keys.spaceKey.wasPressedThisFrame || keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame)
            {
                Advance();
            }
        }

        private void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        /// <summary>
        /// Widens the caption plate to its text, so a long act title ("Act 4 - The Masterpiece of
        /// Binakayan-Dalahican") keeps its full size instead of shrinking into the 620 default.
        /// </summary>
        private void FitCaption()
        {
            float inner = caption.GetPreferredValues(caption.text, 0f, 0f).x;
            float limit = Theme.ReferenceResolution.x - (2f * Theme.Space.Loose);
            float width = Mathf.Clamp(Mathf.Ceil(inner + (2f * Theme.Space.Base) + 8f), 620f, limit);
            captionBox.sizeDelta = new Vector2(width, captionBox.sizeDelta.y);
        }
    }
}
