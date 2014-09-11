using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using LR17.Properties;

namespace LR17
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Mode mode = Mode.Client;
            if(args.Any(a=>a.ToLower().Contains("s")))
                mode = Mode.Server;
            else if(args.Any(a=>a.ToLower().Contains("c")))
                mode = Mode.Client;
            else if(Settings.Default.Mode.ToLower().Contains("serv"))
                mode = Mode.Server;
            else if (Settings.Default.Mode.ToLower().Contains("cli"))
                mode = Mode.Client;
            Application.Run(new Form1(mode));
        }
    }

    public enum Mode
    {
        Standalone,
        Server,
        Client
    }
}
