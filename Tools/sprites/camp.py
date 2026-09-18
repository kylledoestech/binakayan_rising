"""
Encampment buildings and scenery for the sprite pipeline.

Same kit, palette rules and camera as the unit figures (units.py), so a keeper standing in a
doorway looks like they belong to the building behind them.

Every piece stands with the middle of its footprint on the origin. The footprint is measured
in camp tiles: 60 pixels wide on screen, which at the pipeline's 0.1 Blender units per pixel
makes a tile's side 6 / sqrt(2) = 4.24 units. The layout that places these sprites lives in
Assets/_Project/Scripts/Core/Content/Encampment.cs; the sizes here must match its footprints.

Axes, seen through the dimetric camera:
    +X  screen right-down   (the camp grid's -Y)    faces the viewer, in shadow
    -Y  screen left-down    (the camp grid's -X)    faces the viewer, lit
So a door the camp puts on a building's low-X side goes on its -Y face, and one on its low-Y
side goes on its +X face.
"""

import math
import random

import bmesh
import bpy
from mathutils import Vector

from units import WOOD, STEEL, KATIPUNAN_RED

TILE = 6.0 / math.sqrt(2.0)

THATCH = "#A88A4C"
THATCH_DARK = "#7E6534"
BAMBOO = "#C4A866"
BAMBOO_DARK = "#8E7440"
CANVAS = "#DCCDA4"
WHITEWASH = "#E6DCC6"
ADOBE = "#A89479"
TERRACOTTA = "#A5503A"
SOIL = "#6E5234"
EARTH = "#8C7652"
LEAF = "#5E8A3A"
LEAF_DARK = "#44662C"
STRAW = "#D2B46A"
CREAM = "#E9E2D0"
ROCK = "#857A6C"
ROCK_DARK = "#6A5F55"


def poly(kit, mat, verts, faces):
    """An arbitrary mesh part: roofs, gables and anything else a primitive cannot make."""
    mesh = bpy.data.meshes.new("poly")
    mesh.from_pydata([tuple(v) for v in verts], [], faces)
    if len(faces) > 2:
        # Closed shapes get their normals pointed outward, whatever order the faces were listed in.
        shape = bmesh.new()
        shape.from_mesh(mesh)
        bmesh.ops.recalc_face_normals(shape, faces=shape.faces)
        shape.to_mesh(mesh)
        shape.free()
    mesh.update()
    obj = bpy.data.objects.new("poly", mesh)
    bpy.context.scene.collection.objects.link(obj)
    return kit._finish(obj, mat, False)


def gable_roof(kit, mat, cx, cy, along_y, length, width, eave_z, ridge_z, overhang=0.6, caps=True):
    """
    A two-slope roof. The ridge runs along Y when along_y, else along X. Without caps the two
    triangular ends are left open for gable_ends in another material to close.
    """
    l = length * 0.5 + overhang
    w = width * 0.5 + overhang
    # Drop the eave a little further for the overhang, keeping the slope.
    drop = (ridge_z - eave_z) * overhang / (width * 0.5)
    low = eave_z - drop
    if along_y:
        verts = [(cx - w, cy - l, low), (cx - w, cy + l, low), (cx + w, cy + l, low), (cx + w, cy - l, low),
                 (cx, cy - l, ridge_z), (cx, cy + l, ridge_z)]
    else:
        verts = [(cx - l, cy + w, low), (cx + l, cy + w, low), (cx + l, cy - w, low), (cx - l, cy - w, low),
                 (cx - l, cy, ridge_z), (cx + l, cy, ridge_z)]
    faces = [(0, 1, 5, 4), (2, 3, 4, 5)]
    if caps:
        # Open ends would show the underside from the camera, so it closes with the caps.
        faces += [(3, 0, 4), (1, 2, 5), (0, 3, 2, 1)]
    return poly(kit, mat, verts, faces)


def gable_ends(kit, mat, cx, cy, along_y, length, width, eave_z, ridge_z):
    """The two triangular wall ends under a gable roof."""
    l = length * 0.5
    w = width * 0.5
    if along_y:
        verts = [(cx - w, cy - l, eave_z), (cx + w, cy - l, eave_z), (cx, cy - l, ridge_z),
                 (cx - w, cy + l, eave_z), (cx + w, cy + l, eave_z), (cx, cy + l, ridge_z)]
    else:
        verts = [(cx - l, cy - w, eave_z), (cx - l, cy + w, eave_z), (cx - l, cy, ridge_z),
                 (cx + l, cy - w, eave_z), (cx + l, cy + w, eave_z), (cx + l, cy, ridge_z)]
    return poly(kit, mat, verts, [(0, 1, 2), (3, 5, 4)])


