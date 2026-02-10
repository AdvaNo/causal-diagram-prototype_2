using System;
using System.Windows.Forms;
using CausalDiagram.Core;

namespace CausalDiagram // <-- Убедись, что тут твой namespace!
{
    static class Program
    {
        /// <summary>
        /// Главная точка входа для приложения.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Запускаем нашу форму
            Application.Run(new MainForm());
        }
    }
}