using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;
using Napominalka.Core;

namespace Napominalka.App
{
    /// <summary>Пути. Всё лежит рядом с программой — она портабельная.</summary>
    public static class AppPaths
    {
        public static string BaseDir => AppContext.BaseDirectory;
        public static string RemindersFile => Path.Combine(BaseDir, "напоминания.txt");
        public static string SettingsFile => Path.Combine(BaseDir, "настройки.txt");
        public static string JournalFile => Path.Combine(BaseDir, "журнал.txt");
        public static string MelodyDir => Path.Combine(BaseDir, "Мелодии");
        public static string ExePath => Path.Combine(BaseDir, "Напоминалка.exe");

        public static string CurrentExe()
        {
            try
            {
                var p = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(p)) return p;
            }
            catch { }
            return Path.Combine(BaseDir, Environment.ProcessPath ?? "Напоминалка.exe");
        }

        public static void EnsureDirs()
        {
            try { Directory.CreateDirectory(MelodyDir); } catch { }
            UnpackDefaultMelody();
        }

        /// <summary>
        /// Достаёт встроенную мелодию в папку Мелодии, если её там ещё нет.
        /// Нужна, чтобы сигнал звучал сразу после распаковки программы,
        /// даже если рядом лежит один только exe.
        /// </summary>
        public static void UnpackDefaultMelody()
        {
            const string name = "по умолчанию.wav";
            try
            {
                var target = Path.Combine(MelodyDir, name);
                if (File.Exists(target)) return;

                var asm = Assembly.GetExecutingAssembly();
                var res = asm.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("по умолчанию.wav", StringComparison.OrdinalIgnoreCase));
                if (res == null) return;

                using var src = asm.GetManifestResourceStream(res);
                if (src == null) return;
                using var dst = File.Create(target);
                src.CopyTo(dst);
            }
            catch { }
        }
    }

    /// <summary>Автозапуск в пользовательской ветке реестра. Прав администратора не требует.</summary>
    public static class Autostart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "Напоминалка";

        public static bool IsEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
                var v = key?.GetValue(ValueName) as string;
                return !string.IsNullOrEmpty(v);
            }
            catch { return false; }
        }

        public static void Enable()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
                key?.SetValue(ValueName, "\"" + AppPaths.CurrentExe() + "\"");
            }
            catch { }
        }

        public static void Disable()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
                key?.DeleteValue(ValueName, false);
            }
            catch { }
        }

        /// <summary>
        /// Программа могла переехать на другое место (флешка) — тогда старая
        /// запись смотрит в никуда. Проверяем и переписываем путь.
        /// </summary>
        public static void RepairIfNeeded()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
                var v = key?.GetValue(ValueName) as string;
                if (string.IsNullOrEmpty(v)) return;
                var current = "\"" + AppPaths.CurrentExe() + "\"";
                if (!string.Equals(v.Trim(), current, StringComparison.OrdinalIgnoreCase))
                {
                    using var w = Registry.CurrentUser.OpenSubKey(RunKey, true);
                    w?.SetValue(ValueName, current);
                }
            }
            catch { }
        }
    }

    /// <summary>Озвучка: сначала голосом NVDA, если клиент доступен, иначе системным голосом.</summary>
    public static class Speech
    {
        private const int SVSFlagsAsync = 1;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private delegate int SpeakTextDelegate(string text);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int TestRunningDelegate();

        private static IntPtr _nvda;
        private static SpeakTextDelegate _nvdaSpeak;
        private static bool _nvdaChecked;

        private static void InitNvda()
        {
            if (_nvdaChecked) return;
            _nvdaChecked = true;
            try
            {
                string[] candidates =
                {
                    Path.Combine(AppPaths.BaseDir, "nvdaControllerClient64.dll"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "NVDA", "nvdaControllerClient64.dll"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "NVDA", "nvdaControllerClient64.dll"),
                    "nvdaControllerClient64.dll"
                };

                foreach (var path in candidates)
                {
                    if (path != "nvdaControllerClient64.dll" && !File.Exists(path)) continue;
                    var h = LoadLibrary(path);
                    if (h == IntPtr.Zero) continue;
                    var p = GetProcAddress(h, "nvdaController_speakText");
                    if (p == IntPtr.Zero) continue;
                    _nvda = h;
                    _nvdaSpeak = Marshal.GetDelegateForFunctionPointer<SpeakTextDelegate>(p);
                    break;
                }
            }
            catch { }
        }

        public static bool NvdaAvailable()
        {
            InitNvda();
            return _nvdaSpeak != null;
        }

        public static bool Speak(string text, string mode)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            if (string.Equals(mode, "Не озвучивать", StringComparison.OrdinalIgnoreCase)) return false;

            if (string.Equals(mode, "NVDA", StringComparison.OrdinalIgnoreCase))
            {
                InitNvda();
                if (_nvdaSpeak != null)
                {
                    try { if (_nvdaSpeak(text) == 0) return true; } catch { }
                }
            }

            return SpeakSystem(text);
        }

        /// <summary>Системный голос (SAPI). Есть на любой Windows.</summary>
        public static bool SpeakSystem(string text)
        {
            try
            {
                var t = Type.GetTypeFromProgID("SAPI.SpVoice");
                if (t == null) return false;
                var voice = Activator.CreateInstance(t);
                t.InvokeMember("Speak", BindingFlags.InvokeMethod, null, voice, new object[] { text, SVSFlagsAsync });
                return true;
            }
            catch { return false; }
        }
    }

    /// <summary>Проигрывание мелодии: MCI тянет и wav, и mp3.</summary>
    public static class MelodyPlayer
    {
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        private static extern int mciSendString(string command, StringBuilder ret, int retLength, IntPtr hwnd);

        // Псевдоним для MCI держим латиницей: кириллицу в командной строке
        // MCI разбирает ненадёжно, а имя файла при этом русское и это нормально.
        private const string Alias = "wam_napominalka_melody";
        private static bool _opened;

        public static bool Play(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            Stop();
            try
            {
                var err = mciSendString("open \"" + path + "\" alias " + Alias, null, 0, IntPtr.Zero);
                if (err != 0)
                {
                    Fallback(path);
                    return false;
                }
                _opened = true;
                mciSendString("play " + Alias, null, 0, IntPtr.Zero);
                return true;
            }
            catch
            {
                Fallback(path);
                return false;
            }
        }

        private static void Fallback(string path)
        {
            try
            {
                if (Path.GetExtension(path).Equals(".wav", StringComparison.OrdinalIgnoreCase))
                    new System.Media.SoundPlayer(path).Play();
            }
            catch { }
        }

        public static void Stop()
        {
            try
            {
                if (!_opened) return;
                mciSendString("stop " + Alias, null, 0, IntPtr.Zero);
                mciSendString("close " + Alias, null, 0, IntPtr.Zero);
                _opened = false;
            }
            catch { }
        }
    }

    /// <summary>Журнал приёма лекарств.</summary>
    public static class Journal
    {
        public static void Append(string path, string title, string action)
        {
            try
            {
                var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) +
                           "; " + title + "; " + action + Environment.NewLine;
                File.AppendAllText(path, line, new UTF8Encoding(true));
            }
            catch { }
        }

        public static List<string> Read(string path, int maxLines)
        {
            try
            {
                if (!File.Exists(path)) return new List<string>();
                var lines = File.ReadAllLines(path, Encoding.UTF8).Where(l => l.Trim().Length > 0).ToList();
                if (lines.Count > maxLines) lines = lines.Skip(lines.Count - maxLines).ToList();
                return lines;
            }
            catch { return new List<string>(); }
        }

        public static void Trim(string path, int keepDays)
        {
            if (keepDays <= 0) return;
            try
            {
                if (!File.Exists(path)) return;
                var limit = DateTime.Now.Date.AddDays(-keepDays);
                var kept = File.ReadAllLines(path, Encoding.UTF8)
                    .Where(l =>
                    {
                        var parts = l.Split(';');
                        if (parts.Length == 0) return true;
                        return !DateTime.TryParseExact(parts[0].Trim(), "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                               || d >= limit;
                    })
                    .ToList();
                IniFile.WriteFile(path, string.Join(Environment.NewLine, kept) + Environment.NewLine);
            }
            catch { }
        }
    }

    /// <summary>
    /// Проверка обновлений. Пока адрес не задан — проверка выключена и молчит.
    /// Запрос публичный, без всяких токенов: программа только смотрит версию.
    /// </summary>
    public static class Updater
    {
        public static Version CurrentVersion =>
            Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);

        public class Result
        {
            public bool HasUpdate;
            public string NewVersion = "";
            public string DownloadUrl = "";
            public string Message = "";
        }

        public static Result Check(string url)
        {
            var res = new Result();
            if (string.IsNullOrWhiteSpace(url))
            {
                res.Message = "Адрес обновлений не задан.";
                return res;
            }

            try
            {
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(20);
                http.DefaultRequestHeaders.UserAgent.ParseAdd("Napominalka/" + CurrentVersion);
                var json = http.GetStringAsync(url).GetAwaiter().GetResult();

                using var doc = System.Text.Json.JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("tag_name", out var tag)) res.NewVersion = tag.GetString() ?? "";

                if (doc.RootElement.TryGetProperty("assets", out var assets) && assets.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var a in assets.EnumerateArray())
                    {
                        if (a.TryGetProperty("browser_download_url", out var u))
                        {
                            var link = u.GetString() ?? "";
                            if (link.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) { res.DownloadUrl = link; break; }
                        }
                    }
                }

                var clean = res.NewVersion.TrimStart('v', 'V');
                if (Version.TryParse(clean, out var remote) && remote > CurrentVersion)
                {
                    res.HasUpdate = true;
                    res.Message = "Доступна версия " + clean + ".";
                }
                else
                {
                    res.Message = "Установлена последняя версия.";
                }
            }
            catch (Exception ex)
            {
                res.Message = "Не удалось проверить обновления: " + ex.Message;
            }
            return res;
        }
    }
}
