using Antlr4.Runtime.Tree;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using FSS_01.vistas;

namespace FSS_01.sicxe
{
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
            if (modo != null)
            {
                if (modo == "Inmediato") res += "#";
                if (modo == "Indirecto") res += "@";
            }
            foreach (var op in opers)
            {
                if (op != null)
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
