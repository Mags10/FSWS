using Antlr4.Runtime;
using FSS_01.vistas;
using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime.Tree;
using System.Data;
using FSS_01.sicxe;
using System.Text.RegularExpressions;

namespace FSS_01
{
    internal partial class CompiladorSx
    {
        public string secondStepExec()
        {
            try
            {
                if (step == 1)
                {
                    createObjectCode();
                    step = 2;
                    return "Paso 2 completado";
                }
                return "El paso 2 ya fue completado";
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error en el paso 2: " + ex.Message);
                return "Error en el paso 2";
            }
        }

        public string thirdStepExec()
        {
            try
            {
                if (step == 2)
                {
                    createObjectProgram();
                    step = 3;
                    return "Código objeto generado";
                }
                return "El código objeto ya fue generado";
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error en la generación del código objeto: " + ex.Message);
                return "Error en la generación del código objeto";
            }
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
                List<Linea> refslns = new List<Linea>();
                if (lineasec.Count != 0) {
                    //refslns = lineasec.FindAll(x => x.ins.GetText() == "EXTREF" || x.ins.GetText() == "EXTDEF");
                    // Otro metodo para que no explote si x.ins es null
                    foreach (var line in lineasec)
                    {
                        if (line.ins != null && (line.ins.GetText() == "EXTREF" || line.ins.GetText() == "EXTDEF"))
                            refslns.Add(line);
                    }
                }
                foreach (Linea line in refslns)
                {
                    // Si es EXTDEF
                    if (line.ins.GetText() == "EXTDEF")
                    {
                        objProg += "D";
                        foreach (ITerminalNode op in line.opers)
                        {
                            var sim = sec.simbolos.Find(x => x.nombre == op.GetText());
                            if (sim == null)
                            {
                                line.error = "Símbolo no encontrado";
                                break;
                            }
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
                    //Console.WriteLine(linea.ToString());
                    //Si es una instrucción valida, no una directiva, se actualiza primeraInstr
                    //if (opers.Find(x => x.Item1 == linea.ins.GetText()) != null && primeraInstr == -1)
                    //primeraInstr = linea.cp + sec.bloques[linea.bloque].dir;
                    // Igual que arriba pero cuidar casos donde ins es null
                    if (linea.ins != null && opers.Find(x => x.Item1 == linea.ins.GetText()) != null && primeraInstr == -1)
                        primeraInstr = linea.cp + sec.bloques[linea.bloque].dir;

                    if (linea.ins != null && cuts.Contains(linea.ins.GetText()) && tmp != "")
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
                Console.WriteLine("Realoc: " + realoc.Count);
                foreach (Linea line in realoc)
                {
                    if (line.realregmode.Count != 0)
                    {
                        foreach (String regm in line.realregmode)
                        {
                            var regm2 = regm.Replace("[PNAME]", lineasec[0].etq.GetText()).PadRight(6, ' ').Substring(0, 6);
                            if (line.ins.GetText() == "WORD")
                                objProg += "M" + (line.cp + sec.bloques[line.bloque].dir).ToString("X").PadLeft(6, '0') + "06" + regm2 + "\n";
                            else
                                objProg += "M" + (line.cp + sec.bloques[line.bloque].dir + 1).ToString("X").PadLeft(6, '0') + "05" + regm2 + "\n";
                        }
                    }
                    else
                    {
                        if (line.ins.GetText() == "WORD")
                            objProg += "M" + (line.cp + sec.bloques[line.bloque].dir).ToString("X").PadLeft(6, '0') + "06+";
                        else
                            objProg += "M" + (line.cp + sec.bloques[line.bloque].dir + 1).ToString("X").PadLeft(6, '0') + "05+";
                        objProg += lineas[0].etq.GetText().PadRight(6, ' ').Substring(0, 6) + "\n";
                    }
                }

                // Si hay un END, se crea un registro de finalización
                if (lineasec[lineasec.Count - 1].ins.GetText() == "END")
                {
                    //if (lineasec[lineasec.Count - 1].error != null) objProg += "EFFFFFF";
                    // Si tiene operando es una etiqueta, se busca su valor en la tabla de símbolos
                    /*else*/ if (lineasec[lineasec.Count - 1].opers.Count > 0)
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
                                        if (line.ins.GetText() != "SHIFTL" && line.ins.GetText() != "SHIFTR")
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

    }
}
