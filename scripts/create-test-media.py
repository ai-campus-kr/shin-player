"""Generate synthetic, redistributable decoder fixtures. No user videos are needed."""
from pathlib import Path
import subprocess
import sys
import math
import struct
import wave

root = Path(__file__).resolve().parents[1]
out = root / 'artifacts' / 'fixtures'
out.mkdir(parents=True, exist_ok=True)

# Known-amplitude tones for gain/limiter measurements inside the actual mpv pipeline.
# Self-tests mute the audio output; no user recording or audible listening test is used.
for name, amplitude in [('boost-quiet.wav', .025), ('boost-loud.wav', .8)]:
    target = out / name
    if not target.exists():
        second = b''.join(struct.pack('<h', round(amplitude * 32767 * math.sin(2 * math.pi * 440 * n / 48000))) for n in range(48000))
        with wave.open(str(target), 'wb') as wav:
            wav.setparams((1, 2, 48000, 0, 'NONE', 'not compressed'))
            wav.writeframes(second * 30)

def run(*args):
    subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-y', *map(str, args)], check=True)

base = out / '한글 영상 sample.mp4'
if not base.exists():
    run('-f', 'lavfi', '-i', 'testsrc2=size=640x360:rate=30', '-f', 'lavfi', '-i', 'sine=frequency=440:sample_rate=48000', '-t', '12', '-c:v', 'libx264', '-preset', 'ultrafast', '-g', '30', '-pix_fmt', 'yuv420p', '-c:a', 'aac', '-movflags', '+faststart', base)
variants = {
    'hevc.mkv': ['-c:v', 'libx265', '-preset', 'ultrafast', '-x265-params', 'log-level=error', '-c:a', 'aac'],
    'vp9.webm': ['-c:v', 'libvpx-vp9', '-deadline', 'realtime', '-cpu-used', '8', '-c:a', 'libopus'],
    'mpeg4.avi': ['-c:v', 'mpeg4', '-q:v', '4', '-c:a', 'libmp3lame'],
    'h264.mov': ['-c', 'copy'],
    'h264.ts': ['-c', 'copy'],
    'wmv.wmv': ['-c:v', 'wmv2', '-c:a', 'wmav2'],
    'av1.mkv': ['-c:v', 'libsvtav1', '-preset', '12', '-crf', '45', '-svtav1-params', 'lp=2', '-c:a', 'aac'],
    'audio.flac': ['-vn', '-c:a', 'flac'],
}
for name, options in variants.items():
    target = out / name
    if not target.exists():
        run('-i', base, '-t', '6', *options, target)
    print(name, flush=True)
(out / '테스트 자막.srt').write_text('1\n00:00:00,000 --> 00:00:05,000\n신플레이어 한글 자막 테스트\n\n2\n00:00:05,000 --> 00:00:12,000\n배속을 바꿔도 자막은 영상에 맞춰집니다.\n', encoding='utf-8')
(out / 'broken.mp4').write_bytes(b'This is not a video.\x00\xff')

# Subtitle capture: distinct frame colors, embedded Korean/English tracks,
# a filename that would break shell/filter-string interpolation, and ASS in MKV.
captions = out / 'capture-ko.srt'
captions.write_text('1\n00:00:00,200 --> 00:00:01,800\n첫 번째 빨간 장면\n\n2\n00:00:02,200 --> 00:00:03,800\n두 번째 초록 장면\n여러 줄 자막 테스트\n\n3\n00:00:04,200 --> 00:00:05,800\n세 번째 파란 장면\n', encoding='utf-8')
english = out / 'capture-en.srt'
english.write_text('1\n00:00:00,500 --> 00:00:01,500\nRed scene\n\n2\n00:00:04,500 --> 00:00:05,500\nBlue scene\n', encoding='utf-8')
capture_video = out / "캡처 '테스트 [한글].mp4"
if not capture_video.exists():
    run('-f', 'lavfi', '-i', 'color=red:size=640x360:rate=30:duration=2',
        '-f', 'lavfi', '-i', 'color=green:size=640x360:rate=30:duration=2',
        '-f', 'lavfi', '-i', 'color=blue:size=640x360:rate=30:duration=2',
        '-i', captions, '-i', english, '-filter_complex', '[0:v][1:v][2:v]concat=n=3:v=1:a=0[v]',
        '-map', '[v]', '-map', '3:s', '-map', '4:s', '-c:v', 'libx264', '-preset', 'ultrafast',
        '-g', '30', '-c:s', 'mov_text', '-metadata:s:s:0', 'language=kor',
        '-metadata:s:s:1', 'language=eng', capture_video)
capture_mkv = out / 'capture-ass.mkv'
if not capture_mkv.exists():
    run('-i', capture_video, '-map', '0:v', '-map', '0:s:0', '-c:v', 'copy', '-c:s', 'ass', capture_mkv)
# Ten actual minutes with distinct scenes; low source FPS keeps the fixture small.
long_gif_video = out / 'gif-ten-minutes.mp4'
if not long_gif_video.exists():
    run('-f', 'lavfi', '-i', 'color=red:size=640x360:rate=2:duration=200',
        '-f', 'lavfi', '-i', 'color=green:size=640x360:rate=2:duration=200',
        '-f', 'lavfi', '-i', 'color=blue:size=640x360:rate=2:duration=200',
        '-filter_complex', '[0:v][1:v][2:v]concat=n=3:v=1:a=0[v]',
        '-map', '[v]', '-c:v', 'libx264', '-preset', 'ultrafast', '-g', '2', long_gif_video)
print(out)
