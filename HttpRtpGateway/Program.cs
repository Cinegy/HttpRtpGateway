/*   Copyright 2017 Cinegy GmbH

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/

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
using Cinegy.TsDecoder.TransportStream;
using Cinegy.TsDecoder.TransportStream.Buffers;

namespace HttpRtpGateway
{
    internal class Program
    {
        private static bool _pendingExit;
        private static StreamOptions _options;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static UdpClient _udpClient;
        private static TsDecoder _tsDecoder;
        private static bool _warmedUp = false;
        private static ulong _referencePcr;
        private static ulong _referenceTime;
        private static ulong _lastPcr = 0;

        private static readonly RingBuffer RingBuffer = new RingBuffer();

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
            
            var downloadThread = new Thread(StartDownload) { Priority = ThreadPriority.Highest };

            downloadThread.Start();

            var queueThread = new Thread(ProcessQueueWorkerThread) { Priority = ThreadPriority.AboveNormal };

            queueThread.Start();

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
            _tsDecoder = new TsDecoder();
            var httpclient = new HttpClient();
            
            var stream = httpclient.GetStreamAsync(_options.SourceUrl).Result;

            Console.WriteLine("Starting to re-stream RTP packets");

            //this is horribly 'rough' - will shortly route this data through my RTP decoder
            //and then retime against PCR
            
            var buff = new byte[1500];

            var count = stream.Read(buff, 0, buff.Length);
            while (count>0)
            {
                count = stream.Read(buff, 0, buff.Length);

                //TODO: At the moment, this ring buffer is just fixed at ~64,000 elements, which might be too little at higher rates...
                RingBuffer.Add(ref buff);
                
                //TODO: This is just testing code - need to make proper test methods and run mis-aligned data in to validate
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

        private static void ProcessQueueWorkerThread()
        {
            var dataBuffer = new byte[12 + (188 * 7)];

            while (_pendingExit != true)
            {
                int dataSize;
                long timestamp;
                
                try
                {
                    lock (RingBuffer)
                    {
                        var capacity = RingBuffer.Remove(ref dataBuffer, out dataSize, out timestamp);

                        if (capacity > 0)
                        {
                            dataBuffer = new byte[capacity];
                            continue;
                        }

                        if (dataBuffer == null) continue;
                        
                        var tsDiff = (int)(RingBuffer.CurrentTimestamp() - timestamp);

                        var tsPackets = TsPacketFactory.GetTsPacketsFromData(dataBuffer);

                        if (tsPackets == null)
                        {
                            Logger.Log(new TelemetryLogEventInfo { Level = LogLevel.Info, Key = "NullPackets", Message = "Packet recieved with no detected TS packets" });
                            continue;
                        }

                        //this PCR retiming does not work yet...
                        //foreach(var tsPacket in tsPackets)
                        //{
                        //    var pcrDrift = CheckPcr(tsPacket);

                        //    if (pcrDrift < 1) continue;

                        //    if (pcrDrift < 5000)
                        //    {
                        //        Console.WriteLine($"Pcrdrift: {pcrDrift}");
                        //        Console.WriteLine($"Buffer fullness: {RingBuffer.BufferFullness()}");
                        //        Console.WriteLine($"Sleeping for: {5000 - pcrDrift}");
                        //        Thread.Sleep(5000 - pcrDrift);
                        //        Console.WriteLine($"Buffer fullness: {RingBuffer.BufferFullness()}");

                        //    }
                        //}

                        _udpClient.Send(dataBuffer, dataBuffer.Length);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(new TelemetryLogEventInfo { Level = LogLevel.Info, Message = $@"Unhandled exception within network receiver: {ex.Message}" });
                }
            }

            Logger.Log(new TelemetryLogEventInfo { Level = LogLevel.Info, Message = "Stopping analysis thread due to exit request." });
        }

        private static int CheckPcr(TsPacket tsPacket)
        {
            if (!tsPacket.AdaptationFieldExists) return 0;
            if (!tsPacket.AdaptationField.PcrFlag) return 0;
            if (tsPacket.AdaptationField.FieldSize < 1) return 0;

            if (tsPacket.AdaptationField.DiscontinuityIndicator)
            {
                Console.WriteLine("Adaptation field discont indicator");
                return 0;
            }

            if (_lastPcr != 0)
            {
                var latestDelta = tsPacket.AdaptationField.Pcr - _lastPcr;
               
                var elapsedPcr = (long)(tsPacket.AdaptationField.Pcr - _referencePcr);
                var elapsedClock = (long)((DateTime.UtcNow.Ticks * 2.7) - _referenceTime);
                var drift = (int)(elapsedClock - elapsedPcr) / 27000;
                
                drift = (int)(elapsedPcr - elapsedClock) / 27000;

                return drift;
            }
            else
            {
                //first PCR value - set up reference values
                _referencePcr = tsPacket.AdaptationField.Pcr;
                _referenceTime = (ulong)(DateTime.UtcNow.Ticks * 2.7);
            }

            //if (_largePcrDriftCount > 5)
            //{
            //    //exceeded PCR drift ceiling - reset clocks
            //    _referencePcr = tsPacket.AdaptationField.Pcr;
            //    _referenceTime = (ulong)(DateTime.UtcNow.Ticks * 2.7);
            //}

            _lastPcr = tsPacket.AdaptationField.Pcr;

            return 0;
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
