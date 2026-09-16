using System;
using System.Collections.Generic;
using System.Linq;

namespace Napominalka.Core
{
    /// <summary>
    /// Расчёт следующего срабатывания. Чистая логика без Windows — её
    /// проверяют тесты, которые гоняются прямо здесь, на Linux.
    /// </summary>
    public static class Scheduler
    {
        public static DateTime? NextOccurrence(Reminder r, DateTime after)
        {
            if (r == null || !r.Enabled) return null;
            return r.IsMedicine ? NextMedicine(r, after) : NextByRepeat(r, after);
        }

        /// <summary>Ближайшее срабатывание в прошлом — чтобы сообщить о пропущенном.</summary>
        public static DateTime? LastMissed(Reminder r, DateTime now)
        {
            if (r == null || !r.Enabled) return null;
            var from = now.AddDays(-1);
            var next = NextOccurrence(r, from);
            if (next.HasValue && next.Value <= now) return next;
            return null;
        }

        private static DateTime? NextByRepeat(Reminder r, DateTime after)
        {
            switch (r.Repeat)
            {
                case RepeatMode.Once:
                    return r.OnceAt > after ? r.OnceAt : (DateTime?)null;

                case RepeatMode.ExactDate:
                {
                    var dt = r.ExactDate.Date + r.TimeOfDay;
                    return dt > after ? dt : (DateTime?)null;
                }

                case RepeatMode.Weekdays:
                {
                    if (r.Days == null || r.Days.Count == 0) return null;
                    var start = after.Date + r.TimeOfDay;
                    for (int i = 0; i <= 8; i++)
                    {
                        var cand = start.AddDays(i);
                        if (cand > after && r.Days.Contains(cand.DayOfWeek)) return cand;
                    }
                    return null;
                }
            }
            return null;
        }

        private static DateTime? NextMedicine(Reminder r, DateTime after)
        {
            var times = (r.MedTimes != null && r.MedTimes.Count > 0)
                ? r.MedTimes.OrderBy(t => t).ToList()
                : new List<TimeSpan> { r.TimeOfDay };

            DateTime? lastDay = r.CourseDays > 0
                ? r.CourseStart.Date.AddDays(r.CourseDays - 1)
                : (DateTime?)null;

            DateTime? best = null;

            void Consider(DateTime cand)
            {
                if (cand <= after) return;
                if (lastDay.HasValue && cand.Date > lastDay.Value) return;
                if (best == null || cand < best.Value) best = cand;
            }

            if (r.EveryHours > 0)
            {
                var anchor = r.CourseStart.Date + times[0];
                var step = TimeSpan.FromHours(r.EveryHours);
                var cand = anchor;
                if (cand <= after)
                {
                    var k = (long)Math.Floor((after - anchor).TotalHours / r.EveryHours);
                    cand = anchor.AddHours((double)k * r.EveryHours);
                }
                // страховка от дробных погрешностей
                for (int guard = 0; guard < 100 && cand <= after; guard++) cand = cand.Add(step);
                if (cand > after) Consider(cand);
                else
                {
                    for (int i = 0; i < 100000; i++)
                    {
                        cand = cand.Add(step);
                        if (cand > after) { Consider(cand); break; }
                    }
                }
            }
            else if (r.EveryDays > 0)
            {
                for (int i = 0; i < 5000; i++)
                {
                    var day = r.CourseStart.Date.AddDays((long)i * r.EveryDays);
                    if (lastDay.HasValue && day > lastDay.Value) break;
                    foreach (var t in times) Consider(day + t);
                    if (best != null) break;
                }
            }
            else
            {
                for (int i = 0; i < 400; i++)
                {
                    var day = r.CourseStart.Date.AddDays(i);
                    if (lastDay.HasValue && day > lastDay.Value) break;
                    foreach (var t in times) Consider(day + t);
                    if (best != null) break;
                }
            }

            return best;
        }
    }
}
