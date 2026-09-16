using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Napominalka.Core;

namespace Napominalka.App
{
    public class MainForm : Form
    {
        private const int HotkeyId = 0x4E50;
        private const uint MOD_ALT = 0x0001, MOD_CONTROL = 0x0002, MOD_SHIFT = 0x0004, MOD_WIN = 0x0008;
        private const int WM_HOTKEY = 0x0312;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private AppSettings _settings;
        private List<Reminder> _reminders;
        private readonly Dictionary<string, DateTime> _lastFired = new Dictionary<string, DateTime>();
        private readonly Dictionary<string, DateTime> _snoozed = new Dictionary<string, DateTime>();
        // Минута назад, а не «ровно сейчас»: напоминание, срок которого прошёл
        // за несколько секунд до запуска, должно сработать живьём, а не уехать
        // в отчёт о пропущенном.
        private DateTime _startedAt = DateTime.Now.AddMinutes(-1);
        private DateTime? _pausedUntil;
        private bool _alertOpen;
        private bool _reallyExit;
        private bool _hotkeyRegistered;
        private bool _firstRun;

        private MenuStrip _menu;
        private ListBox _list;
        private Label _status;
        private System.Windows.Forms.Timer _timer;
        private NotifyIcon _tray;
        private ToolStripMenuItem _pauseItem;

        public MainForm()
        {
            // Первый запуск: файла настроек ещё нет, значит программу только что распаковали.
            _firstRun = !File.Exists(AppPaths.SettingsFile);

            _settings = AppSettings.Load(AppPaths.SettingsFile);
            _reminders = ReminderStore.Load(AppPaths.RemindersFile);
            LoadState();
            BuildUi();
        }

        // ---------- интерфейс ----------

