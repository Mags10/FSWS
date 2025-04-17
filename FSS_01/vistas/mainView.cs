using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;



namespace FSS_01.vistas
{
    public partial class mainView : Form
    {
        private CompiladorSx comp;

        [DllImport("user32.dll")]
        static extern int GetScrollPos(IntPtr hWnd, int nBar);
        [DllImport("user32.dll")]
        static extern int SetScrollPos(IntPtr hWnd, int nBar, int nPos, bool bRedraw);
        [DllImport("user32.dll")]
        static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        const int SB_VERT = 1;
        const int WM_VSCROLL = 0x115;
        const int SB_THUMBPOSITION = 4;


        public mainView()
        {
            InitializeComponent();
            richTextBox1.VScroll += (s, e) => SyncScroll(richTextBox1, richTextBox3);

            comp = new CompiladorSx();
            
            string path = "E:\\source\\Mags10\\FSWS\\FSS_01\\tests\\AEA\\p02.asm";
            comp.loadCode(path);
            comp.compile();
            setRichTextBoxCode();
            /*
            tab.addTab("Tabla Intermedia", comp.midFile.dataGridView);
            for (int i = 0; i < comp.secciones.Count; i++)
            {
                tab.addTab("Tabla de Simbolos " + i, comp.secciones[i].symTable.dataGridView);
                tab.addTab("Tabla de Bloques " + i, comp.secciones[i].blockTable.dataGridView);
            }
            RichTextBox rtb = new RichTextBox();
            rtb.Dock = DockStyle.Fill;
            rtb.Text = comp.programObj;
            tab.addTab("Código Fuente", rtb);
            this.splitContainer3.Panel1.Controls.Add(tab.tabControl);*/

            // Crear groupBox para Tabla intermedia
            GroupBox groupBox = new GroupBox();
            groupBox.Text = "Tabla Intermedia";
            groupBox.Dock = DockStyle.Fill;
            // refrescar el tamaño de las columnas para que se aplique el ancho fijo
            comp.midFile.dataGridView.Refresh();
            groupBox.Controls.Add(comp.midFile.dataGridView);
            splitContainer2.Panel1.Controls.Add(groupBox);

            // crear un una vista de tabs segun la cantidad de secciones
            // Cada seccion tiene un splitContainer vertical con dos paneles, uno para la tabla de simbolos y otro para la tabla de bloques
            // Va en el panel 2 del splitContainer3
            tabView tab = new tabView("Tablas de Simbolos y Bloques");
            tab.Dock = DockStyle.Fill;
            splitContainer2.Panel2.Controls.Add(tab.tabControl);
            for (int i = 0; i < comp.secciones.Count; i++)
            {
                // Crear splitContainer vertical
                SplitContainer splitContainer = new SplitContainer();
                splitContainer.Dock = DockStyle.Fill;
                splitContainer.Orientation = Orientation.Horizontal;
                splitContainer.SplitterDistance = 200;
                // Crear groupBox para Tabla de Simbolos
                GroupBox groupBox1 = new GroupBox();
                groupBox1.Text = "Tabla de Simbolos - Sección " + i;
                groupBox1.Dock = DockStyle.Fill;
                groupBox1.Controls.Add(comp.secciones[i].symTable.dataGridView);
                // Crear groupBox para Tabla de Bloques
                GroupBox groupBox2 = new GroupBox();
                groupBox2.Text = "Tabla de Bloques - Sección " + i;
                groupBox2.Dock = DockStyle.Fill;
                groupBox2.Controls.Add(comp.secciones[i].blockTable.dataGridView);
                // Agregar groupBox al panel superior del splitContainer
                splitContainer.Panel1.Controls.Add(groupBox1);
                // Crear otro splitContainer para el codigo obj de la seccion
                SplitContainer splitContainer2 = new SplitContainer();
                splitContainer2.Dock = DockStyle.Fill;
                splitContainer2.Orientation = Orientation.Horizontal;
                splitContainer2.SplitterDistance = 200;
                splitContainer2.Panel1.Controls.Add(groupBox2);
                // Crear groupBox para el codigo obj
                GroupBox groupBox3 = new GroupBox();
                groupBox3.Text = "Código Objeto - Sección " + i;
                groupBox3.Dock = DockStyle.Fill;
                // Crear RichTextBox para el codigo obj
                RichTextBox rtb = new RichTextBox();
                rtb.Dock = DockStyle.Fill;
                rtb.Text = comp.secciones[i].objCode;
                rtb.Font = new Font("Consolas", 10);
                rtb.SelectionColor = Color.Black;
                rtb.SelectionTabs = new int[] { 50, 100, 150, 200 };
                rtb.ReadOnly = true;
                groupBox3.Controls.Add(rtb);
                // Agregar groupBox al panel inferior del splitContainer
                splitContainer2.Panel2.Controls.Add(groupBox3);
                // Agregar el splitContainer al panel inferior del splitContainer
                splitContainer.Panel2.Controls.Add(splitContainer2);
                // Crear tabPage para la seccion
                TabPage tabPage = new TabPage("Sección " + i);
                tabPage.Controls.Add(splitContainer);
                // Agregar tabPage al tabControl
                tab.tabControl.TabPages.Add(tabPage);
            }
        }

