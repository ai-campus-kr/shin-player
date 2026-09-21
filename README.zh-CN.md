<div align="center">

[한국어](README.md) · [English](README.en.md) · [日本語](README.ja.md) · **简体中文**

# Shin Player · 신플레이어

**让小音量更清晰，为每条字幕留下一张画面。**

一款 Windows 视频播放器，支持精细调速、音量增强，以及根据内嵌字幕批量截图。

**0.6 测试版新功能：** 常驻地址栏 · YouTube 旁的 AI 聊天面板 · 按请求跳到对应片段。

**Windows 10/11 x64 · 免费 · 应用源码采用 MIT 许可证**

</div>

<p align="center">
  <img src="https://raw.githubusercontent.com/ai-campus-kr/shin-player/1da39e78b50322c309727cd52d082da4a7681893/docs/screenshots/v0.5.0/01-boost-and-speed.png" width="1000" alt="实际播放控件：已选择 +12 dB 音量增强和 1.5 倍速">
</p>

| **0.25–8 倍速** | **最高 +12 dB** | **字幕 → PNG** | **4 种界面** |
|:---:|:---:|:---:|:---:|
| 以 0.05 倍为步长调整 | 放大录制音量较小的声音 | 每个字幕区间保存一张 | 随时切换喜欢的风格 |

