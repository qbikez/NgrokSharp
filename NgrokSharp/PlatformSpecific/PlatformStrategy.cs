﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using static System.Environment;
#pragma warning disable CS1591

namespace NgrokSharp.PlatformSpecific
{
    public abstract class PlatformStrategy : IDisposable
    {
        protected Process _ngrokProcess;
        protected ILogger? _logger;
        public string DownloadFolder { get; private init; }
        public abstract string BinaryPath {get; }
        public string ConfigFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ngrok2", "ngrok.yml");

        public abstract Task RegisterAuthTokenAsync(string authtoken);
        public abstract void StartNgrok(string region);
        public abstract void StartNgrokWithLogging(string region);

        public event Action<string> UrlChanged;

        public PlatformStrategy(string downloadFolder, ILogger? logger = null)
        {
            _logger = logger;
            _ngrokProcess = null;
            DownloadFolder = downloadFolder;
        }

        public void StopNgrok()
        {
            if (_ngrokProcess != null)
            {
                _ngrokProcess.Refresh();
                if (!_ngrokProcess.HasExited)
                {
                    _ngrokProcess.Kill();
                    _ngrokProcess.Close();
                }
                _ngrokProcess?.Dispose();
                _ngrokProcess = null;
            }
        }

        public void Dispose() => _ngrokProcess?.Dispose();

        protected void ProcessStandardError(object sender, DataReceivedEventArgs args)
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                _logger?.LogError(args.Data);
            }
        }

        protected void ProcessStandardOutput(object sender, DataReceivedEventArgs args)
        {
            if (args == null || string.IsNullOrWhiteSpace(args.Data))
            {
                return;
            }

            // Build structured log data
            //var data = ParseLogData(args.Data);
            //var logFormatData = data.Where(d => d.Key != "lvl" && d.Key != "t")
            //    .ToDictionary(e => e.Key, e => e.Value);
            //var logFormatString = GetLogFormatString(logFormatData);
            //var logLevel = ParseLogLevel(data["lvl"]);
            var match  = Regex.Match(args.Data, "addr=([^\\s]+)");
            if (match.Success)
            {
                UrlChanged?.Invoke(match.Groups[1].Value);
            }

            _logger?.Log(LogLevel.Debug, args.Data);
        }
        
        private static Dictionary<string, string> ParseLogData(string input)
        {
            var result = new Dictionary<string, string>();
            var stream = new StringReader(input);
            int lastRead = 0;

            while (lastRead > -1)
            {
                // Read Key
                var keyBuilder = new StringBuilder();
                while (true)
                {
                    lastRead = stream.Read();
                    var c = (char)lastRead;
                    if (c == '=')
                    {
                        break;
                    }
                    keyBuilder.Append(c);
                }

                // Read Value
                var valueBuilder = new StringBuilder();
                lastRead = stream.Read();
                var firstValChar = (char)lastRead;
                bool quoteWrapped = false;
                if (firstValChar == '"')
                {
                    quoteWrapped = true;
                    lastRead = stream.Read();
                    valueBuilder.Append((char)lastRead);
                }
                else
                {
                    valueBuilder.Append(firstValChar);
                }
                while (true)
                {
                    lastRead = stream.Read();
                    if (lastRead == -1)
                    {
                        break;
                    }

                    var c = (char)lastRead;
                    if (quoteWrapped && c == '"')
                    {
                        lastRead = stream.Read();
                        break;
                    }
                    if (!quoteWrapped && c == ' ')
                    {
                        break;
                    }
                    valueBuilder.Append(c);
                }

                result[keyBuilder.ToString()] = valueBuilder.ToString();
            }
            return result;
        }

        private static LogLevel ParseLogLevel(string logLevelRaw)
        {
            //if (!string.IsNullOrWhiteSpace(logLevelRaw))
            //{
            //	return LogLevel.Debug;
            //}

            LogLevel logLevel;
            switch (logLevelRaw)
            {
                case "info":
                    logLevel = LogLevel.Information;
                    break;
                default:
                    var parseResult = Enum.TryParse<LogLevel>(logLevelRaw, out logLevel);
                    if (!parseResult)
                    {
                        logLevel = LogLevel.Debug;
                    }
                    break;
            }

            return logLevel;
        }

        private string GetLogFormatString(Dictionary<string, string> logFormatData)
        {
            StringBuilder logFormatSB = new StringBuilder();
            foreach (var kvp in logFormatData)
            {
                logFormatSB.Append(kvp.Key);
                logFormatSB.Append(": {");
                logFormatSB.Append(kvp.Key);
                logFormatSB.Append("} | ");
            }
            var logFormatString = logFormatSB.ToString().TrimEnd(' ').TrimEnd('|').TrimEnd(' ');
            return logFormatString;
        }

        public virtual void SetExecutionBit(string path) {}

        public static PlatformStrategy Create(string downloadFolder, ILogger? logger = null)
        {
            if (OperatingSystem.IsWindows())
            {
                return new Windows.PlatformWindows(downloadFolder, logger);
            }
            else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                return new Linux.PlatformLinux(downloadFolder, logger);
            }
            else
            {
                throw new PlatformNotSupportedException("The current platform is not supported.");
            }
        }
    }
}