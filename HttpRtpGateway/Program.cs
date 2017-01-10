using System;
using System.IO;
using System.Net;
using System.Net.Http;
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

        /*
                private enum ExitCodes
                {
                    NullOutputWriter = 100,
                    InvalidContext = 101,
                    UrlAccessDenied = 102,
                    UnknownError = 2000
                }
        */

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

            var wc = new WebClient();
            var uri = new Uri(_options.SourceUrl);

            //uri = new Uri("https://1drv.ms/v/s!AusAyiZlw38aiuhc-5BISltUSbq4dQ");

            HttpGetForLargeFileInRightWay(uri).ConfigureAwait(true);


        }

        public static async Task HttpGetPartialDownloadTest(Uri uri)
        {
            //ServicePointManager.CertificatePolicy = delegate { return true; };

            var httpclient = new HttpClient();
            var response = await httpclient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);

            string text = null;

            while (true)
            {
                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    var bytes = new byte[1000];
                    var bytesread = stream.Read(bytes, 0, 1000);

                    Console.WriteLine($"Read: {bytesread} bytes");
                }
            }

        }

        static async Task HttpGetForLargeFileInRightWay(Uri uri)
        {
            using (HttpClient client = new HttpClient())
            {
                client.MaxResponseContentBufferSize = 1388;
                while (true)
                {

                    var buff = await client.GetByteArrayAsync(uri);
                    Console.WriteLine("Buffd");
                }




                //using (HttpResponseMessage response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead))
                //using (Stream streamToReadFrom = await response.Content.ReadAsStreamAsync())
                //{
                //    string fileToWriteTo = Path.GetTempFileName();

                //    Console.WriteLine($"Outputting to temp file: {fileToWriteTo}");

                //    using (Stream streamToWriteTo = File.Open(fileToWriteTo, FileMode.Create))
                //    {
                //        await streamToReadFrom.CopyToAsync(streamToWriteTo);
                //        Console.WriteLine($"Something happened...");
                //    }
                //}
            }
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