[快速开始](#快速开始) · [音量增强](#让小音量更响亮) · [字幕截图](#每条字幕一张图片) · [界面选择](#四种界面随心选择) · [快捷键](#常用快捷键) · [开发与验证](#开发与验证)

## 把精彩片段变成 GIF

点击本地播放器或 YouTube 窗口的 **GIF**，输入开始、结束时间，或用 **현재 위치**（当前位置）标记，再点击 **GIF 만들기**（制作 GIF）。

- **0.2–30 秒**，最长边 **360 / 480 / 720px**，**10 / 12 / 15 / 20fps**。无声、循环播放。
- 保存至 Windows **图片 → 신플레이어 GIF**，文件名包含日期、视频名称和时间范围，不覆盖已有文件。
- **本地视频：** 转换原视频的指定片段，不改变播放位置。
- **YouTube：** 实时截取浏览器显示的视频区域，画面中的字幕和浮层也可能被录入。完成或取消后恢复位置、速度、静音及播放状态。
- 无需字幕或 API 密钥。**不消耗 GPT Token。** 首次使用时准备 FFmpeg。

录制期间请保持 YouTube 窗口显示，不要跳转或调整窗口大小。不支持广告、直播和受保护的视频。实际流畅度取决于电脑的截图速度。

<p align="center"><img src="https://raw.githubusercontent.com/ai-campus-kr/shin-player/v0.6.0-beta.3/docs/screenshots/v0.6.0-beta.3/01-gif-dialog.png" width="620" alt="实际 GIF 制作界面：时间范围、尺寸、帧率及保存结果"></p>

*使用合成测试视频拍摄的实际应用界面。*

## 快速开始

1. 从[最新发行版](https://github.com/ai-campus-kr/shin-player/releases/tag/v0.6.0-beta.3)下载 `ShinPlayer-版本-win-x64.zip`，并完整解压。
2. 运行 **`Install.cmd`**。无需管理员权限，也无需单独安装 .NET。
3. 在开始菜单打开 **신플레이어**，将视频拖入窗口，或点击 **영상 열기**（打开视频）。

首次安装会下载播放引擎并验证 SHA256。截图工具也会在首次需要时下载。更新时重复安装步骤即可，原有设置和播放记录会保留。

如果希望双击视频即可打开，请选择 **`⋯ → Windows 기본 앱으로 설정`**（设为 Windows 默认应用），再在 Windows 设置中将所需扩展名关联到 Shin Player。关闭窗口会停止播放，并让应用在系统托盘中待命。

**上方语言链接切换的是 README 译文。目前应用界面和展示图片均为韩语。** 下文保留了实际的韩语按钮名称，方便对照操作。

<details>
<summary><strong>安装位置、默认应用、自动启动与卸载</strong></summary>

- 可执行文件位置：`%LOCALAPPDATA%\Programs\ShinPlayer\ShinPlayer.exe`。
- 在开始菜单搜索 **신플레이어**，或打开项目文件夹中的 **신플레이어.lnk**。
- 安装会将播放器注册为默认应用候选项，并添加到“打开方式”。不会覆盖现有的 Windows 默认应用选择（`UserChoice`）。
- 登录 Windows 后，播放器在系统托盘中待命。点击窗口的 × 会停止播放并返回托盘；双击托盘图标可重新打开窗口。
- 完全退出：右键点击托盘图标，选择 **완전히 종료**（完全退出），或按 `Ctrl+Q`。
- 自动启动和托盘行为可在 `⋯` 菜单中调整。
- 卸载：Windows 设置 → 应用 → 已安装的应用 → 신플레이어。设置和最近播放记录会保留，以便重新安装后使用。

</details>

## 让小音量更响亮

当视频在常用音量下仍然听不清时，可以开启 **증폭**（增强）。在底部音量滑块旁的按钮中选择档位，或打开 **`⋯ → 음량 증폭…`**（音量增强）。选中的数值会立即显示，并在下次启动时保留。

<p align="center">
  <img src="https://raw.githubusercontent.com/ai-campus-kr/shin-player/1da39e78b50322c309727cd52d082da4a7681893/docs/screenshots/v0.5.0/02-boost-menu.png" width="310" alt="音量增强菜单：关闭、+3、+6、+9、+12 dB，当前选择最高档">
</p>

**关闭 · 轻度 +3 dB · 中度 +6 dB · 强力 +9 dB · 最高 +12 dB**

增强与常规的 0–100 音量控制相互独立，并通过**限幅器**限制过大的峰值。可与静音、变速、音高保持同时使用。选择 **끄기**（关闭）即可恢复原始音量；默认状态为关闭。

此功能会放大**全部音频**，包括人声和背景音。它不会单独提取人声，也不会修复原始录音中的噪声或失真。

## 每条字幕一张图片

想收集讲座或学习视频中的画面时，选择 **자막 캡처 → 전체 캡처**（字幕截图 → 全部截图）。播放器根据视频内的文本字幕，**在每个字幕区间的中间时刻保存一张 PNG**。也可以将字幕文字一并写入图片。

<p align="center">
  <img src="https://raw.githubusercontent.com/ai-campus-kr/shin-player/1da39e78b50322c309727cd52d082da4a7681893/docs/screenshots/v0.5.0/04-capture-completed.png" width="760" alt="实际截图完成窗口：已从演示视频保存 3 张 PNG">
</p>

**以下三张图片均为演示视频实际导出的 PNG。** 它们分别对应三条韩语字幕在第 1、3、5 秒的画面。

| 第 1 秒 · 第一条字幕 | 第 3 秒 · 第二条字幕 | 第 5 秒 · 第三条字幕 |
|:---:|:---:|:---:|
| ![第一条字幕实际导出的 PNG](https://raw.githubusercontent.com/ai-campus-kr/shin-player/1da39e78b50322c309727cd52d082da4a7681893/docs/screenshots/v0.5.0/05-captured-frame-1.png) | ![第二条字幕实际导出的 PNG](https://raw.githubusercontent.com/ai-campus-kr/shin-player/1da39e78b50322c309727cd52d082da4a7681893/docs/screenshots/v0.5.0/05-captured-frame-2.png) | ![第三条字幕实际导出的 PNG](https://raw.githubusercontent.com/ai-campus-kr/shin-player/1da39e78b50322c309727cd52d082da4a7681893/docs/screenshots/v0.5.0/05-captured-frame-3.png) |

**[用 6 秒演示视频立即体验](https://github.com/ai-campus-kr/shin-player/releases/download/v0.5.0/ShinPlayer-subtitle-demo.mp4)**

打开演示 MP4 → 点击 **자막 캡처** 或按 `Ctrl+Shift+S` → 选择韩语字幕轨道 → 点击 **전체 캡처**。Windows 的**图片**文件夹中会生成 `日期_视频名_사진` 文件夹，并保存三张 PNG。末尾的 `사진` 是韩语“照片”，实际文件夹名也使用这两个韩文字符。

截图任务独立于播放运行，保留播放位置和速度，并支持中途取消。**仅适用于含有内嵌文本字幕的视频。** 外部 SRT、图像字幕和烧录在画面中的文字不属于批量截图的处理对象。

<details>
<summary><strong>保存目录、文件名、字幕显示与取消操作</strong></summary>

- 可通过顶部的 **자막 캡처**、`Ctrl+Shift+S`、**`⋯ → 자막별 일괄 캡처…`** 或字幕菜单打开。选择内嵌字幕轨道后，点击 **전체 캡처**。
- 每个字幕区间在中间时刻保存一张 PNG。一条多行字幕仍只生成一张图片。仅处理所选轨道，不混合多个语言轨道。
- 使用 Windows 实际的图片文件夹位置，即使该文件夹已迁移至 OneDrive 等位置也会正确跟随。文件夹名示例：`2026-09-20_视频名_사진`。
- 文件名如 `00001_00-01-23-450.png`，由序号和时、分、秒、毫秒组成。已有同名文件夹时会添加 `(2)`、`(3)` 等后缀，保留原有图片。
- **사진에 자막 포함**（图片中包含字幕）默认开启。关闭后只保存视频画面。字幕使用统一字体，并应用打开对话框时的字幕时间偏移；不会完整保留 ASS 原有的样式和排版。
- 界面显示进度和保存位置，可点击 **취소**（取消）。已保存的 PNG 会保留，`캡처목록.json` 记录每张图片的字幕、时间及完成／取消状态。结束后可点击 **저장 폴더 열기**（打开保存文件夹）。
- 支持 MP4 内嵌文本字幕、MKV 中的 SRT/ASS 等。没有受支持的字幕时，会提示原因且不执行截图。
- 处理过程不会改变播放器的位置和速度。长视频或 4K 视频可能需要较多处理时间和存储空间。
- **不需要 GPT、API 密钥或 Token 费用。** 使用本地 FFmpeg/FFprobe。仅在缺少工具时，从原始发布来源下载固定版本并验证 SHA256。视频和字幕不会上传。

</details>

## 四种界面，随心选择

通过顶部的 **UI 선택**（选择界面），或 **`⋯ → UI 선택…`** 选择风格。除了配色，还会改变控件布局和播放列表的位置。默认使用 **Minimal**。

<p align="center">
  <img src="https://raw.githubusercontent.com/ai-campus-kr/shin-player/1da39e78b50322c309727cd52d082da4a7681893/docs/screenshots/v0.5.0/03-ui-picker.png" width="760" alt="实际界面选择窗口：Minimal、Studio、Light、Lime 四种风格">
</p>

| 风格 | 外观与布局 |
|---|---|
| **Minimal · 미니멀** | 单色调、宽敞的视频区域、底部时间轴 |
| **Studio · 스튜디오** | 左侧播放列表、大号时间码、紧凑控件 |
| **Light · 라이트** | 明亮背景、蓝色点缀、宽松布局 |
| **Lime · 라임** | 深色背景、青柠色点缀、卡片式控件 |

播放时切换界面会**保留播放位置、速度和暂停状态**。所选风格会保存到下次启动，已打开的字幕截图窗口也会同步改变颜色。切换时正在进行的进度条拖动会取消。

<sub>图片为 0.5.0 的实际 UI 渲染图，以及演示视频导出的原始 PNG。<a href="https://github.com/ai-campus-kr/shin-player/blob/1da39e78b50322c309727cd52d082da4a7681893/docs/screenshots/v0.5.0/README.md">图片来源与制作方式（韩语）</a></sub>

## 常用播放功能一应俱全

- **0.25–8 倍速**、**0.05 倍精细调整**、直接输入速度、保持音高。
- 点击或拖动定位、逐帧前进／后退、**A–B 区间循环**、单个视频循环。
- 播放列表、自动播放下一个视频、最近文件、从上次位置继续。
- 外部字幕、字幕／音轨切换、同步调整、单张画面截图。
- 文件／文件夹拖放、韩语路径、单实例运行、托盘待命。
- 播放 MP4、MKV、AVI、MOV、WMV、WebM 等常见本地视频和音频文件。

<details>
<summary><strong>支持格式与播放范围</strong></summary>

向 Windows 注册 30 种视频扩展名，包括 MP4、MKV、AVI、MOV、WMV、WebM、TS、MTS、M2TS、FLV 等。也可以直接打开音频文件。

通过 mpv/FFmpeg 解码 H.264、HEVC、AV1、VP9 等格式。在支持的 GPU 上使用 D3D11 硬件解码，不支持的编解码器则使用软件解码。界面随窗口大小和播放列表状态调整布局。拖入文件夹时，会按文件名顺序添加该文件夹直接包含的媒体文件。重复启动会将文件交给现有进程处理。

不保证播放受 DRM 保护的视频、损坏文件或特殊专有格式。高倍速的实际处理能力取决于分辨率、编解码器、存储设备和 GPU。HDR 色彩准确性及长时间 4K/8K 播放仍需单独验证。

</details>

## 常用快捷键

| 按键 | 功能 |
|---|---|
| `Space` | 播放／暂停 |
| `←` / `→` | 跳转 5 秒 |
| `[` / `]` | 以 0.25 倍为步长调速 |
| `Shift+[` / `Shift+]` | 以 0.05 倍为步长调速 |
| `F` / `F11` | 全屏 |
| `A` | 设置 A → 设置 B → 取消循环 |
| `Alt+D` / `Ctrl+U` | 选中 YouTube 地址栏 |
| `Ctrl+J` | 本地视频 AI 聊天 |
| `Ctrl+S` | 将当前画面保存为 PNG |
| `Ctrl+Shift+S` | 按内嵌字幕批量截图 |

<details>
<summary><strong>全部快捷键</strong></summary>

| 按键 | 功能 |
|---|---|
| Space | 播放／暂停 |
| ← / → | 跳转 5 秒 |
| Shift+← / Shift+→ | 跳转 30 秒 |
| ↑ / ↓、视频区域内的鼠标滚轮 | 音量 |
| [ / ] | 以 0.25 倍为步长调速 |
| Shift+[ / Shift+] | 以 0.05 倍为步长调速 |
| R 或 Backspace | 恢复 1 倍速 |
| F / F11、双击视频 | 全屏 |
| Esc | 退出全屏 |
| A | 设置循环起点 → 终点 → 取消 |
| . / , | 下一帧／上一帧 |
| M / S | 静音／切换字幕显示 |
| N / Shift+N | 下一个／上一个视频 |
| Ctrl+O / Ctrl+L | 打开文件／播放列表 |
| Ctrl+S | 将 PNG 保存到图片文件夹内的 `신플레이어` 目录 |
| Ctrl+Shift+S | 按字幕批量截图 |
| Ctrl+Q | 完全退出 |

</details>

## YouTube 与视频 AI 聊天 — 0.6 测试版

**“解释这段内容” → 根据字幕回答。“跳到那里” → 移动播放位置。** [下载测试版](https://github.com/ai-campus-kr/shin-player/releases/tag/v0.6.0-beta.3)

1. 在顶部常驻的 **地址栏** 输入 YouTube 地址，然后按 **Enter** 或 **→**。按 **Alt+D / Ctrl+U** 可选中地址。可以省略 `https://`，例如直接输入 `youtube.com`。应用内的 **Edge WebView2** 会打开正常的 YouTube 页面。
2. 打开 YouTube 的 **更多 → 显示转录文稿**，然后点击视频旁 **聊天面板 → 자막 불러오기**（加载字幕）。宽窗口放在右侧，窄窗口放在下方。也可通过 **채팅 배치**（聊天布局）手动选择位置。
3. 在 **API 설정**（API 设置）保存自己的 OpenAI 密钥并提问。普通问题不会改变播放位置。明确要求 **“跳到那里”** 或点击依据时间按钮才会跳转。

本地视频使用 **AI / Ctrl+J → 자막 불러오기**，必要时选择语言。仅支持 **内嵌文本字幕**，不使用外部 SRT、图片字幕、OCR 或语音转写。

<p align="center"><img src="https://raw.githubusercontent.com/ai-campus-kr/shin-player/v0.6.0-beta.2/docs/screenshots/v0.6.0-beta.2/02-youtube-chat.png" width="1000" alt="YouTube and docked AI chat"></p>

<sub>使用真实 YouTube 视频与 GPT-5.4-mini API 验证的地址栏及聊天面板。</sub>

- 模型为 **`gpt-5.4-mini`**。密钥按当前 Windows 用户加密，保存在 `%LOCALAPPDATA%\ShinPlayer\openai-key.dat`，可在应用内删除。
- 提问时将 **问题、上一轮问题和回答、视频名称和选定字幕文本**发送给 OpenAI，不发送视频或音频文件。费用和额度适用自己的账户，每次显示输入/输出 Token 数。
- 长字幕先在本地筛选候选片段，包括每条字幕附加信息在内，控制在 60,000 字符预算中。会提示 **部分搜索**，可能遗漏上下文。
- 没有依据、响应无效、取消、关闭窗口或更换视频后的响应不会改变播放位置。广告播放期间也不跳转。
- 聊天字幕保存在内存中，不创建对话历史文件。网页登录和 Cookie 存储在 `%LOCALAPPDATA%\ShinPlayer\youtube-browser`，不导入 Chrome、Edge 或 Whale 的现有配置与扩展。

缺少 WebView2 Runtime 时会提供 Microsoft 官方安装说明。YouTube 的登录、地区及视频限制仍然适用。**외부 열기**可打开默认浏览器，但 AI 跳转不连接该外部窗口。网页视频使用 YouTube 自身控件，本地 mpv 的倍速、音量增强和批量截图设置不适用。不使用视频流提取或下载工具。

**测试版验证范围：** 已通过 81 项集成检查和真实 `gpt-5.4-mini` API 检查。从真实 YouTube 视频读取了 301 条韩语字幕：普通问题保持播放位置，“跳到刚才解释的部分”则跳到 10:45，无需再次调用 API。字幕可用性、广告、登录要求和网站变化可能影响其他视频。

音量增强和本地字幕截图仍然 **在 PC 上处理，无需 API 密钥或 Token 费用**。

## 开发与验证

使用 C# / .NET 8 WPF 和 mpv 构建的 Windows 原生应用，本地播放使用 mpv，可选 YouTube 功能使用 Edge WebView2，无需开发服务器。

0.6 测试版使用 WPF/libmpv 和隔离 WebView2 的 **81 项集成检查**验证。API 使用模拟响应，真实服务验证范围见上文。[更新日志](CHANGELOG.md)

<details>
<summary><strong>从源码构建与安装</strong></summary>

需要 .NET 8 SDK、Windows x64、PowerShell 和 Windows 自带的 `tar.exe`。

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\install.ps1
~~~

`build.ps1` 验证按日期和 SHA256 固定的 mpv 压缩包，并生成包含 .NET 运行时的 `dist\ShinPlayer`。安装仅针对当前用户，无需管理员权限。安装脚本支持 `-NoStartup` 和 `-NoLaunch`。

再次运行 `scripts\install.ps1` 会关闭现有播放器并更新，保留播放设置、历史记录和原有自动启动选择。安装目录中还包含 `ShinPlayer-source.zip` 源码包。

</details>

<details>
<summary><strong>生成 GitHub 发行文件</strong></summary>

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\package-release.ps1
~~~

在 `dist/release` 中生成安装 ZIP、源码 ZIP 和 `SHA256SUMS.txt`。安装 ZIP 包含应用和 .NET 运行时，不包含 mpv 二进制文件；安装器会从原始发行来源直接下载引擎。个人记录、日志、视频、开发路径和快捷方式不会打包。

公开发布请使用 `dist/release` 中的三个文件。开发用 `dist/ShinPlayer` 包含引擎，不应直接上传。来源与分发范围见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。`.github/workflows/build.yml` 在 Windows 上执行编译和打包，不自动公开发布，也不运行 GPU 集成检查。

</details>

<details>
<summary><strong>验证范围与复现方法</strong></summary>

在已安装 Python 和 FFmpeg 的环境中运行：

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test.ps1
~~~

检查会生成合成媒体，并在实际 WPF 窗口和 libmpv 中播放。覆盖韩语路径、8 种容器／编解码器组合、FLAC 音频、速度范围、音高保持、定位、暂停、音量、A–B 循环、韩语 SRT、逐帧操作、全屏、播放列表、错误恢复、自动播放下一个视频、断点续播、托盘停止和重新打开。结果写入 `artifacts\verification\results.json`，解码帧写入 `decoded-*.png`，UI 渲染图写入 `01-home.png` 等文件。WPF UI 渲染图不包含通过独立 HWND 绘制的视频区域。

计时使用开发 PC 和合成的 640×360 视频。`loadMs` 表示从打开请求到 mpv 的 `file-loaded` 事件的时间，并非首帧实际显示前的完整延迟。定位计时会等待 `playback-restart`。这些结果不代表相对 Windows 默认播放器的速度优势。

截图检查使用独立的合成 MP4/MKV，验证内嵌轨道选择、字幕中间时刻的实际帧颜色、PNG 中的韩语文字、不含字幕选项、重复保存、取消后的部分结果保留、工作进程终止、播放位置保持以及截图窗口。检查图片仅保存到 `artifacts` 下。

共运行 81 项集成检查。定位检查覆盖两种窗口大小和播放列表状态下的 28 个位置，以及布局刷新前的重复输入、释放位置、鼠标捕获丢失、文件切换和全屏。也检查 1–2 倍 WPF 布局变换，但未自动化物理鼠标输入或实际显示器 DPI 变更。界面检查渲染四种风格的启动画面、最小窗口、播放列表和截图窗口，检查按钮重叠及字幕选择显示。还检查设置序列化、旧设置兼容性、选择按钮，以及播放时切换风格对位置和速度的保持。

音量增强检查在静音状态下播放合成 PCM 音频，并测量 mpv 滤镜输出的峰值／RMS。验证各档实际增益、最高档限幅、关闭后恢复原始幅度、初始设置恢复、快速连续调整以及其他滤镜的保留。

</details>

<details>
<summary><strong>实现与本地数据</strong></summary>

- `MpvPlayer.cs`：UTF-8 P/Invoke、异步命令、专用事件线程，以及空闲时等待事件。
- `MainWindow.xaml(.cs)`：视频直接输出到原生 HWND，WPF 控件绘制在视频区域外。拖动定位在拖动结束时执行。
- `Program.cs` / `App.xaml.cs`：创建 WPF 之前将文件请求传给现有实例。使用按用户区分的互斥量，以及仅当前用户可访问的命名管道。登录时在不显示窗口的情况下准备窗口和播放引擎。
- `WindowsIntegration.cs`：HKCU 应用注册、30 种视频扩展名、自动启动和 Windows 默认应用设置链接。
- 设置、最近 40 个文件和续播位置：`%LOCALAPPDATA%\ShinPlayer\settings.json`。通过 **`⋯ → 최근 기록 지우기`**（清除最近记录）可清除历史记录。
- 错误日志：同目录的 `player.log`，每 2 MB 轮转。文件不会上传。
- `SubtitleCapture.cs` / `SubtitleCaptureWindow.cs` / `CaptureTools.cs`：内嵌字幕提取、独立截图任务、进度与取消界面。工具缓存位于 `%LOCALAPPDATA%\ShinPlayer\capture-tools`。

</details>

## 许可证与来源

- [mpv 官方网站](https://mpv.io/) / [mpv 手册](https://mpv.io/manual/master/)
- [mpv 官方安装页链接的 Windows 构建](https://mpv.io/installation/)
- [Microsoft 默认应用注册与设置](https://learn.microsoft.com/en-us/windows/apps/develop/windows-integration/default-apps-platform)
- [按应用打开默认设置的 URI](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-default-apps-settings)

Shin Player 自身源码采用 [MIT 许可证](LICENSE)。Copyright (c) 2026 한국AI교육진흥원。mpv、FFmpeg、.NET 等外部组件分别适用各自的许可证。引擎与运行时的来源和分发范围请参阅 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。

---

[한국AI교육진흥원](https://github.com/ai-campus-kr) · [最新发行版](https://github.com/ai-campus-kr/shin-player/releases/tag/v0.6.0-beta.3) · [报告问题或提出功能建议](https://github.com/ai-campus-kr/shin-player/issues)