def hip_roof(kit, mat, cx, cy, size_x, size_y, eave_z, peak_z, overhang=0.7, ridge=0.0):
    """A four-slope roof over a rectangle, with an optional ridge along Y."""
    x = size_x * 0.5 + overhang
    y = size_y * 0.5 + overhang
    r = ridge * 0.5
    verts = [(cx - x, cy - y, eave_z), (cx + x, cy - y, eave_z), (cx + x, cy + y, eave_z), (cx - x, cy + y, eave_z),
             (cx, cy - r, peak_z), (cx, cy + r, peak_z)]
    faces = [(0, 1, 4), (1, 2, 5, 4), (2, 3, 5), (3, 0, 4, 5), (0, 3, 2, 1)]
    return poly(kit, mat, verts, faces)


def stilts(kit, mat, cx, cy, size_x, size_y, height, radius=0.22):
    for sx in (-1, 1):
        for sy in (-1, 1):
            x = cx + sx * (size_x * 0.5 - 0.3)
            y = cy + sy * (size_y * 0.5 - 0.3)
            kit.rod(mat, (x, y, 0.0), (x, y, height), radius, vertices=8)


def boulder(kit, mat, radius, location, scale=(1.0, 1.0, 1.0), seed=0, rough=0.2):
    """A faceted rock, cut flat at the ground so nothing hangs below the footprint."""
    shape = bmesh.new()
    bmesh.ops.create_icosphere(shape, subdivisions=1, radius=radius)
    rng = random.Random(seed)
    for vert in shape.verts:
        vert.co *= 1.0 + rng.uniform(-rough, rough)
        vert.co = Vector((vert.co.x * scale[0] + location[0], vert.co.y * scale[1] + location[1],
                          vert.co.z * scale[2] + location[2]))
    geom = shape.verts[:] + shape.edges[:] + shape.faces[:]
    bmesh.ops.bisect_plane(shape, geom=geom, dist=0.0001, plane_co=(0.0, 0.0, 0.0), plane_no=(0.0, 0.0, 1.0),
                           clear_inner=True)
    mesh = bpy.data.meshes.new("boulder")
    shape.to_mesh(mesh)
    shape.free()
    obj = bpy.data.objects.new("boulder", mesh)
    bpy.context.scene.collection.objects.link(obj)
    return kit._finish(obj, mat, False)


def flag(kit, x, y, pole_height, width=2.2, height=1.4, face="y"):
    """A Katipunan flag: red field, white sun. The sun is on the side the camera sees."""
    kit.begin("prop", outlined=True)
    kit.rod(kit.material("pole", WOOD, flat=True), (x, y, 0.0), (x, y, pole_height), 0.1, vertices=8)
    kit.ball(kit.material("finial", "#C9A13B", flat=True), 0.18, (x, y, pole_height + 0.1))
    red = kit.material("flag_red", KATIPUNAN_RED)
    top = pole_height - 0.1
    if face == "y":
        kit.box(red, (0.06, width, height), (x, y - width * 0.5 - 0.1, top - height * 0.5))
        kit.ball(kit.material("flag_sun", CREAM, flat=True), 0.34, (x + 0.05, y - width * 0.5 - 0.1, top - height * 0.5),
                 scale=(0.2, 1.0, 1.0))
    else:
        kit.box(red, (width, 0.06, height), (x + width * 0.5 + 0.1, y, top - height * 0.5))
        kit.ball(kit.material("flag_sun", CREAM, flat=True), 0.34, (x + width * 0.5 + 0.1, y - 0.05, top - height * 0.5),
                 scale=(1.0, 0.2, 1.0))


def ground_patch(kit, key, color, size_x, size_y, z=0.06, cx=0.0, cy=0.0):
    kit.begin("ground")
    kit.box(kit.material(key, color), (size_x, size_y, z * 2.0), (cx, cy, 0.0))


# ----------------------------------------------------------------------------- buildings


