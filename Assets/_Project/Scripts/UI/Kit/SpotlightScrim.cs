using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BinakayanRising.UI.Kit
{
    /// <summary>
    /// A full-screen dimming layer with rectangular holes cut out of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Drawn, not masked.</b> The mesh is the screen minus the holes, built as a grid of quads
    /// over every hole edge with the cells inside a hole left out. Three holes make at most a 7x7
    /// grid, so there is no stencil buffer, no mask component and no second canvas involved.
    /// </para>
    /// <para>
    /// <b>Clicks.</b> As an <see cref="ICanvasRaycastFilter"/> the scrim tells the event system
    /// whether a point hits it. Everywhere outside a hole it does, and swallows the click. Inside a
    /// hole marked pass-through it does not, so the click falls to whatever is underneath — the
    /// HUD button being pointed at, or the board. Holes that only draw attention still block.
    /// </para>
    /// </remarks>
    [AddComponentMenu("")]
    public sealed class SpotlightScrim : MaskableGraphic, ICanvasRaycastFilter
    {
        private readonly List<Rect> localHoles = new List<Rect>();
        private readonly List<Rect> passThroughScreen = new List<Rect>();
        private readonly List<float> xs = new List<float>();
        private readonly List<float> ys = new List<float>();

        /// <summary>
        /// Replaces the holes.
        /// </summary>
        /// <param name="screenHoles">Hole rectangles in screen pixels.</param>
        /// <param name="passThrough">Per hole, whether clicks inside it reach what is underneath.</param>
        /// <param name="scaleFactor">The canvas scale, pixels per canvas unit.</param>
        /// <returns>True when the geometry changed and the mesh was rebuilt.</returns>
        public bool SetHoles(List<Rect> screenHoles, List<bool> passThrough, float scaleFactor)
        {
            float scale = scaleFactor > 0f ? scaleFactor : 1f;
            Rect bounds = rectTransform.rect;

            passThroughScreen.Clear();
            bool changed = screenHoles.Count != localHoles.Count;

            for (int i = 0; i < screenHoles.Count; i++)
            {
                Rect screen = screenHoles[i];
                if (passThrough[i])
                {
                    passThroughScreen.Add(screen);
                }

                Rect local = new Rect(
                    bounds.xMin + (screen.xMin / scale),
                    bounds.yMin + (screen.yMin / scale),
                    screen.width / scale,
                    screen.height / scale);

                if (i < localHoles.Count)
                {
                    if (!Approximately(localHoles[i], local))
                    {
                        localHoles[i] = local;
                        changed = true;
                    }
                }
                else
                {
                    localHoles.Add(local);
                }
            }

            if (localHoles.Count > screenHoles.Count)
            {
                localHoles.RemoveRange(screenHoles.Count, localHoles.Count - screenHoles.Count);
            }

            if (changed)
            {
                SetVerticesDirty();
            }

            return changed;
        }

        /// <inheritdoc />
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            for (int i = 0; i < passThroughScreen.Count; i++)
            {
                if (passThroughScreen[i].Contains(screenPoint))
                {
                    return false;
                }
            }

            return true;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect bounds = GetPixelAdjustedRect();

            xs.Clear();
            ys.Clear();
            xs.Add(bounds.xMin);
            xs.Add(bounds.xMax);
            ys.Add(bounds.yMin);
            ys.Add(bounds.yMax);

            for (int i = 0; i < localHoles.Count; i++)
            {
                Rect hole = localHoles[i];
                xs.Add(Mathf.Clamp(hole.xMin, bounds.xMin, bounds.xMax));
                xs.Add(Mathf.Clamp(hole.xMax, bounds.xMin, bounds.xMax));
                ys.Add(Mathf.Clamp(hole.yMin, bounds.yMin, bounds.yMax));
                ys.Add(Mathf.Clamp(hole.yMax, bounds.yMin, bounds.yMax));
            }

            xs.Sort();
            ys.Sort();

            Color32 tint = color;
            for (int xi = 0; xi < xs.Count - 1; xi++)
            {
                float x0 = xs[xi];
                float x1 = xs[xi + 1];
                if (x1 - x0 < 0.01f)
                {
                    continue;
                }

                for (int yi = 0; yi < ys.Count - 1; yi++)
                {
                    float y0 = ys[yi];
                    float y1 = ys[yi + 1];
                    if (y1 - y0 < 0.01f)
                    {
                        continue;
                    }

                    Vector2 centre = new Vector2((x0 + x1) * 0.5f, (y0 + y1) * 0.5f);
                    if (InsideHole(centre))
                    {
                        continue;
                    }

                    int start = vh.currentVertCount;
                    vh.AddVert(new Vector3(x0, y0), tint, Vector4.zero);
                    vh.AddVert(new Vector3(x0, y1), tint, Vector4.zero);
                    vh.AddVert(new Vector3(x1, y1), tint, Vector4.zero);
                    vh.AddVert(new Vector3(x1, y0), tint, Vector4.zero);
                    vh.AddTriangle(start, start + 1, start + 2);
                    vh.AddTriangle(start + 2, start + 3, start);
                }
            }
        }

        private bool InsideHole(Vector2 point)
        {
            for (int i = 0; i < localHoles.Count; i++)
            {
                if (localHoles[i].Contains(point))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Approximately(Rect a, Rect b)
        {
            const float Tolerance = 0.5f;
            return Mathf.Abs(a.xMin - b.xMin) < Tolerance
                && Mathf.Abs(a.yMin - b.yMin) < Tolerance
                && Mathf.Abs(a.width - b.width) < Tolerance
                && Mathf.Abs(a.height - b.height) < Tolerance;
        }
    }
}