        private void BuildUi()
        {
            Text = "Напоминалка";
            Width = 820;
            Height = 560;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = true;
            KeyPreview = true;
            AccessibleName = "Напоминалка";

            _menu = new MenuStrip();
            var mFile = new ToolStripMenuItem("Файл");
            mFile.DropDownItems.Add("Экспорт списка...", null, (s, e) => ExportList());
            mFile.DropDownItems.Add("Импорт списка...", null, (s, e) => ImportList());
            mFile.DropDownItems.Add(new ToolStripSeparator());
            mFile.DropDownItems.Add("Выход", null, (s, e) => { _reallyExit = true; Close(); });

            var mRem = new ToolStripMenuItem("Напоминание");
            mRem.DropDownItems.Add("Создать", null, (s, e) => CreateReminder());
            mRem.DropDownItems.Add("Изменить", null, (s, e) => EditReminder());
            mRem.DropDownItems.Add("Удалить", null, (s, e) => DeleteReminder());
            mRem.DropDownItems.Add("Включить или выключить", null, (s, e) => ToggleEnabled());

            var mSet = new ToolStripMenuItem("Настройки");
            mSet.DropDownItems.Add("Настройки...", null, (s, e) => OpenSettings());
            _pauseItem = new ToolStripMenuItem("Пауза на час", null, (s, e) => PauseFor(TimeSpan.FromHours(1)));
            mSet.DropDownItems.Add(_pauseItem);
            mSet.DropDownItems.Add("Пауза до утра", null, (s, e) => PauseUntilMorning());
            mSet.DropDownItems.Add("Снять паузу", null, (s, e) => { _pausedUntil = null; RefreshStatus(); });
            mSet.DropDownItems.Add(new ToolStripSeparator());
            mSet.DropDownItems.Add("Проверить обновления", null, (s, e) => CheckUpdatesNow());

            var mHelp = new ToolStripMenuItem("Справка");
            mHelp.DropDownItems.Add("Инструкция, клавиша F1", null, (s, e) => ShowHelp());
            mHelp.DropDownItems.Add("О программе", null, (s, e) => ShowAbout());

            _menu.Items.Add(mFile);
            _menu.Items.Add(mRem);
            _menu.Items.Add(mSet);
            _menu.Items.Add(mHelp);
            MainMenuStrip = _menu;
            Controls.Add(_menu);

            _list = new ListBox
            {
                Left = 12,
                Top = 34,
                Width = 780,
                Height = 380,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                IntegralHeight = false,
                AccessibleName = "Список напоминаний",
                AccessibleDescription = "Стрелки вверх и вниз для перехода по списку"
            };
            _list.SelectedIndexChanged += (s, e) => RefreshStatus();
            _list.DoubleClick += (s, e) => EditReminder();
            Controls.Add(_list);

            int y = 424;
            var bCreate = MakeButton("Создать", 12, y, (s, e) => CreateReminder(), "Создать новое напоминание");
            var bEdit = MakeButton("Изменить", 122, y, (s, e) => EditReminder(), "Изменить выбранное напоминание");
            var bDelete = MakeButton("Удалить", 232, y, (s, e) => DeleteReminder(), "Удалить выбранное напоминание");
            var bToggle = MakeButton("Включить или выключить", 342, y, (s, e) => ToggleEnabled(), "Включить или выключить выбранное напоминание", 210);

            var bSettings = MakeButton("Настройки", 12, y + 38, (s, e) => OpenSettings(), "Открыть настройки программы");
            var bHelp = MakeButton("Справка", 122, y + 38, (s, e) => ShowHelp(), "Открыть инструкцию, клавиша F1");
            var bTray = MakeButton("Свернуть в трей", 232, y + 38, (s, e) => HideToTray(), "Свернуть программу в область уведомлений");

            Controls.Add(bCreate);
            Controls.Add(bEdit);
            Controls.Add(bDelete);
            Controls.Add(bToggle);
            Controls.Add(bSettings);
            Controls.Add(bHelp);
            Controls.Add(bTray);

            _status = new Label
            {
                Left = 12,
                Top = 500,
                Width = 780,
                Height = 24,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                AccessibleName = "Состояние",
                Text = "Готово"
            };
            Controls.Add(_status);

            _tray = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "Напоминалка",
                Visible = true
            };
            var trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Открыть напоминалку", null, (s, e) => ShowFromTray());
            trayMenu.Items.Add("Пауза на час", null, (s, e) => PauseFor(TimeSpan.FromHours(1)));
            trayMenu.Items.Add("Пауза до утра", null, (s, e) => PauseUntilMorning());
            trayMenu.Items.Add("Снять паузу", null, (s, e) => { _pausedUntil = null; RefreshStatus(); });
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("Выход", null, (s, e) => { _reallyExit = true; Close(); });
            _tray.ContextMenuStrip = trayMenu;
            _tray.DoubleClick += (s, e) => ShowFromTray();

            _timer = new System.Windows.Forms.Timer { Interval = 15000 };
            _timer.Tick += (s, e) => CheckDue();
            _timer.Start();

            KeyDown += MainForm_KeyDown;
            Resize += (s, e) => { if (WindowState == FormWindowState.Minimized && _settings.MinimizeToTray) HideToTray(); };
            FormClosing += MainForm_FormClosing;

            RefreshList();
        }

        private Button MakeButton(string text, int x, int y, EventHandler onClick, string accName, int width = 100)
        {
            var b = new Button
            {
                Text = text,
                Left = x,
                Top = y,
                Width = width,
                Height = 30,
                AccessibleName = accName
            };
            b.Click += onClick;
            return b;
        }

