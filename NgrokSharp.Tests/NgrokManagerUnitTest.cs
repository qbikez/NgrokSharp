using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Extensions.Logging;
using NgrokSharp.DTO;
using NgrokSharp.PlatformSpecific;
using Xunit;

namespace NgrokSharp.Tests
{
    public class NgrokManagerUnitTest : IClassFixture<NgrokManagerOneTimeSetUp>, IDisposable
    {
        private readonly byte[] _ngrokBytes;
        private readonly string _ngrokYml = @"
version: 3
authtoken: xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
";
        private readonly string _downloadFolder = NgrokManager.DefaultDownloadFolder();
        public static readonly HttpClient HttpClient = new HttpClient();
        ILogger<NgrokManagerUnitTest> _logger;

        public NgrokManagerUnitTest(NgrokManagerOneTimeSetUp ngrokManagerOneTimeSetUp)
        {
            _ngrokYml = ngrokManagerOneTimeSetUp.environmentVariableNgrokYml;
            _ngrokBytes = ngrokManagerOneTimeSetUp.ngrokBytes;
            if (!Directory.Exists(_downloadFolder))
            {
                Directory.CreateDirectory(_downloadFolder);
            }
        }

        public void Dispose()
        {
            foreach (var process in Process.GetProcessesByName("ngrok")) process.Kill();
            //Because ngrok is only downloaded once in NgrokManagerOneTimeSetUp.
            //The File.WriteAllBytes method, can sometimes fail due killing the process and writing a new one, due to slow IO on some systems.
            //Even though I don't like it. It is a trade off between downloading ngrok every test or handling slow IO on some systems.
            //I choose to handle slow IO, and not download in every test. That is why the sleep is need here!  
            Thread.Sleep(100);
        }

        [Fact]
        public async Task DownloadNgrok_CheckIfNgrokIsDownloaded_True()
        {
            // ARRANGE
            using var ngrokManager = new NgrokManager();

            // ACT

            await ngrokManager.DownloadAndUnzipNgrokAsync();
            // ASSERT

            if (OperatingSystem.IsWindows()) Assert.True(File.Exists($"{_downloadFolder}ngrok.exe"));

            if (OperatingSystem.IsLinux()) Assert.True(File.Exists($"{_downloadFolder}ngrok"));
        }

        [Fact]
        public async Task StartNgrok_ShouldStartNgrok_True()
        {
            // ARRANGE
            ExtractNgrokBinary();
            SetNgrokYml();

            using var ngrokManager = new NgrokManager();
            // ACT
            ngrokManager.StartNgrok();
            await ngrokManager.WaitForNgrok();

            // ASSERT
            var downloadedString = await HttpClient.GetStringAsync("http://localhost:4040/api/");

            Assert.False(string.IsNullOrWhiteSpace(downloadedString));
        }

        [Fact]
        public async Task StartNgrok_SetNgrokDirectory_True()
        {
            // ARRANGE
            var random = new Random();
            var customFolder = $"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + Path.DirectorySeparatorChar}NgrokCus{random.Next().ToString()}{Path.DirectorySeparatorChar}";

            ExtractNgrokBinary(customFolder);

            using var ngrokManager = new NgrokManager();
            // ACT
            ngrokManager.SetNgrokDirectory(customFolder);
            ngrokManager.StartNgrok();
            await ngrokManager.WaitForNgrok();

            // ASSERT
            var downloadedString = await HttpClient.GetStringAsync("http://localhost:4040/api/");

            Assert.False(string.IsNullOrWhiteSpace(downloadedString));
        }

        [Fact]
        public async Task StartTunnel_StartTunnel8080_True()
        {
            // ARRANGE
            ExtractNgrokBinary();
            SetNgrokYml();

            var loggerFactory = LoggerFactory.Create(builder =>
                            {
                                builder.AddFile("app.log");
                            }
                        );

            using var ngrokManager = new NgrokManager(loggerFactory.CreateLogger<NgrokManagerUnitTest>());
            // ACT
            ngrokManager.StartNgrokWithLogging();
            await ngrokManager.WaitForNgrok();

            var startTunnelDto = new StartTunnelDTO
            {
                name = "foundryvtt",
                proto = "http",
                addr = "30000",
                
            };

            await ngrokManager.StartTunnelAsync(startTunnelDto);

            // ASSERT
            var downloadedString = await HttpClient.GetStringAsync("http://localhost:4040/api/tunnels/foundryvtt");

            Assert.Contains("http://localhost:30000", downloadedString);
        }

