using Antlr4.Runtime;
using FSS_01.vistas;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FSS_01
{
    internal class CompiladorSx
    {
        public string asm_code;
        public AntlrInputStream input;
        public sicxeLexer lexer;
        public CommonTokenStream tokens;
        public sicxeParser parser;
        public ErrorParserListener parslistener;
        public ErrorLexerListener lexelistener;
        public sicxeParser.ProgContext tree;
        private DateTime date;
        private bool alreadyCompiled = false;
        public List<Tuple<string, int>> ruleList = new List<Tuple<string, int>>();
        public Table midFile;
        public Table symTable;

        public CompiladorSx(string path)
        {
            loadCode(path);
        }

        public void loadCode(string path)
        {
            string[] lines = System.IO.File.ReadAllLines(path);
            // juntar todas las lineas en un solo string sin eliminar los saltos de linea
            string cad = string.Join("\n", lines);
            setCode(cad);
        }

        public void setCode(string code)
        {
            asm_code = code;
            // Crear un objeto de la clase AntlrInputStream
            input = new AntlrInputStream(code);
            // Crear un objeto de la clase AsmLexer
            lexer = new sicxeLexer(input);

            var lexr = this.lexer;
            // Crear lista de reglas
            ruleList.Clear();
            foreach (var rule in lexr.RuleNames)
            {
                ruleList.Add(new Tuple<string, int>(rule, (int)lexr.GetType().GetField(rule).GetValue(lexr)));
            }

            // Crear un objeto de la clase CommonTokenStream
            tokens = new CommonTokenStream(lexer);
            // Crear un objeto de la clase AsmParser
            parser = new sicxeParser(tokens);
            // Agregar los oyentes de errores
            parslistener = new ErrorParserListener();
            lexelistener = new ErrorLexerListener();
            lexer.RemoveErrorListeners();
            lexer.AddErrorListener(lexelistener);
            parser.RemoveErrorListeners();
            parser.AddErrorListener(parslistener);
            alreadyCompiled = false;
        }

        public void compile()
        {
            tree = parser.prog();
            midFile = new Table("Archivo intermedio");
            symTable = new Table("Tabla de símbolos");
            postCompileAnalizer();
            alreadyCompiled = true;
            date = DateTime.Now;
        }

        private void postCompileAnalizer()
        {
            var temp = this.tokens.GetTokens();
            // Añadir columnas al DataGridView NUM, FORMATO, CP, ETQ, INS, OPER, MODO
            string[] columns = { "NUM", "FORMATO", "CP", "ETQ", "INS", "OPER", "MODO" };
            foreach (var col in columns) midFile.dataGridView.Columns.Add(col, col);
            // Agregar una fila vacia para los errores
            string[] direcs = { "START", "END", "BASE", "BYTE", "WORD", "RESB", "RESW", "+", "RSUB" };
            int programCounter = 0;
            List<Tuple<string, int>> tabsym = new List<Tuple<string, int>>();
            Tuple<string, int> tup = null;
            for (int i = 0; i < temp.Count; i++)
            {
                List<IToken> tokens = new List<IToken>();
                string line = temp[i].Line.ToString();
                //Console.WriteLine("Linea: " + line);
                while (i < temp.Count && line == temp[i].Line.ToString())
                {
                    //Console.WriteLine(temp[i].Text);
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
                string tmptype = this.getTokenType(tokens[j].Type);
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
                                errortop = "Error: Símbolo duplicado";
                                errors = true;
                                break;
                            }
                        }
                        etq = tokens[j].Text;
                        j++;
                    }
                    if (j < tokens.Count)
                    {
                        tmptype = this.getTokenType(tokens[j].Type);
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
                            tmptype = this.getTokenType(tokens[j].Type);
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
                    foreach (var token in tokens)
                    {
                        //Console.WriteLine(token.Text);
                    }
                }
                modo = "-";
                if (formato == "3" || formato == "4")
                {
                    if (opers.Contains("#")) modo = "Inmediato";
                    else if (opers.Contains("@")) modo = "Indirecto";
                    else modo = "Simple";
                }
                //.WriteLine("Linea: " + line);
                string errlex = this.lexelistener.getErrorByLine(int.Parse(line));
                string errparse = this.parslistener.getErrorByLine(int.Parse(line));
                //Console.WriteLine("Error lexico: " + errlex);
                //Console.WriteLine("Error sintactico: " + errparse);
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
                //Console.WriteLine("===============================");
                if (errortop != "") modo = errortop;

                // Cp formateado a 4
                String cpFormat = cp.PadLeft(4, '0');
                var row = midFile.dataGridView.Rows[midFile.dataGridView.Rows.Add(line, formato, cpFormat, etq, ins, opers, modo)];
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
                if (errortop != "") modo = errortop;
                tup = null;
            }
            // Añadir columnas al DataGridView ETQ, VALOR
            string[] columns2 = { "ETQ", "VALOR" };
            foreach (var col in columns2) symTable.dataGridView.Columns.Add(col, col);
            foreach (var tup2 in tabsym)
            {
                // Formatear el valor a 4
                String valFormat = tup2.Item2.ToString("X").PadLeft(4, '0');
                var row = symTable.dataGridView.Rows[symTable.dataGridView.Rows.Add(tup2.Item1, valFormat)];
            }
        }

        public string results()
        {
            if (!alreadyCompiled) return "El código no ha sido compilado";
            string output = "";
            // Obtener fecha y hora actual
            output += "Fecha y hora de compilación: " + date.ToString("yyyy-MM-dd HH:mm:ss") + "\n\n";
            if (parslistener.getErroresCount() > 0)
            {
                output += "Errores sintácticos en el código: " + parslistener.getErroresCount() + "\n";
                output += parslistener.getErrores() + "\n";
            }
            else
            {
                output += "No se encontraron errores sintácticos en el código\n\n";
            }
            if (lexelistener.getErroresCount() > 0)
            {
                output += "Errores léxicos en el código: " + lexelistener.getErroresCount() + "\n";
                output += lexelistener.getErrores() + "\n";
            }
            else
            {
                output += "No se encontraron errores léxicos en el código\n\n";
            }

            output += "Árbol de análisis sintáctico:\n";
            output += tree.ToStringTree(parser) + "\n";
            return output;
        }

        private string getTokenType(int type)
        {
            foreach (var rule in ruleList)
            {
                if (rule.Item2 == type)
                {
                    return rule.Item1;
                }
            }
            return null;
        }

    }

    // Implementación de un oyente de errores del parser
    internal class ErrorParserListener : IAntlrErrorListener<IToken>
    {
        private String errores = "";
        public List<Tuple<int, string>> errorsLines = new List<Tuple<int, string>>();
        private int c = 0;

        public String getErrores()
        {
            return errores;
        }

        public int getErroresCount()
        {
            return c;
        }

        public void SyntaxError(IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            // Manejar el error según tus necesidades
            //Console.WriteLine($"Línea {line}:{charPositionInLine} - {msg}");
            errores += $"Line {line}:{charPositionInLine} - {msg}\n";
            errorsLines.Add(new Tuple<int, string>(line, msg));
            c++;
        }

        public string getErrorByLine(int line)
        {
            foreach (var error in errorsLines)
            {
                if (error.Item1 == line)
                {
                    return error.Item2;
                }
            }
            return null;
        }
    }

    // Implementación de un oyente de errores del lexer
    internal class ErrorLexerListener : IAntlrErrorListener<int>
    {
        private int c = 0;
        private String errores = "";
        public List<Tuple<int, string>> errorsLines = new List<Tuple<int, string>>();

        public String getErrores()
        {
            return errores;
        }

        public int getErroresCount()
        {
            return c;
        }

        public void SyntaxError(IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            // Manejar el error según tus necesidades
            //Console.WriteLine($"Línea {line}:{charPositionInLine} - {msg}");
            errores += $"Line {line}:{charPositionInLine} - {msg}\n";
            errorsLines.Add(new Tuple<int, string>(line, msg));
            c++;
        }

        public string getErrorByLine(int line)
        {
            foreach (var error in errorsLines)
            {
                if (error.Item1 == line)
                {
                    return error.Item2;
                }
            }
            return null;
        }
    }
}
