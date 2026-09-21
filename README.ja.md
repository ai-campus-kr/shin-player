<div align="center">

[한국어](README.md) · [English](README.en.md) · **日本語** · [简体中文](README.zh-CN.md)

# Shin Player · 신플레이어

**小さな音を聞き取りやすく。字幕ごとに、シーンを一枚。**

細かな速度調整、音量ブースト、内蔵字幕を使った一括キャプチャに対応する Windows 動画プレーヤーです。

**0.6 ベータの新機能：** 常時表示のアドレスバー・動画横の AI チャット・依頼したときに該当区間へ移動。

**Windows 10/11 x64 · 無料 · アプリのソースは MIT ライセンスで公開**

</div>

<p align="center">
  <img src="https://raw.githubusercontent.com/ai-campus-kr/shin-player/v0.6.0-beta.7/docs/screenshots/v0.6.0-beta.7/01-home-badges.png" width="1000" alt="右下に鮮やかな紫のKAI・緑のNAVERバッジを固定した新プレーヤー">
</p>

| **0.25〜8倍速** | **最大 +12 dB** | **字幕 → PNG** | **4種類の UI** |
|:---:|:---:|:---:|:---:|
| 0.05倍刻みで調整 | 小さく録音された音を増幅 | 字幕の区間ごとに一枚 | その場でスタイルを切り替え |

