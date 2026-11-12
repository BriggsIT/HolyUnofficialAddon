using System;
using System.Collections.Generic;
using UnityEngine;

namespace HolyAddon.UI;

// Small sprite helpers: a grey copy of a game sprite (to recolour it and keep its shading), and a soft glow.
internal static class SpriteTools
{
	private static readonly Dictionary<Sprite, Sprite> greys = new Dictionary<Sprite, Sprite>();

	private static Sprite glow;

	// The sprite in shades of grey, its average brightness lifted to about 90%: tinted with a colour it looks like
	// the original in that colour (gloss, edges and rounded corners kept). Null if it can't be read.
	public static Sprite GreyCopy(Sprite s)
	{
		if (!s)
		{
			return null;
		}
		if (greys.TryGetValue(s, out var cached))
		{
			return cached;
		}
		Sprite result = null;
		try
		{
			Rect packed = MmoCards.PackedRect(s);
			int w = Mathf.Max(1, Mathf.RoundToInt(packed.width));
			int h = Mathf.Max(1, Mathf.RoundToInt(packed.height));
			Color[] px = MmoCards.ReadRegion(s.texture, packed, w, h);
			float sum = 0f;
			int count = 0;
			for (int i = 0; i < px.Length; i++)
			{
				if (px[i].a > 0.5f)
				{
					sum += px[i].maxColorComponent;
					count++;
				}
			}
			float average = ((count > 0) ? (sum / (float)count) : 1f);
			float scale = 0.9f / Mathf.Max(0.05f, average);
			for (int j = 0; j < px.Length; j++)
			{
				float v = Mathf.Clamp01(px[j].maxColorComponent * scale);
				px[j] = new Color(v, v, v, px[j].a);
			}
			Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false)
			{
				name = "Holy_Grey_" + s.name,
				wrapMode = TextureWrapMode.Clamp,
				filterMode = FilterMode.Bilinear,
				hideFlags = HideFlags.DontUnloadUnusedAsset
			};
			tex.SetPixels(px);
			tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);
			Vector2 pivot = new Vector2(s.pivot.x / Mathf.Max(1f, s.rect.width), s.pivot.y / Mathf.Max(1f, s.rect.height));
			result = Sprite.Create(tex, new Rect(0f, 0f, w, h), pivot, s.pixelsPerUnit, 0u, SpriteMeshType.FullRect, s.border);
			result.name = tex.name;
			result.hideFlags = HideFlags.DontUnloadUnusedAsset;
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Grey copy of " + s.name + ": " + ex.Message);
		}
		greys[s] = result;
		return result;
	}

	private static readonly Dictionary<Sprite, Color> mainColors = new Dictionary<Sprite, Color>();

	// A picture's main colour: the average of its strongly coloured pixels (for a title bar in a logo's colour).
	public static Color MainColor(Sprite s, Color fallback)
	{
		if (!s)
		{
			return fallback;
		}
		if (mainColors.TryGetValue(s, out var cached))
		{
			return cached;
		}
		Color c = fallback;
		try
		{
			Rect packed = MmoCards.PackedRect(s);
			int w = Mathf.Max(1, Mathf.RoundToInt(packed.width));
			int h = Mathf.Max(1, Mathf.RoundToInt(packed.height));
			Color[] px = MmoCards.ReadRegion(s.texture, packed, w, h);
			float r = 0f;
			float g = 0f;
			float b = 0f;
			float weight = 0f;
			for (int i = 0; i < px.Length; i += 2)
			{
				Color p = px[i];
				if (p.a < 0.6f)
				{
					continue;
				}
				Color.RGBToHSV(p, out float _, out float sat, out float val);
				if (sat >= 0.35f && val >= 0.3f)
				{
					float k = sat * p.a;
					r += p.r * k;
					g += p.g * k;
					b += p.b * k;
					weight += k;
				}
			}
			if (weight > 1f)
			{
				c = new Color(r / weight, g / weight, b / weight);
				// Dark enough for white title text.
				Color.RGBToHSV(c, out float hh, out float ss, out float vv);
				c = Color.HSVToRGB(hh, ss, Mathf.Min(vv, 0.72f));
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Main colour of " + s.name + ": " + ex.Message);
		}
		mainColors[s] = c;
		return c;
	}

	// A round soft glow: white in the middle, clear at the edge.
	public static Sprite Glow()
	{
		if ((bool)glow)
		{
			return glow;
		}
		const int size = 64;
		Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
		{
			name = "Holy_Glow",
			wrapMode = TextureWrapMode.Clamp,
			filterMode = FilterMode.Bilinear,
			hideFlags = HideFlags.DontUnloadUnusedAsset
		};
		Color[] px = new Color[size * size];
		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
				float a = Mathf.Clamp01(1f - d);
				px[y * size + x] = new Color(1f, 1f, 1f, a * a);
			}
		}
		tex.SetPixels(px);
		tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);
		glow = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
		glow.name = "Holy_Glow";
		glow.hideFlags = HideFlags.DontUnloadUnusedAsset;
		return glow;
	}
}