        [Fact]
        public async Task StartTunnel_UseSubDomainGuid_True()
        {
            // ARRANGE
            if (!Directory.Exists(_downloadFolder))
            {
                Directory.CreateDirectory(_downloadFolder);
            }
            File.WriteAllBytes($"{_downloadFolder}ngrok-stable-amd64.zip", _ngrokBytes);

            var fastZip = new FastZip();
            fastZip.ExtractZip($"{_downloadFolder}ngrok-stable-amd64.zip", _downloadFolder, null);

            SetNgrokYml();

            var newGuid = Guid.NewGuid().ToString();

            using var ngrokManager = new NgrokManager();
            // ACT
            ngrokManager.StartNgrok();
            await ngrokManager.WaitForNgrok();

            var startTunnelDto = new StartTunnelDTO
            {
                name = "foundryvtt",
                proto = "http",
                addr = "30000",
                subdomain = newGuid
            };

            await ngrokManager.StartTunnelAsync(startTunnelDto);

            // ASSERT
            var downloadedString = await HttpClient.GetStringAsync("http://localhost:4040/api/tunnels/foundryvtt");

            Assert.Contains(newGuid, downloadedString);
        }

        [Fact]
        public async Task StartTunnel_WithCustomDomain_True()
        {
            // ARRANGE
            if (!Directory.Exists(_downloadFolder))
            {
                Directory.CreateDirectory(_downloadFolder);
            }
            File.WriteAllBytes($"{_downloadFolder}ngrok-stable-amd64.zip", _ngrokBytes);

            var fastZip = new FastZip();
            fastZip.ExtractZip($"{_downloadFolder}ngrok-stable-amd64.zip", _downloadFolder, null);

            SetNgrokYml();

            using var ngrokManager = new NgrokManager();
            // ACT
            ngrokManager.StartNgrok();
            await ngrokManager.WaitForNgrok();

            var startTunnelDto = new StartTunnelDTO
            {
                name = "foundryvtt",
                proto = "http",
                addr = "30000",
                hostname = "ngroksharp.davidjensen.dev"
            };

            await ngrokManager.StartTunnelAsync(startTunnelDto);

            // ASSERT
            var downloadedString = await HttpClient.GetStringAsync("http://localhost:4040/api/tunnels/foundryvtt");

            Assert.Contains("ngroksharp.davidjensen.dev", downloadedString);
        }

        [Theory]
        [InlineData("eu", "Europe")]
        [InlineData("ap", "AsiaPacific")]
        [InlineData("au", "Australia")]
        [InlineData("sa", "SouthAmerica")]
        [InlineData("jp", "Japan")]
        [InlineData("in", "India")]
        public async Task StartTunnel_TestOptionalRegions_True(string regionNameShort, string regionNameFull)
        {
            // ARRANGE
            ExtractNgrokBinary();
            SetNgrokYml();

            var region = (NgrokManager.Region)Enum.Parse(typeof(NgrokManager.Region), regionNameFull, true);

            using var ngrokManager = new NgrokManager();
            // ACT
            ngrokManager.StartNgrok(region);
            await ngrokManager.WaitForNgrok();

            var startTunnelDto = new StartTunnelDTO
            {
                name = "test",
                proto = "http",
                addr = "30000",
                
            };


            var httpResponseMessage = await ngrokManager.StartTunnelAsync(startTunnelDto);
            //Wait for ngrok to start, it can be slow on some systems.
            Thread.Sleep(1000);

            // ASSERT
            var tunnelDetail =
                JsonSerializer.Deserialize<TunnelDetailDTO>(
                    await httpResponseMessage.Content.ReadAsStringAsync());

            Assert.Contains($".{regionNameShort}.", tunnelDetail.PublicUrl.ToString());
        }

        private DirectoryInfo SetNgrokYml()
        {
            var path = Directory.CreateDirectory(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ngrok2"));

            //File.WriteAllText($"{path.FullName+Path.DirectorySeparatorChar}ngrok.yml", _ngrokYml);

            return path;
        }


        [Fact]
        public async Task StartTunnel_MissingAddrArgumentNullException_True()
        {
            // ARRANGE
            ExtractNgrokBinary();
            SetNgrokYml();

            using var ngrokManager = new NgrokManager();
            // ACT
            ngrokManager.StartNgrok();
            await ngrokManager.WaitForNgrok();

            var startTunnelDto = new StartTunnelDTO
            {
                name = "foundryvtt",
                proto = "http",
                addr = "",
                
            };

            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await ngrokManager.StartTunnelAsync(startTunnelDto));

            // ASSERT
            Assert.Equal("Value cannot be null or whitespace. (Parameter 'addr')", ex.Message);
        }

