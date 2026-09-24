"""
The title splash: Katipunan soldiers holding a Cavite trench line at dusk, the red flag up.

Built from primitives in code, like the unit sprites (Tools/sprites/units.py), so a change is a
reviewable diff rather than an edited binary. The scene is rendered small with EEVEE, snapped to
a limited palette, and scaled up with nearest-neighbour so it sits with the game's pixel art.

Run headless:

    blender -b --factory-startup -P Tools/splash/splash.py -- --out Assets/_Project/Resources/Splash/splash.png

The game loads the result from Resources/Splash/splash (see UI/Shell/SplashScreen.cs); the title
and the prompt are drawn over it in the game, not baked in, so both stay localized.
"""

import math
import os
import sys

import bpy
import numpy as np
from mathutils import Euler, Vector

# Rendered size. Scaled by UPSCALE to the 1920x1080 the interface is authored against.
WIDTH = 480
HEIGHT = 270
UPSCALE = 4

# How far the trench floor sits below the field.
TRENCH_FLOOR = -0.75

# The largest number of colours the finished picture keeps.
PALETTE_SIZE = 40


# ----------------------------------------------------------------------------- colour


def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def hex_rgba(value, alpha=1.0):
    value = value.lstrip("#")
    return tuple(srgb_to_linear(int(value[i:i + 2], 16) / 255.0) for i in (0, 2, 4)) + (alpha,)


# Dusk over Manila Bay: warm at the horizon, violet overhead.
SKY_HORIZON = "#F6A24E"
SKY_LOW = "#E0714F"
SKY_MID = "#9C4A6B"
SKY_HIGH = "#3A2B55"
SKY_TOP = "#1C1733"

EARTH = "#6B4A33"
EARTH_DARK = "#4A3224"
FIELD = "#5E6B34"
SEA = "#C9785A"
HILLS = "#4B3550"
HILLS_NEAR = "#3A2A3C"
PALM = "#2A2230"
BAMBOO = "#A88A4A"
SANDBAG = "#8C7250"

SKIN = "#9A6440"
CAMISA = "#E6DCC4"
TROUSERS = "#7A1F1F"
SALAKOT = "#C8A060"
WOOD = "#5A3A22"
STEEL = "#5E626C"
FLAG_RED = "#C0262D"
FLAG_WHITE = "#F2EEE0"


# ----------------------------------------------------------------------------- helpers

materials = {}


def material(name, colour, emission=0.0):
    if name in materials:
        return materials[name]

    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    bsdf = nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = hex_rgba(colour)
    bsdf.inputs["Roughness"].default_value = 0.9
    if emission > 0.0:
        bsdf.inputs["Emission Color"].default_value = hex_rgba(colour)
        bsdf.inputs["Emission Strength"].default_value = emission

    materials[name] = mat
    return mat


def place(obj, mat, location=None, rotation=None, scale=None):
    if location is not None:
        obj.location = location
    if rotation is not None:
        obj.rotation_euler = Euler([math.radians(a) for a in rotation])
    if scale is not None:
        obj.scale = scale
    obj.data.materials.append(mat)
    for poly in obj.data.polygons:
        poly.use_smooth = False
    return obj


def cube(mat, location, scale, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1.0)
    return place(bpy.context.object, mat, location, rotation, scale)


def cylinder(mat, location, radius, depth, rotation=(0, 0, 0), vertices=8):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth)
    return place(bpy.context.object, mat, location, rotation)


def cone(mat, location, radius, depth, rotation=(0, 0, 0), vertices=10, top=0.0):
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=radius, radius2=top, depth=depth)
    return place(bpy.context.object, mat, location, rotation)


def sphere(mat, location, radius, scale=(1, 1, 1), subdivisions=1):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=radius)
    return place(bpy.context.object, mat, location, None, scale)


def limb(mat, start, end, radius, vertices=6):
    """A cylinder from start to end."""
    start = Vector(start)
    end = Vector(end)
    mid = (start + end) / 2.0
    direction = end - start
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=direction.length)
    obj = bpy.context.object
    obj.location = mid
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(direction.normalized())
    obj.data.materials.append(mat)
    return obj


