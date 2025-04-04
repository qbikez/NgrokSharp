﻿#pragma warning disable CS1591
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NgrokSharp.PlatformSpecific.Linux
{
    public class PlatformLinux : PlatformStrategy
    {
        public override string BinaryPath => $"{DownloadFolder}ngrok";

        public PlatformLinux(string downloadFolder, ILogger? logger = null) : base(downloadFolder, logger)
        {
        }

        public override async Task RegisterAuthTokenAsync(string authtoken)
        {
            if(_ngrokProcess == null)
            {
                if (!File.Exists(ConfigFile))
                {
                    await File.WriteAllTextAsync(ConfigFile, "authtoken:");
                }
                using var registerProcess = new Process
                {
                    StartInfo = new ProcessStartInfo()
                    {
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        FileName = BinaryPath,
                        Arguments = $"config add-authtoken {authtoken}"
                    }
                };
                registerProcess.Start();
                await registerProcess.WaitForExitAsync();
            }
            else
            {
                throw new Exception("The Ngrok process is already running. Please use StopNgrok() and then register the AuthToken again.");
            }
        }

        public override void StartNgrok(string region)
        {
            if(_ngrokProcess == null)
            {
                _ngrokProcess = new Process();
                var startInfo = new ProcessStartInfo
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    FileName = BinaryPath,
                    Arguments = $"start --none --region {region}"
                };
                try
                {
                    _ngrokProcess.StartInfo = startInfo;
                }
                catch (InvalidOperationException e)
                {
                    if (e.Message == "Process is already associated with a real process, so the requested operation cannot be performed.")
                    {
                        _ngrokProcess = new Process {StartInfo = startInfo};
                    }
                }
                _ngrokProcess.Start();
            }
            else
            {
                throw new Exception("The Ngrok process is already running. Please use StopNgrok() and then StartNgrok again.");
            }
        }

        public override void StartNgrokWithLogging(string region)
        {
            if(_ngrokProcess == null)
            {
                _ngrokProcess = new Process();
                var startInfo = new ProcessStartInfo
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    FileName = BinaryPath,
                    Arguments = $"start --none --region {region} --log=stdout"
                };
                try
                {
                    _ngrokProcess.StartInfo = startInfo;
                }
                catch (InvalidOperationException e)
                {
                    if (e.Message == "Process is already associated with a real process, so the requested operation cannot be performed.")
                    {
                        _ngrokProcess = new Process();
                        _ngrokProcess.StartInfo = startInfo;
                    }
                }
                _ngrokProcess.Start();
                
                _ngrokProcess.OutputDataReceived += ProcessStandardOutput;
                _ngrokProcess.ErrorDataReceived += ProcessStandardError;
                _ngrokProcess.Start();
                _ngrokProcess.BeginOutputReadLine();
                _ngrokProcess.BeginErrorReadLine();
            }
            else
            {
                throw new Exception("The Ngrok process is already running. Please use StopNgrok() and then StartNgrok again.");
            }
        }

        public override void SetExecutionBit(string path)
        {
            // chown +x ngrok
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"chmod +x {path}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            process.WaitForExit();
        }
    }
}