using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace LR17
{
    public partial class SelectPortForm : Form
    {
        public SelectPortForm()
        {
            InitializeComponent();
        }

        public string Port { get; set; }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            button1.Enabled = comboBox1.SelectedItem != null;
            Port = comboBox1.SelectedItem.ToString();
        }

        private void SelectPortForm_Load(object sender, EventArgs e)
        {
            var portNames = SerialPort.GetPortNames();
            comboBox1.Items.AddRange(portNames);
            if (portNames.Length == 1)
            {
                comboBox1.SelectedItem = portNames[0];
                DialogResult =DialogResult.OK;
                Close();
            }
        }
    }
}