# ----------------------------------------------------------------------------- scene


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def sky():
    world = bpy.data.worlds.new("Dusk")
    bpy.context.scene.world = world
    world.use_nodes = True
    nodes = world.node_tree.nodes
    links = world.node_tree.links
    nodes.clear()

    coords = nodes.new("ShaderNodeTexCoord")
    split = nodes.new("ShaderNodeSeparateXYZ")
    ramp_in = nodes.new("ShaderNodeMapRange")
    ramp_in.inputs["From Min"].default_value = -0.02
    ramp_in.inputs["From Max"].default_value = 0.55
    ramp = nodes.new("ShaderNodeValToRGB")
    stops = ramp.color_ramp.elements
    stops[0].position = 0.0
    stops[0].color = hex_rgba(SKY_HORIZON)
    stops[1].position = 1.0
    stops[1].color = hex_rgba(SKY_TOP)
    for position, colour in ((0.10, SKY_LOW), (0.30, SKY_MID), (0.62, SKY_HIGH)):
        stop = stops.new(position)
        stop.color = hex_rgba(colour)
    ramp.color_ramp.interpolation = "LINEAR"

    background = nodes.new("ShaderNodeBackground")
    background.inputs["Strength"].default_value = 1.0
    output = nodes.new("ShaderNodeOutputWorld")

    links.new(coords.outputs["Generated"], split.inputs["Vector"])
    links.new(split.outputs["Z"], ramp_in.inputs["Value"])
    links.new(ramp_in.outputs["Result"], ramp.inputs["Fac"])
    links.new(ramp.outputs["Color"], background.inputs["Color"])
    links.new(background.outputs["Background"], output.inputs["Surface"])


def lights():
    # The sun sits low over the bay, in front of the soldiers: they are lit from behind.
    bpy.ops.object.light_add(type="SUN", location=(0, 0, 10))
    sun = bpy.context.object
    sun.data.energy = 3.2
    sun.data.color = (1.0, 0.62, 0.36)
    sun.data.angle = math.radians(2.0)
    sun.rotation_euler = Euler((math.radians(-78), 0, math.radians(200)))

    # A cool fill from the camera side keeps faces and cloth from going flat black.
    bpy.ops.object.light_add(type="SUN", location=(0, 0, 10))
    fill = bpy.context.object
    fill.data.energy = 0.9
    fill.data.color = (0.62, 0.55, 0.85)
    fill.rotation_euler = Euler((math.radians(55), 0, math.radians(-25)))

    # The sun's disc, a flat bright shape just above the far hills.
    disc = material("SunDisc", "#FFD27A", emission=2.2)
    bpy.ops.mesh.primitive_circle_add(vertices=16, radius=5.5, fill_type="NGON", location=(4, 150, 8.5), rotation=(math.radians(90), 0, 0))
    bpy.context.object.data.materials.append(disc)


def land():
    earth = material("Earth", EARTH)
    earth_dark = material("EarthDark", EARTH_DARK)
    field = material("Field", FIELD)
    sea = material("Sea", SEA, emission=0.6)
    hills = material("Hills", HILLS)
    hills_near = material("HillsNear", HILLS_NEAR)

    # The trench floor, dug a metre down, and the near bank on the camera side of it: from
    # behind, the soldiers show from the waist up over the bank, which is what reads as a trench.
    cube(earth_dark, (0, 0.2, -1.0), (60, 3.0, 0.5))
    cube(earth_dark, (0, -5.0, -0.5), (60, 7.0, 1.0))
    cube(earth, (0, -1.3, -0.12), (60, 0.9, 0.3), rotation=(10, 0, 0))

    # The parapet: a long bank of earth thrown up in front of the trench, sloped outward.
    cube(earth, (0, 1.9, 0.12), (60, 1.6, 0.36))
    cube(earth, (0, 3.1, 0.05), (60, 1.6, 0.2), rotation=(-8, 0, 0))


    # The open field beyond, the rice paddies and the bay.
    cube(field, (0, 40, -0.1), (200, 72, 0.2))
    cube(sea, (0, 115, -0.05), (400, 80, 0.1))

    # Far hills across the bay, low and blue-violet in the haze.
    for x, s, h in ((-70, 26, 5.5), (-30, 20, 4.0), (25, 30, 6.0), (70, 22, 4.5), (110, 30, 5.0)):
        sphere(hills, (x, 150, 0), 1.0, scale=(s, 6, h), subdivisions=1)

    # Nearer tree line along the far edge of the field.
    for i in range(22):
        x = -80 + i * 7.5 + math.sin(i * 1.7) * 2.0
        sphere(hills_near, (x, 74, 0.0), 1.0, scale=(4.0 + (i % 3), 2.5, 2.2 + (i % 4) * 0.5), subdivisions=1)


def palm(x, y, height, lean):
    trunk = material("Palm", PALM)
    top = Vector((x + lean, y, height))
    limb(trunk, (x, y, 0), top, 0.18 + height * 0.01, vertices=5)
    for i in range(7):
        angle = i * (360.0 / 7.0) + 13.0
        a = math.radians(angle)
        tip = top + Vector((math.cos(a) * 2.6, math.sin(a) * 2.6, -1.3 - (i % 2) * 0.4))
        leaf = limb(trunk, top, tip, 0.2, vertices=4)
        leaf.scale = (1.4, 0.18, 1.0)


