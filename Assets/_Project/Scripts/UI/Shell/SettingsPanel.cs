using System;
using System.Collections.Generic;
using BinakayanRising.Core.Localization;
using BinakayanRising.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BinakayanRising.UI.Shell
{
    /// <summary>
    /// Settings, over whatever is on screen: audio, display, language and text speed, and the
    /// save and tutorial controls.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An overlay rather than a state. It opens from the title screen, the encampment and (later)
    /// the battle, and a state would need an edge from each and back.
    /// </para>
    /// <para>
    /// Every control applies at once and writes <see cref="UserPrefs"/> at once: there is no
    /// Apply button to forget. Deleting the save is the one thing that asks first.
    /// </para>
    /// </remarks>
    public sealed class SettingsPanel : MonoBehaviour
    {
        private const float CardWidth = 1240f;
        private const float CardHeight = 700f;
        private const float ColumnWidth = 540f;
        private const float ControlWidth = 300f;

        private static readonly UserPrefs.TextSpeed[] Speeds =
        {
            UserPrefs.TextSpeed.Slow, UserPrefs.TextSpeed.Normal, UserPrefs.TextSpeed.Fast, UserPrefs.TextSpeed.Instant,
        };

        private GameShell shell;
        private RectTransform root;
        private RectTransform card;

        private Slider music;
        private Slider sfx;
        private Toggle mute;
        private UiControls.Stepper resolution;
        private Toggle fullscreen;
        private UiControls.Stepper language;
        private UiControls.Stepper textSpeed;
        private Button deleteSave;
        private TextMeshProUGUI saveNote;

        private readonly List<Vector2Int> resolutions = new List<Vector2Int>();
        private int resolutionIndex;
        private bool syncing;
        private int renderedVersion = -1;

        public bool IsOpen
        {
            get { return root != null && root.gameObject.activeSelf; }
        }

        /// <summary>The card, for screenshots and layout checks.</summary>
        public RectTransform Card
        {
            get { return card; }
        }

        public event Action Closed;

        public static SettingsPanel Create(GameShell shell)
        {
            Canvas canvas = UiKit.Screen("Settings", Theme.Layer.Settings);
            canvas.transform.SetParent(shell.transform, worldPositionStays: false);

            var panel = canvas.gameObject.AddComponent<SettingsPanel>();
            panel.shell = shell;
            panel.Build(canvas.transform);
            panel.root.gameObject.SetActive(false);
            return panel;
        }

        public void Open()
        {
            if (IsOpen)
            {
                return;
            }

            root.gameObject.SetActive(true);
            Sync();
            UiSfx.Play(UiSfx.Cue.Open);
            CoroutineHost.Run(UiTween.Punch(card));
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            root.gameObject.SetActive(false);
            UiSfx.Play(UiSfx.Cue.Close);

            Action handler = Closed;
            if (handler != null)
            {
                handler();
            }
        }

        private void Update()
        {
            if (IsOpen && UnityEngine.InputSystem.Keyboard.current != null
                && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
        }

        private void LateUpdate()
        {
            // Steppers show composed values; a language switch re-reads them.
            if (IsOpen && renderedVersion != Loc.Version)
            {
                Sync();
            }
        }

        // ------------------------------------------------------------------ build

        private void Build(Transform canvas)
        {
            root = UiKit.NewRect(canvas, "Settings");
            UiKit.Stretch(root);
            UiKit.Scrim(root);

            card = UiKit.Panel(root, "Card");
            UiKit.Anchor(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardWidth, CardHeight));

            RectTransform column = UiKit.Column(card, "Body", Theme.Space.Snug, Theme.Space.FramePadding + 16f, TextAnchor.UpperLeft);
            UiKit.Stretch(column);
            UiLayout.FillWidth(column);

            // Header.
            RectTransform header = UiKit.Row(column, "Header", Theme.Space.Snug, 0f, TextAnchor.MiddleLeft);
            UiLayout.Fix(header, 0f, 56f);
            Image sigil = UiKit.Sigil(header, 44f, Theme.Gold);
            UiLayout.Fix((RectTransform)sigil.transform, 44f, 44f);
            TextMeshProUGUI title = UiKit.Display(header, Loc.Get(TextKey.SetTitle), Theme.Type.Title, TextAlignmentOptions.Left);
            UiKit.Localize(title, TextKey.SetTitle);
            UiLayout.OneLine(title, Theme.Type.Title);
            UiLayout.Flexible(title.rectTransform);
            Button close = UiKit.IconButton(header, Theme.IconClose, Close, 52f, "Button Close X");
            UiLayout.Fix((RectTransform)close.transform, 52f, 52f);

            Image rule = UiKit.Divider(column);
            UiLayout.Fix(rule.rectTransform, 0f, 20f);

            // Two columns of sections.
            RectTransform columns = UiKit.Row(column, "Columns", Theme.Space.Huge, 0f, TextAnchor.UpperLeft);
            UiLayout.FlexibleHeight(columns);

            RectTransform left = Section(columns, "Left");
            RectTransform right = Section(columns, "Right");

            UiControls.SectionHeading(left, TextKey.SetAudio);
            music = UiControls.Slider(UiControls.LabelledRow(left, TextKey.SetMusic), "Music", ControlWidth, v => Apply(() => UserPrefs.MusicVolume = v));
            sfx = UiControls.Slider(UiControls.LabelledRow(left, TextKey.SetSfx), "Sfx", ControlWidth, v => Apply(() => UserPrefs.SfxVolume = v));
            mute = UiControls.Checkbox(UiControls.LabelledRow(left, TextKey.SetMute), "Mute", on => Apply(() => UserPrefs.Muted = on));
            Gap(left);

            UiControls.SectionHeading(left, TextKey.SetDisplay);
            resolution = UiControls.StepperControl(UiControls.LabelledRow(left, TextKey.SetResolution), "Resolution", ControlWidth, () => StepResolution(-1), () => StepResolution(1));
            fullscreen = UiControls.Checkbox(UiControls.LabelledRow(left, TextKey.SetFullscreen), "Fullscreen", on => Apply(() => SetFullscreen(on)));

            UiControls.SectionHeading(right, TextKey.SetLanguageText);
            language = UiControls.StepperControl(UiControls.LabelledRow(right, TextKey.SetLanguage), "Language", ControlWidth, ToggleLanguage, ToggleLanguage);
            textSpeed = UiControls.StepperControl(UiControls.LabelledRow(right, TextKey.SetTextSpeed), "Text Speed", ControlWidth, () => StepSpeed(-1), () => StepSpeed(1));
            Gap(right);

            UiControls.SectionHeading(right, TextKey.SetSaveTutorial);
            RectTransform saveRow = UiKit.Row(right, "Save Buttons", Theme.Space.Base, 0f, TextAnchor.MiddleLeft);
            UiLayout.Fix(saveRow, 0f, 64f);
            float half = (ColumnWidth - Theme.Space.Base) * 0.5f;
            UiKit.SealButton(saveRow, TextKey.SetReplayTutorial, ReplayTutorial, half, 56f, Theme.Type.Small + 2f, "Button Replay Tutorial");
            deleteSave = UiKit.SealButton(saveRow, TextKey.SetDeleteSave, AskDeleteSave, half, 56f, Theme.Type.Small + 2f, "Button Delete Save");

            saveNote = UiKit.Caption(right, string.Empty, TextAlignmentOptions.Right);
            UiLayout.Fix(saveNote.rectTransform, 0f, 24f);

            // Footer.
            RectTransform footer = UiKit.Row(column, "Footer", 0f, 0f, TextAnchor.MiddleCenter);
            UiLayout.Fix(footer, 0f, 64f);
            UiKit.SealButton(footer, TextKey.CommonClose, Close, 260f, 60f, 0f, "Button Close");
        }

        private static RectTransform Section(RectTransform parent, string name)
        {
            RectTransform section = UiKit.Column(parent, name, Theme.Space.Tight, 0f, TextAnchor.UpperLeft);
            UiLayout.Fix(section, ColumnWidth, 0f);
            UiLayout.FlexibleHeight(section);
            UiLayout.FillWidth(section);
            return section;
        }

        private static void Gap(RectTransform column)
        {
            UiLayout.Fix(UiKit.NewRect(column, "Gap"), 0f, Theme.Space.Wide);
        }

        // ------------------------------------------------------------------ state

        /// <summary>Reads every control back from the saved preferences.</summary>
        private void Sync()
        {
            syncing = true;

            music.SetValueWithoutNotify(UserPrefs.MusicVolume);
            sfx.SetValueWithoutNotify(UserPrefs.SfxVolume);
            mute.SetIsOnWithoutNotify(UserPrefs.Muted);

            CollectResolutions();
            resolution.Value.text = resolutions.Count > 0
                ? string.Format("{0} × {1}", resolutions[resolutionIndex].x, resolutions[resolutionIndex].y)
                : string.Format("{0} × {1}", Screen.width, Screen.height);
            resolution.Previous.interactable = resolutionIndex > 0;
            resolution.Next.interactable = resolutionIndex < resolutions.Count - 1;
            fullscreen.SetIsOnWithoutNotify(Screen.fullScreenMode != FullScreenMode.Windowed);

            language.Value.text = Loc.Get(Loc.Current == Language.Filipino ? TextKey.LangFilipino : TextKey.LangEnglish);
            textSpeed.Value.text = Loc.Get(SpeedKey(UserPrefs.DialogueSpeed));

            bool hasSave = shell.Session.HasSave;
            deleteSave.interactable = hasSave;
            saveNote.text = hasSave ? string.Empty : Loc.Get(TextKey.SetNoSave);

            renderedVersion = Loc.Version;
            syncing = false;
        }

        private void Apply(Action change)
        {
            if (syncing)
            {
                return;
            }

            change();
        }

        private void CollectResolutions()
        {
            resolutions.Clear();
            Resolution[] available = Screen.resolutions;
            for (int i = 0; i < available.Length; i++)
            {
                var size = new Vector2Int(available[i].width, available[i].height);
                if (size.x >= 1024 && !resolutions.Contains(size))
                {
                    resolutions.Add(size);
                }
            }

            resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

            var current = new Vector2Int(Screen.width, Screen.height);
            resolutionIndex = resolutions.IndexOf(current);
            if (resolutionIndex < 0)
            {
                // A window dragged to an odd size: show the nearest listed one.
                resolutionIndex = 0;
                int best = int.MaxValue;
                for (int i = 0; i < resolutions.Count; i++)
                {
                    int distance = Mathf.Abs(resolutions[i].x - current.x) + Mathf.Abs(resolutions[i].y - current.y);
                    if (distance < best)
                    {
                        best = distance;
                        resolutionIndex = i;
                    }
                }
            }
        }

        private void StepResolution(int delta)
        {
            int target = resolutionIndex + delta;
            if (target < 0 || target >= resolutions.Count)
            {
                return;
            }

            resolutionIndex = target;
            Vector2Int size = resolutions[target];
            Screen.SetResolution(size.x, size.y, Screen.fullScreenMode);
            resolution.Value.text = string.Format("{0} × {1}", size.x, size.y);
            resolution.Previous.interactable = resolutionIndex > 0;
            resolution.Next.interactable = resolutionIndex < resolutions.Count - 1;
        }

        private static void SetFullscreen(bool on)
        {
            Screen.fullScreenMode = on ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        }

        private void ToggleLanguage()
        {
            UserPrefs.ChooseLanguage(Loc.Current == Language.Filipino ? Language.English : Language.Filipino);
            Sync();
        }

        private void StepSpeed(int delta)
        {
            int index = Array.IndexOf(Speeds, UserPrefs.DialogueSpeed) + delta;
            index = Mathf.Clamp(index, 0, Speeds.Length - 1);
            UserPrefs.DialogueSpeed = Speeds[index];
            Sync();
        }

        private static TextKey SpeedKey(UserPrefs.TextSpeed speed)
        {
            switch (speed)
            {
                case UserPrefs.TextSpeed.Slow: return TextKey.SpeedSlow;
                case UserPrefs.TextSpeed.Fast: return TextKey.SpeedFast;
                case UserPrefs.TextSpeed.Instant: return TextKey.SpeedInstant;
                default: return TextKey.SpeedNormal;
            }
        }

        private void ReplayTutorial()
        {
            UserPrefs.ResetTutorial();
            UiControls.Toast(Loc.Get(TextKey.SetReplayDone));
        }

        private void AskDeleteSave()
        {
            UiControls.Confirm(TextKey.SetDeleteTitle, TextKey.SetDeleteBody, TextKey.SetDeleteSave, () =>
            {
                shell.DeleteSave();
                Close();
            });
        }
    }
}
