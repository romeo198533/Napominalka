using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Napominalka.Core;

namespace Napominalka.App
{
    /// <summary>Создание и изменение напоминания.</summary>
    public class ReminderForm : Form
    {
        public Reminder Result;

        private readonly AppSettings _settings;
        private readonly Reminder _source;

        private TextBox _txtTitle;
        private ComboBox _cmbKind;

        private GroupBox _grpRepeat;
        private RadioButton _rbOnce, _rbDays, _rbDate;
        private DateTimePicker _onceDate, _onceTime;
        private CheckBox[] _dayBoxes;
        private DateTimePicker _daysTime;
        private DateTimePicker _exactDate, _exactTime;

        private GroupBox _grpMedicine;
        private TextBox _txtDose;
        private RadioButton _rbMedTimes, _rbMedHours, _rbMedDays;
        private TextBox _txtMedTimes;
        private NumericUpDown _numHours, _numMedDays;
        private DateTimePicker _dtCourseStart;
        private NumericUpDown _numCourseDays;

        private ComboBox _cmbMelody;
        private CheckBox _chkNag;
        private NumericUpDown _numNag;

        public ReminderForm(Reminder existing, AppSettings settings)
        {
            _settings = settings;
            _source = existing;
            BuildUi();
            FillFrom(existing);
            UpdateVisibility();
        }