def defences():
    bamboo = material("Bamboo", BAMBOO)
    sandbag = material("Sandbag", SANDBAG)

    # Sandbags along the parapet's crest.
    for i in range(46):
        x = -22 + i * 0.95
        sphere(sandbag, (x, 1.6 + (i % 2) * 0.12, 0.42), 0.5, scale=(1.0, 0.7, 0.42), subdivisions=1)

    # Sharpened bamboo stakes out in front, angled toward the enemy.
    for i in range(24):
        x = -20 + i * 1.8
        y = 5.2 + (i % 3) * 0.35
        limb(bamboo, (x, y - 0.4, -0.1), (x + 0.1, y + 0.9, 0.75 + (i % 2) * 0.2), 0.06, vertices=5)


def soldier(x, y, facing=0.0, hat=True, weapon="rifle", arm_up=False, height=1.0):
    """A low-poly Katipunero standing in the trench, facing +Y (the enemy)."""
    skin = material("Skin", SKIN)
    camisa = material("Camisa", CAMISA)
    trousers = material("Trousers", TROUSERS)
    salakot = material("Salakot", SALAKOT)
    wood = material("Wood", WOOD)
    steel = material("Steel", STEEL)

    parts = []
    h = height
    parts.append(limb(trousers, (x - 0.14, y, 0), (x - 0.14, y, 0.85 * h), 0.12))
    parts.append(limb(trousers, (x + 0.14, y, 0), (x + 0.14, y, 0.85 * h), 0.12))
    parts.append(cylinder(camisa, (x, y, 1.2 * h), 0.3, 0.75 * h, vertices=7))
    parts.append(sphere(skin, (x, y, 1.78 * h), 0.2, subdivisions=1))
    if hat:
        parts.append(cone(salakot, (x, y, 1.98 * h), 0.52, 0.28, vertices=10))

    shoulder_l = (x - 0.34, y, 1.5 * h)
    shoulder_r = (x + 0.34, y, 1.5 * h)
    if weapon == "rifle":
        # Rifle levelled over the parapet.
        hand = (x + 0.05, y + 0.55, 1.62 * h)
        parts.append(limb(camisa, shoulder_l, hand, 0.08))
        parts.append(limb(camisa, shoulder_r, (x + 0.2, y + 0.3, 1.5 * h), 0.08))
        parts.append(limb(wood, (x + 0.15, y - 0.2, 1.62 * h), (x + 0.05, y + 1.7, 1.66 * h), 0.06, vertices=4))
    elif weapon == "bolo":
        tip_z = 2.55 if arm_up else 1.3
        hand = (x + 0.45, y + 0.1, (2.25 if arm_up else 1.0) * h)
        parts.append(limb(camisa, shoulder_r, hand, 0.08))
        parts.append(limb(camisa, shoulder_l, (x - 0.45, y, 1.0 * h), 0.08))
        blade = limb(steel, hand, (hand[0] + 0.15, hand[1] + 0.25, tip_z * h + 0.2), 0.06, vertices=4)
        blade.scale = (1.6, 0.4, 1.0)
        parts.append(blade)
    return parts


def flag_bearer(x, y):
    """The standard: a tall pole and the red flag with its white sun, streaming to the left."""
    soldier(x, y, weapon="none")
    wood = material("Wood", WOOD)
    red = material("FlagRed", FLAG_RED, emission=0.35)
    white = material("FlagWhite", FLAG_WHITE, emission=0.5)
    camisa = material("Camisa", CAMISA)

    pole_x = x + 0.4
    limb(wood, (pole_x, y + 0.1, 0.2), (pole_x, y + 0.1, 5.4), 0.05, vertices=5)
    limb(camisa, (x + 0.34, y, 1.5), (pole_x, y + 0.1, 2.0), 0.08)
    limb(camisa, (x - 0.34, y, 1.5), (pole_x, y + 0.1, 1.35), 0.08)

    # The cloth: a subdivided plane, rippled with a sine so it reads as moving in the wind.
    width, height = 2.6, 1.6
    bpy.ops.mesh.primitive_grid_add(x_subdivisions=12, y_subdivisions=4, size=1.0)
    cloth = bpy.context.object
    for v in cloth.data.vertices:
        u = v.co.x + 0.5  # 0 at the pole, 1 at the fly
        w = v.co.y + 0.5
        v.co.x = -u * width
        v.co.z = w * height - u * 0.35
        v.co.y = math.sin(u * math.pi * 2.2) * 0.28 * u
    cloth.location = (pole_x, y + 0.1, 3.75)
    cloth.data.materials.append(red)

    # The white sun at the centre of the cloth, sitting just proud of it on the camera side.
    centre_u = 0.5
    cx = pole_x - centre_u * width
    cz = 3.75 + height * 0.5 - centre_u * 0.35
    cy = y + 0.1 + math.sin(centre_u * math.pi * 2.2) * 0.28 * centre_u - 0.08
    bpy.ops.mesh.primitive_circle_add(vertices=12, radius=0.3, fill_type="NGON", location=(cx, cy, cz), rotation=(math.radians(90), 0, 0))
    bpy.context.object.data.materials.append(white)
    for i in range(8):
        a = math.radians(i * 45.0)
        start = (cx + math.cos(a) * 0.36, cy, cz + math.sin(a) * 0.36)
        end = (cx + math.cos(a) * 0.55, cy, cz + math.sin(a) * 0.55)
        limb(white, start, end, 0.045, vertices=4)


