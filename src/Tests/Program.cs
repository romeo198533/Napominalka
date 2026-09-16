using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Napominalka.Core;

namespace Napominalka.Tests
{
    /// <summary>
    /// Проверки логики расписания и файлов. Гоняются здесь, на Linux,
    /// потому что это чистая логика без Windows.
    /// </summary>
    internal static class Program
    {
        private static int _failed;
        private static int _passed;

        private static int Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            OnceTests();
            WeekdayTests();
            ExactDateTests();
            MedicineTests();
            MissedTests();
            StorageTests();
            SettingsTests();

            Console.WriteLine();
            Console.WriteLine("Пройдено: " + _passed + ", провалено: " + _failed);
            return _failed == 0 ? 0 : 1;
        }

        // ---------- Один раз ----------

        private static void OnceTests()
        {
            var now = new DateTime(2026, 9, 16, 12, 0, 0);

            var r = new Reminder { Repeat = RepeatMode.Once, OnceAt = now.AddHours(2) };
            Check("Один раз: будущее срабатывание найдено",
                Scheduler.NextOccurrence(r, now) == now.AddHours(2));

            r.OnceAt = now.AddHours(-2);
            Check("Один раз: прошедшее не срабатывает",
                Scheduler.NextOccurrence(r, now) == null);

            r.OnceAt = now.AddHours(2);
            r.Enabled = false;
            Check("Выключенное не срабатывает",
                Scheduler.NextOccurrence(r, now) == null);
        }

        // ---------- Дни недели ----------

        private static void WeekdayTests()
        {
            // 16 сентября 2026 — среда
            var wednesday = new DateTime(2026, 9, 16, 10, 0, 0);

            var r = new Reminder
            {
                Repeat = RepeatMode.Weekdays,
                TimeOfDay = new TimeSpan(9, 0, 0),
                Days = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Friday }
            };

            var next = Scheduler.NextOccurrence(r, wednesday);
            Check("Дни недели: со среды переходим на пятницу",
                next == new DateTime(2026, 9, 18, 9, 0, 0));

            // если сегодня пятница и время ещё не прошло — срабатывает сегодня
            var fridayEarly = new DateTime(2026, 9, 18, 7, 0, 0);
            Check("Дни недели: в пятницу до девяти — сработает сегодня",
                Scheduler.NextOccurrence(r, fridayEarly) == new DateTime(2026, 9, 18, 9, 0, 0));

            // если время уже прошло — ближайший из отмеченных дней, то есть понедельник
            var fridayLate = new DateTime(2026, 9, 18, 10, 0, 0);
            Check("Дни недели: после девяти — ближайший отмеченный день",
                Scheduler.NextOccurrence(r, fridayLate) == new DateTime(2026, 9, 21, 9, 0, 0));

            // только пятница — тогда действительно через неделю
            var fridayOnly = new Reminder
            {
                Repeat = RepeatMode.Weekdays,
                TimeOfDay = new TimeSpan(9, 0, 0),
                Days = new List<DayOfWeek> { DayOfWeek.Friday }
            };
            Check("Дни недели: одна пятница, время прошло — через неделю",
                Scheduler.NextOccurrence(fridayOnly, fridayLate) == new DateTime(2026, 9, 25, 9, 0, 0));

