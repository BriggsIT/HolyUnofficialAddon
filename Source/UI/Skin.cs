using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI;

// A window's own look, drawn over the game's UI pieces: MMO Management as a dark game launcher (navy and gold),
// the digital store in your store's colours. EditorWindow and ListView ask the skin how to draw labels, headers,
// panels, rows, buttons and inputs, so every page in a skinned window gets the look without drawing it itself.
internal class Skin
{
	public string Name;

	// The window frame.
	public Color Border;

	public Color Background;

	public Color Bar;

	public Color Title;

	// Behind the page.
	public Color Body;

	public Color Glow;

	public Color Panel;

	public Color PanelLine;

	public Color Row;

	public Color RowLine;

	public Color RowHighlight;

	public Color RowHighlightLine;

	public Color Ink;

	public Color InkDim;

	public Color Accent;

	public Color AccentLight;

	// Main buttons: filled with the accent colour.
	public Color Primary;

	public Color PrimaryText;

	// Other buttons: dark with an accent edge.
	public Color Secondary;

	public Color SecondaryText;

	// Readable versions of the game's text colours on a dark background.
	public Color Good = new Color(0.47f, 0.86f, 0.53f);

	public Color Bad = new Color(1f, 0.47f, 0.45f);

	public Color Muted = new Color(0.6f, 0.64f, 0.71f);

	public Color Info = new Color(0.55f, 0.75f, 1f);

	public Color Warn = new Color(1f, 0.72f, 0.3f);

	// Upper-case headers.
	public bool CapsHeaders = true;

	// The rich-text colours the pages use, made readable on a dark background.
	private Dictionary<string, string> textColors;

	public string Fix(string text)
	{
		if (string.IsNullOrEmpty(text) || text.IndexOf("<color=", System.StringComparison.Ordinal) < 0)
		{
			return text;
		}
		if (textColors == null)
		{
			string dim = "#" + ColorUtility.ToHtmlStringRGB(InkDim);
			textColors = new Dictionary<string, string>
			{
				{ "#666666", dim },
				{ "#666", dim },
				{ "#c62828", "#ff7a72" },
				{ "#2e7d32", "#7ee08a" },
				{ "#1565c0", "#8fc0ff" },
				{ "#2a7ad0", "#8fc0ff" },
				{ "#8e44ad", "#c9a0ff" },
				{ "#8a6d1f", "#e8c77a" },
				{ "red", "#ff7a72" },
				{ "blue", "#8fc0ff" },
				{ "green", "#7ee08a" }
			};
		}
		foreach (KeyValuePair<string, string> kv in textColors)
		{
			text = text.Replace("<color=" + kv.Key + ">", "<color=" + kv.Value + ">");
		}
		return text;
	}

	// The game's own text colours as this skin shows them (UIKit.Red/Green/Grey/Blue/Orange while it draws).
	public Color Map(Color c)
	{
		if (c == UIKit.BaseRed)
		{
			return Bad;
		}
		if (c == UIKit.BaseGreen)
		{
			return Good;
		}
		if (c == UIKit.BaseGrey)
		{
			return Muted;
		}
		if (c == UIKit.BaseBlue)
		{
			return Info;
		}
		if (c == UIKit.BaseOrange)
		{
			return Warn;
		}
		return c;
	}

	// ---- The two skins ----

	public static readonly Color Gold = new Color(0.86f, 0.7f, 0.35f);

	public static readonly Color GoldLight = new Color(0.96f, 0.85f, 0.56f);

	// MMO Management: a dark fantasy game launcher, navy with gold.
	public static readonly Skin Launcher = new Skin
	{
		Name = "launcher",
		Border = new Color(0.55f, 0.44f, 0.2f),
		Background = new Color(0.04f, 0.06f, 0.1f),
		Bar = new Color(0.06f, 0.09f, 0.14f),
		Title = GoldLight,
		Body = new Color(0.05f, 0.075f, 0.12f),
		Glow = new Color(0.86f, 0.7f, 0.35f, 0.07f),
		Panel = new Color(0.085f, 0.125f, 0.19f, 0.96f),
		PanelLine = new Color(0.86f, 0.7f, 0.35f, 0.28f),
		Row = new Color(0.1f, 0.145f, 0.215f),
		RowLine = new Color(0.86f, 0.7f, 0.35f, 0.16f),
		RowHighlight = new Color(0.2f, 0.17f, 0.09f),
		RowHighlightLine = Gold,
		Ink = new Color(0.9f, 0.92f, 0.95f),
		InkDim = new Color(0.6f, 0.65f, 0.73f),
		Accent = Gold,
		AccentLight = GoldLight,
		Primary = new Color(0.82f, 0.64f, 0.28f),
		PrimaryText = new Color(0.1f, 0.075f, 0.02f),
		Secondary = new Color(0.11f, 0.16f, 0.24f),
		SecondaryText = new Color(0.93f, 0.9f, 0.82f)
	};

	private static readonly Dictionary<string, Skin> stores = new Dictionary<string, Skin>();