        [Fact]
        public async Task StartTunnel_MissingNameArgumentNullException_True()
        {
            // ARRANGE
            if (!Directory.Exists(_downloadFolder))
            {
                Directory.CreateDirectory(_downloadFolder);
            }
            File.WriteAllBytes($"{_downloadFolder}ngrok-stable-amd64.zip", _ngrokBytes);

            var fastZip = new FastZip();
            fastZip.ExtractZip($"{_downloadFolder}ngrok-stable-amd64.zip", _downloadFolder, null);

            SetNgrokYml();

            using var ngrokManager = new NgrokManager();
            // ACT
            ngrokManager.StartNgrok();
            await ngrokManager.WaitForNgrok();

            var startTunnelDto = new StartTunnelDTO
            {
                name = "",
                proto = "http",
                addr = "8080"
            };

            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await ngrokManager.StartTunnelAsync(startTunnelDto));

            // ASSERT

            Assert.Equal("Value cannot be null or whitespace. (Parameter 'name')", ex.Message);
        }

        [Fact]
        public async Task StartTunnel_MissingProtoArgumentNullException_True()
        {
            // ARRANGE
            if (!Directory.Exists(_downloadFolder))
            {
                Directory.CreateDirectory(_downloadFolder);
            }
            File.WriteAllBytes($"{_downloadFolder}ngrok-stable-amd64.zip", _ngrokBytes);

            var fastZip = new FastZip();
            fastZip.ExtractZip($"{_downloadFolder}ngrok-stable-amd64.zip", _downloadFolder, null);

            SetNgrokYml();

            using var ngrokManager = new NgrokManager();
            // ACT
            ngrokManager.StartNgrok();
            await ngrokManager.WaitForNgrok();

            var startTunnelDto = new StartTunnelDTO
            {
                name = "foundryvtt",
                proto = "",
                addr = "8080",
                
            };

            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await ngrokManager.StartTunnelAsync(startTunnelDto));

            // ASSERT

