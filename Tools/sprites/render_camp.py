"""
Builds every encampment building and prop and renders its sprite.

Run through run_camp.sh, which launches Blender headless:

    blender -b --factory-startup --python render_camp.py -- --out <dir> [--only Farm]

Every piece renders onto the same canvas with the middle of its footprint on the same pixel,
so Unity imports them all with one pivot and places each on its footprint's centre.
"""

import argparse
import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import build_and_render as studio  # noqa: E402
import camp  # noqa: E402
import pixelize  # noqa: E402
import units  # noqa: E402

CANVAS = (256, 256)

# Where a piece's footprint centre lands, from the canvas's bottom-left. A 3x3 footprint is a
# 180x90 diamond, so its near corner sits 3 pixels above the bottom edge. Unity's importer uses
# the same numbers as the pivot (UiArtPostprocessor.CampPivot).
GROUND_PIXEL = (128, 48)

# Same density as the unit figures: one pixel is 0.1 Blender units.
ORTHO = CANVAS[1] * 0.1


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", required=True)
    parser.add_argument("--only", default=None)
    return parser.parse_args(argv)


def main():
    args = parse_args()
    out_dir = os.path.abspath(args.out)
    os.makedirs(out_dir, exist_ok=True)
    scratch = os.path.join(HERE, "out", "camp")
    os.makedirs(scratch, exist_ok=True)

    for name, build in camp.PIECES:
        if args.only and args.only != name:
            continue

        bpy.ops.wm.read_factory_settings(use_empty=True)
        scene = studio.setup_scene(CANVAS)
        kit = units.Kit()
        build(kit)
        studio.add_sun(scene)
        camera = studio.add_camera(scene, "CampCamera", studio.BODY_ROTATION, ORTHO, (0.0, 0.0, 0.0), GROUND_PIXEL, CANVAS)

        raw = os.path.join(scratch, name + ".png")
        studio.render(scene, camera, CANVAS, raw)
        studio.render_groups(scene, camera, CANVAS, raw.replace(".png", "_groups.png"))
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(scratch, name + ".blend"))

        outline = tuple(round(c * 255) for c in units.hex_srgb(units.INK))
        worst = pixelize.finish(raw, raw.replace(".png", "_groups.png"), os.path.join(out_dir, name + ".png"),
                                kit.tones, kit.shadow_of, kit.inline, kit.outlined, kit.holds, outline)
        print("SPRITE %s palette=%d worst_snap=%d" % (name, len(kit.tones) + 1, worst))


if __name__ == "__main__":
    main()
