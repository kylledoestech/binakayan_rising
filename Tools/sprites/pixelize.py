"""
Turns a raw EEVEE render into finished pixel art: hard alpha, palette colours, 1px outline.

Runs inside Blender's bundled Python, which ships numpy, so the pipeline needs nothing
installed beyond Blender itself.
"""

import bpy
import numpy as np

# The largest patch treated as noise. A hand is about six pixels, so this stays below it.
BLOTCH_PIXELS = 5


def _load(path):
    image = bpy.data.images.load(path, check_existing=False)
    width, height = image.size
    pixels = np.array(image.pixels[:], dtype=np.float32).reshape(height, width, 4)
    bpy.data.images.remove(image)
    return pixels


def _neighbours(mask):
    """True where any edge-adjacent pixel is set."""
    near = np.zeros_like(mask)
    near[1:, :] |= mask[:-1, :]
    near[:-1, :] |= mask[1:, :]
    near[:, 1:] |= mask[:, :-1]
    near[:, :-1] |= mask[:, 1:]
    return near


def finish(raw_path, groups_path, out_path, tones, shadow_of, inline, outlined, holds, outline):
    """Writes the finished sprite and returns the largest palette-snap distance (0-255 units).

    A large distance means the render produced a colour no material declares, usually a
    colour-management setting drifting; it is printed so that shows up in the build log.
    """
    pixels = _load(raw_path)
    height, width = pixels.shape[:2]
    groups = np.round(_load(groups_path)[..., 0] * 255.0).astype(np.int32) - 1

    rgb = np.round(pixels[..., :3] * 255.0)
    solid = pixels[..., 3] >= 0.5

    palette = np.array(tones, dtype=np.float32)
    distance = np.linalg.norm(rgb[:, :, None, :] - palette[None, None, :, :], axis=-1)
    nearest = palette[np.argmin(distance, axis=-1)]
    worst = int(np.max(np.where(solid, np.min(distance, axis=-1), 0.0))) if solid.any() else 0

    result = np.zeros((height, width, 4), dtype=np.float32)
    result[solid, :3] = nearest[solid]
    result[solid, 3] = 255.0

    # Small blotches are shading noise where a low-poly curve grazes the light threshold. A
    # patch of one tone, a few pixels big, that sits entirely inside its own lit/shadow partner
    # on the same part takes the partner's colour. Pairs only, so eyes, bands and thin props
    # are never touched.
    partner = dict(shadow_of)
    partner.update({shade: lit for lit, shade in shadow_of.items()})
    colours = result[..., :3].astype(np.int32)
    seen = np.zeros_like(solid)
    for y0, x0 in zip(*np.nonzero(solid)):
        if seen[y0, x0]:
            continue
        here = tuple(int(c) for c in colours[y0, x0])
        group = groups[y0, x0]
        patch = []
        stack = [(y0, x0)]
        seen[y0, x0] = True
        border_ok = True
        while stack:
            y, x = stack.pop()
            patch.append((y, x))
            for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                if not (0 <= ny < height and 0 <= nx < width) or not solid[ny, nx]:
                    # The silhouette edge counts as a border, except on hands, where a
                    # three-pixel lit knuckle is real shape rather than noise.
                    border_ok = border_ok and group not in holds
                    continue
                if groups[ny, nx] == group and tuple(int(c) for c in colours[ny, nx]) == here:
                    if not seen[ny, nx]:
                        seen[ny, nx] = True
                        stack.append((ny, nx))
                elif groups[ny, nx] != group or tuple(int(c) for c in colours[ny, nx]) != partner.get(here):
                    border_ok = False
        if here in partner and border_ok and len(patch) <= BLOTCH_PIXELS:
            for y, x in patch:
                result[y, x, :3] = partner[here]

    # Inner edges: a pixel of an inline group (arms, head, hat) that borders a different
    # group drops to its own shadow tone. Hair and eyes are already ink and need no help.
    ink = np.all(result[..., :3] == np.array(outline, dtype=np.float32), axis=-1)
    edge = np.zeros_like(solid)
    for group in inline:
        other = solid & (groups != group) & ~ink
        edge |= solid & (groups == group) & _neighbours(other)
    for y, x in zip(*np.nonzero(edge)):
        shade = shadow_of.get(tuple(int(c) for c in result[y, x, :3]))
        if shade is not None:
            result[y, x, :3] = shade

    # Props get an ink line drawn on whatever they cross, except the hand that grips them.
    for group in outlined:
        mine = solid & (groups == group)
        receivers = solid & ~mine & ~np.isin(groups, list(holds) + list(outlined))
        result[receivers & _neighbours(mine), :3] = outline

    # Outline every empty pixel that touches the figure along an edge. Diagonal neighbours
    # are left out: they thicken the line at corners and blur a 64px silhouette.
    rim = _neighbours(solid) & ~solid
    result[rim, :3] = outline
    result[rim, 3] = 255.0

    finished = bpy.data.images.new("finished", width, height, alpha=True)
    finished.alpha_mode = "STRAIGHT"
    finished.pixels[:] = (result / 255.0).ravel()
    finished.filepath_raw = out_path
    finished.file_format = "PNG"
    finished.save()
    bpy.data.images.remove(finished)
    return worst
