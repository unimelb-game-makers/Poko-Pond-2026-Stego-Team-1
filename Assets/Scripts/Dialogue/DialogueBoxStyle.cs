using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generates an Undertale-style dialogue box sprite at runtime:
/// black fill, white border, slightly rounded pixelated corners.
/// Attach to the DialoguePanel GameObject (requires an Image component).
/// </summary>
[RequireComponent(typeof(Image))]
public class DialogueBoxStyle : MonoBehaviour
{
    [Tooltip("Border thickness in pixels.")]
    [SerializeField] private int borderWidth = 3;

    [Tooltip("Corner cut size in pixels (1-2 for subtle rounding).")]
    [SerializeField] private int cornerSize = 2;

    [Tooltip("Texture resolution. Stretched via 9-slice so can be small.")]
    [SerializeField] private int textureSize = 32;

    [SerializeField] private Color borderColor = Color.white;
    [SerializeField] private Color fillColor   = Color.black;

    private void Awake()
    {
        var tex = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                // Check if pixel is in a cut corner
                if (IsCornerCut(x, y))
                {
                    tex.SetPixel(x, y, Color.clear);
                }
                else if (IsBorder(x, y))
                {
                    tex.SetPixel(x, y, borderColor);
                }
                else
                {
                    tex.SetPixel(x, y, fillColor);
                }
            }
        }

        tex.Apply();

        int b = borderWidth + cornerSize;
        var sprite = Sprite.Create(
            tex,
            new Rect(0, 0, textureSize, textureSize),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(b, b, b, b) // 9-slice borders
        );

        var img = GetComponent<Image>();
        img.sprite = sprite;
        img.type   = Image.Type.Sliced;
        img.color  = Color.white;
    }

    private int CornerDist(int x, int y)
    {
        // Returns the minimum diagonal distance to any corner.
        // A pixel with CornerDist < cornerSize is fully outside (cut).
        // A pixel with CornerDist < cornerSize + borderWidth is border in the corner region.
        int maxIdx = textureSize - 1;
        int bl = x + y;
        int br = (maxIdx - x) + y;
        int tl = x + (maxIdx - y);
        int tr = (maxIdx - x) + (maxIdx - y);
        return Mathf.Min(bl, Mathf.Min(br, Mathf.Min(tl, tr)));
    }

    private bool IsCornerCut(int x, int y)
    {
        return CornerDist(x, y) < cornerSize;
    }

    private bool IsBorder(int x, int y)
    {
        int maxIdx = textureSize - 1;

        // Border along the corner diagonals
        if (CornerDist(x, y) < cornerSize + borderWidth) return true;

        // Border along straight edges
        return x < borderWidth || y < borderWidth ||
               x > maxIdx - borderWidth || y > maxIdx - borderWidth;
    }
}
