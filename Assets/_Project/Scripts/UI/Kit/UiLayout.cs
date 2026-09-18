using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BinakayanRising.UI.Kit
{
    /// <summary>
    /// Layout-element shorthands for building screens inside layout groups.
    /// </summary>
    /// <remarks>
    /// A layout group sizes its children from their <see cref="LayoutElement"/>, not from their
    /// <c>sizeDelta</c>, so a rect sized any other way is silently resized the next frame. These
    /// helpers are the only way screens size things inside a group.
    /// </remarks>
    public static class UiLayout
    {
        /// <summary>The rect's layout element, added if missing.</summary>
        public static LayoutElement Element(RectTransform rect)
        {
            LayoutElement element = rect.GetComponent<LayoutElement>();
            return element != null ? element : rect.gameObject.AddComponent<LayoutElement>();
        }

        /// <summary>Fixes a height, and a width when non-zero.</summary>
        public static RectTransform Fix(RectTransform rect, float width, float height)
        {
            LayoutElement element = Element(rect);
            element.minHeight = height;
            element.preferredHeight = height;
            element.flexibleHeight = 0f;
            if (width > 0f)
            {
                element.minWidth = width;
                element.preferredWidth = width;
                element.flexibleWidth = 0f;
            }

            return rect;
        }

        /// <summary>Lets the rect take whatever width its row has left.</summary>
        public static RectTransform Flexible(RectTransform rect)
        {
            LayoutElement element = Element(rect);
            element.minWidth = 0f;
            element.preferredWidth = 0f;
            element.flexibleWidth = 1f;
            return rect;
        }

        /// <summary>Lets the rect take whatever height its column has left.</summary>
        public static RectTransform FlexibleHeight(RectTransform rect)
        {
            LayoutElement element = Element(rect);
            element.minHeight = 0f;
            element.preferredHeight = 0f;
            element.flexibleHeight = 1f;
            return rect;
        }

        /// <summary>
        /// Keeps a label on one line, shrinking it to fit before cutting it off. Filipino runs
        /// longer than English, and this is what stops it spilling out of its box.
        /// </summary>
        public static TextMeshProUGUI OneLine(TextMeshProUGUI label, float maxSize)
        {
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMax = maxSize;
            label.fontSizeMin = Mathf.Max(10f, maxSize * 0.6f);
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        /// <summary>Makes a column stretch its children to its own width.</summary>
        public static RectTransform FillWidth(RectTransform column)
        {
            var group = column.GetComponent<HorizontalOrVerticalLayoutGroup>();
            if (group != null)
            {
                group.childForceExpandWidth = true;
            }

            return column;
        }
    }
}