        // ---------- горячая клавиша ----------

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyHotkey();
        }

        private void ApplyHotkey()
        {
            if (_hotkeyRegistered)
            {
                UnregisterHotKey(Handle, HotkeyId);
                _hotkeyRegistered = false;
            }
            if (TryParseHotkey(_settings.Hotkey, out var mods, out var key))
                _hotkeyRegistered = RegisterHotKey(Handle, HotkeyId, mods, key);
        }

        public static bool TryParseHotkey(string text, out uint mods, out uint key)
        {
            mods = 0;
            key = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;

            foreach (var raw in text.Split('+'))
            {
                var part = raw.Trim();
                if (part.Length == 0) continue;
                switch (part.ToLowerInvariant())
                {
                    case "control":
                    case "ctrl": mods |= MOD_CONTROL; continue;
                    case "alt": mods |= MOD_ALT; continue;
                    case "shift": mods |= MOD_SHIFT; continue;
                    case "win": mods |= MOD_WIN; continue;
                }

                if (part.Length == 1 && char.IsLetter(part[0]))
                {
                    key = (uint)char.ToUpperInvariant(part[0]);
                    return true;
                }
                var named = part.ToLowerInvariant();
                if (named == "f1") { key = 0x70; return true; }
                if (named == "f2") { key = 0x71; return true; }
                if (named == "f3") { key = 0x72; return true; }
                if (named == "f4") { key = 0x73; return true; }
                if (named == "f5") { key = 0x74; return true; }
                if (named == "f6") { key = 0x75; return true; }
                if (named == "f7") { key = 0x76; return true; }
                if (named == "f8") { key = 0x77; return true; }
                if (named == "f9") { key = 0x78; return true; }
                if (named == "f10") { key = 0x79; return true; }
                if (named == "f11") { key = 0x7A; return true; }
                if (named == "f12") { key = 0x7B; return true; }
                if (named == "пробел" || named == "space") { key = 0x20; return true; }
                return false;
            }
            return false;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
                ShowFromTray();
            base.WndProc(ref m);
        }

        // ---------- горячие клавиши в окне ----------

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F1) { ShowHelp(); e.Handled = true; return; }
            if (e.KeyCode == Keys.Insert) { CreateReminder(); e.Handled = true; return; }
            if (e.KeyCode == Keys.Delete) { DeleteReminder(); e.Handled = true; return; }
            if (e.KeyCode == Keys.Enter && _list.Focused) { EditReminder(); e.Handled = true; return; }
            if (e.KeyCode == Keys.Space && _list.Focused) { ToggleEnabled(); e.Handled = true; }
        }

        // ---------- список ----------

        private Reminder Selected =>
            _list.SelectedIndex >= 0 && _list.SelectedIndex < _reminders.Count ? _reminders[_list.SelectedIndex] : null;

        private void RefreshList()
        {
            var selected = _list.SelectedIndex;
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var r in _reminders) _list.Items.Add(Describe(r));
            _list.EndUpdate();
            if (selected >= 0 && selected < _list.Items.Count) _list.SelectedIndex = selected;
            else if (_list.Items.Count > 0) _list.SelectedIndex = 0;
            RefreshStatus();
        }

        private string Describe(Reminder r)
        {
            var sb = new StringBuilder();
            if (!r.Enabled) sb.Append("Выключено. ");
            sb.Append(string.IsNullOrWhiteSpace(r.Title) ? "Без названия" : r.Title);
            sb.Append(". ").Append(Reminder.KindName(r.Kind));
            var schedule = r.DescribeSchedule();
            if (!string.IsNullOrWhiteSpace(schedule)) sb.Append(". ").Append(schedule);
            var next = NextFor(r);
            if (next.HasValue) sb.Append(". Следующее срабатывание ")
                .Append(next.Value.ToString("dd.MM.yyyy в HH:mm", CultureInfo.GetCultureInfo("ru-RU")));
            else sb.Append(". Не сработает");
            return sb.ToString();
        }

        private DateTime? NextFor(Reminder r)
        {
            var after = _lastFired.TryGetValue(r.Id, out var lf) ? lf : DateTime.Now.AddMinutes(-1);
            return Scheduler.NextOccurrence(r, after);
        }

        private void RefreshStatus()
        {
            var parts = new List<string>();
            parts.Add("Напоминаний: " + _reminders.Count);

            if (_pausedUntil.HasValue && _pausedUntil.Value > DateTime.Now)
                parts.Add("Пауза до " + _pausedUntil.Value.ToString("HH:mm"));

            var cur = Selected;
            if (cur != null)
            {
                var next = NextFor(cur);
                parts.Add(next.HasValue
                    ? "Выбранное сработает " + next.Value.ToString("dd.MM.yyyy в HH:mm", CultureInfo.GetCultureInfo("ru-RU"))
                    : "Выбранное больше не сработает");
            }

            _status.Text = string.Join(". ", parts);
        }

        // ---------- действия ----------

        private void CreateReminder()
        {
            using var dlg = new ReminderForm(null, _settings);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            _reminders.Add(dlg.Result);
            SaveAll();
            RefreshList();
            _list.SelectedIndex = _list.Items.Count - 1;
        }

        private void EditReminder()
        {
            var r = Selected;
            if (r == null) return;
            using var dlg = new ReminderForm(r, _settings);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            var idx = _reminders.IndexOf(r);
            if (idx >= 0) _reminders[idx] = dlg.Result;
            SaveAll();
            RefreshList();
        }

        private void DeleteReminder()
        {
            var r = Selected;
            if (r == null) return;
            var answer = MessageBox.Show(this,
                "Удалить напоминание «" + r.Title + "»?",
                "Напоминалка", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes) return;
            _reminders.Remove(r);
            _lastFired.Remove(r.Id);
            _snoozed.Remove(r.Id);
            SaveAll();
            RefreshList();
        }

        private void ToggleEnabled()
        {
            var r = Selected;
            if (r == null) return;
            r.Enabled = !r.Enabled;
            SaveAll();
            RefreshList();
            Speech.Speak(r.Title + (r.Enabled ? " включено" : " выключено"), _settings.SpeechMode);
        }

        private void OpenSettings()
        {
            using var dlg = new SettingsForm(_settings);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            _settings = dlg.Settings;
            _settings.Save(AppPaths.SettingsFile);
            ApplyAutostart();
            ApplyHotkey();
            RefreshList();
        }

        private void ApplyAutostart()
        {
            if (_settings.Autostart) Autostart.Enable();
            else Autostart.Disable();
        }

        private void ShowHelp()
        {
            using var dlg = new HelpForm();
            dlg.ShowDialog(this);
        }

        private void ShowAbout()
        {
            var version = typeof(MainForm).Assembly.GetName().Version;
            MessageBox.Show(this,
                "Напоминалка, версия " + version + ".\n\nПортабельная программа: все файлы лежат в её папке.\nПрограмма не отправляет данные никуда, кроме проверки обновлений, если она включена.",
                "О программе", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void PauseFor(TimeSpan span)
        {
            _pausedUntil = DateTime.Now.Add(span);
            RefreshStatus();
            Speech.Speak("Напоминания приостановлены до " + _pausedUntil.Value.ToString("HH:mm"), _settings.SpeechMode);
        }

        private void PauseUntilMorning()
        {
            var morning = DateTime.Today.AddDays(1).AddHours(8);
            _pausedUntil = morning;
            RefreshStatus();
            Speech.Speak("Напоминания приостановлены до утра", _settings.SpeechMode);
        }

        private void ExportList()
        {
            using var dlg = new SaveFileDialog
            {
                Title = "Сохранить список напоминаний",
                Filter = "Текстовый файл (*.txt)|*.txt|Все файлы (*.*)|*.*",
                FileName = "напоминания-копия.txt"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                ReminderStore.Save(dlg.FileName, _reminders);
                MessageBox.Show(this, "Список сохранён.", "Напоминалка", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Не удалось сохранить: " + ex.Message, "Напоминалка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ImportList()
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Загрузить список напоминаний",
                Filter = "Текстовый файл (*.txt)|*.txt|Все файлы (*.*)|*.*"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                var loaded = ReminderStore.Load(dlg.FileName);
                if (loaded.Count == 0)
                {
                    MessageBox.Show(this, "В файле не нашлось ни одного напоминания.", "Напоминалка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                var answer = MessageBox.Show(this,
                    "В файле напоминаний: " + loaded.Count + ". Заменить текущий список?",
                    "Напоминалка", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer != DialogResult.Yes) return;
                _reminders = loaded;
                SaveAll();
                RefreshList();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Не удалось загрузить: " + ex.Message, "Напоминалка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void CheckUpdatesNow()
        {
            if (string.IsNullOrWhiteSpace(_settings.UpdateUrl))
            {
                var text = "Адрес обновлений не задан. Его можно вписать в настройках, раздел «Общие».";
                Speech.Speak(text, _settings.SpeechMode);
                MessageBox.Show(this, text, "Обновление", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Speech.Speak("Проверяю обновления", _settings.SpeechMode);
            CheckUpdates(manual: true);
        }

        // ---------- трей ----------

        private void HideToTray()
        {
            Hide();
            ShowInTaskbar = false;
            _tray.Visible = true;
        }

        private void ShowFromTray()
        {
            Show();
            ShowInTaskbar = true;
            WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
            _list.Focus();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_reallyExit && _settings.MinimizeToTray)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }
            _timer?.Stop();
            if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
            if (_hotkeyRegistered) UnregisterHotKey(Handle, HotkeyId);
            SaveAll();
        }

        // ---------- срабатывание ----------

        private void CheckDue()
        {
            if (_alertOpen) return;
            var now = DateTime.Now;

            if (_pausedUntil.HasValue)
            {
                if (now < _pausedUntil.Value) return;
                _pausedUntil = null;
            }

            foreach (var r in _reminders.ToList())
            {
                if (!r.Enabled) continue;

                if (_snoozed.TryGetValue(r.Id, out var due))
                {
                    if (now >= due)
                    {
                        _snoozed.Remove(r.Id);
                        Fire(r, due);
                        return;
                    }
                    continue;
                }

                var after = _lastFired.TryGetValue(r.Id, out var lf) ? lf : _startedAt;
                var next = Scheduler.NextOccurrence(r, after);
                if (next.HasValue && next.Value <= now)
                {
                    Fire(r, next.Value);
                    return;
                }
            }
            RefreshStatus();
        }

        private void Fire(Reminder r, DateTime when)
        {
            _lastFired[r.Id] = when;
            SaveState();

            var nag = r.IsMedicine ? (_settings.MedicineNag || r.Nag) : r.Nag;
            _alertOpen = true;
            try
            {
                using var dlg = new AlertForm(r, _settings, nag);
                dlg.ShowDialog(this);

                // Журнал — про приём лекарств. Дни рождения и встречи в него
                // не пишутся, иначе он зарастает посторонними строками.
                var log = _settings.KeepJournal && r.IsMedicine;

                switch (dlg.Choice)
                {
                    case "отложить":
                        _snoozed[r.Id] = DateTime.Now.AddMinutes(Math.Max(1, _settings.SnoozeMinutes));
                        if (log) Journal.Append(AppPaths.JournalFile, r.Title, "отложено на " + _settings.SnoozeMinutes + " минут");
                        break;
                    case "пропустить":
                        if (log) Journal.Append(AppPaths.JournalFile, r.Title, "пропущено");
                        break;
                    default:
                        if (log) Journal.Append(AppPaths.JournalFile, r.Title, "принято");
                        break;
                }
            }
            finally
            {
                _alertOpen = false;
                MelodyPlayer.Stop();
            }
            RefreshList();
        }

        /// <summary>Сообщаем о том, что сработало, пока программа не работала.</summary>
        private void ReportMissed()
        {
            var missed = new List<string>();
            foreach (var r in _reminders)
            {
                if (!r.Enabled) continue;
                var after = _lastFired.TryGetValue(r.Id, out var lf) ? lf : DateTime.Now.AddDays(-1);
                var next = Scheduler.NextOccurrence(r, after);

                // Только то, что прошло до запуска. Срок, прошедший в последнюю
                // минуту, сработает живьём — сообщать о нём дважды незачем.
                if (next.HasValue && next.Value <= _startedAt)
                    missed.Add(r.Title + ", " + next.Value.ToString("dd.MM.yyyy в HH:mm", CultureInfo.GetCultureInfo("ru-RU")));
            }
            if (missed.Count == 0) return;

            var text = "Пока программа была закрыта, пропущено:" + Environment.NewLine + string.Join(Environment.NewLine, missed);
            Speech.Speak("Пропущенные напоминания: " + string.Join(", ", missed), _settings.SpeechMode);
            MessageBox.Show(this, text, "Пропущенные напоминания", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ---------- хранение ----------

        private void SaveAll()
        {
            try { ReminderStore.Save(AppPaths.RemindersFile, _reminders); } catch { }
            SaveState();
        }

        private void SaveState()
        {
            try
            {
                var s = new IniSection { Name = "Состояние" };
                foreach (var kv in _lastFired) s.Set(kv.Key, kv.Value);
                IniFile.WriteFile(StatePath(), IniFile.Write(new[] { s }));
            }
            catch { }
        }

        private static string StatePath() => Path.Combine(AppPaths.BaseDir, "состояние.txt");

        private void LoadState()
        {
            try
            {
                foreach (var sec in IniFile.Parse(IniFile.ReadFile(StatePath())))
                {
                    if (sec.Name != "Состояние") continue;
                    foreach (var kv in sec.Items)
                        _lastFired[kv.Key] = sec.GetDate(kv.Key, DateTime.MinValue);
                }
            }
            catch { }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (_firstRun) AskAutostartOnFirstRun();

            Autostart.RepairIfNeeded();
            if (_settings.Autostart) Autostart.Enable();
            if (_settings.KeepJournal) Journal.Trim(AppPaths.JournalFile, _settings.JournalDays);
            ReportMissed();

            if (_settings.CheckUpdates) CheckUpdatesQuietly();
        }

        /// <summary>
        /// Первый запуск: спрашиваем про автозапуск один раз и запоминаем ответ.
        /// </summary>
        private void AskAutostartOnFirstRun()
        {
            try
            {
                Speech.Speak("Напоминалка запущена впервые", _settings.SpeechMode);
                var answer = MessageBox.Show(this,
                    "Добавить Напоминалку в автозапуск, чтобы она включалась вместе с Windows?",
                    "Первый запуск", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                _settings.Autostart = answer == DialogResult.Yes;
                _settings.Save(AppPaths.SettingsFile);
                ApplyAutostart();

                Speech.Speak(_settings.Autostart
                    ? "Автозапуск включён"
                    : "Автозапуск выключен, включить можно в настройках", _settings.SpeechMode);
            }
            catch { }
        }

        private void CheckUpdatesQuietly()
        {
            if (string.IsNullOrWhiteSpace(_settings.UpdateUrl)) return;
            CheckUpdates(manual: false);
        }

        /// <summary>
        /// Проверка обновлений идёт в стороне от окна: если сеть медленная,
        /// программа не выглядит зависшей, а окно отвечает на клавиши.
        /// При ручной проверке ответ показываем всегда, при тихой — только
        /// когда обновление действительно есть.
        /// </summary>
        private void CheckUpdates(bool manual)
        {
            var url = _settings.UpdateUrl;

            System.Threading.Tasks.Task.Run(() =>
            {
                Updater.Result res;
                try { res = Updater.Check(url); }
                catch { return; }
                if (IsDisposed) return;

                try
                {
                    BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            if (!res.HasUpdate)
                            {
                                if (!manual) return;
                                Speech.Speak(res.Message, _settings.SpeechMode);
                                MessageBox.Show(this, res.Message, "Обновление", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                return;
                            }

                            Speech.Speak("Есть новая версия программы", _settings.SpeechMode);
                            var text = "Есть новая версия программы: " + res.NewVersion + ". Открыть страницу загрузки?";
                            var answer = MessageBox.Show(this, text, "Обновление", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                            if (answer == DialogResult.Yes && !string.IsNullOrEmpty(res.DownloadUrl))
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(res.DownloadUrl) { UseShellExecute = true });
                        }
                        catch { }
                    }));
                }
                catch { }
            });
        }
    }
}
