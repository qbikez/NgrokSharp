[![Contributors][contributors-shield]][contributors-url]
[![Forks][forks-shield]][forks-url]
[![Stargazers][stars-shield]][stars-url]
[![Issues][issues-shield]][issues-url]
[![MIT License][license-shield]][license-url]
[![Coverage][coverage-shield]][coverage-url]
[![Discord][discord-shield]][discord-url]



<!-- PROJECT LOGO 
<br />
<p align="center">
  <a href="https://github.com/entvex/NgrokSharp">
    <img src="images/logo.png" alt="Logo" width="80" height="80">
  </a>
-->
<h3 align="center">NgrokSharp</h3>

  <p align="center">
    A dotnet library for ngrok.
    <br />
    <a href="https://entvex.github.io/NgrokSharp"><strong>Explore the docs »</strong></a>
    <br />
    <br />
    <a href="https://github.com/entvex/NgrokSharp/issues">Report Bug</a>
    ·
    <a href="https://github.com/entvex/NgrokSharp/issues">Request Feature</a>
  </p>
</p>

<!-- TABLE OF CONTENTS -->
<details open="open">
  <summary><h2 style="display: inline-block">Table of Contents</h2></summary>
  <ol>
<!-- 
    <li>
      <a href="#about-the-project">About The Project</a>
      <ul>
        <li><a href="#built-with">Built With</a></li>
      </ul>
    </li>
-->
    <li>
      <a href="#getting-started">Getting Started</a>
      <ul>
        <li><a href="#installation">Installation</a></li>
      </ul>
    </li>
    <li><a href="#usage">Usage</a></li>
    <li><a href="#roadmap">Roadmap</a></li>
    <li><a href="#contributing">Contributing</a></li>
    <li><a href="#license">License</a></li>
    <li><a href="#acknowledgements">Acknowledgements</a></li>
  </ol>
</details>

<!-- ABOUT THE PROJECT -->
## About The Project

### Features

* Easy to use API
* Downloads the correct Ngrok version for your platform
* Supports different logging providers by implementing Microsoft.Extensions.Logging

<!-- GETTING STARTED -->
## Getting Started

### Installation

Install via [nuget.org](https://www.nuget.org/packages/NgrokSharp/)

<!-- USAGE EXAMPLES -->
## Usage

Code example
```csharp
static async Task Main(string[] args)
{
    INgrokManager _ngrokManager;
    _ngrokManager = new NgrokManager();

    await _ngrokManager.DownloadAndUnzipNgrokAsync();

    // Insert your token, if you have one.
    //await _ngrokManager.RegisterAuthTokenAsync("Your token");

    _ngrokManager.StartNgrok();

    var tunnel = new StartTunnelDTO
    {
        name = "reverse proxy",
        proto = "http",
        addr = "8080"
    };

    var httpResponseMessage = await _ngrokManager.StartTunnelAsync(tunnel);

    if ((int)httpResponseMessage.StatusCode == 201)
    {
        var tunnelDetail =
            JsonSerializer.Deserialize<TunnelDetailDTO>(
                await httpResponseMessage.Content.ReadAsStringAsync());

        Console.WriteLine(tunnelDetail.PublicUrl);
    }
}
```

Projects using NgrokSharp

[NgrokGUI](https://github.com/entvex/NgrokGUI)

<!-- ROADMAP -->
## Roadmap

See the [open issues](https://github.com/entvex/NgrokSharp/issues) for a list of proposed features (and known issues).

<!-- CONTRIBUTING -->
## Contributing

Contributions are what make the open source community such an amazing place to be learn, inspire, and create. Any contributions you make are **greatly appreciated**. But first please read [this](https://github.com/entvex/NgrokSharp/blob/master/CONTRIBUTING.md).

<!-- LICENSE -->
## License

Distributed under the MIT License. See `LICENSE` for more information.

<!-- ACKNOWLEDGEMENTS -->
## Acknowledgements
Thanks to these
* [Ngrok for testing account](https://ngrok.com/)

<!-- MARKDOWN LINKS & IMAGES -->
<!-- https://www.markdownguide.org/basic-syntax/#reference-style-links -->
[contributors-shield]: https://img.shields.io/github/contributors/entvex/NgrokSharp.svg?style=for-the-badge
[contributors-url]: https://github.com/entvex/NgrokSharp/graphs/contributors
[forks-shield]: https://img.shields.io/github/forks/entvex/NgrokSharp.svg?style=for-the-badge
[forks-url]: https://github.com/entvex/NgrokSharp/network/members
[stars-shield]: https://img.shields.io/github/stars/entvex/NgrokSharp.svg?style=for-the-badge
[stars-url]: https://github.com/entvex/NgrokSharp/stargazers
[issues-shield]: https://img.shields.io/github/issues/entvex/NgrokSharp.svg?style=for-the-badge
[issues-url]: https://github.com/entvex/NgrokSharp/issues
[license-shield]: https://img.shields.io/github/license/entvex/NgrokSharp.svg?style=for-the-badge
[license-url]: https://github.com/entvex/repo/blob/master/LICENSE.txt
[coverage-shield]: https://img.shields.io/codecov/c/github/entvex/NgrokSharp/master?style=for-the-badge
[coverage-url]: https://app.codecov.io/gh/entvex/NgrokSharp
[discord-shield]: https://img.shields.io/discord/865308817172725770?style=for-the-badge
[discord-url]: https://discord.gg/T3sarz6k5a
