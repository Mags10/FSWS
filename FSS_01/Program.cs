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

            string path = "E:\\source\\Mags10\\FSWS\\FSS_01\\tests\\P08\\BLOQUES_COPY_START0H.asm";
            //string path = "E:\\source\\Mags10\\FSWS\\FSS_01\\tests\\P08\\BLOQUES_EJEM2_START0H.asm";
            //string path = "E:\\source\\Mags10\\FSWS\\FSS_01\\tests\\P08\\BLOQUES_EJERFINAL_START0H.asm";
            CompiladorSx comp = new CompiladorSx(path);
            comp.compile();

            tabView tab = new tabView("Tablas");
            tab.addTab("Tabla Intermedia", comp.midFile.dataGridView);
            tab.addTab("Tabla de Simbolos", comp.symTable.dataGridView);
            tab.addTab("Tabla de Bloques", comp.blockTable.dataGridView);
            // Crear un richTextBox para mostrar el código fuente
            RichTextBox rtb = new RichTextBox();
            rtb.Dock = DockStyle.Fill;
            rtb.Text = comp.programObj;
            tab.addTab("Código Fuente", rtb);

            Application.Run(tab);
        }
    }
}