[使い始める](#使い始める) · [音量ブースト](#小さな音をもっと大きく) · [字幕キャプチャ](#字幕ごとに一枚の画像) · [UI 選択](#好みで選べる4種類の-ui) · [ショートカット](#よく使うショートカット) · [開発と検証](#開発と検証)

## 好きな場面を GIF に

ローカル動画ファイルを開き、プレーヤーの **GIF** ボタンから、**タイムライン両端のハンドルをドラッグ**して区間を選び、**GIF 만들기**（GIF 作成）を押します。選択範囲の中央をドラッグすると、長さを保ったまま区間を移動できます。

- **선택 확대**（選択範囲を拡大）と **전체 보기**（全体表示）で短い区間も調整できます。ハンドルにフォーカスし、矢印キーで0.1秒、Shift併用で1秒、Ctrl併用で10秒ずつ移動します。
- 時刻の直接入力と **현재 위치**（現在位置）も引き続き使え、タイムラインと相互に連動します。

- **0.25〜100倍速**を直接入力するかプリセットを選択。**元動画10分 ÷ 10倍速 → 1分の GIF**のように予定の長さを即時表示します。
- 元動画の区間は最大**1時間**、完成 GIF は**0.2秒〜5分**。最大辺 **360 / 480 / 720px**、**10 / 12 / 15 / 20fps**。音声なしのループ再生です。
- Windows の **ピクチャ → 신플레이어 GIF** に日付・動画名・区間を含む名前で保存。既存ファイルは上書きしません。
- **ローカル:** 元動画の指定区間を変換し、再生位置は変えません。
- 字幕・API キーは不要。**GPT トークンは使いません。** FFmpeg は初回利用時に準備します。

**GIF 作成はローカル動画ファイルのみ対応します。YouTube の GIF キャプチャ機能は提供していません。**

<p align="center"><img src="https://raw.githubusercontent.com/ai-campus-kr/shin-player/v0.6.0-beta.6/docs/screenshots/v0.6.0-beta.6/01-gif-range.png" width="620" alt="実際の GIF 画面：ドラッグで区間選択・移動、拡大と時刻入力の連動"></p>

*合成テスト動画を使った実際のアプリ画面です。*

## 使い始める

1. [最新リリース](https://github.com/ai-campus-kr/shin-player/releases/tag/v0.6.0-beta.9)から `ShinPlayer-バージョン-win-x64.zip` をダウンロードし、すべて展開します。
2. **`Install.cmd`** を実行します。管理者権限や .NET の別途インストールは不要です。
3. スタートメニューから **신플레이어** を開き、動画をドラッグ＆ドロップするか、**영상 열기**（動画を開く）を押します。

初回インストール時に再生エンジンをダウンロードし、SHA256 を確認します。キャプチャ用ツールも初めて必要になった時点で準備します。更新も同じ手順で行い、設定と再生履歴は保持されます。

動画のダブルクリックで起動するには、**`⋯ → Windows 기본 앱으로 설정`**（Windows の既定のアプリに設定）を開き、Windows の設定で対象の拡張子に Shin Player を指定してください。ウィンドウを閉じると再生を停止し、タスクトレイで待機します。

**上部の言語リンクは README の翻訳です。現在のアプリ画面と掲載画像は韓国語です。** 操作箇所が分かるよう、本文には実際の韓国語ボタン名も記載しています。

<details>
<summary><strong>インストール先・既定のアプリ・自動起動・アンインストール</strong></summary>

- 実行ファイル：`%LOCALAPPDATA%\Programs\ShinPlayer\ShinPlayer.exe`。
- スタートメニューで **신플레이어** を検索するか、プロジェクトフォルダーの **신플레이어.lnk** を開きます。
- インストール時に、既定のアプリ候補と「プログラムから開く」に登録します。既存の既定アプリ設定（`UserChoice`）は変更しません。
- Windows サインイン時はタスクトレイで待機します。ウィンドウの × で再生を停止してトレイに戻り、トレイアイコンのダブルクリックで再表示します。
- 完全に終了するには、トレイアイコンを右クリックして **완전히 종료**（完全に終了）を選ぶか、`Ctrl+Q` を押します。
- 自動起動とトレイの動作は `⋯` メニューで変更できます。
- 削除は Windows の設定 → アプリ → インストールされているアプリ → 신플레이어 から行います。再インストールに備え、設定と最近の再生履歴は保持します。

</details>

## 小さな音をもっと大きく

通常の音量では聞き取りにくい動画に、**증폭**（ブースト）を使えます。下部の音量スライダー横のボタン、または **`⋯ → 음량 증폭…`**（音量ブースト）で段階を選択します。選んだ値はすぐに表示され、次回起動時にも引き継がれます。

<p align="center">
  <img src="https://raw.githubusercontent.com/ai-campus-kr/shin-player/1da39e78b50322c309727cd52d082da4a7681893/docs/screenshots/v0.5.0/02-boost-menu.png" width="310" alt="音量ブーストのメニュー：オフ、+3、+6、+9、+12 dB。最大値を選択した状態">
</p>

**オフ · 弱 +3 dB · 中 +6 dB · 強 +9 dB · 最大 +12 dB**

通常の 0〜100 の音量とは独立して調整でき、大きなピークは **リミッター**で抑えます。ミュート、速度変更、音程維持と併用できます。**끄기**（オフ）で元の音量に戻ります。初期設定はオフです。

声と背景音を含む**音声全体の増幅**です。声だけの分離や、元の録音に含まれるノイズ・歪みの修復は行いません。

## 字幕ごとに一枚の画像

講義や学習動画のシーンをまとめて残すには、**자막 캡처 → 전체 캡처**（字幕キャプチャ → すべてキャプチャ）を選びます。動画内のテキスト字幕をもとに、**各字幕区間の中間時点を PNG として保存**します。画像に字幕を含めることもできます。

<p align="center">
  <img src="https://raw.githubusercontent.com/ai-campus-kr/shin-player/1da39e78b50322c309727cd52d082da4a7681893/docs/screenshots/v0.5.0/04-capture-completed.png" width="760" alt="デモ動画から PNG 3枚を保存した、実際のキャプチャ完了画面">
</p>

**以下はデモ動画から実際に保存した PNG です。** 韓国語字幕の3区間に対応する、1秒・3秒・5秒のシーンをキャプチャしています。

| 1秒 · 最初の字幕 | 3秒 · 2番目の字幕 | 5秒 · 3番目の字幕 |
|:---:|:---:|:---:|
| ![最初の字幕から保存した PNG](https://raw.githubusercontent.com/ai-campus-kr/shin-player/1da39e78b50322c309727cd52d082da4a7681893/docs/screenshots/v0.5.0/05-captured-frame-1.png) | ![2番目の字幕から保存した PNG](https://raw.githubusercontent.com/ai-campus-kr/shin-player/1da39e78b50322c309727cd52d082da4a7681893/docs/screenshots/v0.5.0/05-captured-frame-2.png) | ![3番目の字幕から保存した PNG](https://raw.githubusercontent.com/ai-campus-kr/shin-player/1da39e78b50322c309727cd52d082da4a7681893/docs/screenshots/v0.5.0/05-captured-frame-3.png) |

**[6秒のデモ動画で試す](https://github.com/ai-campus-kr/shin-player/releases/download/v0.5.0/ShinPlayer-subtitle-demo.mp4)**

デモ MP4 を開く → **자막 캡처** または `Ctrl+Shift+S` → 韓国語トラックを選択 → **전체 캡처**。Windows の**ピクチャ**フォルダー内に `日付_動画名_사진` フォルダーと PNG 3枚が保存されます。末尾の `사진` は韓国語で「写真」を意味し、実際のフォルダー名にもこの文字が使われます。

再生とは独立して処理し、再生位置と速度を保持します。途中でキャンセルすることもできます。**内蔵テキスト字幕がある動画が対象**です。外部 SRT、画像形式の字幕、映像に焼き込まれた文字は一括キャプチャの対象外です。

<details>
<summary><strong>保存先・ファイル名・字幕表示・キャンセルの詳細</strong></summary>

- 上部の **자막 캡처**、`Ctrl+Shift+S`、**`⋯ → 자막별 일괄 캡처…`**、または字幕メニューから開けます。内蔵字幕トラックを選択し、**전체 캡처** を押します。
- 各字幕区間の中間時点で PNG を一枚保存します。複数行の字幕も一区間なら一枚です。選択したトラックだけを処理し、複数言語を混在させません。
- Windows が実際に使っているピクチャフォルダーを使用します。OneDrive などに移動していても、その保存先を使います。フォルダー名の例は `2026-09-20_動画名_사진` です。
- ファイル名は `00001_00-01-23-450.png` のように、連番と時・分・秒・ミリ秒で構成します。同名フォルダーがある場合は `(2)`、`(3)` を付け、既存の画像を保持します。
- **사진에 자막 포함**（画像に字幕を含める）は初期状態でオンです。オフにすると映像のみを保存します。字幕は共通のフォントで表示し、ダイアログを開いた時点の字幕タイミング補正を反映します。ASS の装飾や配置を完全に再現する機能ではありません。
- 進捗と保存先を表示し、**취소**（キャンセル）できます。保存済みの PNG は残り、`캡처목록.json` に各画像の字幕・時刻・完了／キャンセル状態を記録します。終了後は **저장 폴더 열기**（保存フォルダーを開く）で確認できます。
- MP4 の内蔵テキスト字幕、MKV 内の SRT/ASS などに対応します。対応する字幕がない場合は理由を表示し、キャプチャしません。
- プレーヤーの再生位置と速度を変えずに処理します。長い動画や 4K 動画では、処理時間と保存容量が多く必要になる場合があります。
- **GPT・API キー・トークン料金は不要です。** ローカルの FFmpeg/FFprobe を使います。ツールがない場合のみ、固定バージョンを元の配布元からダウンロードして SHA256 を確認します。動画や字幕はアップロードしません。

</details>

## 好みで選べる4種類の UI

上部の **UI 선택**（UI 選択）、または **`⋯ → UI 선택…`** から選びます。色だけでなく、コントロールの配置や再生リストの位置も変わります。標準は **Minimal** です。

<p align="center">
  <img src="https://raw.githubusercontent.com/ai-campus-kr/shin-player/1da39e78b50322c309727cd52d082da4a7681893/docs/screenshots/v0.5.0/03-ui-picker.png" width="760" alt="実際の UI 選択画面：Minimal、Studio、Light、Lime の4種類">
</p>

| スタイル | 見た目と配置 |
|---|---|
| **Minimal · 미니멀** | モノトーン、広い動画領域、下部のタイムライン |
| **Studio · 스튜디오** | 左側の再生リスト、大きなタイムコード、コンパクトな操作部 |
| **Light · 라이트** | 明るい背景、青いアクセント、ゆとりのある配置 |
| **Lime · 라임** | 暗い背景、ライム色のアクセント、カード型の操作部 |

再生中に切り替えても、**再生位置・速度・一時停止状態を保持**します。選択は次回起動にも反映され、開いている字幕キャプチャ画面の色も変わります。切り替え時に行っていたシークバーのドラッグはキャンセルします。

<sub>画像は 0.5.0 の実際の UI レンダーと、デモ動画から保存した PNG 原本です。<a href="https://github.com/ai-campus-kr/shin-player/blob/1da39e78b50322c309727cd52d082da4a7681893/docs/screenshots/v0.5.0/README.md">画像の出典と作成方法（韓国語）</a></sub>

## 基本の再生機能も充実

- **0.25〜8倍速**、**0.05倍刻みの微調整**、速度の直接入力、音程維持。
- クリック・ドラッグでシーク、コマ送り／戻し、**A–B 区間リピート**、現在の動画を繰り返し再生。
- 再生リスト、次の動画の自動再生、最近の動画、前回の位置から再開。
- 外部字幕、字幕・音声トラックの切り替え、タイミング補正、単一フレームの保存。
- ファイル／フォルダーのドラッグ＆ドロップ、韓国語パス、単一インスタンス、トレイ待機。
- MP4・MKV・AVI・MOV・WMV・WebM など、一般的なローカル動画・音声の再生。

<details>
<summary><strong>対応形式と再生範囲</strong></summary>

MP4、MKV、AVI、MOV、WMV、WebM、TS、MTS、M2TS、FLV など、30種類の動画拡張子を Windows に登録します。音声ファイルも直接開けます。

H.264、HEVC、AV1、VP9 などを mpv/FFmpeg でデコードし、対応 GPU では D3D11 ハードウェアデコードを使用します。ハードウェアで処理できないコーデックはソフトウェアで再生します。ウィンドウサイズと再生リストの表示状態に合わせてレイアウトを調整します。フォルダーを追加すると、直下のメディアファイルを名前順に読み込みます。再度起動した場合は既存のプロセスにファイルを渡します。

DRM で保護された動画、破損ファイル、特殊な独自形式の再生は保証しません。高倍速での処理性能は解像度・コーデック・ストレージ・GPU に依存します。HDR の色再現や長時間の 4K/8K 再生には、別途検証が必要です。

</details>

## よく使うショートカット

| キー | 操作 |
|---|---|
| `Space` | 再生／一時停止 |
| `←` / `→` | 5秒移動 |
| `[` / `]` | 0.25倍刻みで速度変更 |
| `Shift+[` / `Shift+]` | 0.05倍刻みで速度変更 |
| `F` / `F11` | 全画面 |
| `A` | A を設定 → B を設定 → リピート解除 |
| `Alt+D` / `Ctrl+U` | YouTube アドレスバーを選択 |
| `Ctrl+J` | ローカル動画 AI チャット |
| `Ctrl+S` | 現在のフレームを PNG で保存 |
| `Ctrl+Shift+S` | 内蔵字幕ごとの一括キャプチャ |

<details>
<summary><strong>すべてのショートカット</strong></summary>

| キー | 操作 |
|---|---|
| Space | 再生／一時停止 |
| ← / → | 5秒移動 |
| Shift+← / Shift+→ | 30秒移動 |
| ↑ / ↓、動画上のマウスホイール | 音量 |
| [ / ] | 0.25倍刻みで速度変更 |
| Shift+[ / Shift+] | 0.05倍刻みで速度変更 |
| R または Backspace | 1倍速に戻す |
| F / F11、動画のダブルクリック | 全画面 |
| Esc | 全画面を解除 |
| A | リピート開始 → 終了 → 解除 |
| . / , | 次／前のフレーム |
| M / S | ミュート／字幕表示切り替え |
| N / Shift+N | 次／前の動画 |
| Ctrl+O / Ctrl+L | ファイルを開く／再生リスト |
| Ctrl+S | ピクチャ内の `신플레이어` フォルダーに PNG 保存 |
| Ctrl+Shift+S | 字幕ごとの一括キャプチャ |
| Ctrl+Q | 完全に終了 |

</details>

## YouTube と動画 AI チャット — 0.6 ベータ

**「この内容を説明して」→ 字幕に基づいて回答。「その部分へ移動して」→ 再生位置を移動。** [ベータ版をダウンロード](https://github.com/ai-campus-kr/shin-player/releases/tag/v0.6.0-beta.9)

1. 上部に常時表示される **アドレスバー** に YouTube の URL を入力し、**Enter** または **→** を押します。**Alt+D / Ctrl+U** でアドレスを選択できます。`youtube.com` のように `https://` を省略できます。アプリ内の **Edge WebView2** で通常の YouTube ページを開きます。
2. **動画を開くと字幕を自動で読み込みます。** 字幕がなければ **「자막이 없는 영상입니다」**（字幕のない動画です）と表示します。チャットは広いウィンドウでは右側、狭いウィンドウでは下側に表示され、**채팅 배치**で位置を選べます。
3. **API 설정** ウィンドウで自分の OpenAI キーを保存します。緑色の **✓ API 키 저장됨** が保存済みの目印です。**연결 확인**（接続確認）で実際の API を試し、成功すると **✓ API 연결 확인됨** と表示します。通常の質問では再生位置を変えず、<strong>「その部分へ移動して」</strong>または根拠の時刻ボタンで移動します。

ローカル動画は **AI / Ctrl+J** を開くと内蔵テキスト字幕を自動で読み込みます。韓国語、既定トラック、最初のトラックの順に選択します。外部 SRT・画像字幕・OCR・音声文字起こしは使いません。

<p align="center"><img src="https://raw.githubusercontent.com/ai-campus-kr/shin-player/v0.6.0-beta.9/docs/screenshots/v0.6.0-beta.9/01-api-settings.png" width="430" alt="API key saved status"><img src="https://raw.githubusercontent.com/ai-campus-kr/shin-player/v0.6.0-beta.9/docs/screenshots/v0.6.0-beta.9/02-chat-ready.png" width="430" alt="Automatic subtitles and chat"></p>

<sub>隔離したテストキーと合成字幕で撮影した実際の設定・チャット画面です。本物の API キーは表示していません。</sub>

- モデルは **`gpt-5.4-mini`**。キーは Windows ユーザー用に暗号化して `%LOCALAPPDATA%\ShinPlayer\openai-key.dat` に保存し、アプリから削除できます。
- 字幕の自動確認に API キーやトークンは不要です。**接続確認**は動画・字幕なしの短いリクエストで、少量のトークンを使います。
- 質問時に **質問・直前の質問と回答・動画名・選択した字幕テキスト**を OpenAI に送ります。動画・音声ファイルは送りません。自分のアカウントの料金と上限が適用され、入出力トークン数を表示します。
- 長い字幕はローカルで候補を選び、行の付加情報を含む 60,000 文字の予算内に収めます。**部分検索**と表示され、文脈を見落とす場合があります。
- 根拠なし・不正な応答・取り消し・ウィンドウ終了・動画変更後の回答では移動しません。広告再生中も移動しません。
- 字幕はチャットのメモリに保持し、会話履歴ファイルは作りません。Web のログインと Cookie は `%LOCALAPPDATA%\ShinPlayer\youtube-browser` に保存します。Chrome・Edge・Whale の既存プロファイルや拡張機能は取り込みません。

WebView2 Runtime がない場合は Microsoft のインストール案内を表示します。YouTube のログイン・地域・動画制限は適用されます。**외부 열기**で既定のブラウザーを開けますが、外部ウィンドウには AI 移動は連携しません。Web 動画は YouTube のコントロールを使い、ローカル mpv の速度・ブースト・一括キャプチャ設定は適用しません。ストリーム抽出やダウンローダーは使いません。

**ベータ版の検証範囲：** 86 項目の統合検査でキーの保存・接続状態、字幕の自動取得・欠落・動画切り替え、チャット配置とローカル GIF を確認しました。実際の YouTube 字幕取得、GPT-5.4-mini の回答・時刻移動も別途検証しました。字幕の提供状況やサイト変更により利用できない動画もあります。

音量ブーストとローカル字幕キャプチャは引き続き **API キー・トークン料金なしで PC 内で処理**します。

## 開発と検証

C# / .NET 8 WPF と mpv で作られた Windows ネイティブアプリです。ローカル再生は mpv、任意の YouTube 機能は Edge WebView2 を使用します。開発サーバーは不要です。

0.6 ベータ版は WPF/libmpv と隔離された WebView2 の **86項目の統合検査**で検証します。API は模擬応答を使い、実サービスの検証範囲は上記のとおりです。[変更履歴](CHANGELOG.md)

<details>
<summary><strong>ソースからビルド・インストール</strong></summary>

.NET 8 SDK、Windows x64、PowerShell、Windows 標準の `tar.exe` が必要です。

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\install.ps1
~~~

`build.ps1` は日付と SHA256 を固定した mpv アーカイブを検証し、.NET ランタイムを含む `dist\ShinPlayer` を生成します。インストールは現在のユーザーのみが対象で、管理者権限は不要です。インストールスクリプトは `-NoStartup` と `-NoLaunch` に対応します。

`scripts\install.ps1` を再実行すると、起動中のプレーヤーを終了して更新します。再生設定、履歴、自動起動の選択は保持します。インストール先には `ShinPlayer-source.zip` としてソースも含まれます。

</details>

<details>
<summary><strong>GitHub 配布ファイルの作成</strong></summary>

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\package-release.ps1
~~~

`dist/release` にインストール用 ZIP、ソース ZIP、`SHA256SUMS.txt` を生成します。インストール用 ZIP はアプリと .NET ランタイムを含み、mpv バイナリは含みません。エンジンはインストーラーが元のリリースから直接ダウンロードします。個人の履歴・ログ・動画・開発用パス・ショートカットは含めません。

公開には `dist/release` の3ファイルを使用します。開発用の `dist/ShinPlayer` はエンジンを含むため、そのままアップロードしないでください。出典と配布区分は [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) に記載しています。`.github/workflows/build.yml` は Windows 上でコンパイルとパッケージ作成を行います。自動公開や GPU 統合検査は行いません。

</details>

<details>
<summary><strong>検証範囲と再現方法</strong></summary>

Python と FFmpeg がインストールされた環境で実行します。

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test.ps1
~~~

合成メディアを作成し、実際の WPF ウィンドウと libmpv で再生します。韓国語パス、8種類のコンテナー／コーデックの組み合わせ、FLAC、速度範囲、音程維持、シーク、一時停止、音量、A–B リピート、韓国語 SRT、コマ送り、全画面、再生リスト、エラー復旧、次の動画の自動再生、再開、トレイでの停止と再表示を確認します。結果は `artifacts\verification\results.json`、デコードしたフレームは `decoded-*.png`、UI レンダーは `01-home.png` などに保存します。WPF の UI レンダーには、別の HWND で描画する動画領域は含まれません。

時間計測は開発 PC と合成 640×360 動画を基準にしています。`loadMs` はファイルを開く要求から mpv の `file-loaded` イベントまでで、最初のフレームが見えるまでの総遅延ではありません。シーク計測は `playback-restart` まで待ちます。この結果から Windows 標準プレーヤーに対する速度優位を主張するものではありません。

キャプチャ検査は別の合成 MP4/MKV を使用します。内蔵トラック選択、字幕中間時点の実際のフレーム色、PNG の韓国語字幕、字幕なし設定、重複保存、キャンセル時の部分結果保持、作業プロセス終了、再生位置保持、キャプチャ画面を確認します。検査用画像は `artifacts` 配下にのみ保存します。

統合検査は計86項目です。シークは2種類のウィンドウサイズと再生リストの状態にわたる28地点、レイアウト更新前の連続入力、マウスを離す位置、キャプチャ解除、ファイル切り替え、全画面を含みます。1〜2倍の WPF レイアウト変換も確認しますが、物理マウス入力や実モニターの DPI 変更は自動化していません。4種類の UI の起動画面・最小サイズ・再生リスト・キャプチャ画面をレンダーし、ボタンの重なりと字幕選択表示を確認します。設定の保存形式と旧設定との互換性、選択ボタン、再生中のスタイル変更に伴う位置・速度の保持も対象です。

音量ブースト検査は合成 PCM 音源をミュートで再生し、mpv フィルター出力のピーク／RMS を測定します。各段階の実際のゲイン、最大時のリミッター、オフ時の元の振幅への復帰、初期設定の復元、素早い連続変更、他のフィルターの保持を確認します。

</details>

<details>
<summary><strong>実装とローカルデータ</strong></summary>

- `MpvPlayer.cs`：UTF-8 P/Invoke、非同期コマンド、専用イベントスレッド、アイドル時のイベント待機。
- `MainWindow.xaml(.cs)`：動画はネイティブ HWND に直接描画し、WPF コントロールはその外側に配置します。ドラッグによるシークはドラッグ終了時に実行します。
- `Program.cs` / `App.xaml.cs`：WPF 起動前に既存インスタンスへファイル要求を渡します。ユーザー別ミューテックスと、現在のユーザーだけがアクセスできる名前付きパイプを使用します。サインイン時はウィンドウを表示せず、画面とエンジンを準備します。
- `WindowsIntegration.cs`：HKCU アプリ登録、30種類の動画拡張子、自動起動、Windows 既定アプリ設定へのリンク。
- 設定・最近の40ファイル・再開位置：`%LOCALAPPDATA%\ShinPlayer\settings.json`。履歴は **`⋯ → 최근 기록 지우기`**（最近の履歴を消去）で削除できます。
- エラーログ：同じフォルダーの `player.log`。2 MB ごとにローテーションし、外部にアップロードしません。
- `SubtitleCapture.cs` / `SubtitleCaptureWindow.cs` / `CaptureTools.cs`：内蔵字幕抽出、独立したフレーム保存、進捗・キャンセル画面。ツールキャッシュは `%LOCALAPPDATA%\ShinPlayer\capture-tools` に保存します。

</details>

## ライセンスと出典

- [mpv 公式サイト](https://mpv.io/) / [mpv マニュアル](https://mpv.io/manual/master/)
- [mpv 公式ページから案内されている Windows ビルド](https://mpv.io/installation/)
- [Microsoft の既定アプリ登録・設定](https://learn.microsoft.com/en-us/windows/apps/develop/windows-integration/default-apps-platform)
- [アプリ別の既定アプリ設定 URI](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-default-apps-settings)

Shin Player 本体のソースは [MIT ライセンス](LICENSE)で公開しています。Copyright (c) 2026 한국AI교육진흥원。mpv・FFmpeg・.NET などの外部コンポーネントには、それぞれのライセンスが適用されます。エンジンとランタイムの出典・配布区分は [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) を参照してください。

---

[한국AI교육진흥원](https://github.com/ai-campus-kr) · [最新リリース](https://github.com/ai-campus-kr/shin-player/releases/tag/v0.6.0-beta.9) · [不具合報告・機能の提案](https://github.com/ai-campus-kr/shin-player/issues)
