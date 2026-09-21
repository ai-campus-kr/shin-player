"""Rebuild the monochrome S + play mark at native Windows icon sizes."""
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / 'src/ShinPlayer/Assets'
SIZES = (16, 20, 24, 32, 40, 48, 64, 96, 128, 256)

def render(size):
    scale = 8
    image = Image.new('RGBA', (size * scale, size * scale))
    draw = ImageDraw.Draw(image)
    unit = size * scale / 256
    def points(coords): return [(round(x * unit), round(y * unit)) for x, y in coords]
    draw.rounded_rectangle((0, 0, size * scale - 1, size * scale - 1), radius=round(48 * unit), fill='black')
    # A compact angular S pairs with an unmistakable, separate play triangle.
    draw.polygon(points([(55,57),(144,57),(144,86),(86,86),(86,112),(118,112),
                         (144,137),(144,175),(120,199),(43,199),(43,170),
                         (112,170),(112,146),(79,146),(55,122)]), fill='white')
    draw.polygon(points([(162,84),(220,128),(162,172)]), fill='white')
    return image.resize((size, size), Image.Resampling.LANCZOS)

if __name__ == '__main__':
    ASSETS.mkdir(parents=True, exist_ok=True)
    icons = [render(size) for size in SIZES]
    icons[-1].save(ASSETS / 'ShinPlayer.ico', sizes=[(s,s) for s in SIZES], append_images=icons[:-1])
    icons[-1].save(ASSETS / 'ShinPlayer.png')
    out = ROOT / 'artifacts/icon-preview'
    out.mkdir(parents=True, exist_ok=True)
    sheet = Image.new('RGB',(840,330),'#eeeeee')
    for x, size in zip((24,320,405,470,525,575,625,685), (256,64,48,32,24,20,16,96)):
        icon = render(size); sheet.paste(icon,(x,30),icon)
    sheet.save(out / 'sizes.png')
    print('Created monochrome S + play icon:', ', '.join(map(str, SIZES)))
