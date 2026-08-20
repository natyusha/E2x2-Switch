#!/usr/bin/env python3
import os
from PIL import Image, ImageOps

# Map of source PNG filenames to desired base names (PS: File > Export > Layers to Files... > PNG-24 ---> PngOptimizer)
ICON_MAP = {
    "tray_icons_0000_headphones.png":        "hp",
    "tray_icons_0001_headphones_gain.png":   "hp_gain",
    "tray_icons_0002_speakers.png":          "spk",
    "tray_icons_0003_speakers_gain.png":     "spk_gain",
    "tray_icons_0004_speakers_settings.png": "app",
    "tray_icons_0005_both.png":              "both",
    "tray_icons_0006_both_gain.png":         "both_gain",
}

# Full multi-resolution size tiers matching Microsoft iconography specs (100% to 400% DPI scaling)
SIZES = [
    (16, 16),   # 100% small / tray
    (20, 20),   # 125% tray
    (24, 24),   # 150% tray
    (32, 32),   # 100% standard taskbar / 200% tray
    (36, 36),   # 225% tray
    (40, 40),   # 125% taskbar / 250% tray
    (48, 48),   # 150% taskbar / large icon
    (64, 64),   # 200% taskbar
    (72, 72),   # 225% taskbar
    (96, 96),   # 300% taskbar
    (128, 128), # Extra large list
    (256, 256)  # Jumbo icon / high-DPI master
]

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
OUTPUT_DIR = os.path.normpath(os.path.join(SCRIPT_DIR, "..", "E2x2Switch", "Assets"))
os.makedirs(OUTPUT_DIR, exist_ok=True)

def invert_preserve_alpha(img):
    """Inverts RGB channels while preserving original transparency."""
    if img.mode != "RGBA":
        img = img.convert("RGBA")
    r, g, b, a = img.split()
    rgb = Image.merge("RGB", (r, g, b))
    inv_rgb = ImageOps.invert(rgb)
    inv_r, inv_g, inv_b = inv_rgb.split()
    return Image.merge("RGBA", (inv_r, inv_g, inv_b, a))

for src_file, base_name in ICON_MAP.items():
    src_path = os.path.join(SCRIPT_DIR, src_file)
    if not os.path.exists(src_path):
        print(f"Skipping {src_file} (not found)")
        continue

    img_white = Image.open(src_path).convert("RGBA")
    img_dark  = invert_preserve_alpha(img_white)

    # Save White variant (for Dark taskbars)
    path_white = os.path.join(OUTPUT_DIR, f"{base_name}_light.ico")
    img_white.save(path_white, format="ICO", sizes=SIZES)

    # Save Dark variant (for Light taskbars)
    path_dark = os.path.join(OUTPUT_DIR, f"{base_name}_dark.ico")
    img_dark.save(path_dark, format="ICO", sizes=SIZES)

    print(f"Generated: {base_name}_light.ico & {base_name}_dark.ico")

print(f"\nAll .ico files generated in: {OUTPUT_DIR}")