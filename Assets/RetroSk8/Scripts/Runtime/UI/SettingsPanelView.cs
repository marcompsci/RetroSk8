using System;
using RetroSk8.Audio;
using RetroSk8.Replay;
using RetroSk8.Game;
using RetroSk8.Save;
using UnityEngine;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>Volume (music / effects / ambience), haptics, best-run ghost, run clips, and a guarded progress reset. Saves when closed.</summary>
    public sealed class SettingsPanelView : MonoBehaviour
    {
        private Text _hapticsLabel;
        private Text _ghostLabel;
        private Text _clipsLabel;
        private Text _resetLabel;
        private Text _musicLabel;
        private Text _crowdLabel;
        private Text _remindLabel;
        private GameObject _skip;
        private GameObject _accessPanel;
        private float _resetArmedUntil;
        private Action _onClose;

        public void Build(RectTransform root, Action onClose)
        {
            _onClose = onClose;
            var dim = UIFactory.Panel("Dim", root, Theme.InkSoft, true);
            UIFactory.Stretch(dim.rectTransform);

            var panel = UIFactory.Panel("Settings", root, Theme.Ink, true);
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 1040f));
            var title = UIFactory.TapeLabel("Title", panel.transform, "SETTINGS", 64, Theme.Tape, -2f);
            UIFactory.Place(title.transform.parent as RectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 100f));

            var s = SaveManager.Data.settings;
            AddSlider(panel.transform, "MUSIC", -150f, s.musicVolume, v => { s.musicVolume = v; AudioManager.Instance?.SetVolume(AudioBus.Music, v); });
            AddSlider(panel.transform, "EFFECTS", -240f, s.effectsVolume, v => { s.effectsVolume = v; AudioManager.Instance?.SetVolume(AudioBus.Effects, v); });
            AddSlider(panel.transform, "AMBIENCE", -330f, s.ambienceVolume, v => { s.ambienceVolume = v; AudioManager.Instance?.SetVolume(AudioBus.Ambience, v); });

            _hapticsLabel = ToggleButton(panel.transform, "Haptics", -340f, Theme.Teal, ToggleHaptics);
            _ghostLabel = ToggleButton(panel.transform, "Ghost", 0f, Theme.Teal, ToggleGhost);
            // Clip recording only exists where ReplayKit does (iOS devices).
            if (ClipRecorder.IsSupported) _clipsLabel = ToggleButton(panel.transform, "Clips", 340f, Theme.Teal, ToggleClips);

            // Music: park themes, the in-game radio (three stations), or off; plus the distant crowd.
            var music = UIFactory.MakeButton("MusicMode", panel.transform, "", new Vector2(520f, 90f), Theme.Cream, CycleMusic, 32);
            UIFactory.Place((RectTransform)music.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-185f, -545f), new Vector2(520f, 90f));
            _musicLabel = music.GetComponentInChildren<Text>();
            var skip = UIFactory.MakeButton("Skip", panel.transform, "NEXT SONG", new Vector2(200f, 90f), Theme.Cream, () => AudioManager.Instance?.SkipSong(), 28);
            UIFactory.Place((RectTransform)skip.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(205f, -545f), new Vector2(200f, 90f));
            _skip = skip.gameObject;
            var crowd = UIFactory.MakeButton("Crowd", panel.transform, "", new Vector2(220f, 90f), Theme.Teal, ToggleCrowd, 28);
            UIFactory.Place((RectTransform)crowd.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(430f, -545f), new Vector2(220f, 90f));
            _crowdLabel = crowd.GetComponentInChildren<Text>();

            var access = UIFactory.MakeButton("Access", panel.transform, "CONTROLS & ACCESSIBILITY", new Vector2(720f, 90f), Theme.Tape, () => _accessPanel.SetActive(true), 36);
            UIFactory.Place((RectTransform)access.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -650f), new Vector2(720f, 90f));

            // Phase 15: opt-in reminders share the reset row where notifications exist (iOS devices).
            bool reminders = NotificationService.IsSupported;
            float resetX = reminders ? 250f : 0f, resetW = reminders ? 460f : 520f;
            var reset = UIFactory.MakeButton("Reset", panel.transform, "", new Vector2(resetW, 80f), Theme.Coral, ResetProgress, 34);
            UIFactory.Place((RectTransform)reset.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(resetX, -755f), new Vector2(resetW, 80f));
            if (reminders)
            {
                var remind = UIFactory.MakeButton("Reminders", panel.transform, "", new Vector2(460f, 80f), Theme.Teal, ToggleReminders, 32);
                UIFactory.Place((RectTransform)remind.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-250f, -755f), new Vector2(460f, 80f));
                _remindLabel = remind.GetComponentInChildren<Text>();
            }

            var accessRoot = UIFactory.Rect("AccessPanel", root);
            UIFactory.Stretch(accessRoot);
            accessRoot.gameObject.AddComponent<AccessibilityPanelView>().Build(accessRoot, () => _accessPanel.SetActive(false));
            _accessPanel = accessRoot.gameObject;
            _accessPanel.SetActive(false);
            _resetLabel = reset.GetComponentInChildren<Text>();

            var back = UIFactory.MakeButton("Back", panel.transform, "DONE", new Vector2(360f, 100f), Theme.Tape, Close, 48);
            UIFactory.Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(360f, 100f));
            Refresh();
        }

        private static void AddSlider(Transform parent, string label, float y, float value, Action<float> onChange)
        {
            var text = UIFactory.Label(label, parent, label, 40, Theme.Cream, TextAnchor.MiddleLeft, false);
            UIFactory.Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(90f, y), new Vector2(300f, 60f));
            var go = DefaultControls.CreateSlider(new DefaultControls.Resources());
            go.transform.SetParent(parent, false);
            UIFactory.Place((RectTransform)go.transform, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-90f, y), new Vector2(560f, 44f));
            foreach (var img in go.GetComponentsInChildren<Image>())
            {
                if (img.gameObject.name == "Background") img.color = new Color(1f, 1f, 1f, 0.2f);
                else if (img.gameObject.name == "Fill") img.color = Theme.Teal;
                else if (img.gameObject.name == "Handle") img.color = Theme.Tape;
            }
            var slider = go.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(v => onChange(v));
        }

        private static Text ToggleButton(Transform parent, string name, float x, Color color, Action onClick)
        {
            var b = UIFactory.MakeButton(name, parent, "", new Vector2(320f, 90f), color, onClick, 32);
            UIFactory.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(x, -440f), new Vector2(320f, 90f));
            return b.GetComponentInChildren<Text>();
        }

        private void ToggleGhost()
        {
            var s = SaveManager.Data.settings;
            s.ghostHidden = !s.ghostHidden;
            Refresh();
        }

        private void ToggleClips()
        {
            var s = SaveManager.Data.settings;
            s.recordClips = !s.recordClips;
            Refresh();
        }

        private void CycleMusic()
        {
            AudioManager.Ensure().CycleMusicMode();
            Refresh();
        }

        private void ToggleCrowd()
        {
            var s = SaveManager.Data.settings;
            s.crowdOff = !s.crowdOff;
            Refresh();
        }

        private void ToggleReminders()
        {
            NotificationService.SetEnabled(!SaveManager.Data.settings.reminders);
            Refresh();
        }

        private void ToggleHaptics()
        {
            var s = SaveManager.Data.settings;
            s.hapticsEnabled = !s.hapticsEnabled;
            Refresh();
        }

        private void ResetProgress()
        {
            if (Time.unscaledTime > _resetArmedUntil)
            {
                _resetArmedUntil = Time.unscaledTime + 3f;
                _resetLabel.text = "TAP AGAIN TO ERASE";
                return;
            }
            SaveManager.ResetAll();
            _resetArmedUntil = 0f;
            _resetLabel.text = "PROGRESS RESET";
        }

        private void Update()
        {
            if (_resetArmedUntil > 0f && Time.unscaledTime > _resetArmedUntil && _resetLabel.text == "TAP AGAIN TO ERASE")
            {
                _resetArmedUntil = 0f;
                Refresh();
            }
        }

        private void Refresh()
        {
            var settings = SaveManager.Data.settings;
            _hapticsLabel.text = settings.hapticsEnabled ? "HAPTICS: ON" : "HAPTICS: OFF";
            _ghostLabel.text = settings.ghostHidden ? "GHOST: OFF" : "GHOST: ON";
            if (_clipsLabel != null) _clipsLabel.text = settings.recordClips ? "CLIPS: ON" : "CLIPS: OFF";
            _resetLabel.text = "RESET PROGRESS";
            _musicLabel.text = RetroSk8.Core.Radio.ModeName((RetroSk8.Core.MusicMode)settings.musicMode, settings.radioStation);
            _skip.SetActive(settings.musicMode == (int)RetroSk8.Core.MusicMode.Radio);
            _crowdLabel.text = settings.crowdOff ? "CROWD: OFF" : "CROWD: ON";
            if (_remindLabel != null)
                _remindLabel.text = !settings.reminders ? "REMINDERS: OFF" : NotificationService.AuthState == 3 ? "REMINDERS: BLOCKED IN iOS" : "REMINDERS: ON";
        }

        private void Close()
        {
            SaveManager.Save();
            _onClose?.Invoke();
        }
    }
}
