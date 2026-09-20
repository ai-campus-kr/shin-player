# Third-party components

The original ShinPlayer application source is licensed under the MIT License in `LICENSE`, Copyright (c) 2026 한국AI교육진흥원. This does not relicense mpv, FFmpeg, .NET, or any other third-party component. Their own license terms and notices continue to apply.

## mpv / libmpv / FFmpeg and bundled codec libraries

- mpv source: https://github.com/mpv-player/mpv
- Build source and dependency recipes: https://github.com/shinchiro/mpv-winbuild-cmake/tree/20260920
- Windows build: https://github.com/shinchiro/mpv-winbuild-cmake/releases/tag/20260920
- Archive: `mpv-dev-x86_64-20260920-git-e76a35ec95.7z`
- Archive SHA256: `60f9102db46aea8cef9bfb4345ee6a106f34fdbd1df9587e38f0660688039341`
- Observed runtime version: `mpv v0.41.0-1050-ge76a35ec9`
- Build is the general x86_64 variant, not x86_64-v3.
- mpv is copyrighted by the mpv developers and contributors. mpv's default build is GPL-2.0-or-later; the selected Windows build bundles other dependencies with their own licenses. The client API header is ISC licensed. License source: https://github.com/mpv-player/mpv/blob/master/Copyright
- FFmpeg: https://ffmpeg.org/ ; build configuration and third-party codec licenses are governed by the upstream recipes.

The public archive produced by `scripts/package-release.ps1` does **not** contain this prebuilt engine, its archive, or its codec binaries. `Install.cmd` downloads the pinned archive directly from the upstream release above, verifies its SHA256, and installs the engine locally. The build date and hash are intentionally fixed; the installer does not silently follow a latest release.

Local builds made with `scripts/build.ps1` include the engine for local use. Do not upload the local `dist/ShinPlayer` directory as the public release. `ShinPlayer-source.zip` supplies the ShinPlayer application source, not the complete corresponding source of the prebuilt engine and all its dependencies. Redistribution of an engine-bundled package requires supplying the matching corresponding source and notices for that exact engine and its dependencies. Links here are provenance information, not a substitute for those materials.

## Microsoft .NET

The self-contained Windows build includes .NET 8 and Windows Desktop runtime files. They are provided under their Microsoft/.NET open-source licenses. The exact runtime package's licenses and notices are copied to `licenses/dotnet-runtime` and `licenses/dotnet-windowsdesktop` during packaging. See also https://github.com/dotnet/runtime/blob/main/LICENSE.TXT and https://github.com/dotnet/wpf/blob/main/LICENSE.TXT.

## Application icon and test material

The ShinPlayer icon consists of original geometric shapes. Test footage uses FFmpeg's synthetic testsrc2 and sine sources; subtitle text was created for this project. No user videos or third-party films are included.
