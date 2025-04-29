using Antlr4.Runtime;
using FSS_01.vistas;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Data;
using System.Text.RegularExpressions;
using FSS_01.sicxe;

namespace FSS_01
{
    internal partial class CompiladorSx
    {
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

        // Tabla de símbolos
        public List<Seccion> secciones = new List<Seccion>();

        public List<Linea> lineas = new List<Linea>();

        public String programObj = "";

        private static bool inic = false;
        // Relación de instrucciones y su código de operación
        static private List<Tuple<string, int>> opers = new List<Tuple<string, int>>();
        // Relación de registros y su código de operación
        static private List<Tuple<string, int>> regs = new List<Tuple<string, int>>();

        public int step = 0;

        public void createTables()
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
                midFile.dataGridView.Rows[index].Cells[8].Value = line.codobj + ((line.realoc && !line.modreg.Contains("*R")) ? " *R" : "") + " " + line.modreg;
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
    }
}
