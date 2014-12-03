using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml.Serialization;
using DevExpress.Utils;
using DevExpress.XtraCharts;

namespace LR17
{
    public partial class VisualisationForm : Form
    {
        private List<Entry> _data;

        private static int _count;

        public VisualisationForm(List<Entry> data)
        {
            _data = data;
            InitializeComponent();
            Text += " " + (++_count);

            LoadData();
        }

        private bool optimized = true;

        private void LoadData()
        {
            chartControl1.Series[0].Points.Clear();
            chartControl1.BeginInit();
            var s = chartControl1.Series[0];
            for (int i = 0; i < _data.Count; i++)
            {
                var entry = _data[i];
                if(i!=0 && optimized && i!=_data.Count-1)
                {
                    var prev = _data[i - 1];
                    var next = _data[i + 1];
                    if(prev.Point.X<entry.Point.X && entry.Point.X<next.Point.X)
                        continue;
                    if (prev.Point.X > entry.Point.X && entry.Point.X > next.Point.X)
                        continue;
                }
                s.Points.Add(new SeriesPoint(entry.TotalMiliseconds, entry.Point.X));
            }
            chartControl1.EndInit();
            InvalidateLegend();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            chartControl1.ShowPrintPreview();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            var sfd = new SaveFileDialog {Filter = "Файлы лабораторноый работы 17|*.lr17"};
            if(sfd.ShowDialog(this)==DialogResult.OK)
            {
                using (var fileStream = File.Create(sfd.FileName))
                {
                    var xs = new XmlSerializer(typeof(FileData));
                    xs.Serialize(fileStream, new FileData { Data = _data,Frequency = spinEdit1.Value,Current = spinEdit2.Value});
                }
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            var sfd = new OpenFileDialog { Filter = "Файлы лабораторноый работы 17|*.lr17" };
            if (sfd.ShowDialog(this) == DialogResult.OK)
            {
                using (var fileStream = File.OpenRead(sfd.FileName))
                {
                    var xs = new XmlSerializer(typeof(FileData));
                    var dto = (FileData) xs.Deserialize(fileStream);
                    _data = dto.Data;
                    spinEdit1.Value = dto.Frequency;
                    spinEdit2.Value = dto.Current;
                    LoadData();
                }
            }
            InvalidateLegend();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (chartControl1.Series[0].LabelsVisibility != DefaultBoolean.True)
                chartControl1.Series[0].LabelsVisibility = DefaultBoolean.True;
            else
                chartControl1.Series[0].LabelsVisibility = DefaultBoolean.False;
        }

        private void spinEdit1_EditValueChanged(object sender, EventArgs e)
        {
            InvalidateLegend();
        }

        private void InvalidateLegend()
        {
            chartControl1.Series[0].LegendText = string.Format("Частота {0}Гц, Ток демпфирования {1}мА", spinEdit1.Value,
                                                               spinEdit2.Value);
        }

        private void spinEdit2_EditValueChanged(object sender, EventArgs e)
        {
            InvalidateLegend();
        }

        private void button5_Click(object sender, EventArgs e)
        {
            if (Form1.Instance.Mode != Mode.Client)
                Close();
            else
                Form1.Instance.QueryServer();
        }

        private void button6_Click(object sender, EventArgs e)
        {
            optimized = false;
            LoadData();
        }
    }

    public class FileData
    {
        public List<Entry> Data { get; set; }
        public decimal Frequency { get; set; }
        public decimal Current { get; set; }
    }
}
