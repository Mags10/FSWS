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

        private bool beautify = false;

        public mainView()
        {
            InitializeComponent();
            richTextBox1.VScroll += (s, e) => SyncScroll(richTextBox1, richTextBox3);

            comp = new CompiladorSx();

            // Obtener directorio de test
            string testDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../tests");
            // Obtener estructura de directorios y archivos asm, ponerlo en ejemplosToolStripMenuItem en forma de submenú
            // Si hay subdirectorios, crear un submenú por cada uno

            string[] subDirs = System.IO.Directory.GetDirectories(testDir);
            foreach (string subDir in subDirs)
            {
                string dirName = System.IO.Path.GetFileName(subDir);
                ToolStripMenuItem subMenu = new ToolStripMenuItem(dirName);
                ejemplosToolStripMenuItem.DropDownItems.Add(subMenu);
                // Obtener archivos asm en el subdirectorio
                string[] asmFiles = System.IO.Directory.GetFiles(subDir, "*.asm");
                foreach (string asmFile in asmFiles)
                {
                    string fileName = System.IO.Path.GetFileName(asmFile);
                    ToolStripMenuItem fileMenuItem = new ToolStripMenuItem(fileName);
                    fileMenuItem.Click += (s, e) => openFromFile(asmFile);
                    subMenu.DropDownItems.Add(fileMenuItem);
                }
            }

        }

        public void codObjGen()
        {
            toolStripStatusLabel1.Text = "Generando código objeto...";
            segundoPaso(false);
            toolStripStatusLabel1.Text = comp.thirdStepExec();
            refreshDashboard();
        }

        public void segundoPaso(bool update = true)
        {
            if (update) toolStripStatusLabel1.Text = "Ejecutando segundo paso...";
            primerPaso(false);
            toolStripStatusLabel1.Text = comp.secondStepExec();
            if (update) refreshDashboard();
        }

        public void primerPaso(bool update = true)
        {
            if (update) toolStripStatusLabel1.Text = "Ejecutando primer paso...";
            comp.setCode(richTextBox1.Text);
            toolStripStatusLabel1.Text = comp.firstStepExec();
            if(update) refreshDashboard();
        }

        public void refreshDashboard()
        {
            analisisLexSic();
            comp.createTables();
            splitContainer1.Panel2Collapsed = false;
            // Crear groupBox para Tabla intermedia
            GroupBox groupBox = new GroupBox();
            groupBox.Text = "Tabla Intermedia";
            groupBox.Dock = DockStyle.Fill;
            // refrescar el tamaño de las columnas para que se aplique el ancho fijo
            comp.midFile.dataGridView.Refresh();
            groupBox.Controls.Clear();
            groupBox.Controls.Add(comp.midFile.dataGridView);
            splitContainer2.Panel1.Controls.Clear();
            splitContainer2.Panel1.Controls.Add(groupBox);

            // crear un una vista de tabs segun la cantidad de secciones
            // Cada seccion tiene un splitContainer vertical con dos paneles, uno para la tabla de simbolos y otro para la tabla de bloques
            // Va en el panel 2 del splitContainer3
            tabView tab = new tabView("Tablas de Simbolos y Bloques");
            tab.Dock = DockStyle.Fill;
            splitContainer2.Panel2.Controls.Clear();
            splitContainer2.Panel2.Controls.Add(tab.tabControl);
            for (int i = 0; i < comp.secciones.Count; i++)
            {
                // Crear SplitContainer principal (dividir en 1/3 y 2/3)
                SplitContainer splitContainer1 = new SplitContainer();
                splitContainer1.Dock = DockStyle.Fill;
                splitContainer1.Orientation = Orientation.Horizontal;

                // Crear SplitContainer secundario (dividir los 2/3 en 1/2 y 1/2)
                SplitContainer splitContainer2 = new SplitContainer();
                splitContainer2.Dock = DockStyle.Fill;
                splitContainer2.Orientation = Orientation.Horizontal;

                // === Tabla de Símbolos ===
                GroupBox groupBox1 = new GroupBox();
                groupBox1.Text = "Tabla de Símbolos - Sección " + i;
                groupBox1.Dock = DockStyle.Fill;
                groupBox1.Controls.Add(comp.secciones[i].symTable.dataGridView);

                // === Tabla de Bloques ===
                GroupBox groupBox2 = new GroupBox();
                groupBox2.Text = "Tabla de Bloques - Sección " + i;
                groupBox2.Dock = DockStyle.Fill;
                groupBox2.Controls.Add(comp.secciones[i].blockTable.dataGridView);

                // === Código Objeto (visualizar) ===
                GroupBox groupBox3 = new GroupBox();
                groupBox3.Text = "Código Objeto - Sección " + i;
                groupBox3.Dock = DockStyle.Fill;

                RichTextBox rtb = new RichTextBox();
                rtb.Dock = DockStyle.Fill;
                rtb.Text = comp.secciones[i].objCode;
                rtb.Font = new Font("Consolas", 10);
                rtb.SelectionColor = Color.Black;
                rtb.SelectionTabs = new int[] { 50, 100, 150, 200 };
                rtb.ReadOnly = true;

                groupBox3.Controls.Add(rtb);

                // Armar SplitContainers
                splitContainer2.Panel1.Controls.Add(groupBox2);  // 2/3 arriba - bloques
                splitContainer2.Panel2.Controls.Add(groupBox3);  // 2/3 abajo - obj code

                splitContainer1.Panel1.Controls.Add(groupBox1);      // 1/3 símbolos
                splitContainer1.Panel2.Controls.Add(splitContainer2); // 2/3 resto

                // Crear tabPage y agregar el contenedor principal
                TabPage tabPage = new TabPage("Sección " + i + " - " + comp.secciones[i].nombre.GetText());
                tabPage.Controls.Add(splitContainer1);

                // Ajustar dinámicamente el tamaño vertical a 1/3 para cada parte
                tabPage.Resize += (sender, e) =>
                {
                    int totalHeight = tabPage.Height;
                    splitContainer1.SplitterDistance = totalHeight / 3;
                    splitContainer2.SplitterDistance = (totalHeight * 2 / 3) / 2;
                };

                // Agregar tabPage al tabControl
                tab.tabControl.TabPages.Add(tabPage);
            }
            splitContainer1.Panel1.Refresh();
            splitContainer1.Panel2.Refresh();
            splitContainer2.Panel1.Refresh();
            splitContainer2.Panel2.Refresh();
        }

        private void analisisLexSic()
        {
            if (beautify) return;
            try
            {
                string code = richTextBox1.Text;
                comp.setCode(code);
                comp.firstStepExec();

                // 1) Preparar RichTextBox
                richTextBox1.Clear();
                richTextBox1.Font = new Font("Consolas", 10);
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

                    // Modo de direccionamiento 
                    if (line.modo != null)
                    {
                        string modoText = line.modo == "Inmediato" ? "#" : line.modo == "Indirecto" ? "@" : "";
                        richTextBox1.AppendText(modoText);
                    }

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


                    richTextBox4.Clear();
                    richTextBox4.AppendText(comp.lexelistener.getErrores());
                    richTextBox4.AppendText(comp.parslistener.getErrores());

                }

                // 6) Llevar caret al final
                richTextBox1.SelectionStart = richTextBox1.TextLength;
                richTextBox1.ScrollToCaret();
                beautify = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private bool IsCodop(int type)
        {
            return type == 19 || type == 20 || type == 21 || type == 22 || type == 23;
        }

        private bool IsDirective(int type)
        {
            return type >= 2 && type <= 16;
        }
        private void richTextBox1_TextChanged_1(object sender, EventArgs e)
        {
            isSaved = false;
            richTextBox3.Clear();
            richTextBox3.Font = new Font("Consolas", 10);
            richTextBox3.SelectionColor = Color.Black;
            richTextBox3.Font = richTextBox1.Font;
            richTextBox3.SelectionTabs = richTextBox1.SelectionTabs;

            // Contar líneas y en richTextBox3 poner el número de líneas
            int lineCount = richTextBox1.Lines.Length;
            richTextBox3.Clear();
            for (int i = 1; i <= lineCount; i++)
                richTextBox3.AppendText(i.ToString() + Environment.NewLine);

            beautify = false;
        }

        private void SyncScroll(RichTextBox source, RichTextBox target)
        {
            int scrollPos = GetScrollPos(source.Handle, SB_VERT);
            SetScrollPos(target.Handle, SB_VERT, scrollPos, true);
            SendMessage(target.Handle, WM_VSCROLL, (scrollPos << 16) | SB_THUMBPOSITION, 0);
        }

        private void toolStripButton5_Click(object sender, EventArgs e)
        {
            analisisLexSic();
        }

        private void análisisLexSinToolStripMenuItem_Click(object sender, EventArgs e)
        {
            analisisLexSic();
        }

        private void paso1ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            primerPaso();
        }

        private void paso2ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            segundoPaso();
        }

        private void generarOBJToolStripMenuItem_Click(object sender, EventArgs e)
        {
            codObjGen();
        }

        private void ensamblarToolStripMenuItem_Click(object sender, EventArgs e)
        {
            codObjGen();
        }

        private void toolStripButton4_Click(object sender, EventArgs e)
        {
            codObjGen();
        }


        // Variables para el menú de archivo
        private bool isSaved = true;
        private string currentFilePath = string.Empty;

        private void nuevoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Nuevo archivo
            if (!isSaved)
            {
                DialogResult result = MessageBox.Show("¿Desea guardar los cambios?", "Guardar", MessageBoxButtons.YesNoCancel);
                if (result == DialogResult.Yes)
                {
                    guardarToolStripMenuItem_Click(sender, e);
                    richTextBox1.Clear();
                    richTextBox3.Clear();
                    currentFilePath = string.Empty;
                    isSaved = true;
                    splitContainer2.Panel1.Controls.Clear();
                    splitContainer2.Panel2.Controls.Clear();
                    toolStripStatusLabel1.Text = "Nuevo archivo";
                }
                else if (result == DialogResult.No)
                {
                    richTextBox1.Clear();
                    richTextBox3.Clear();
                    currentFilePath = string.Empty;
                    isSaved = true;
                    splitContainer2.Panel1.Controls.Clear();
                    splitContainer2.Panel2.Controls.Clear();
                    toolStripStatusLabel1.Text = "Nuevo archivo";
                }
                else if (result == DialogResult.Cancel)
                {
                    toolStripStatusLabel1.Text = "Operación cancelada";
                    return;
                }
            }
            else
            {
                richTextBox1.Clear();
                richTextBox3.Clear();
                currentFilePath = string.Empty;
                isSaved = true;
                splitContainer2.Panel1.Controls.Clear();
                splitContainer2.Panel2.Controls.Clear();
                if (currentFilePath != string.Empty)
                {
                    toolStripStatusLabel1.Text = currentFilePath;
                }
                else
                {
                    toolStripStatusLabel1.Text = "Nuevo archivo sin guardar";
                }   
            }
        }

        private void abrirToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Abrir archivo
            // Verificar si hay cambios sin guardar
            if (!isSaved)
            {
                DialogResult result = MessageBox.Show("¿Desea guardar los cambios?", "Guardar", MessageBoxButtons.YesNoCancel);
                if (result == DialogResult.Yes)
                {
                    guardarToolStripMenuItem_Click(sender, e);
                }
                else if (result == DialogResult.No)
                {
                    richTextBox1.Clear();
                    richTextBox3.Clear();
                    currentFilePath = string.Empty;
                    isSaved = true;
                    splitContainer2.Panel1.Controls.Clear();
                    splitContainer2.Panel2.Controls.Clear();
                }
                else if (result == DialogResult.Cancel)
                {
                    toolStripStatusLabel1.Text = "Operación cancelada";
                    return;
                }
            }

            // Abrir archivo

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Archivos de ensamblador (*.asm)|*.asm|Todos los archivos (*.*)|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    currentFilePath = openFileDialog.FileName;
                    string code = System.IO.File.ReadAllText(currentFilePath);
                    richTextBox1.Clear();
                    richTextBox1.AppendText(code);
                    richTextBox1.SelectionStart = 0;
                    richTextBox1.ScrollToCaret();
                    isSaved = true;
                    splitContainer2.Panel1.Controls.Clear();
                    splitContainer2.Panel2.Controls.Clear();
                    toolStripStatusLabel1.Text = currentFilePath;
                }
                else
                {
                    toolStripStatusLabel1.Text = "Operación cancelada";
                    return;
                }
            }

        }

        private void guardarToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Guardar archivo
            if (string.IsNullOrEmpty(currentFilePath))
            {
                using (SaveFileDialog saveFileDialog = new SaveFileDialog())
                {
                    saveFileDialog.Filter = "Archivos de ensamblador (*.asm)|*.asm|Todos los archivos (*.*)|*.*";
                    if (saveFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        currentFilePath = saveFileDialog.FileName;
                        System.IO.File.WriteAllText(currentFilePath, richTextBox1.Text);
                        isSaved = true;
                        toolStripStatusLabel1.Text = currentFilePath;
                    }
                }
            }
            else
            {
                System.IO.File.WriteAllText(currentFilePath, richTextBox1.Text);
                isSaved = true;
                toolStripStatusLabel1.Text = currentFilePath;
            }
        }

        private void guardarComoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Guardar como
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "Archivos de ensamblador (*.asm)|*.asm|Todos los archivos (*.*)|*.*";
                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    currentFilePath = saveFileDialog.FileName;
                    System.IO.File.WriteAllText(currentFilePath, richTextBox1.Text);
                    isSaved = true;
                    toolStripStatusLabel1.Text = "Archivo guardado como: " + currentFilePath;
                }
            }
        }

        // Para abrir un archivo de ejemplo
        private void openFromFile(string path)
        {
            // Verificar si hay cambios sin guardar
            if (!isSaved)
            {
                DialogResult result = MessageBox.Show("¿Desea guardar los cambios?", "Guardar", MessageBoxButtons.YesNoCancel);
                if (result == DialogResult.Yes)
                {
                    guardarToolStripMenuItem_Click(this, EventArgs.Empty);
                }
                else if (result == DialogResult.No)
                {
                    richTextBox1.Clear();
                    richTextBox3.Clear();
                    currentFilePath = string.Empty;
                    splitContainer2.Panel1.Controls.Clear();
                    splitContainer2.Panel2.Controls.Clear();
                    isSaved = true;
                }
                else if (result == DialogResult.Cancel)
                {
                    toolStripStatusLabel1.Text = "Operación cancelada";
                    return;
                }
            }

            // Abrir archivo
            try
            {
                string code = System.IO.File.ReadAllText(path);
                richTextBox1.Clear();
                richTextBox1.AppendText(code);
                richTextBox1.SelectionStart = 0;
                richTextBox1.ScrollToCaret();
                currentFilePath = path;
                isSaved = true;
                toolStripStatusLabel1.Text = currentFilePath;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al abrir el archivo: " + ex.Message);
            }
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            // Nuevo archivo
            nuevoToolStripMenuItem_Click(sender, e);
        }

        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            // Abrir archivo
            abrirToolStripMenuItem_Click(sender, e);
        }

        private void toolStripButton3_Click(object sender, EventArgs e)
        {
            // Guardar archivo
            guardarToolStripMenuItem_Click(sender, e);
        }

        private void cargarToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Abrir un diálogo solicitando la dirección de inicio en hexadecimal
            string input = "";
            using (Form prompt = new Form())
            {
                prompt.Width = 350;
                prompt.Height = 150;
                prompt.Text = "Cargar programa";
                Label textLabel = new Label() { Left = 20, Top = 20, Text = "Ingrese la dirección de inicio en hexadecimal:", Width = 280 };
                TextBox inputBox = new TextBox() { Left = 20, Top = 50, Width = 280, Text = "0" };
                Button confirmation = new Button() { Text = "Aceptar", Left = 150, Width = 80, Top = 80, DialogResult = DialogResult.OK };
                prompt.Controls.Add(textLabel);
                prompt.Controls.Add(inputBox);
                prompt.Controls.Add(confirmation);
                prompt.AcceptButton = confirmation;

                if (prompt.ShowDialog() == DialogResult.OK)
                {
                    input = inputBox.Text;
                }
                else
                {
                    toolStripStatusLabel1.Text = "Operación cancelada";
                    return;
                }
            }

            int dirProg = 0;
            try
            {
                dirProg = Convert.ToInt32(input, 16);
            }
            catch
            {
                MessageBox.Show("Dirección inválida. Debe ser un número hexadecimal.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            comp.cargaLiga0();
            comp.cargaLigaP1(dirProg);
            comp.cargaLigaP2(dirProg);

            Table tmp = comp.memoryMap;
            Table tabse = comp.tabseTable;

            // Mostar ventana con dos tablas con dos pestañas
            // Crear un TabControl
            TabControl tabControl = new TabControl();
            tabControl.Dock = DockStyle.Fill;
            // Crear dos TabPages
            TabPage tabPage1 = new TabPage("Mapa de Memoria");
            TabPage tabPage2 = new TabPage("Tabla de Secciones");

            // Agregar las tablas a las pestañas
            tabPage1.Controls.Add(tmp.dataGridView);
            tabPage2.Controls.Add(tabse.dataGridView);
            // Agregar las pestañas al TabControl
            tabControl.TabPages.Add(tabPage1);
            tabControl.TabPages.Add(tabPage2);
            // Crear un Form para mostrar el TabControl
            Form form = new Form();
            form.Text = "Cargado y ligado";
            form.Size = new Size(800, 600);
            form.Controls.Add(tabControl);
            form.StartPosition = FormStartPosition.CenterScreen;
            form.ShowDialog();


        }

        private void simularToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Carga y ligado para documentos txt externos
            // Abrir un diálogo solicitando la dirección de inicio en hexadecimal
            // Abrir un diálogo solicitando la dirección de inicio en hexadecimal
            string input = "";
            using (Form prompt = new Form())
            {
                prompt.Width = 350;
                prompt.Height = 150;
                prompt.Text = "Cargar programa";
                Label textLabel = new Label() { Left = 20, Top = 20, Text = "Ingrese la dirección de inicio en hexadecimal:", Width = 280 };
                TextBox inputBox = new TextBox() { Left = 20, Top = 50, Width = 280, Text = "0" };
                Button confirmation = new Button() { Text = "Aceptar", Left = 150, Width = 80, Top = 80, DialogResult = DialogResult.OK };
                prompt.Controls.Add(textLabel);
                prompt.Controls.Add(inputBox);
                prompt.Controls.Add(confirmation);
                prompt.AcceptButton = confirmation;

                if (prompt.ShowDialog() == DialogResult.OK)
                {
                    input = inputBox.Text;
                }
                else
                {
                    toolStripStatusLabel1.Text = "Operación cancelada";
                    return;
                }
            }

            int dirProg = 0;
            try
            {
                dirProg = Convert.ToInt32(input, 16);
            }
            catch
            {
                MessageBox.Show("Dirección inválida. Debe ser un número hexadecimal.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }


            // Solicitar los documentos txt, hasta que el usuario diga que ya no quiere agregar mas
            List<string> files = new List<string>();
            // Solo permite de uno en uno
            while (true)
            {
                using (OpenFileDialog openFileDialog = new OpenFileDialog())
                {
                    openFileDialog.Filter = "Archivos de texto (*.txt)|*.txt|Todos los archivos (*.*)|*.*";
                    openFileDialog.Multiselect = false;
                    if (openFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        files.Add(openFileDialog.FileName);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            // Obtener las cadenas de texto de los archivos
            List<string> codes = new List<string>();
            foreach (string file in files)
            {
                string code = System.IO.File.ReadAllText(file);
                codes.Add(code);
            }

            // Cargar y ligar los archivos
            comp.cargaExt(codes);
            comp.cargaLigaP1(dirProg);
            comp.cargaLigaP2(dirProg);

            Table tmp = comp.memoryMap;
            Table tabse = comp.tabseTable;

            // Mostar ventana con dos tablas con dos pestañas
            // Crear un TabControl
            TabControl tabControl = new TabControl();
            tabControl.Dock = DockStyle.Fill;
            // Crear dos TabPages
            TabPage tabPage1 = new TabPage("Mapa de Memoria");
            TabPage tabPage2 = new TabPage("Tabla de Secciones");

            // Agregar las tablas a las pestañas
            tabPage1.Controls.Add(tmp.dataGridView);
            tabPage2.Controls.Add(tabse.dataGridView);
            // Agregar las pestañas al TabControl
            tabControl.TabPages.Add(tabPage1);
            tabControl.TabPages.Add(tabPage2);
            // Crear un Form para mostrar el TabControl
            Form form = new Form();
            form.Text = "Cargado y ligado";
            form.Size = new Size(800, 600);
            form.Controls.Add(tabControl);
            form.StartPosition = FormStartPosition.CenterScreen;
            form.ShowDialog();



        }
    }
}
