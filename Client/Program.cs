using DiscordRPC;
using DiscordRPC.Logging;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Media;
using System.Net;
using System.Timers;
using System.Net.Sockets;
using System.Threading;
using System.Net.NetworkInformation;
using Timer = System.Timers.Timer;

namespace SwitchPresence_Rewritten
{
    public class Program
    {
        private Thread listenThread;
        static Socket client;
        static DiscordRpcClient rpc;
        private IPAddress ipAddress;
        private PhysicalAddress macAddress;
        bool ManualUpdate = false;
        string LastGame = "";
        private Config cfg;
        private Timestamps time = null;
        private Timer timer;

        public struct MacIpPair
        {
            public string MacAddress;
            public string IpAddress;
        }

        [STAThread]
        static void Main()
        {
            new Program();
        }

        public Program()
        {
            Console.CancelKeyPress += new ConsoleCancelEventHandler(Close);
            LoadConfig();
            listenThread = new Thread(TryConnect);
            Connect();
        }

        private void Connect()
        {
            if (string.IsNullOrWhiteSpace(cfg.Client))
            {
                Console.Error.WriteLine("Client ID cannot be empty");
                Environment.Exit(1);
            }

            if (!IPAddress.TryParse(cfg.IP, out ipAddress))
            {
                try
                {
                    macAddress = PhysicalAddress.Parse(cfg.IP.ToUpper());
                    IPAddress.TryParse(Utils.GetIpByMac(cfg.IP), out ipAddress);
                }
                catch (FormatException)
                {
                    Console.Error.WriteLine("Invalid IP or MAC Address");
                    Environment.Exit(1);
                }
            }

            listenThread.Start();
        }

        private void Disconnect()
        {
            listenThread.Abort();
            if (rpc != null && !rpc.IsDisposed)
            {
                rpc.ClearPresence();
                rpc.Dispose();
            }

            if (client != null) client.Close();
            if (timer != null) timer.Dispose();
            listenThread = new Thread(TryConnect);

            macAddress = null;
            ipAddress = null;
            LastGame = "";
            time = null;
        }

        private void OnConnectTimeout(object source, ElapsedEventArgs e)
        {
            LastGame = "";
            time = null;
        }

        private void TryConnect()
        {
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 0xCAFE);

            if (rpc != null && !rpc.IsDisposed)
            {
                rpc.ClearPresence();
                rpc.Dispose();
            }

            rpc = new DiscordRpcClient(cfg.Client);
            rpc.Initialize();

            timer = new Timer()
            {
                Interval = 15000,
                Enabled = false,
            };
            timer.Elapsed += new ElapsedEventHandler(OnConnectTimeout);

            rpc.Logger = new ConsoleLogger() { Level = LogLevel.Warning };
            rpc.OnReady += (s, obj) =>
            {
                Console.WriteLine("Received Ready from user {0}", obj.User.Username);
            };

            while (true)
            {
                client = new Socket(SocketType.Stream, ProtocolType.Tcp)
                {
                    ReceiveTimeout = 5500,
                    SendTimeout = 5500,
                };

                Console.WriteLine("Attemping to connect to server...");
                timer.Enabled = true;

                try
                {
                    IAsyncResult result = client.BeginConnect(localEndPoint, null, null);
                    bool success = result.AsyncWaitHandle.WaitOne(2000, true);
                    if (!success)
                    {
                        //Console.Error.WriteLine("Could not connect to Server! Retrying...");
                        client.Close();
                    }
                    else
                    {
                        client.EndConnect(result);
                        timer.Enabled = false;

                        DataListen();
                    }
                }
                catch (SocketException)
                {
                    client.Close();
                    if (rpc != null && !rpc.IsDisposed) rpc.ClearPresence();
                }
            }
        }

        private void DataListen()
        {
            while (true)
            {
                try
                {
                    byte[] bytes = new byte[600];
                    int cnt = client.Receive(bytes);
                    if (string.IsNullOrEmpty(LastGame)) Console.WriteLine("Connected to the server!");
                    Title title = new Title(bytes);
                    if (title.Magic == 0xffaadd23)
                    {
                        if (LastGame != title.Name)
                        {
                            time = Timestamps.Now;
                        }
                        if ((rpc != null && rpc.CurrentPresence == null) || LastGame != title.Name || ManualUpdate)
                        {
                            Assets ass = new Assets
                            {
                                SmallImageKey = cfg.SmallKey,
                                SmallImageText = "Switch-Presence Rewritten"
                            };
                            RichPresence presence = new RichPresence
                            {
                                State = cfg.State
                            };

                            if (title.Name == "NULL")
                            {
                                ass.LargeImageText = "Home Menu";
                                ass.LargeImageText = !string.IsNullOrWhiteSpace(cfg.BigText) ? cfg.BigText : "Home Menu";
                                ass.LargeImageKey = !string.IsNullOrWhiteSpace(cfg.BigKey) ? cfg.BigKey : string.Format("0{0:x}", 0x0100000000001000);
                                presence.Details = "In the home menu";
                            }
                            else
                            {
                                ass.LargeImageText = !string.IsNullOrWhiteSpace(cfg.BigText) ? cfg.BigText : title.Name;
                                ass.LargeImageKey = !string.IsNullOrWhiteSpace(cfg.BigKey) ? cfg.BigKey : string.Format("0{0:x}", title.Tid);
                                presence.Details = $"Playing {title.Name}";
                            }

                            presence.Assets = ass;
                            if (cfg.DisplayTimer) presence.Timestamps = time;
                            rpc.SetPresence(presence);

                            ManualUpdate = false;
                            LastGame = title.Name;
                        }
                    }
                    else
                    {
                        if (rpc != null && !rpc.IsDisposed) rpc.ClearPresence();
                        client.Close();
                        return;
                    }
                }
                catch (SocketException)
                {
                    if (rpc != null && !rpc.IsDisposed) rpc.ClearPresence();
                    client.Close();
                    return;
                }
            }
        }

        private void LoadConfig()
        {
            cfg = JsonConvert.DeserializeObject<Config>(File.ReadAllText("Config.json"));
        }

        private void Close(object sender, ConsoleCancelEventArgs e)
        {
            if (rpc != null && !rpc.IsDisposed)
            {
                rpc.ClearPresence();
                rpc.Dispose();
            }

            if (client != null) client.Close();
        }
    }
}
