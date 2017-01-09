using CommandLine;

namespace HttpRtpGateway
{
    internal class Options
    {
        [Option('q', "quiet", Required = false, Default = false,
        HelpText = "Don't print anything to the console")]
        public bool SuppressOutput { get; set; }

        //[Option('l', "logfile", Required = false,
        //HelpText = "Optional file to record events to.")]
        //public string LogFile { get; set; }
        
        [Option('t', "telemetry", Required = false, Default = false,
        HelpText = "Enables telemetry logging to ElasticSearch.")]
        public bool TelemetryLogging { get; set; }

        [Option('e', "elasticsearchurl", Required = false, Default = "http://analytics1.cinegy.net",
        HelpText = "Optional URL to send telemetry logging (defaults to Cinegy public URL).")]
        public string ElasticSearchUrl { get; set; }

        [Option('d', "descriptortags", Required = false,
        HelpText = "Comma separated tag values added to telemetry log entries for instance and machine identification")]
        public string DescriptorTags { get; set; }

        //[Option('e', "timeserieslogging", Required = false,
        //HelpText = "Record time slice metric data to.")]
        //public bool TimeSeriesLogging { get; set; }

        //[Option('v', "verboselogging", Required = false,
        //HelpText = "Creates event logs for all discontinuities and skips.")]
        //public bool VerboseLogging { get; set; }

    }

    // Define a class to receive parsed values
    [Verb("stream", HelpText = "Stream from the network.")]
    internal class StreamOptions : Options
    {
        [Option('m', "multicastaddress", Required = true,
        HelpText = "Multicast address to write to.")]
        public string MulticastAddress { get; set; }

        [Option('g', "mulicastgroup", Required = true,
        HelpText = "Multicast group port to write to.")]
        public int MulticastGroup { get; set; }

        [Option('a', "adapter", Required = false,
        HelpText = "IP address of the adapter to output multicast packets (if not set, tries first binding adapter).")]
        public string AdapterAddress { get; set; }

        [Option('u', "sourceurl", Required = true,
        HelpText = "Source URL from which to download MPEGTS stream")]
        public string SourceUrl { get; set; }
    }
}
