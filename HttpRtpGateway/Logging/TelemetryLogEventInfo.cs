using NLog;

namespace HttpRtpGateway.Logging
{

    public class TelemetryLogEventInfo : LogEventInfo
    {
        #region Properties

        public string Key { get; set; }
        public object TelemetryObject { get; set; }

        #endregion
    }
}