        private void BuildUi()
        {
            Text = _source == null ? "Новое напоминание" : "Изменение напоминания";
            Width = 700;
            Height = 760;
            StartPosition = FormStartPosition.CenterParent;
            KeyPreview = true;
            MaximizeBox = false;

            int y = 12;
            _txtTitle = new TextBox { Left = 160, Top = y, Width = 500, AccessibleName = "Название напоминания" };
            y = AddLabeled("Название", _txtTitle, y);

            var lblKind = new Label { Text = "Тип события", Left = 12, Top = y + 4, Width = 140 };
            _cmbKind = new ComboBox { Left = 160, Top = y, Width = 500, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Тип события" };
            _cmbKind.Items.AddRange(new object[] { "Другое", "День рождения", "Годовщина", "Встреча", "Приём лекарств" });
            _cmbKind.SelectedIndexChanged += (s, e) => UpdateVisibility();
            Controls.Add(lblKind);
            Controls.Add(_cmbKind);
            y += 34;

            // ---- повторение ----
            _grpRepeat = new GroupBox { Text = "Повторение", Left = 12, Top = y, Width = 660, Height = 250, AccessibleName = "Повторение" };

            _rbOnce = new RadioButton { Text = "Один раз", Left = 16, Top = 26, Width = 160, Checked = true, AccessibleName = "Один раз" };
            _onceDate = new DateTimePicker { Left = 186, Top = 24, Width = 190, Format = DateTimePickerFormat.Short, AccessibleName = "Дата" };
            _onceTime = new DateTimePicker { Left = 386, Top = 24, Width = 120, Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, AccessibleName = "Время" };

            _rbDays = new RadioButton { Text = "Дни недели", Left = 16, Top = 70, Width = 160, AccessibleName = "Дни недели" };
            _dayBoxes = new CheckBox[7];
            var dayNames = new[] { "Понедельник", "Вторник", "Среда", "Четверг", "Пятница", "Суббота", "Воскресенье" };
            for (int i = 0; i < 7; i++)
            {
                _dayBoxes[i] = new CheckBox { Text = dayNames[i], Left = 186, Top = 68 + i * 22, Width = 200, AccessibleName = dayNames[i] };
                _grpRepeat.Controls.Add(_dayBoxes[i]);
            }
            var lblDaysTime = new Label { Text = "Время", Left = 400, Top = 70, Width = 60 };
            _daysTime = new DateTimePicker { Left = 460, Top = 66, Width = 120, Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, AccessibleName = "Время срабатывания" };

            _rbDate = new RadioButton { Text = "Конкретная дата", Left = 16, Top = 232, Width = 160, AccessibleName = "Конкретная дата" };
            _exactDate = new DateTimePicker { Left = 186, Top = 228, Width = 190, Format = DateTimePickerFormat.Short, AccessibleName = "Дата" };
            _exactTime = new DateTimePicker { Left = 386, Top = 228, Width = 120, Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, AccessibleName = "Время" };

            _grpRepeat.Controls.Add(_rbOnce);
            _grpRepeat.Controls.Add(_onceDate);
            _grpRepeat.Controls.Add(_onceTime);
            _grpRepeat.Controls.Add(_rbDays);
            _grpRepeat.Controls.Add(lblDaysTime);
            _grpRepeat.Controls.Add(_daysTime);
            _grpRepeat.Controls.Add(_rbDate);
            _grpRepeat.Controls.Add(_exactDate);
            _grpRepeat.Controls.Add(_exactTime);
            Controls.Add(_grpRepeat);
            y += 262;

            // ---- лекарство ----
            _grpMedicine = new GroupBox { Text = "Лекарство", Left = 12, Top = y, Width = 660, Height = 250, AccessibleName = "Лекарство", Visible = false };

            var lblDose = new Label { Text = "Доза", Left = 16, Top = 28, Width = 120 };
            _txtDose = new TextBox { Left = 186, Top = 24, Width = 440, AccessibleName = "Доза" };

            _rbMedTimes = new RadioButton { Text = "Несколько раз в день", Left = 16, Top = 64, Width = 220, Checked = true, AccessibleName = "Несколько раз в день" };
            _txtMedTimes = new TextBox { Left = 246, Top = 62, Width = 380, AccessibleName = "Времена приёма через запятую" };

            _rbMedHours = new RadioButton { Text = "Каждые", Left = 16, Top = 104, Width = 80, AccessibleName = "Каждые несколько часов" };
            _numHours = new NumericUpDown { Left = 106, Top = 102, Width = 60, Minimum = 1, Maximum = 24, Value = 6, AccessibleName = "Часов" };
            var lblHours = new Label { Text = "часов (отсчёт от первого времени приёма)", Left = 176, Top = 106, Width = 400 };

            _rbMedDays = new RadioButton { Text = "Раз в", Left = 16, Top = 144, Width = 80, AccessibleName = "Раз в несколько дней" };
            _numMedDays = new NumericUpDown { Left = 106, Top = 142, Width = 60, Minimum = 1, Maximum = 365, Value = 2, AccessibleName = "Дней" };
            var lblMedDays = new Label { Text = "дней", Left = 176, Top = 146, Width = 100 };

            var lblCourseStart = new Label { Text = "Начало курса", Left = 16, Top = 188, Width = 120 };
            _dtCourseStart = new DateTimePicker { Left = 186, Top = 184, Width = 190, Format = DateTimePickerFormat.Short, AccessibleName = "Начало курса" };
            var lblCourseDays = new Label { Text = "Дней курса", Left = 396, Top = 188, Width = 90 };
            _numCourseDays = new NumericUpDown { Left = 490, Top = 184, Width = 70, Minimum = 0, Maximum = 3650, Value = 0, AccessibleName = "Дней курса, ноль значит бессрочно" };
            var lblCourseHint = new Label { Text = "0 — бессрочно", Left = 566, Top = 188, Width = 90 };

            _grpMedicine.Controls.Add(lblDose);
            _grpMedicine.Controls.Add(_txtDose);
            _grpMedicine.Controls.Add(_rbMedTimes);
            _grpMedicine.Controls.Add(_txtMedTimes);
            _grpMedicine.Controls.Add(_rbMedHours);
            _grpMedicine.Controls.Add(_numHours);
            _grpMedicine.Controls.Add(lblHours);
            _grpMedicine.Controls.Add(_rbMedDays);
            _grpMedicine.Controls.Add(_numMedDays);
            _grpMedicine.Controls.Add(lblMedDays);
            _grpMedicine.Controls.Add(lblCourseStart);
            _grpMedicine.Controls.Add(_dtCourseStart);
            _grpMedicine.Controls.Add(lblCourseDays);
            _grpMedicine.Controls.Add(_numCourseDays);
            _grpMedicine.Controls.Add(lblCourseHint);
            Controls.Add(_grpMedicine);

            // ---- мелодия и дожимание ----
            var lblMelody = new Label { Text = "Мелодия", Left = 12, Top = y + 262, Width = 140 };
            _cmbMelody = new ComboBox { Left = 160, Top = y + 258, Width = 340, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Мелодия" };
            var btnPreview = new Button { Text = "Прослушать", Left = 510, Top = y + 256, Width = 110, AccessibleName = "Прослушать мелодию" };
            btnPreview.Click += (s, e) => PreviewMelody();
            Controls.Add(lblMelody);
            Controls.Add(_cmbMelody);
            Controls.Add(btnPreview);

            _chkNag = new CheckBox { Text = "Дожимать, если не ответил", Left = 160, Top = y + 294, Width = 260, AccessibleName = "Дожимать, если не ответил" };
            _numNag = new NumericUpDown { Left = 430, Top = y + 292, Width = 60, Minimum = 1, Maximum = 120, Value = 5, AccessibleName = "Минут между повторами" };
            var lblNag = new Label { Text = "минут между повторами", Left = 500, Top = y + 296, Width = 200 };
            Controls.Add(_chkNag);
            Controls.Add(_numNag);
            Controls.Add(lblNag);

            int by = y + 336;
            var btnOk = new Button { Text = "Сохранить", Left = 440, Top = by, Width = 110, Height = 32, AccessibleName = "Сохранить" };
            var btnCancel = new Button { Text = "Отмена", Left = 560, Top = by, Width = 110, Height = 32, AccessibleName = "Отмена" };
            btnOk.Click += (s, e) => Save();
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
            AcceptButton = btnOk;
            CancelButton = btnCancel;

            KeyDown += (s, e) => { if (e.KeyCode == Keys.F1) { new HelpForm().ShowDialog(this); e.Handled = true; } };
        }

        private int AddLabeled(string label, Control control, int y)
        {
            var lbl = new Label { Text = label, Left = 12, Top = y + 4, Width = 140 };
            Controls.Add(lbl);
            Controls.Add(control);
            return y + 34;
        }

        private void UpdateVisibility()
        {
            var isMedicine = _cmbKind.SelectedIndex == 4;
            _grpMedicine.Visible = isMedicine;
            _grpRepeat.Visible = !isMedicine;
        }

        private void FillFrom(Reminder r)
        {
            LoadMelodies();

            if (r == null)
            {
                _cmbKind.SelectedIndex = 0;
                _onceDate.Value = DateTime.Today;
                _onceTime.Value = DateTime.Today.AddHours(9);
                _daysTime.Value = DateTime.Today.AddHours(9);
                _exactDate.Value = DateTime.Today;
                _exactTime.Value = DateTime.Today.AddHours(9);
                _dtCourseStart.Value = DateTime.Today;
                _chkNag.Checked = _settings.NagDefault;
                _numNag.Value = Math.Max(1, _settings.NagMinutesDefault);
                _txtMedTimes.Text = "08:00, 14:00, 20:00";
                return;
            }

            _txtTitle.Text = r.Title;
            _cmbKind.SelectedIndex = KindIndex(r.Kind);

            _rbOnce.Checked = r.Repeat == RepeatMode.Once;
            _rbDays.Checked = r.Repeat == RepeatMode.Weekdays;
            _rbDate.Checked = r.Repeat == RepeatMode.ExactDate;

            _onceDate.Value = Clamp(r.OnceAt);
            _onceTime.Value = Clamp(r.OnceAt);
            _daysTime.Value = DateTime.Today + r.TimeOfDay;
            _exactDate.Value = Clamp(r.ExactDate);
            _exactTime.Value = DateTime.Today + r.TimeOfDay;

            foreach (var box in _dayBoxes) box.Checked = false;
            foreach (var d in r.Days)
            {
                int idx = ((int)d + 6) % 7;
                if (idx >= 0 && idx < 7) _dayBoxes[idx].Checked = true;
            }

            _txtDose.Text = r.Dose;
            _txtMedTimes.Text = string.Join(", ", r.MedTimes.Select(t => t.ToString(@"hh\:mm")));
            if (r.EveryHours > 0) { _rbMedHours.Checked = true; _numHours.Value = Math.Min(24, Math.Max(1, r.EveryHours)); }
            else if (r.EveryDays > 0) { _rbMedDays.Checked = true; _numMedDays.Value = Math.Min(365, Math.Max(1, r.EveryDays)); }
            else _rbMedTimes.Checked = true;

            _dtCourseStart.Value = Clamp(r.CourseStart);
            _numCourseDays.Value = Math.Min(3650, Math.Max(0, r.CourseDays));

            _chkNag.Checked = r.Nag;
            _numNag.Value = Math.Min(120, Math.Max(1, r.NagMinutes));

            var melody = string.IsNullOrWhiteSpace(r.Melody) ? _settings.DefaultMelody : r.Melody;
            SelectMelody(melody);
        }

        private static DateTime Clamp(DateTime d)
            => d < new DateTime(1900, 1, 1) || d > new DateTime(2999, 12, 31) ? DateTime.Today : d;

        private static int KindIndex(EventKind k)
        {
            switch (k)
            {
                case EventKind.Birthday: return 1;
                case EventKind.Anniversary: return 2;
                case EventKind.Meeting: return 3;
                case EventKind.Medicine: return 4;
                default: return 0;
            }
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
                        .Select(Path.GetFileName)
                        .OrderBy(x => x)
                        .ToList()
                    : new List<string>();

                foreach (var f in files) _cmbMelody.Items.Add(f);
                if (_cmbMelody.Items.Count == 0) _cmbMelody.Items.Add("(мелодий нет, положите файл в папку Мелодии)");
            }
            catch { }
        }

        private void SelectMelody(string name)
        {
            if (!string.IsNullOrEmpty(name) && _cmbMelody.Items.Contains(name)) _cmbMelody.SelectedItem = name;
            else if (_cmbMelody.Items.Count > 0) _cmbMelody.SelectedIndex = 0;
        }

        private void PreviewMelody()
        {
            var name = _cmbMelody.SelectedItem as string;
            if (string.IsNullOrEmpty(name) || name.StartsWith("(")) return;
            var path = Path.Combine(AppPaths.MelodyDir, name);
            if (!File.Exists(path))
            {
                MessageBox.Show(this, "Файл мелодии не найден.", "Напоминалка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            MelodyPlayer.Play(path);
        }

        private void Save()
        {
            var r = _source ?? new Reminder();

            if (string.IsNullOrWhiteSpace(_txtTitle.Text))
            {
                MessageBox.Show(this, "Впишите название напоминания.", "Напоминалка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtTitle.Focus();
                return;
            }

            r.Title = _txtTitle.Text.Trim();
            r.Kind = ReminderStore.ParseKind(_cmbKind.SelectedItem as string);
            r.Melody = _cmbMelody.SelectedItem as string;
            if (!string.IsNullOrEmpty(r.Melody) && r.Melody.StartsWith("(")) r.Melody = "";
            r.Nag = _chkNag.Checked;
            r.NagMinutes = (int)_numNag.Value;

            if (r.IsMedicine)
            {
                r.Dose = _txtDose.Text.Trim();
                r.MedTimes = ParseTimes(_txtMedTimes.Text);
                r.EveryHours = _rbMedHours.Checked ? (int)_numHours.Value : 0;
                r.EveryDays = _rbMedDays.Checked ? (int)_numMedDays.Value : 0;

                if (r.EveryHours == 0 && r.EveryDays == 0 && r.MedTimes.Count == 0)
                {
                    MessageBox.Show(this, "Укажите времена приёма, например 08:00, 14:00, 20:00.", "Напоминалка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _txtMedTimes.Focus();
                    return;
                }
                if (r.EveryHours > 0 && r.MedTimes.Count == 0) r.MedTimes.Add(new TimeSpan(8, 0, 0));

                r.CourseStart = _dtCourseStart.Value.Date;
                r.CourseDays = (int)_numCourseDays.Value;
                r.Repeat = RepeatMode.Once;
            }
            else
            {
                if (_rbDays.Checked)
                {
                    r.Repeat = RepeatMode.Weekdays;
                    r.Days = new List<DayOfWeek>();
                    for (int i = 0; i < 7; i++)
                        if (_dayBoxes[i].Checked)
                            r.Days.Add((DayOfWeek)((i + 1) % 7));

                    if (r.Days.Count == 0)
                    {
                        MessageBox.Show(this, "Отметьте хотя бы один день недели.", "Напоминалка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        _dayBoxes[0].Focus();
                        return;
                    }
                    r.TimeOfDay = _daysTime.Value.TimeOfDay;
                }
                else if (_rbDate.Checked)
                {
                    r.Repeat = RepeatMode.ExactDate;
                    r.ExactDate = _exactDate.Value.Date;
                    r.TimeOfDay = _exactTime.Value.TimeOfDay;
                }
                else
                {
                    r.Repeat = RepeatMode.Once;
                    r.OnceAt = _onceDate.Value.Date + _onceTime.Value.TimeOfDay;
                }
            }

            // Дата в прошлом — напоминание не сработает никогда. Слепому
            // человеку это по календарю не видно, поэтому спрашиваем вслух.
            DateTime? single = r.Repeat == RepeatMode.ExactDate
                ? r.ExactDate.Date + r.TimeOfDay
                : (r.Repeat == RepeatMode.Once ? r.OnceAt : (DateTime?)null);

            if (single.HasValue && single.Value <= DateTime.Now)
            {
                Speech.Speak("Это время уже прошло", _settings.SpeechMode);
                var answer = MessageBox.Show(this,
                    "Это время уже прошло: " + single.Value.ToString("dd.MM.yyyy в HH:mm", CultureInfo.GetCultureInfo("ru-RU")) +
                    ".\nТакое напоминание не сработает. Сохранить всё равно?",
                    "Напоминалка", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer != DialogResult.Yes) return;
            }

            Result = r;
            DialogResult = DialogResult.OK;
            Close();
        }

        private static List<TimeSpan> ParseTimes(string text)
        {
            var list = new List<TimeSpan>();
            foreach (var part in (text ?? "").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var t = ReminderStore.ParseTime(part.Trim());
                if (t.HasValue) list.Add(t.Value);
            }
            return list.OrderBy(x => x).ToList();
        }
    }
}
