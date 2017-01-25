using System.Collections.Generic;
using System.Linq;
using NLog;
using NLog.Config;
using NLog.Layouts;
using NLog.Targets;
using NLog.Targets.Wrappers;

namespace HttpRtpGateway.Logging
{
    internal static class LogSetup
    {
        internal static void ConfigureLogger(StreamOptions options)
        {
            var config = new LoggingConfiguration();

            var consoleTarget = new ColoredConsoleTarget();
            config.AddTarget("console", consoleTarget);
            consoleTarget.Layout = @"${date:format=HH\:mm\:ss} ${logger} ${message}";
            config.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, consoleTarget));

            if (options.TelemetryLogging)
            {
                var bufferedEsTarget = ConfigureEsLog(options);
                config.AddTarget("elasticsearch", bufferedEsTarget);
                config.LoggingRules.Add(new TelemetryLoggingRule("*", LogLevel.Info, bufferedEsTarget));
            }

            LogManager.Configuration = config;

        }

        private static BufferingTargetWrapper ConfigureEsLog(StreamOptions options)
        {
            var indexNameParts = new List<string> { "HttpRtpGateway", "${date:format=yyyy.MM.dd}" };

            if (!string.IsNullOrEmpty(options.OrganisationId))
            {
                indexNameParts = new List<string> { $"HttpRtpGateway-{options.OrganisationId}-", "${date:format=yyyy.MM.dd}" };
            }

            var renderedIndex = Layout.FromString(string.Join("-", indexNameParts));

            var elasticSearchTarget = new ElasticSearchTarget
            {
                Layout = new TelemetryLayout(options.DescriptorTags.Split(',').Enumerate().ToArray()),
                Uri = options.ElasticSearchUrl,
                Index = renderedIndex
            };

            var bufferingTarget = new BufferingTargetWrapper(elasticSearchTarget)
            {
                FlushTimeout = 5000
            };

            return bufferingTarget;
        }
    }
}
