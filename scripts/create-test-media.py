"""Generate synthetic, redistributable decoder fixtures. No user videos are needed."""
from pathlib import Path
import subprocess
import sys

root = Path(__file__).resolve().parents[1]
out = root / 'artifacts' / 'fixtures'
out.mkdir(parents=True, exist_ok=True)

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
print(out)
