#!/usr/bin/env python3
"""
Checks the finished encampment sprites and writes an enlarged contact sheet for review.

Reads the PNGs with Pillow, independently of the Blender code that wrote them.

    check_camp.py <encampment dir> [--sheet out/camp_sheet.png]
"""

import argparse
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

CANVAS = (256, 256)
GROUND_PIXEL = (128, 48)
SCALE = 2


def check(path):
    problems = []
    image = Image.open(path)
    pixels = np.array(image.convert("RGBA"))
    alpha = pixels[..., 3]
    if image.size != CANVAS:
        problems.append("size %s, expected %s" % (image.size, CANVAS))
    if np.any((alpha > 0) & (alpha < 255)):
        problems.append("%d partially transparent pixels" % int(np.sum((alpha > 0) & (alpha < 255))))

    solid = alpha == 255
    if not solid.any():
        return problems + ["empty"]
    if solid[0, :].any() or solid[:, 0].any() or solid[:, -1].any() or solid[-1, :].any():
        problems.append("touches an edge of the canvas (clipped)")

    # Pillow rows count from the top; the ground pixel counts from the bottom.
    ground_row = CANVAS[1] - 1 - GROUND_PIXEL[1]
    rows = np.where(solid.any(axis=1))[0]
    if rows.max() < ground_row:
        problems.append("nothing reaches the ground point; lowest row is %d above it" % (ground_row - rows.max()))
    return problems


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("camp_dir")
    parser.add_argument("--sheet", default=None)
    args = parser.parse_args()

    names = sorted(f for f in os.listdir(args.camp_dir) if f.endswith(".png"))
    failures = 0
    tiles = []
    for name in names:
        problems = check(os.path.join(args.camp_dir, name))
        status = "FAIL" if problems else "ok  "
        failures += bool(problems)
        print("%s %s %s" % (status, name, "; ".join(problems)))
        tiles.append((name, Image.open(os.path.join(args.camp_dir, name)).convert("RGBA")))

    if args.sheet and tiles:
        columns = 6
        cell = (CANVAS[0] * SCALE, CANVAS[1] * SCALE + 20)
        rows = (len(tiles) + columns - 1) // columns
        sheet = Image.new("RGBA", (cell[0] * columns, cell[1] * rows), (58, 70, 44, 255))
        draw = ImageDraw.Draw(sheet)
        for i, (name, image) in enumerate(tiles):
            x = (i % columns) * cell[0]
            y = (i // columns) * cell[1]
            big = image.resize((CANVAS[0] * SCALE, CANVAS[1] * SCALE), Image.NEAREST)
            sheet.alpha_composite(big, (x, y))
            gx = x + GROUND_PIXEL[0] * SCALE
            gy = y + (CANVAS[1] - GROUND_PIXEL[1]) * SCALE
            draw.line([(gx - 6, gy), (gx + 6, gy)], fill=(255, 0, 255, 255))
            draw.line([(gx, gy - 6), (gx, gy + 6)], fill=(255, 0, 255, 255))
            draw.text((x + 6, y + CANVAS[1] * SCALE + 4), name, fill=(255, 255, 255, 255))
        os.makedirs(os.path.dirname(os.path.abspath(args.sheet)), exist_ok=True)
        sheet.save(args.sheet)
        print("sheet: %s" % args.sheet)

    sys.exit(1 if failures else 0)


if __name__ == "__main__":
    main()
