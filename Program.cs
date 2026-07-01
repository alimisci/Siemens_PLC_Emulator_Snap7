using System;
using System.Windows.Forms;

namespace S7Emulator
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize(); // .NET 6+ WinForms; on older versions use Application.EnableVisualStyles()
            Application.Run(new MainForm());
        }
    }
}
