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
using System.Collections.Generic;
using System.Diagnostics;
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
using Cinegy.TsDecoder.Buffers;

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
        private static ulong _lastPcr;
        private static long _longestWait;
        private static readonly TsPacketFactory Factory = new TsPacketFactory();

        private const int TsPacketSize = 188;
        private const short SyncByte =0x47;

        private static readonly RingBuffer RingBuffer = new RingBuffer();

        #region Main, Constructors and Destructors

        private static int Main(string[] args)
        {
            Console.CursorVisible = false;
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
                Logger.Info($"Started Cinegy HTTP to RTP gateway tool (Built: {File.GetCreationTime(location).ToLongDateString()})");


            Console.WriteLine("Running");
            Console.WriteLine("Hit CTRL-C to quit");

            PrepareOutputClient();

            var downloadThread = new Thread(StartDownload) { Priority = ThreadPriority.Highest };

            downloadThread.Start();

            var queueThread = new Thread(ProcessQueueWorkerThread) { Priority = ThreadPriority.AboveNormal };

            queueThread.Start();


            while (!_pendingExit)
            {
                Console.SetCursorPosition(0, 4);
                Console.WriteLine($"Buffer fullness: {RingBuffer.BufferFullness()}\t\t\t");
                Console.WriteLine($"Longest Wait:{_longestWait}\t\t\t");
                _longestWait = 0;
                Thread.Sleep(100);
            }

            return 0;
        }

        #endregion

        #region Core Methods

        private static void StartDownload()
        {
            try
            {
                _tsDecoder = new TsDecoder();
                var httpclient = new HttpClient();

                var stream = httpclient.GetStreamAsync(_options.SourceUrl).Result;

                Console.WriteLine("Starting to re-stream RTP packets");

                //this is horribly 'rough' - will shortly route this data through my RTP decoder
                //and then retime against PCR

                var buff = new byte[250000];

                var buffPos = 0;
                var count = stream.Read(buff, buffPos, buff.Length);
               
                while (count > 0)
                {
                    buffPos += count;

                    if (buffPos >= TsPacketSize*7)
                    {
                        //buffer now should be big enough to hold 7 ts packets - check sync and load up the ringbuffer

                        var startOfPackets = FindSync(buff, 0);

                        if (startOfPackets != 0)
                        {
                            Console.WriteLine($"Buffer out of sync - should not happen! Must add case to handle this!");
                            throw new InvalidDataException("Junk data in stream start");
                        }

                        var data = new byte[TsPacketSize*7];
                        Buffer.BlockCopy(buff, 0, data, 0, data.Length);

                        AddDataToRingBuffer(ref data);

                        //copy residual data back into buffer and reset pos
                        Buffer.BlockCopy(buff,data.Length,buff,buffPos,buffPos - data.Length);
                        buffPos -= data.Length;
                    }

                    //TODO: This is just testing code - need to make proper test methods and run mis-aligned data in to validate
                    if (count != 1316)
                    {
                        Console.WriteLine($"Byte array returned {count} bytes (expected 1316)");
                    }

                    count = stream.Read(buff, buffPos, buff.Length - buffPos);
                }

            }
            catch (Exception ex)
            {
                Logger.Fatal($"Downloading failed: {ex.Message}");
                Environment.Exit(-1);
            }
        }

        private static void AddDataToRingBuffer(ref byte[] data)
        {
            CheckPcr(data);

            if (_lastPcr > 0)
            {
                //add to buffer once we have a PCR, and set timestamp to the earliest playback time
                var pcrDelta = _lastPcr - _referencePcr;

                var span = new TimeSpan((long)(pcrDelta / 2.7));
                //var broadcastTime = DateTime.UtcNow.Ticks + TimeSpan.TicksPerSecond;

                var broadcastTime = _referenceTime + (pcrDelta / 2.7) + (TimeSpan.TicksPerSecond * 1);

                //if (DateTime.UtcNow.Millisecond%400 == 0)
                //{
                //    Console.WriteLine("PCR Delta: " + span);
                //    Console.WriteLine("Broadcast time: {0}", new TimeSpan((long)(broadcastTime)));
                //}
                //pcrDelta = 0;

                RingBuffer.Add(ref data, (ulong)broadcastTime);

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
                try
                {
                    lock (RingBuffer)
                    {
                        int dataSize;
                        ulong timestamp;
                        var capacity = RingBuffer.Remove(ref dataBuffer, out dataSize, out timestamp);

                        if (capacity > 0)
                        {
                            dataBuffer = new byte[capacity];
                            continue;
                        }

                        if (dataBuffer == null) continue;

                        if (_lastPcr < 1)
                        {
                            continue;
                        }

                        //var elapsedClock = (long)((DateTime.UtcNow.Ticks * 2.7) - _referenceTime);

                        var waitTime = (long)(timestamp - (ulong)(DateTime.UtcNow.Ticks)) / TimeSpan.TicksPerMillisecond;

                        if (_longestWait < waitTime) _longestWait = waitTime;

                        if ((waitTime < 8000) & (waitTime > 0))
                        {
                            if (waitTime > 40)
                            {
                                Console.WriteLine($"Waittime: {waitTime}");
                                Console.WriteLine($"Buffer fullness: {RingBuffer.BufferFullness()}");
                                Console.WriteLine($"Sleeping for: {waitTime}");
                            }

                            Thread.Sleep((int)waitTime);

                            if (RingBuffer.BufferFullness() < 40)
                            {
                                //buffer exhausted - reset
                                Logger.Log(new TelemetryLogEventInfo { Level = LogLevel.Info, Message = $@"Buffer was exhausted - resetting timers" });
                                _lastPcr = 0;
                            }

                            _udpClient.Send(dataBuffer, dataBuffer.Length);
                        }
                        else if (waitTime > -50)
                        {
                            _udpClient.Send(dataBuffer, dataBuffer.Length);
                        }
                        else
                        {
                            Console.WriteLine("Crazy wait time! " + waitTime);
                        }

                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(new TelemetryLogEventInfo { Level = LogLevel.Info, Message = $@"Unhandled exception within network receiver: {ex.Message}" });
                }
            }

            Logger.Log(new TelemetryLogEventInfo { Level = LogLevel.Info, Message = "Stopping analysis thread due to exit request." });
        }

        private static void CheckPcr(byte[] dataBuffer)
        {
            var tsPackets = Factory.GetTsPacketsFromData(dataBuffer);

            if (tsPackets == null)
            {
                Logger.Log(new TelemetryLogEventInfo
                {
                    Level = LogLevel.Info,
                    Key = "NullPackets",
                    Message = "Packet recieved with no detected TS packets"
                });
                return;
            }

            foreach (var tsPacket in tsPackets)
            {
                if (!tsPacket.AdaptationFieldExists) continue;
                if (!tsPacket.AdaptationField.PcrFlag) continue;
                if (tsPacket.AdaptationField.FieldSize < 1) continue;

                if (tsPacket.AdaptationField.DiscontinuityIndicator)
                {
                    Console.WriteLine("Adaptation field discont indicator");
                    continue;
                }

                if (_lastPcr == 0)
                {
                    _referencePcr = tsPacket.AdaptationField.Pcr;
                    _referenceTime = (ulong)(DateTime.UtcNow.Ticks);
                }

                _lastPcr = tsPacket.AdaptationField.Pcr;
            }
        }

        public static int FindSync(IList<byte> tsData, int offset)
        {
            if (tsData == null) throw new ArgumentNullException(nameof(tsData));

            //not big enough to be any kind of single TS packet
            if (tsData.Count < 188)
            {
                return -1;
            }

            try
            {
                for (var i = offset; i < tsData.Count; i++)
                {
                    //check to see if we found a sync byte
                    if (tsData[i] != SyncByte) continue;
                    if (i + 1 * TsPacketSize < tsData.Count && tsData[i + 1 * TsPacketSize] != SyncByte) continue;
                    if (i + 2 * TsPacketSize < tsData.Count && tsData[i + 2 * TsPacketSize] != SyncByte) continue;
                    if (i + 3 * TsPacketSize < tsData.Count && tsData[i + 3 * TsPacketSize] != SyncByte) continue;
                    if (i + 4 * TsPacketSize < tsData.Count && tsData[i + 4 * TsPacketSize] != SyncByte) continue;
                    // seems to be ok
                    return i;
                }
                return -1;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Problem in FindSync algorithm... : ", ex.Message);
                throw;
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
