using System;
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
        private readonly Mode _mode;
        private TcpListener _client;
        
        protected static readonly int ServerPort = 8375;
        protected static readonly int ClientPort = 8374;

        private const int PacketSize = 32727;

        static Random rnd = new Random();

        protected static void Send(byte[] dataBytes, IPEndPoint address)
        {
            using (var client = new TcpClient())
            {
                client.Connect(address);
                client.GetStream().Write(dataBytes, 0,dataBytes.Length);
            }
        }

        protected static TcpListener AttachToPort(int port)
        {
            var tcpListener = new TcpListener(port);
            tcpListener.Start();
            return tcpListener;
        }

        public static Form1 Instance;

        public Form1(Mode mode)
        {
            Instance = this;
            _mode = mode;

            InitializeComponent();
            new ChartControl();
            switch (mode)
            {
                case Mode.Standalone:
                    break;
                case Mode.Server:
                    Text += " режим СЕРВЕР";
                    _client = AttachToPort(ServerPort);
                    CursorToCenter();
                    break;
                case Mode.Client:
                    _client = AttachToPort(ClientPort);
                    Text += " режим клиента";
                    break;
                default:
                    throw new ArgumentOutOfRangeException("mode");
            }
        }

        public Mode Mode
        {
            get { return _mode; }
        }

        private bool running;

        private List<Entry> _data;
        private Queue<Entry> _serverData = new Queue<Entry>();

        const string lr17speed = "lr17speed";

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (_mode == Mode.Standalone)
                    EnterHit();
                if (_mode == Mode.Client)
                    Send(new byte[] { (byte)1 }, new IPEndPoint(Dns.GetHostAddresses(Settings.Default.ServerAdress).First(a => a.AddressFamily == AddressFamily.InterNetwork), ServerPort));
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
            if (_mode == Mode.Server)
            {
                var ms = new MemoryStream();
                new BinaryFormatter().Serialize(ms, _data);
                byte[] data = ms.ToArray();
                Send(data, new IPEndPoint(Dns.GetHostAddresses(Settings.Default.ClientAddress).First(a => a.AddressFamily == AddressFamily.InterNetwork), ClientPort));
                sw.Stop();
                try
                {
                    File.WriteAllText(lr17speed, (_data.Count / (double)sw.ElapsedMilliseconds).ToString());
                }
                catch
                {
                }
            }
            else
            {
                var form = new VisualisationForm(_data);
                sw.Stop();
                try
                {
                    File.WriteAllText(lr17speed, (_data.Count / (double)sw.ElapsedMilliseconds).ToString());
                }
                catch
                {
                }

                form.Show(this);
            }
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
                correctedX = (int?)avgX;
            }
        }

        int startx;
        int starty;

        Stopwatch sw = new Stopwatch();
        Stopwatch sws = Stopwatch.StartNew();

        private void RestartRun()
        {
            _data = new List<Entry>(1 << 18);
            sw.Restart();
            CursorToCenter();
        }

        private void CursorToCenter()
        {
            startx = Screen.PrimaryScreen.WorkingArea.Width / 2;
            starty = Screen.PrimaryScreen.WorkingArea.Height / 2;
            Cursor.Position = new Point(startx, starty);
        }

        private bool ommitIntermediate = LR17.Properties.Settings.Default.OmmitIntermediateDots;

        private void timer_Tick(object sender, EventArgs e)
        {
            if (running)
            {
                ClientTick();
            }

            if (_mode == Mode.Server)
            {
                if (_client.Pending())
                {
                    TcpClient client = _client.AcceptTcpClient();
                    var ns = client.GetStream();
                    List<byte> bytes = new List<byte>();
                    while (ns.DataAvailable)
                    {
                        bytes.Add((byte) ns.ReadByte());
                    }
                    client.Close();

                    
                    if (bytes.Count == 1)
                    {
                        EnterHit();
                    }
                    else
                    {
                        var milis = BitConverter.ToDouble(bytes.ToArray(),0);
                        TimeSpan ts = TimeSpan.FromMilliseconds(milis);
                        TimeSpan current = sws.Elapsed;
                        var start = current - ts;
                        List<Entry> list = _serverData.Where(ex => ex.Time > start).ToList();
                        var ms = new MemoryStream();
                        new BinaryFormatter().Serialize(ms, list);
                        byte[] data = ms.ToArray();
                        Send(data, new IPEndPoint(Dns.GetHostAddresses(Settings.Default.ClientAddress).First(a => a.AddressFamily == AddressFamily.InterNetwork), ClientPort));
                    }
                }

                ServerTick();
            }

            if (_mode == Mode.Client)
            {
                if (_client.Pending())
                {
                    TcpClient client = _client.AcceptTcpClient();
                    var ns = client.GetStream();
                    List<byte> bytes = new List<byte>();
                    while (ns.DataAvailable)
                    {
                        bytes.Add((byte)ns.ReadByte());
                    }
                    client.Close();

                    var bf = new BinaryFormatter();
                    _data = (List<Entry>)bf.Deserialize(new MemoryStream(bytes.ToArray()));
                    var start = _data.Min(d=>d.TotalMiliseconds);
                    foreach (var entry in _data)
                    {
                        entry.TotalMiliseconds -= start;
                    }

                    ShowVisualization(Stopwatch.StartNew());
                }
            }
        }

        readonly Dictionary<int, List<byte[]>> _buffers = new Dictionary<int, List<byte[]>>();

        

        private void ServerTick()
        {
            Point mp = MousePosition;
            var time = sws.Elapsed;
            var x = mp.X - startx;
            mp.X = x;
            var y = mp.Y - starty;
            var cmo = _serverData.Count - 1;
            if (cmo > 1)
            {
                var lastEntry = _serverData.Peek();
                var prelastEntry = _serverData.ElementAt(cmo - 1);
                if (correctedX.HasValue && lastEntry.Point == prelastEntry.Point && (int)lastEntry.Point.X == x &&
                    (int)lastEntry.Point.Y == y)
                {
                    lastEntry.Time = time;
                    return;
                }

                if (ommitIntermediate && cmo > 3)
                {
                    var p2 = _serverData.ElementAt(cmo - 2);
                    var p3 = _serverData.ElementAt(cmo - 3);
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

            _serverData.Enqueue(new Entry() { Point = mp, Time = time });

            while (_serverData.First().Time < sw.Elapsed - TimeSpan.FromMinutes(10))
                _serverData.Dequeue();
        }

        private void ClientTick()
        {
            Point mp = MousePosition;
            var time = sw.Elapsed;
            var x = mp.X - startx;
            mp.X = x;
            var y = mp.Y - starty;
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

        private void Form1_Load(object sender, EventArgs e)
        {
            if (_mode == Mode.Client)
            {
                dateTimePicker1.Visible = true;
                button1.Visible = true;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            QueryServer();
        }

        public void QueryServer()
        {
            var dt = dateTimePicker1.Value.TimeOfDay;
            if (dt > TimeSpan.FromMinutes(10))
                dt = TimeSpan.FromMinutes(10);

            byte[] bytes = BitConverter.GetBytes(dt.TotalMilliseconds);

            Send(bytes,
                new IPEndPoint(
                    Dns.GetHostAddresses(Settings.Default.ServerAdress)
                        .First(a => a.AddressFamily == AddressFamily.InterNetwork), ServerPort));
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