def build_mission_tent(kit):
    """A large ridge tent, ridge along Y, its mouth on the -Y end facing the plaza."""
    ground_patch(kit, "trampled", EARTH, 11.5, 11.5)
    kit.begin("walls")
    # The lit end and the shaded slope catch almost the same light, so the end wall is a paler
    # canvas than the roof or the two would read as one flat shape.
    canvas = kit.material("canvas", CREAM)
    length, width, wall, ridge = 9.6, 8.0, 1.3, 7.0
    kit.box(canvas, (width, length, wall), (0.0, 0.0, wall * 0.5))
    gable_ends(kit, canvas, 0.0, 0.0, True, length, width, wall, ridge)

    kit.begin("roof", inline=True)
    # No overhang at the ends, or the roof's end would stand in front of the mouth.
    gable_roof(kit, kit.material("canvas_roof", "#CDBB8E", stripe="#B8A575", stripe_px=4),
               0.0, 0.0, True, length, width, wall, ridge, overhang=0.0, caps=False)

    kit.begin("door")
    face = -length * 0.5
    mouth = kit.material("mouth", "#2E241A", flat=True)
    poly(kit, mouth, [(-1.7, face - 0.05, 0.0), (1.7, face - 0.05, 0.0), (0.0, face - 0.05, 4.6)], [(0, 1, 2)])
    # The flaps tied back to either side of the mouth.
    flap = kit.material("flap", "#B8A575")
    poly(kit, flap, [(-1.7, face - 0.1, 0.0), (-2.9, face - 0.1, 0.0), (-0.3, face - 0.1, 4.3)], [(0, 1, 2)])
    poly(kit, flap, [(1.7, face - 0.1, 0.0), (2.9, face - 0.1, 0.0), (0.3, face - 0.1, 4.3)], [(0, 1, 2)])

    kit.begin("prop", outlined=True)
    pole = kit.material("pole", WOOD, flat=True)
    kit.rod(pole, (0.0, face - 0.2, 0.0), (0.0, face - 0.2, ridge + 0.8), 0.12, vertices=8)
    # Guy ropes to pegs, on the two sides the camera sees.
    rope = kit.material("rope", "#8A7650", flat=True)
    for y in (-3.0, 0.0, 3.0):
        kit.rod(rope, (width * 0.5 + 0.2, y, wall + 0.2), (width * 0.5 + 1.6, y, 0.0), 0.05, vertices=6)
    for x in (-2.5, 2.5):
        kit.rod(rope, (x, face - 0.2, wall + 0.2), (x, face - 1.6, 0.0), 0.05, vertices=6)
    flag(kit, 0.0, length * 0.5 - 0.6, ridge + 3.2)

    # A map table beside the mouth, the thing the tent is for.
    kit.begin("detail")
    tx, ty = 3.4, face - 1.8
    kit.box(kit.material("table", "#7A5634"), (1.8, 1.2, 0.12), (tx, ty, 1.3))
    for dx in (-0.75, 0.75):
        for dy in (-0.45, 0.45):
            kit.rod(pole, (tx + dx, ty + dy, 0.0), (tx + dx, ty + dy, 1.25), 0.07, vertices=6)
    kit.box(kit.material("map", "#E3D3A0", flat=True), (1.4, 0.9, 0.04), (tx, ty, 1.38))


def build_library(kit):
    """A whitewashed schoolhouse with a tile roof, door on the -Y face."""
    ground_patch(kit, "trampled", EARTH, 11.5, 11.5)
    size, wall = 8.2, 4.4
    kit.begin("walls")
    kit.box(kit.material("plinth", "#9A8C78"), (size + 0.4, size + 0.4, 0.7), (0.0, 0.0, 0.35))
    kit.box(kit.material("whitewash", WHITEWASH), (size, size, wall), (0.0, 0.0, 0.7 + wall * 0.5))

    kit.begin("roof", inline=True)
    hip_roof(kit, kit.material("tiles", TERRACOTTA, stripe="#8C3F2C", stripe_px=3),
             0.0, 0.0, size, size, 0.7 + wall, 0.7 + wall + 3.6, overhang=0.8, ridge=2.4)

    kit.begin("detail")
    door = kit.material("door", "#5B3A22")
    frame = kit.material("frame", "#3E2A18", flat=True)
    face_y = -size * 0.5 - 0.05
    kit.box(frame, (2.0, 0.12, 3.2), (0.0, face_y, 0.7 + 1.6))
    kit.box(door, (1.6, 0.14, 2.9), (0.0, face_y - 0.02, 0.7 + 1.45))
    capiz = kit.material("capiz", "#EDE6C8", stripe="#B9AE88", stripe_px=2)
    for x in (-2.7, 2.7):
        kit.box(frame, (1.7, 0.12, 1.7), (x, face_y, 0.7 + 2.6))
        kit.box(capiz, (1.4, 0.14, 1.4), (x, face_y - 0.02, 0.7 + 2.6))
    for y in (-2.0, 2.0):
        kit.box(frame, (0.12, 1.7, 1.7), (size * 0.5 + 0.05, y, 0.7 + 2.6))
        kit.box(capiz, (0.14, 1.4, 1.4), (size * 0.5 + 0.07, y, 0.7 + 2.6))
    # The sign over the door: an open book.
    kit.box(kit.material("sign", "#6A4327"), (2.4, 0.12, 0.9), (0.0, face_y - 0.05, 0.7 + wall - 0.35))
    kit.box(kit.material("page", CREAM, flat=True), (1.3, 0.14, 0.5), (0.0, face_y - 0.08, 0.7 + wall - 0.35))

    kit.begin("prop", outlined=True)
    # A bell on a small post by the door, as schoolhouses had.
    pole = kit.material("pole", WOOD, flat=True)
    kit.rod(pole, (-2.2, face_y - 1.4, 0.0), (-2.2, face_y - 1.4, 3.4), 0.1, vertices=8)
    kit.rod(pole, (-2.2, face_y - 1.4, 3.3), (-1.4, face_y - 1.4, 3.3), 0.07, vertices=6)
    kit.cone(kit.material("bell", "#C9A13B", flat=True), 0.4, 0.15, 0.6, (-1.4, face_y - 1.4, 2.9))


