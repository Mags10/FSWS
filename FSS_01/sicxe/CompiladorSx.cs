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
        public Table blockTable;
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
        private List<Bloque> bloques = new List<Bloque>();
        List<Linea> lineas = new List<Linea>();

        public String programObj = "";

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
            firstStep();
            createObjectCode();
            createObjectProgram();
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

        private void createObjectProgram()
        {
            // Instrucciones de corte
            List<String> cuts = new List<String> { "ORG", "RESW", "RESB", "USE", "END" };
            String objProg = "H";
            // Nombre del programa a 6 caracteres
            objProg += lineas[0].etq.GetText().PadRight(6, ' ').Substring(0, 6);
            // Dirección de inicio (del primer bloque)
            objProg += bloques[0].dir.ToString("X").PadLeft(6, '0');
            // Longitud del programa
            int progLen = bloques[bloques.Count - 1].dir + bloques[bloques.Count - 1].lon - bloques[0].dir;
            objProg += progLen.ToString("X").PadLeft(6, '0');
            objProg += "\n";
            int lenght = 0;
            String tmp = "";
            String inic = "";
            int primeraInstr = -1;
            foreach (Linea linea in lineas)
            {
                //Si es una instrucción valida, no una directiva, se actualiza primeraInstr
                if(opers.Find(x => x.Item1 == linea.ins.GetText()) != null && primeraInstr == -1)
                    primeraInstr = linea.cp + bloques[linea.bloque].dir;

                if (cuts.Contains(linea.ins.GetText()) && tmp != "")
                { 
                    objProg += "T" + inic + lenght.ToString("X").PadLeft(2, '0') + tmp + "\n";
                    tmp = "";
                    lenght = 0;
                }
                else if (linea.codobj != null)
                {
                    if (tmp == "")
                        inic = (linea.cp + bloques[linea.bloque].dir).ToString("X").PadLeft(6, '0');
                    tmp += linea.codobj;
                    lenght += linea.formato;
                }
            }

            List<Linea> realoc = lineas.FindAll(x => x.realoc);
            foreach (Linea line in realoc)
            {
                // Si es word, se crea un registro de realocación desde su dirección, en 6 medios bytes + Nombre de programa
                // Cualquier otro caso, se crea un registro de realocación desde su dirección + 1 en 5 medios bytes + Nombre de programa
                if (line.ins.GetText() == "WORD")
                    objProg += "M" + (line.cp + bloques[line.bloque].dir).ToString("X").PadLeft(6, '0') + "06+";
                else
                    objProg += "M" + (line.cp + bloques[line.bloque].dir + 1).ToString("X").PadLeft(6, '0') + "05+";
                objProg += lineas[0].etq.GetText().PadRight(6, ' ').Substring(0, 6) + "\n";
            }

            // Si hay un END, se crea un registro de finalización
            if (lineas[lineas.Count - 1].ins.GetText() == "END"){
                // Si tiene operando es una etiqueta, se busca su valor en la tabla de símbolos
                if (lineas[lineas.Count - 1].opers.Count > 0)
                {
                    var sim = simbolos.Find(x => x.nombre == lineas[lineas.Count - 1].opers[0].GetText());
                    if (sim != null)
                        objProg += "E" + sim.valor.ToString("X").PadLeft(6, '0');
                }
                else
                    objProg += "E" + primeraInstr.ToString("X").PadLeft(6, '0');
            }

            this.programObj = objProg;
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
                                        codobj += Convert.ToString(int.Parse(op.GetText()), 2).PadLeft(4, '0');
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
                                // Calcular dirección
                                int dir = 0;
                                if (line.opers.Count > 0)
                                {
                                    // Evaluar la expresión
                                    Tuple<string, int> evalres;
                                    if (line.opers[0].Symbol.Type == sicxeLexer.NUM)
                                        evalres = evalExpression(toInt(line.opers[0].GetText()).ToString(), line, true);
                                    else
                                        evalres = evalExpression(line.opers[0].GetText(), line, true);

                                    // Si es ABS y está entre 0 y 4095 es c
                                    if (evalres.Item1 == "ABS" && evalres.Item2 >= 0 && evalres.Item2 <= 4095)
                                        // El valor de la dirección es el valor de la expresión
                                        dir = evalres.Item2;
                                    else
                                    {
                                        // Es m pero si es ABS 
                                        if (evalres.Item1 == "ABS" || line.formato == 4)
                                        {
                                            // Si no es mayor a 4095, es error de operando fuera de rango
                                            if (line.formato == 4 && evalres.Item1 == "REL") line.realoc = true;
                                            else if (evalres.Item2 <= 4095)
                                            {
                                                line.error = "Operando fuera de rango";
                                                break;
                                            }
                                            dir = evalres.Item2;
                                        }
                                        else
                                        {
                                            int blokDir = bloques.Find(xf => xf.num == line.bloque).dir;
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
                                    if (line.error == "Símbolo duplicado") dir = -1;
                                    
                                }

                                // Juntar los bits de bandera
                                codobj += Convert.ToString(n, 2) + Convert.ToString(i, 2) + Convert.ToString(x, 2) + Convert.ToString(b, 2) + Convert.ToString(p, 2) + Convert.ToString(e, 2);

                                // Si la dirección es negativa.
                                if (dir < 0)
                                    dir = Convert.ToInt32(Convert.ToString(dir, 2).Substring(32 - 12), 2);

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
                            var evalres = evalExpression(line.opers[0].GetText(), line);
                            // Si es relativo, marcar para realocación
                            if (evalres.Item1 == "REL") line.realoc = true;
                            line.codobj = evalres.Item2.ToString("X").PadLeft(6, '0');
                        }
                        else if (line.ins.GetText() == "BYTE")
                        {
                            // Convertir a hexadecimal
                            string val = line.opers[0].GetText();
                            if (val.Contains("X") || val.Contains("x"))
                                val = val.Substring(2, val.Length - 3);
                            else if (val.Contains("C") || val.Contains("c"))
                                val = string.Join("", val.Substring(2, val.Length - 3).Select(x => ((int)x).ToString("X")));
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
                        var sim = simbolos.Find(x => x.nombre == line.opers[0].GetText());
                        if (sim != null) baseReg = sim.valor;
                        else line.error = "Símbolo no encontrado";
                    }
                }
            }
        }

        private void createTables()
        {
            midFile = new Table("Tabla Intermedia");
            List<String> mdHeaders = new List<String> { "Linea", "Formato", "Bloque", "CP", "Etiqueta", "Instrucción", "Operando", "Código Objeto", "Error", "Modo" };
            foreach (var header in mdHeaders) midFile.dataGridView.Columns.Add(header, header);
            foreach (var line in lineas)
            {
                var index = midFile.dataGridView.Rows.Add();
                midFile.dataGridView.Rows[index].Cells[0].Value = line.line;
                midFile.dataGridView.Rows[index].Cells[1].Value = line.formato;
                midFile.dataGridView.Rows[index].Cells[2].Value = line.bloque;
                // Pasar a hexadecimal
                midFile.dataGridView.Rows[index].Cells[3].Value = line.cp.ToString("X");
                midFile.dataGridView.Rows[index].Cells[4].Value = (line.etq != null) ? line.etq.GetText() : "";
                midFile.dataGridView.Rows[index].Cells[5].Value = (line.ins != null) ? line.ins.GetText() : "RSUB";
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
                midFile.dataGridView.Rows[index].Cells[6].Value = oper;
                midFile.dataGridView.Rows[index].Cells[7].Value = line.codobj + ((line.realoc) ? " *" : "");
                midFile.dataGridView.Rows[index].Cells[8].Value = line.error;
                midFile.dataGridView.Rows[index].Cells[9].Value = line.modo;
            }

            symTable = new Table("Tabla de Símbolos");
            List<String> symHeaders = new List<String> { "Nombre", "Valor", "Expresión", "Tipo", "Bloque" };
            foreach (var header in symHeaders) symTable.dataGridView.Columns.Add(header, header);
            foreach (var sim in simbolos)
            {
                var index = symTable.dataGridView.Rows.Add();
                symTable.dataGridView.Rows[index].Cells[0].Value = sim.nombre;
                symTable.dataGridView.Rows[index].Cells[1].Value = sim.valor.ToString("X");
                symTable.dataGridView.Rows[index].Cells[2].Value = sim.expresion;
                symTable.dataGridView.Rows[index].Cells[3].Value = sim.tipo;
                symTable.dataGridView.Rows[index].Cells[4].Value = sim.bloque;

            }


            blockTable = new Table("Tabla de Bloques");
            List<String> blockHeaders = new List<String> { "Número", "Nombre", "Longitud", "Dirección"};
            foreach (var header in blockHeaders) blockTable.dataGridView.Columns.Add(header, header);
            foreach (var block in bloques)
            {
                var index = blockTable.dataGridView.Rows.Add();
                blockTable.dataGridView.Rows[index].Cells[0].Value = block.num;
                blockTable.dataGridView.Rows[index].Cells[1].Value = block.nombre;
                blockTable.dataGridView.Rows[index].Cells[2].Value = block.lon.ToString("X");
                blockTable.dataGridView.Rows[index].Cells[3].Value = block.dir.ToString("X");
            }

            // Autoajustar columnas al contenido
            midFile.dataGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            midFile.dataGridView.AutoResizeColumns();
            midFile.dataGridView.AutoResizeRows();
            midFile.dataGridView.Refresh();

            symTable.dataGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            symTable.dataGridView.AutoResizeColumns();
            symTable.dataGridView.AutoResizeRows();
            symTable.dataGridView.Refresh();

            blockTable.dataGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            blockTable.dataGridView.AutoResizeColumns();
            blockTable.dataGridView.AutoResizeRows();
            blockTable.dataGridView.Refresh();
        }

        private void firstStep()
        {
            int numLine = 1;
            // Crear lineas de código
            Linea tmpLine = new Linea();

            // Bloques
            Bloque tmpBloque = new Bloque();
            tmpBloque.dir = 0;
            tmpBloque.localCP = 0;
            tmpBloque.num = 0;
            tmpBloque.nombre = "Por omisión";

            //tree.inicio().etiqueta();
            tmpLine.etq = tree.inicio().etiqueta();
            tmpLine.ins = tree.inicio().START();
            tmpLine.opers = new List<ITerminalNode> { tree.inicio().NUM() };
            tmpLine.line = numLine++;
            tmpLine.bloque = tmpBloque.num;
            tmpLine.cp = tmpBloque.localCP;
            checkError();
            lineas.Add(tmpLine);

            var tmp = tree.proposiciones();
            var tmp2 = tmp.proposicion();
            foreach (var prop in tmp2)
            {
                tmpLine = new Linea();
                tmpLine.cp = tmpBloque.localCP;
                tmpLine.bloque = tmpBloque.num;
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
                        var sim = simbolos.Find(x => x.nombre == id.GetText());
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
                        if (!bloques.Contains(tmpBloque))
                            bloques.Add(tmpBloque);
                        // Revisar si no existe el bloque con el nombre
                        if (id != null)
                        {
                            if (bloques.FindIndex(x => x.nombre == id.GetText()) == -1)
                            {
                                tmpBloque = new Bloque();
                                tmpBloque.num = bloques.Count;
                                tmpBloque.nombre = id.GetText();
                                tmpBloque.localCP = 0;
                            }
                            else tmpBloque = bloques.Find(x => x.nombre == id.GetText());
                        }
                        else
                        {
                            // Tomar el bloque por omisión
                            tmpBloque = bloques.Find(x => x.nombre == "Por omisión");
                        }
                        tmpLine.bloque = tmpBloque.num;
                        tmpLine.cp = tmpBloque.localCP;
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
                // Calcular CP solo si no hay errores sintacticos, si es de simbolo duplicado, sumarlo igul
                // if (tmpLine.error == null) progCP += tmpLine.formato;
                if (tmpLine.error == "Símbolo duplicado" || tmpLine.error == null) tmpBloque.localCP += tmpLine.formato;
                lineas.Add(tmpLine);
            }

            // Añadir bloque a la lista
            if (!bloques.Contains(tmpBloque))
                bloques.Add(tmpBloque);

            tmpLine = new Linea();
            tmpLine.cp = tmpBloque.localCP;
            tmpLine.bloque = tmpBloque.num;
            tmpLine.ins = tree.fin().END();
            tmpLine.opers = new List<ITerminalNode> { };
            tmpLine.line = numLine++;
            if (tree.fin().ID() != null) tmpLine.opers.Add(tree.fin().ID());
            checkError();
            lineas.Add(tmpLine);


            // Calcular direcciones de los bloques
            for (int i = 0; i < bloques.Count; i++)
            {
                bloques[i].lon = bloques[i].localCP;
                if (i > 0)
                    bloques[i].dir = bloques[i - 1].dir + bloques[i - 1].lon;
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

            void addToSymTable(string etq, string value, string type)
            {
                // Si ya hay errores, no hacer nada
                if (tmpLine.error != null) return;
                Simbolo sim = new Simbolo();
                sim.nombre = etq;
                sim.tipo = type;
                sim.bloque = tmpBloque.num;
                //sim.expresion = value;
                if (type != "EXP")
                    sim.valor = Convert.ToInt32(value);
                else
                {
                    sim.expresion = value;
                    var res = evalExpression(value, tmpLine);
                    sim.valor = res.Item2;
                    sim.tipo = res.Item1;
                }
                // Verificar si no existe
                if (simbolos.FindIndex(x => x.nombre == etq) == -1)
                    simbolos.Add(sim);
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

        private Tuple<string, int> evalExpression(string expr, Linea tmpLine, bool realDirs = false)
        {
            int res = -1;
            string tipo = "";
            string resalt = expr;
            foreach (var sim in simbolos)
            {
                // Revisar si si está en la expresión
                if (expr.Contains(sim.nombre))
                {
                    resalt = resalt.Replace(sim.nombre, sim.tipo);
                    if (sim.tipo == "REL" && realDirs)
                        expr = expr.Replace(sim.nombre, (sim.valor + bloques.Find(x => x.num == sim.bloque).dir).ToString());
                    else
                        expr = expr.Replace(sim.nombre, sim.valor.ToString());
                }
            }
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
                    tmpLine.error = "Expresión inválida";
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

            resalt = ExpressionTransformer.Transform(resalt);

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
                    tmpLine.error = "Expresión inválida";
                    tipo = "ABS";
                    res = -1;
                }
            }
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
        public int bloque { get; set; }
        public bool realoc { get; set; }

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

    public class Simbolo
    {
        public string nombre { get; set; }
        public int valor { get; set; }
        public string expresion { get; set; }
        public string tipo { get; set; }
        public int bloque { get; set; }
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
