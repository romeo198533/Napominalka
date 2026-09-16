using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Napominalka.Core;

namespace Napominalka.App
{
    /// <summary>Настройки программы. Четыре раздела, чтобы главное окно не засорять.</summary>
    public class SettingsForm : Form
    {
        public AppSettings Settings;

        private readonly AppSettings _s;

        private CheckBox _chkAutostart, _chkTray, _chkTopMost, _chkUpdates;
        private TextBox _txtHotkey, _txtUpdateUrl;
        private ComboBox _cmbMelody, _cmbSpeech;
        private NumericUpDown _numRepeats, _numSnooze, _numJournalDays, _numNagDefault;
        private CheckBox _chkMedNag, _chkJournal, _chkNagDefault;
        private Label _lblPaths;

        public SettingsForm(AppSettings settings)
        {
            _s = settings;
            Settings = Clone(settings);
            BuildUi();
            Fill();
        }

        private static AppSettings Clone(AppSettings s) => new AppSettings
        {
            Autostart = s.Autostart,
            MinimizeToTray = s.MinimizeToTray,
            Hotkey = s.Hotkey,
            TopMostAlerts = s.TopMostAlerts,
            CheckUpdates = s.CheckUpdates,
            UpdateUrl = s.UpdateUrl,
            DefaultMelody = s.DefaultMelody,
            SignalRepeats = s.SignalRepeats,
            SpeechMode = s.SpeechMode,
            NagDefault = s.NagDefault,
            NagMinutesDefault = s.NagMinutesDefault,
            MedicineNag = s.MedicineNag,
            SnoozeMinutes = s.SnoozeMinutes,
            KeepJournal = s.KeepJournal,
            JournalDays = s.JournalDays
        };

        private void BuildUi()
        {
            Text = "Настройки";
            Width = 660;
            Height = 520;
            StartPosition = FormStartPosition.CenterParent;
            KeyPreview = true;
            MaximizeBox = false;
            MinimizeBox = false;

            var tabs = new TabControl { Left = 12, Top = 12, Width = 620, Height = 400, AccessibleName = "Разделы настроек" };

            tabs.TabPages.Add(BuildCommonTab());
            tabs.TabPages.Add(BuildSignalTab());
            tabs.TabPages.Add(BuildMedicineTab());
            tabs.TabPages.Add(BuildDataTab());
            Controls.Add(tabs);

            var btnOk = new Button { Text = "Сохранить", Left = 400, Top = 424, Width = 110, Height = 32, AccessibleName = "Сохранить настройки" };
            var btnCancel = new Button { Text = "Отмена", Left = 520, Top = 424, Width = 110, Height = 32, AccessibleName = "Отмена" };
            btnOk.Click += (s, e) => Save();
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
            AcceptButton = btnOk;
            CancelButton = btnCancel;

            KeyDown += (s, e) => { if (e.KeyCode == Keys.F1) { new HelpForm().ShowDialog(this); e.Handled = true; } };
        }

