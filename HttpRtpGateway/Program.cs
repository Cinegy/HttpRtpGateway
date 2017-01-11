using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using HttpRtpGateway.Logging;
using NLog;

namespace HttpRtpGateway
{
    internal class Program
    {
        private static bool _pendingExit;
        private static StreamOptions _options;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static UdpClient _udpClient;
        
        #region Main, Constructors and Destructors

        private static int Main(string[] args)
        {

            var result = Parser.Default.ParseArguments<StreamOptions>(args);

            return result.MapResult(
                Run,
                errs => CheckArgumentErrors());
            
        }

        ~Program()
        {
            Console.CursorVisible = true;
        }

        #endregion

        #region Startup Methods

        private static int CheckArgumentErrors()
        {
            //will print using library the appropriate help - now pause the console for the viewer
            Console.WriteLine("Hit enter to quit");
            Console.ReadLine();
            return -1;
        }

        private static int Run(StreamOptions options)
        {
            Console.CancelKeyPress += Console_CancelKeyPress;
            
            _options = options;

            LogSetup.ConfigureLogger(_options);

            var location = Assembly.GetExecutingAssembly().Location;
            if (location != null)
                Logger.Info($"Cinegy HTTP to RTP gateway tool (Built: {File.GetCreationTime(location)})\n");
            
            Console.WriteLine("Running");
            Console.WriteLine("Hit CTRL-C to quit");

            PrepareOutputClient();

            StartDownload();

            while (!_pendingExit)
            {
                Thread.Sleep(100);
            }

            return 0;
        }

        #endregion

        #region Core Methods

        private static void StartDownload()
        {
            var uri = new Uri(_options.SourceUrl);

            var httpclient = new HttpClient();
            
            var stream = httpclient.GetStreamAsync(uri).Result;

            Console.WriteLine("Starting to re-stream RTP packets");

            //this is horribly 'rough' - will shortly route this data through my RTP decoder
            //and then retime against PCR

            var buff = new byte[1500];

            var count = stream.Read(buff, 0, buff.Length);
            while (count>0)
            {
                count = stream.Read(buff, 0, buff.Length);
                _udpClient.Send(buff, buff.Length);
                if(count!=1316)
                {
                    Console.WriteLine($"Byte array returned {count} bytes (expected 1316)");
                }
            }
        }

        private static void PrepareOutputClient()
        {
            var outputIp = _options.AdapterAddress != null ? IPAddress.Parse(_options.AdapterAddress) : IPAddress.Any;
            Console.WriteLine($"Outputting multicast data to {_options.MulticastAddress}:{_options.MulticastGroup} via adapter {outputIp}");

            _udpClient = new UdpClient { ExclusiveAddressUse = false };
            var localEp = new IPEndPoint(outputIp, _options.MulticastGroup);

            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpClient.ExclusiveAddressUse = false;
            _udpClient.Client.Bind(localEp);

            var parsedMcastAddr = IPAddress.Parse(_options.MulticastAddress);
            _udpClient.Connect(parsedMcastAddr, _options.MulticastGroup);            
        }
        
        #endregion

        #region Events

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Console.CursorVisible = true;
            if (_pendingExit) return; //already trying to exit - allow normal behaviour on subsequent presses
            _pendingExit = true;
            e.Cancel = true;
        }

        #endregion
    }
}
