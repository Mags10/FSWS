using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Antlr4.Runtime;

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
            //Application.Run(new Form1());
            string path = "E:/source/FSWS/FSS_01/test/code3.asm";
            CompiladorSx comp = new CompiladorSx(path);
            comp.compile();
            string exit = "--------------------------------------------------------\n";
            exit += comp.results();
            System.IO.File.AppendAllLines("E:/source/FSWS/FSS_01/test/out.txt", new string[] { exit });
        }
    }
}
