# Generates the original Game Center achievement images (1024 x 1024 PNG, no transparency) for Retro Sk8.
# Run from the project folder: python3 Tools/make_badges.py   ->   AppStoreAssets/Achievements/<id>.png
import math, os, re
from PIL import Image, ImageDraw, ImageFont
INK=(18,19,23); CORAL=(255,89,77); TAPE=(242,194,48); CREAM=(242,232,209); TEAL=(31,199,181); VIOLET=(150,90,230)

# id, title (from Core/Achievements.cs), colour, glyph
BADGES = [
    ("class_dismissed", "CLASS DISMISSED", TEAL, "star"),
    ("first_bank", "MONEY IN THE BANK", TAPE, "coin"),
    ("line_10k", "TEN GRAND LINE", CORAL, "bars1"),
    ("line_50k", "FIFTY GRAND LINE", VIOLET, "bars3"),
    ("run_100k", "SIX FIGURES", TAPE, "bolt"),
    ("spin_540", "WASHING MACHINE", TEAL, "spiral"),
    ("gap_hunter", "GAP HUNTER", CORAL, "gap"),
    ("contractor", "ON CONTRACT", TEAL, "check"),
    ("all_contracts", "CLOSED BOOK", VIOLET, "book"),
    ("daily_regular", "REGULAR", TAPE, "sun"),
    ("tourist", "TOURIST", CORAL, "pin"),
    ("double_feature", "DOUBLE FEATURE", VIOLET, "film"),
]

def font(size):
    for path in ["/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", "/System/Library/Fonts/Supplemental/Arial Bold.ttf", "/Library/Fonts/Arial Bold.ttf"]:
        if os.path.exists(path): return ImageFont.truetype(path, size)
    return ImageFont.load_default()

def glyph(d, kind, c, r, col):
    cx, cy = c
    w = int(r * 0.12)
    if kind == "star":
        pts = [(cx + r * math.cos(math.radians(-90 + i * 36)) * (1 if i % 2 == 0 else 0.45), cy + r * math.sin(math.radians(-90 + i * 36)) * (1 if i % 2 == 0 else 0.45)) for i in range(10)]
        d.polygon(pts, fill=col)
    elif kind == "coin":
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=col); d.ellipse([cx - r * 0.65, cy - r * 0.65, cx + r * 0.65, cy + r * 0.65], outline=INK, width=w)
    elif kind in ("bars1", "bars3"):
        n = 3 if kind == "bars3" else 2
        for i in range(n + 1):
            h = r * (0.6 + 0.7 * i / max(1, n)); x = cx - r + i * (2 * r / (n + 1))
            d.rectangle([x, cy + r - h, x + r * 0.42, cy + r], fill=col)
    elif kind == "bolt":
        d.polygon([(cx + r * 0.2, cy - r), (cx - r * 0.6, cy + r * 0.15), (cx - r * 0.05, cy + r * 0.15), (cx - r * 0.25, cy + r), (cx + r * 0.6, cy - r * 0.2), (cx + r * 0.05, cy - r * 0.2)], fill=col)
    elif kind == "spiral":
        pts = [(cx + (r * t / 540) * math.cos(math.radians(t)), cy + (r * t / 540) * math.sin(math.radians(t))) for t in range(0, 541, 6)]
        d.line(pts, fill=col, width=w, joint="curve")
    elif kind == "gap":
        d.rectangle([cx - r, cy + r * 0.3, cx - r * 0.35, cy + r], fill=col); d.rectangle([cx + r * 0.35, cy + r * 0.3, cx + r, cy + r], fill=col)
        d.arc([cx - r * 0.75, cy - r * 0.6, cx + r * 0.75, cy + r * 0.9], 200, 340, fill=col, width=w)
    elif kind == "check":
        d.line([(cx - r * 0.8, cy), (cx - r * 0.2, cy + r * 0.6), (cx + r * 0.85, cy - r * 0.6)], fill=col, width=int(w * 1.6), joint="curve")
    elif kind == "book":
        d.rectangle([cx - r, cy - r * 0.7, cx - r * 0.05, cy + r * 0.8], fill=col); d.rectangle([cx + r * 0.05, cy - r * 0.7, cx + r, cy + r * 0.8], fill=col)
    elif kind == "sun":
        d.ellipse([cx - r * 0.5, cy - r * 0.5, cx + r * 0.5, cy + r * 0.5], fill=col)
        for i in range(8):
            a = math.radians(i * 45); d.line([(cx + r * 0.7 * math.cos(a), cy + r * 0.7 * math.sin(a)), (cx + r * math.cos(a), cy + r * math.sin(a))], fill=col, width=w)
    elif kind == "pin":
        d.ellipse([cx - r * 0.6, cy - r, cx + r * 0.6, cy + r * 0.2], fill=col); d.polygon([(cx - r * 0.5, cy - r * 0.2), (cx + r * 0.5, cy - r * 0.2), (cx, cy + r)], fill=col)
        d.ellipse([cx - r * 0.22, cy - r * 0.62, cx + r * 0.22, cy - r * 0.18], fill=INK)

    elif kind == "film":
        # two film frames side by side, with sprocket holes top and bottom
        for sx in (-1, 1):
            x0 = cx + (sx - 1) * r * 0.5 + (0.04 * r if sx > 0 else -0.04 * r); x1 = x0 + r * 0.92
            d.rectangle([x0, cy - r * 0.8, x1, cy + r * 0.8], fill=col)
            d.rectangle([x0 + r * 0.12, cy - r * 0.5, x1 - r * 0.12, cy + r * 0.5], fill=INK)
            for i in range(3):
                hx = x0 + r * (0.14 + i * 0.27)
                d.rectangle([hx, cy - r * 0.72, hx + r * 0.12, cy - r * 0.6], fill=INK)
                d.rectangle([hx, cy + r * 0.6, hx + r * 0.12, cy + r * 0.72], fill=INK)

def badge(title, col, kind):
    S = 1024
    img = Image.new("RGB", (S, S), INK)
    d = ImageDraw.Draw(img)
    # tape stripes behind (the game's motif)
    slope = math.tan(math.radians(14))
    for c0, t, cc in [(0.62, 0.16, col), (0.8, 0.05, CREAM)]:
        cy = S * c0; th = S * t
        d.polygon([(-S, cy - th / 2 + slope * S), (2 * S, cy - th / 2 - slope * 2 * S), (2 * S, cy + th / 2 - slope * 2 * S), (-S, cy + th / 2 + slope * S)], fill=cc)
    # medallion
    r = S * 0.34
    d.ellipse([S / 2 - r - 18, S * 0.42 - r - 18, S / 2 + r + 18, S * 0.42 + r + 18], fill=CREAM)
    d.ellipse([S / 2 - r, S * 0.42 - r, S / 2 + r, S * 0.42 + r], fill=INK)
    glyph(d, kind, (S / 2, S * 0.42), r * 0.55, col)
    # title
    f = font(78)
    while d.textlength(title, font=f) > S * 0.9 and f.size > 40: f = font(f.size - 4)
    w = d.textlength(title, font=f)
    d.rectangle([0, S * 0.84, S, S], fill=INK)
    d.text(((S - w) / 2, S * 0.865), title, font=f, fill=CREAM)
    return img

if __name__ == "__main__":
    out = os.path.join("AppStoreAssets", "Achievements")
    os.makedirs(out, exist_ok=True)
    for aid, title, col, kind in BADGES:
        badge(title, col, kind).save(os.path.join(out, aid + ".png"), optimize=True)
        print("wrote", os.path.join(out, aid + ".png"))