def build_armory(kit):
    """An adobe powder store with a heavy door on the +X face and a rifle rack beside it."""
    ground_patch(kit, "trampled", EARTH, 11.5, 11.5)
    size, wall = 8.0, 3.9
    kit.begin("walls")
    kit.box(kit.material("adobe", ADOBE, stripe="#968268", stripe_px=5), (size, size, wall), (0.0, 0.0, wall * 0.5))
    # Buttresses at the corners, the shape that says "store" rather than "house".
    buttress = kit.material("buttress", "#968268")
    for sx in (-1, 1):
        for sy in (-1, 1):
            kit.box(buttress, (1.0, 1.0, wall - 0.4), (sx * (size * 0.5 - 0.1), sy * (size * 0.5 - 0.1), (wall - 0.4) * 0.5))

    # A gable end toward the lit side: from this camera a low hip roof flattens into a diamond.
    gable_ends(kit, kit.material("adobe_gable", "#968268"), 0.0, 0.0, True, size, size, wall, wall + 3.4)
    kit.begin("roof", inline=True)
    gable_roof(kit, kit.material("tiles_dark", "#8E4332", stripe="#743626", stripe_px=3),
               0.0, 0.0, True, size, size, wall, wall + 3.4, overhang=0.6, caps=False)

    kit.begin("detail")
    face_x = size * 0.5 + 0.05
    iron = kit.material("iron", "#4A4744", flat=True)
    kit.box(kit.material("door", "#5B3A22"), (0.16, 2.3, 2.8), (face_x, 0.0, 1.4))
    for z in (0.7, 2.1):
        kit.box(iron, (0.2, 2.3, 0.18), (face_x + 0.02, 0.0, z))
    # Crossed rifles over the door.
    steel = kit.material("steel", STEEL, flat=True)
    kit.rod(steel, (face_x + 0.1, -1.2, 2.9), (face_x + 0.1, 1.2, 3.6), 0.07, vertices=6)
    kit.rod(steel, (face_x + 0.1, -1.2, 3.6), (face_x + 0.1, 1.2, 2.9), 0.07, vertices=6)

    kit.begin("prop", outlined=True)
    wood = kit.material("wood", WOOD, flat=True)
    rack_y = -2.9
    kit.rod(wood, (face_x + 1.1, rack_y - 1.1, 1.6), (face_x + 1.1, rack_y + 1.1, 1.6), 0.08, vertices=6)
    for y in (-1.1, 1.1):
        kit.rod(wood, (face_x + 1.1, rack_y + y, 0.0), (face_x + 1.1, rack_y + y, 1.8), 0.1, vertices=6)
    for i in range(4):
        y = rack_y - 0.75 + i * 0.5
        kit.rod(steel, (face_x + 1.4, y, 0.1), (face_x + 1.0, y, 2.4), 0.06, vertices=6)


def build_recruitment_hall(kit):
    """A bahay kubo on stilts: bamboo walls, a steep nipa roof, a ladder on the +X face."""
    ground_patch(kit, "trampled", EARTH, 11.5, 11.5)
    size, lift, wall = 8.0, 1.4, 3.3
    kit.begin("walls")
    bamboo = kit.material("bamboo_post", BAMBOO_DARK)
    stilts(kit, bamboo, 0.0, 0.0, size, size, lift)
    kit.box(kit.material("floor", "#8E7440"), (size + 0.3, size + 0.3, 0.3), (0.0, 0.0, lift))
    kit.box(kit.material("bamboo_wall", BAMBOO, stripe=BAMBOO_DARK, stripe_px=3), (size, size, wall),
            (0.0, 0.0, lift + wall * 0.5))

    kit.begin("roof", inline=True)
    hip_roof(kit, kit.material("nipa", THATCH, stripe=THATCH_DARK, stripe_px=3),
             0.0, 0.0, size, size, lift + wall, lift + wall + 4.8, overhang=1.1, ridge=1.0)

    kit.begin("detail")
    face_x = size * 0.5 + 0.05
    kit.box(kit.material("doorway", "#2E241A", flat=True), (0.12, 1.6, 2.4), (face_x, 0.0, lift + 1.2))
    window = kit.material("window", "#3A2E20", flat=True)
    for x in (-2.0, 2.0):
        kit.box(window, (1.4, 0.12, 1.1), (x, -size * 0.5 - 0.05, lift + 2.0))

    kit.begin("prop", outlined=True)
    ladder = kit.material("ladder", BAMBOO_DARK, flat=True)
    for y in (-0.6, 0.6):
        kit.rod(ladder, (face_x + 1.4, y, 0.0), (face_x + 0.1, y, lift + 0.2), 0.08, vertices=6)
    for i in range(3):
        t = (i + 1) / 4.0
        x = face_x + 1.4 - 1.3 * t
        kit.rod(ladder, (x, -0.6, lift * t), (x, 0.6, lift * t), 0.06, vertices=6)
    flag(kit, face_x + 1.4, -size * 0.5 - 0.6, lift + wall + 5.2)


