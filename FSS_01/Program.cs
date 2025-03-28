using System;
using System.Windows.Forms;
using FSS_01.vistas;
using System.Threading;

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

            string path = "E:\\source\\Mags10\\FSWS\\FSS_01\\tests\\P03\\code2.asm";
            CompiladorSx comp = new CompiladorSx(path);
            comp.compile();

            tabView tab = new tabView("Tablas");
            tab.addTab("Tabla Intermedia", comp.midFile.dataGridView);
            tab.addTab("Tabla de Simbolos", comp.symTable.dataGridView);

            Application.Run(tab);
        }
    }
}
