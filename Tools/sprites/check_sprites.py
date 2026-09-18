#!/usr/bin/env python3
"""
Checks the finished unit sprites and writes an enlarged contact sheet for review.

Reads the PNGs from disk with Pillow, independently of the Blender code that wrote them,
so a bug in pixelize.py cannot vouch for itself.

    check_sprites.py <units dir> [--sheet out/sheet.png]
"""

import argparse
import os
import sys

import numpy as np
from PIL import Image

EXPECTED = ["Marksman", "Engineer", "Evangelista", "Aguinaldo", "Vanguard", "SpanishRegular",
            "FieldMedic", "MagdaloInfantry", "MagdiwangInfantry",
            "Tomas", "Farmer", "Miner", "Trader", "DrillSergeant"]
SIZES = {"body.png": (48, 64), "portrait.png": (24, 24)}
MAX_COLOURS = 16
SCALE = 8


def check(path, size):
    problems = []
    image = Image.open(path)
    pixels = np.array(image.convert("RGBA"))
    alpha = pixels[..., 3]

    if image.size != size:
        problems.append("size %s, expected %s" % (image.size, size))
    if np.any((alpha > 0) & (alpha < 255)):
        problems.append("%d partially transparent pixels" % int(np.sum((alpha > 0) & (alpha < 255))))

    colours = len(np.unique(pixels[alpha == 255][:, :3], axis=0))
    if colours > MAX_COLOURS:
        problems.append("%d colours, limit %d" % (colours, MAX_COLOURS))

    solid = alpha == 255
    if not solid.any():
        return problems + ["empty"], colours

    if path.endswith("body.png"):
        # Rows count from the top in Pillow. The figure must reach the bottom few rows (its
        # feet) and must not be clipped by the other three edges.
        rows = np.where(solid.any(axis=1))[0]
        if rows.max() < size[1] - 3:
            problems.append("lowest pixel on row %d from the bottom" % (size[1] - 1 - rows.max()))
        if solid[0, :].any() or solid[:, 0].any() or solid[:, -1].any():
            problems.append("touches the top or side edge (clipped)")

    # The outline colour must be the most common edge colour of the silhouette.
    empty = ~solid
    edge = np.zeros_like(solid)
    edge[1:, :] |= empty[:-1, :]
    edge[:-1, :] |= empty[1:, :]
    edge[:, 1:] |= empty[:, :-1]
    edge[:, :-1] |= empty[:, 1:]
    edge &= solid
    edge_colours, counts = np.unique(pixels[edge][:, :3], axis=0, return_counts=True)
    if counts.max() < 0.6 * counts.sum():
        problems.append("no dominant outline colour on the silhouette edge")

    return problems, colours


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("units_dir")
    parser.add_argument("--sheet", default=None)
    args = parser.parse_args()

    failures = 0
    tiles = []
    for archetype in EXPECTED:
        row = []
        for name, size in SIZES.items():
            path = os.path.join(args.units_dir, archetype, name)
            if not os.path.exists(path):
                print("FAIL %s/%s missing" % (archetype, name))
                failures += 1
                continue
            problems, colours = check(path, size)
            status = "FAIL" if problems else "ok  "
            print("%s %s/%s colours=%d %s" % (status, archetype, name, colours, "; ".join(problems)))
            failures += len(problems) > 0
            row.append(Image.open(path).convert("RGBA"))
        tiles.append(row)

    if args.sheet:
        cell_w = 48 * SCALE + 24 * SCALE + 3 * 16
        cell_h = 64 * SCALE + 16
        rows = (len(tiles) + 2) // 3
        sheet = Image.new("RGBA", (cell_w * 3, cell_h * rows), (205, 190, 150, 255))
        for i, row in enumerate(tiles):
            x0 = (i % 3) * cell_w + 16
            y0 = (i // 3) * cell_h + 8
            x = x0
            for tile in row:
                big = tile.resize((tile.width * SCALE, tile.height * SCALE), Image.NEAREST)
                sheet.alpha_composite(big, (x, y0))
                x += big.width + 16
        os.makedirs(os.path.dirname(os.path.abspath(args.sheet)), exist_ok=True)
        sheet.save(args.sheet)
        print("sheet", args.sheet)

    sys.exit(1 if failures else 0)


if __name__ == "__main__":
    main()
