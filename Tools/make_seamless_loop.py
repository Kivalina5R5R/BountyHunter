"""Make a green-screen video loop seamlessly (wall characters: Rabbit/Sloth typing, dog idles).

The loop is frames start..end-1 of the source. Its first K frames are cross-faded with the K
frames that follow end (the natural continuation of the loop's last frame): output frame i
(i < K) is source[end + i] * (1 - w) + source[start + i] * w with w = (i + 1) / (K + 1), then the
untouched frames start+K..end-1. So the last frame flows on into the first, and the fade only has
to cover the small difference between source[end] and source[start]. Pick start/end where those
poses match; the defaults use the whole clip. Blending is done on the raw YUV planes, so the
colours are not converted at all (no green shift). The output is H.264 baseline without B-frames
or audio, which Unity's Windows video decoder loops without stalling.

Usage: python make_seamless_loop.py <source.mp4> <output.mp4> [fade frames, default 24] [start] [end]
  Wall loops used: Rabbit 36 276, Sloth 2 336, Chiwawa 96 336, Pom 96 336, Saibirian 6 326,
  Shiba 96 336 (fade 24, 60 fps sources of 360 frames).
"""
import subprocess
import sys

from PIL import Image

source, output = sys.argv[1], sys.argv[2]
FADE = int(sys.argv[3]) if len(sys.argv) > 3 else 24
START = int(sys.argv[4]) if len(sys.argv) > 4 else 0
END = int(sys.argv[5]) if len(sys.argv) > 5 else None


def probe(path):
    result = subprocess.run(['ffprobe', '-v', 'error', '-select_streams', 'v', '-count_frames',
                             '-show_entries', 'stream=width,height,r_frame_rate,nb_read_frames',
                             '-of', 'csv=p=0', path], capture_output=True, text=True, check=True)
    width, height, rate, frames = result.stdout.strip().split(',')
    return int(width), int(height), rate, int(frames)


def read_frames(path, size):
    """Yields each frame of the video as raw yuv420p bytes."""
    decoder = subprocess.Popen(['ffmpeg', '-v', 'error', '-i', path, '-f', 'rawvideo', '-pix_fmt', 'yuv420p', '-'],
                               stdout=subprocess.PIPE)
    while True:
        frame = decoder.stdout.read(size)
        if len(frame) < size:
            break
        yield frame
    decoder.wait()


def blend(a, b, weight, planes):
    """a * (1 - weight) + b * weight, plane by plane."""
    out = bytearray()
    start = 0
    for plane_width, plane_height in planes:
        length = plane_width * plane_height
        first = Image.frombytes('L', (plane_width, plane_height), a[start:start + length])
        second = Image.frombytes('L', (plane_width, plane_height), b[start:start + length])
        out += Image.blend(first, second, weight).tobytes()
        start += length
    return bytes(out)


if __name__ == '__main__':
    width, height, rate, count = probe(source)
    chroma = ((width + 1) // 2, (height + 1) // 2)
    planes = [(width, height), chroma, chroma]
    size = sum(w * h for w, h in planes)

    end = END if END is not None else count - FADE
    if end + FADE > count or START + FADE > end:
        sys.exit(f'need start + fade <= end and end + fade <= {count}')

    # First pass keeps only the frames the fade needs: the loop's opening and what follows its end.
    head, after = [], []
    for index, frame in enumerate(read_frames(source, size)):
        if START <= index < START + FADE:
            head.append(frame)
        elif end <= index < end + FADE:
            after.append(frame)

    encoder = subprocess.Popen(['ffmpeg', '-v', 'error', '-y', '-f', 'rawvideo', '-pix_fmt', 'yuv420p',
                                '-s', f'{width}x{height}', '-r', rate, '-i', '-',
                                '-an', '-c:v', 'libx264', '-profile:v', 'baseline', '-level', '4.2', '-bf', '0',
                                '-g', '60', '-crf', '18', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', output],
                               stdin=subprocess.PIPE)
    for i in range(FADE):
        encoder.stdin.write(blend(after[i], head[i], (i + 1) / (FADE + 1), planes))

    # Second pass streams the untouched rest of the loop.
    for index, frame in enumerate(read_frames(source, size)):
        if START + FADE <= index < end:
            encoder.stdin.write(frame)

    encoder.stdin.close()
    encoder.wait()
    print(f'{output}: frames {START}..{end - 1} ({end - START} frames), {FADE} faded in')
