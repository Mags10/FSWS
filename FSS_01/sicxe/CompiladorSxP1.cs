using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime.Tree;
using FSS_01.sicxe;

namespace FSS_01
{
    internal partial class CompiladorSx
    {
        public string firstStepExec()
        {
            try
            {
                if (step == 0)
                {
                    firstStep();
                    step++;
                    return "Paso 1 completado";
                }
                return "El paso 1 ya fue completado";
            }
            catch (Exception ex)
            {
                // Manejo de errores
                Console.WriteLine("Error en el paso 1: " + ex.Message);
                return "Error en el paso 1";
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
                                if (num != null)
                                {
                                    int numvalue = toInt(num.GetText());
                                    addToSymTable(tmpLine.etq.GetText(), numvalue.ToString(), "ABS");
                                }
                                else
                                {
                                    // El simbolo no está en el mismo bloque (¿Marcar error?)
                                    addToSymTable(tmpLine.etq.GetText(), "-1", "ABS");
                                    tmpLine.error = "Símbolo no está en el mismo bloque";
                                    // Añadir con -1 en la tabla de símbolos (ABS)
                                }
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

    }
}
