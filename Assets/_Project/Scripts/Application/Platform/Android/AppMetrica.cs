using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Io.AppMetrica;
using Maze.Core.Common;

namespace Maze.Application.Platform
{
    /// <summary>
    /// Game events to AppMetrica (the Android build): the event's name and its parameters as one JSON object. Without
    /// an API key (<see cref="RuStoreKeys.AppMetricaApiKey"/>) it stays off; the log sink keeps writing the events.
    /// Sessions and crashes are AppMetrica's own (automatic).
    /// </summary>
    public sealed class AppMetricaSink : IAnalyticsSink
    {
        private readonly StringBuilder _json = new StringBuilder(256);
        private bool _active;

        public string Name => "AppMetrica";

        public UniTask InitializeAsync(CancellationToken cancellation)
        {
            var key = RuStoreKeys.AppMetricaApiKey.Trim();
            if (key.Length == 0)
            {
                GameLog.Info(LogChannel.Analytics, "AppMetrica: no API key (RuStoreKeys) — off.");
                return UniTask.CompletedTask;
            }

            AppMetrica.Activate(new AppMetricaConfig(key)
            {
                // The first version in the store: a first launch is an install, not an update.
                FirstActivationAsUpdate = false,
                CrashReporting = true,
                NativeCrashReporting = true,
                LocationTracking = false,
                Logs = UnityEngine.Debug.isDebugBuild,
            });
            _active = true;
            return UniTask.CompletedTask;
        }

        public void Send(string eventName, IReadOnlyDictionary<string, object> parameters)
        {
            if (!_active) return;
            if (parameters.Count == 0) AppMetrica.ReportEvent(eventName);
            else AppMetrica.ReportEvent(eventName, ToJson(parameters, _json));
        }

        /// <summary>Parameters as a flat JSON object: strings quoted and escaped, numbers invariant, bools true/false.</summary>
        public static string ToJson(IReadOnlyDictionary<string, object> parameters, StringBuilder buffer = null)
        {
            var json = buffer ?? new StringBuilder(256);
            json.Clear().Append('{');
            var first = true;
            foreach (var pair in parameters)
            {
                if (!first) json.Append(',');
                first = false;
                AppendString(json, pair.Key);
                json.Append(':');
                AppendValue(json, pair.Value);
            }

            return json.Append('}').ToString();
        }

        private static void AppendValue(StringBuilder json, object value)
        {
            switch (value)
            {
                case null:
                    json.Append("null");
                    break;
                case bool b:
                    json.Append(b ? "true" : "false");
                    break;
                case float f:
                    AppendNumber(json, f);
                    break;
                case double d:
                    AppendNumber(json, d);
                    break;
                case int or long or short or byte or uint or ulong:
                    json.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                    break;
                default:
                    AppendString(json, Convert.ToString(value, CultureInfo.InvariantCulture));
                    break;
            }
        }

        private static void AppendNumber(StringBuilder json, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) json.Append("null");
            else json.Append(value.ToString("0.###", CultureInfo.InvariantCulture));
        }

        private static void AppendString(StringBuilder json, string text)
        {
            json.Append('"');
            foreach (var c in text ?? "")
            {
                switch (c)
                {
                    case '"': json.Append("\\\""); break;
                    case '\\': json.Append("\\\\"); break;
                    case '\n': json.Append("\\n"); break;
                    case '\r': json.Append("\\r"); break;
                    case '\t': json.Append("\\t"); break;
                    default:
                        if (c < ' ') json.Append("\\u").Append(((int)c).ToString("x4"));
                        else json.Append(c);
                        break;
                }
            }

            json.Append('"');
        }
    }
}