def army():
    """Left to right along the trench, on its floor a metre down. The flag stands right of
    centre, leaving the sky at the upper left clear for the title."""
    before = set(bpy.data.objects)
    soldier(-6.2, 0.9, weapon="rifle")
    soldier(-4.6, 1.0, weapon="rifle", height=0.95)
    soldier(-3.0, 0.8, weapon="bolo", arm_up=True)
    soldier(-1.4, 1.0, weapon="rifle")
    soldier(0.2, 0.9, weapon="bolo", arm_up=False, hat=False)
    flag_bearer(2.0, 0.8)
    soldier(3.8, 0.9, weapon="rifle", height=0.97)
    soldier(5.4, 1.0, weapon="bolo", arm_up=True)
    soldier(7.0, 0.9, weapon="rifle")
    for obj in set(bpy.data.objects) - before:
        obj.location.z += TRENCH_FLOOR


def camera():
    bpy.ops.object.camera_add(location=(-3.0, -10.5, 2.5))
    cam = bpy.context.object
    target = Vector((0.8, 6.0, 1.9))
    direction = target - cam.location
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = 30
    bpy.context.scene.camera = cam


def render(out_path):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = WIDTH
    scene.render.resolution_y = HEIGHT
    scene.render.resolution_percentage = 100
    scene.render.filter_size = 0.0
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"

    raw = os.path.join(os.path.dirname(out_path), "_splash_raw.png")
    scene.render.filepath = raw
    bpy.ops.render.render(write_still=True)

    image = bpy.data.images.load(raw, check_existing=False)
    pixels = np.array(image.pixels[:], dtype=np.float32).reshape(HEIGHT, WIDTH, 4)[..., :3]
    bpy.data.images.remove(image)
    os.remove(raw)

    rgb = np.round(pixels * 255.0)
    rgb = quantize(rgb, PALETTE_SIZE)

    # Nearest-neighbour upscale: each rendered pixel becomes a UPSCALE x UPSCALE block.
    big = np.repeat(np.repeat(rgb, UPSCALE, axis=0), UPSCALE, axis=1)
    out = np.concatenate([big / 255.0, np.ones(big.shape[:2] + (1,), dtype=np.float32)], axis=-1)

    result = bpy.data.images.new("splash", width=WIDTH * UPSCALE, height=HEIGHT * UPSCALE, alpha=False)
    result.colorspace_settings.name = "sRGB"
    result.pixels[:] = out.astype(np.float32).ravel()
    result.filepath_raw = out_path
    result.file_format = "PNG"
    result.save()
    print("SPLASH", out_path, WIDTH * UPSCALE, "x", HEIGHT * UPSCALE, "colours", len(np.unique(rgb.reshape(-1, 3), axis=0)))


def quantize(rgb, count, rounds=12):
    """A small k-means over the picture's colours: the pixel-art palette snap."""
    flat = rgb.reshape(-1, 3)
    unique, inverse = np.unique(flat, axis=0, return_inverse=True)
    inverse = inverse.ravel()
    weights = np.bincount(inverse).astype(np.float64)
    order = np.argsort(-weights)
    centres = unique[order[:count]].astype(np.float64)
    for _ in range(rounds):
        distance = np.linalg.norm(unique[:, None, :] - centres[None, :, :], axis=-1)
        nearest = np.argmin(distance, axis=-1)
        for k in range(len(centres)):
            chosen = nearest == k
            if chosen.any():
                centres[k] = np.average(unique[chosen], axis=0, weights=weights[chosen])

    distance = np.linalg.norm(unique[:, None, :] - centres[None, :, :], axis=-1)
    snapped = np.round(centres[np.argmin(distance, axis=-1)])
    return snapped[inverse].reshape(rgb.shape)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = os.path.abspath(argv[argv.index("--out") + 1]) if "--out" in argv else os.path.abspath("splash.png")
    os.makedirs(os.path.dirname(out), exist_ok=True)

    reset()
    sky()
    lights()
    land()
    for x, y, h, lean in ((-19, 26, 8.0, 0.6), (15, 12, 8.0, -0.9), (19, 18, 10.0, 0.5), (-28, 40, 9.0, 0.4), (30, 40, 8.5, -0.3)):
        palm(x, y, h, lean)
    defences()
    army()
    camera()
    render(out)


main()
