using System;
using System.Windows.Forms;
using FSS_01.vistas;
using System.Threading;
using System.Linq;

namespace FSS_01
{
    internal static class Program
    {
        /// <summary>
        /// Punto de entrada principal para la aplicación.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);


            mainView main = new mainView();
            Application.Run(main);
        }
    }
}
