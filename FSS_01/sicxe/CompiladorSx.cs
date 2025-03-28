using Antlr4.Runtime;
using FSS_01.vistas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Antlr4.Runtime.Tree;

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

        // Relación de instrucciones y su código de operación
        private List<Tuple<string,int>> opers = new List<Tuple<string, int>>();
        // Relación de registros y su código de operación
        private List<Tuple<string, int>> regs = new List<Tuple<string, int>>();
        // Lista de directivas
        private List<string> directivas = new List<string>();
        // Path de archivo
        private string path;

        // Tabla de símbolos
        private List<Simbolo> simbolos = new List<Simbolo>();

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
            // Cargar las directivas
            directivas = jsonObject["directivas"].ToObject<List<string>>();
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
                ruleList.Add(new Tuple<string, int>(rule, (int)lexr.GetType().GetField(rule).GetValue(lexr)));
            // Crear un objeto de la clase CommonTokenStream
            tokens = new CommonTokenStream(lexer);
            // Crear un objeto de la clase AsmParser
            parser = new sicxeParser(tokens);
            //parser.Trace = true;
            //parser.TrimParseTree = true;

            // Agregar los oyentes de errores
            parslistener = new ErrorParserListener();
            lexelistener = new ErrorLexerListener();
            lexer.RemoveErrorListeners();
            lexer.AddErrorListener(lexelistener);
            parser.RemoveErrorListeners();
            parser.AddErrorListener(parslistener);
        }

        public void compile()
        {
            tree = parser.prog();

            int progCP = 0;
            int numLine = 1;
            // Crear lineas de código
            List<Linea> lineas = new List<Linea>();
            Linea tmpLine = new Linea();

            //tree.inicio().etiqueta();
            tmpLine.etq = tree.inicio().etiqueta();
            tmpLine.ins = tree.inicio().START();
            tmpLine.opers = new List<ITerminalNode> { tree.inicio().NUM() };
            tmpLine.line = numLine++;
            checkError();
            lineas.Add(tmpLine);

            var tmp = tree.proposiciones();
            var tmp2 = tmp.proposicion();
            Console.WriteLine("Preposiciones: " + tmp2.Length);
            foreach (var prop in tmp2)
            {
                tmpLine = new Linea();
                tmpLine.cp = progCP;
                tmpLine.line = numLine++;
                tmpLine.opers = new List<ITerminalNode>();
                tmpLine.indexado = false;
                checkError();

                if (prop.directiva() != null)
                {
                    var lineaDirect = prop.directiva();
                    tmpLine.etq = lineaDirect.etiqueta() != null ? lineaDirect.etiqueta() : null;

                    var num = lineaDirect.NUM();
                    var expr = lineaDirect.EXPR();
                    var id = lineaDirect.ID();
                    var consChar = lineaDirect.CONSTCAD();
                    var consHex = lineaDirect.CONSTHEX();
                    var cpref = lineaDirect.CPREF();
                    if (lineaDirect.RESB() != null)
                    {
                        tmpLine.ins = lineaDirect.RESB();
                        tmpLine.opers.Add(num);
                        tmpLine.formato = toInt(num.GetText());
                        if (tmpLine.etq != null)
                            addToSymTable(tmpLine.etq.GetText(), tmpLine.cp.ToString(), "REL"); 
                    }
                    else if (lineaDirect.RESW() != null)
                    {
                        tmpLine.ins = lineaDirect.RESW();
                        tmpLine.opers.Add(num);
                        tmpLine.formato = toInt(num.GetText()) * 3;
                        if (tmpLine.etq != null)
                            addToSymTable(tmpLine.etq.GetText(), tmpLine.cp.ToString(), "REL");
                    }
                    else if (lineaDirect.WORD() != null)
                    {
                        tmpLine.ins = lineaDirect.WORD();
                        tmpLine.opers.Add((num != null) ? num : expr);
                        tmpLine.formato = 3;
                        if (tmpLine.etq != null)
                            addToSymTable(tmpLine.etq.GetText(), tmpLine.cp.ToString(), "REL");
                    }
                    else if (lineaDirect.BYTE() != null)
                    {
                        tmpLine.ins = lineaDirect.BYTE();
                        tmpLine.opers.Add((consChar != null) ? consChar : consHex);
                        if (consChar != null) tmpLine.formato = toBytes(consChar.GetText());
                        if (consHex != null) tmpLine.formato = toBytes(consHex.GetText());
                        if (tmpLine.etq != null)
                            addToSymTable(tmpLine.etq.GetText(), tmpLine.cp.ToString(), "REL");
                    }
                    else if (lineaDirect.BASE() != null)
                    {
                        tmpLine.ins = lineaDirect.BASE();
                        tmpLine.opers.Add(id);
                        if (tmpLine.etq != null)
                            addToSymTable(tmpLine.etq.GetText(), tmpLine.cp.ToString(), "REL");
                    }
                    else if (lineaDirect.EQU() != null)
                    {
                        tmpLine.ins = lineaDirect.EQU();
                        tmpLine.opers.Add((expr != null) ? expr : cpref);
                        if (tmpLine.etq != null){
                            if (expr == null)
                                addToSymTable(tmpLine.etq.GetText(), expr.GetText(), "ABS");
                            else
                                addToSymTable(tmpLine.etq.GetText(), cpref.GetText(), "PEN");
                        }
                    }
                    else if (lineaDirect.USE() != null)
                    {
                        tmpLine.ins = lineaDirect.USE();
                        tmpLine.opers.Add(id);
                    }
                }
                else if (prop.instruccion() != null)
                {
                    var lineaInstr = prop.instruccion();
                    tmpLine.etq = lineaInstr.etiqueta() != null ? lineaInstr.etiqueta() : null;

                    if (lineaInstr.opinstruccion() != null)
                    {
                        var instruccion = lineaInstr.opinstruccion().formato();

                        if (instruccion.f1() != null)
                        {
                            tmpLine.ins = instruccion.f1().CODOPF1();
                            tmpLine.formato = 1;
                        }
                        else if (instruccion.f2() != null)
                        {
                            tmpLine.ins = instruccion.f2().CODOPF2();
                            tmpLine.formato = 2;

                            var f2regs = instruccion.f2().REG();
                            var f2num = instruccion.f2().NUM();
                            if (f2regs.Length > 0) tmpLine.opers.Add(f2regs[0]);
                            if (f2regs.Length > 1) tmpLine.opers.Add(f2regs[1]);
                            if (f2num != null) tmpLine.opers.Add(f2num);
                        }
                        else if (instruccion.f3() != null || instruccion.f4() != null)
                        {
                            int format = (instruccion.f3() != null) ? 3 : 4;
                            tmpLine.formato = format;
                            Object f3Line = (format == 3) ? (Object)instruccion.f3() : (Object)instruccion.f4();
                            var f3Oper = (format == 3) ? ((sicxeParser.F3Context)f3Line).CODOPF3() : ((sicxeParser.F4Context)f3Line).CODOPF4();
                            if (f3Oper != null)
                            {
                                tmpLine.ins = f3Oper;
                                var simpleLine = (format == 3) ? ((sicxeParser.F3Context)f3Line).simple() : ((sicxeParser.F4Context)f3Line).simple();
                                var indircLine = (format == 3) ? ((sicxeParser.F3Context)f3Line).indirecto() : ((sicxeParser.F4Context)f3Line).indirecto();
                                var inmedLine = (format == 3) ? ((sicxeParser.F3Context)f3Line).inmediato() : ((sicxeParser.F4Context)f3Line).inmediato();
                                if (simpleLine != null)
                                {
                                    tmpLine.modo = "Simple";
                                    if (simpleLine.NUM() != null) tmpLine.opers.Add(simpleLine.NUM());
                                    if (simpleLine.ID() != null) tmpLine.opers.Add(simpleLine.ID());
                                    if (simpleLine.EXPR() != null) tmpLine.opers.Add(simpleLine.EXPR());
                                    if (simpleLine.REG() != null)
                                    {
                                        tmpLine.opers.Add(simpleLine.REG());
                                        tmpLine.indexado = simpleLine.REG().GetText().Contains("X");
                                    }
                                }
                                else if (indircLine != null)
                                {
                                    tmpLine.modo = "Indirecto";
                                    if (indircLine.NUM() != null) tmpLine.opers.Add(indircLine.NUM());
                                    if (indircLine.ID() != null) tmpLine.opers.Add(indircLine.ID());
                                    if (indircLine.EXPR() != null) tmpLine.opers.Add(indircLine.EXPR());
                                }
                                else if (inmedLine != null)
                                {
                                    tmpLine.modo = "Inmediato";
                                    if (inmedLine.NUM() != null) tmpLine.opers.Add(inmedLine.NUM());
                                    if (inmedLine.ID() != null) tmpLine.opers.Add(inmedLine.ID());
                                    if (inmedLine.EXPR() != null) tmpLine.opers.Add(inmedLine.EXPR());
                                }
                            }
                            else
                            {
                                tmpLine.modo = "Simple";
                                tmpLine.ins = null;
                            }
                        }
                    }
                    // Añadir a la tabla de símbolos si es que hay etiqueta
                    if (tmpLine.etq != null)
                        addToSymTable(tmpLine.etq.GetText(), tmpLine.cp.ToString(), "REL");
                }
                // Calcular CP solo si no hay errores sintacticos, si es de simbolo duplicado, sumarlo igul
                // if (tmpLine.error == null) progCP += tmpLine.formato;
                if (tmpLine.error == "Símbolo duplicado" || tmpLine.error == null) progCP += tmpLine.formato;
                lineas.Add(tmpLine);
            }
            
            tmpLine = new Linea();
            tmpLine.cp = progCP;
            tmpLine.ins = tree.fin().END();
            tmpLine.opers = new List<ITerminalNode> { };
            tmpLine.line = numLine++;
            if (tree.fin().ID() != null) tmpLine.opers.Add(tree.fin().ID());
            checkError();
            lineas.Add(tmpLine);

            Console.WriteLine("=====================================");

            // Imprimir lineas
            foreach (var line in lineas)
            {
                Console.WriteLine("Línea: " + line.line);
                Console.WriteLine("CP: " + line.cp);
                if (line.etq != null)
                    Console.WriteLine("Etiqueta: " + line.etq.GetText());
                if (line.ins != null)
                    Console.WriteLine("Instrucción: " + line.ins.GetText());
                else
                    Console.WriteLine("Instrucción: RSUB");
                foreach (var oper in line.opers)
                    {
                    if (oper != null)
                        Console.WriteLine("Operando: " + oper.GetText());
                    }
                Console.WriteLine("Formato: " + line.formato);
                Console.WriteLine("Indexado: " + line.indexado);
                if (line.modo != null)
                    Console.WriteLine("Modo: " + line.modo);
                Console.WriteLine("=====================================");
            }

            // Crear tabla de codigo en midTable
            midFile = new Table("Tabla Intermedia");
            midFile.dataGridView.Columns.Add("Linea", "Linea");
            midFile.dataGridView.Columns.Add("Formato", "For/Tam");
            midFile.dataGridView.Columns.Add("CP", "CP");
            midFile.dataGridView.Columns.Add("Etiqueta", "Etiqueta");
            midFile.dataGridView.Columns.Add("Instrucción", "Instrucción");
            midFile.dataGridView.Columns.Add("Operando", "Operando");
            midFile.dataGridView.Columns.Add("Código Objeto", "Código Objeto");
            midFile.dataGridView.Columns.Add("Error", "Error");
            midFile.dataGridView.Columns.Add("Modo", "Modo");
            foreach (var line in lineas)
            {
                var index = midFile.dataGridView.Rows.Add();
                midFile.dataGridView.Rows[index].Cells[0].Value = line.line;
                midFile.dataGridView.Rows[index].Cells[1].Value = line.formato;
                // Pasar a hexadecimal
                midFile.dataGridView.Rows[index].Cells[2].Value = line.cp.ToString("X");
                midFile.dataGridView.Rows[index].Cells[3].Value = (line.etq != null) ? line.etq.GetText() : "";
                midFile.dataGridView.Rows[index].Cells[4].Value = (line.ins != null) ? line.ins.GetText() : "RSUB";
                string oper = "";
                // Ver el tipo de operando
                if(line.modo == "Inmediato")
                    oper += "#";
                else if (line.modo == "Indirecto")
                    oper += "@";
                foreach (var op in line.opers)
                {
                    if (op != null)
                        oper += op.GetText() + ", ";
                }
                oper = oper.TrimEnd(' ').TrimEnd(',');
                midFile.dataGridView.Rows[index].Cells[5].Value = oper;
                midFile.dataGridView.Rows[index].Cells[6].Value = line.codobj;
                midFile.dataGridView.Rows[index].Cells[7].Value = line.error;
                midFile.dataGridView.Rows[index].Cells[8].Value = line.modo;
            }

            // Tabla de símbolos
            symTable = new Table("Tabla de Símbolos");
            symTable.dataGridView.Columns.Add("Nombre", "Nombre");
            symTable.dataGridView.Columns.Add("Valor", "Valor");
            symTable.dataGridView.Columns.Add("Expresión", "Expresión");
            symTable.dataGridView.Columns.Add("Tipo", "Tipo");
            foreach (var sim in simbolos)
            {
                var index = symTable.dataGridView.Rows.Add();
                symTable.dataGridView.Rows[index].Cells[0].Value = sim.nombre;
                symTable.dataGridView.Rows[index].Cells[1].Value = sim.valor;
                // En hexadecimal
                symTable.dataGridView.Rows[index].Cells[2].Value = int.Parse(sim.expresion).ToString("X");
                symTable.dataGridView.Rows[index].Cells[3].Value = sim.tipo;
            }


            // Autoajustar columnas al contenido
            midFile.dataGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            midFile.dataGridView.AutoResizeColumns();
            midFile.dataGridView.AutoResizeRows();
            midFile.dataGridView.Refresh();

            alreadyCompiled = true;
            date = DateTime.Now;
            // Imprimir todos los errores
            Console.WriteLine("Errores léxicos: " + lexelistener.getErroresCount());
            Console.WriteLine(lexelistener.getErrores());
            Console.WriteLine("Errores sintácticos: " + parslistener.getErroresCount());
            Console.WriteLine(parslistener.getErrores());

            // Imprimir árbol de análisis sintáctico
            Console.WriteLine(tree.ToStringTree(parser));


            // Funcion interna para que, de una cadena, se transforme a un entero
            int toInt(string s)
            {
                // Revisar si tiene h o H, si es así transformar a entero
                if (s.Contains("h") || s.Contains("H"))
                {
                    return Convert.ToInt32(s.Substring(0, s.Length - 1), 16);
                }
                // Fue un número normal
                return Convert.ToInt32(s);
            }

            // Función interna para que, de una cadena, con X'...' o C'...', se transforme a el tamaño en bytes
            int toBytes(string s)
            {
                // Revisar si es X o C
                if (s.Contains("X") || s.Contains("x"))
                {
                    // Redondear hacia arriba
                    return (s.Length - 3 + 1) / 2;
                }
                else if (s.Contains("C") || s.Contains("c"))
                {
                    return s.Length - 3;
                }
                return -1;
            }

            bool checkError()
            {
                // Si ya hay errores, no hacer nada
                if (tmpLine.error != null) return true;
                Console.WriteLine("Errores en línea: " + tmpLine.line);
                Console.WriteLine(parslistener.getErrorByLine(tmpLine.line));
                if (parslistener.getErrorByLine(tmpLine.line) != null)
                {
                    if (parslistener.getErrorByLine(tmpLine.line).Contains("expecting"))
                        tmpLine.error = "Error de sintaxis";
                    else if (parslistener.getErrorByLine(tmpLine.line).Contains("no viable"))
                        tmpLine.error = "Instrucción no existe";
                    return true;
                }
                return false;
            }

            void addToSymTable(string etq, string value, string type)
            {
                // Si ya hay errores, no hacer nada
                if (tmpLine.error != null) return;
                Simbolo sim = new Simbolo();
                sim.nombre = etq;
                sim.expresion = value;
                sim.tipo = type;
                // Verificar si no existe
                if (simbolos.FindIndex(x => x.nombre == etq) == -1)
                    simbolos.Add(sim);
                else
                    tmpLine.error = "Símbolo duplicado";
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

    // Clase para lineas de código
    public class Linea
    {
        public int line;
        public int cp { get; set; }
        public sicxeParser.EtiquetaContext etq { get; set; }
        public ITerminalNode ins { get; set; }
        public List<ITerminalNode> opers { get; set; }
        public string codobj { get; set; }
        public string error { get; set; }
        public string modo { get; set; }
        public bool indexado { get; set; }
        public int formato { get; set; }
    }

    public class Simbolo
    {
        public string nombre { get; set; }
        public int valor { get; set; }
        public string expresion { get; set; }
        public string tipo { get; set; }
    }
}
