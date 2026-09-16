using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Napominalka.Core
{
    /// <summary>Одна секция простого текстового файла: пары ключ=значение по порядку.</summary>
    public class IniSection
    {
        public string Name = "";
        public List<KeyValuePair<string, string>> Items = new List<KeyValuePair<string, string>>();

        public string Get(string key, string fallback = "")
        {
            foreach (var kv in Items)
                if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                    return kv.Value;
            return fallback;
        }

        public int GetInt(string key, int fallback)
        {
            var s = Get(key);
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;
        }

        public bool GetBool(string key, bool fallback)
        {
            var s = Get(key).Trim().ToLowerInvariant();
            if (s == "да" || s == "true" || s == "1" || s == "yes") return true;
            if (s == "нет" || s == "false" || s == "0" || s == "no") return false;
            return fallback;
        }

        public DateTime GetDate(string key, DateTime fallback)
        {
            var s = Get(key).Trim();
            if (DateTime.TryParseExact(s, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var v)) return v;
            if (DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out v)) return v;
            return fallback;
        }

        public void Set(string key, string value)
        {
            for (int i = 0; i < Items.Count; i++)
                if (string.Equals(Items[i].Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    Items[i] = new KeyValuePair<string, string>(key, value);
                    return;
                }
            Items.Add(new KeyValuePair<string, string>(key, value));
        }

        public void Set(string key, bool value) => Set(key, value ? "да" : "нет");
        public void Set(string key, int value) => Set(key, value.ToString(CultureInfo.InvariantCulture));
        public void Set(string key, DateTime value) => Set(key, value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
    }

    /// <summary>Чтение и запись простого текстового файла с секциями. Правится руками.</summary>
    public static class IniFile
    {
        public static List<IniSection> Parse(string text)
        {
            var result = new List<IniSection>();
            IniSection current = null;

            foreach (var raw in (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("#") || line.StartsWith(";")) continue;

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    current = new IniSection { Name = line.Substring(1, line.Length - 2).Trim() };
                    result.Add(current);
                    continue;
                }

                var eq = line.IndexOf('=');
                if (eq < 0) continue;
                var key = line.Substring(0, eq).Trim();
                var val = line.Substring(eq + 1).Trim();
                if (key.Length == 0) continue;

                if (current == null)
                {
                    current = new IniSection { Name = "" };
                    result.Add(current);
                }
                current.Items.Add(new KeyValuePair<string, string>(key, val));
            }
            return result;
        }

        public static string Write(IEnumerable<IniSection> sections)
        {
            var sb = new StringBuilder();
            foreach (var s in sections)
            {
                sb.Append('[').Append(s.Name).Append(']').Append('\n');
                foreach (var kv in s.Items)
                    sb.Append(kv.Key).Append('=').Append(kv.Value).Append('\n');
                sb.Append('\n');
            }
            return sb.ToString();
        }

        public static string ReadFile(string path)
            => File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : "";

        public static void WriteFile(string path, string text)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, text, new UTF8Encoding(true));
        }
    }

    /// <summary>Хранилище напоминаний в файле рядом с программой.</summary>
    public static class ReminderStore
    {
        public const string SectionName = "Напоминание";

        public static List<Reminder> Load(string path)
        {
            var list = new List<Reminder>();
            foreach (var s in IniFile.Parse(IniFile.ReadFile(path)))
            {
                if (!string.Equals(s.Name, SectionName, StringComparison.OrdinalIgnoreCase)) continue;
                list.Add(FromSection(s));
            }
            return list;
        }

        public static void Save(string path, IEnumerable<Reminder> reminders)
        {
            var sections = reminders.Select(ToSection).ToList();
            IniFile.WriteFile(path, IniFile.Write(sections));
        }

        public static Reminder FromSection(IniSection s)
        {
            var r = new Reminder
            {
                Id = s.Get("Ид", Guid.NewGuid().ToString("N")),
                Title = s.Get("Название"),
                Kind = ParseKind(s.Get("Тип")),
                Repeat = ParseRepeat(s.Get("Повтор")),
                Enabled = s.GetBool("Включено", true),
                OnceAt = s.GetDate("Разово", DateTime.Now.AddHours(1)),
                ExactDate = s.GetDate("Дата", DateTime.Today),
                Melody = s.Get("Мелодия"),
                Nag = s.GetBool("Дожимание", false),
                NagMinutes = s.GetInt("ДожиманиеМинут", 5),
                Dose = s.Get("Доза"),
                EveryHours = s.GetInt("КаждыеЧасов", 0),
                EveryDays = s.GetInt("КаждыеДней", 0),
                CourseStart = s.GetDate("НачалоКурса", DateTime.Today),
                CourseDays = s.GetInt("ДнейКурса", 0)
            };

            var t = ParseTime(s.Get("Время"));
            if (t.HasValue) r.TimeOfDay = t.Value;

            foreach (var d in SplitList(s.Get("Дни")))
            {
                var dw = ParseDay(d);
                if (dw.HasValue && !r.Days.Contains(dw.Value)) r.Days.Add(dw.Value);
            }

            foreach (var x in SplitList(s.Get("ВременаПриёма")))
            {
                var tt = ParseTime(x);
                if (tt.HasValue) r.MedTimes.Add(tt.Value);
            }
            r.MedTimes = r.MedTimes.OrderBy(x => x).ToList();

            return r;
        }

        public static IniSection ToSection(Reminder r)
        {
            var s = new IniSection { Name = SectionName };
            s.Set("Ид", r.Id);
            s.Set("Название", r.Title);
            s.Set("Тип", KindText(r.Kind));
            s.Set("Повтор", RepeatText(r.Repeat));
            s.Set("Включено", r.Enabled);
            s.Set("Разово", r.OnceAt);
            s.Set("Дни", string.Join(",", r.Days.Select(Reminder.DayName)));
            s.Set("Время", r.TimeOfDay.ToString(@"hh\:mm"));
            s.Set("Дата", r.ExactDate.ToString("yyyy-MM-dd"));
            s.Set("Мелодия", r.Melody);
            s.Set("Дожимание", r.Nag);
            s.Set("ДожиманиеМинут", r.NagMinutes);
            s.Set("Доза", r.Dose);
            s.Set("ВременаПриёма", string.Join(",", r.MedTimes.OrderBy(x => x).Select(x => x.ToString(@"hh\:mm"))));
            s.Set("КаждыеЧасов", r.EveryHours);
            s.Set("КаждыеДней", r.EveryDays);
            s.Set("НачалоКурса", r.CourseStart);
            s.Set("ДнейКурса", r.CourseDays);
            return s;
        }

        public static string KindText(EventKind k)
        {
            switch (k)
            {
                case EventKind.Birthday: return "День рождения";
                case EventKind.Anniversary: return "Годовщина";
                case EventKind.Meeting: return "Встреча";
                case EventKind.Medicine: return "Приём лекарств";
                default: return "Другое";
            }
        }

        public static EventKind ParseKind(string s)
        {
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "день рождения": return EventKind.Birthday;
                case "годовщина": return EventKind.Anniversary;
                case "встреча": return EventKind.Meeting;
                case "приём лекарств":
                case "прием лекарств":
                case "лекарство": return EventKind.Medicine;
                default: return EventKind.Other;
            }
        }

        public static string RepeatText(RepeatMode m)
        {
            switch (m)
            {
                case RepeatMode.Weekdays: return "Дни недели";
                case RepeatMode.ExactDate: return "Конкретная дата";
                default: return "Один раз";
            }
        }

        public static RepeatMode ParseRepeat(string s)
        {
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "дни недели": return RepeatMode.Weekdays;
                case "конкретная дата": return RepeatMode.ExactDate;
                default: return RepeatMode.Once;
            }
        }

        public static DayOfWeek? ParseDay(string s)
        {
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "понедельник": return DayOfWeek.Monday;
                case "вторник": return DayOfWeek.Tuesday;
                case "среда": return DayOfWeek.Wednesday;
                case "четверг": return DayOfWeek.Thursday;
                case "пятница": return DayOfWeek.Friday;
                case "суббота": return DayOfWeek.Saturday;
                case "воскресенье": return DayOfWeek.Sunday;
                default: return null;
            }
        }

        public static TimeSpan? ParseTime(string s)
        {
            var t = (s ?? "").Trim();
            if (TimeSpan.TryParseExact(t, @"hh\:mm", CultureInfo.InvariantCulture, out var v)) return v;
            if (TimeSpan.TryParseExact(t, @"h\:mm", CultureInfo.InvariantCulture, out v)) return v;
            return null;
        }

        private static IEnumerable<string> SplitList(string s)
            => (s ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim());
    }

    /// <summary>Настройки программы. Хранятся в простом текстовом файле.</summary>
    public class AppSettings
    {
        public bool Autostart = true;
        public bool MinimizeToTray = true;
        public string Hotkey = "Control+Alt+N";
        public bool TopMostAlerts = true;
        public bool CheckUpdates = false;
        public string UpdateUrl = "";
        public string DefaultMelody = "по умолчанию.wav";
        public int SignalRepeats = 3;
        public string SpeechMode = "NVDA";      // NVDA | Системный | Не озвучивать
        public bool NagDefault = false;
        public int NagMinutesDefault = 5;
        public bool MedicineNag = true;
        public int SnoozeMinutes = 10;
        public bool KeepJournal = true;
        public int JournalDays = 90;

        public static AppSettings Load(string path)
        {
            var s = new AppSettings();
            foreach (var sec in IniFile.Parse(IniFile.ReadFile(path)))
            {
                switch (sec.Name)
                {
                    case "Общие":
                        s.Autostart = sec.GetBool("Автозапуск", s.Autostart);
                        s.MinimizeToTray = sec.GetBool("СворачиватьВТрей", s.MinimizeToTray);
                        s.Hotkey = sec.Get("ГорячаяКлавиша", s.Hotkey);
                        s.TopMostAlerts = sec.GetBool("ПоверхДругих", s.TopMostAlerts);
                        s.CheckUpdates = sec.GetBool("ПроверятьОбновления", s.CheckUpdates);
                        s.UpdateUrl = sec.Get("АдресОбновлений", s.UpdateUrl);
                        break;
                    case "Сигнал":
                        s.DefaultMelody = sec.Get("МелодияПоУмолчанию", s.DefaultMelody);
                        s.SignalRepeats = sec.GetInt("ПовторовСигнала", s.SignalRepeats);
                        s.SpeechMode = sec.Get("Озвучка", s.SpeechMode);
                        break;
                    case "Лекарства":
                        s.MedicineNag = sec.GetBool("Дожимание", s.MedicineNag);
                        s.SnoozeMinutes = sec.GetInt("ОтложитьМинут", s.SnoozeMinutes);
                        s.KeepJournal = sec.GetBool("ВестиЖурнал", s.KeepJournal);
                        s.JournalDays = sec.GetInt("ХранитьЖурналДней", s.JournalDays);
                        break;
                    case "СозданиеНапоминаний":
                        s.NagDefault = sec.GetBool("ДожиманиеПоУмолчанию", s.NagDefault);
                        s.NagMinutesDefault = sec.GetInt("ДожиманиеМинут", s.NagMinutesDefault);
                        break;
                }
            }
            return s;
        }

        public void Save(string path)
        {
            var list = new List<IniSection>();

            var common = new IniSection { Name = "Общие" };
            common.Set("Автозапуск", Autostart);
            common.Set("СворачиватьВТрей", MinimizeToTray);
            common.Set("ГорячаяКлавиша", Hotkey);
            common.Set("ПоверхДругих", TopMostAlerts);
            common.Set("ПроверятьОбновления", CheckUpdates);
            common.Set("АдресОбновлений", UpdateUrl);
            list.Add(common);

            var signal = new IniSection { Name = "Сигнал" };
            signal.Set("МелодияПоУмолчанию", DefaultMelody);
            signal.Set("ПовторовСигнала", SignalRepeats);
            signal.Set("Озвучка", SpeechMode);
            list.Add(signal);

            var med = new IniSection { Name = "Лекарства" };
            med.Set("Дожимание", MedicineNag);
            med.Set("ОтложитьМинут", SnoozeMinutes);
            med.Set("ВестиЖурнал", KeepJournal);
            med.Set("ХранитьЖурналДней", JournalDays);
            list.Add(med);

            var mk = new IniSection { Name = "СозданиеНапоминаний" };
            mk.Set("ДожиманиеПоУмолчанию", NagDefault);
            mk.Set("ДожиманиеМинут", NagMinutesDefault);
            list.Add(mk);

            IniFile.WriteFile(path, IniFile.Write(list));
        }
    }
}
