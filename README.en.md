<div align="center">

[한국어](README.md) · **English** · [日本語](README.ja.md) · [简体中文](README.zh-CN.md)

# Shin Player · 신플레이어

**Make quiet videos easier to hear. Save a frame for every subtitle.**

A Windows video player with fine speed control, audio boost, and batch frame capture from embedded subtitles.

**New in 0.6 beta:** Persistent address bar · docked YouTube AI chat · seek when requested.

**Windows 10/11 x64 · Free · App source under the MIT license**

</div>

<p align="center">
  <img src="https://raw.githubusercontent.com/ai-campus-kr/shin-player/1da39e78b50322c309727cd52d082da4a7681893/docs/screenshots/v0.5.0/01-boost-and-speed.png" width="1000" alt="Actual Shin Player controls with +12 dB audio boost and 1.5× playback selected">
</p>

| **0.25–8×** | **Up to +12 dB** | **Subtitles → PNG** | **4 UI styles** |
|:---:|:---:|:---:|:---:|
| Adjust in 0.05× steps | Boost quiet recordings | One frame per subtitle cue | Switch styles instantly |

[Quick start](#quick-start) · [Audio boost](#make-quiet-audio-louder) · [Subtitle capture](#one-frame-for-every-subtitle) · [UI styles](#four-ways-to-make-it-yours) · [Shortcuts](#everyday-shortcuts) · [Development](#development-and-verification)

## Turn a moment into a GIF

Click **GIF** in the local player or YouTube window. Enter start/end times or use **현재 위치** (current position), then **GIF 만들기** (create GIF).

- **0.2–30 seconds**, maximum dimension **360 / 480 / 720px**, **10 / 12 / 15 / 20fps**. Silent, endlessly looping output.
- Saves to Windows **Pictures → 신플레이어 GIF**, with date, video title and time range. Existing files are preserved.
- **Local files:** converts the original video segment without changing playback position.
- **YouTube:** captures the rendered video area in real time. Visible captions and overlays may appear. Completion or cancellation restores position, speed, mute and play/pause state.
- No subtitles or API key required. **No GPT tokens used.** FFmpeg is prepared on first use.

Keep the YouTube window visible and avoid seeking or resizing during capture. Ads, live streams and protected videos are unsupported. Actual smoothness depends on the PC's capture speed.

<p align="center"><img src="https://raw.githubusercontent.com/ai-campus-kr/shin-player/v0.6.0-beta.3/docs/screenshots/v0.6.0-beta.3/01-gif-dialog.png" width="620" alt="Actual GIF dialog: time range, dimensions, frame rate and saved result"></p>

*Actual app screenshot using a synthetic test video.*

## Quick start

1. Download `ShinPlayer-<version>-win-x64.zip` from the **[latest release](https://github.com/ai-campus-kr/shin-player/releases/tag/v0.6.0-beta.3)** and extract the entire archive.
2. Run **`Install.cmd`**. No administrator privileges or separate .NET installation are required.
3. Open **신플레이어** from the Start menu, then drop a video onto it or click **영상 열기** (Open video).

The first installation downloads the playback engine and verifies its SHA256 checksum. Capture tools are downloaded when first needed. To update, repeat the installation; your settings and playback history are preserved.

To open videos by double-clicking them, use **`⋯ → Windows 기본 앱으로 설정`** (Set as Windows default app), then select Shin Player for the desired extensions in Windows Settings. Closing the window stops playback and leaves the app in the system tray.

**These language links translate the README. The current app interface and screenshots are in Korean.** Korean button labels are included below to help you find each feature.

<details>
<summary><strong>Installation location, default apps, startup, and removal</strong></summary>

- Installed executable: `%LOCALAPPDATA%\Programs\ShinPlayer\ShinPlayer.exe`.
- Search for **신플레이어** in the Start menu or open **신플레이어.lnk** in the project folder.
- Installation registers the player as an available default app and in “Open with.” It does not override existing Windows default-app choices (`UserChoice`).
- On Windows sign-in, the player waits in the system tray. Closing the window stops playback and returns it to the tray. Double-click the tray icon to reopen it.
- To exit completely, right-click the tray icon and choose **완전히 종료** (Exit completely), or press `Ctrl+Q`.
- Startup and tray behavior can be changed in the `⋯` menu.
- Uninstall from Windows Settings → Apps → Installed apps → 신플레이어. Settings and recent playback history are retained for reinstallation.

</details>

## Make quiet audio louder

Turn on **증폭** (Boost) when a video is still too quiet at your usual volume. Choose a level using the button beside the bottom volume slider, or open **`⋯ → 음량 증폭…`** (Audio boost). The selected level appears immediately and is remembered for the next session.

<p align="center">
  <img src="https://raw.githubusercontent.com/ai-campus-kr/shin-player/1da39e78b50322c309727cd52d082da4a7681893/docs/screenshots/v0.5.0/02-boost-menu.png" width="310" alt="Audio boost menu: Off, +3, +6, +9, and +12 dB, with the maximum level selected">
</p>

**Off · Low +3 dB · Medium +6 dB · High +9 dB · Maximum +12 dB**

Boost is independent of the normal 0–100 volume control. A **limiter** caps loud peaks. It works alongside mute, speed adjustment, and pitch correction. Selecting **끄기** (Off) restores the original audio level; Off is the default.

This boosts **all audio**, including voices and background sound. It does not isolate speech or repair noise and distortion already present in the recording.

## One frame for every subtitle

Collect scenes from lectures or study videos with **자막 캡처 → 전체 캡처** (Subtitle capture → Capture all). Shin Player uses the embedded text subtitles to **save a PNG at the midpoint of each subtitle cue**. You can include the subtitle text in each image.

<p align="center">
  <img src="https://raw.githubusercontent.com/ai-campus-kr/shin-player/1da39e78b50322c309727cd52d082da4a7681893/docs/screenshots/v0.5.0/04-capture-completed.png" width="760" alt="Actual completed capture dialog showing three PNGs saved from the demo video">
</p>

**These are actual PNGs exported from the demo video.** They show the frames at 1, 3, and 5 seconds, corresponding to its three Korean subtitle cues.

| 1 s · First subtitle | 3 s · Second subtitle | 5 s · Third subtitle |
|:---:|:---:|:---:|
| ![Actual PNG for the first subtitle](https://raw.githubusercontent.com/ai-campus-kr/shin-player/1da39e78b50322c309727cd52d082da4a7681893/docs/screenshots/v0.5.0/05-captured-frame-1.png) | ![Actual PNG for the second subtitle](https://raw.githubusercontent.com/ai-campus-kr/shin-player/1da39e78b50322c309727cd52d082da4a7681893/docs/screenshots/v0.5.0/05-captured-frame-2.png) | ![Actual PNG for the third subtitle](https://raw.githubusercontent.com/ai-campus-kr/shin-player/1da39e78b50322c309727cd52d082da4a7681893/docs/screenshots/v0.5.0/05-captured-frame-3.png) |

**[Try it with the 6-second demo video](https://github.com/ai-campus-kr/shin-player/releases/download/v0.5.0/ShinPlayer-subtitle-demo.mp4)**

Open the demo MP4 → choose **자막 캡처** or press `Ctrl+Shift+S` → select the Korean track → choose **전체 캡처**. Three PNGs are saved in a `date_video-name_사진` folder inside your Windows **Pictures** folder. The literal suffix `사진` means “photos.”

Capture runs independently of playback, preserving position and speed, and can be canceled. **It requires embedded text subtitles.** External SRT files, image-based subtitles, and text burned into the video are not used for batch capture.

<details>
<summary><strong>Folders, filenames, subtitle rendering, and cancellation</strong></summary>

- Open capture from the top **자막 캡처** button, `Ctrl+Shift+S`, **`⋯ → 자막별 일괄 캡처…`**, or the subtitle menu. Select an embedded subtitle track, then choose **전체 캡처**.
- One PNG is saved at the midpoint of each cue. A multiline cue produces one image. Only the selected language track is processed.
- The destination uses the actual Windows Pictures location, including redirected locations such as OneDrive. Folder names follow `2026-09-20_video-name_사진`.
- Filenames follow `00001_00-01-23-450.png`: sequence number and hours-minutes-seconds-milliseconds. Existing folders are preserved by adding `(2)`, `(3)`, and so on.
- **사진에 자막 포함** (Include subtitles in images) is enabled by default. Disable it for video-only images. Subtitles use a common font and the subtitle timing offset active when the dialog was opened. Original ASS styling and positioning are not fully preserved.
- Progress and the output location are displayed. **취소** (Cancel) keeps completed PNGs. `캡처목록.json` records each image’s subtitle, timestamp, and completion/cancellation status. Use **저장 폴더 열기** (Open output folder) when finished.
- Supported sources include embedded text subtitles in MP4 and SRT/ASS tracks in MKV. If no supported subtitles exist, the app explains why capture is unavailable.
- Capture does not change playback position or speed. Long or 4K videos may require substantial processing time and disk space.
- **No GPT, API key, or token costs.** Processing uses local FFmpeg/FFprobe. When tools are missing, a pinned version is downloaded from its original distribution source and verified with SHA256. Videos and subtitles are not uploaded.

</details>

## Four ways to make it yours

Choose **UI 선택** (Choose UI) at the top, or **`⋯ → UI 선택…`**. Each style changes colors, control layout, and playlist placement. **Minimal** is the default.

<p align="center">
  <img src="https://raw.githubusercontent.com/ai-campus-kr/shin-player/1da39e78b50322c309727cd52d082da4a7681893/docs/screenshots/v0.5.0/03-ui-picker.png" width="760" alt="Actual UI picker showing Minimal, Studio, Light, and Lime">
</p>

| Style | Look and layout |
|---|---|
| **Minimal · 미니멀** | Monochrome, a spacious video area, and a bottom timeline |
| **Studio · 스튜디오** | Left-side playlist, large timecode, and compact controls |
| **Light · 라이트** | Light surfaces, blue accents, and generous spacing |
| **Lime · 라임** | Dark background, lime accents, and card-style controls |

Switching during playback **preserves position, speed, and pause state**. Your choice is saved, and an open capture dialog changes color with it. Any seek-bar drag in progress is canceled when switching styles.

<sub>Images show actual UI renders from 0.5.0 and original PNGs exported from the demo. <a href="https://github.com/ai-campus-kr/shin-player/blob/1da39e78b50322c309727cd52d082da4a7681893/docs/screenshots/v0.5.0/README.md">Image provenance and capture method (Korean)</a></sub>

## Playback essentials included

- **0.25–8× speed**, **0.05× fine adjustment**, direct speed entry, and pitch correction.
- Click and drag to seek, frame stepping, **A–B repeat**, and repeat current video.
- Playlists, automatic next video, recent files, and resume from the last position.
- External subtitles, subtitle/audio track selection, timing adjustment, and single-frame screenshots.
- File/folder drag and drop, Korean paths, a single app instance, and tray standby.
- Common local video and audio files, including MP4, MKV, AVI, MOV, WMV, and WebM.

<details>
<summary><strong>Supported formats and playback limits</strong></summary>

The app registers 30 video extensions with Windows, including MP4, MKV, AVI, MOV, WMV, WebM, TS, MTS, M2TS, and FLV. Audio files can also be opened directly.

mpv/FFmpeg decodes formats such as H.264, HEVC, AV1, and VP9. D3D11 hardware decoding is used on supported GPUs, with software decoding for unsupported codecs. The layout adjusts to window size and playlist visibility. Dropping a folder adds media files directly inside it in filename order. Launching the app again passes files to the existing instance.

Playback is not guaranteed for DRM-protected videos, damaged files, or specialized proprietary formats. Actual high-speed performance depends on resolution, codec, storage, and GPU. HDR color accuracy and extended 4K/8K playback still require separate verification.

</details>

## Everyday shortcuts

| Key | Action |
|---|---|
| `Space` | Play / pause |
| `←` / `→` | Seek 5 seconds |
| `[` / `]` | Adjust speed by 0.25× |
| `Shift+[` / `Shift+]` | Adjust speed by 0.05× |
| `F` / `F11` | Full screen |
| `A` | Set A → set B → clear repeat |
| `Alt+D` / `Ctrl+U` | Focus the YouTube address bar |
| `Ctrl+J` | Local video AI chat |
| `Ctrl+S` | Save current frame as PNG |
| `Ctrl+Shift+S` | Batch capture from embedded subtitles |

<details>
<summary><strong>All shortcuts</strong></summary>

| Key | Action |
|---|---|
| Space | Play / pause |
| ← / → | Seek 5 seconds |
| Shift+← / Shift+→ | Seek 30 seconds |
| ↑ / ↓, mouse wheel over video | Volume |
| [ / ] | Adjust speed by 0.25× |
| Shift+[ / Shift+] | Adjust speed by 0.05× |
| R or Backspace | Return to 1× |
| F / F11, double-click video | Full screen |
| Esc | Leave full screen |
| A | Set repeat start → end → clear |
| . / , | Next / previous frame |
| M / S | Mute / toggle subtitles |
| N / Shift+N | Next / previous video |
| Ctrl+O / Ctrl+L | Open file / playlist |
| Ctrl+S | Save PNG in the `신플레이어` folder under Pictures |
| Ctrl+Shift+S | Batch subtitle capture |
| Ctrl+Q | Exit completely |

</details>

## YouTube and video chat — 0.6 beta

**“Explain this” → an answer based on the transcript. “Go to that part” → seek to the supporting moment.** [Download the beta](https://github.com/ai-campus-kr/shin-player/releases/tag/v0.6.0-beta.3)

1. Enter a YouTube address in the always-visible top **address bar**, then press **Enter** or **→**. **Alt+D / Ctrl+U** selects the address. You can omit `https://`, for example `youtube.com`. The app opens the normal YouTube page in **Edge WebView2**.
2. Open YouTube's **More → Show transcript**, then choose **자막 불러오기** (Load subtitles) in the **docked chat panel**. It appears on the right in wide windows and below in narrow windows. **채팅 배치** (Chat placement) also lets you choose the position.
3. Save your own OpenAI key under **API 설정** (API settings). Ordinary questions leave playback in place. Explicitly ask **“Go to that part”** or click a supporting timestamp to seek.

For local video, use **AI / Ctrl+J → 자막 불러오기** and select a language if needed. Only **embedded text subtitles** qualify. External SRT, image subtitles, OCR and speech transcription are excluded.

<p align="center"><img src="https://raw.githubusercontent.com/ai-campus-kr/shin-player/v0.6.0-beta.2/docs/screenshots/v0.6.0-beta.2/02-youtube-chat.png" width="1000" alt="YouTube and docked AI chat"></p>

<sub>The address bar and docked chat panel, verified with a real YouTube video and the GPT-5.4-mini API.</sub>

- Model: **`gpt-5.4-mini`**. Keys are encrypted for the current Windows user at `%LOCALAPPDATA%\ShinPlayer\openai-key.dat` and can be deleted in the app.
- Asking sends your **question, previous question and answer, video label and selected subtitle text** to OpenAI, never video or audio files. Your account's charges and limits apply. Each response shows input/output token usage.
- Long transcripts use local candidate selection within a 60,000-character budget including per-cue overhead. A **partial search** notice appears; context may be missed.
- Missing evidence, invalid answers, cancelled requests and responses after window closure or video changes cannot trigger seeking. Seeking is blocked during ads.
- Chat subtitles stay in memory; no conversation file is written. Web login and cookies use `%LOCALAPPDATA%\ShinPlayer\youtube-browser`. Existing Chrome, Edge or Whale profiles and extensions are not imported.

Missing WebView2 Runtime opens Microsoft's installation guidance. YouTube login, region and video restrictions still apply. **외부 열기** opens the default browser, without AI seek integration. Web videos use YouTube's controls; local mpv speed, audio boost and batch capture settings do not apply. No stream extraction or downloader is used.

**Beta validation:** Passed 81 integration checks and live `gpt-5.4-mini` API checks. A real YouTube video supplied 301 Korean transcript cues: an ordinary question preserved playback position, then “Go to the part you just explained” sought to 10:45 without another API request. Subtitle availability, ads, login requirements and site changes may affect other videos.

Audio boost and local subtitle capture still work **on your PC without API keys or token costs**.

## Development and verification

A native Windows app built with C# / .NET 8 WPF and mpv. Local playback uses mpv; the optional YouTube window uses Edge WebView2. No development server is needed.

The 0.6 beta has **81 integration checks** using WPF/libmpv and isolated WebView2. API tests use mock responses; live-service validation limits are described above. See the [changelog](CHANGELOG.md).

<details>
<summary><strong>Build and install from source</strong></summary>

Requires .NET 8 SDK, Windows x64, PowerShell, and Windows `tar.exe`.

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\install.ps1
~~~

`build.ps1` verifies an mpv archive pinned by date and SHA256, then creates `dist\ShinPlayer` with the .NET runtime included. Installation is for the current user and requires no administrator privileges. The install script supports `-NoStartup` and `-NoLaunch`.

Running `scripts\install.ps1` again closes the existing player and updates it, preserving playback settings, history, and the existing startup choice. The install folder also contains the project source as `ShinPlayer-source.zip`.

</details>

<details>
<summary><strong>Create GitHub release packages</strong></summary>

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\package-release.ps1
~~~

The script creates the installation ZIP, source ZIP, and `SHA256SUMS.txt` in `dist/release`. The installation ZIP includes the app and .NET runtime, but not the mpv binaries. The installer downloads the engine directly from its original release. Personal history, logs, videos, development paths, and shortcuts are excluded.

Publish the three files from `dist/release`. Do not upload the development `dist/ShinPlayer` folder directly, because it includes the engine. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for sources and distribution details. `.github/workflows/build.yml` compiles and packages on Windows; it does not automatically publish releases or run GPU integration checks.

</details>

<details>
<summary><strong>Verification scope and reproduction</strong></summary>

With Python and FFmpeg installed:

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test.ps1
~~~

The suite generates synthetic media and plays it through actual WPF windows and libmpv. Checks cover Korean paths, eight container/codec combinations, FLAC audio, speed range, pitch correction, seeking, pause, volume, A–B repeat, Korean SRT, frame stepping, full screen, playlists, error recovery, automatic next video, resume, tray stop, and reopening. Results go to `artifacts\verification\results.json`, decoded frames to `decoded-*.png`, and UI renders to files such as `01-home.png`. WPF UI renders do not include the video surface rendered in a separate HWND.

Timing measurements use synthetic 640×360 media on the development PC. `loadMs` measures from the open request to mpv’s `file-loaded` event, not the full delay until the first visible frame. Seek measurements wait for `playback-restart`. These results do not establish a speed advantage over the Windows default player.

Capture checks use separate synthetic MP4/MKV files. They verify embedded track selection, actual frame colors at subtitle midpoints, Korean text in PNGs, the no-subtitle option, duplicate output, partial results after cancellation, worker-process termination, playback-position preservation, and the capture dialog. Test images are saved only under `artifacts`.

There are 81 integration checks. Seeking covers 28 points across two window sizes and playlist states, repeated input before layout refresh, release position, capture loss, file changes, and full screen. WPF layout transforms from 1× to 2× are tested; physical mouse input and real monitor-DPI changes are not automated. UI checks render all four styles at startup and minimum size, plus playlists and capture dialogs, checking for overlapping buttons and subtitle-selection visibility. Settings serialization, backward compatibility, selection buttons, and position/speed preservation during live style changes are also covered.

Audio-boost checks play synthetic PCM audio while muted and measure peak/RMS levels at the mpv filter output. They verify the actual gain of every preset, the limiter at maximum boost, restoration when disabled, initial state restoration, rapid adjustment, and preservation of other filters.

</details>

<details>
<summary><strong>Implementation and local data</strong></summary>

- `MpvPlayer.cs`: UTF-8 P/Invoke, asynchronous commands, a dedicated event thread, and event waiting while idle.
- `MainWindow.xaml(.cs)`: video renders directly to a native HWND, with WPF controls outside it. Drag seeking commits when the drag ends.
- `Program.cs` / `App.xaml.cs`: file requests go to the existing instance before WPF starts. A per-user mutex and a named pipe restricted to the current user are used. At sign-in, the window and engine are prepared without showing the window.
- `WindowsIntegration.cs`: HKCU app registration, 30 video extensions, startup, and the Windows default-app settings link.
- Settings, the most recent 40 files, and resume positions: `%LOCALAPPDATA%\ShinPlayer\settings.json`. Use **`⋯ → 최근 기록 지우기`** (Clear recent history) to clear history.
- Errors: `player.log` in the same folder, rotated at 2 MB. Files are not uploaded.
- `SubtitleCapture.cs` / `SubtitleCaptureWindow.cs` / `CaptureTools.cs`: embedded subtitle extraction, independent frame capture, and progress/cancel UI. Tools are cached in `%LOCALAPPDATA%\ShinPlayer\capture-tools`.

</details>

## License and sources

- [Official mpv site](https://mpv.io/) / [mpv manual](https://mpv.io/manual/master/)
- [Windows builds linked from mpv’s installation page](https://mpv.io/installation/)
- [Microsoft default-app registration and settings](https://learn.microsoft.com/en-us/windows/apps/develop/windows-integration/default-apps-platform)
- [Per-app default settings URI](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-default-apps-settings)

Shin Player’s own source is released under the [MIT license](LICENSE). Copyright (c) 2026 한국AI교육진흥원. External components, including mpv, FFmpeg, and .NET, have their own licenses. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for engine/runtime sources and distribution details.

---

[한국AI교육진흥원](https://github.com/ai-campus-kr) · [Latest release](https://github.com/ai-campus-kr/shin-player/releases/tag/v0.6.0-beta.3) · [Report bugs or suggest features](https://github.com/ai-campus-kr/shin-player/issues)
