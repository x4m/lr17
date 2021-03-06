﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using System.Xml.Serialization;
using DevExpress.Utils.Text;
using DevExpress.XtraCharts;
using DevExpress.XtraEditors;
using LR17.Properties;

namespace LR17
{
    public partial class Form1 : Form
    {

        static Random rnd = new Random();


        public static Form1 Instance;

        public Form1()
        {
            Instance = this;

            InitializeComponent();
            new ChartControl();
        }


        private bool running;

        private List<Entry> _data;

        const string lr17speed = "lr17speed";

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                EnterHit();
            }
        }

        private void EnterHit()
        {
            if (!running)
            {
                label1.Text = "Записываем";
                label1.ForeColor = Color.Red;
            }
            else
            {
                label1.Text = "Ожидание";
                try
                {
                    if (File.Exists(lr17speed))
                    {
                        double d;
                        if (double.TryParse(File.ReadAllText(lr17speed), out d))
                        {
                            int ms = (int)(_data.Count / d);
                            label1.Text += "; Ожидаемое время визуализации " + new TimeSpan(0, 0, 0, 0, ms).ToString();
                        }
                    }
                }
                catch
                {
                }
                label1.ForeColor = Color.Black;
            }
            Application.DoEvents();
            running = !running;
            if (running)
                RestartRun();
            else
                Stop();
        }

        private void Stop()
        {
            //Cursor.Show();
            var sw = new Stopwatch();
            sw.Start();
            var t = textBox1.Text;
            PrintStats();
            label1.Text = "Ожидание";
            ShowVisualization(sw);
            textBox1.Text = t;
        }

        private void ShowVisualization(Stopwatch sw)
        {
            var form = new VisualisationForm(_data);
            sw.Stop();
            try
            {
                File.WriteAllText(lr17speed, (_data.Count/(double) sw.ElapsedMilliseconds).ToString());
            }
            catch
            {
            }

            form.Show(this);
        }

        private int? correctedX;

        private void PrintStats()
        {
            textBox1.Text = "";
            textBox1.Text += "Время измерения " + sw.Elapsed + Environment.NewLine;
            textBox1.Text += "Количество точек " + _data.Count + Environment.NewLine;

            var avgX = _data.Average(t => t.Point.X);
            var avgY = _data.Average(t => t.Point.Y);

            textBox1.Text += "Среднее отклонение от нуля Х " + avgX + Environment.NewLine;
            textBox1.Text += "Среднее отклонение от нуля Y " + avgY + Environment.NewLine;

            if (correctedX.HasValue)
            {
                foreach (var entry in _data)
                {
                    entry.Point = new PointF(entry.Point.X - correctedX.Value, entry.Point.Y);
                }
            }
            else
            {
                //correctedX = (int?)avgX;
            }
        }

        Stopwatch sw = new Stopwatch();
        Stopwatch sws = Stopwatch.StartNew();

        private void RestartRun()
        {
            _data = new List<Entry>(1 << 18);
            sw.Restart();
        }


        private bool ommitIntermediate = LR17.Properties.Settings.Default.OmmitIntermediateDots;

        private void timer_Tick(object sender, EventArgs e)
        {
            if (running)
            {
                ClientTick();
            }
            
        }



        private void ClientTick()
        {
            if (!al.HasNew)
                return;
            PointF mp = al.Position;
            var time = sw.Elapsed;
            var x = mp.X;
            mp.X = x;
            var y = mp.Y;
            var cmo = _data.Count - 1;
            if (cmo > 1)
            {
                var lastEntry = _data[cmo];
                var prelastEntry = _data[cmo - 1];
                if (correctedX.HasValue && lastEntry.Point == prelastEntry.Point && (int)lastEntry.Point.X == x &&
                    (int)lastEntry.Point.Y == y)
                {
                    lastEntry.Time = time;
                    return;
                }

                if (ommitIntermediate && cmo > 3)
                {
                    var p2 = _data[cmo - 2];
                    var p3 = _data[cmo - 3];
                    if (lastEntry.Point.Y >= prelastEntry.Point.Y && (prelastEntry.Point.Y >= p2.Point.Y) &&
                        y >= lastEntry.Point.Y && (p2.Point.Y >= p3.Point.Y))
                    {
                        mp.Y = y;
                        lastEntry.Point = mp;
                        lastEntry.Time = time;
                        return;
                    }

                    if (lastEntry.Point.Y <= prelastEntry.Point.Y && (prelastEntry.Point.Y <= p2.Point.Y) &&
                        y <= lastEntry.Point.Y && (p2.Point.Y <= p3.Point.Y))
                    {
                        mp.Y = y;
                        lastEntry.Point = mp;
                        lastEntry.Time = time;
                        return;
                    }
                }
            }

            mp.Y = y;

            _data.Add(new Entry() { Point = mp, Time = time });
        }

        private AccelListener al;

        private void Form1_Load(object sender, EventArgs e)
        {
            var spf = new SelectPortForm();
            if (spf.ShowDialog() != DialogResult.OK)
            {
                Close();
                return;
            }
            al = new AccelListener(spf.Port);
        }

        private void button1_Click(object sender, EventArgs e)
        {
        }

       
    }

    [Serializable]
    public class Entry
    {
        private TimeSpan _time;
        [XmlIgnore]
        public TimeSpan Time
        {
            get { return _time; }
            set
            {
                _time = value;
                TotalMiliseconds = value.TotalMilliseconds;
            }
        }

        public double TotalMiliseconds { get; set; }

        public PointF Point { get; set; }
    }
}
