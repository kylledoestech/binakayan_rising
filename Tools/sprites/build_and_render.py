"""
Builds every unit figure and renders its board sprite and HUD portrait.

Run through run.sh, which launches Blender headless:

    blender -b --factory-startup --python build_and_render.py -- --out <dir> [--only Marksman]

Each unit gets a fresh empty scene, so no part, material or light leaks between figures.
Raw renders and one .blend per unit go to out/ next to this script for inspection; the
finished pixel-art PNGs go to <dir>/<ArchetypeId>/{body,portrait}.png.
"""

import argparse
import math
import os
import sys

import bpy
from mathutils import Euler, Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import pixelize  # noqa: E402
import units  # noqa: E402

BODY_SIZE = (48, 64)
PORTRAIT_SIZE = (24, 24)

# One body pixel is 0.1 Blender units. Every figure shares this scale, so a tall hat makes
# a taller sprite instead of a smaller unit.
BODY_ORTHO = BODY_SIZE[1] * 0.1

# Where the figure's ground point (the world origin) lands, in pixels from the sprite's
# bottom-left corner. The rows underneath hold the toes and their outline. Unity's importer
# uses the same numbers as the sprite pivot, so a unit's feet sit exactly on its cell.
GROUND_PIXEL = (24, 5)

# Dimetric: 60 degrees off vertical gives the 2:1 diamond the board tiles are drawn at.
BODY_ROTATION = (60.0, 0.0, 45.0)

# The portrait looks at the face from lower down, close to eye level.
PORTRAIT_ROTATION = (78.0, 0.0, 45.0)
PORTRAIT_ORTHO = 3.5
PORTRAIT_TARGET = (0.0, 0.0, units.HEAD_Z + 0.3)

# Figures are modelled facing +X, which the camera sees 45 degrees off, nearly in profile.
# Turning them this far toward the viewer shows both eyes while still reading as facing
# right, which is the direction Unity's flipX mirrors.
FIGURE_TURN = -30.0


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", required=True)
    parser.add_argument("--only", default=None)
    return parser.parse_args(argv)


def setup_scene(size):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x, scene.render.resolution_y = size
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True

    # Filter size 0 is what makes EEVEE's coverage binary: a pixel is the figure or it is
    # empty, with no anti-aliased fringe for the palette snap to misread.
    scene.render.filter_size = 0.0
    scene.render.dither_intensity = 0.0
    scene.eevee.taa_render_samples = 1

    # Standard maps emission straight to sRGB, so a material's hex colour is the pixel colour.
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.view_settings.exposure = 0.0
    scene.view_settings.gamma = 1.0
    scene.display_settings.display_device = "sRGB"

    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    return scene


def add_sun(scene):
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
    sun.data.energy = math.pi
    sun.data.angle = 0.0
    scene.collection.objects.link(sun)

    # From the viewer's upper left, the usual key light direction for board art.
    toward_light = Vector((0.2, -1.0, 1.3)).normalized()
    sun.rotation_mode = "QUATERNION"
    sun.rotation_quaternion = (-toward_light).to_track_quat("-Z", "Y")


def add_camera(scene, name, rotation_degrees, ortho_scale, target, target_pixel, size):
    camera = bpy.data.objects.new(name, bpy.data.cameras.new(name))
    scene.collection.objects.link(camera)
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = ortho_scale
    camera.data.clip_start = 0.1
    camera.data.clip_end = 100.0

    rotation = Euler(tuple(math.radians(a) for a in rotation_degrees), "XYZ").to_matrix()
    right = rotation @ Vector((1.0, 0.0, 0.0))
    up = rotation @ Vector((0.0, 1.0, 0.0))
    forward = rotation @ Vector((0.0, 0.0, -1.0))

    # Shift the camera so the target lands on target_pixel rather than the frame centre.
    pixel = ortho_scale / max(size)
    offset_x = (target_pixel[0] - size[0] * 0.5) * pixel
    offset_y = (target_pixel[1] - size[1] * 0.5) * pixel
    camera.location = Vector(target) - (right * offset_x) - (up * offset_y) - (forward * 30.0)
    camera.rotation_euler = Euler(tuple(math.radians(a) for a in rotation_degrees), "XYZ")
    return camera