	// The digital store: dark slate like the PC launchers, in the colour of your store's logo.
	public static Skin Store(Color accent)
	{
		string key = ColorUtility.ToHtmlStringRGB(accent);
		if (stores.TryGetValue(key, out var skin))
		{
			return skin;
		}
		Color.RGBToHSV(accent, out float h, out float s, out float v);
		Color strong = Color.HSVToRGB(h, Mathf.Clamp(s, 0.45f, 0.85f), Mathf.Clamp(v, 0.7f, 0.9f));
		Color light = Color.HSVToRGB(h, Mathf.Clamp(s * 0.55f, 0.25f, 0.6f), 1f);
		Color dark = Color.HSVToRGB(h, Mathf.Clamp(s, 0.4f, 0.8f), 0.32f);
		skin = new Skin
		{
			Name = "store-" + key,
			Border = dark,
			Background = new Color(0.055f, 0.065f, 0.09f),
			Bar = new Color(0.08f, 0.095f, 0.13f),
			Title = Color.white,
			Body = new Color(0.07f, 0.085f, 0.115f),
			Glow = new Color(strong.r, strong.g, strong.b, 0.1f),
			Panel = new Color(0.1f, 0.12f, 0.16f, 0.97f),
			PanelLine = new Color(strong.r, strong.g, strong.b, 0.3f),
			Row = new Color(0.115f, 0.14f, 0.185f),
			RowLine = new Color(1f, 1f, 1f, 0.06f),
			RowHighlight = new Color(strong.r * 0.35f, strong.g * 0.35f, strong.b * 0.35f),
			RowHighlightLine = strong,
			Ink = new Color(0.91f, 0.93f, 0.96f),
			InkDim = new Color(0.6f, 0.64f, 0.71f),
			Accent = strong,
			AccentLight = light,
			Primary = strong,
			PrimaryText = Color.white,
			Secondary = new Color(0.15f, 0.18f, 0.24f),
			SecondaryText = new Color(0.9f, 0.92f, 0.96f),
			CapsHeaders = false
		};
		stores[key] = skin;
		return skin;
	}

	// ---- Shapes ----

	private static Sprite rounded;

	private static Sprite ring;

	private static Sprite fadeLeft;

	private static Sprite fadeUp;

	// A white rounded rectangle (9-sliced, so it stretches to any size).
	public static Sprite Rounded()
	{
		if (!rounded)
		{
			rounded = Shape(false);
		}
		return rounded;
	}

	// Its outline.
	public static Sprite Ring()
	{
		if (!ring)
		{
			ring = Shape(true);
		}
		return ring;
	}

