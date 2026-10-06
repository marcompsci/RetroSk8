using System;
using RetroSk8.Save;
using UnityEngine;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>
    /// Settings > SAVE &amp; BACKUP (Phase 22): back up to iCloud now, restore the iCloud backup, and copy or paste a
    /// SAVE CODE. Restoring and pasting replace your progress, so both ask twice and show what the backup holds first.
    /// </summary>
    public sealed class BackupPanelView : MonoBehaviour
    {
        private const float ArmSeconds = 4f;
        private Text _status, _restoreLabel, _pasteLabel, _result;
        private Action _onClose;
        private float _restoreArmedUntil, _pasteArmedUntil;
        private SaveData _pending;
        private string _pendingCode;

        public void Build(RectTransform root, Action onClose)
        {
            _onClose = onClose;
            var dim = UIFactory.Panel("Dim", root, Theme.InkSoft, true);
            UIFactory.Stretch(dim.rectTransform);
            var panel = UIFactory.Panel("Backup", root, Theme.Ink, true);
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 900f));
            var title = UIFactory.TapeLabel("Title", panel.transform, "SAVE & BACKUP", 54, Theme.Tape, -2f);
            UIFactory.Place(title.transform.parent as RectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 96f));

            _status = UIFactory.Label("Status", panel.transform, "", 30, Theme.Cream, TextAnchor.MiddleCenter, false);
            UIFactory.Place(_status.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(1000f, 50f));

            var col = UIFactory.Rect("Rows", panel.transform);
            UIFactory.Place(col, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(900f, 470f));
            var layout = col.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = false;

            UIFactory.MakeButton("BackupNow", col, "BACK UP TO ICLOUD NOW", new Vector2(820f, 96f), Theme.Teal, BackupNow, 36);
            _restoreLabel = UIFactory.MakeButton("Restore", col, "", new Vector2(820f, 96f), Theme.Coral, Restore, 32).GetComponentInChildren<Text>();
            UIFactory.MakeButton("CopyCode", col, "COPY SAVE CODE", new Vector2(820f, 96f), Theme.Cream, CopyCode, 36);
            _pasteLabel = UIFactory.MakeButton("PasteCode", col, "", new Vector2(820f, 96f), Theme.Coral, PasteCode, 32).GetComponentInChildren<Text>();

            _result = UIFactory.Label("Result", panel.transform, "", 32, Theme.Tape, TextAnchor.MiddleCenter, false);
            UIFactory.Place(_result.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(1040f, 50f));
            var note = UIFactory.Label("Note", panel.transform, "Ghosts aren't backed up. Paid packs come back with Restore Purchases.", 26, new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleCenter, false);
            UIFactory.Place(note.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1040f, 40f));
            var back = UIFactory.MakeButton("Back", panel.transform, "DONE", new Vector2(360f, 100f), Theme.Tape, Close, 48);
            UIFactory.Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(360f, 100f));
            Refresh();
        }

        private void OnEnable()
        {
            if (_status != null) { _result.text = ""; Disarm(); Refresh(); }
        }

        private void BackupNow()
        {
            CloudBackup.BackupNow(out string message);
            _result.text = message;
            Refresh();
        }

        private void Restore()
        {
            if (Time.unscaledTime > _restoreArmedUntil)
            {
                Disarm();
                if (!CloudBackup.PeekCloud(out _pending, out long made, out string error)) { _result.text = error; return; }
                _restoreArmedUntil = Time.unscaledTime + ArmSeconds;
                _restoreLabel.text = "TAP AGAIN: " + CloudBackup.Describe(_pending, made);
                _result.text = "THIS REPLACES YOUR PROGRESS ON THIS PHONE";
                return;
            }
            CloudBackup.Apply(_pending, fromICloud: true);
            Disarm();
            Reload("PROGRESS RESTORED FROM ICLOUD");
        }

        private void CopyCode()
        {
            string code = CloudBackup.MakeCode();
            GUIUtility.systemCopyBuffer = code;
            _result.text = $"SAVE CODE COPIED ({code.Length / 1024 + 1} KB). KEEP IT PRIVATE.";
        }

        private void PasteCode()
        {
            if (Time.unscaledTime > _pasteArmedUntil)
            {
                Disarm();
                _pendingCode = GUIUtility.systemCopyBuffer;
                if (!CloudBackup.Peek(_pendingCode, out _pending, out long made, out string error)) { _result.text = error; return; }
                _pasteArmedUntil = Time.unscaledTime + ArmSeconds;
                _pasteLabel.text = "TAP AGAIN: " + CloudBackup.Describe(_pending, made);
                _result.text = "THIS REPLACES YOUR PROGRESS ON THIS PHONE";
                return;
            }
            CloudBackup.Apply(_pending, fromICloud: false);
            Disarm();
            Reload("SAVE CODE LOADED");
        }

        /// <summary>Every open screen still holds the old progress, so start again from the main menu.</summary>
        private static void Reload(string message)
        {
            RetroSk8.Game.CareerService.Pending.Add(message);
            Theme.ApplySettings(SaveManager.Data.settings); // the restored text size and colours
            var st = SaveManager.Data.settings; // and its volumes (the audio manager outlives the scene)
            RetroSk8.Audio.AudioManager.Instance?.SetVolume(RetroSk8.Audio.AudioBus.Music, st.musicVolume);
            RetroSk8.Audio.AudioManager.Instance?.SetVolume(RetroSk8.Audio.AudioBus.Effects, st.effectsVolume);
            RetroSk8.Audio.AudioManager.Instance?.SetVolume(RetroSk8.Audio.AudioBus.Ambience, st.ambienceVolume);
            RetroSk8.Game.DevicePerformance.Instance?.RefreshPlan(); // and its frame-rate choice
            RetroSk8.Game.SceneRouter.Load(RetroSk8.Game.SceneNames.MainMenu);
        }

        private void Disarm()
        {
            _restoreArmedUntil = _pasteArmedUntil = 0f;
            _pending = null;
            _pendingCode = null;
            if (_restoreLabel != null) _restoreLabel.text = "RESTORE ICLOUD BACKUP";
            if (_pasteLabel != null) _pasteLabel.text = "PASTE SAVE CODE";
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            if ((_restoreArmedUntil > 0f && now > _restoreArmedUntil) || (_pasteArmedUntil > 0f && now > _pasteArmedUntil))
            {
                Disarm();
                _result.text = "";
            }
        }

        private void Refresh()
        {
            if (_restoreLabel != null && _restoreArmedUntil <= 0f) _restoreLabel.text = "RESTORE ICLOUD BACKUP";
            if (_pasteLabel != null && _pasteArmedUntil <= 0f) _pasteLabel.text = "PASTE SAVE CODE";
            if (!CloudBackup.Available) { _status.text = "ICLOUD BACKUP: OFF (SIGN IN TO ICLOUD ON AN IPHONE BUILD)"; return; }
            long last = CloudBackup.LastBackupUnix;
            _status.text = last <= 0 ? "ICLOUD BACKUP: TAP BACK UP NOW ONCE (OR RESTORE) TO START AUTOMATIC BACKUPS"
                : "ICLOUD BACKUP: ON · LAST " + RetroSk8.Core.SaveBackup.Ago(last, CloudBackup.NowUnix);
        }

        private void Close()
        {
            Disarm();
            _onClose?.Invoke();
        }
    }
}
