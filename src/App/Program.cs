using System;
using System.Threading;
using System.Windows.Forms;

namespace Napominalka.App
{
    internal static class Program
    {
        private static Mutex _mutex;

        [STAThread]
        private static void Main()
        {
            // Один экземпляр: второй запуск просто показывает уже открытое окно.
            _mutex = new Mutex(true, "Напоминалка_WAM_ОдинЭкземпляр", out var isFirst);
            if (!isFirst) return;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);

            AppPaths.EnsureDirs();

            Application.ThreadException += (s, e) => ShowError(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => ShowError(e.ExceptionObject as Exception);

            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
            finally
            {
                _mutex.ReleaseMutex();
            }
        }

        private static void ShowError(Exception ex)
        {
            try
            {
                MessageBox.Show(
                    "Произошла ошибка: " + (ex?.Message ?? "неизвестная") +
                    "\n\nПрограмма продолжит работу. Если ошибка повторяется, покажите этот текст разработчику.",
                    "Напоминалка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch { }
        }
    }
}
