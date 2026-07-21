import argparse
import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter, ImageFont

SIZE = 1024
BG = (10, 10, 12, 255)
ICO_SIZES = [16, 24, 32, 48, 64, 128, 256]
PNG_SIZES = [16, 32, 48, 64, 128, 256, 512, 1024]

STOPS = [
    (0.00, (209, 79, 232)),
    (0.35, (181, 126, 220)),
    (0.70, (201, 160, 220)),
    (1.00, (238, 222, 248)),
]

FONT_CANDIDATES = [
    "C:/Windows/Fonts/segoeuil.ttf",
    "C:/Windows/Fonts/segoeui.ttf",
    "/System/Library/Fonts/HelveticaNeue.ttc",
    "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
]


def sample_gradient(t):
    t = max(0.0, min(1.0, t))
    for i in range(len(STOPS) - 1):
        t0, c0 = STOPS[i]
        t1, c1 = STOPS[i + 1]
        if t0 <= t <= t1:
            k = (t - t0) / (t1 - t0)
            return tuple(round(c0[j] + (c1[j] - c0[j]) * k) for j in range(3))
    return STOPS[-1][1]


def rounded_mask(size, radius_ratio=0.22):
    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).rounded_rectangle(
        [0, 0, size - 1, size - 1], radius=int(size * radius_ratio), fill=255
    )
    return mask


def draw_backdrop(size):
    layer = Image.new("RGBA", (size, size), BG)
    glow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(glow)
    cx, cy = size * 0.5, size * 0.62
    for i in range(60, 0, -1):
        r = size * 0.42 * (i / 60.0)
        a = int(46 * (1.0 - i / 60.0) ** 1.6)
        d.ellipse([cx - r, cy - r * 0.85, cx + r, cy + r * 0.85], fill=(154, 95, 196, a))
    glow = glow.filter(ImageFilter.GaussianBlur(size * 0.05))
    return Image.alpha_composite(layer, glow)


def draw_plume(size, seed=7):
    rng = random.Random(seed)
    layer = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)

    cx = size * 0.5
    base_y = size * 0.80
    top_y = size * 0.20
    turns = 2.35

    pool_w = size * 0.20
    for i in range(26, 0, -1):
        k = i / 26.0
        w = pool_w * k
        a = int(150 * (1.0 - k) ** 1.2)
        d.ellipse(
            [cx - w, base_y - w * 0.30, cx + w, base_y + w * 0.30],
            fill=sample_gradient(0.0) + (a,),
        )

    steps = 2600
    for i in range(steps):
        t = i / (steps - 1.0)
        y = base_y + (top_y - base_y) * (t ** 0.86)
        spread = size * 0.055 + size * 0.170 * (t ** 1.45)
        phase = t * turns * 2.0 * math.pi
        x = cx + math.sin(phase) * spread
        depth = 0.55 + 0.45 * math.cos(phase)

        radius = (size * 0.036) * (1.0 - 0.72 * t) * (0.55 + 0.45 * depth)
        radius *= 1.0 + rng.uniform(-0.16, 0.16)
        alpha = int(235 * (1.0 - 0.45 * t) * (0.45 + 0.55 * depth))
        color = sample_gradient(t) + (max(0, min(255, alpha)),)
        d.ellipse([x - radius, y - radius, x + radius, y + radius], fill=color)

    embers = 190
    for _ in range(embers):
        t = rng.uniform(0.42, 1.06)
        y = base_y + (top_y - base_y) * min(1.12, t ** 0.86)
        spread = size * 0.055 + size * 0.215 * (t ** 1.35)
        x = cx + rng.uniform(-spread, spread)
        r = size * rng.uniform(0.0035, 0.0125) * (1.0 - 0.4 * min(t, 1.0))
        a = int(rng.uniform(90, 245) * (1.0 - 0.35 * min(t, 1.0)))
        d.ellipse([x - r, y - r, x + r, y + r], fill=sample_gradient(min(t, 1.0)) + (a,))

    return layer


def compose(size, with_text=False):
    canvas = draw_backdrop(size)
    plume = draw_plume(size)

    halo = plume.filter(ImageFilter.GaussianBlur(size * 0.032))
    canvas = Image.alpha_composite(canvas, halo)
    canvas = Image.alpha_composite(canvas, halo)
    soft = plume.filter(ImageFilter.GaussianBlur(size * 0.006))
    canvas = Image.alpha_composite(canvas, soft)
    canvas = Image.alpha_composite(canvas, plume)

    if with_text:
        draw_wordmark(canvas, size)

    canvas.putalpha(rounded_mask(size))
    return canvas


def load_font(px):
    for path in FONT_CANDIDATES:
        if os.path.exists(path):
            try:
                return ImageFont.truetype(path, px)
            except OSError:
                continue
    return ImageFont.load_default()


def draw_wordmark(canvas, size):
    d = ImageDraw.Draw(canvas)
    font = load_font(int(size * 0.062))
    text = "VESPER LAUNCHER"
    try:
        box = d.textbbox((0, 0), text, font=font)
    except AttributeError:
        box = (0, 0) + d.textsize(text, font=font)
    w = box[2] - box[0]
    d.text(
        ((size - w) / 2 - box[0], size * 0.885),
        text,
        font=font,
        fill=(242, 238, 246, 226),
    )


def derive_from_source(path, size):
    src = Image.open(path).convert("RGBA")
    if src.width != src.height:
        side = min(src.width, src.height)
        left = (src.width - side) // 2
        top = (src.height - side) // 2
        src = src.crop((left, top, left + side, top + side))
    out = src.resize((size, size), Image.LANCZOS)
    out.putalpha(rounded_mask(size))
    return out


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", help="derive all sizes from an existing square image")
    parser.add_argument("--outdir", default=os.path.dirname(os.path.abspath(__file__)))
    args = parser.parse_args()

    outdir = args.outdir
    generated = os.path.join(outdir, "generated")
    os.makedirs(generated, exist_ok=True)

    if args.source:
        master = derive_from_source(args.source, SIZE)
        wordmark = master
    else:
        master = compose(SIZE, with_text=False)
        wordmark = compose(SIZE, with_text=True)

    master.save(os.path.join(outdir, "icon.png"))
    wordmark.save(os.path.join(outdir, "icon-wordmark.png"))

    for s in PNG_SIZES:
        master.resize((s, s), Image.LANCZOS).save(
            os.path.join(generated, "icon-%d.png" % s)
        )

    master.save(
        os.path.join(outdir, "icon.ico"),
        format="ICO",
        sizes=[(s, s) for s in ICO_SIZES],
    )

    print("wrote icon.png, icon-wordmark.png, icon.ico and %d sizes" % len(PNG_SIZES))


if __name__ == "__main__":
    main()