            r.Days.Clear();
            Check("Дни недели: без выбранных дней не срабатывает",
                Scheduler.NextOccurrence(r, wednesday) == null);
        }

        // ---------- Конкретная дата ----------

        private static void ExactDateTests()
        {
            var now = new DateTime(2026, 9, 16, 12, 0, 0);

            var r = new Reminder
            {
                Repeat = RepeatMode.ExactDate,
                ExactDate = new DateTime(2026, 12, 31),
                TimeOfDay = new TimeSpan(18, 30, 0)
            };
            Check("Конкретная дата: будущая дата найдена",
                Scheduler.NextOccurrence(r, now) == new DateTime(2026, 12, 31, 18, 30, 0));

            r.ExactDate = new DateTime(2026, 1, 1);
            Check("Конкретная дата: прошедшая дата не срабатывает",
                Scheduler.NextOccurrence(r, now) == null);
        }

        // ---------- Лекарства ----------

        private static void MedicineTests()
        {
            var start = new DateTime(2026, 9, 16);

            var r = new Reminder
            {
                Kind = EventKind.Medicine,
                CourseStart = start,
                MedTimes = new List<TimeSpan>
                {
                    new TimeSpan(8, 0, 0),
                    new TimeSpan(14, 0, 0),
                    new TimeSpan(20, 0, 0)
                }
            };

            Check("Лекарство: после утреннего приёма — дневной",
                Scheduler.NextOccurrence(r, new DateTime(2026, 9, 16, 9, 0, 0)) == new DateTime(2026, 9, 16, 14, 0, 0));

            Check("Лекарство: после вечернего — утренний следующего дня",
                Scheduler.NextOccurrence(r, new DateTime(2026, 9, 16, 21, 0, 0)) == new DateTime(2026, 9, 17, 8, 0, 0));

            Check("Лекарство: до первого приёма — первый приём",
                Scheduler.NextOccurrence(r, new DateTime(2026, 9, 16, 6, 0, 0)) == new DateTime(2026, 9, 16, 8, 0, 0));

            // курс три дня
            var course = new Reminder
            {
                Kind = EventKind.Medicine,
                CourseStart = start,
                CourseDays = 3,
                MedTimes = new List<TimeSpan> { new TimeSpan(8, 0, 0) }
            };
            Check("Лекарство: в пределах курса срабатывает",
                Scheduler.NextOccurrence(course, new DateTime(2026, 9, 18, 6, 0, 0)) == new DateTime(2026, 9, 18, 8, 0, 0));
            Check("Лекарство: после окончания курса не срабатывает",
                Scheduler.NextOccurrence(course, new DateTime(2026, 9, 18, 9, 0, 0)) == null);

            // каждые 6 часов от 08:00
            var everyHours = new Reminder
            {
                Kind = EventKind.Medicine,
                CourseStart = start,
                EveryHours = 6,
                MedTimes = new List<TimeSpan> { new TimeSpan(8, 0, 0) }
            };
            Check("Лекарство каждые 6 часов: 08, 14, 20, 02",
                Scheduler.NextOccurrence(everyHours, new DateTime(2026, 9, 16, 15, 0, 0)) == new DateTime(2026, 9, 16, 20, 0, 0));
            Check("Лекарство каждые 6 часов: ночной приём в два часа",
                Scheduler.NextOccurrence(everyHours, new DateTime(2026, 9, 16, 21, 0, 0)) == new DateTime(2026, 9, 17, 2, 0, 0));

            // раз в два дня
            var everyDays = new Reminder
            {
                Kind = EventKind.Medicine,
                CourseStart = start,
                EveryDays = 2,
                MedTimes = new List<TimeSpan> { new TimeSpan(9, 0, 0) }
            };
            Check("Лекарство раз в два дня",
                Scheduler.NextOccurrence(everyDays, new DateTime(2026, 9, 16, 10, 0, 0)) == new DateTime(2026, 9, 18, 9, 0, 0));
        }

        // ---------- Пропущенное ----------

        private static void MissedTests()
        {
            var r = new Reminder
            {
                Repeat = RepeatMode.Weekdays,
                TimeOfDay = new TimeSpan(9, 0, 0),
                Days = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday }
            };

            var now = new DateTime(2026, 9, 16, 10, 0, 0);
            Check("Пропущенное: сегодняшнее утреннее найдено",
                Scheduler.LastMissed(r, now) == new DateTime(2026, 9, 16, 9, 0, 0));

            var early = new DateTime(2026, 9, 16, 8, 0, 0);
            Check("Пропущенное: вчерашнее утреннее найдено",
                Scheduler.LastMissed(r, early) == new DateTime(2026, 9, 15, 9, 0, 0));

            // напоминание только по понедельникам: в среду пропущенного нет
            var mondayOnly = new Reminder
            {
                Repeat = RepeatMode.Weekdays,
                TimeOfDay = new TimeSpan(9, 0, 0),
                Days = new List<DayOfWeek> { DayOfWeek.Monday }
            };
            Check("Пропущенное: если в прошлые сутки срока не было — пусто",
                Scheduler.LastMissed(mondayOnly, new DateTime(2026, 9, 16, 12, 0, 0)) == null);
        }

        // ---------- Файлы ----------

        private static void StorageTests()
        {
            var dir = Path.Combine(Path.GetTempPath(), "napominalka-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "напоминания.txt");

            try
            {
                var original = new Reminder
                {
                    Title = "Выпить таблетку = утренняя",
                    Kind = EventKind.Medicine,
                    Enabled = true,
                    Dose = "1 таблетка",
                    MedTimes = new List<TimeSpan> { new TimeSpan(8, 0, 0), new TimeSpan(20, 0, 0) },
                    Nag = true,
                    NagMinutes = 7,
                    CourseStart = new DateTime(2026, 9, 10),
                    CourseDays = 30,
                    Melody = "chime.wav"
                };

                var weekdays = new Reminder
                {
                    Title = "Зарядка",
                    Kind = EventKind.Other,
                    Repeat = RepeatMode.Weekdays,
                    Days = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Friday },
                    TimeOfDay = new TimeSpan(7, 30, 0),
                    Enabled = false
                };

                ReminderStore.Save(path, new[] { original, weekdays });
                var loaded = ReminderStore.Load(path);

                Check("Файл: сохранено два напоминания", loaded.Count == 2);

                var m = loaded.FirstOrDefault(x => x.Kind == EventKind.Medicine);
                Check("Файл: название с пробелами и равно сохранено", m != null && m.Title == original.Title);
                Check("Файл: доза сохранена", m != null && m.Dose == "1 таблетка");
                Check("Файл: времена приёма сохранены", m != null && m.MedTimes.Count == 2 && m.MedTimes[0] == new TimeSpan(8, 0, 0));
                Check("Файл: дожимание сохранено", m != null && m.Nag && m.NagMinutes == 7);
                Check("Файл: курс сохранён", m != null && m.CourseDays == 30 && m.CourseStart == new DateTime(2026, 9, 10));
                Check("Файл: мелодия сохранена", m != null && m.Melody == "chime.wav");

                var w = loaded.FirstOrDefault(x => x.Title == "Зарядка");
                Check("Файл: дни недели сохранены", w != null && w.Days.Count == 2 && w.Days.Contains(DayOfWeek.Friday));
                Check("Файл: время сохранено", w != null && w.TimeOfDay == new TimeSpan(7, 30, 0));
                Check("Файл: выключенное осталось выключенным", w != null && !w.Enabled);
                Check("Файл: тип события сохранён", w != null && w.Kind == EventKind.Other);

                var text = File.ReadAllText(path);
                Check("Файл: читаемый текст с русскими ключами", text.Contains("Название=") && text.Contains("ВременаПриёма="));

                var broken = ReminderStore.Load(Path.Combine(dir, "нет-такого-файла.txt"));
                Check("Файл: отсутствующий файл не роняет программу", broken.Count == 0);

                var ok = false;
                try
                {
                    IniFile.Parse("мусор\n[Напоминание]\nНазвание=Тест\nнепонятная строка без равно\n");
                    ok = true;
                }
                catch { }
                Check("Файл: мусор в файле не роняет разбор", ok);
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        private static void SettingsTests()
        {
            var dir = Path.Combine(Path.GetTempPath(), "napominalka-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "настройки.txt");

            try
            {
                var s = new AppSettings
                {
                    Autostart = false,
                    Hotkey = "Control+Shift+R",
                    SpeechMode = "Системный",
                    SnoozeMinutes = 15,
                    CheckUpdates = true,
                    UpdateUrl = "https://api.github.com/repos/romeo198533/napominalka/releases/latest"
                };
                s.Save(path);

                var back = AppSettings.Load(path);
                Check("Настройки: автозапуск сохранён", back.Autostart == false);
                Check("Настройки: горячая клавиша сохранена", back.Hotkey == "Control+Shift+R");
                Check("Настройки: озвучка сохранена", back.SpeechMode == "Системный");
                Check("Настройки: отложить сохранено", back.SnoozeMinutes == 15);
                Check("Настройки: проверка обновлений сохранена", back.CheckUpdates && back.UpdateUrl.Contains("napominalka"));
                Check("Настройки: значения по умолчанию не потерялись", back.SignalRepeats == 3 && back.MedicineNag);
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        // ---------- проверки ----------

        private static void Check(string name, bool condition)
        {
            if (condition)
            {
                _passed++;
                Console.WriteLine("ок   " + name);
            }
            else
            {
                _failed++;
                Console.WriteLine("ПРОВАЛ " + name);
            }
        }
    }
}
