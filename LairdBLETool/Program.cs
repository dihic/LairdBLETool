using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace LairdBLETool
{
    class Program : IDisposable
    {
        private readonly SerialPort serial;
        private readonly AutoResetEvent syncEvent=new AutoResetEvent(false);

        private int phase;
        private readonly StringBuilder message = new StringBuilder();
        private string lastMessage;
        private bool verbose = true;

        public Program(string port) 
        {
            serial = new SerialPort(port, 9600, Parity.None, 8, StopBits.One)
            {
                Handshake = Handshake.RequestToSend,
                NewLine = "\r",
            };
            serial.DataReceived +=(o,e)=>
            {
                var num = serial.BytesToRead;
                if (num == 0)
                    return;
                var data = new byte[num];
                serial.Read(data, 0, num);
                foreach (var b in data)
                {
                    switch (phase)
                    {
                        case 0:
                            if (b == 0x0A)
                                phase = 1;
                            else
                                message.Append((char) b);
                            break;
                        case 1:
                            phase = (b == 0x30) ? 2 : 0;
                            break;
                        case 2:
                            phase = (b == 0x30) ? 3 : 0;
                            break;
                        case 3:
                            if (b == 0x0D)
                            {
                                if (message.Length > 0 && verbose)
                                    Console.WriteLine(message);
                                lastMessage = message.ToString();
                                message.Clear();
                                syncEvent.Set();
                            }
                            phase = 0;
                            break;
                    }
                }
            };
            serial.Open();
            //serial.RtsEnable = true;
            serial.DiscardOutBuffer();
            serial.DiscardInBuffer();
        }

        private void WriteCommand(string payload, int timeout=1000)
        {
            serial.WriteLine(payload);
            if (!syncEvent.WaitOne(timeout))
                throw new InvalidOperationException("Response timeout!");
        }

        private void RequestInfo()
        {
            WriteCommand("AT I 0");
            WriteCommand("AT I 3");
            WriteCommand("AT I 13");
            WriteCommand("AT I 4");
        }

        private void Format()
        {
            WriteCommand("AT&F *", 3000);
        }

        private FileInfo Compile(string path)
        {
            verbose = false;
            WriteCommand("AT I 0");
            var module = lastMessage.Substring(lastMessage.LastIndexOf((char) 0x09)).Trim();
            WriteCommand("AT I 13");
            var hash = lastMessage.Substring(lastMessage.LastIndexOf((char) 0x09)).Trim().Replace(" ", "_");
            verbose = true;

            var compiler = "XComp_" + module + "_" + hash + ".exe";

            if (!File.Exists(compiler))
                throw new InvalidOperationException("Compiler not exist");

            Console.WriteLine("Compiling...");

            var psi = new ProcessStartInfo(compiler, path)
            {
                RedirectStandardOutput = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false
            };
            var process = Process.Start(psi);
            if (process != null)
            {
                var myOutput = process.StandardOutput;
                while (!process.HasExited)
                    Console.WriteLine(myOutput.ReadLine());
            }
            var info = new FileInfo(path);
            var bin = info.Name.Remove(info.Name.LastIndexOf(".", StringComparison.InvariantCulture))+".uwc";
            if (!File.Exists(path))
                throw new InvalidOperationException("Source file " + path + " compile error!");
            Console.WriteLine("Done.");
            return new FileInfo(bin);
        }

        private void WriteFile(string path)
        {
            if (!File.Exists(path))
                throw new IOException("File "+path+" Missing!");
            var info = new FileInfo(path);
            var ext = info.Extension.ToLower();
            var isSource = false;
            if (ext == ".sb")
            {
                info = Compile(path);
                isSource = true;
            }
            else if (ext != ".uwc")
                throw new ArgumentException("Only .uwc file accepted!");

            var split = info.Name.Split('.');
            if (split.Length == 0)
                return;

            var file = info.OpenRead();
            Console.Write("Uploading");
            WriteCommand("AT+DEL \"" + split[0] + "\" +");
            WriteCommand("AT+FOW \"" + split[0] + "\"");
            var sb = new StringBuilder();
            var buffer = new byte[31];
            int len;
            do
            {
                len = file.Read(buffer, 0, 31);
                sb.Clear();
                for (var i = 0; i < len; i++)
                    sb.Append(buffer[i].ToString("X2"));
                WriteCommand("AT+FWRH \"" + sb + "\"");
                Console.Write(".");
            } while (len==31);
            file.Close();
            Console.WriteLine();
            WriteCommand("AT+FCL");
            if (isSource)
                info.Delete();
            Console.WriteLine("Done.");
        }

        private static void RequestUpdateFw()
        {
            //Process.Start(ConfigurationManager.AppSettings["Reflash"]);
            var psi = new ProcessStartInfo(ConfigurationManager.AppSettings["Reflash"])
            {
                RedirectStandardOutput = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false
            };
            var process = Process.Start(psi);
            if (process != null)
            {
                var myOutput = process.StandardOutput;
                while (!process.HasExited)
                    Console.WriteLine(myOutput.ReadLine());
            }
        }
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("No parameters");
                Console.WriteLine();
                Console.WriteLine("Laird BLE Module Tool");
                Console.WriteLine("Ver " + Assembly.GetExecutingAssembly().GetName().Version);
                Console.WriteLine();
                Console.WriteLine("Available commands: ");
                Console.WriteLine(" info");
                Console.WriteLine(" format");
                Console.WriteLine(" upload *.uwc|*.sb");
                Console.WriteLine(" reflash");
                Console.WriteLine(" command \"parameter\"");
                Console.WriteLine();
                return;
            }
            var port = ConfigurationManager.AppSettings["Port"];
            var ports = SerialPort.GetPortNames();
            if (ports.Length == 0)
            {
                Console.WriteLine("No available comm port!");
                return;
            }
            if (port == "AUTO")
                port = ports[0];
            if (ports.All(p => p != port))
            {
                Console.WriteLine("No available comm port!");
                return;
            }
            try
            {
                var program = new Program(port);
                switch (args[0].ToLower())
                {
                    case "upload":
                        if (args.Length!=2)
                           throw new InvalidOperationException("Invalid parameters");
                        program.WriteFile(args[1]);
                        break;
                    case "format":
                        if (args.Length != 1)
                            throw new InvalidOperationException("Invalid parameters");
                        program.Format();
                        break;
                    case "info":
                        if (args.Length != 1)
                            throw new InvalidOperationException("Invalid parameters");
                        program.RequestInfo();
                        break;
                    case "reflash":
                        if (args.Length != 1)
                            throw new InvalidOperationException("Invalid parameters");
                        RequestUpdateFw();
                        break;
                    case "command":
                        if (args.Length != 2)
                            throw new InvalidOperationException("Invalid parameters");
                        program.WriteCommand(args[1]);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine(ex.Message);
            }
        }

        public void Dispose()
        {
            if (serial!=null)
            {
                serial.Close();
            }
        }
    }
}
