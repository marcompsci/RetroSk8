using System;
using RetroSk8.Audio;
using RetroSk8.Save;
using UnityEngine;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>Volume (music / effects / ambience), haptics, and a guarded progress reset. Saves when closed.</summary>
    public sealed class SettingsPanelView : MonoBehaviour
    {
        private Text _hapticsLabel;
        private Text _resetLabel;
        private float _resetArmedUntil;
        private Action _onClose;

        public void Build(RectTransform root, Action onClose)
        {
            _onClose = onClose;
            var dim = UIFactory.Panel("Dim", root, Theme.InkSoft, true);
            UIFactory.Stretch(dim.rectTransform);

            var panel = UIFactory.Panel("Settings", root, Theme.Ink, true);
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 860f));
            var title = UIFactory.TapeLabel("Title", panel.transform, "SETTINGS", 64, Theme.Tape, -2f);
            UIFactory.Place(title.transform.parent as RectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 100f));

            var s = SaveManager.Data.settings;
            AddSlider(panel.transform, "MUSIC", -160f, s.musicVolume, v => { s.musicVolume = v; AudioManager.Instance?.SetVolume(AudioBus.Music, v); });
            AddSlider(panel.transform, "EFFECTS", -260f, s.effectsVolume, v => { s.effectsVolume = v; AudioManager.Instance?.SetVolume(AudioBus.Effects, v); });
            AddSlider(panel.transform, "AMBIENCE", -360f, s.ambienceVolume, v => { s.ambienceVolume = v; AudioManager.Instance?.SetVolume(AudioBus.Ambience, v); });

            var haptics = UIFactory.MakeButton("Haptics", panel.transform, "", new Vector2(520f, 90f), Theme.Teal, ToggleHaptics, 40);
            UIFactory.Place((RectTransform)haptics.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -480f), new Vector2(520f, 90f));
            _hapticsLabel = haptics.GetComponentInChildren<Text>();

            var reset = UIFactory.MakeButton("Reset", panel.transform, "", new Vector2(520f, 80f), Theme.Coral, ResetProgress, 34);
            UIFactory.Place((RectTransform)reset.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -600f), new Vector2(520f, 80f));
            _resetLabel = reset.GetComponentInChildren<Text>();

            var back = UIFactory.MakeButton("Back", panel.transform, "DONE", new Vector2(360f, 100f), Theme.Tape, Close, 48);
            UIFactory.Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(360f, 100f));
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
            _hapticsLabel.text = SaveManager.Data.settings.hapticsEnabled ? "HAPTICS: ON" : "HAPTICS: OFF";
            _resetLabel.text = "RESET PROGRESS";
        }

        private void Close()
        {
            SaveManager.Save();
            _onClose?.Invoke();
        }
    }
}
