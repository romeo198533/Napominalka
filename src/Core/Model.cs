using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Napominalka.Core
{
    /// <summary>Тип события. Влияет на набор полей и поведение сигнала.</summary>
    public enum EventKind
    {
        Other,
        Birthday,
        Anniversary,
        Meeting,
        Medicine
    }

    /// <summary>Режим повтора для обычных событий.</summary>
    public enum RepeatMode
    {
        Once,
        Weekdays,
        ExactDate
    }

    /// <summary>Одно напоминание. Поля — плоские, чтобы легко писались в текстовый файл.</summary>
    public class Reminder
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string Title = "";
        public EventKind Kind = EventKind.Other;
        public RepeatMode Repeat = RepeatMode.Once;
        public bool Enabled = true;

        // Один раз
        public DateTime OnceAt = DateTime.Now.AddHours(1);

        // Дни недели
        public List<DayOfWeek> Days = new List<DayOfWeek>();
        public TimeSpan TimeOfDay = new TimeSpan(9, 0, 0);

        // Конкретная дата (год, месяц, день) + TimeOfDay
        public DateTime ExactDate = DateTime.Today;

        // Мелодия: имя файла в папке "Мелодии". Пусто — мелодия по умолчанию.
        public string Melody = "";

        // Дожимание
        public bool Nag = false;
        public int NagMinutes = 5;

        // Лекарство
        public string Dose = "";
        public List<TimeSpan> MedTimes = new List<TimeSpan>();
        public int EveryHours = 0;   // > 0 — каждые N часов от первого приёма
        public int EveryDays = 0;    // > 0 — раз в N дней
        public DateTime CourseStart = DateTime.Today;
        public int CourseDays = 0;   // 0 — бессрочно

        public bool IsMedicine => Kind == EventKind.Medicine;

        /// <summary>Текстовое описание схемы — для списка и для озвучки.</summary>
        public string DescribeSchedule()
        {
            if (IsMedicine) return DescribeMedicine();

            switch (Repeat)
            {
                case RepeatMode.Once:
                    return "один раз, " + OnceAt.ToString("dd.MM.yyyy в HH:mm", CultureInfo.GetCultureInfo("ru-RU"));
                case RepeatMode.ExactDate:
                    return "дата " + (ExactDate.Date + TimeOfDay).ToString("dd.MM.yyyy в HH:mm", CultureInfo.GetCultureInfo("ru-RU"));
                case RepeatMode.Weekdays:
                    if (Days.Count == 0) return "дни недели не выбраны";
                    return string.Join(", ", Days.Select(DayName)) + " в " + TimeOfDay.ToString(@"hh\:mm");
            }
            return "";
        }

        public string DescribeMedicine()
        {
            var parts = new List<string>();

            if (EveryHours > 0)
                parts.Add("каждые " + EveryHours + " " + Plural(EveryHours, "час", "часа", "часов"));
            else if (EveryDays > 0)
                parts.Add("раз в " + EveryDays + " " + Plural(EveryDays, "день", "дня", "дней"));
            else if (MedTimes.Count > 0)
                parts.Add(string.Join(", ", MedTimes.OrderBy(t => t).Select(t => t.ToString(@"hh\:mm"))));

            if (CourseDays > 0)
                parts.Add("курс " + CourseDays + " " + Plural(CourseDays, "день", "дня", "дней") +
                          " с " + CourseStart.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("ru-RU")));
            else
                parts.Add("бессрочно");

            var s = string.Join(", ", parts);
            if (!string.IsNullOrWhiteSpace(Dose)) s = "доза " + Dose + ", " + s;
            return s;
        }

        public static string DayName(DayOfWeek d)
        {
            switch (d)
            {
                case DayOfWeek.Monday: return "Понедельник";
                case DayOfWeek.Tuesday: return "Вторник";
                case DayOfWeek.Wednesday: return "Среда";
                case DayOfWeek.Thursday: return "Четверг";
                case DayOfWeek.Friday: return "Пятница";
                case DayOfWeek.Saturday: return "Суббота";
                default: return "Воскресенье";
            }
        }

        public static string KindName(EventKind k)
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

        public static string Plural(int n, string one, string few, string many)
        {
            int m10 = Math.Abs(n) % 10, m100 = Math.Abs(n) % 100;
            if (m10 == 1 && m100 != 11) return one;
            if (m10 >= 2 && m10 <= 4 && (m100 < 12 || m100 > 14)) return few;
            return many;
        }
    }
}