def build_mine(kit):
    """A rocky outcrop with a timbered adit on the -Y face, rails and a cart of scrap."""
    ground_patch(kit, "spoil", "#7E6A50", 11.5, 11.5)
    kit.begin("walls")
    rock = kit.material("rock", ROCK)
    rock_dark = kit.material("rock_dark", ROCK_DARK)
    # The outcrop fills the back of the footprint and falls away toward the adit.
    boulder(kit, rock, 3.6, (0.6, 2.2, 0.0), scale=(1.3, 1.0, 1.35), seed=1)
    boulder(kit, rock_dark, 2.8, (-3.0, 2.8, 0.0), scale=(1.0, 1.0, 1.2), seed=2)
    boulder(kit, rock, 2.4, (3.6, 0.2, 0.0), scale=(1.0, 1.2, 1.1), seed=3)
    boulder(kit, rock_dark, 2.2, (-3.2, -0.8, 0.0), scale=(1.0, 1.0, 1.0), seed=4)
    boulder(kit, rock, 1.1, (3.8, -3.4, 0.0), seed=5)
    boulder(kit, rock_dark, 0.8, (-3.9, -3.9, 0.0), seed=6)
    kit.begin("grass")
    grass = kit.material("grass_tuft", LEAF)
    for x, y, z in ((0.2, 2.6, 4.9), (-3.0, 3.2, 3.2), (3.8, 0.6, 2.5)):
        kit.ball(grass, 0.8, (x, y, z), scale=(1.0, 1.0, 0.5))

    kit.begin("door")
    face_y = -1.2
    kit.box(kit.material("adit", "#1E1612", flat=True), (2.4, 2.0, 2.8), (0.4, face_y, 1.4))
    kit.begin("prop", outlined=True)
    timber = kit.material("timber", "#7A5634", flat=True)
    for x in (-1.0, 1.8):
        kit.rod(timber, (x, face_y - 1.1, 0.0), (x, face_y - 1.1, 3.2), 0.2, vertices=8)
    kit.box(timber, (3.4, 0.45, 0.45), (0.4, face_y - 1.1, 3.2))

    kit.begin("detail")
    rail = kit.material("rail", "#5C5854", flat=True)
    for x in (-0.3, 1.1):
        kit.box(rail, (0.12, 5.2, 0.1), (x, face_y - 3.6, 0.14))
    for i in range(5):
        kit.box(kit.material("sleeper", "#6A4E32"), (2.0, 0.3, 0.08), (0.4, face_y - 1.6 - i * 1.0, 0.1))

    kit.begin("prop", outlined=True)
    cart = kit.material("cart", "#6E6A66")
    cy = face_y - 3.4
    kit.box(cart, (1.8, 2.0, 1.0), (0.4, cy, 0.9))
    wheel = kit.material("wheel", "#3E3A36", flat=True)
    for x in (-0.55, 1.35):
        for y in (-0.6, 0.6):
            disc = kit.cone(wheel, 0.35, 0.35, 0.12, (x, cy + y, 0.35), vertices=10)
            disc.rotation_euler = (0.0, math.radians(90.0), 0.0)
    scrap = kit.material("scrap", "#9AA1A8")
    for dx, dy in ((0.0, -0.4), (0.5, 0.3), (-0.4, 0.4)):
        kit.ball(scrap, 0.45, (0.4 + dx, cy + dy, 1.5))
    # A pick left against the rails.
    kit.rod(timber, (2.4, cy + 0.6, 0.0), (2.0, cy + 0.2, 1.8), 0.07, vertices=6)
    kit.rod(kit.material("pick", STEEL, flat=True), (1.6, cy + 0.2, 1.7), (2.5, cy + 0.2, 2.0), 0.08, vertices=6)


def build_farm(kit):
    """A three-by-three field of rice and vegetables, with a small granary at the back."""
    half = 3 * TILE * 0.5 - 0.3
    kit.begin("ground")
    kit.box(kit.material("dike", "#8A6E48"), (half * 2.0, half * 2.0, 0.3), (0.0, 0.0, 0.15))
    kit.box(kit.material("paddy", SOIL), (half * 2.0 - 0.8, half * 2.0 - 0.8, 0.34), (0.0, 0.0, 0.15))

    kit.begin("crops")
    rice = kit.material("rice", "#88A84A")
    rice_dark = kit.material("rice_dark", LEAF)
    rows = 6
    for r in range(rows):
        x = -half + 1.2 + r * ((half * 2.0 - 2.4) / (rows - 1))
        for c in range(7):
            y = -half + 1.2 + c * ((half * 2.0 - 2.4) / 6.0)
            # The granary's corner is left clear.
            if x < -1.5 and y > 1.5:
                continue
            kit.cone(rice if (r + c) % 3 else rice_dark, 0.55, 0.05, 1.1, (x, y, 0.8), vertices=6)

    kit.begin("walls")
    gx, gy = -3.3, 3.3
    post = kit.material("bamboo_post", BAMBOO_DARK)
    stilts(kit, post, gx, gy, 2.6, 2.6, 1.2, radius=0.15)
    kit.box(kit.material("bamboo_wall", BAMBOO, stripe=BAMBOO_DARK, stripe_px=3), (2.6, 2.6, 1.8), (gx, gy, 1.2 + 0.9))
    kit.begin("roof", inline=True)
    hip_roof(kit, kit.material("nipa", THATCH, stripe=THATCH_DARK, stripe_px=3), gx, gy, 2.6, 2.6, 3.0, 5.2, overhang=0.6)

    kit.begin("prop", outlined=True)
    # A scarecrow: a cross of bamboo in a shirt and salakot.
    sx, sy = 2.3, -1.6
    kit.rod(post, (sx, sy, 0.0), (sx, sy, 3.2), 0.1, vertices=6)
    kit.rod(post, (sx, sy - 1.0, 2.4), (sx, sy + 1.0, 2.4), 0.08, vertices=6)
    kit.box(kit.material("rag", "#C9BFA6"), (0.5, 1.2, 1.1), (sx, sy, 2.1))
    kit.cone(kit.material("salakot", "#A8793D"), 0.9, 0.08, 0.5, (sx, sy, 3.35))


