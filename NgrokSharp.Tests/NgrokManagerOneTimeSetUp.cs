using System;
using System.Collections;
using System.Net;

namespace NgrokSharp.Tests
{
    public class NgrokManagerOneTimeSetUp : IDisposable
    {
        
        public string environmentVariableNgrokYml;

        public readonly byte[] ngrokBytes;

        public NgrokManagerOneTimeSetUp()
        {
            var webClient = new WebClient();

            var ngrokUrl = NgrokManager.GetDownloadUrl();
            ngrokBytes = webClient.DownloadData(ngrokUrl);
            
            environmentVariableNgrokYml =
                Environment.GetEnvironmentVariable("NGROKYML_TOKEN");
        }

        public void Dispose()
        {
        }
    }
}