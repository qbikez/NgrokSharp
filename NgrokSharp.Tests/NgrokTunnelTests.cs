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
    public class NgrokTunnelTests : IDisposable
    {
        private readonly string _ngrokYml = @"
version: ""2""
authtoken: """"
";
        private readonly string _downloadFolder = NgrokManager.DefaultDownloadFolder();
        public static readonly HttpClient HttpClient = new HttpClient();
        ILogger<NgrokManagerUnitTest> _logger;
        private NgrokManager ngrokManager;

        public NgrokTunnelTests()
        {
            EnsureNgrokBinary();

            var loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.AddFile("app.log");
                });

            ngrokManager = new NgrokManager(loggerFactory.CreateLogger<NgrokManagerUnitTest>());

            ngrokManager.StartNgrokWithLogging();
            ngrokManager.WaitForNgrok().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            ngrokManager?.Dispose();

            foreach (var process in Process.GetProcessesByName("ngrok")) process.Kill();
            //Because ngrok is only downloaded once in NgrokManagerOneTimeSetUp.
            //The File.WriteAllBytes method, can sometimes fail due killing the process and writing a new one, due to slow IO on some systems.
            //Even though I don't like it. It is a trade off between downloading ngrok every test or handling slow IO on some systems.
            //I choose to handle slow IO, and not download in every test. That is why the sleep is need here!  
            Thread.Sleep(100);
        }

        [Fact]
        public async Task StartTunnel_StartTunnel8080_True()
        {
            var startTunnelDto = new StartTunnelDTO
            {
                name = "foundryvtt",
                proto = "http",
                addr = "30000",

            };
            await ngrokManager.StartTunnelAsync(startTunnelDto);
            var downloadedString = await HttpClient.GetStringAsync("http://localhost:4040/api/tunnels/foundryvtt");


            Assert.Contains("http://localhost:30000", downloadedString);
        }

        [Fact]
        /// Requires valid API key
        public async Task StartTunnel_UseSubDomainGuid_True()
        {
            var startTunnelDto = new StartTunnelDTO
            {
                name = "foundryvtt",
                proto = "http",
                addr = "30000",
                subdomain = Guid.NewGuid().ToString()
            };
            await ngrokManager.StartTunnelAsync(startTunnelDto);

            var downloadedString = await HttpClient.GetStringAsync("http://localhost:4040/api/tunnels/foundryvtt");
            Assert.Contains(startTunnelDto.subdomain, downloadedString);
        }

        [Fact]
        /// Requires valid API key
        public async Task StartTunnel_WithCustomDomain_True()
        {
            var startTunnelDto = new StartTunnelDTO
            {
                name = "foundryvtt",
                proto = "http",
                addr = "30000",
                hostname = "ngroksharp.davidjensen.dev"
            };
            await ngrokManager.StartTunnelAsync(startTunnelDto);

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
            EnsureNgrokBinary();
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

            // ASSERT
            var tunnelDetail =
                JsonSerializer.Deserialize<TunnelDetailDTO>(
                    await httpResponseMessage.Content.ReadAsStringAsync());

            // seems like ngrok v3 doesn't use regional infixes anymore
            // Assert.Contains($".{regionNameShort}.", tunnelDetail.PublicUrl.ToString());
            Assert.NotEmpty(tunnelDetail.PublicUrl.ToString());
        }

        private DirectoryInfo SetNgrokYml()
        {
            var path = Directory.CreateDirectory(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ngrok2"));

            File.WriteAllText($"{path.FullName + Path.DirectorySeparatorChar}ngrok.yml", _ngrokYml);

            return path;
        }


        [Fact]
        public async Task StartTunnel_MissingAddrArgumentNullException_True()
        {
            // ARRANGE
            EnsureNgrokBinary();
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
            var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () => await ngrokManager.StartTunnelAsync(null));

            // ASSERT

            Assert.Equal("Value cannot be null. (Parameter 'startTunnelDto')", ex.Message);
        }

        [Fact]
        public async Task RegisterAuthToken_ThrowsExptionUsingRegisterAuthTokenWhileAlreadyStarted_True()
        {
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
            // ACT
            ngrokManager.StopNgrok();

            var token = Guid.NewGuid().ToString();
            await ngrokManager.RegisterAuthTokenAsync(token);

            using var platform = PlatformStrategy.Create(_downloadFolder);
            var lines = File.ReadAllLines(platform.ConfigFile);

            Assert.Contains($"authtoken: {token}", lines);
        }

        [Fact]
        public async Task StopTunnel_StopATunnelThatIsRunning_True()
        {
            var startTunnelDto = new StartTunnelDTO
            {
                name = "foundryvtt",
                proto = "http",
                addr = "30000",

            };

            await ngrokManager.StartTunnelAsync(startTunnelDto);

            // ACT
            var stopTunnel = await ngrokManager.StopTunnelAsync("foundryvtt");

            // ASSERT
            Assert.Equal(HttpStatusCode.NoContent, stopTunnel.StatusCode); // Should return 204 status code with no content
        }

        [Fact]
        public async Task StopTunnel_StopTunnelNameIsNullArgumentNullException_True()
        {
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
            var startTunnelDto = new StartTunnelDTO
            {
                name = "foundryvtt",
                proto = "http",
                addr = "30000",

            };

            await ngrokManager.StartTunnelAsync(startTunnelDto);

            string log;

            using (var fileStream = File.Open("app.log", FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (var streamReader = new StreamReader(fileStream, Encoding.Default))
                {
                    log = streamReader.ReadToEnd();
                }
            }

            // ASSERT
            Assert.Contains("client session established", log);
        }

        [Fact]
        public async Task DeleteCapturedRequests_Return204WithNoBody_True()
        {
            var startTunnelDto = new StartTunnelDTO
            {
                name = "foundryvtt",
                proto = "http",
                addr = "30000",

            };

            await ngrokManager.StartTunnelAsync(startTunnelDto);

            // ACT
            var httpResponseMessage = await ngrokManager.DeleteCapturedRequests();

            // ASSERT
            Assert.Equal(HttpStatusCode.NoContent, httpResponseMessage.StatusCode);
        }

        [Fact]
        public async Task ListCapturedRequests_ReturnCapturedRequestRootDTO_True()
        {
            var startTunnelDto = new StartTunnelDTO
            {
                name = "foundryvtt",
                proto = "http",
                addr = "30000",

            };

            await ngrokManager.StartTunnelAsync(startTunnelDto);

            // ACT
            var httpResponseMessage = await ngrokManager.ListCapturedRequests();

            var capturedRequestRootDTO =
                JsonSerializer.Deserialize<CapturedRequestRootDTO>(
                    await httpResponseMessage.Content.ReadAsStringAsync());

            // ASSERT
            Assert.Equal("/api/requests/http", capturedRequestRootDTO.uri);
        }

        private void EnsureNgrokBinary(string? targetFolder = null)
        {
            targetFolder ??= _downloadFolder;
            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }
            
            using var platform = PlatformStrategy.Create(targetFolder);

            if (!File.Exists(platform.BinaryPath))
            {
                var webClient = new WebClient();
                var ngrokBytes = webClient.DownloadData(NgrokManager.GetDownloadUrl());
                File.WriteAllBytes($"{targetFolder}ngrok.zip", ngrokBytes);
                new FastZip().ExtractZip($"{targetFolder}ngrok.zip", targetFolder, null);
                platform.SetExecutionBit(platform.BinaryPath);
            }
            if (File.Exists(platform.ConfigFile)) {
                File.Delete(platform.ConfigFile);
            }
        }
    }
}