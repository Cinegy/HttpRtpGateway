using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Elasticsearch.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using NLog.Common;
using NLog.Config;
using NLog.Layouts;
using NLog.Targets;

namespace HttpRtpGateway.Logging
{
    [Target("ElasticSearch")]
    public class ElasticSearchTarget : TargetWithLayout
    {
        #region Static members

        public static string GetConnectionString(string name)
        {
            var value = GetEnvironmentVariable(name);
            if (!string.IsNullOrEmpty(value))
                return value;

            var connectionString = ConfigurationManager.ConnectionStrings[name];
            return connectionString?.ConnectionString;
        }

        private static string GetEnvironmentVariable(string name)
        {
            return string.IsNullOrEmpty(name) ? null : Environment.GetEnvironmentVariable(name);
        }

        #endregion

        private IElasticLowLevelClient _client;

        #region Constructors

        public ElasticSearchTarget()
        {
            Name = "ElasticSearch";
            Uri = "http://localhost:9200";
            DocumentType = "logevent";
            Index = "httprtpgateway-${date:format=yyyy-MM-dd}";
        }

        #endregion

        #region Properties

        /// <summary>
        ///     Gets or sets a connection string name to retrieve the Url from.
        ///     Use as an alternative to Url
        /// </summary>
        public string ConnectionStringName { get; set; }

        /// <summary>
        ///     Gets or sets the document type for the elasticsearch index.
        /// </summary>
        [RequiredParameter]
        public Layout DocumentType { get; set; }

        /// <summary>
        ///     Gets or sets an alertnative serializer for the elasticsearch client to use.
        /// </summary>
        public IElasticsearchSerializer ElasticsearchSerializer { get; set; }

        /// <summary>
        ///     Gets or sets the name of the elasticsearch index to write to.
        /// </summary>
        public Layout Index { get; set; }

        /// <summary>
        ///     Password for basic auth
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        ///     Set it to true if ElasticSearch uses BasicAuth
        /// </summary>
        public bool RequireAuth { get; set; }

        /// <summary>
        ///     Gets or sets the elasticsearch uri, can be multiple comma separated.
        /// </summary>
        public string Uri { get; set; }

        /// <summary>
        ///     Username for basic auth
        /// </summary>
        public string Username { get; set; }

        #endregion

        #region Override members

        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            var uri = GetConnectionString(ConnectionStringName) ?? Uri;
            var nodes = uri.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(url => new Uri(url));
            var connectionPool = new StaticConnectionPool(nodes);

            var config = new ConnectionConfiguration(connectionPool);

            if (RequireAuth)
            {
                config.BasicAuthentication(Username, Password);
            }

            if (ElasticsearchSerializer != null)
                config = new ConnectionConfiguration(connectionPool, _ => ElasticsearchSerializer);

            _client = new ElasticLowLevelClient(config);
        }

        protected override void Write(AsyncLogEventInfo logEvent)
        {
            Write(new[] { logEvent });
        }

        protected override void Write(AsyncLogEventInfo[] logEvents)
        {
            SendBatch(logEvents);
        }

        #endregion

        #region Members

        private List<object> FormPayload(IEnumerable<LogEventInfo> logEvents)
        {
            var payload = new List<object>();

            foreach (var logEvent in logEvents)
            {
                var rendered = Layout.Render(logEvent);
                var index = Index.Render(logEvent).ToLowerInvariant();
                var type = DocumentType.Render(logEvent);

                payload.Add(new
                {
                    index = new
                    {
                        _index = index,
                        _type = type,
                    }
                });

                var parsedObject = Parse((JObject)JsonConvert.DeserializeObject(rendered));
                payload.Add(parsedObject);
            }

            return payload;
        }

        Dictionary<string, object> Parse(JObject obj)
        {
            var dictionary = obj.ToObject<Dictionary<string, object>>();

            foreach (var key in dictionary.Keys.ToArray())
            {
                var value = dictionary[key];
                var jObject = value as JObject;
                if (jObject != null) dictionary[key] = Parse(jObject);
            }

            return dictionary;
        }

        private void SendBatch(IEnumerable<AsyncLogEventInfo> events)
        {
            try
            {
                var logEvents = events.Select(e => e.LogEvent);

                var payload = FormPayload(logEvents);

                var result = _client.Bulk<byte[]>(payload);

                if (result.Success)
                    return;

                InternalLogger.Error("Failed to send log messages to elasticsearch: status={0}, message=\"{1}\"",
                                     result.HttpStatusCode,
                                     result.OriginalException?.Message ?? "No error message. Enable Trace logging for more information.");
                InternalLogger.Trace("Failed to send log messages to elasticsearch: result={0}", result);

                if (result.OriginalException != null)
                    throw result.OriginalException;
            }
            catch (Exception ex)
            {
                InternalLogger.Error("Error while sending log messages to elasticsearch: message=\"{0}\"", ex.Message);
            }
        }

        #endregion
    }
}