            Assert.Equal("Value cannot be null or whitespace. (Parameter 'proto')", ex.Message);
        }

        [Fact]
        public async Task StartTunnel_StartTunnelDTOIsNullArgumentNullException_True()
        {
            // ARRANGE
            if (!Directory.Exists(_downloadFolder))
            {
                Directory.CreateDirectory(_downloadFolder);
            }
            File.WriteAllBytes($"{_downloadFolder}ngrok-stable-amd64.zip", _ngrokBytes);

            var fastZip = new FastZip();
            fastZip.ExtractZip($"{_downloadFolder}ngrok-stable-amd64.zip", _downloadFolder, null);

            SetNgrokYml();

            using var ngrokManager = new NgrokManager();
            // ACT
            ngrokManager.StartNgrok();
            await ngrokManager.WaitForNgrok();

            var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () => await ngrokManager.StartTunnelAsync(null));

            // ASSERT

            Assert.Equal("Value cannot be null. (Parameter 'startTunnelDto')", ex.Message);
        }

        [Fact]
        public async Task RegisterAuthToken_ThrowsExptionUsingRegisterAuthTokenWhileAlreadyStarted_True()
        {
            // ARRANGE
            if (!Directory.Exists(_downloadFolder))
            {
                Directory.CreateDirectory(_downloadFolder);
            }
            File.WriteAllBytes($"{_downloadFolder}ngrok-stable-amd64.zip", _ngrokBytes);

            var fastZip = new FastZip();
            fastZip.ExtractZip($"{_downloadFolder}ngrok-stable-amd64.zip", _downloadFolder, null);

            SetNgrokYml();

            using var ngrokManager = new NgrokManager();
            // ACT

            ngrokManager.StartNgrok();
            await ngrokManager.WaitForNgrok();

            // ASSERT
            var ex = await Assert.ThrowsAsync<Exception>(async () =>
                await ngrokManager.RegisterAuthTokenAsync("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"));

            Assert.Equal(
                "The Ngrok process is already running. Please use StopNgrok() and then register the AuthToken again.",
                ex.Message);
        }

        [Fact]
        public async Task RegisterAuthToken_AddNewAuthTokenAfterStop_True()
        {
            // ARRANGE
            ExtractNgrokBinary();
            DirectoryInfo path = SetNgrokYml();

            using var ngrokManager = new NgrokManager();
            ngrokManager.StartNgrok();
            await ngrokManager.WaitForNgrok();

            // ACT
            ngrokManager.StopNgrok();
            //Wait for ngrok to stop, it can be slow on some systems.
            Thread.Sleep(1000);

            await ngrokManager.RegisterAuthTokenAsync("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx");

            // ASSERT
            Thread.Sleep(TimeSpan.FromSeconds(1)); // wait for the ngrok process to start and write the file

            string acualNgrokYml = null;

            if (OperatingSystem.IsWindows()) acualNgrokYml = File.ReadAllText($"{path.FullName + Path.DirectorySeparatorChar}ngrok.yml");

            if (OperatingSystem.IsLinux()) acualNgrokYml = File.ReadAllText($"{path.FullName + Path.DirectorySeparatorChar}ngrok.yml");

            Assert.Equal("authtoken: xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx\n", acualNgrokYml);
        }

        [Fact]
        public async Task StopTunnel_StopATunnelThatIsRunning_True()
        {
            // ARRANGE
            ExtractNgrokBinary();
            SetNgrokYml();

            using var ngrokManager = new NgrokManager();

            ngrokManager.StartNgrok();
            await ngrokManager.WaitForNgrok();

            var startTunnelDto = new StartTunnelDTO
            {
                name = "foundryvtt",
                proto = "http",
                addr = "30000",
                
            };

            await ngrokManager.StartTunnelAsync(startTunnelDto);
            //Wait for ngrok to start, it can be slow on some systems.
            Thread.Sleep(1000);

            // ACT
            var stopTunnel = await ngrokManager.StopTunnelAsync("foundryvtt");

            // ASSERT
            Assert.Equal(HttpStatusCode.NoContent,
                stopTunnel.StatusCode); // Should return 204 status code with no content
        }

        [Fact]
        public async Task StopTunnel_StopTunnelNameIsNullArgumentNullException_True()
        {
            // ARRANGE
            ExtractNgrokBinary();
            SetNgrokYml();

            using var ngrokManager = new NgrokManager();

            ngrokManager.StartNgrok();
            await ngrokManager.WaitForNgrok();

            var startTunnelDto = new StartTunnelDTO
            {
                name = "foundryvtt",
                proto = "http",
                addr = "30000",
                
            };
            await ngrokManager.StartTunnelAsync(startTunnelDto);
            // ACT

            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await ngrokManager.StopTunnelAsync(""));
            // ASSERT

            Assert.Equal("Value cannot be null or whitespace. (Parameter 'name')", ex.Message);
        }

        [Fact]
        public async Task ListTunnels_StartTunnel8080AndCheckTheList_True()
        {
            // ARRANGE
            ExtractNgrokBinary();
            //SetNgrokYml();

            var loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.AddFile("app.log");
                }
            );

            using var ngrokManager = new NgrokManager(loggerFactory.CreateLogger<NgrokManagerUnitTest>());

            ngrokManager.StartNgrokWithLogging();
            await ngrokManager.WaitForNgrok(TimeSpan.FromSeconds(2));

            var startTunnelDto = new StartTunnelDTO
            {
                name = "foundryvtt",
                proto = "http",
                addr = "30000"
            };

            var resp = await ngrokManager.StartTunnelAsync(startTunnelDto);
            var content = await resp.Content.ReadAsStringAsync();
            resp.EnsureSuccessStatusCode();

            // ACT
            var httpResponseMessage = await ngrokManager.ListTunnelsAsync();

            var tunnelDetail =
                JsonSerializer.Deserialize<TunnelsDetailsDTO>(
                    await httpResponseMessage.Content.ReadAsStringAsync());

            // ASSERT
            Assert.NotEmpty(tunnelDetail.Tunnels);
            Assert.Equal("foundryvtt", tunnelDetail.Tunnels[0].Name);
        }

        [Fact]
        public async Task StartNgrokWithLogging_StartTunnel8080AndCheckLog_True()
        {
            // ARRANGE
            var are = new AutoResetEvent(false);
            if (!Directory.Exists(_downloadFolder))
            {
                Directory.CreateDirectory(_downloadFolder);
            }

            var loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.AddFile("app.log");
                }
            );

            _logger = loggerFactory.CreateLogger<NgrokManagerUnitTest>();

            File.WriteAllBytes($"{_downloadFolder}ngrok-stable-amd64.zip", _ngrokBytes);

            var fastZip = new FastZip();
            fastZip.ExtractZip($"{_downloadFolder}ngrok-stable-amd64.zip", _downloadFolder, null);

            SetNgrokYml();

            var ngrokManager = new NgrokManager(_logger);

            ngrokManager.StartNgrokWithLogging();
            await ngrokManager.WaitForNgrok();

            var startTunnelDto = new StartTunnelDTO
            {
                name = "foundryvtt",
                proto = "http",
                addr = "30000",
                
            };

            await ngrokManager.StartTunnelAsync(startTunnelDto);
            //Wait for ngrok to start, it can be slow on some systems.
            await Task.Delay(1000);

            string log;

            using (var fileStream = File.Open("app.log", FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (var streamReader = new StreamReader(fileStream, Encoding.Default))
                {
                    log = streamReader.ReadToEnd();
                }
            }

            // ACT
            are.WaitOne(TimeSpan.FromSeconds(4));

            // ASSERT
            Assert.Contains("client session established", log);
        }

        [Fact]
        public async Task DeleteCapturedRequests_Return204WithNoBody_True()
        {
            // ARRANGE
            var are = new AutoResetEvent(false);
            if (!Directory.Exists(_downloadFolder))
            {
                Directory.CreateDirectory(_downloadFolder);
            }
            File.WriteAllBytes($"{_downloadFolder}ngrok-stable-amd64.zip", _ngrokBytes);

            var fastZip = new FastZip();
            fastZip.ExtractZip($"{_downloadFolder}ngrok-stable-amd64.zip", _downloadFolder, null);

            SetNgrokYml();

            using var ngrokManager = new NgrokManager();

            ngrokManager.StartNgrok();
            await ngrokManager.WaitForNgrok();

            var startTunnelDto = new StartTunnelDTO
            {
                name = "foundryvtt",
                proto = "http",
                addr = "30000",
                
            };

            await ngrokManager.StartTunnelAsync(startTunnelDto);
            //Wait for ngrok to start, it can be slow on some systems.
            Thread.Sleep(1000);

            // ACT
            are.WaitOne(TimeSpan.FromSeconds(1));
            var httpResponseMessage = await ngrokManager.DeleteCapturedRequests();

            // ASSERT
            Assert.Equal(HttpStatusCode.NoContent, httpResponseMessage.StatusCode);
        }

        [Fact]
        public async Task ListCapturedRequests_ReturnCapturedRequestRootDTO_True()
        {
            // ARRANGE
            var are = new AutoResetEvent(false);
            if (!Directory.Exists(_downloadFolder))
            {
                Directory.CreateDirectory(_downloadFolder);
            }
            File.WriteAllBytes($"{_downloadFolder}ngrok-stable-amd64.zip", _ngrokBytes);

            var fastZip = new FastZip();
            fastZip.ExtractZip($"{_downloadFolder}ngrok-stable-amd64.zip", _downloadFolder, null);

            SetNgrokYml();

            using var ngrokManager = new NgrokManager();

            ngrokManager.StartNgrok();
            await ngrokManager.WaitForNgrok();

            var startTunnelDto = new StartTunnelDTO
            {
                name = "foundryvtt",
                proto = "http",
                addr = "30000",
                
            };

            await ngrokManager.StartTunnelAsync(startTunnelDto);
            //Wait for ngrok to start, it can be slow on some systems.
            Thread.Sleep(1000);

            // ACT
            are.WaitOne(TimeSpan.FromSeconds(1));
            var httpResponseMessage = await ngrokManager.ListCapturedRequests();

            var capturedRequestRootDTO =
                JsonSerializer.Deserialize<CapturedRequestRootDTO>(
                    await httpResponseMessage.Content.ReadAsStringAsync());

            // ASSERT
            Assert.Equal("/api/requests/http", capturedRequestRootDTO.uri);
        }

        private void ExtractNgrokBinary(string? targetFolder = null)
        {
            targetFolder ??= _downloadFolder;

            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }
            File.WriteAllBytes($"{targetFolder}ngrok-stable-amd64.zip", _ngrokBytes);

            var fastZip = new FastZip();
            fastZip.ExtractZip($"{targetFolder}ngrok-stable-amd64.zip", targetFolder, null);

            var platform = PlatformStrategy.Create(targetFolder);
            platform.SetExecutionBit($"{targetFolder}ngrok");
        }
    }
}