        private TabPage BuildCommonTab()
        {
            var page = new TabPage("Общие") { AccessibleName = "Общие" };

            _chkAutostart = new CheckBox { Text = "Запускать вместе с Windows", Left = 16, Top = 20, Width = 500, AccessibleName = "Запускать вместе с Windows" };
            _chkTray = new CheckBox { Text = "Сворачивать в трей при закрытии окна", Left = 16, Top = 50, Width = 500, AccessibleName = "Сворачивать в трей при закрытии окна" };
            _chkTopMost = new CheckBox { Text = "Показывать напоминание поверх других окон", Left = 16, Top = 80, Width = 500, AccessibleName = "Показывать напоминание поверх других окон" };

            var lblHotkey = new Label { Text = "Горячая клавиша вызова окна", Left = 16, Top = 118, Width = 240 };
            _txtHotkey = new TextBox { Left = 16, Top = 142, Width = 220, ReadOnly = true, AccessibleName = "Горячая клавиша" };
            var btnHotkey = new Button { Text = "Назначить", Left = 246, Top = 140, Width = 120, AccessibleName = "Назначить горячую клавишу" };
            btnHotkey.Click += (s, e) => AssignHotkey();

            var lblHint = new Label { Left = 16, Top = 176, Width = 560, Height = 40, Text = "Нажмите «Назначить», затем нажмите нужное сочетание клавиш." };

            _chkUpdates = new CheckBox { Text = "Проверять обновления при запуске", Left = 16, Top = 222, Width = 400, AccessibleName = "Проверять обновления при запуске" };

            var lblUpdates = new Label { Text = "Адрес обновлений", Left = 16, Top = 254, Width = 240 };
            _txtUpdateUrl = new TextBox { Left = 16, Top = 278, Width = 560, AccessibleName = "Адрес обновлений" };

            var lblUpdatesHint = new Label
            {
                Left = 16,
                Top = 306,
                Width = 560,
                Height = 60,
                Text = "Сюда вставляется адрес страницы выпусков на GitHub. Если поле пустое, " +
                       "проверка обновлений просто не выполняется. Токен и пароль здесь не нужны."
            };

            page.Controls.Add(_chkAutostart);
            page.Controls.Add(_chkTray);
            page.Controls.Add(_chkTopMost);
            page.Controls.Add(lblHotkey);
            page.Controls.Add(_txtHotkey);
            page.Controls.Add(btnHotkey);
            page.Controls.Add(lblHint);
            page.Controls.Add(_chkUpdates);
            page.Controls.Add(lblUpdates);
            page.Controls.Add(_txtUpdateUrl);
            page.Controls.Add(lblUpdatesHint);
            return page;
        }

