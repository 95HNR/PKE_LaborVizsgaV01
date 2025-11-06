using System;
using System.Windows.Forms;

namespace UMFST.MIP.Variant1_Bookstore
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            // A MainWindow indítása
            Application.Run(new MainWindow());
        }
    }
}