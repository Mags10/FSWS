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

        List<string> listaobjs = new List<string>();
        // Desde archivos externos
        public void cargaExt(List<string> lista)
        {
            listaobjs = lista;
        }

        public void cargaLiga0()
        {
            listaobjs = new List<string>();
            foreach (Seccion entrada in this.secciones)
            {
                // Sección de código objeto
                string objCode = entrada.objCode;
                // Agregar a la lista de objetos
                listaobjs.Add(objCode);
            }
        }

        private List<tabseElement> tabse;
        public Table memoryMap;

        public void cargaLigaP1(int DIRPROG)
        {
            tabse = new List<tabseElement>();
            int DIRSC = DIRPROG;
            foreach (string entrada in listaobjs)
            {
                List<String> regs = entrada.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
                String hsec = regs[0];
                String nombre = hsec.Substring(1, 6);
                String dir = hsec.Substring(7, 6);
                String lon = hsec.Substring(13, 6);
                int LONSC = Convert.ToInt32(lon, 16);

                if (tabse.Any(x => x.secControl == nombre))
                {
                    // Error: símbolo externo duplicado
                    Console.WriteLine($"Error: símbolo externo duplicado {nombre}");
                }
                else
                {
                    tabseElement tmp = new tabseElement()
                    {
                        secControl = nombre,
                        simbolo = "",
                        direccion = DIRSC,
                        longitud = LONSC
                    };
                    tabse.Add(tmp);
                    Console.WriteLine(tmp);
                }

                foreach (String reg in regs)
                {
                    if (reg[0] == 'D')
                    {
                        // Cadena temporal para ir borrando, eliminar el primer caracter
                        string regtmp = reg.Substring(1);
                        String nametmp = regtmp.Substring(0, 6);
                        String dirtmp = regtmp.Substring(6, 6);
                        while (true)
                        {
                            // Buscar si el símbolo ya existe en TABSE
                            if (tabse.Any(x => x.simbolo == nametmp))
                            {
                                // Error: símbolo externo duplicado
                                Console.WriteLine($"Error: símbolo externo duplicado {nametmp}");
                            }
                            else
                            {
                                // Agregar el símbolo a TABSE
                                tabseElement tmp = new tabseElement()
                                {
                                    secControl = "",
                                    simbolo = nametmp,
                                    direccion = DIRSC + Convert.ToInt32(dirtmp, 16),
                                    longitud = 0
                                };
                                tabse.Add(tmp);
                                Console.WriteLine(tmp);
                            }
                            // Remover los primeros 12 caracteres
                            regtmp = regtmp.Substring(12);
                            if (regtmp.Length < 12) break;
                            // Leer el siguiente símbolo
                            nametmp = regtmp.Substring(0, 6);
                            dirtmp = regtmp.Substring(6, 6);
                        }
                    }
                }
                DIRSC += LONSC;
            }
        }

        public void cargaLigaP2(int DIRPROG)
        {
            // Iniciar memorymap, con columnas del 0 al F
            memoryMap = new Table("Mapa de Memoria");

            memoryMap.dataGridView.Columns.Add("dir", "Dirección");
            for (int i = 0; i < 16; i++)
            {
                memoryMap.dataGridView.Columns.Add(i.ToString("X"), i.ToString("X"));
                // Redimensionar a un tamaño fijo pequeño
                memoryMap.dataGridView.Columns[i+1].Width = 30;
            }
            int DIRSC = DIRPROG;
            int DIREJ = DIRPROG;
            foreach (string entrada in listaobjs)
            {
                List<String> regs = entrada.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
                String hsec = regs[0];
                String lon = hsec.Substring(13, 6);
                int LONSC = Convert.ToInt32(lon, 16);

                foreach (String reg in regs)
                {
                    if (reg[0] == 'T')
                    {
                        // T00000012050000691000002F1000000F9000001BA003
                        // T 000000 12 050000691000002F1000000F9000001BA003
                        int dir = Convert.ToInt32(reg.Substring(1, 6), 16) + DIRSC;
                        int longt = Convert.ToInt32(reg.Substring(7, 2), 16);
                        int fint = dir + longt;
                        // Ponerlo en el mapa de memoria (crear un renglon con la dirección y los bytes de dos en dos
                        // de todo el contenido despues de la longitud
                        int init = dir - (dir % 16);
                        int fin = fint - (fint % 16);
                        Console.WriteLine($"dir: {dir:X6} longt: {longt:X6} fint: {fint:X6} init: {init:X6} fin: {fin:X6}");

                        // Revisar si no hay ninguna fila en memoryMap.dataGridView
                        if (memoryMap.dataGridView.Rows.Count == 1)
                        {
                            // Si init es diferente de 0, añadir una fila con la dirección 0
                            if (init != 0)
                            {
                                int index = memoryMap.dataGridView.Rows.Add();
                                memoryMap.dataGridView.Rows[index].Cells[0].Value = "000000";
                                for (int j = 0; j < 16; j++)
                                {
                                    memoryMap.dataGridView.Rows[index].Cells[j + 1].Value = "FF";
                                    // Cambiar color de texto a gris claro
                                    memoryMap.dataGridView.Rows[index].Cells[j + 1].Style.ForeColor = System.Drawing.Color.LightGray;
                                }
                            }
                            // Si init no es 0010, añadir fila con ...
                            if (init != 16)
                            {
                                int index = memoryMap.dataGridView.Rows.Add();
                                memoryMap.dataGridView.Rows[index].Cells[0].Value = "...";
                            }
                        }

                        for (int i = init; i <= fin; i += 16)
                        {

                            int index = memoryMap.dataGridView.Rows.Add();
                            memoryMap.dataGridView.Rows[index].Cells[0].Value = i.ToString("X6");

                            for (int j = 0; j < 16; j++)
                            {
                                int currentAddr = i + j;

                                if (currentAddr < dir)
                                {
                                    memoryMap.dataGridView.Rows[index].Cells[j + 1].Value = "FF";
                                    // Cambiar color de texto a gris claro
                                    memoryMap.dataGridView.Rows[index].Cells[j + 1].Style.ForeColor = System.Drawing.Color.LightGray;
                                }
                                else if (currentAddr >= dir && currentAddr < fint)
                                {
                                    Console.WriteLine($"i: {i} j: {j} dir: {dir} fint: {fint}");

                                    int byteIndex = 9 + (currentAddr - dir) * 2;

                                    if (byteIndex + 2 <= reg.Length)
                                    {
                                        string hex = reg.Substring(byteIndex, 2);
                                        memoryMap.dataGridView.Rows[index].Cells[j + 1].Value = hex;
                                    }
                                    else
                                    {
                                        // Fuera del rango, evitar la excepción
                                        memoryMap.dataGridView.Rows[index].Cells[j + 1].Value = "";
                                    }
                                }
                                else
                                {
                                    memoryMap.dataGridView.Rows[index].Cells[j + 1].Value = "FF";
                                    // Cambiar color de texto a gris claro
                                    memoryMap.dataGridView.Rows[index].Cells[j + 1].Style.ForeColor = System.Drawing.Color.LightGray;
                                }
                            }
                        }
                        int tmpind = memoryMap.dataGridView.Rows.Add();
                        memoryMap.dataGridView.Rows[tmpind].Cells[0].Value = "...";
                    }
                    else if (reg[0] == 'M')
                    {
                        // Obtiene la dirección
                        int dir = Convert.ToInt32(reg.Substring(1, 6), 16) + DIRSC;
                        // Obtiene la longitud
                        int longt = Convert.ToInt32(reg.Substring(7, 2), 16);
                        // Signo 
                        String signo = reg.Substring(9, 1);
                        // Simbolo
                        String simbolo = reg.Substring(10, 6);
                        // Obtiene el valor del símbolo
                        int valSimbolo = 0;
                        foreach (var elem in tabse)
                        {
                            if (elem.simbolo == simbolo || elem.secControl == simbolo)
                            {
                                valSimbolo = elem.direccion;
                                break;
                            }
                        }
                        // Si no se encuentra el símbolo, activa la bandera de error
                        if (valSimbolo == 0)
                        {
                            // Error: símbolo externo indefinido
                            Console.WriteLine($"Error: símbolo externo indefinido {simbolo}");
                        }
                        else
                        {
                            // Buscar el renglon en el que se encuentra la dirección
                            int tmpSearchDir = dir - (dir % 16);
                            int index = 0;
                            for (int i = 0; i < memoryMap.dataGridView.Rows.Count; i++)
                            {
                                if (memoryMap.dataGridView.Rows[i].Cells[0].Value.ToString() == tmpSearchDir.ToString("X6"))
                                {
                                    index = i;
                                    break;
                                }
                            }
                            // Obtener los valores de la casilla, y de las proximas dos
                            // Considerar que si es de las ultimas, debe tomar las del siguiente renglon
                            String val1, val2, val3;
                            if ((dir % 16) + 1 <= 16)
                                val1 = memoryMap.dataGridView.Rows[index].Cells[(dir % 16) + 1].Value.ToString();
                            else
                                val1 = memoryMap.dataGridView.Rows[index + 1].Cells[(dir % 16) -16 + 1].Value.ToString();
                            if ((dir % 16) + 2 <= 16)
                                val2 = memoryMap.dataGridView.Rows[index].Cells[(dir % 16) + 2].Value.ToString();
                            else
                                val2 = memoryMap.dataGridView.Rows[index + 1].Cells[(dir % 16) -16 + 2].Value.ToString();
                            if ((dir % 16) + 3 <= 16)
                                val3 = memoryMap.dataGridView.Rows[index].Cells[(dir % 16) + 3].Value.ToString();
                            else
                                val3 = memoryMap.dataGridView.Rows[index + 1].Cells[(dir % 16) -16 + 3].Value.ToString();
                            // Concatenar los valores
                            string val = val1 + val2 + val3;
                            Console.WriteLine($"val1: {val1} val2: {val2} val3: {val3} val: {val}");
                            // Convertir a entero
                            int valInt = Convert.ToInt32(val, 16);
                            // Sumar o restar el valor del símbolo
                            if (signo == "+")
                            {
                                valInt += valSimbolo;
                            }
                            else if (signo == "-")
                            {
                                valInt -= valSimbolo;
                            }
                            // Convertir a hexadecimal
                            string valHex = valInt.ToString("X6");

                            // Si longt es 5, el primer nibble dejarlo como el original
                            if (longt == 5)
                            {
                                valHex = val1.Substring(0,1) + valHex.Substring(1, 5);
                            }

                            // Volver a ponerlo en el mapa de memoria
                            if ((dir % 16) + 1 <= 16)
                            {
                                memoryMap.dataGridView.Rows[index].Cells[(dir % 16) + 1].Value = valHex.Substring(0, 2);
                                memoryMap.dataGridView.Rows[index].Cells[(dir % 16) + 1].Style.ForeColor = System.Drawing.Color.Red;
                            }
                            else
                            {
                                memoryMap.dataGridView.Rows[index + 1].Cells[(dir % 16) - 16 + 1].Value = valHex.Substring(0, 2);
                                memoryMap.dataGridView.Rows[index + 1].Cells[(dir % 16) - 16 + 1].Style.ForeColor = System.Drawing.Color.Red;
                            }

                            if ((dir % 16) + 2 <= 16)
                            {
                                memoryMap.dataGridView.Rows[index].Cells[(dir % 16) + 2].Value = valHex.Substring(2, 2);
                                memoryMap.dataGridView.Rows[index].Cells[(dir % 16) + 2].Style.ForeColor = System.Drawing.Color.Red;
                            }
                            else
                            {
                                memoryMap.dataGridView.Rows[index + 1].Cells[(dir % 16) - 16 + 2].Value = valHex.Substring(2, 2);
                                memoryMap.dataGridView.Rows[index + 1].Cells[(dir % 16) - 16 + 2].Style.ForeColor = System.Drawing.Color.Red;
                            }

                            if ((dir % 16) + 3 <= 16)
                            {
                                memoryMap.dataGridView.Rows[index].Cells[(dir % 16) + 3].Value = valHex.Substring(4, 2);
                                memoryMap.dataGridView.Rows[index].Cells[(dir % 16) + 3].Style.ForeColor = System.Drawing.Color.Red;
                            }
                            else
                            {
                                memoryMap.dataGridView.Rows[index + 1].Cells[(dir % 16) - 16 + 3].Value = valHex.Substring(4, 2);
                                memoryMap.dataGridView.Rows[index + 1].Cells[(dir % 16) - 16 + 3].Style.ForeColor = System.Drawing.Color.Red;
                            }
                        }
                    }
                }

                /*
                 Si se especifica una dirección {en el registro de fin} Entonces 
                  Asigna DIREJ = DIRSC + dirección especificada 
                 Fin_Si 
                 Suma DIRSC = DIRSC + LONSC  
                 
                 */
                if (regs[regs.Count - 1][0] == 'E')
                {
                    try
                    {
                        int dir = Convert.ToInt32(regs[regs.Count - 1].Substring(1, 6), 16);
                        if (dir != 0)
                        {
                            DIREJ = DIRSC + dir;
                        }
                    }
                    catch (Exception e)
                    {
                    }
                }
                DIRSC += LONSC;
            }

            this.createTabseTable();
        }

        // Crear tabse
        public Table tabseTable;
        public void createTabseTable()
        {
            tabseTable = new Table("TABSE");
            List<String> tabseHeaders = new List<String> { "Sección", "Simbolo", "Dirección", "Longitud" };
            foreach (var header in tabseHeaders) tabseTable.dataGridView.Columns.Add(header, header);
            foreach (var elem in tabse)
            {
                var index = tabseTable.dataGridView.Rows.Add();
                tabseTable.dataGridView.Rows[index].Cells[0].Value = elem.secControl;
                tabseTable.dataGridView.Rows[index].Cells[1].Value = elem.simbolo;
                tabseTable.dataGridView.Rows[index].Cells[2].Value = elem.direccion.ToString("X6");
                tabseTable.dataGridView.Rows[index].Cells[3].Value = elem.longitud.ToString("X6");
            }
        }
    }

    public class tabseElement
    {
        public string secControl { get; set; }
        public string simbolo { get; set; }
        public int direccion { get; set; }
        public int longitud { get; set; }

        public override string ToString()
        {
            return $"Sección: {secControl}, Simbolo: {simbolo}, Dirección: {direccion:X6}, Longitud: {longitud:X6}";
        }
    }
}
