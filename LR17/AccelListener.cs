using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;

namespace LR17
{
    class AccelListener:IDisposable
    {
        private readonly string _name;

        public AccelListener(string name)
        {
            _name = name;
            new Thread(Reading) { IsBackground = true }.Start();
        }

        private bool _valuesInitialized;
        private float _x;
        private float _y;
        private float _z;

        private void Reading()
        {
            var currentPort = new SerialPort(_name, 115200);
            currentPort.Open();
            while (!stop)
            {
                var line = currentPort.ReadLine();
                try
                {
                    var strings = line.Replace("\r", "").Split(',');
                    var x = (int)int.Parse(strings[0]) / 16384.0F;
                    var y = (int)int.Parse(strings[1]) / 16384.0F;
                    var z = (int)int.Parse(strings[2]) / 16384.0F;
                    if (!_valuesInitialized)
                    {
                        _x = x;
                        _y = y;
                        _z = z;
                        _valuesInitialized = true;
                    }

                    _x = (_x*3 + x)/4;
                    _y = (_y*3 + y)/4;
                    _z = (_z*3 + z)/4;
                }
                catch (Exception)
                {

                }

                HasNew = true;
            }
        }

        private bool stop;
        private bool _hasNew;

        public float Acceleration { get { return (float)Math.Sqrt(_x * _x + _y * _y + _z * _z); } }

        public float X
        {
            get { return _x; }
        }

        public float Y
        {
            get { return _y; }
        }

        public float Z
        {
            get { return _z; }
        }

        public PointF Position { get{return new PointF((float)Acceleration,(float)X);} }

        public bool HasNew
        {
            get
            {
                var b = _hasNew;
                _hasNew = false;
                return b;
            }
            set { _hasNew = value; }
        }

        public void Dispose()
        {
            stop = true;
        }
    }
}
