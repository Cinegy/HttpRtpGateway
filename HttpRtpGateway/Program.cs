using System;
using System.IO;
using System.Reflection;
using System.Threading;
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

            while (!_pendingExit)
            {
                Thread.Sleep(100);
            }

            return 0;
        }

        #endregion

        #region Core Methods

        private void StartDownload()
        {
            
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
