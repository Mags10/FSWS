using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FSS_01.vistas
{
    public partial class tabView : Form
    {
        public tabView(String title)
        {
            InitializeComponent();
            this.Text = title;
        }

        public void addTab(String title, Control mid)
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.Controls.Add(mid);
            TabPage tab = new TabPage(title);
            tab.Controls.Add(panel);
            tabControl.TabPages.Add(tab);
        }
    }
}