        private void richTextBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F4)
            {
                e.SuppressKeyPress = true;

                // Guardamos la posición inicial del cursor
                int selectionStart = richTextBox1.SelectionStart;
                int lineIndex = richTextBox1.GetLineFromCharIndex(selectionStart); // Obtener la línea actual

                // Primero procesamos el código
                string code = richTextBox1.Text;
                comp.setCode(code);
                comp.procLines();

                // Ahora insertamos un salto de línea (sin sobrescribir el texto)
                richTextBox1.AppendText(Environment.NewLine);

                // Restauramos la posición del cursor después del salto de línea
                richTextBox1.SelectionStart = richTextBox1.GetFirstCharIndexFromLine(lineIndex) + Environment.NewLine.Length;
                richTextBox1.SelectionLength = 0;

                // Actualizamos el RichTextBox después de procesar el texto
                setRichTextBoxCode();

                e.Handled = true;
            }
        }
        private void setRichTextBoxCode()
        {
            try
            {
                // 1) Preparar RichTextBox
                richTextBox1.Clear();
                richTextBox1.Font = new Font("Consolas", 10);                              // Monoespaciada :contentReference[oaicite:6]{index=6}
                richTextBox1.SelectionColor = Color.Black;

                // 2) Medir anchos de columna
                int maxEtqLen = 0, maxInsLen = 0;
                foreach (var line in comp.lineas)
                {
                    int etqLen = line.etq != null ? line.etq.Payload.GetText().Length : 0;
                    int insLen = line.ins != null ? line.ins.ToString().Length : 0;
                    maxEtqLen = Math.Max(maxEtqLen, etqLen);
                    maxInsLen = Math.Max(maxInsLen, insLen);
                }

                // 3) Pintar línea por línea
                foreach (var line in comp.lineas)
                {
                    // --- ETIQUETA ---
                    string etqText = (line.etq?.Payload.GetText() ?? "")
                                     .PadRight(maxEtqLen + 2);                           // Alinear con espacios :contentReference[oaicite:7]{index=7}
                    richTextBox1.SelectionColor = ColorTranslator.FromHtml("#003366");          // Azul oscuro
                    richTextBox1.AppendText(etqText);

                    // --- INSTRUCCIÓN / DIRECTIVA ---
                    string insText = (line.ins?.ToString() ?? "")
                                     .PadRight(maxInsLen + 2);
                    if (line.ins != null && IsCodop(line.ins.Symbol.Type))
                        richTextBox1.SelectionColor = ColorTranslator.FromHtml("#006400");      // Verde oscuro
                    else if (line.ins != null && IsDirective(line.ins.Symbol.Type))
                        richTextBox1.SelectionColor = ColorTranslator.FromHtml("#8B6600");      // Rojo oscuro
                    else
                        richTextBox1.SelectionColor = Color.Black;
                    richTextBox1.AppendText(insText);

                    // --- OPERANDOS ---
                    foreach (var oper in line.opers.Where(o => o != null))
                    {
                        switch (oper.Symbol.Type)
                        {
                            case 24: // REG
                                richTextBox1.SelectionColor = ColorTranslator.FromHtml("#800080"); break; // Púrpura
                            case 25: // NUM
                                richTextBox1.SelectionColor = ColorTranslator.FromHtml("#FF8C00"); break; // Naranja oscuro
                            case 26: // CONSTHEX
                                richTextBox1.SelectionColor = ColorTranslator.FromHtml("#008B8B"); break; // Cian oscuro
                            case 27: // CONSTCAD
                                richTextBox1.SelectionColor = ColorTranslator.FromHtml("#8B4513"); break; // Marrón
                            case 29: // SIMB
                                richTextBox1.SelectionColor = ColorTranslator.FromHtml("#003366"); break; // Mismo azul de etiqueta
                            case 30: // EXPRE
                                richTextBox1.SelectionColor = ColorTranslator.FromHtml("#008B00"); break; // Verde claro
                            default:
                                richTextBox1.SelectionColor = Color.Black; break;
                        }
                        richTextBox1.AppendText(oper.GetText() + ", ");                            // AppendText preserva formato :contentReference[oaicite:8]{index=8}
                    }

                    // 4) Eliminar última coma sin resetear formato
                    if (line.opers.Any(o => o != null))
                    {
                        int pos = richTextBox1.TextLength - 2;
                        richTextBox1.Select(pos, 2);                                              // Seleccionar los últimos 2 caracteres
                        richTextBox1.SelectedText = "";                                            // Reemplazar por cadena vacía :contentReference[oaicite:9]{index=9}
                    }

                    // 5) Salto de línea y reset de color
                    richTextBox1.SelectionColor = Color.Black;
                    richTextBox1.AppendText(Environment.NewLine);

                    // 5.1) Resaltar en rojo si hay error
                    if (!string.IsNullOrEmpty(line.error))
                    {
                        int start = richTextBox1.GetFirstCharIndexOfCurrentLine();
                        int length = richTextBox1.TextLength - start;

                        richTextBox1.Select(start, length);
                        richTextBox1.SelectionBackColor = Color.FromArgb(255, 200, 200); // Rojo claro, personalizable
                    }

                }

                // 6) Llevar caret al final
                richTextBox1.SelectionStart = richTextBox1.TextLength;
                richTextBox1.ScrollToCaret();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }


        // Helpers
        private bool IsCodop(int type)
        {
            return type == 19 || type == 20 || type == 21 || type == 22 || type == 23; // CODOPs
        }

        // Método para verificar si el tipo es una directiva
        private bool IsDirective(int type)
        {
            return type >= 2 && type <= 16; // Directivas (RESB, RESW, WORD, BYTE, EQU, BASE, etc.)
        }


        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {
        }

        private void toolStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {

        }

        private void richTextBox1_TextChanged_1(object sender, EventArgs e)
        {
            // Dar el mismo formato al richTextBox2
            richTextBox3.Clear();
            richTextBox3.Font = new Font("Consolas", 10);
            richTextBox3.SelectionColor = Color.Black;
            richTextBox3.Font = richTextBox1.Font;
            richTextBox3.SelectionTabs = richTextBox1.SelectionTabs;

            // Contar líneas y en richTextBox3 poner el número de líneas
            int lineCount = richTextBox1.Lines.Length;
            richTextBox3.Clear();
            for (int i = 1; i <= lineCount; i++)
            {
                richTextBox3.AppendText(i.ToString() + Environment.NewLine);
            }
        }

        private void SyncScroll(RichTextBox source, RichTextBox target)
        {
            int scrollPos = GetScrollPos(source.Handle, SB_VERT);
            SetScrollPos(target.Handle, SB_VERT, scrollPos, true);
            SendMessage(target.Handle, WM_VSCROLL, (scrollPos << 16) | SB_THUMBPOSITION, 0);
        }

    }
}