def build_training_grounds(kit):
    """A fenced drill yard with straw dummies and a target."""
    half = 3 * TILE * 0.5 - 0.4
    kit.begin("ground")
    kit.box(kit.material("yard", "#9A8258"), (half * 2.0, half * 2.0, 0.12), (0.0, 0.0, 0.0))

    kit.begin("walls")
    fence = kit.material("fence", BAMBOO_DARK, flat=True)
    rail = kit.material("fence_rail", BAMBOO, flat=True)
    # Along the two far sides and half of each near side, leaving the corner open.
    runs = [((-half, half), (half, half)), ((-half, -half), (-half, half)),
            ((half, half), (half, 0.6)), ((-half, -half), (-0.6, -half))]
    for a, b in runs:
        a = Vector((a[0], a[1], 0.0))
        b = Vector((b[0], b[1], 0.0))
        steps = max(1, int((b - a).length / 1.6))
        for i in range(steps + 1):
            p = a.lerp(b, i / steps)
            kit.rod(fence, (p.x, p.y, 0.0), (p.x, p.y, 1.5), 0.1, vertices=6)
        for z in (0.7, 1.3):
            kit.rod(rail, (a.x, a.y, z), (b.x, b.y, z), 0.07, vertices=6)

    kit.begin("prop", outlined=True)
    straw = kit.material("straw", STRAW)
    post = kit.material("post", WOOD, flat=True)
    for x, y in ((-1.8, 1.2), (1.4, 2.2)):
        kit.rod(post, (x, y, 0.0), (x, y, 3.0), 0.12, vertices=6)
        kit.rod(post, (x, y - 0.9, 2.3), (x, y + 0.9, 2.3), 0.09, vertices=6)
        kit.cone(straw, 0.55, 0.45, 1.5, (x, y, 1.9))
        kit.ball(straw, 0.45, (x, y, 3.1))
        kit.box(kit.material("sash", KATIPUNAN_RED), (1.12, 1.12, 0.22), (x, y, 1.7))
    # Target board, face toward the lit side.
    tx, ty = 2.2, -2.2
    kit.rod(post, (tx, ty + 0.4, 0.0), (tx, ty + 0.4, 2.0), 0.1, vertices=6)
    kit.rod(post, (tx + 0.3, ty + 0.4, 0.0), (tx + 0.3, ty + 0.1, 1.6), 0.08, vertices=6)
    rings = ((0.95, CREAM), (0.7, KATIPUNAN_RED), (0.45, CREAM), (0.2, KATIPUNAN_RED))
    for i, (radius, color) in enumerate(rings):
        disc = kit.cone(kit.material("ring%d" % i, color, flat=True), radius, radius, 0.08, (tx, ty - 0.02 * i, 2.1))
        disc.rotation_euler = (math.radians(90.0), 0.0, 0.0)


def build_exchange(kit):
    """A market stall under a striped awning, counter toward the viewer."""
    half = 2 * TILE * 0.5
    kit.begin("walls")
    counter = kit.material("counter", "#7A5634")
    kit.box(counter, (1.6, 5.6, 1.4), (1.2, 0.0, 0.7))
    kit.box(kit.material("counter_top", "#94704A"), (1.9, 5.9, 0.16), (1.2, 0.0, 1.45))
    post = kit.material("post", WOOD, flat=True)
    for x in (-2.4, 2.1):
        for y in (-2.9, 2.9):
            kit.rod(post, (x, y, 0.0), (x, y, 3.6 if x > 0 else 4.4), 0.12, vertices=6)

    kit.begin("roof", inline=True)
    awning = kit.material("awning", KATIPUNAN_RED, stripe=CREAM, stripe_px=4)
    poly(kit, awning, [(-2.7, -3.3, 4.5), (-2.7, 3.3, 4.5), (2.7, 3.3, 3.5), (2.7, -3.3, 3.5)], [(0, 1, 2, 3)])
    # A valance along the front edge.
    poly(kit, awning, [(2.7, -3.3, 3.5), (2.7, 3.3, 3.5), (2.7, 3.3, 3.0), (2.7, -3.3, 3.0)], [(0, 1, 2, 3)])

    kit.begin("prop", outlined=True)
    sack = kit.material("sack", "#C8B48A")
    for y in (-1.8, -0.9):
        kit.ball(sack, 0.5, (1.2, y, 1.9), scale=(1.0, 1.0, 1.2))
    kit.cone(kit.material("basket", "#9C7A44"), 0.55, 0.7, 0.5, (1.2, 0.6, 1.8))
    kit.ball(kit.material("produce", "#C9A13B"), 0.3, (1.2, 0.6, 2.1))
    kit.box(kit.material("scrap_pile", "#9AA1A8"), (0.7, 0.9, 0.4), (1.2, 1.9, 1.75))
    # The price board, on its own post by the near corner.
    kit.rod(post, (2.8, -3.9, 0.0), (2.8, -3.9, 2.4), 0.09, vertices=6)
    kit.box(kit.material("board", "#2E3A2E", flat=True), (0.12, 1.4, 1.1), (2.9, -3.9, 2.3))
    chalk = kit.material("chalk", CREAM, flat=True)
    for z in (2.5, 2.2):
        kit.box(chalk, (0.14, 0.9, 0.08), (2.92, -3.9, z))


