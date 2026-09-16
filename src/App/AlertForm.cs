using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Napominalka.Core;

namespace Napominalka.App
{
    /// <summary>Окно сработавшего напоминания. Показывается поверх других.</summary>
    public class AlertForm : Form
    {
        public string Choice = "принял";

        private readonly Reminder _reminder;
        private readonly AppSettings _settings;
        private readonly bool _nag;
        private readonly int _nagMinutes;
        private readonly string _message;

        private int _signalLeft;
        private DateTime _nagDue;
        private System.Windows.Forms.Timer _timer;
        private string _missingMelody = "";
        private bool _noMelodiesAtAll;
        private bool _melodyWarned;

        public AlertForm(Reminder reminder, AppSettings settings, bool nag)
        {
            _reminder = reminder;
            _settings = settings;
            _nag = nag;
            // Свой интервал дожимания у напоминания важнее общего из настроек.
            _nagMinutes = reminder.Nag && reminder.NagMinutes > 0
                ? reminder.NagMinutes
                : settings.NagMinutesDefault;
            _message = BuildMessage();
            BuildUi();
        }

        private string BuildMessage()
        {
            var sb = new StringBuilder();
            var now = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);

            if (_reminder.IsMedicine)
            {
                sb.Append("Приём лекарства. ");
                sb.Append(string.IsNullOrWhiteSpace(_reminder.Title) ? "Название не указано" : _reminder.Title);
                if (!string.IsNullOrWhiteSpace(_reminder.Dose)) sb.Append(". Доза: ").Append(_reminder.Dose);
            }
            else
            {
                sb.Append(Reminder.KindName(_reminder.Kind)).Append(". ");
                sb.Append(string.IsNullOrWhiteSpace(_reminder.Title) ? "Без названия" : _reminder.Title);
            }

            sb.Append(". Время: ").Append(now).Append('.');
            return sb.ToString();
        }

        private void BuildUi()
        {
            Text = _reminder.IsMedicine ? "Приём лекарства" : "Напоминание";
            Width = 640;
            Height = 300;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = _settings.TopMostAlerts;
            ShowInTaskbar = true;
            KeyPreview = true;
            MaximizeBox = false;
            MinimizeBox = false;
            AccessibleName = Text;

            var box = new TextBox
            {
                Left = 16,
                Top = 16,
                Width = 590,
                Height = 120,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Text = _message,
                Font = new Font("Segoe UI", 12f),
                AccessibleName = "Текст напоминания",
                TabStop = true
            };
            Controls.Add(box);

            var bAccept = new Button { Text = "Принял, клавиша 1", Left = 16, Top = 156, Width = 190, Height = 40, AccessibleName = "Принял" };
            var bSnooze = new Button { Text = "Отложить, клавиша 2", Left = 216, Top = 156, Width = 190, Height = 40, AccessibleName = "Отложить" };
            var bSkip = new Button { Text = "Пропустить, клавиша 3", Left = 416, Top = 156, Width = 190, Height = 40, AccessibleName = "Пропустить" };

            bAccept.Click += (s, e) => Finish("принял");
            bSnooze.Click += (s, e) => Finish("отложить");
            bSkip.Click += (s, e) => Finish("пропустить");

            Controls.Add(bAccept);
            Controls.Add(bSnooze);
            Controls.Add(bSkip);

            var hint = new Label
            {
                Left = 16,
                Top = 206,
                Width = 590,
                Height = 40,
                Text = _nag
                    ? "Если не ответить, сигнал повторится через " + _nagMinutes + " минут."
                    : "Enter — принял, Esc — отложить."
            };
            Controls.Add(hint);

            AcceptButton = bAccept;
            KeyDown += AlertForm_KeyDown;
            Shown += AlertForm_Shown;
            FormClosed += (s, e) => { _timer?.Stop(); MelodyPlayer.Stop(); };
        }

        private void AlertForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.D1 || e.KeyCode == Keys.NumPad1) { Finish("принял"); e.Handled = true; }
            else if (e.KeyCode == Keys.D2 || e.KeyCode == Keys.NumPad2) { Finish("отложить"); e.Handled = true; }
            else if (e.KeyCode == Keys.D3 || e.KeyCode == Keys.NumPad3) { Finish("пропустить"); e.Handled = true; }
            else if (e.KeyCode == Keys.Escape) { Finish("отложить"); e.Handled = true; }
        }

        private void AlertForm_Shown(object sender, EventArgs e)
        {
            Activate();
            BringToFront();
            PlaySignal();

            _signalLeft = Math.Max(0, _settings.SignalRepeats - 1);
            _nagDue = DateTime.Now.AddMinutes(Math.Max(1, _nagMinutes));

            _timer = new System.Windows.Forms.Timer { Interval = 2000 };
            _timer.Tick += (s, ev) =>
            {
                if (_signalLeft > 0)
                {
                    PlaySignal();
                    _signalLeft--;
                    return;
                }
                if (_nag && DateTime.Now >= _nagDue)
                {
                    Speech.Speak(_message + MelodyWarning(), _settings.SpeechMode);
                    PlaySignal();
                    _nagDue = DateTime.Now.AddMinutes(Math.Max(1, _nagMinutes));
                }
            };
            _timer.Start();

            Speech.Speak(_message + MelodyWarning(), _settings.SpeechMode);
        }

        private void PlaySignal()
        {
            var file = ResolveMelody();
            if (!string.IsNullOrEmpty(file)) MelodyPlayer.Play(file);
        }

        private string ResolveMelody()
        {
            try
            {
                var name = string.IsNullOrWhiteSpace(_reminder.Melody) ? _settings.DefaultMelody : _reminder.Melody;
                if (string.IsNullOrWhiteSpace(name)) return "";
                var path = Path.Combine(AppPaths.MelodyDir, name);
                if (File.Exists(path)) return path;

                // Мелодию убрали или перенесли. Ищем замену, а о пропаже
                // скажем голосом — иначе непонятно, почему звучит не то.
                _missingMelody = name;

                var fallback = Path.Combine(AppPaths.MelodyDir, _settings.DefaultMelody);
                if (File.Exists(fallback)) return fallback;

                var any = Directory.Exists(AppPaths.MelodyDir)
                    ? Directory.GetFiles(AppPaths.MelodyDir, "*.wav")
                    : Array.Empty<string>();
                if (any.Length > 0) return any[0];

                _missingMelody = "";   // мелодий нет вообще, скажем об этом отдельно
                _noMelodiesAtAll = true;
                return "";
            }
            catch { return ""; }
        }

        /// <summary>
        /// Что сказать про мелодию. Пустая строка — всё в порядке.
        /// Говорим один раз за окно, чтобы дожимание не повторяло это каждый раз.
        /// </summary>
        private string MelodyWarning()
        {
            var text = "";
            if (_noMelodiesAtAll && !_melodyWarned)
            {
                text = " Мелодий не найдено, сигнал только голосом.";
            }
            else if (!string.IsNullOrEmpty(_missingMelody) && !_melodyWarned)
            {
                text = " Мелодия " + Path.GetFileNameWithoutExtension(_missingMelody) + " не найдена, звучит запасная.";
            }

            if (!string.IsNullOrEmpty(text)) _melodyWarned = true;
            return text;
        }

        private void Finish(string choice)
        {
            Choice = choice;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