        private TabPage BuildSignalTab()
        {
            var page = new TabPage("Сигнал") { AccessibleName = "Сигнал" };

            var lblMelody = new Label { Text = "Мелодия по умолчанию", Left = 16, Top = 22, Width = 240 };
            _cmbMelody = new ComboBox { Left = 16, Top = 46, Width = 380, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Мелодия по умолчанию" };
            var btnPreview = new Button { Text = "Прослушать", Left = 406, Top = 44, Width = 120, AccessibleName = "Прослушать мелодию" };
            btnPreview.Click += (s, e) => Preview();

            var lblRepeats = new Label { Text = "Сколько раз повторять сигнал", Left = 16, Top = 92, Width = 260 };
            _numRepeats = new NumericUpDown { Left = 286, Top = 90, Width = 70, Minimum = 1, Maximum = 20, AccessibleName = "Сколько раз повторять сигнал" };

            var lblSpeech = new Label { Text = "Озвучка напоминания", Left = 16, Top = 136, Width = 260 };
            _cmbSpeech = new ComboBox { Left = 286, Top = 134, Width = 240, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Озвучка напоминания" };
            _cmbSpeech.Items.AddRange(new object[] { "NVDA", "Системный", "Не озвучивать" });

            var lblSpeechHint = new Label
            {
                Left = 16,
                Top = 168,
                Width = 560,
                Height = 56,
                Text = "Если выбран NVDA, программа ищет файл nvdaControllerClient64.dll рядом с собой или в папке NVDA. " +
                       "Когда его нет, используется системный голос Windows."
            };

            // Дожимание по умолчанию — сюда попадают новые напоминания.
            _chkNagDefault = new CheckBox
            {
                Left = 16,
                Top = 230,
                Width = 560,
                Text = "Дожимать напоминание, пока не подтвердишь, — по умолчанию для новых напоминаний",
                AccessibleName = "Дожимать напоминание по умолчанию для новых напоминаний"
            };

            var lblNagDefault = new Label { Text = "Повторять дожимание через, минут", Left = 16, Top = 270, Width = 300 };
            _numNagDefault = new NumericUpDown { Left = 326, Top = 268, Width = 70, Minimum = 1, Maximum = 120, AccessibleName = "Повторять дожимание через минут" };

            page.Controls.Add(lblMelody);
            page.Controls.Add(_cmbMelody);
            page.Controls.Add(btnPreview);
            page.Controls.Add(lblRepeats);
            page.Controls.Add(_numRepeats);
            page.Controls.Add(lblSpeech);
            page.Controls.Add(_cmbSpeech);
            page.Controls.Add(lblSpeechHint);
            page.Controls.Add(_chkNagDefault);
            page.Controls.Add(lblNagDefault);
            page.Controls.Add(_numNagDefault);
            return page;
        }

        private TabPage BuildMedicineTab()
        {
            var page = new TabPage("Лекарства") { AccessibleName = "Лекарства" };

            _chkMedNag = new CheckBox { Text = "Дожимать напоминание о лекарстве, пока не подтвердишь приём", Left = 16, Top = 20, Width = 560, AccessibleName = "Дожимать напоминание о лекарстве" };

            var lblSnooze = new Label { Text = "Откладывать на, минут", Left = 16, Top = 60, Width = 240 };
            _numSnooze = new NumericUpDown { Left = 266, Top = 58, Width = 70, Minimum = 1, Maximum = 240, AccessibleName = "Откладывать на минут" };

            _chkJournal = new CheckBox { Text = "Вести журнал приёма", Left = 16, Top = 100, Width = 400, AccessibleName = "Вести журнал приёма" };

            var lblDays = new Label { Text = "Хранить журнал, дней", Left = 16, Top = 140, Width = 240 };
            _numJournalDays = new NumericUpDown { Left = 266, Top = 138, Width = 70, Minimum = 1, Maximum = 3650, AccessibleName = "Хранить журнал дней" };

            var lblJournal = new Label
            {
                Left = 16,
                Top = 180,
                Width = 560,
                Height = 60,
                Text = "Журнал лежит в папке программы, файл журнал.txt. Его можно открыть и показать врачу."
            };

            page.Controls.Add(_chkMedNag);
            page.Controls.Add(lblSnooze);
            page.Controls.Add(_numSnooze);
            page.Controls.Add(_chkJournal);
            page.Controls.Add(lblDays);
            page.Controls.Add(_numJournalDays);
            page.Controls.Add(lblJournal);
            return page;
        }

        private TabPage BuildDataTab()
        {
            var page = new TabPage("Данные") { AccessibleName = "Данные" };

            _lblPaths = new Label
            {
                Left = 16,
                Top = 20,
                Width = 570,
                Height = 90,
                AccessibleName = "Папка программы"
            };

            var btnFolder = new Button { Text = "Открыть папку программы", Left = 16, Top = 120, Width = 240, AccessibleName = "Открыть папку программы" };
            btnFolder.Click += (s, e) => OpenPath(AppPaths.BaseDir);

            var btnMelody = new Button { Text = "Открыть папку Мелодии", Left = 266, Top = 120, Width = 240, AccessibleName = "Открыть папку Мелодии" };
            btnMelody.Click += (s, e) => { AppPaths.EnsureDirs(); OpenPath(AppPaths.MelodyDir); };

            var btnJournal = new Button { Text = "Открыть журнал", Left = 16, Top = 160, Width = 240, AccessibleName = "Открыть журнал" };
            btnJournal.Click += (s, e) => OpenPath(AppPaths.JournalFile);

            var btnClear = new Button { Text = "Очистить журнал", Left = 266, Top = 160, Width = 240, AccessibleName = "Очистить журнал" };
            btnClear.Click += (s, e) => ClearJournal();

            var btnReset = new Button { Text = "Сбросить настройки", Left = 16, Top = 200, Width = 490, AccessibleName = "Сбросить настройки" };
            btnReset.Click += (s, e) => ResetSettings();

            var lblHint = new Label
            {
                Left = 16,
                Top = 244,
                Width = 570,
                Height = 80,
                Text = "Экспорт и импорт списка напоминаний — в меню «Файл» главного окна."
            };

            page.Controls.Add(_lblPaths);
            page.Controls.Add(btnFolder);
            page.Controls.Add(btnMelody);
            page.Controls.Add(btnJournal);
            page.Controls.Add(btnClear);
            page.Controls.Add(btnReset);
            page.Controls.Add(lblHint);
            return page;
        }

        private void Fill()
        {
            _chkAutostart.Checked = _s.Autostart;
            _chkTray.Checked = _s.MinimizeToTray;
            _chkTopMost.Checked = _s.TopMostAlerts;
            _txtHotkey.Text = _s.Hotkey;
            _chkUpdates.Checked = _s.CheckUpdates;
            _txtUpdateUrl.Text = _s.UpdateUrl;

            LoadMelodies();

            _numRepeats.Value = Math.Min(20, Math.Max(1, _s.SignalRepeats));
            _cmbSpeech.SelectedItem = _cmbSpeech.Items.Contains(_s.SpeechMode) ? _s.SpeechMode : "NVDA";
            _chkNagDefault.Checked = _s.NagDefault;
            _numNagDefault.Value = Math.Min(120, Math.Max(1, _s.NagMinutesDefault));

            _chkMedNag.Checked = _s.MedicineNag;
            _numSnooze.Value = Math.Min(240, Math.Max(1, _s.SnoozeMinutes));
            _chkJournal.Checked = _s.KeepJournal;
            _numJournalDays.Value = Math.Min(3650, Math.Max(1, _s.JournalDays));

            _lblPaths.Text = "Папка программы: " + AppPaths.BaseDir + Environment.NewLine +
                             "Напоминания: " + AppPaths.RemindersFile + Environment.NewLine +
                             "Настройки: " + AppPaths.SettingsFile;
        }

        private void LoadMelodies()
        {
            _cmbMelody.Items.Clear();
            try
            {
                AppPaths.EnsureDirs();
                var files = Directory.Exists(AppPaths.MelodyDir)
                    ? Directory.GetFiles(AppPaths.MelodyDir)
                        .Where(f => new[] { ".wav", ".mp3", ".wma" }.Contains(Path.GetExtension(f).ToLowerInvariant()))
                        .Select(Path.GetFileName).OrderBy(x => x).ToList()
                    : new List<string>();
                foreach (var f in files) _cmbMelody.Items.Add(f);
                if (_cmbMelody.Items.Count == 0) _cmbMelody.Items.Add("(мелодий нет)");
                if (_cmbMelody.Items.Contains(_s.DefaultMelody)) _cmbMelody.SelectedItem = _s.DefaultMelody;
                else _cmbMelody.SelectedIndex = 0;
            }
            catch { }
        }

        private void Preview()
        {
            var name = _cmbMelody.SelectedItem as string;
            if (string.IsNullOrEmpty(name) || name.StartsWith("(")) return;
            var path = Path.Combine(AppPaths.MelodyDir, name);
            if (File.Exists(path)) MelodyPlayer.Play(path);
            else MessageBox.Show(this, "Файл мелодии не найден.", "Напоминалка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void AssignHotkey()
        {
            using var dlg = new HotkeyForm(_txtHotkey.Text);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            _txtHotkey.Text = dlg.Hotkey;
        }

        private void OpenPath(string path)
        {
            try
            {
                if (Directory.Exists(path)) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                else if (File.Exists(path)) Process.Start(new ProcessStartInfo("notepad.exe", "\"" + path + "\"") { UseShellExecute = true });
                else MessageBox.Show(this, "Файл или папка не найдены.", "Напоминалка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Не удалось открыть: " + ex.Message, "Напоминалка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ClearJournal()
        {
            var answer = MessageBox.Show(this, "Удалить все записи журнала?", "Напоминалка", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes) return;
            try { if (File.Exists(AppPaths.JournalFile)) File.Delete(AppPaths.JournalFile); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Напоминалка", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void ResetSettings()
        {
            var answer = MessageBox.Show(this, "Вернуть все настройки к исходным?", "Напоминалка", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes) return;
            var fresh = new AppSettings();
            _chkAutostart.Checked = fresh.Autostart;
            _chkTray.Checked = fresh.MinimizeToTray;
            _chkTopMost.Checked = fresh.TopMostAlerts;
            _txtHotkey.Text = fresh.Hotkey;
            _chkUpdates.Checked = fresh.CheckUpdates;
            _txtUpdateUrl.Text = fresh.UpdateUrl;
            _numRepeats.Value = fresh.SignalRepeats;
            _cmbSpeech.SelectedItem = fresh.SpeechMode;
            _chkNagDefault.Checked = fresh.NagDefault;
            _numNagDefault.Value = fresh.NagMinutesDefault;
            _chkMedNag.Checked = fresh.MedicineNag;
            _numSnooze.Value = fresh.SnoozeMinutes;
            _chkJournal.Checked = fresh.KeepJournal;
            _numJournalDays.Value = fresh.JournalDays;
        }

        private void Save()
        {
            Settings.Autostart = _chkAutostart.Checked;
            Settings.MinimizeToTray = _chkTray.Checked;
            Settings.TopMostAlerts = _chkTopMost.Checked;
            Settings.CheckUpdates = _chkUpdates.Checked;
            Settings.UpdateUrl = _txtUpdateUrl.Text.Trim();

            var hotkey = _txtHotkey.Text.Trim();
            if (!MainForm.TryParseHotkey(hotkey, out _, out _))
            {
                MessageBox.Show(this, "Сочетание клавиш не распознано. Назначьте заново.", "Напоминалка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Settings.Hotkey = hotkey;

            var melody = _cmbMelody.SelectedItem as string;
            Settings.DefaultMelody = (string.IsNullOrEmpty(melody) || melody.StartsWith("(")) ? "" : melody;
            Settings.SignalRepeats = (int)_numRepeats.Value;
            Settings.SpeechMode = _cmbSpeech.SelectedItem as string ?? "NVDA";
            Settings.NagDefault = _chkNagDefault.Checked;
            Settings.NagMinutesDefault = (int)_numNagDefault.Value;

            Settings.MedicineNag = _chkMedNag.Checked;
            Settings.SnoozeMinutes = (int)_numSnooze.Value;
            Settings.KeepJournal = _chkJournal.Checked;
            Settings.JournalDays = (int)_numJournalDays.Value;

            DialogResult = DialogResult.OK;
            Close();
        }
    }

    /// <summary>Маленькое окно: нажми сочетание клавиш.</summary>
    public class HotkeyForm : Form
    {
        public string Hotkey = "";

        private Label _lbl;
        private bool _captured;

        public HotkeyForm(string current)
        {
            Text = "Назначение горячей клавиши";
            Width = 520;
            Height = 220;
            StartPosition = FormStartPosition.CenterParent;
            KeyPreview = true;
            MaximizeBox = false;
            MinimizeBox = false;

            _lbl = new Label
            {
                Left = 16,
                Top = 20,
                Width = 470,
                Height = 60,
                Text = "Нажмите нужное сочетание клавиш. Сейчас назначено: " + current,
                AccessibleName = "Нажмите сочетание клавиш"
            };
            Controls.Add(_lbl);

            var ok = new Button { Text = "Сохранить", Left = 250, Top = 110, Width = 110, Height = 32, AccessibleName = "Сохранить" };
            var cancel = new Button { Text = "Отмена", Left = 370, Top = 110, Width = 110, Height = 32, AccessibleName = "Отмена" };
            ok.Click += (s, e) => { if (_captured) { DialogResult = DialogResult.OK; Close(); } };
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;

            KeyDown += HotkeyForm_KeyDown;
        }

        private void HotkeyForm_KeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;

            if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); return; }
            if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.Menu) return;

            var parts = new List<string>();
            if (e.Control) parts.Add("Control");
            if (e.Alt) parts.Add("Alt");
            if (e.Shift) parts.Add("Shift");
            if (parts.Count == 0) return;

            string keyName;
            if (e.KeyCode >= Keys.A && e.KeyCode <= Keys.Z) keyName = e.KeyCode.ToString();
            else if (e.KeyCode >= Keys.F1 && e.KeyCode <= Keys.F12) keyName = e.KeyCode.ToString();
            else if (e.KeyCode == Keys.Space) keyName = "Пробел";
            else return;

            parts.Add(keyName);
            Hotkey = string.Join("+", parts);
            _captured = true;
            _lbl.Text = "Назначено: " + Hotkey;
            Speech.Speak("Назначено " + Hotkey, "Системный");
        }
    }
}
