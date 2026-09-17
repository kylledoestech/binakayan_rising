"""
Chibi unit models for the sprite pipeline.

Every figure is assembled from Blender primitives in code, so a change to a hat or a weapon
is a reviewable diff instead of an edited binary. Figures stand on the origin and face +X.
With the dimetric camera in build_and_render.py, +X reads as screen right-down, so the
figure's right side (-Y) is the side turned toward the viewer and carries the weapon.

One Blender unit is one tenth of a body-sprite pixel's height on screen, so the anatomy
constants below can be read as pixels at a glance.
"""

import math

import bpy
from mathutils import Quaternion, Vector

# ----------------------------------------------------------------------------- colour


def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def linear_to_srgb(c):
    c = max(0.0, min(1.0, c))
    return c * 12.92 if c <= 0.0031308 else (1.055 * (c ** (1.0 / 2.4))) - 0.055


def hex_srgb(value):
    value = value.lstrip("#")
    return tuple(int(value[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


# Multiplied in linear light. Squared-ish by the sRGB curve, this lands near 63% brightness
# with a slight cool tilt, which is what separates a shadow band from a darker cloth.
SHADOW = (0.36, 0.34, 0.44)

# Shader-to-RGB brightness above which a surface counts as lit.
LIGHT_THRESHOLD = 0.22

# Hair, eyes and the sprite outline share one near-black, which keeps the palette small.
INK = "#1E1612"


class Kit:
    """Makes materials and parts for one figure and remembers the tones they can render."""

    def __init__(self):
        self.materials = {}
        self.tones = []
        self.shadow_of = {}
        self.groups = []
        self.inline = set()
        self.outlined = set()
        self.holds = set()
        self.current = 0
        self.begin("body")

    def begin(self, name, inline=False, outlined=False, holds=False):
        """Starts a part group.

        inline: the group's own pixels get its shadow tone where it crosses another group,
        which is what lets an arm read against a shirt of the same colour.
        outlined: other groups' pixels next to this one turn ink, so a thin blade stays
        legible in front of anything. Groups marked holds (hands) never take that ink,
        or a grip would eat the fist holding it.
        """
        if name not in self.groups:
            self.groups.append(name)
        self.current = self.groups.index(name)
        if inline:
            self.inline.add(self.current)
        if outlined:
            self.outlined.add(self.current)
        if holds:
            self.holds.add(self.current)

    # -------------------------------------------------------------- materials

    def material(self, key, color, flat=False, stripe=None, stripe_px=3):
        if key in self.materials:
            return self.materials[key]

        mat = bpy.data.materials.new(key)
        nodes = mat.node_tree.nodes
        links = mat.node_tree.links
        nodes.clear()

        output = nodes.new("ShaderNodeOutputMaterial")
        emission = nodes.new("ShaderNodeEmission")
        links.new(emission.outputs["Emission"], output.inputs["Surface"])

        base = self._color_socket(mat, color, stripe, stripe_px)
        if flat:
            links.new(base, emission.inputs["Color"])
        else:
            diffuse = nodes.new("ShaderNodeBsdfDiffuse")
            diffuse.inputs["Color"].default_value = (1.0, 1.0, 1.0, 1.0)
            to_rgb = nodes.new("ShaderNodeShaderToRGB")
            links.new(diffuse.outputs["BSDF"], to_rgb.inputs["Shader"])

            ramp = nodes.new("ShaderNodeValToRGB")
            ramp.color_ramp.interpolation = "CONSTANT"
            ramp.color_ramp.elements[0].position = 0.0
            ramp.color_ramp.elements[0].color = (SHADOW[0], SHADOW[1], SHADOW[2], 1.0)
            ramp.color_ramp.elements[1].position = LIGHT_THRESHOLD
            ramp.color_ramp.elements[1].color = (1.0, 1.0, 1.0, 1.0)
            links.new(to_rgb.outputs["Color"], ramp.inputs["Fac"])

            multiply = nodes.new("ShaderNodeVectorMath")
            multiply.operation = "MULTIPLY"
            links.new(base, multiply.inputs[0])
            links.new(ramp.outputs["Color"], multiply.inputs[1])
            links.new(multiply.outputs["Vector"], emission.inputs["Color"])

        for tone in [color] + ([stripe] if stripe else []):
            srgb = hex_srgb(tone)
            lit = self._add_tone(srgb)
            if not flat:
                shade = self._add_tone(tuple(
                    linear_to_srgb(srgb_to_linear(srgb[i]) * SHADOW[i]) for i in range(3)))
                self.shadow_of[lit] = shade

        self.materials[key] = mat
        return mat

    def _add_tone(self, srgb):
        rounded = tuple(round(c * 255) for c in srgb)
        if rounded not in self.tones:
            self.tones.append(rounded)
        return rounded

    @staticmethod
    def _color_socket(mat, color, stripe, stripe_px):
        nodes = mat.node_tree.nodes
        links = mat.node_tree.links

        rgb = nodes.new("ShaderNodeRGB")
        rgb.outputs[0].default_value = tuple(srgb_to_linear(c) for c in hex_srgb(color)) + (1.0,)
        if stripe is None:
            return rgb.outputs[0]

        # Stripes are planes of constant x + y, which is the camera's screen-right axis, so
        # they stay vertical on screen and exactly stripe_px pixels apart on every face.
        stripe_rgb = nodes.new("ShaderNodeRGB")
        stripe_rgb.outputs[0].default_value = tuple(srgb_to_linear(c) for c in hex_srgb(stripe)) + (1.0,)

        geometry = nodes.new("ShaderNodeNewGeometry")
        split = nodes.new("ShaderNodeSeparateXYZ")
        links.new(geometry.outputs["Position"], split.inputs["Vector"])

        add = nodes.new("ShaderNodeMath")
        add.operation = "ADD"
        links.new(split.outputs["X"], add.inputs[0])
        links.new(split.outputs["Y"], add.inputs[1])

        scale = nodes.new("ShaderNodeMath")
        scale.operation = "DIVIDE"
        links.new(add.outputs[0], scale.inputs[0])
        scale.inputs[1].default_value = stripe_px * 0.1 * math.sqrt(2.0)

        fract = nodes.new("ShaderNodeMath")
        fract.operation = "FRACT"
        links.new(scale.outputs[0], fract.inputs[0])

        less = nodes.new("ShaderNodeMath")
        less.operation = "LESS_THAN"
        links.new(fract.outputs[0], less.inputs[0])
        less.inputs[1].default_value = 1.0 / stripe_px

        mix = nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        links.new(less.outputs[0], _socket(mix.inputs, "Factor_Float"))
        links.new(rgb.outputs[0], _socket(mix.inputs, "A_Color"))
        links.new(stripe_rgb.outputs[0], _socket(mix.inputs, "B_Color"))
        return _socket(mix.outputs, "Result_Color")

    # -------------------------------------------------------------- parts

    def _finish(self, obj, mat, smooth):
        obj["group"] = self.current
        obj.data.materials.append(mat)
        if smooth:
            for polygon in obj.data.polygons:
                polygon.use_smooth = True
        return obj

    def box(self, mat, size, location, rotation=(0.0, 0.0, 0.0)):
        bpy.ops.mesh.primitive_cube_add(size=1.0, location=location, rotation=rotation)
        obj = bpy.context.active_object
        obj.scale = size
        return self._finish(obj, mat, False)

    def ball(self, mat, radius, location, scale=(1.0, 1.0, 1.0)):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, radius=radius, location=location)
        obj = bpy.context.active_object
        obj.scale = scale
        return self._finish(obj, mat, True)

    def cone(self, mat, bottom, top, depth, location, scale=(1.0, 1.0, 1.0), vertices=16):
        bpy.ops.mesh.primitive_cone_add(
            vertices=vertices, radius1=bottom, radius2=top, depth=depth, location=location)
        obj = bpy.context.active_object
        obj.scale = scale
        return self._finish(obj, mat, True)

    def rod(self, mat, start, end, radius, end_radius=None, vertices=16):
        """A cylinder (or taper) running from one point to another."""
        start = Vector(start)
        end = Vector(end)
        axis = end - start
        bpy.ops.mesh.primitive_cone_add(
            vertices=vertices, radius1=radius, radius2=radius if end_radius is None else end_radius,
            depth=axis.length, location=(start + end) * 0.5)
        obj = bpy.context.active_object
        obj.rotation_mode = "QUATERNION"
        obj.rotation_quaternion = axis.normalized().to_track_quat("Z", "Y")
        return self._finish(obj, mat, True)

    def slab(self, mat, start, end, width, thickness, twist=0.0):
        """A flat blade or plank from one point to another; width lies across the figure's X."""
        start = Vector(start)
        end = Vector(end)
        axis = end - start
        bpy.ops.mesh.primitive_cube_add(size=1.0, location=(start + end) * 0.5)
        obj = bpy.context.active_object
        obj.scale = (width, thickness, axis.length)
        obj.rotation_mode = "QUATERNION"
        obj.rotation_quaternion = axis.normalized().to_track_quat("Z", "X") @ Quaternion((0.0, 0.0, 1.0), math.radians(twist))
        return self._finish(obj, mat, False)


def _socket(sockets, identifier):
    for socket in sockets:
        if socket.identifier == identifier:
            return socket
    raise KeyError(identifier)


# ----------------------------------------------------------------------------- anatomy

SKIN = "#B77A4C"
SPANISH_SKIN = "#D9A27A"
WOOD = "#6A4327"
STEEL = "#9AA1A8"
KATIPUNAN_RED = "#B3261E"

HEAD_Z = 4.0
HEAD_R = 1.22
SHOULDER = (0.0, 0.84, 2.62)
TORSO_BOTTOM = 1.2
TORSO_TOP = 2.85


def mirror(point, side):
    """side -1 is the figure's right (toward the viewer), +1 its left."""
    return (point[0], point[1] * side, point[2])


def base_figure(kit, look):
    """Legs, torso, arms, head, face and hair. Returns hand positions for props."""
    skin = kit.material("skin", look.get("skin", SKIN))
    ink = kit.material("ink", INK, flat=True)
    shirt = kit.material("shirt", look["shirt"], stripe=look.get("shirt_stripe"))
    trousers = kit.material("trousers", look["trousers"], stripe=look.get("trousers_stripe"))
    feet = skin if look.get("barefoot") else kit.material("shoes", look.get("shoes", "#3A2A1E"))
    sleeve = kit.material("sleeve", look["sleeve"]) if "sleeve" in look else shirt

    kit.begin("legs")
    for side in (-1, 1):
        kit.box(feet, (0.56, 0.32, 0.22), mirror((0.1, 0.33, 0.11), side))
        kit.rod(trousers, mirror((0.0, 0.33, 0.18), side), mirror((0.0, 0.36, TORSO_BOTTOM + 0.2), side), 0.27)

    # A coat or a tunic can hang lower than the shirt line; trousers stay underneath it.
    kit.begin("torso")
    torso_bottom = look.get("torso_bottom", TORSO_BOTTOM)
    torso_depth = TORSO_TOP - torso_bottom
    kit.cone(shirt, look.get("hem", 0.82), 0.7, torso_depth,
             (0.0, 0.0, torso_bottom + torso_depth * 0.5), scale=(0.8, 1.0, 1.0))

    hands = {}
    for side in (-1, 1):
        kit.begin("arm_right" if side < 0 else "arm_left", inline=True, holds=True)
        shoulder = mirror(SHOULDER, side)
        hand = mirror(look.get("right_hand" if side < 0 else "left_hand", (0.2, 1.02, 1.55)), side)
        elbow = Vector(shoulder).lerp(Vector(hand), 0.5)
        if look.get("rolled_sleeves"):
            kit.rod(sleeve, shoulder, elbow, 0.24)
            kit.rod(skin, elbow, hand, 0.21)
        else:
            kit.rod(sleeve, shoulder, hand, 0.25, end_radius=0.22)
        kit.ball(skin, 0.26, hand)
        hands[side] = Vector(hand)

    kit.begin("head", inline=True)
    kit.ball(skin, HEAD_R, (0.0, 0.0, HEAD_Z), scale=(1.0, 1.0, 0.95))
    kit.begin("face")
    for y in (-0.42, 0.42):
        kit.box(ink, (0.12, 0.2, 0.34), (1.1, y, HEAD_Z - 0.08))

    hair = look.get("hair", "cap")
    if hair == "cap":
        kit.ball(ink, 1.28, (-0.28, 0.0, HEAD_Z + 0.22), scale=(1.0, 1.0, 0.96))
    elif hair == "parted":
        kit.ball(ink, 1.28, (-0.22, 0.0, HEAD_Z + 0.26), scale=(1.0, 1.0, 0.96))
        kit.box(ink, (0.5, 1.0, 0.3), (0.72, 0.35, HEAD_Z + 0.95), rotation=(0.0, math.radians(-25.0), 0.0))
    return hands, skin, ink


# ----------------------------------------------------------------------------- hats and props


def hat_brimmed(kit, key, color, band, brim, crown, crown_depth):
    kit.begin("hat", inline=True)
    straw = kit.material(key, color)
    kit.cone(straw, brim, brim, 0.1, (0.0, 0.0, HEAD_Z + 1.02))
    kit.cone(straw, crown, crown * 0.92, crown_depth, (0.0, 0.0, HEAD_Z + 1.07 + crown_depth * 0.5))
    if band:
        kit.cone(kit.material(key + "_band", band, flat=True), crown + 0.03, crown + 0.03, 0.16,
                 (0.0, 0.0, HEAD_Z + 1.17))


def hat_salakot(kit):
    kit.begin("hat", inline=True)
    woven = kit.material("salakot", "#A8793D")
    kit.cone(woven, 1.55, 0.12, 0.85, (0.0, 0.0, HEAD_Z + 1.35))
    kit.ball(kit.material("brass", "#C9A13B", flat=True), 0.14, (0.0, 0.0, HEAD_Z + 1.81))


def headband(kit, color, tails=True):
    kit.begin("cloth")
    cloth = kit.material("band", color)
    kit.cone(cloth, 1.26, 1.24, 0.3, (0.0, 0.0, HEAD_Z + 0.45))
    if tails:
        kit.slab(cloth, (-1.15, -0.2, HEAD_Z + 0.45), (-1.55, -0.55, HEAD_Z - 0.35), 0.1, 0.24)
        kit.slab(cloth, (-1.15, 0.2, HEAD_Z + 0.45), (-1.6, -0.05, HEAD_Z - 0.45), 0.1, 0.24)


def neckerchief(kit, color):
    kit.begin("cloth")
    cloth = kit.material("band", color)
    kit.cone(cloth, 0.8, 0.72, 0.28, (0.0, 0.0, TORSO_TOP - 0.02), scale=(0.85, 1.0, 1.0))
    kit.box(cloth, (0.1, 0.34, 0.42), (0.66, 0.0, TORSO_TOP - 0.3), rotation=(0.0, math.radians(-12.0), 0.0))


def rifle(kit, butt, muzzle):
    kit.begin("prop", outlined=True)
    wood = kit.material("wood", WOOD, flat=True)
    steel = kit.material("steel", STEEL, flat=True)
    butt = Vector(butt)
    muzzle = Vector(muzzle)
    grip = butt.lerp(muzzle, 0.62)
    kit.slab(wood, butt, grip, 0.26, 0.16)
    kit.rod(steel, grip, muzzle, 0.07)


def blade(kit, hilt, tip, width, handle_length=0.45, guard=None):
    kit.begin("prop", outlined=True)
    steel = kit.material("steel", STEEL, flat=True)
    hilt = Vector(hilt)
    tip = Vector(tip)
    direction = (tip - hilt).normalized()
    kit.rod(kit.material("wood", WOOD, flat=True), hilt - direction * handle_length, hilt, 0.09)
    if guard:
        kit.box(kit.material("brass", guard, flat=True), (0.14, 0.44, 0.1), tuple(hilt))
    kit.slab(steel, hilt, tip, width, 0.06)


# ----------------------------------------------------------------------------- units


def build_marksman(kit):
    base_figure(kit, {
        "shirt": "#E9E2D0",
        "trousers": "#8C7651",
        "barefoot": True,
        "right_hand": (0.95, 0.4, 1.7),
        "left_hand": (1.0, 0.45, 2.45),
    })
    neckerchief(kit, KATIPUNAN_RED)
    hat_brimmed(kit, "straw", "#D8BD78", "#5A3E22", brim=1.45, crown=0.92, crown_depth=0.5)
    rifle(kit, (1.05, -0.95, 1.05), (1.1, 1.0, 3.25))


def build_engineer(kit):
    base_figure(kit, {
        "shirt": "#DCD3BC",
        "trousers": "#5E5446",
        "shoes": "#4A3522",
        "rolled_sleeves": True,
        "right_hand": (0.5, 1.22, 1.5),
    })
    hat_salakot(kit)
    kit.begin("prop", outlined=True)
    wood = kit.material("wood", WOOD, flat=True)
    steel = kit.material("steel", STEEL, flat=True)
    # Shovel slung across the back, blade up past the right shoulder where it clears the head.
    kit.rod(wood, (-0.9, 0.6, 2.1), (-0.95, -1.25, 4.3), 0.08)
    kit.slab(steel, (-0.95, -1.25, 4.25), (-0.98, -1.55, 5.05), 0.08, 0.55, twist=90.0)
    blade(kit, (0.55, -1.22, 1.45), (1.15, -1.75, 0.45), 0.34)


def build_evangelista(kit):
    base_figure(kit, {
        "shirt": "#2F3A4B",
        "trousers": "#2A2723",
        "shoes": "#1F1812",
        "torso_bottom": 0.72,
        "hem": 0.98,
        "right_hand": (0.85, 1.0, 1.95),
    })
    kit.begin("detail")
    kit.box(kit.material("collar", "#E9E2D0", flat=True), (0.12, 0.34, 0.26), (0.58, 0.0, TORSO_TOP - 0.08))
    hat_brimmed(kit, "felt", "#4A3A2A", "#1E1612", brim=1.5, crown=0.92, crown_depth=0.65)
    kit.begin("prop", outlined=True)
    # Rolled plans held out in front, revolver holstered on the near hip.
    kit.rod(kit.material("plans", "#5E8FCB"), (0.95, -1.35, 2.05), (1.05, -0.55, 1.9), 0.2)
    kit.box(kit.material("leather", "#5B3A22", flat=True), (0.3, 0.2, 0.5), (0.2, -0.95, 1.1))


def build_aguinaldo(kit):
    base_figure(kit, {
        "shirt": "#EFEADF",
        "trousers": "#E2DBCB",
        "shoes": "#2B2119",
        "hair": "parted",
        "right_hand": (0.55, 1.22, 1.9),
    })
    kit.begin("detail")
    red = kit.material("band", KATIPUNAN_RED)
    # On the far arm, which is the one the camera sees clear of the sword.
    kit.rod(red, (0.05, 0.88, 2.36), (0.1, 0.92, 2.1), 0.29)
    kit.box(kit.material("tie", "#1E1612", flat=True), (0.1, 0.16, 0.7), (0.66, 0.0, TORSO_TOP - 0.42))
    blade(kit, (0.58, -1.24, 1.95), (0.95, -1.95, 4.15), 0.22, guard="#C9A13B")


def build_vanguard(kit):
    base_figure(kit, {
        "shirt": "#E6DDC8",
        "trousers": "#A08556",
        "barefoot": True,
        "right_hand": (0.6, 1.22, 2.0),
    })
    headband(kit, KATIPUNAN_RED)
    blade(kit, (0.65, -1.24, 2.0), (1.0, -1.85, 3.35), 0.36)


def build_spanish_regular(kit):
    base_figure(kit, {
        "skin": SPANISH_SKIN,
        "shirt": "#D9E1E6",
        "shirt_stripe": "#56779C",
        "trousers": "#D9E1E6",
        "trousers_stripe": "#56779C",
        "shoes": "#3B2A1D",
        "right_hand": (0.35, 1.08, 1.85),
    })
    kit.begin("detail")
    kit.box(kit.material("leather", "#5B3A22", flat=True), (0.96, 1.5, 0.2), (0.0, 0.0, TORSO_BOTTOM + 0.12))
    hat_brimmed(kit, "straw", "#E1CC8C", "#2F4F7F", brim=1.35, crown=0.95, crown_depth=0.45)
    rifle(kit, (0.35, -1.2, 0.08), (0.4, -1.22, 3.65))


# Folder names are the archetype ids used by PlaytestScenario, so Unity can find each set.
UNITS = [
    ("Marksman", build_marksman),
    ("Engineer", build_engineer),
    ("Evangelista", build_evangelista),
    ("Aguinaldo", build_aguinaldo),
    ("Vanguard", build_vanguard),
    ("SpanishRegular", build_spanish_regular),
]
