using System;
using BinakayanRising.Core.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BinakayanRising.UI.Kit
{
    /// <summary>
    /// The input widgets the settings and campaign screens need beyond <see cref="UiKit"/>'s
    /// buttons: a slider, a check box, a left/right value stepper, a confirmation dialog and a toast.
    /// </summary>
    /// <remarks>
    /// Same rules as <see cref="UiKit"/>: colours and sizes come from <see cref="Theme"/>, and only
    /// the graphic that is meant to be clicked has raycasts on.
    /// </remarks>
    public static class UiControls
    {
        // ------------------------------------------------------------------ slider

        /// <summary>A horizontal 0..1 slider: bar trough, red fill, gold handle.</summary>
        public static Slider Slider(Transform parent, string name, float width, UnityAction<float> onChanged)
        {
            RectTransform root = UiKit.NewRect(parent, name);
            UiKit.SetSize(root, width, 36f);

            // The trough is the hit area, so a click anywhere along it jumps the handle there.
            RectTransform trackRect = UiKit.NewRect(root, "Track");
            trackRect.anchorMin = new Vector2(0f, 0.5f);
            trackRect.anchorMax = new Vector2(1f, 0.5f);
            trackRect.sizeDelta = new Vector2(0f, 16f);
            Image track = trackRect.gameObject.AddComponent<Image>();
            track.sprite = Theme.BarTrack;
            track.type = Image.Type.Sliced;
            track.color = Theme.RevolutionDark;
            track.raycastTarget = true;

            RectTransform fillArea = UiKit.NewRect(root, "Fill Area");
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.sizeDelta = new Vector2(-4f, 12f);

            RectTransform fillRect = UiKit.NewRect(fillArea, "Fill");
            UiKit.Stretch(fillRect);
            Image fill = fillRect.gameObject.AddComponent<Image>();
            fill.sprite = Theme.BarFill;
            fill.type = Image.Type.Sliced;
            fill.color = Theme.Revolution;
            fill.raycastTarget = false;

            RectTransform handleArea = UiKit.NewRect(root, "Handle Area");
            UiKit.Stretch(handleArea);
            handleArea.offsetMin = new Vector2(14f, 0f);
            handleArea.offsetMax = new Vector2(-14f, 0f);

            RectTransform handleRect = UiKit.NewRect(handleArea, "Handle");
            handleRect.sizeDelta = new Vector2(28f, 0f);
            handleRect.anchorMin = new Vector2(0f, 0f);
            handleRect.anchorMax = new Vector2(0f, 1f);
            Image handle = handleRect.gameObject.AddComponent<Image>();
            handle.sprite = Theme.Slot;
            handle.type = Image.Type.Sliced;
            handle.color = Theme.Gold;
            handle.raycastTarget = true;

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;

            var colors = slider.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
            slider.colors = colors;

            if (onChanged != null)
            {
                slider.onValueChanged.AddListener(onChanged);
            }

            return slider;
        }

        // ------------------------------------------------------------------ check box

        /// <summary>A square check box. The tick is a sprite when themed, a glyph otherwise.</summary>
        public static Toggle Checkbox(Transform parent, string name, UnityAction<bool> onChanged)
        {
            RectTransform root = UiKit.NewRect(parent, name);
            UiKit.SetSize(root, 40f, 40f);

            Image box = UiKit.NewRect(root, "Box").gameObject.AddComponent<Image>();
            box.sprite = Theme.Slot;
            box.type = Image.Type.Sliced;
            box.color = Theme.ParchmentDeep;
            box.raycastTarget = true;
            UiKit.Stretch(box.rectTransform);

            Graphic tick;
            if (Theme.IconCheck != null)
            {
                Image image = UiKit.NewRect(root, "Tick").gameObject.AddComponent<Image>();
                image.sprite = Theme.IconCheck;
                image.preserveAspect = true;
                image.color = Theme.Revolution;
                image.raycastTarget = false;
                UiKit.Stretch(image.rectTransform, 6f);
                tick = image;
            }
            else
            {
                TextMeshProUGUI glyph = UiKit.Body(root, "✓", Theme.Type.Title, TextAlignmentOptions.Center);
                glyph.color = Theme.Revolution;
                UiKit.Stretch(glyph.rectTransform);
                tick = glyph;
            }

            var toggle = root.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = box;
            toggle.graphic = tick;
            toggle.toggleTransition = Toggle.ToggleTransition.None;

            toggle.onValueChanged.AddListener(_ => UiSfx.Play(UiSfx.Cue.Toggle));
            if (onChanged != null)
            {
                toggle.onValueChanged.AddListener(onChanged);
            }

            root.gameObject.AddComponent<UiRowFeel>();
            return toggle;
        }

        // ------------------------------------------------------------------ stepper

        /// <summary>A value between two arrow buttons, for picking one of a short list.</summary>
        public sealed class Stepper
        {
            public RectTransform Root;
            public Button Previous;
            public Button Next;
            public TextMeshProUGUI Value;
        }

        /// <summary>Builds a stepper <paramref name="width"/> wide.</summary>
        public static Stepper StepperControl(Transform parent, string name, float width, UnityAction onPrevious, UnityAction onNext)
        {
            var stepper = new Stepper();
            stepper.Root = UiKit.Row(parent, name, Theme.Space.Tight, 0f, TextAnchor.MiddleCenter);
            UiKit.SetSize(stepper.Root, width, 44f);

            stepper.Previous = UiKit.SealButton(stepper.Root, "◂", onPrevious, 44f, 44f, Theme.Type.Heading, "Previous");

            RectTransform well = UiKit.Well(stepper.Root, "Value");
            UiLayout.Fix(well, width - 2f * (44f + Theme.Space.Tight), 44f);
            stepper.Value = UiKit.Body(well, string.Empty, Theme.Type.Body, TextAlignmentOptions.Center);
            stepper.Value.fontStyle = FontStyles.Bold;
            UiKit.Stretch(stepper.Value.rectTransform, Theme.Space.Hair);
            UiLayout.OneLine(stepper.Value, Theme.Type.Body);

            stepper.Next = UiKit.SealButton(stepper.Root, "▸", onNext, 44f, 44f, Theme.Type.Heading, "Next");
            return stepper;
        }

        // ------------------------------------------------------------------ labelled row

        /// <summary>
        /// A settings row: a label taking the left of the row, and a control slot on the right.
        /// </summary>
        /// <returns>The slot to build the control into.</returns>
        public static RectTransform LabelledRow(Transform parent, TextKey label, float height = 48f)
        {
            RectTransform row = UiKit.Row(parent, "Row " + label, Theme.Space.Base, 0f, TextAnchor.MiddleLeft);
            UiLayout.Fix(row, 0f, height);

            TextMeshProUGUI text = UiKit.Body(row, Loc.Get(label), Theme.Type.Body, TextAlignmentOptions.Left);
            UiKit.Localize(text, label);
            UiLayout.OneLine(text, Theme.Type.Body);
            UiLayout.Flexible(text.rectTransform);

            return row;
        }

        /// <summary>A section heading inside a card: small caps in red, over a rule.</summary>
        public static TextMeshProUGUI SectionHeading(Transform parent, TextKey key)
        {
            TextMeshProUGUI heading = UiKit.Display(parent, Loc.Get(key), Theme.Type.Heading, TextAlignmentOptions.Left);
            heading.color = Theme.Revolution;
            UiKit.Localize(heading, key);
            UiLayout.OneLine(heading, Theme.Type.Heading);
            UiLayout.Fix(heading.rectTransform, 0f, 34f);
            return heading;
        }

        // ------------------------------------------------------------------ dialogs

        /// <summary>
        /// Asks a yes/no question over everything else. Destroys itself when answered.
        /// </summary>
        public static GameObject Confirm(TextKey title, TextKey body, TextKey confirmLabel, Action onConfirm)
        {
            Canvas canvas = UiKit.Screen("Confirm " + title, Theme.Layer.Modal);
            UiKit.EnsureEventSystem();
            GameObject dialog = canvas.gameObject;

            UiKit.Scrim(canvas.transform);

            RectTransform card = UiKit.Panel(canvas.transform, "Card");
            UiKit.Anchor(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 330f));

            RectTransform column = UiKit.Column(card, "Body", Theme.Space.Base, Theme.Space.FramePadding + 12f, TextAnchor.UpperCenter);
            UiKit.Stretch(column);
            UiLayout.FillWidth(column);

            TextMeshProUGUI heading = UiKit.Display(column, Loc.Get(title), Theme.Type.Title);
            UiLayout.OneLine(heading, Theme.Type.Title);
            UiLayout.Fix(heading.rectTransform, 0f, 48f);

            TextMeshProUGUI text = UiKit.Body(column, Loc.Get(body), Theme.Type.Body + 2f, TextAlignmentOptions.Center);
            UiLayout.FlexibleHeight(text.rectTransform);

            RectTransform buttons = UiKit.Row(column, "Buttons", Theme.Space.Wide, 0f, TextAnchor.MiddleCenter);
            UiLayout.Fix(buttons, 0f, 64f);

            UiKit.SealButton(buttons, TextKey.CommonCancel, () =>
            {
                UiSfx.Play(UiSfx.Cue.Close);
                UnityEngine.Object.Destroy(dialog);
            }, 240f, 60f, 0f, "Button Cancel");

            UiKit.SealButton(buttons, confirmLabel, () =>
            {
                UnityEngine.Object.Destroy(dialog);
                if (onConfirm != null)
                {
                    onConfirm();
                }
            }, 240f, 60f, 0f, "Button Confirm");

            UiSfx.Play(UiSfx.Cue.Open);
            CoroutineHost.Run(UiTween.Punch(card));
            return dialog;
        }

        /// <summary>How far the toast sits above the bottom edge, in canvas units.</summary>
        /// <remarks>Low enough to clear the camp panels, which end <see cref="ToastClearance"/> above it.</remarks>
        public const float ToastBottom = 32f;

        /// <summary>The strip at the bottom of the screen a toast may cover.</summary>
        public const float ToastClearance = ToastBottom + ToastHeight + Theme.Space.Base;

        private const float ToastHeight = 76f;

        /// <summary>A short message that fades in at the bottom of the screen and away again.</summary>
        public static void Toast(string message, float seconds = 2.6f)
        {
            Canvas canvas = UiKit.Screen("Toast", Theme.Layer.Toast, raycaster: false);
            RectTransform card = UiKit.Panel(canvas.transform, "Card", blocksClicks: false);
            UiKit.Anchor(card, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, ToastBottom), new Vector2(760f, ToastHeight));

            TextMeshProUGUI text = UiKit.Body(card, message, Theme.Type.Body + 2f, TextAlignmentOptions.Center);
            UiKit.Stretch(text.rectTransform, Theme.Space.Wide);
            UiLayout.OneLine(text, Theme.Type.Body + 2f);

            CanvasGroup group = UiKit.Group(card.gameObject);
            CoroutineHost.Run(ToastRoutine(canvas.gameObject, group, seconds));
        }

        private static System.Collections.IEnumerator ToastRoutine(GameObject owner, CanvasGroup group, float seconds)
        {
            group.alpha = 0f;
            yield return UiTween.Fade(group, 1f, 0.18f);
            float end = Time.unscaledTime + seconds;
            while (owner != null && Time.unscaledTime < end)
            {
                yield return null;
            }

            if (owner == null)
            {
                yield break;
            }

            yield return UiTween.Fade(group, 0f, 0.3f);
            if (owner != null)
            {
                UnityEngine.Object.Destroy(owner);
            }
        }
    }
}
