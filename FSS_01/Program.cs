using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
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

            string path = "E:/source/FSWS/FSS_01/tests/P02/code2.asm";
            CompiladorSx comp = new CompiladorSx(path);

            comp.compile();

            var temp = comp.tokens.GetTokens();

            // Table es un formulario, instanciarlo y mostrarlo
            vistas.Table table = new vistas.Table();
            // Añadir columnas al DataGridView NUM, FORMATO, CP, ETQ, INS, OPER, MODO
            string[] columns = { "NUM", "FORMATO", "CP", "ETQ", "INS", "OPER", "MODO" };
            foreach (var col in columns) table.dataGridView.Columns.Add(col, col);
            // Agregar una fila vacia para los errores
            string[] direcs = { "START", "END", "BASE", "BYTE", "WORD", "RESB", "RESW", "+", "RSUB" };
            int programCounter = 0;
            List<Tuple<string, int>> tabsym = new List<Tuple<string, int>>();
            Tuple<string, int> tup = null;
            for (int i = 0; i < temp.Count; i++)
            {
                List<IToken> tokens = new List<IToken>();
                string line = temp[i].Line.ToString();
                Console.WriteLine("Linea: " + line);
                while (i < temp.Count && line == temp[i].Line.ToString())
                {
                    Console.WriteLine(temp[i].Text);
                    tokens.Add(temp[i]);
                    i++;
                }
                i--;
                string formato = "-";
                string cp = programCounter.ToString("X");
                string etq = "";
                string ins = "";
                string opers = "";
                string modo = "";
                string errortop = "";
                bool errors = false;
                int j = 0;
                string tmptype = comp.getTokenType(tokens[j].Type);
                if (tmptype == "ID" || tmptype.Contains("CODOP") || direcs.Contains(tokens[j].Text))
                {
                    if (tmptype == "ID")
                    {
                        tup = new Tuple<string, int>(tokens[j].Text, programCounter);
                        // Revisar si el nombre de la etiqueta ya existe
                        foreach (var t in tabsym)
                        {
                            if (t.Item1 == tup.Item1)
                            {
                                //Console.WriteLine("Error: Etiqueta ya existe");
                                errors = true;
                                break;
                            }
                        }
                        etq = tokens[j].Text;
                        j++;
                    }
                    if (j < tokens.Count)
                    {
                        tmptype = comp.getTokenType(tokens[j].Type);
                        while (tmptype != null && (tmptype.Contains("CODOP") || direcs.Contains(tokens[j].Text)))
                        {
                            if (formato == "-")
                                // Revisar si no es RSUB
                                if (tokens[j].Text == "RSUB") formato = "3";
                                else 
                                switch (tmptype)
                                {
                                    case string s when s.Contains("1"):
                                        formato = "1";
                                        break;
                                    case string s when s.Contains("2"):
                                        formato = "2";
                                        break;
                                    case string s when s.Contains("3") || s == "RSUB":
                                        formato = "3";
                                        break;
                                    default:
                                        if (tokens[j].Text == "+") formato = "4";
                                        break;
                                }
                            ins += tokens[j].Text;
                            j++;
                            tmptype = comp.getTokenType(tokens[j].Type);
                        }
                    }
                    // Guardar el restante en opers
                    for (int k = j; k < tokens.Count; k++)
                    {
                        opers += tokens[k].Text + " ";
                    }
                }
                else
                {
                    // Imprime en consola lo que no se pudo reconocer
                    foreach(var token in tokens)
                    {
                        Console.WriteLine(token.Text);
                    }
                }
                modo = "-";
                if (formato == "3" || formato == "4")
                {
                    if (opers.Contains("#")) modo = "Inmediato";
                    else if (opers.Contains("@")) modo = "Indirecto";
                    else modo = "Simple";
                }
                Console.WriteLine("Linea: " + line);
                string errlex = comp.lexelistener.getErrorByLine(int.Parse(line));
                string errparse = comp.parslistener.getErrorByLine(int.Parse(line));
                Console.WriteLine("Error lexico: " + errlex);
                Console.WriteLine("Error sintactico: " + errparse);
                if (errlex != null)
                {
                    modo = "Error: Sintaxis";
                    errors = true;
                }
                else if (errparse != null)
                {
                    if (errparse.Contains("alternative")) modo = "Error: Instrucción no existe";
                    else modo = "Error: Sintaxis";
                    errors = true;
                }
                else
                {
                    // formato a int, si es - es 0
                    programCounter += (formato == "-") ? 0 : int.Parse(formato);
                    if (formato == "-")
                    {
                        // Revisar si la operación es una directiva
                        if (direcs.Contains(ins))
                        {
                            switch (ins)
                            {
                                case "START":
                                    break;
                                case "END":
                                    break;
                                case "BASE":
                                    break;
                                case "BYTE":
                                    // programCounter += 1;
                                    // Si contiene una "C'" o "c'" contar el numero de caracteres, cada uno necesita un byte (Ejemplo: C'HELLO' = 5 bytes)
                                    // Si contiene una "X'" o "x'" contar el numero de nibles, cada dos nibles necesitan un byte´(Ejemplo: X'F1' = 1 byte)
                                    if (opers.Contains("C'") || opers.Contains("c'"))
                                    {
                                        var val = opers.Replace("C'", "").Replace("c'", "").Replace("'", "").Replace(" ", "").Replace("\t", "").Replace("\n", "");
                                        Console.WriteLine(val + " " + val.Length);
                                        programCounter += val.Length;
                                    }
                                    else
                                    {
                                        var val = opers.Replace("X'", "").Replace("x'", "").Replace("'", "").Replace(" ", "").Replace("\t", "").Replace("\n", "");
                                        Console.WriteLine(val + " " + val.Length);
                                        programCounter += (val.Length % 2 == 0) ? val.Length / 2 : (val.Length / 2) + 1;
                                    }
                                    break;
                                case "WORD":
                                    // Una palabra son 3 bytes
                                    programCounter += 3;
                                    break;
                                case "RESB":
                                    //programCounter += int.Parse(opers);
                                    // Si hay una h o H, tratar como hexadecimal, si no como decimal
                                    if (opers.Contains("H") || opers.Contains("h"))
                                    {
                                        programCounter += int.Parse(opers.Replace("H", "").Replace("h", ""), System.Globalization.NumberStyles.HexNumber);
                                    }
                                    else
                                    {
                                        programCounter += int.Parse(opers);
                                    }
                                    break;
                                case "RESW":
                                    // programCounter += int.Parse(opers) * 3;
                                    // Si hay una h o H, tratar como hexadecimal, si no como decimal
                                    if (opers.Contains("H") || opers.Contains("h"))
                                    {
                                        programCounter += int.Parse(opers.Replace("H", "").Replace("h", ""), System.Globalization.NumberStyles.HexNumber) * 3;
                                    }
                                    else
                                    {
                                        programCounter += int.Parse(opers) * 3;
                                    }
                                    break;
                            }
                        }
                    }
                }
                if (errortop != "") modo = errortop;
                Console.WriteLine("===============================");

                // Cp formateado a 4
                String cpFormat = cp.PadLeft(4, '0');
                var row = table.dataGridView.Rows[table.dataGridView.Rows.Add(line, formato, cpFormat, etq, ins, opers, modo)];
                if (errors)
                {
                    // Color del texto en rojo
                    row.DefaultCellStyle.ForeColor = System.Drawing.Color.Red;
                }
                else
                {
                    if (tup != null && ins != "START" && ins != "END")
                    {
                        tabsym.Add(tup);
                    }
                }
                tup = null;
            }
            // Ajustar tabla a contenido
            table.dataGridView.AutoResizeColumns();
            Application.Run(table);

            table.dataGridView.AutoResizeColumns();

            // Mostrar otra tabla con la tabla de símbolos
            vistas.Table table2 = new vistas.Table();
            // Añadir columnas al DataGridView ETQ, VALOR
            string[] columns2 = { "ETQ", "VALOR" };
            foreach (var col in columns2) table2.dataGridView.Columns.Add(col, col);
            foreach (var tup2 in tabsym)
            {
                var row = table2.dataGridView.Rows[table2.dataGridView.Rows.Add(tup2.Item1, tup2.Item2.ToString("X"))];
            }
            // Ajustar tabla a contenido
            table2.dataGridView.AutoResizeColumns();
            Application.Run(table2);

        }
    }

    


}