def turn_figure(degrees):
    # Parts set their scale and rotation as properties; matrix_world only reflects them after
    # a depsgraph update, and composing onto a stale matrix would reset every part to a cube.
    bpy.context.view_layer.update()
    turn = Matrix.Rotation(math.radians(degrees), 4, "Z")
    for obj in bpy.context.scene.objects:
        if obj.type == "MESH":
            obj.matrix_world = turn @ obj.matrix_world


def render_groups(scene, camera, size, path):
    """Renders each part group as a flat colour whose red channel is the group index + 1.

    Raw view transform writes the emission value straight to the file, so the index comes
    back exactly and pixelize.py can find where one part crosses another.
    """
    material = bpy.data.materials.new("GroupId")
    nodes = material.node_tree.nodes
    nodes.clear()
    info = nodes.new("ShaderNodeObjectInfo")
    emission = nodes.new("ShaderNodeEmission")
    output = nodes.new("ShaderNodeOutputMaterial")
    material.node_tree.links.new(info.outputs["Color"], emission.inputs["Color"])
    material.node_tree.links.new(emission.outputs["Emission"], output.inputs["Surface"])

    for obj in scene.objects:
        if obj.type == "MESH":
            obj.color = ((obj["group"] + 1) / 255.0, 0.0, 0.0, 1.0)

    layer = bpy.context.view_layer
    layer.material_override = material
    scene.view_settings.view_transform = "Raw"
    render(scene, camera, size, path)
    scene.view_settings.view_transform = "Standard"
    layer.material_override = None


def render(scene, camera, size, path):
    scene.camera = camera
    scene.render.resolution_x, scene.render.resolution_y = size
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def main():
    args = parse_args()
    out_dir = os.path.abspath(args.out)
    scratch = os.path.join(HERE, "out")
    os.makedirs(os.path.join(scratch, "raw"), exist_ok=True)

    for archetype, build in units.UNITS:
        if args.only and args.only != archetype:
            continue

        bpy.ops.wm.read_factory_settings(use_empty=True)
        scene = setup_scene(BODY_SIZE)
        kit = units.Kit()
        build(kit)
        turn_figure(FIGURE_TURN)
        add_sun(scene)

        body_camera = add_camera(scene, "BodyCamera", BODY_ROTATION, BODY_ORTHO,
                                 (0.0, 0.0, 0.0), GROUND_PIXEL, BODY_SIZE)
        portrait_camera = add_camera(scene, "PortraitCamera", PORTRAIT_ROTATION, PORTRAIT_ORTHO,
                                     PORTRAIT_TARGET, (PORTRAIT_SIZE[0] * 0.5, PORTRAIT_SIZE[1] * 0.5),
                                     PORTRAIT_SIZE)

        raw_body = os.path.join(scratch, "raw", archetype + "_body.png")
        raw_portrait = os.path.join(scratch, "raw", archetype + "_portrait.png")
        render(scene, body_camera, BODY_SIZE, raw_body)
        render(scene, portrait_camera, PORTRAIT_SIZE, raw_portrait)
        render_groups(scene, body_camera, BODY_SIZE, raw_body.replace(".png", "_groups.png"))
        render_groups(scene, portrait_camera, PORTRAIT_SIZE, raw_portrait.replace(".png", "_groups.png"))
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(scratch, archetype + ".blend"))

        unit_dir = os.path.join(out_dir, archetype)
        os.makedirs(unit_dir, exist_ok=True)
        outline = tuple(round(c * 255) for c in units.hex_srgb(units.INK))
        for raw, name in ((raw_body, "body.png"), (raw_portrait, "portrait.png")):
            worst = pixelize.finish(raw, raw.replace(".png", "_groups.png"), os.path.join(unit_dir, name),
                                    kit.tones, kit.shadow_of, kit.inline, kit.outlined, kit.holds, outline)
            print("SPRITE %s/%s palette=%d worst_snap=%d" % (archetype, name, len(kit.tones) + 1, worst))


main()
