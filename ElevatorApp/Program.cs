using System;
using System.Windows.Forms;

namespace ElevatorApp
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            try { LogRepository.Initialise(); }
            catch (Exception ex)
            {
                MessageBox.Show("Could not initialise the log database:\n" + ex.Message,
                    "Elevator", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            Application.Run(new MainForm());
        }
    }
}