	private static Sprite Shape(bool outline)
	{
		const int size = 32;
		const float radius = 7f;
		Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
		{
			name = outline ? "Holy_SkinRing" : "Holy_SkinRounded",
			wrapMode = TextureWrapMode.Clamp,
			filterMode = FilterMode.Bilinear,
			hideFlags = HideFlags.DontUnloadUnusedAsset
		};
		Color[] px = new Color[size * size];
		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				// Distance outside the rounded rectangle (negative inside).
				float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
				float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
				float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)) - radius;
				float a = Mathf.Clamp01(0.5f - d);
				if (outline)
				{
					a *= Mathf.Clamp01(d + 1.6f);
				}
				px[y * size + x] = new Color(1f, 1f, 1f, a);
			}
		}
		tex.SetPixels(px);
		tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);
		Sprite s = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, new Vector4(9f, 9f, 9f, 9f));
		s.name = tex.name;
		s.hideFlags = HideFlags.DontUnloadUnusedAsset;
		return s;
	}

	// White fading to clear: towards the right (left = solid), or towards the top (bottom = solid).
	public static Sprite Fade(bool up)
	{
		Sprite cached = (up ? fadeUp : fadeLeft);
		if ((bool)cached)
		{
			return cached;
		}
		int n = 64;
		Texture2D tex = new Texture2D(up ? 1 : n, up ? n : 1, TextureFormat.RGBA32, mipChain: false)
		{
			name = up ? "Holy_FadeUp" : "Holy_FadeLeft",
			wrapMode = TextureWrapMode.Clamp,
			filterMode = FilterMode.Bilinear,
			hideFlags = HideFlags.DontUnloadUnusedAsset
		};
		Color[] px = new Color[n];
		for (int i = 0; i < n; i++)
		{
			float t = (float)i / (float)(n - 1);
			px[i] = new Color(1f, 1f, 1f, Mathf.SmoothStep(1f, 0f, t));
		}
		tex.SetPixels(px);
		tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);
		Sprite s = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
		s.name = tex.name;
		s.hideFlags = HideFlags.DontUnloadUnusedAsset;
		if (up)
		{
			fadeUp = s;
		}
		else
		{
			fadeLeft = s;
		}
		return s;
	}

	// ---- Dressing the game's pieces ----

	public static Image Shape(Transform parent, string name, Color color, bool outline)
	{
		Image img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
		img.transform.SetParent(parent, worldPositionStays: false);
		UIKit.Stretch(img.transform);
		img.sprite = outline ? Ring() : Rounded();
		img.type = Image.Type.Sliced;
		img.color = color;
		img.raycastTarget = false;
		return img;
	}

	// A filled rounded box with an edge, on a game piece's own Image.
	public void Box(GameObject go, Color fill, Color line)
	{
		Image img = go.GetComponent<Image>();
		if ((bool)img)
		{
			img.sprite = Rounded();
			img.type = Image.Type.Sliced;
			img.color = fill;
			img.material = null;
		}
		if (line.a > 0.001f)
		{
			// Drawn over the fill (the piece's own Image), under its text.
			Image edge = Shape(go.transform, "SkinEdge", line, outline: true);
			edge.transform.SetAsFirstSibling();
		}
	}

	// A button: the main kind filled with the accent colour, the others dark with an accent edge.
	public void DressButton(GameObject go, bool primary)
	{
		foreach (Image child in go.GetComponentsInChildren<Image>(includeInactive: true))
		{
			if (child.gameObject != go)
			{
				child.enabled = false;
			}
		}
		Box(go, primary ? Primary : Secondary, primary ? new Color(1f, 1f, 1f, 0.18f) : new Color(Accent.r, Accent.g, Accent.b, 0.55f));
		UnityEngine.UI.Button button = go.GetComponent<UnityEngine.UI.Button>();
		if ((bool)button)
		{
			button.transition = Selectable.Transition.ColorTint;
			ColorBlock colors = button.colors;
			colors.normalColor = Color.white;
			colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
			colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
			colors.selectedColor = Color.white;
			colors.disabledColor = new Color(0.65f, 0.65f, 0.65f, 0.45f);
			colors.colorMultiplier = 1f;
			button.colors = colors;
		}
		foreach (Text t in go.GetComponentsInChildren<Text>(includeInactive: true))
		{
			t.color = primary ? PrimaryText : SecondaryText;
			Shadow shadow = t.GetComponent<Shadow>();
			if ((bool)shadow)
			{
				shadow.enabled = false;
			}
		}
	}

	// A list row: dark, lighter under the pointer.
	public void DressRow(GameObject go)
	{
		Box(go, Row, RowLine);
		UnityEngine.UI.Button button = go.GetComponent<UnityEngine.UI.Button>();
		if ((bool)button && button.transition == Selectable.Transition.ColorTint)
		{
			ColorBlock colors = button.colors;
			colors.normalColor = Color.white;
			colors.highlightedColor = new Color(1.45f, 1.45f, 1.45f, 1f);
			colors.pressedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
			colors.selectedColor = Color.white;
			colors.disabledColor = Color.white;
			colors.colorMultiplier = 1f;
			button.colors = colors;
		}
	}

	public void HighlightRow(Transform row)
	{
		Image img = row.GetComponent<Image>();
		if ((bool)img)
		{
			img.color = RowHighlight;
		}
		Transform edge = row.Find("SkinEdge");
		if ((bool)edge)
		{
			edge.GetComponent<Image>().color = RowHighlightLine;
		}
	}

	public void DressScroll(GameObject go)
	{
		Image img = go.GetComponent<Image>();
		if ((bool)img)
		{
			img.color = new Color(0f, 0f, 0f, 0.18f);
			img.sprite = Rounded();
			img.type = Image.Type.Sliced;
		}
		Scrollbar bar = go.GetComponentInChildren<Scrollbar>(includeInactive: true);
		if ((bool)bar)
		{
			Image track = bar.GetComponent<Image>();
			if ((bool)track)
			{
				track.color = new Color(0f, 0f, 0f, 0.35f);
			}
			if ((bool)bar.handleRect)
			{
				Image handle = bar.handleRect.GetComponent<Image>();
				if ((bool)handle)
				{
					handle.color = new Color(Accent.r, Accent.g, Accent.b, 0.7f);
				}
			}
		}
	}

	public void DressDropdown(GameObject go)
	{
		Box(go, Secondary, new Color(Accent.r, Accent.g, Accent.b, 0.55f));
		Transform label = go.transform.Find("Label");
		if ((bool)label)
		{
			label.GetComponent<Text>().color = SecondaryText;
		}
		Transform arrow = go.transform.Find("Arrow");
		if ((bool)arrow && (bool)arrow.GetComponent<Image>())
		{
			arrow.GetComponent<Image>().color = Accent;
		}
	}

	public void DressInput(GameObject go)
	{
		Transform square = go.transform.Find("Square");
		if ((bool)square)
		{
			Box(square.gameObject, new Color(0.03f, 0.04f, 0.06f, 0.9f), new Color(Accent.r, Accent.g, Accent.b, 0.5f));
		}
		Transform text = go.transform.Find("Text");
		if ((bool)text)
		{
			text.GetComponent<Text>().color = Ink;
		}
		Transform hint = go.transform.Find("Placeholder");
		if ((bool)hint)
		{
			hint.GetComponent<Text>().color = InkDim;
		}
	}
}