# ----------------------------------------------------------------------------- props


def build_flag(kit):
    flag(kit, 0.0, 0.0, 8.6, width=2.8, height=1.8)
    kit.begin("walls")
    kit.cone(kit.material("stones", ROCK), 0.9, 0.6, 0.5, (0.0, 0.0, 0.25))


def palm(kit, x=0.0, y=0.0, height=9.0, lean=(0.9, -0.6), frond_count=7, key="palm"):
    kit.begin("trunk_" + key)
    trunk = kit.material("trunk", "#7A6040")
    segments = 5
    points = []
    for i in range(segments + 1):
        t = i / segments
        points.append(Vector((x + lean[0] * t * t, y + lean[1] * t * t, height * t)))
    for a, b in zip(points, points[1:]):
        kit.rod(trunk, a, b, 0.32, end_radius=0.26, vertices=8)
    top = points[-1]
    kit.begin("leaves_" + key, inline=True)
    leaf = kit.material("frond", LEAF)
    leaf_dark = kit.material("frond_dark", LEAF_DARK)
    for i in range(frond_count):
        angle = (i / frond_count) * math.tau + 0.3
        direction = Vector((math.cos(angle), math.sin(angle), 0.0))
        mid = top + direction * 1.6 + Vector((0.0, 0.0, 0.4))
        tip = top + direction * 3.2 + Vector((0.0, 0.0, -1.2))
        kit.slab(leaf if i % 2 else leaf_dark, top, mid, 0.9, 0.12)
        kit.slab(leaf_dark if i % 2 else leaf, mid, tip, 0.7, 0.12)
    kit.begin("nuts_" + key)
    nut = kit.material("coconut", "#5C4A2A")
    for dx, dy in ((0.3, -0.2), (-0.2, 0.3), (0.1, 0.35)):
        kit.ball(nut, 0.26, (top.x + dx, top.y + dy, top.z - 0.4))


def build_palm(kit):
    palm(kit)


def build_crates(kit):
    kit.begin("walls")
    wood = kit.material("crate", "#8C6A3E", stripe="#6E522E", stripe_px=3)
    kit.box(wood, (1.8, 1.8, 1.6), (0.3, -0.4, 0.8))
    kit.box(wood, (1.5, 1.5, 1.3), (-0.8, 0.9, 0.65))
    kit.box(wood, (1.4, 1.4, 1.2), (0.1, -0.1, 2.2), rotation=(0.0, 0.0, math.radians(20.0)))


def build_cart(kit):
    """A carabao cart loaded with scrap iron."""
    kit.begin("walls")
    bed = kit.material("cart_bed", "#7A5634")
    kit.box(bed, (2.0, 3.2, 0.9), (0.0, 0.0, 1.2))
    kit.begin("prop", outlined=True)
    wheel = kit.material("wheel", "#5B3A22")
    for x in (-1.15, 1.15):
        wheel_obj = kit.cone(wheel, 0.95, 0.95, 0.16, (x, 0.2, 0.95), vertices=12)
        wheel_obj.rotation_euler = (0.0, math.radians(90.0), 0.0)
    kit.rod(kit.material("shaft", WOOD, flat=True), (0.0, -1.6, 1.1), (0.0, -3.0, 0.9), 0.1, vertices=6)
    scrap = kit.material("scrap", "#9AA1A8")
    for dx, dy in ((-0.4, -0.6), (0.4, 0.2), (-0.2, 0.8), (0.3, -0.9)):
        kit.ball(scrap, 0.5, (dx, dy, 1.9))


def build_sacks(kit):
    kit.begin("walls")
    sack = kit.material("sack", "#C2A878")
    tie = kit.material("tie", "#8A7650", flat=True)
    for x, y, z in ((0.5, -0.5, 0.6), (-0.6, 0.4, 0.6), (0.6, 0.8, 0.6), (0.0, 0.1, 1.6)):
        kit.ball(sack, 0.8, (x, y, z), scale=(1.0, 1.0, 0.8))
        kit.ball(tie, 0.18, (x, y, z + 0.65))


