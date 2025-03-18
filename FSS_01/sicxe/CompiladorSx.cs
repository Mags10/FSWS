using Antlr4.Runtime;
using FSS_01.vistas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FSS_01
{
    internal class CompiladorSx
    {
        // Código fuente en ensamblador
        private string asm_code;
        private AntlrInputStream input;
        // Lexer para el código fuente
        private sicxeLexer lexer;
        // Código fuente en ensamblador tokenizado
        private CommonTokenStream tokens;
        // Parser para el código fuente
        private sicxeParser parser;
        // Listeners para errores
        private ErrorParserListener parslistener;
        private ErrorLexerListener lexelistener;
        // Árbol de análisis sintáctico
        private sicxeParser.ProgContext tree;
        // Variables de estado
        private DateTime date;
        private bool alreadyCompiled = false;
        // Lista de reglas por ANTLR
        private List<Tuple<string, int>> ruleList = new List<Tuple<string, int>>();
        // Tablas de datos
        public Table midFile;
        public Table symTable;
        // Datos en lista de listas del tipo ["NUM", "FORMATO", "CP", "ETQ", "INS", "OPER", "CODOBJ", "ERROR", "MODO"]
        //private List<List<string>> midData = new List<List<string>>();
        private List<Line> midData = new List<Line>();
        private List<Tuple<string,string>> symData = new List<Tuple<string,string>>();
        // Relación de instrucciones y su código de operación
        private List<Tuple<string,int>> opers = new List<Tuple<string, int>>();
        // Relación de registros y su código de operación
        private List<Tuple<string, int>> regs = new List<Tuple<string, int>>();
        // Codigo objeto
        private string objCode = "";
        // Path de archivo
        private string path;
        // Cuadro de texto para mostrar el objcode
        public RichTextBox objTextBox = new RichTextBox();

        public CompiladorSx(string path)
        {
            loadCode(path);
            // Cargar las instrucciones y sus códigos de operación
            var json = System.IO.File.ReadAllText("../../sicxe/sicxe.json");
            var jsonObject = JsonConvert.DeserializeObject<JObject>(json);
            var instrucciones = jsonObject["instrucciones"].ToObject<Dictionary<string, string>>();
            var registros = jsonObject["registros"].ToObject<Dictionary<string, string>>();
            foreach (var instr in instrucciones)
                opers.Add(new Tuple<string, int>(instr.Key, int.Parse(instr.Value, System.Globalization.NumberStyles.HexNumber)));
            foreach (var reg in registros)
                regs.Add(new Tuple<string, int>(reg.Key, int.Parse(reg.Value, System.Globalization.NumberStyles.HexNumber)));
        }

        public void loadCode(string path)
        {
            this.path = path;
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
            pstCmpAnlz = false;
        }

        public void compile()
        {
            tree = parser.prog();
            //midFile = new Table("Archivo intermedio");
            //symTable = new Table("Tabla de símbolos");
            postCompileAnalizer();
            generateObjectCode();
            // Exportar obj a mismo directorio del archivo
            string objPath = path.Replace(".asm", ".obj");
            System.IO.File.WriteAllText(objPath, objCode);
            objTextBox.Text = objCode;

            // Configurar objeTextBox para que use todo el espacio del control
            objTextBox.Dock = DockStyle.Fill;

            createTables();
            alreadyCompiled = true;
            date = DateTime.Now;

            // Imprimir todos los errores
            Console.WriteLine("Errores léxicos: " + lexelistener.getErroresCount());
            Console.WriteLine(lexelistener.getErrores());
            Console.WriteLine("Errores sintácticos: " + parslistener.getErroresCount());
            Console.WriteLine(parslistener.getErrores());

            // Imprimir árbol de análisis sintáctico
            Console.WriteLine(tree.ToStringTree(parser));
        }

        private bool pstCmpAnlz = false;
        private void postCompileAnalizer()
        {
            var temp = this.tokens.GetTokens();
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
                string error = "";
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
                    for (int k = j; k < tokens.Count; k++) opers += tokens[k].Text + " ";
                }
                modo = "-";
                if (formato == "3" || formato == "4")
                {
                    Console.WriteLine("Opers: " + opers);
                    if (opers.Contains("#")) modo = "Inmediato";
                    else if (opers.Contains("@")) modo = "Indirecto";
                    else modo = "Simple";
                    Console.WriteLine("Modo: " + modo);
                }
                //.WriteLine("Linea: " + line);
                string errlex = this.lexelistener.getErrorByLine(int.Parse(line));
                string errparse = this.parslistener.getErrorByLine(int.Parse(line));
                //Console.WriteLine("Error lexico: " + errlex);
                //Console.WriteLine("Error sintactico: " + errparse);
                if (errlex != null)
                {
                    error = "Error: Sintaxis";
                    errors = true;
                }
                else if (errparse != null)
                {
                    if (errparse.Contains("alternative")) error = "Error: Instrucción no existe";
                    else error = "Error: Sintaxis";
                    errors = true;
                }
                else
                {
                    // formato a int, si es - es 0
                    programCounter += (formato == "-") ? 0 : int.Parse(formato);

                    // Revisar si la operación es una directiva
                    if (formato == "-" && direcs.Contains(ins))
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
                                    programCounter += int.Parse(opers.Replace("H", "").Replace("h", ""), System.Globalization.NumberStyles.HexNumber);
                                else
                                    programCounter += int.Parse(opers);
                                break;
                            case "RESW":
                                // programCounter += int.Parse(opers) * 3;
                                // Si hay una h o H, tratar como hexadecimal, si no como decimal
                                if (opers.Contains("H") || opers.Contains("h"))
                                    programCounter += int.Parse(opers.Replace("H", "").Replace("h", ""), System.Globalization.NumberStyles.HexNumber) * 3;
                                else                                
                                    programCounter += int.Parse(opers) * 3;
                                break;
                        }
                    }
                }
                //Console.WriteLine("===============================");
                if (errortop != "") error = errortop;
                // Cp formateado a 4
                String cpFormat = cp.PadLeft(4, '0');
                if (!errors) {
                    if (tup != null && ins != "START" && ins != "END") tabsym.Add(tup);
                }
                //else modo = "";
                //List<string> row = new List<string> { line, formato, cpFormat, etq, ins, opers, "", error, modo };
                Line tmpline = new Line(line, formato, cpFormat, etq, ins, opers, "", error, modo);
                Console.WriteLine("Modo registrado: " + tmpline.modo);
                Console.WriteLine("=====================================");
                midData.Add(tmpline);
                tup = null;
            }

            foreach (var t in tabsym)
            {
                String cpFormat = t.Item2.ToString("X").PadLeft(4, '0');
                symData.Add(new Tuple<string, string>(t.Item1, cpFormat));
            }

            pstCmpAnlz = true;

            
        }

        private void generateObjectCode()
        {
            string cad = "H";
            // Etiqueta de start
            string start = "";
            // buscar la etiqueta de start
            foreach (Line line in midData)
            {
                if (line.instruccion == "START")
                {
                    start = line.etiqueta;
                    break;
                }
            }
            Console.WriteLine("Generando código objeto para: " + start);
            // concatenar la etiqueta de start a 6 caracteres desde la izquierda, si no, rellenar con espacios
            cad += start.PadRight(6, ' ');

            // Poner la dirección de inicio en 6 caracteres desde la izquierda, si no, rellenar con ceros
            cad += midData[0].cp.PadLeft(6, '0');

            // Poner la dirección final en 6 caracteres desde la izquierda, si no, rellenar con ceros
            cad += midData[midData.Count - 1].cp.PadLeft(6, '0');

            Console.WriteLine(cad);

            string tmpcad = "";
            int count = 0;

            if (!pstCmpAnlz) return;

            int baseAddress = -1;

            foreach (Line line in midData)
            {
                // Buscar en la lista de operaciones la instrucción
                var oper = opers.Find(x => x.Item1 == line.instruccion.Replace("+", ""));
                if (oper != null)
                {
                    String codop = oper.Item2.ToString("X");
                    string error = "";
                    // Si es formato 1 queda igual
                    if (line.formato == "1")
                    {
                        line.codobj = codop;
                    }
                    // Si fue formato dos
                    else if (line.formato == "2")
                    {
                        string reg1 = "0";
                        string reg2 = "0";
                        // Limpiar operadores en caso de que tengan saltos de linea o otras cosas
                        line.operadores = line.operadores.Replace("\n", "").Replace("\t", "").Replace(" ", "");
                        if (!line.operadores.Contains(","))
                        {
                            var reg = regs.Find(x => x.Item1 == line.operadores);
                            if (reg != null) reg1 = reg.Item2.ToString("X");
                        }
                        else
                        {
                            var tmpregs = line.operadores.Split(',');
                            var reg = regs.Find(x => x.Item1 == tmpregs[0]);
                            if (reg != null) reg1 = reg.Item2.ToString("X");
                            reg = regs.Find(x => x.Item1 == tmpregs[1]);
                            if (reg != null) reg2 = reg.Item2.ToString("X");
                        }
                        line.codobj = codop + reg1 + reg2;
                    }
                    else if (line.formato == "3" || line.formato == "4")
                    {
                        //Console.WriteLine("Instrucción: " + line.instruccion);
                        // Pasar codop a binario
                        string bin = Convert.ToString(oper.Item2, 2);
                        //Console.WriteLine(bin);
                        // Poner a 8 bits
                        bin = bin.PadLeft(8, '0');
                        //Console.WriteLine(bin);
                        // Quitar ultimos 2 bits (LSB)
                        bin = bin.Substring(0, bin.Length - 2);
                        //Console.WriteLine(bin);
                        if (line.instruccion == "RSUB")
                        {
                            line.codobj = "4C0000";
                        }
                        else
                        {
                            String n = "0";
                            String i = "0";
                            String x = "0";
                            String b = "0";
                            String p = "0";
                            String e = "0";
                            //Console.WriteLine("Modo: " + line.modo);
                            // Si es inmediato
                            if (line.modo == "Inmediato")
                            {
                                //Console.WriteLine("Inmediato");
                                i = "1";
                                n = "0";
                            }
                            // Si es indirecto
                            else if (line.modo == "Indirecto")
                            {
                               // Console.WriteLine("Indirecto");
                                i = "0";
                                n = "1";
                            }
                            // Si es simple
                            else
                            {
                                //Console.WriteLine("Simple");
                                i = "1";
                                n = "1";
                            }
                            // Si es extendido
                            if (line.formato == "4")
                            {
                                //Console.WriteLine("Extendido");
                                e = "1";
                                // Si es RSUB
                                if (line.instruccion == "RSUB")
                                {
                                    line.codobj = "4C000000";
                                    continue;
                                }
                            }
                            // Si es indexado
                            if (line.operadores.Contains("X"))
                            {
                                //Console.WriteLine("Indexado");
                                x = "1";
                            }

                            int dir = 0;
                            string desp = "";

                            // Obtener operando
                            string operando = line.operadores;
                            if (operando.Contains(","))
                            {
                                operando = operando.Split(',')[0];
                            }
                            // Limpiar operando
                            operando = operando.Replace("\n", "").Replace("\t", "").Replace(" ", "");
                            operando = operando.Replace("#", "").Replace("@", "");

                            bool isetiqueta = false;
                            // Si contiene una h o H, remplazar por nada
                            if (operando.Contains("H") || operando.Contains("h"))
                            {
                                // revisar si quitando la h, contiene letras
                                if (operando.Replace("H", "").Replace("h", "").Any(char.IsLetter)) isetiqueta = true;
                                else 
                                {
                                    operando = operando.Replace("H", "").Replace("h", "");
                                    // convertir a int
                                    dir = int.Parse(operando, System.Globalization.NumberStyles.HexNumber);
                                }
                            }
                            else
                            {
                                // Si contiene letras, es una etiqueta
                                if (operando.Any(char.IsLetter)) isetiqueta = true;
                                else
                                {// Si no, convertir a int
                                    dir = int.Parse(operando);
                                }   
                            }
                            if (isetiqueta)
                            {
                                // Buscar en la tabla de símbolos
                                var sym = symData.Find(item => item.Item1 == operando);
                                if (sym != null)
                                {
                                    dir = int.Parse(sym.Item2, System.Globalization.NumberStyles.HexNumber);
                                }
                                else
                                {
                                    //Console.WriteLine("Error: Etiqueta no encontrada");
                                    error = "Error: Etiqueta no encontrada";
                                }
                            }

                            if (line.formato == "3")
                            {
                              //  Console.WriteLine("Operandos: " + operando);
                              //  Console.WriteLine("Direccion: " + dir);
                                // Con la dirección, calcular si es relativa a PC o Base
                                // Se revisa con el CP de la siguiente instrucción
                                // Obtener indice actual
                                int index = midData.FindIndex(item => item.cp == line.cp);
                                //cp de la siguiente instrucción
                                int nextcp = int.Parse(midData[index + 1].cp, System.Globalization.NumberStyles.HexNumber);
                                // Calcular la dirección relativa
                                int rel = dir - nextcp;
                                //Console.WriteLine("Relativa: " + rel);

                                // Ahora calcular respecto a la base
                                int baseDir = dir - baseAddress;
                               // Console.WriteLine("Base: " + baseDir);

                                // Si no es etiqueta y es inmediato
                               // Console.WriteLine("Etiqueta: " + isetiqueta);
                               // Console.WriteLine("modo: " + line.modo);
                                if (!isetiqueta)
                                {
                                  //  Console.WriteLine("Inmediato");
                                    p = "0";
                                    b = "0";
                                    // Pasar a binario
                                    desp = Convert.ToString(dir, 2);
                                }
                                // Esta dentro del rango -2048 a 2047
                                else if (-2048 <= rel && rel <= 2047)
                                {
                                  //  Console.WriteLine("Relativo a PC");
                                    p = "1";
                                    b = "0";
                                    // Pasar a binario
                                    desp = Convert.ToString(rel, 2);
                                }
                                // Esta dentro del rango de la base
                                else if (0 <= baseDir && baseDir <= 4095)
                                {
                                   // Console.WriteLine("Relativo a Base");
                                    p = "0";
                                    b = "1";
                                    // Pasar a binario
                                    desp = Convert.ToString(baseDir, 2);
                                }
                                // Si no, error
                                else
                                {
                                    //Console.WriteLine("Error: No relativo al CP/B");
                                    error = "Error: No relativo al CP/B";
                                }
                            }
                            else
                            {
                                //Console.WriteLine("Operandos (f4): " + dir);
                                // Pasar a binario
                                desp = Convert.ToString(dir, 2);
                                // Revisar si es simple e indexada, si lo es, es error
                                if (x == "1" && n == "1")
                                {
                                    //Console.WriteLine("Error: No existe combinación de MD");
                                    error = "Error: No existe combinación de MD";
                                }
                            }

                            // Dependiendo del formato, tomar los ultimos 12 bits o 20 bits
                            desp = desp.PadLeft(20, '0');
                            if (line.formato == "3")
                            {
                                // Tomar los ultimos 12 bits (LSB)
                                desp = desp.Substring(desp.Length - 12);
                            }
                            else if (line.formato == "4")
                            {
                                // Tomar los ultimos 20 bits (LSB)
                                desp = desp.Substring(desp.Length - 20);
                            }

                            if (error != "")
                            {
                                line.error = error;
                                b = "1";
                                p = "1";
                                // desp remplazar todos los bits por 1
                                for (int k = 0; k < desp.Length; k++)
                                {
                                    desp = desp.Remove(k, 1).Insert(k, "1");
                                }
                            }

                            line.codobj = bin + n + i + x + b + p + e + desp;
                           // Console.WriteLine(line.codobj);
                            // Pasar a hex de 4 en 4 para no perder ceros
                            string hex = "";
                            for (int ij = 0; ij < line.codobj.Length; ij += 4)
                            {
                                hex += Convert.ToInt32(line.codobj.Substring(ij, 4), 2).ToString("X");
                            }
                            line.codobj = hex;

                            //Console.WriteLine(line.codobj);
                            //Console.WriteLine("=====================================");

                        }
                    }
                }
                else
                {
                    if (line.instruccion.Contains("BASE"))
                    {
                        //Console.WriteLine("BASE");
                        // Limpiar operadores en caso de que tengan saltos de linea o otras cosas
                        line.operadores = line.operadores.Replace("\n", "").Replace("\t", "").Replace(" ", "");
                        // Buscar en la tabla de símbolos
                        var sym = symData.Find(item => item.Item1 == line.operadores);
                        if (sym != null)
                        {
                            baseAddress = int.Parse(sym.Item2, System.Globalization.NumberStyles.HexNumber);
                        }
                        //Console.WriteLine("Base: " + baseAddress);
                        //Console.WriteLine("=====================================");
                    }
                    // Revisar si fue BYTE
                    else if (line.instruccion.Contains("BYTE"))
                    {
                        //Console.WriteLine("BYTE");
                        // Limpiar operadores en caso de que tengan saltos de linea o otras cosas
                        line.operadores = line.operadores.Replace("\n", "").Replace("\t", "").Replace(" ", "");
                        // Si contiene una "C'" o "c'" contar el numero de caracteres, cada uno necesita un byte (Ejemplo: C'HELLO' = 5 bytes)
                        // Si contiene una "X'" o "x'" contar el numero de nibles, cada dos nibles necesitan un byte´(Ejemplo: X'F1' = 1 byte)
                        if (line.operadores.Contains("C'") || line.operadores.Contains("c'"))
                        {
                            var val = line.operadores.Replace("C'", "").Replace("c'", "").Replace("'", "").Replace(" ", "").Replace("\t", "").Replace("\n", "");
                            //Console.WriteLine(val + " " + val.Length);
                            // Si es un caracter, pasar a ascii
                            line.codobj = "";
                            foreach (char c in val)
                            {
                                line.codobj += ((int)c).ToString("X");
                            }
                        }
                        else
                        {
                            var val = line.operadores.Replace("X'", "").Replace("x'", "").Replace("'", "").Replace(" ", "").Replace("\t", "").Replace("\n", "");
                           // Console.WriteLine(val + " " + val.Length);
                            line.codobj = val;
                            // Si es impar, agregar un 0 al inicio
                            if (val.Length % 2 != 0) line.codobj = "0" + line.codobj;
                        }
                        //Console.WriteLine("=====================================");
                    }
                    // Revisar si fue WORD
                    else if (line.instruccion.Contains("WORD"))
                    {
                        //Console.WriteLine("WORD");
                        // Limpiar operadores en caso de que tengan saltos de linea o otras cosas
                        line.operadores = line.operadores.Replace("\n", "").Replace("\t", "").Replace(" ", "");
                        // Revisar si es hexadecimal o decimal
                        if (line.operadores.Contains("H") || line.operadores.Contains("h"))
                        {
                            line.codobj = line.operadores.Replace("H", "").Replace("h", "");
                        }
                        else
                        {
                            line.codobj = int.Parse(line.operadores).ToString("X");
                        }
                        // Pasar a 6 caracteres
                        line.codobj = line.codobj.PadLeft(6, '0');
                    }
                }

                // Revisar si hay un corte de registro de texto
                // Cuando hay un ORG, END, BASE, BYTE, WORD, RESB, RESW, RSUB
                if (line.instruccion == "ORG" || line.instruccion == "END" || line.instruccion == "RESB" || line.instruccion == "RESW")
                {
                    // Si tmpcad no esta vacio, agregar a cad
                    if (tmpcad != "")
                    {
                        cad += count.ToString("X").PadLeft(2, '0') + tmpcad;
                        // Limpiar tmpcad
                        tmpcad = "";
                        // Agregar a tmpcad
                        tmpcad += line.codobj;
                        count = 0;
                        Console.WriteLine("Cuenta rst 3: " + count);
                    }
                }

                // Ver si genero codigo objeto sin errores
                if (line.codobj != "")
                {
                    //Console.WriteLine(line.codobj);

                    // Si tmpcad esta vacio, poner la dirección de inici
                    if (tmpcad == "")
                    {
                        cad += "\nT";
                        // a 6 caracteres desde la izquierda, si no, rellenar con ceros
                        cad += line.cp.PadLeft(6, '0');
                        count = 0;
                        Console.WriteLine("Cuenta rst 1: " + count);
                    }
                    if (tmpcad.Length + line.codobj.Length <= 60)
                    {
                        tmpcad += line.codobj;
                        // Sumar a count la longitud de codobj entre 2
                        count += line.codobj.Length / 2;
                        //count++;
                        Console.WriteLine("Cuenta: " + count);
                    }
                    else
                    {
                        // Si tmpcad no esta vacio, agregar a cad
                        cad += count.ToString("X").PadLeft(2, '0') + tmpcad;
                        cad += "\nT";
                        // a 6 caracteres desde la izquierda, si no, rellenar con ceros
                        cad += line.cp.PadLeft(6, '0');

                        // Limpiar tmpcad
                        tmpcad = "";
                        // Agregar a tmpcad
                        tmpcad += line.codobj;
                        count = 0;
                        Console.WriteLine("Cuenta rst 2: " + count);
                    }

                }


                // Si fue formatp 4 y no hubo errores, marcar como realocalizable con un * en codop
                if (line.formato == "4" && line.error == "")
                {
                    line.codobj += "*";
                }
                Console.WriteLine("Instrucción: " + line.codobj);
            }

            // Recorrer lineas, para ver aquellas con *y agregarlas a cad como registro M
            foreach (Line line in midData)
            {
                if (line.codobj.Contains("*"))
                {
                    // Poner M
                    cad += "\nM";
                    // Poner la dirección de inicio + 1
                    cad += (int.Parse(line.cp, System.Globalization.NumberStyles.HexNumber) + 1).ToString("X").PadLeft(6, '0');
                    // Poner 05
                    cad += "05+" + start;
                }
            }

            // Registro E, Si tiene etiqueta, poner su valor segun tabla de simbolos
            // Si no, buscar la primera instrucción (que no sea directiva) y poner su dirección
            cad += "\nE";
            Line last = midData.Last();
            if (last.etiqueta != "")
            {
                var sym = symData.Find(item => item.Item1 == last.etiqueta);
                if (sym != null)
                {
                    cad += sym.Item2.PadLeft(6, '0');
                }
            }
            else
            {
                foreach (Line line in midData)
                {
                    if (line.etiqueta == "")
                    {
                        // Buscar la primera instrucción que no sea directiva
                        if (!line.instruccion.Contains("RES") && !line.instruccion.Contains("BYTE") && !line.instruccion.Contains("WORD"))
                        {
                            cad += line.cp.PadLeft(6, '0');
                            break;
                        }
                    }
                }
            }
            //Console.WriteLine(cad);
            this.objCode = cad;

            return;
        }



        public void createTables()
        {
            midFile = new Table("Archivo intermedio");
            symTable = new Table("Tabla de símbolos");

            // Agregar columnas al DataGridView
            string[] columns = { "NUM", "FORMATO", "CP", "ETQ", "INS", "OPER", "CODOBJ", "ERROR", "MODO" };
            foreach (var col in columns) midFile.dataGridView.Columns.Add(col, col);
            // Agregar filas al DataGridView
            foreach (Line row in midData)
            {
                midFile.dataGridView.Rows.Add(row.ToArray());
            }

            // Agregar columnas al DataGridView
            string[] columnsSym = { "ETQ", "CP" };
            foreach (var col in columnsSym) symTable.dataGridView.Columns.Add(col, col);
            // Agregar filas al DataGridView
            foreach (var row in symData) symTable.dataGridView.Rows.Add(row.Item1, row.Item2);

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

    public class Line
    {
        public string linea { get; set; }
        public string formato { get; set; }
        public string cp { get; set; }
        public string etiqueta { get; set; }
        public string instruccion { get; set; }
        public string operadores { get; set; }
        public string codobj { get; set; }
        public string error { get; set; }
        public string modo { get; set; }

        public Line(string linea, string formato, string cp, string etiqueta, string instruccion, string operadores, string codobj, string error, string modo)
        {
            this.linea = linea;
            this.formato = formato;
            this.cp = cp;
            this.etiqueta = etiqueta;
            this.instruccion = instruccion;
            this.operadores = operadores;
            this.codobj = codobj;
            this.error = error;
            this.modo = modo;
        }

        public string[] ToArray()
        {
            List<string> tmp = new List<string> { linea, formato, cp, etiqueta, instruccion, operadores, codobj, error, modo };
            return tmp.ToArray();
        }
    }

    
}
