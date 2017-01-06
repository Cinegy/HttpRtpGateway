using NLog.Config;
using NLog.Targets;

namespace HttpRtpGateway.Logging
{
    public class TelemetryLoggingRule : LoggingRule
    {
        #region Constructors

        /// <summary>
        ///     Create an empty <see cref="T:NLog.Config.LoggingRule" />.
        /// </summary>
        public TelemetryLoggingRule()
        {
        }

        /// <summary>
        ///     Create a new <see cref="T:NLog.Config.LoggingRule" /> with a <paramref name="minLevel" /> and
        ///     <paramref name="maxLevel" /> which writes to <paramref name="target" />.
        /// </summary>
        /// <param name="loggerNamePattern">
        ///     Logger name pattern. It may include the '*' wildcard at the beginning, at the end or at
        ///     both ends.
        /// </param>
        /// <param name="minLevel">Minimum log level needed to trigger this rule.</param>
        /// <param name="maxLevel">Maximum log level needed to trigger this rule.</param>
        /// <param name="target">Target to be written to when the rule matches.</param>
        public TelemetryLoggingRule(string loggerNamePattern, NLog.LogLevel minLevel, NLog.LogLevel maxLevel, Target target)
            : base(loggerNamePattern, minLevel, maxLevel, target)
        {
        }

        /// <summary>
        ///     Create a new <see cref="T:NLog.Config.LoggingRule" /> with a <paramref name="minLevel" /> which writes to
        ///     <paramref name="target" />.
        /// </summary>
        /// <param name="loggerNamePattern">
        ///     Logger name pattern. It may include the '*' wildcard at the beginning, at the end or at
        ///     both ends.
        /// </param>
        /// <param name="minLevel">Minimum log level needed to trigger this rule.</param>
        /// <param name="target">Target to be written to when the rule matches.</param>
        public TelemetryLoggingRule(string loggerNamePattern, NLog.LogLevel minLevel, Target target) : base(loggerNamePattern, minLevel, target)
        {
        }

        /// <summary>
        ///     Create a (disabled) <see cref="T:NLog.Config.LoggingRule" />. You should call
        ///     <see cref="M:NLog.Config.LoggingRule.EnableLoggingForLevel(NLog.LogLevel)" /> or see cref="EnableLoggingForLevels"/
        ///     &gt; to enable logging.
        /// </summary>
        /// <param name="loggerNamePattern">
        ///     Logger name pattern. It may include the '*' wildcard at the beginning, at the end or at
        ///     both ends.
        /// </param>
        /// <param name="target">Target to be written to when the rule matches.</param>
        public TelemetryLoggingRule(string loggerNamePattern, Target target) : base(loggerNamePattern, target)
        {
        }

        #endregion
    }
}
