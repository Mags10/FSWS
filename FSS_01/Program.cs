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

            string path = "E:/source/FSWS/FSS_01/tests/P02/code1.asm";
            CompiladorSx comp = new CompiladorSx(path);

            comp.compile();

            var temp = comp.tokens.GetTokens();

            // Table es un formulario, instanciarlo y mostrarlo
            vistas.Table table = new vistas.Table();
            // Añadir columnas al DataGridView NUM, FORMATO, CP, ETQ, INS, OPER, MODO
            string[] columns = { "NUM", "FORMATO", "CP", "ETQ", "INS", "OPER", "MODO" };
            foreach (var col in columns) table.dataGridView.Columns.Add(col, col);
            // Agregar una fila vacia para los errores
            string[] direcs = { "START", "END", "BASE", "BYTE", "WORD", "RESB", "RESW", "+" };
            int programCounter = 0;
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
                int j = 0;
                string tmptype = comp.getTokenType(tokens[j].Type);
                if (tmptype == "ID" || tmptype.Contains("CODOP") || direcs.Contains(tokens[j].Text))
                {
                    if (tmptype == "ID")
                    {
                        etq = tokens[j].Text;
                        j++;
                    }
                    if (j < tokens.Count)
                    {
                        tmptype = comp.getTokenType(tokens[j].Type);
                        while (tmptype != null && (tmptype.Contains("CODOP") || direcs.Contains(tokens[j].Text)))
                        {
                            if (formato == "-")
                                switch (tmptype)
                                {
                                    case string s when s.Contains("1"):
                                        formato = "1";
                                        break;
                                    case string s when s.Contains("2"):
                                        formato = "2";
                                        break;
                                    case string s when s.Contains("3"):
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
                }
                else if(errparse != null)
                {
                    if (errparse.Contains("alternative")) modo = "Error: Instrucción no existe";
                    else modo = "Error: Sintaxis";
                }
                else
                {
                    // formato a int, si es - es 0
                    programCounter += (formato == "-") ? 0 : int.Parse(formato);
                }
                Console.WriteLine("===============================");
                table.dataGridView.Rows.Add(line, formato, cp, etq, ins, opers, modo);
            }

            Application.Run(table);
        }
    }

    


}
