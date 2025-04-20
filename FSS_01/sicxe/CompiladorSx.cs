using Antlr4.Runtime;
using FSS_01.vistas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Antlr4.Runtime.Tree;
using System.Data;
using System.Text.RegularExpressions;
using FSS_01.sicxe;
using System.IO.Packaging;

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
        public CommonTokenStream tokens;
        // Parser para el código fuente
        private sicxeParser parser;
        // Listeners para errores
        public ErrorParserListener parslistener;
        public ErrorLexerListener lexelistener;
        // Árbol de análisis sintáctico
        private sicxeParser.ProgContext tree;
        // Variables de estado
        private DateTime date;
        private bool alreadyCompiled = false;
        // Lista de reglas por ANTLR
        public List<Tuple<string, int>> ruleList = new List<Tuple<string, int>>();
        // Tablas de datos
        public Table midFile;

        // Path de archivo
        private string path;

        // Tabla de símbolos
        public List<Seccion> secciones = new List<Seccion>();

        public List<Linea> lineas = new List<Linea>();

        public String programObj = "";

        private static bool inic = false;
        // Relación de instrucciones y su código de operación
        static private List<Tuple<string, int>> opers = new List<Tuple<string, int>>();
        // Relación de registros y su código de operación
        static private List<Tuple<string, int>> regs = new List<Tuple<string, int>>();
        // Lista de directivas
        static private List<string> directivas = new List<string>();

        public CompiladorSx()
        {
            // Cargar las instrucciones y sus códigos de operación para todas las instancias
            if (!inic)
            {
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
                inic = true;
            }
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

            // Reinciar lo necesario
            lineas.Clear();
            secciones.Clear();
            midFile = null;
            programObj = "";
            date = DateTime.Now;
            alreadyCompiled = false;
            step = 0;
        }

        private int step = 0;

        public void procLines()
        {
            step = 0;
            tree = parser.prog();
            firstStep();
            step = 1;
        }

        public void compile()
        {
            if (step == 0) procLines();
            createObjectCode();
            createObjectProgram();
            createTables();

            alreadyCompiled = true;
            date = DateTime.Now;
            // Imprimir todos los errores
            //+-Console.WriteLine("Errores léxicos: " + lexelistener.getErroresCount());
            //+-Console.WriteLine(lexelistener.getErrores());
            //+-Console.WriteLine("Errores sintácticos: " + parslistener.getErroresCount());
            //+-Console.WriteLine(parslistener.getErrores());

            // Imprimir árbol de análisis sintáctico
            //+-Console.WriteLine(tree.ToStringTree(parser));
        }

        
        private void createObjectProgram()
        {
            foreach (Seccion sec in secciones)
            {
                List<Linea> lineasec = lineas.FindAll(x => x.seccion == sec.num);
                // Instrucciones de corte
                List<String> cuts = new List<String> { "ORG", "RESW", "RESB", "USE", "END" };
                String objProg = "H";
                // Nombre del programa a 6 caracteres
                objProg += lineasec[0].etq.GetText().PadRight(6, ' ').Substring(0, 6);
                // Dirección de inicio (del primer bloque)
                objProg += sec.bloques[0].dir.ToString("X").PadLeft(6, '0');
                // Longitud del programa
                int progLen = sec.bloques[sec.bloques.Count - 1].dir + sec.bloques[sec.bloques.Count - 1].lon - sec.bloques[0].dir;
                objProg += progLen.ToString("X").PadLeft(6, '0');
                objProg += "\n";

                // Obtener todas las lineas de código de la sección con EXTREF y EXTDEF
                List<Linea> refslns = lineasec.FindAll(x => x.ins.GetText() == "EXTREF" || x.ins.GetText() == "EXTDEF");
                foreach(Linea line in refslns)
                {
                    // Si es EXTDEF
                    if (line.ins.GetText() == "EXTDEF")
                    {
                        objProg += "D";
                        foreach (ITerminalNode op in line.opers)
                        {
                            var sim = sec.simbolos.Find(x => x.nombre == op.GetText());
                            objProg += sim.nombre.PadRight(6, ' ').Substring(0, 6) + (sim.valor + sec.bloques[sim.bloque].dir).ToString("X").PadLeft(6, '0');    
                        }
                        objProg += "\n";
                    }
                    else if (line.ins.GetText() == "EXTREF")
                    {
                        objProg += "R";
                        foreach (ITerminalNode op in line.opers)
                        {
                            var sim = sec.simbolos.Find(x => x.nombre == op.GetText());
                            objProg += sim.nombre.PadRight(6, ' ').Substring(0, 6);
                        }
                        objProg += "\n";
                    }
                }

                int lenght = 0;
                String tmp = "";
                String inic = "";
                int primeraInstr = -1;
                foreach (Linea linea in lineasec)
                {
                    Console.WriteLine(linea.ToString());
                    //Si es una instrucción valida, no una directiva, se actualiza primeraInstr
                    if (opers.Find(x => x.Item1 == linea.ins.GetText()) != null && primeraInstr == -1)
                        primeraInstr = linea.cp + sec.bloques[linea.bloque].dir;

                    if (cuts.Contains(linea.ins.GetText()) && tmp != "")
                    { 
                        objProg += "T" + inic + lenght.ToString("X").PadLeft(2, '0') + tmp + "\n";
                        tmp = "";
                        lenght = 0;
                    }
                    else if (linea.codobj != null)
                    {
                        if (tmp == "")
                            inic = (linea.cp + sec.bloques[linea.bloque].dir).ToString("X").PadLeft(6, '0');
                        tmp += linea.codobj;
                        lenght += linea.formato;
                    }
                }
                // En caso de que no se haya añadido un corte, se añade el último
                if (tmp != "")
                {
                    objProg += "T" + inic + lenght.ToString("X").PadLeft(2, '0') + tmp + "\n";
                    tmp = "";
                    lenght = 0;
                }

                List<Linea> realoc = lineasec.FindAll(x => x.realoc || x.realregmode.Count > 0);
                foreach (Linea line in realoc)
                {
                    if (line.realregmode.Count != 0)
                    {
                        foreach(String regm in line.realregmode)
                        {
                            var regm2 = regm.Replace("[PNAME]", lineasec[0].etq.GetText()).PadRight(6, ' ').Substring(0, 6);
                            if (line.ins.GetText() == "WORD")
                                objProg += "M" + (line.cp + sec.bloques[line.bloque].dir).ToString("X").PadLeft(6, '0') + "06" + regm2 + "\n";
                            else
                                objProg += "M" + (line.cp + sec.bloques[line.bloque].dir + 1).ToString("X").PadLeft(6, '0') + "05" + regm2 + "\n";
                        }
                    }
                    /*
                    if (line.realoc) 
                    { 
                        // Si es word, se crea un registro de realocación desde su dirección, en 6 medios bytes + Nombre de programa
                        // Cualquier otro caso, se crea un registro de realocación desde su dirección + 1 en 5 medios bytes + Nombre de programa
                        if (line.ins.GetText() == "WORD")
                            objProg += "M" + (line.cp + sec.bloques[line.bloque].dir).ToString("X").PadLeft(6, '0') + "06+";
                        else
                            objProg += "M" + (line.cp + sec.bloques[line.bloque].dir + 1).ToString("X").PadLeft(6, '0') + "05+";

                        objProg +=  + "\n";
                    }*/
                }

                // Si hay un END, se crea un registro de finalización
                if (lineasec[lineasec.Count - 1].ins.GetText() == "END"){
                    if (lineasec[lineasec.Count - 1].error != null) objProg += "EFFFFFF";
                    // Si tiene operando es una etiqueta, se busca su valor en la tabla de símbolos
                    else if (lineasec[lineasec.Count - 1].opers.Count > 0)
                    {
                        var sim = sec.simbolos.Find(x => x.nombre == lineasec[lineasec.Count - 1].opers[0].GetText());
                        if (sim != null)
                            objProg += "E" + (sim.valor + sec.bloques[sim.bloque].dir).ToString("X").PadLeft(6, '0');
                    }
                    else
                        objProg += "E" + primeraInstr.ToString("X").PadLeft(6, '0');
                }
                // Si no, pero, es una seccion diferente de la 0
                else if (lineasec[lineasec.Count - 1].seccion != 0)
                {
                    objProg += "E";
                }

                //this.programObj = objProg;
                sec.objCode = objProg;

            }
        }
        
        private void createObjectCode()
        {
            int baseReg = -1;
            foreach (Linea line in lineas)
            {
                String codobj = "";
                if (line.error != null && line.error != "Símbolo duplicado")
                {
                    line.codobj = "";
                    continue;
                }
                // Para instrucciones
                if (line.formato > 0)
                {
                    // Buscar la instrucción y convertir a hex
                    var instr = opers.Find(x => x.Item1 == line.ins.GetText().Replace("+", ""));
                    if (instr != null)
                    {
                        // A binario el item2 de 8 bits
                        string opCodeOg = Convert.ToString(instr.Item2, 2).PadLeft(8, '0');
                        // Quitar los ultimos 2 bits
                        string opCode = opCodeOg.Substring(0, opCodeOg.Length - 2);
                        codobj += opCode;
                        switch (line.formato)
                        {
                            case 1:
                                codobj = opCodeOg;
                                break;
                            case 2:
                                codobj = opCodeOg;
                                // Recorrer los operandos
                                int opers = line.opers.Count;
                                foreach (var op in line.opers)
                                {
                                    if (op.Symbol.Type == sicxeLexer.REG)
                                    {
                                        var reg = regs.Find(xf => xf.Item1 == op.GetText());
                                        if (reg != null) codobj += Convert.ToString(reg.Item2, 2).PadLeft(4, '0');
                                    }
                                    else
                                    {
                                        if(line.ins.GetText() != "SHIFTL" && line.ins.GetText() != "SHIFTR")
                                            codobj += Convert.ToString(int.Parse(op.GetText()), 2).PadLeft(4, '0');
                                        else
                                            codobj += Convert.ToString(int.Parse(op.GetText()) - 1, 2).PadLeft(4, '0');
                                    }
                                }
                                if (opers == 1) codobj += "0000";
                                break;
                            default:
                                int n = 1;
                                int i = 1;
                                int x = 0;
                                int b = 0;
                                int p = 0;
                                int e = 0;
                                if (line.modo == "Indirecto") i = 0;
                                else if (line.modo == "Inmediato") n = 0;
                                if (line.formato == 4) e = 1;
                                foreach (var op in line.opers)
                                {
                                    if (op.Symbol.Type == sicxeLexer.REG && op.GetText().Contains("X"))
                                    {
                                        x = 1;
                                        break;
                                    }
                                }
                                //+-Console.WriteLine("X: " + x);
                                // Calcular dirección
                                int dir = 0;
                                if (line.opers.Count > 0)
                                {
                                    // Evaluar la expresión
                                    Tuple<string, int> evalres;
                                    if (line.opers[0].Symbol.Type == sicxeLexer.NUM)
                                        evalres = evalExpression(secciones[line.seccion], toInt(line.opers[0].GetText()).ToString(), line, true);
                                    else
                                        evalres = evalExpression(secciones[line.seccion], line.opers[0].GetText(), line, true);

                                    //+-Console.WriteLine(line.ToString());
                                    //+-Console.WriteLine("Evalres: " + evalres.Item1 + " " + evalres.Item2);

                                    // Si es ABS y está entre 0 y 4095 es c
                                    if (evalres.Item1 == "ABS" && evalres.Item2 >= 0 && evalres.Item2 <= 4095 && line.formato == 3)
                                        // El valor de la dirección es el valor de la expresión
                                        dir = evalres.Item2;
                                    else
                                    {
                                        // Es m pero si es ABS 
                                        if (evalres.Item1 == "ABS" || line.formato == 4)
                                        {
                                            dir = evalres.Item2;
                                            // Si no es mayor a 4095, es error de operando fuera de rango
                                            if (line.formato == 4 && evalres.Item1 == "REL") line.realoc = true;
                                            else if (line.error == "Simbolo no encontrado" || line.error == "Expresión inválida")
                                            {
                                                b = 1;
                                                p = 1;
                                            }
                                            else if (evalres.Item1 == "SE")
                                            {
                                                dir = evalres.Item2;
                                            }
                                            else if (evalres.Item2 <= 4095)
                                            {
                                                b = 1;
                                                p = 1;
                                                dir = -1;
                                                line.error = "Operando fuera de rango";
                                            } 
                                        }
                                        else
                                        {
                                            int blokDir = secciones[line.seccion].bloques.Find(xf => xf.num == line.bloque).dir;
                                            var cp = line.cp + line.formato;
                                            var despcp = evalres.Item2 - (cp + blokDir);
                                            var desbase = evalres.Item2 - baseReg;
                                            // Si es relativo al CP
                                            if (despcp >= -2048 && despcp <= 2047)
                                            {
                                                p = 1;
                                                dir = despcp;
                                            }
                                            // Si es relativo a la base
                                            else if (desbase >= 0 && desbase <= 4095)
                                            {
                                                b = 1;
                                                dir = desbase;
                                            }
                                            else
                                            {
                                                line.error = "La instruccion no es relativa al CP ni a la base";
                                                break;
                                            }
                                        }
                                    }
                                    //if (line.error == "Símbolo duplicado") dir = -1;
                                    
                                }

                                // Juntar los bits de bandera
                                codobj += Convert.ToString(n, 2) + Convert.ToString(i, 2) + Convert.ToString(x, 2) + Convert.ToString(b, 2) + Convert.ToString(p, 2) + Convert.ToString(e, 2);

                                // Si la dirección es negativa.
                                if (dir < 0 && line.formato == 3)
                                    dir = Convert.ToInt32(Convert.ToString(dir, 2).Substring(32 - 12), 2);
                                else if (dir < 0 && line.formato == 4)
                                    dir = Convert.ToInt32(Convert.ToString(dir, 2).Substring(32 - 20), 2);

                                // Si es formato 4, poner la dirección en 20 bits, si no, en 12
                                if (line.formato == 4)
                                    codobj += Convert.ToString(dir, 2).PadLeft(20, '0');
                                else
                                    codobj += Convert.ToString(dir, 2).PadLeft(12, '0');
                                codobj = codobj.PadLeft(line.formato * 2, '0');
                                break;
                        }
                        line.codobj = Convert.ToInt64(codobj, 2).ToString("X").PadLeft(line.formato * 2, '0');
                    }
                    else
                    {
                        if (line.ins.GetText() == "WORD")
                        {
                            var evalres = evalExpression(secciones[line.seccion], line.opers[0].GetText(), line, true);
                            // Si es relativo, marcar para realocación
                            if (evalres.Item1 == "REL") line.realoc = true;
                            line.codobj = evalres.Item2.ToString("X").PadLeft(6, '0');
                            // Limitar a ultimos 6 caracteres
                            line.codobj = line.codobj.Substring(line.codobj.Length - 6);
                        }
                        else if (line.ins.GetText() == "BYTE")
                        {
                            // Convertir a hexadecimal
                            string val = line.opers[0].GetText();
                            if (val.Contains("X") || val.Contains("x"))
                                val = val.Substring(2, val.Length - 3);
                            else if (val.Contains("C") || val.Contains("c"))
                                val = string.Join("", val.Substring(2, val.Length - 3).Select(x => ((int)x).ToString("X")));
                            // Si la longitud es impar, poner un 0 a la izquierda
                            if (val.Length % 2 != 0) val = "0" + val;
                            line.codobj = val;
                        }
                    }
                }
                else
                {
                    // Revisar si es BASE
                    if (line.ins.GetText() == "BASE")
                    {
                        // Buscar el valor en la tabla de símbolos
                        var sim = secciones[line.seccion].simbolos.Find(x => x.nombre == line.opers[0].GetText());
                        if (sim != null) baseReg = sim.valor;
                        else line.error = "Símbolo no encontrado";
                    }
                    else if (line.ins.GetText() == "END" && line.opers.Count > 0)
                    {
                        //+-Console.WriteLine("End: " + line.opers.Count);
                        // Revisar que exista el id en tabsim
                        string val = line.opers[0].GetText();
                        var sim = secciones[line.seccion].simbolos.Find(x => x.nombre == val);
                        if (sim == null)
                            line.error = "Símbolo no encontrado";
                    }
                }
            }
        }
        

        private void createTables()
        {
            midFile = new Table("Tabla Intermedia");
            List<String> mdHeaders = new List<String> { "Linea", "Formato", "Sección", "Bloque", "CP", "Etiqueta", "Instrucción", "Operando", "Código Objeto", "Error", "Modo" };
            foreach (var header in mdHeaders) midFile.dataGridView.Columns.Add(header, header);
            foreach (var line in lineas)
            {
                var index = midFile.dataGridView.Rows.Add();
                midFile.dataGridView.Rows[index].Cells[0].Value = line.line;
                midFile.dataGridView.Rows[index].Cells[1].Value = line.formato;
                midFile.dataGridView.Rows[index].Cells[2].Value = line.seccion;
                midFile.dataGridView.Rows[index].Cells[3].Value = line.bloque;
                // Pasar a hexadecimal y ajustar a 4 caracteres
                midFile.dataGridView.Rows[index].Cells[4].Value = line.cp.ToString("X").PadLeft(4, '0');
                midFile.dataGridView.Rows[index].Cells[5].Value = (line.etq != null) ? line.etq.GetText() : "";
                midFile.dataGridView.Rows[index].Cells[6].Value = (line.ins != null) ? line.ins.GetText() : "RSUB";
                string oper = "";
                // Ver el tipo de operando
                if (line.modo == "Inmediato")
                    oper += "#";
                else if (line.modo == "Indirecto")
                    oper += "@";
                foreach (var op in line.opers)
                {
                    if (op != null)
                        oper += op.GetText() + ", ";
                }
                oper = oper.TrimEnd(' ').TrimEnd(',');
                midFile.dataGridView.Rows[index].Cells[7].Value = oper;
                midFile.dataGridView.Rows[index].Cells[8].Value = line.codobj + ((line.realoc) ? " *R" : "") + " " + line.modreg;
                midFile.dataGridView.Rows[index].Cells[9].Value = line.error;
                midFile.dataGridView.Rows[index].Cells[10].Value = line.modo;
            }

            // 1. Desactivar el autosize general
            midFile.dataGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

            for (int i = 0; i < 5 && i < midFile.dataGridView.Columns.Count; i++)
            {
                midFile.dataGridView.Columns[i].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                midFile.dataGridView.Columns[i].Width = 40;
                midFile.dataGridView.Columns[i].Resizable = DataGridViewTriState.False;
            }

            // Las demás se ajustan automáticamente al contenido
            for (int i = 5; i < midFile.dataGridView.Columns.Count; i++)
            {
                midFile.dataGridView.Columns[i].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            }

            // Cambiar el tamaño de ñetra del header    
            midFile.dataGridView.ColumnHeadersDefaultCellStyle.Font = new System.Drawing.Font("Arial", 6);

            midFile.dataGridView.Refresh();

            // para cada sección, crear sus tablas 
            foreach(Seccion sec in secciones)
            {
                sec.createTables();
            }
        }

        private void firstStep()
        {
            int numLine = 1;

            // Seccion
            Seccion tmpSeccion = new Seccion();
            tmpSeccion.num = secciones.Count();
            tmpSeccion.tmpBloque = new Bloque();

            // Bloques
            tmpSeccion.tmpBloque.dir = 0;
            tmpSeccion.tmpBloque.localCP = 0;
            tmpSeccion.tmpBloque.num = 0;
            tmpSeccion.tmpBloque.nombre = "Por omisión";

            // Crear lineas de código
            Linea tmpLine = new Linea();
            //tree.inicio().etiqueta();
            tmpSeccion.nombre = tree.inicio().etiqueta();
            tmpLine.etq = tree.inicio().etiqueta();
            tmpLine.ins = tree.inicio().START();
            tmpLine.opers = new List<ITerminalNode> { tree.inicio().NUM() };
            tmpLine.line = numLine++;
            tmpLine.bloque = tmpSeccion.tmpBloque.num;
            tmpLine.cp = tmpSeccion.tmpBloque.localCP;
            tmpLine.seccion = tmpSeccion.num;

            checkError();
            lineas.Add(tmpLine);

            var tmp = tree.proposiciones();
            var tmp2 = tmp.proposicion();
            foreach (var prop in tmp2)
            {
                tmpLine = new Linea();
                tmpLine.cp = tmpSeccion.tmpBloque.localCP;
                tmpLine.bloque = tmpSeccion.tmpBloque.num;
                tmpLine.line = numLine++;
                tmpLine.opers = new List<ITerminalNode>();
                tmpLine.indexado = false;
                tmpLine.seccion = tmpSeccion.num;
                checkError();

                if (prop.directiva() != null)
                {
                    var lineaDirect = prop.directiva();
                    tmpLine.etq = lineaDirect.etiqueta() != null ? lineaDirect.etiqueta() : null;

                    var num = lineaDirect.NUM();
                    var expr = lineaDirect.EXPR();
                    var ids = lineaDirect.ID();
                    ITerminalNode id = null;
                    if (lineaDirect.ID() != null && lineaDirect.ID().Length > 0)
                        id = lineaDirect.ID()[0];
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
                        //tmpLine.opers.Add((num != null) ? num : expr);
                        if (num != null) tmpLine.opers.Add(num);
                        if (expr != null) tmpLine.opers.Add(expr);
                        if (id != null) tmpLine.opers.Add(id);
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
                        // Buscar el valor en la tabla de símbolos
                        var sim = tmpSeccion.simbolos.Find(x => x.nombre == id.GetText());
                        if (tmpLine.etq != null)
                            addToSymTable(tmpLine.etq.GetText(), tmpLine.cp.ToString(), "REL");
                    }
                    else if (lineaDirect.EQU() != null)
                    {
                        tmpLine.ins = lineaDirect.EQU();
                        if (tmpLine.etq != null)
                        {
                            if (expr != null)
                            {
                                tmpLine.opers.Add(expr);
                                addToSymTable(tmpLine.etq.GetText(), expr.GetText(), "EXP");
                            }
                            else if (cpref != null)
                            {
                                tmpLine.opers.Add(cpref);
                                addToSymTable(tmpLine.etq.GetText(), tmpLine.cp.ToString(), "REL");
                            }
                            else
                            {
                                tmpLine.opers.Add(num);
                                int numvalue = toInt(num.GetText());
                                addToSymTable(tmpLine.etq.GetText(), numvalue.ToString(), "ABS");
                            }
                        }
                    }
                    else if (lineaDirect.USE() != null)
                    {
                        // Añadir bloque a la lista si no está
                        if (!tmpSeccion.bloques.Contains(tmpSeccion.tmpBloque))
                            tmpSeccion.bloques.Add(tmpSeccion.tmpBloque);
                        // Revisar si no existe el bloque con el nombre
                        if (id != null)
                        {
                            if (tmpSeccion.bloques.FindIndex(x => x.nombre == id.GetText()) == -1)
                            {
                                tmpSeccion.tmpBloque = new Bloque();
                                tmpSeccion.tmpBloque.num = tmpSeccion.bloques.Count;
                                tmpSeccion.tmpBloque.nombre = id.GetText();
                                tmpSeccion.tmpBloque.localCP = 0;
                            }
                            else
                                tmpSeccion.tmpBloque = tmpSeccion.bloques.Find(x => x.nombre == id.GetText());
                        }
                        else
                        {
                            // Tomar el bloque por omisión
                            tmpSeccion.tmpBloque = tmpSeccion.bloques.Find(x => x.nombre == "Por omisión");
                        }
                        tmpLine.bloque = tmpSeccion.tmpBloque.num;
                        tmpLine.cp = tmpSeccion.tmpBloque.localCP;
                        tmpLine.ins = lineaDirect.USE();
                        tmpLine.opers.Add(id);
                    }
                    else if (lineaDirect.ORG() != null)
                    {
                        tmpLine.ins = lineaDirect.ORG();
                        tmpLine.opers.Add(num);
                        int valCpPr = toInt(num.GetText());
                        tmpLine.formato = valCpPr - tmpLine.cp;
                    }
                    else if (lineaDirect.EXTREF() != null)
                    {
                        tmpLine.ins = lineaDirect.EXTREF();
                        foreach (var ext in ids)
                        {
                            addToSymTable(ext.GetText(), "-", "-", true);
                            tmpLine.opers.Add(ext);
                        }
                        tmpLine.formato = 0;
                    }
                    else if (lineaDirect.EXTDEF() != null)
                    {
                        tmpLine.ins = lineaDirect.EXTDEF();
                        foreach (var ext in ids)
                        {
                            tmpSeccion.definidos.Add(ext);
                            tmpLine.opers.Add(ext);
                        }
                        tmpLine.formato = 0;
                    } 
                    else if (lineaDirect.CSECT() != null)
                    {
                        //+-Console.WriteLine("CSECT");
                        tmpLine.ins = lineaDirect.CSECT();
                        // Se añade bloque a la lista
                        if (!tmpSeccion.bloques.Contains(tmpSeccion.tmpBloque))
                            tmpSeccion.bloques.Add(tmpSeccion.tmpBloque);

                        // Se añade sección a la lista
                        if (!secciones.Contains(tmpSeccion))
                            secciones.Add(tmpSeccion);

                        // Se crea una nueva sección
                        tmpSeccion = new Seccion();
                        tmpSeccion.tmpBloque = new Bloque();
                        tmpSeccion.nombre = lineaDirect.etiqueta();


                        // Bloques
                        tmpSeccion.tmpBloque.dir = 0;
                        tmpSeccion.tmpBloque.localCP = 0;
                        tmpSeccion.tmpBloque.num = 0;
                        tmpSeccion.tmpBloque.nombre = "Por omisión";

                        tmpSeccion.num = secciones.Count();
                        tmpLine.seccion = tmpSeccion.num;

                        tmpLine.formato = 0;
                        tmpLine.cp = 0;
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
                            tmpLine.formato = 2;
                            var f2tpe = instruccion.f2().CODOPF2T1();
                            if(f2tpe != null)
                            {
                                tmpLine.ins = f2tpe;
                                var f2regs = instruccion.f2().REG();
                                var f2num = instruccion.f2().NUM();
                                if (f2regs.Length > 0) tmpLine.opers.Add(f2regs[0]);
                                if (f2regs.Length > 1) tmpLine.opers.Add(f2regs[1]);
                                else if (f2num != null) tmpLine.opers.Add(f2num);
                            }
                            else
                            {
                                tmpLine.ins = instruccion.f2().CODOPF2T2();
                                var f2regs = instruccion.f2().REG();
                                var f2num = instruccion.f2().NUM();
                                if (f2regs.Length > 0) tmpLine.opers.Add(f2regs[0]);
                                else if (f2num != null) tmpLine.opers.Add(f2num);
                            }
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
                                if (instruccion.f4() != null) {
                                    tmpLine.formato = 4;
                                    tmpLine.ins = instruccion.f4().RSUB();
                                }
                                else
                                {
                                    tmpLine.formato = 3;
                                    tmpLine.ins = instruccion.f3().RSUB();
                                }
                                tmpLine.modo = "Simple";
                            }
                        }
                    }
                    // Añadir a la tabla de símbolos si es que hay etiqueta
                    if (tmpLine.etq != null)
                        addToSymTable(tmpLine.etq.GetText(), tmpLine.cp.ToString(), "REL");
                }
                // Calcular CP solo si no hay errores sintacticos, si es de simbolo duplicado, sumarlo igual
                // el formato guarda el tamaño de la instrucción
                if (tmpLine.error == "Símbolo duplicado" || tmpLine.error == null) tmpSeccion.tmpBloque.localCP += tmpLine.formato;
                lineas.Add(tmpLine);

                ////+-Console.WriteLine("Linea: " + tmpLine.line + " " + tmpLine.ins.GetText() + " " + tmpLine.opers.Count);
            }

            // Añadir bloque a la lista
            if (!tmpSeccion.bloques.Contains(tmpSeccion.tmpBloque))
                tmpSeccion.bloques.Add(tmpSeccion.tmpBloque);

            // Si no se añadió la sección, añadirla, en casi de que solo haya una sección
            secciones.Add(tmpSeccion);

            tmpLine = new Linea();
            tmpLine.cp = secciones[0].bloques[0].localCP;
            tmpLine.bloque = secciones[0].bloques[0].num;
            tmpLine.ins = tree.fin().END();
            tmpLine.opers = new List<ITerminalNode> { };
            tmpLine.line = numLine++;
            tmpLine.seccion = secciones[0].num;
            if (tree.fin().ID() != null) tmpLine.opers.Add(tree.fin().ID());
            checkError();
            lineas.Add(tmpLine);

            // Calcular direcciones de los bloques de todas las secciones
            /*
            for (int i = 0; i < tmpSeccion.bloques.Count; i++)
            {
                tmpSeccion.bloques[i].lon = tmpSeccion.bloques[i].localCP;
                if (i > 0)
                    tmpSeccion.bloques[i].dir = tmpSeccion.bloques[i - 1].dir + tmpSeccion.bloques[i - 1].lon;
            }*/
            foreach (Seccion sec in secciones)
            {
                for (int i = 0; i < sec.bloques.Count; i++)
                {
                    sec.bloques[i].lon = sec.bloques[i].localCP;
                    if (i > 0)
                        sec.bloques[i].dir = sec.bloques[i - 1].dir + sec.bloques[i - 1].lon;
                }
            }

            // Setear definidos para cada sección
            /*
            foreach (var defs in definidos)
            {
                // Revisar si existe el símbolo
                var sim = tmpSeccion.simbolos.Find(x => x.nombre == defs.GetText());
                if (sim != null)
                {
                    sim.definido = true;
                }
                else
                {
                    tmpLine.error = "Símbolo no encontrado para definición externa";
                }
            }*/
            foreach(var sec in secciones)
            {
                foreach (var defs in sec.definidos)
                {
                    // Revisar si existe el símbolo
                    var sim = sec.simbolos.Find(x => x.nombre == defs.GetText());
                    if (sim != null)
                    {
                        sim.definido = true;
                    }
                    else
                    {
                        tmpLine.error = "Símbolo no encontrado para definición externa";
                    }
                }
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
                if (parslistener.getErrorByLine(tmpLine.line) != null)
                {
                    if (parslistener.getErrorByLine(tmpLine.line).Contains("expecting"))
                        tmpLine.error = "Sintaxis";
                    else if (parslistener.getErrorByLine(tmpLine.line).Contains("no viable"))
                        tmpLine.error = "Instrucción no existe";
                    return true;
                }
                return false;
            }

            void addToSymTable(string etq, string value, string type, bool externo = false)
            {
                // Si ya hay errores, no hacer nada
                if (tmpLine.error != null) return;
                Simbolo sim = new Simbolo();
                sim.nombre = etq;
                sim.tipo = type;
                sim.bloque = tmpSeccion.tmpBloque.num;
                sim.externo = externo;
                sim.definido = false;
                if (!externo)
                {
                    if (type != "EXP")
                        sim.valor = Convert.ToInt32(value);
                    else
                    {
                        sim.expresion = value;
                        var res = evalExpression(tmpSeccion, value, tmpLine);
                        sim.valor = res.Item2;
                        sim.tipo = res.Item1;
                     }
                }
                // Verificar si no existe
                if (tmpSeccion.simbolos.FindIndex(x => x.nombre == etq) == -1)
                    tmpSeccion.simbolos.Add(sim);
                else
                    tmpLine.error = "Símbolo duplicado";
            }
        }

        private int toInt(string s)
        {
            // Revisar si tiene h o H, si es así transformar a entero
            if (s.Contains("h") || s.Contains("H"))
            {
                return Convert.ToInt32(s.Substring(0, s.Length - 1), 16);
            }
            // Fue un número normal
            return Convert.ToInt32(s);
        }

        private Tuple<string, int> evalExpression(Seccion sec, string expr, Linea tmpLine, bool realDirs = false)
        {
            ////+-Console.WriteLine("Evaluando expresión: " + expr);
            int res = -1;
            string tipo = "";
            string resalt = expr;
            int blk = -1;
            int rels = 0;

            List<Tuple<string, int>> regmode = new List<Tuple<string, int>>();
            foreach (var sim in sec.simbolos)
            {
                // Revisar si si está en la expresión
                string pattern = $@"\b{Regex.Escape(sim.nombre)}\b";
                if (Regex.IsMatch(expr, pattern))
                {
                    // Revisar que la coincidencia sea exacta, por que puede existir EXPRES, y entraria si hay un simbolo llamado EXPRE
                    // Entraria falsamente
                    if (expr.Contains(sim.nombre + " "))
                        continue;

                    //resalt = resalt.Replace(sim.nombre, sim.tipo);
                    if (sim.tipo != "-")
                    {
                        resalt = resalt.Replace(sim.nombre, sim.tipo);
                        if (sim.tipo == "REL") rels++;
                    }
                    else
                    {
                        Console.WriteLine("Simbolo externo: " + sim.nombre);

                        // Extraer operandos: números y símbolos
                        string patternoprs = @"\b[0-9A-Fa-f]+[hH]?\b|\b[0-9]+\b|\b[A-Za-z_][A-Za-z0-9_]*\b";
                        MatchCollection matches = Regex.Matches(expr, patternoprs);

                        // Buscar el índice del símbolo en la lista de operandos
                        int operandIndex = -1;
                        for (int i = 0; i < matches.Count; i++)
                        {
                            if (matches[i].Value == sim.nombre)
                            {
                                operandIndex = i;
                                break; // Solo la primera aparición
                            }
                        }

                        Console.WriteLine("Número de operando: " + operandIndex);

                        regmode.Add(new Tuple<string, int>(sim.nombre, operandIndex));

                        resalt = resalt.Replace(sim.nombre, "SE");
                        tmpLine.modreg += "*SE ";
                    }

                    //+-Console.WriteLine("RA- Reemplazando " + sim.nombre + " por " + sim.tipo);
                    if (sim.tipo == "REL" && realDirs)
                    {
                        //+-Console.WriteLine("Reemplazando " + sim.nombre + " por " + (sim.valor + sec.bloques.Find(x => x.num == sim.bloque).dir).ToString());
                        expr = expr.Replace(sim.nombre, (sim.valor + sec.bloques.Find(x => x.num == sim.bloque).dir).ToString());
                    }
                    else
                    {
                        //+-Console.WriteLine("Reemplazando " + sim.nombre + " por " + sim.valor.ToString());
                        expr = expr.Replace(sim.nombre, sim.valor.ToString());
                    }
                    if (blk == -1) blk = sim.bloque;
                    else if (blk != sim.bloque && step == 0 && !realDirs)
                    {
                        tmpLine.error = "Expresión inválida 1";
                        res = -1;
                        return new Tuple<string, int>("ABS", res);
                    }
                }
            }

            // Si tiene por lo menos un *SE, y rels es impar, agregar 1 *R
            if (resalt.Contains("SE") && rels % 2 == 1)
            {
                tmpLine.realoc = true;
                tmpLine.modreg += "*R ";
            }

            // Sin contiene h o H, transformar ese valor a entero
            string patternhex = @"\b[0-9A-Fa-f]+(h|H)\b";
            if (Regex.IsMatch(expr, patternhex))
                expr = Regex.Replace(expr, patternhex, m => Convert.ToInt32(m.Value.Substring(0, m.Value.Length - 1), 16).ToString());
            
            if (Regex.IsMatch(resalt, patternhex))
                resalt = Regex.Replace(resalt, patternhex, m => Convert.ToInt32(m.Value.Substring(0, m.Value.Length - 1), 16).ToString());
            

            //+-Console.WriteLine("Expresión: " + expr);
            if (expr.Contains("+") || expr.Contains("-") || expr.Contains("*") || expr.Contains("/"))
            {
                try
                {
                    // Calcular la expresión
                    DataTable dt = new DataTable();
                    res = Convert.ToInt32(dt.Compute(expr, ""));
                }
                catch (Exception e)
                {
                    res = -1;
                    // Indicar error de expresión
                    tmpLine.error = "Expresión inválida 2";
                }
            }
            else
            {
                try
                {
                    res = Convert.ToInt32(expr);
                }
                catch (Exception e)
                {
                    res = -1;
                    // Indicar error de expresión
                    tmpLine.error = "Simbolo no encontrado";
                }
            }

            //+-Console.WriteLine("Pre evaluación: " + resalt);
            resalt = ExpressionTransformer.Transform(resalt);
            //+-Console.WriteLine("Expresión transformada: " + resalt);

            // Obtener todos los operandos con su signo
            // regex, solo pueden ser palabras y signos +, -
            string pattern2 = @"[+-]?\b[A-Za-z_][A-Za-z0-9_]*\b";
            MatchCollection matches2 = Regex.Matches(resalt, pattern2);

            // Lista temporal 
            // Recorrer regmode
            foreach (var reg in regmode)
            {
                // obtener la match con el indice de la lista sobre match2
                Console.WriteLine("Reg: " + reg.Item1 + " " + matches2[reg.Item2].Value);
                if (matches2[reg.Item2].Value.Contains("-"))
                    tmpLine.realregmode.Add("-" + reg.Item1);
                else
                    tmpLine.realregmode.Add("+" + reg.Item1);
                Console.WriteLine("Regmode: " + tmpLine.realregmode[tmpLine.realregmode.Count - 1]);
            }

            int negAbs = Regex.Matches(resalt, "\\-ABS").Count;
            resalt = resalt.Replace("-ABS", "");

            int posAbs = Regex.Matches(resalt, "\\+ABS").Count;
            resalt = resalt.Replace("+ABS", "");
            posAbs += Regex.Matches(resalt, "ABS").Count;

            int negRel = Regex.Matches(resalt, "\\-REL").Count;
            resalt = resalt.Replace("-REL", "");

            int posRel = Regex.Matches(resalt, "\\+REL").Count;
            resalt = resalt.Replace("+REL", "");
            posRel += Regex.Matches(resalt, "REL").Count;

            // Si contiene un SE, es un símbolo externo, no se revisan las reglas
            if (!resalt.Contains("SE"))
            {
                if (posRel == 0 && negRel == 0 && posAbs > 0 && negAbs >= 0)
                {
                    tipo = "ABS";
                }
                else
                {
                    // Emparejar los términos relativos
                    int termRel = posRel - negRel;
                    if (termRel == 1)
                    {
                        tipo = "REL";
                    }
                    else if (termRel == 0)
                    {
                        tipo = "ABS";
                    }
                    else
                    {
                        tmpLine.error = "Expresión inválida 3";
                        tipo = "ABS";
                        res = -1;
                    }
                }
            }
            else
            {
                tipo = "SE";
                if (tmpLine.realoc)
                {
                    string val = ((negRel - posRel) == 1) ? "-" : "+";
                    tmpLine.realregmode.Add(val + "[PNAME]");
                }
            }
            //+-Console.WriteLine("Resultado: " + tipo + " " + res);
            return new Tuple<string, int>(tipo, res);
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

        public string getTokenType(int type)
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
        public int bloque { get; set; }
        public bool realoc { get; set; }
        public int seccion { get; set; }
        public string modreg { get; set; } = "";

        public List<string> realregmode = new List<string>();

        public override String ToString()
        {
            String res = "";
            res += etq != null ? etq.GetText() + " " : "";
            res += ins.GetText() + " ";
            if (modo != null){
                if (modo == "Inmediato") res += "#";
                if (modo == "Indirecto") res += "@";
            }
            foreach (var op in opers)
            {
                res += op.GetText() + ", ";
            }
            res = res.TrimEnd(' ').TrimEnd(',');
            return res;
        }
    }

    public class Seccion
    {
        public sicxeParser.EtiquetaContext nombre { get; set; }
        public int num { get; set; }
        public List<Simbolo> simbolos = new List<Simbolo>();
        public List<Bloque> bloques = new List<Bloque>();
        public Table symTable;
        public Table blockTable;
        public Bloque tmpBloque;
        public List<ITerminalNode> definidos = new List<ITerminalNode>();
        public string objCode { get; set; } = "";

        public void createTables()
        {
            symTable = new Table("Tabla de Símbolos");
            List<String> symHeaders = new List<String> { "Nombre", "Valor", "Expresión", "Tipo", "Bloque", "Externo", "Definido" };
            foreach (var header in symHeaders) symTable.dataGridView.Columns.Add(header, header);
            foreach (var sim in simbolos)
            {
                var index = symTable.dataGridView.Rows.Add();
                symTable.dataGridView.Rows[index].Cells[0].Value = sim.nombre;
                symTable.dataGridView.Rows[index].Cells[1].Value = sim.valor.ToString("X").PadLeft(4, '0').Substring(sim.valor.ToString("X").PadLeft(4, '0').Length - 4);
                symTable.dataGridView.Rows[index].Cells[2].Value = sim.expresion;
                symTable.dataGridView.Rows[index].Cells[3].Value = sim.tipo;
                symTable.dataGridView.Rows[index].Cells[4].Value = sim.bloque;
                symTable.dataGridView.Rows[index].Cells[5].Value = sim.externo ? "Sí" : "No";
                symTable.dataGridView.Rows[index].Cells[6].Value = sim.definido ? "Sí" : "No";

            }


            blockTable = new Table("Tabla de Bloques");
            List<String> blockHeaders = new List<String> { "Número", "Nombre", "Longitud", "Dirección" };
            foreach (var header in blockHeaders) blockTable.dataGridView.Columns.Add(header, header);
            foreach (var block in bloques)
            {
                var index = blockTable.dataGridView.Rows.Add();
                blockTable.dataGridView.Rows[index].Cells[0].Value = block.num;
                blockTable.dataGridView.Rows[index].Cells[1].Value = block.nombre;
                blockTable.dataGridView.Rows[index].Cells[2].Value = block.lon.ToString("X").PadLeft(4, '0').Substring(block.lon.ToString("X").PadLeft(4, '0').Length - 4);
                blockTable.dataGridView.Rows[index].Cells[3].Value = block.dir.ToString("X").PadLeft(4, '0').Substring(block.dir.ToString("X").PadLeft(4, '0').Length - 4);
            }

            symTable.dataGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            symTable.dataGridView.AutoResizeColumns();
            symTable.dataGridView.AutoResizeRows();
            symTable.dataGridView.Refresh();

            blockTable.dataGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            blockTable.dataGridView.AutoResizeColumns();
            blockTable.dataGridView.AutoResizeRows();
            blockTable.dataGridView.Refresh();
        }
    }

    public class Simbolo
    {
        public string nombre { get; set; }
        public int valor { get; set; }
        public string expresion { get; set; }
        public string tipo { get; set; }
        public int bloque { get; set; }
        public bool externo { get; set; }
        public bool definido { get; set; }
    }

    public class Bloque
    {
        public int num { get; set; }
        public int dir { get; set; }
        public int lon { get; set; }
        public int localCP { get; set; }
        public string nombre { get; set; }
    }
}
