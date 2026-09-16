using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace Napominalka.App
{
    /// <summary>Инструкция по клавише F1.</summary>
    public class HelpForm : Form
    {
        public HelpForm()
        {
            Text = "Инструкция";
            Width = 760;
            Height = 620;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;

            var box = new TextBox
            {
                Left = 12,
                Top = 12,
                Width = 720,
                Height = 520,
                Multiline = true,
                ReadOnly = true,
                WordWrap = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new System.Drawing.Font("Segoe UI", 11f),
                Text = LoadHelpText(),
                AccessibleName = "Текст инструкции",
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            Controls.Add(box);

            var close = new Button
            {
                Text = "Закрыть",
                Left = 622,
                Top = 542,
                Width = 110,
                Height = 32,
                AccessibleName = "Закрыть инструкцию",
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            close.Click += (s, e) => Close();
            Controls.Add(close);

            CancelButton = close;
            box.Select(0, 0);
            Shown += (s, e) => { box.Focus(); box.Select(0, 0); };
        }

        private static string LoadHelpText()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var name = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("справка.txt", StringComparison.OrdinalIgnoreCase));
                if (name == null) return "Файл инструкции не найден.";
                using var stream = asm.GetManifestResourceStream(name);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                return reader.ReadToEnd();
            }
            catch (Exception ex)
            {
                return "Не удалось прочитать инструкцию: " + ex.Message;
            }
        }
    }
}