def build_rack(kit):
    """A rack of bamboo spears."""
    kit.begin("prop", outlined=True)
    wood = kit.material("wood", WOOD, flat=True)
    kit.rod(wood, (0.0, -1.2, 1.4), (0.0, 1.2, 1.4), 0.09, vertices=6)
    for y in (-1.2, 1.2):
        kit.rod(wood, (0.0, y, 0.0), (0.0, y, 1.7), 0.11, vertices=6)
    spear = kit.material("spear", BAMBOO, flat=True)
    steel = kit.material("steel", STEEL, flat=True)
    for i in range(5):
        y = -0.9 + i * 0.45
        kit.rod(spear, (0.4, y, 0.0), (-0.3, y, 3.6), 0.06, vertices=6)
        kit.cone(steel, 0.12, 0.0, 0.5, (-0.34, y, 3.85), vertices=6)


# ----------------------------------------------------------------------------- scenery


def build_bamboo(kit):
    """A clump of bamboo: culms arching out from one root, feathery leaves toward the tips."""
    culms = []
    for i, (angle, height, lean) in enumerate(((0.3, 9.4, 1.4), (1.5, 8.2, 2.2), (2.6, 8.8, 1.8), (3.7, 7.6, 2.4),
                                               (4.8, 9.0, 1.6), (5.8, 7.2, 2.6))):
        out = Vector((math.cos(angle), math.sin(angle), 0.0))
        points = [out * 0.35 + out * lean * (t / 4.0) ** 2 + Vector((0.0, 0.0, height * t / 4.0)) for t in range(5)]
        culms.append((out, points))
    kit.begin("walls")
    stalk = kit.material("stalk", "#8FA24E", stripe="#6B7A34", stripe_px=4)
    for out, points in culms:
        for a, b in zip(points, points[1:]):
            kit.rod(stalk, a, b, 0.16, end_radius=0.12, vertices=6)
    kit.begin("leaves", inline=True)
    leaf = kit.material("bamboo_leaf", "#7A9A40")
    leaf_dark = kit.material("bamboo_leaf_dark", LEAF_DARK)
    for i, (out, points) in enumerate(culms):
        side = Vector((-out.y, out.x, 0.0))
        for j, base in enumerate(points[2:]):
            for k, turn in enumerate((-1.0, -0.3, 0.4, 1.0)):
                # Narrow drooping blades, a fan of them at each node.
                tip = base + (out * 0.7 + side * turn) * 1.5 + Vector((0.0, 0.0, -0.9))
                half = side * 0.22
                poly(kit, leaf if (i + j + k) % 2 else leaf_dark, [base - half, base + half, tip], [(0, 1, 2)])


def build_bush(kit):
    kit.begin("walls")
    leaf = kit.material("bush", "#557A34")
    leaf_dark = kit.material("bush_dark", LEAF_DARK)
    kit.ball(leaf_dark, 1.3, (0.0, 0.0, 0.8), scale=(1.0, 1.0, 0.8))
    kit.ball(leaf, 1.0, (0.6, -0.6, 0.9), scale=(1.0, 1.0, 0.8))
    kit.ball(leaf, 0.9, (-0.6, 0.5, 1.2), scale=(1.0, 1.0, 0.8))


def build_banana(kit):
    kit.begin("walls")
    kit.rod(kit.material("banana_stem", "#7E8C44"), (0.0, 0.0, 0.0), (0.0, 0.0, 3.4), 0.35, end_radius=0.25, vertices=8)
    kit.begin("leaves", inline=True)
    leaf = kit.material("banana_leaf", "#6E9A3C")
    leaf_dark = kit.material("banana_leaf_dark", LEAF_DARK)
    for i in range(6):
        angle = (i / 6.0) * math.tau
        direction = Vector((math.cos(angle), math.sin(angle), 0.0))
        start = Vector((0.0, 0.0, 3.2))
        kit.slab(leaf if i % 2 else leaf_dark, start, start + direction * 2.6 + Vector((0.0, 0.0, 0.8)), 1.1, 0.1)


# Name, builder, canvas. The name is the sprite file under Art/Encampment/ and the id the
# layout refers to. Canvases share one ground point so every sprite imports with one pivot.
PIECES = [
    ("MissionTent", build_mission_tent),
    ("Library", build_library),
    ("Armory", build_armory),
    ("RecruitmentHall", build_recruitment_hall),
    ("Mine", build_mine),
    ("Farm", build_farm),
    ("TrainingGrounds", build_training_grounds),
    ("Exchange", build_exchange),
    ("Flag", build_flag),
    ("Palm", build_palm),
    ("Crates", build_crates),
    ("Cart", build_cart),
    ("Sacks", build_sacks),
    ("Rack", build_rack),
    ("Bamboo", build_bamboo),
    ("Bush", build_bush),
    ("Banana", build_banana),
]
