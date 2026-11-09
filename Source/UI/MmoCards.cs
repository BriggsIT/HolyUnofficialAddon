using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HolyAddon.Core.Mmo;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI;

// The number and the blue calendar on an MMO's card: its place in the MMO subscription charts.
internal class MmoRankBadge : MonoBehaviour
{
	public Text Number;

	public Image Icon;

	// The card's own weekly chart place and red calendar, which the badge copies its sizes from.
	public Text SourceText;

	public Image SourceIcon;

	public int Rank = int.MinValue;

	public float NextLayout;
}

// The game cards on the right of the screen, for MMOs:
// - an MMO's card shows its place in the MMO subscription charts as a blue calendar, a copy of the card's red
//   weekly-charts calendar, in the free space of the icon line at the top;
// - MMO addons don't get a card there: they're listed on their MMO's page in MMO Management.
[HarmonyPatch]
internal static class MmoCards
{
	private const string BadgeName = "Holy_MmoRank";

	public static readonly Color Blue = new Color(0.16f, 0.48f, 0.82f);

	private static Sprite blueCalendar;

	private static bool blueTried;

	private static bool failed;

	private static bool dumped;

	// Cards whose bars were redrawn once MMO Management had loaded the save.
	private static readonly HashSet<int> refreshed = new HashSet<int>();

	private static readonly Vector3[] corners = new Vector3[4];

	// The card's parts, written to the log once a session (paths, positions, sizes) to check the badge's place.
	private static void Dump(gameTab tab)
	{
		try
		{
			List<string> lines = new List<string>();
			RectTransform card = (RectTransform)tab.transform;
			GameObject[] ui = tab.uiObjects;
			foreach (RectTransform rt in tab.GetComponentsInChildren<RectTransform>(includeInactive: true))
			{
				string path = (rt == card) ? "(card)" : rt.name;
				Transform p = rt.parent;
				while ((bool)p && p != card)
				{
					path = p.name + "/" + path;
					p = p.parent;
				}
				Rect r = LocalRect(card, rt);
				string what = "";
				int index = Array.IndexOf(ui, rt.gameObject);
				if (index >= 0)
				{
					what += " ui[" + index + "]";
				}
				if (tab.uiBalken != null && Array.IndexOf(tab.uiBalken, rt.gameObject) >= 0)
				{
					what += " bar";
				}
				Image image = rt.GetComponent<Image>();
				if ((bool)image)
				{
					what += " Image(" + image.type + ", " + (image.sprite ? image.sprite.name : "-") + ", " + ColorUtility.ToHtmlStringRGBA(image.color) + ")";
				}
				Text text = rt.GetComponent<Text>();
				if ((bool)text)
				{
					what += " Text(\"" + text.text + "\", " + text.fontSize + (text.resizeTextForBestFit ? " bestfit" : "") + ", " + text.alignment + ")";
				}
				lines.Add("  " + path + (rt.gameObject.activeInHierarchy ? "" : " [off]") + "  x " + Mathf.RoundToInt(r.xMin) + ".." + Mathf.RoundToInt(r.xMax) + "  y " + Mathf.RoundToInt(r.yMin) + ".." + Mathf.RoundToInt(r.yMax) + what);
			}
			Plugin.Log.LogInfo("MMO card layout (" + tab.gS_.GetNameSimple() + "):\n" + string.Join("\n", lines));
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("MMO card layout: " + ex.Message);
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(gameTab), "Update")]
	private static void ChartBadge(gameTab __instance)
	{
		try
		{
			GameObject[] ui = __instance.uiObjects;
			if (ui == null || ui.Length <= 19 || !ui[9] || !ui[19])
			{
				return;
			}
			Transform host = ui[9].transform.parent;
			if (!host)
			{
				return;
			}
			Transform found = host.Find(BadgeName);
			MmoRankBadge badge = (found ? found.GetComponent<MmoRankBadge>() : null);
			gameScript g = __instance.gS_;
			if (!MmoOps.On || !g || g.gameTyp != 1 || g.typ_mmoaddon || !g.isOnMarket || g.inDevelopment)
			{
				if ((bool)badge && badge.gameObject.activeSelf)
				{
					badge.gameObject.SetActive(value: false);
				}
				return;
			}
			// The card's bars were drawn before MMO Management loaded the save: draw them again once it has.
			int id = __instance.GetInstanceID();
			if (!refreshed.Contains(id) && MmoOps.Find(g.myID) != null)
			{
				refreshed.Add(id);
				__instance.UpdateData();
			}
			// Only build and place the badge on a fully open card: a folded one has everything on top of each other.
			bool full = g.gameTab_fullView && ui[19].activeInHierarchy;
			if (!badge)
			{
				if (!full)
				{
					return;
				}
				if (!dumped)
				{
					dumped = true;
					Dump(__instance);
				}
				badge = MakeBadge(__instance, host);
				if (!badge)
				{
					return;
				}
			}
			if (!badge.gameObject.activeSelf)
			{
				badge.gameObject.SetActive(value: true);
				badge.NextLayout = 0f;
			}
			int rank = MmoOps.ChartRank(g);
			if (rank != badge.Rank)
			{
				badge.Rank = rank;
				badge.Number.text = ((rank > 0) ? rank.ToString() : "-");
				string tip = "<color=#2a7ad0>MMO subscription charts: " + ((rank > 0) ? ("place " + rank + " of " + MmoOps.ChartSize() + " MMOs.") : "not in them.") + "</color>\nThe red calendar below is the weekly sales charts.";
				UIKit.SetTooltip(badge.Number.gameObject, tip);
				if ((bool)badge.Icon)
				{
					UIKit.SetTooltip(badge.Icon.gameObject, tip);
				}
				badge.NextLayout = 0f;
			}
			if (full && Time.unscaledTime >= badge.NextLayout)
			{
				badge.NextLayout = Time.unscaledTime + 1f;
				Layout(__instance, badge, host);
			}
		}
		catch (Exception ex)
		{
			if (!failed)
			{
				failed = true;
				Plugin.Log.LogError("MMO card chart place: " + ex);
			}
		}
	}

	// Copies of the card's weekly chart place and its calendar, the calendar recoloured blue.
	private static MmoRankBadge MakeBadge(gameTab tab, Transform host)
	{
		Text source = tab.uiObjects[19].GetComponent<Text>();
		if (!source)
		{
			return null;
		}
		Image calendar = FindCalendar(tab, source);
		GameObject root = new GameObject(BadgeName, typeof(RectTransform));
		root.transform.SetParent(host, worldPositionStays: false);
		Center((RectTransform)root.transform, 0f, 0f, 0f, 0f);
		MmoRankBadge badge = root.AddComponent<MmoRankBadge>();
		badge.SourceText = source;
		badge.SourceIcon = calendar;
		GameObject number = Copy(source.gameObject, root.transform, "Number");
		Text text = number.GetComponent<Text>();
		// Best fit sizes the text to its box, which the badge sizes to the text: fix the size the card shows.
		if (text.resizeTextForBestFit)
		{
			int used = source.cachedTextGenerator.fontSizeUsedForBestFit;
			text.resizeTextForBestFit = false;
			if (used > 0)
			{
				text.fontSize = used;
			}
		}
		text.color = Blue;
		text.alignment = TextAnchor.MiddleRight;
		text.horizontalOverflow = HorizontalWrapMode.Overflow;
		text.raycastTarget = true;
		text.text = "-";
		badge.Number = text;
		if ((bool)calendar)
		{
			GameObject icon = Copy(calendar.gameObject, root.transform, "Icon");
			Image image = icon.GetComponent<Image>();
			Sprite blue = BlueCopy(calendar.sprite);
			if ((bool)blue)
			{
				image.sprite = blue;
			}
			else
			{
				image.color = new Color(0.55f, 0.75f, 1f);
			}
			image.raycastTarget = true;
			badge.Icon = image;
		}
		return badge;
	}

	private static GameObject Copy(GameObject template, Transform parent, string name)
	{
		GameObject copy = UnityEngine.Object.Instantiate(template, parent, worldPositionStays: false);
		copy.name = name;
		for (int i = copy.transform.childCount - 1; i >= 0; i--)
		{
			UnityEngine.Object.Destroy(copy.transform.GetChild(i).gameObject);
		}
		setText[] setters = copy.GetComponents<setText>();
		for (int j = 0; j < setters.Length; j++)
		{
			UnityEngine.Object.DestroyImmediate(setters[j]);
		}
		copy.SetActive(value: true);
		return copy;
	}

	// The calendar is the icon just right of the weekly chart place, on the same line.
	private static Image FindCalendar(gameTab tab, Text source)
	{
		// Its own row first, then the whole card.
		Transform row = source.transform.parent;
		Image found = (row ? FindCalendarIn(tab, source, row) : null);
		if (!found)
		{
			found = FindCalendarIn(tab, source, tab.transform);
		}
		return found;
	}

	private static Image FindCalendarIn(gameTab tab, Text source, Transform within)
	{
		Rect s = WorldRect((RectTransform)source.transform);
		float right = VisibleRight(source, s);
		Image best = null;
		float bestGap = float.MaxValue;
		foreach (Image image in within.GetComponentsInChildren<Image>(includeInactive: false))
		{
			if (!Icon(tab, image))
			{
				continue;
			}
			Rect r = WorldRect(image.rectTransform);
			if (r.height > s.height * 3f)
			{
				continue;
			}
			if (Mathf.Abs(r.center.y - s.center.y) > Mathf.Max(r.height, s.height) * 0.5f)
			{
				continue;
			}
			float gap = r.xMin - right;
			if (gap < (0f - r.width) * 0.5f)
			{
				continue;
			}
			if (gap < bestGap)
			{
				bestGap = gap;
				best = image;
			}
		}
		return best;
	}

	// A picture shown as an icon: a roughly square sprite drawn whole. Bars (the sales chart, interest and wear)
	// are filled images; panels and lines aren't square.
	private static bool Icon(gameTab tab, Image image)
	{
		if (!image.sprite || image.type == Image.Type.Filled || image.type == Image.Type.Sliced || image.type == Image.Type.Tiled)
		{
			return false;
		}
		if ((tab.uiBalken != null && Array.IndexOf(tab.uiBalken, image.gameObject) >= 0) || (tab.uiBalkenOnline != null && Array.IndexOf(tab.uiBalkenOnline, image.gameObject) >= 0))
		{
			return false;
		}
		Rect r = WorldRect(image.rectTransform);
		if (r.width <= 2f || r.height <= 2f)
		{
			return false;
		}
		float aspect = r.width / r.height;
		if (aspect >= 0.6f)
		{
			return aspect <= 1.67f;
		}
		return false;
	}

	// Centres the badge in the free space of the card's icon line: between the icons on its left half and
	// those on its right half.
	private static void Layout(gameTab tab, MmoRankBadge badge, Transform host)
	{
		RectTransform line = (RectTransform)tab.uiObjects[9].transform;
		Rect anchor = LocalRect(host, line);
		Rect card = LocalRect(host, (RectTransform)tab.transform);
		float left = card.xMin + 4f;
		float right = card.xMax - 4f;
		foreach (Image image in tab.GetComponentsInChildren<Image>(includeInactive: false))
		{
			if (image.transform.IsChildOf(badge.transform) || !Icon(tab, image))
			{
				continue;
			}
			Rect r = LocalRect(host, image.rectTransform);
			if (Mathf.Abs(r.center.y - anchor.center.y) > anchor.height * 0.5f)
			{
				continue;
			}
			if (r.center.x < card.center.x)
			{
				left = Mathf.Max(left, r.xMax);
			}
			else
			{
				right = Mathf.Min(right, r.xMin);
			}
		}
		Rect textRect = LocalRect(host, (RectTransform)badge.SourceText.transform);
		float textWidth = badge.Number.preferredWidth;
		float iconWidth = 0f;
		float iconHeight = 0f;
		float gap = 0f;
		if ((bool)badge.Icon && (bool)badge.SourceIcon)
		{
			Rect iconRect = LocalRect(host, badge.SourceIcon.rectTransform);
			iconWidth = iconRect.width;
			iconHeight = iconRect.height;
			float sourceRight = VisibleRight(badge.SourceText, textRect);
			gap = Mathf.Clamp(iconRect.xMin - sourceRight, 1f, 6f);
		}
		float total = textWidth + gap + iconWidth;
		float x = (left + right) * 0.5f - total * 0.5f;
		float y = anchor.center.y;
		RectTransform root = (RectTransform)badge.transform;
		Center(root, 0f, 0f, 0f, 0f);
		root.SetAsLastSibling();
		Center(badge.Number.rectTransform, x + textWidth * 0.5f - 2f, y, textWidth + 4f, textRect.height);
		if ((bool)badge.Icon)
		{
			Center(badge.Icon.rectTransform, x + textWidth + gap + iconWidth * 0.5f, y, iconWidth, iconHeight);
		}
	}

	// Places a rect by its centre, in its parent's local space.
	private static void Center(RectTransform rt, float x, float y, float width, float height)
	{
		rt.anchorMin = new Vector2(0.5f, 0.5f);
		rt.anchorMax = new Vector2(0.5f, 0.5f);
		rt.pivot = new Vector2(0.5f, 0.5f);
		rt.sizeDelta = new Vector2(width, height);
		rt.localPosition = new Vector3(x, y, 0f);
		rt.localScale = Vector3.one;
	}

	private static Rect WorldRect(RectTransform rt)
	{
		rt.GetWorldCorners(corners);
		return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
	}

	// A rect in another transform's local space.
	private static Rect LocalRect(Transform space, RectTransform rt)
	{
		rt.GetWorldCorners(corners);
		Vector3 a = space.InverseTransformPoint(corners[0]);
		Vector3 b = space.InverseTransformPoint(corners[2]);
		return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
	}

	// Where the text's characters end, in the same space as its rect.
	private static float VisibleRight(Text t, Rect r)
	{
		float scale = r.width / Mathf.Max(1f, t.rectTransform.rect.width);
		float width = t.preferredWidth * scale;
		switch (t.alignment)
		{
		case TextAnchor.UpperLeft:
		case TextAnchor.MiddleLeft:
		case TextAnchor.LowerLeft:
			return r.xMin + width;
		case TextAnchor.UpperCenter:
		case TextAnchor.MiddleCenter:
		case TextAnchor.LowerCenter:
			return r.center.x + width * 0.5f;
		default:
			return r.xMax;
		}
	}

	// The calendar with its red parts turned blue (read back through a render texture: game sprites aren't readable).
	private static Sprite BlueCopy(Sprite s)
	{
		if (blueTried)
		{
			return blueCalendar;
		}
		blueTried = true;
		if (!s)
		{
			return null;
		}
		try
		{
			Texture source = s.texture;
			Rect packed = PackedRect(s);
			Vector2 offset = s.packed ? s.textureRectOffset : Vector2.zero;
			int width = Mathf.RoundToInt(s.rect.width);
			int height = Mathf.RoundToInt(s.rect.height);
			int pw = Mathf.Clamp(Mathf.RoundToInt(packed.width), 1, width);
			int ph = Mathf.Clamp(Mathf.RoundToInt(packed.height), 1, height);
			Color[] read = ReadRegion(source, packed, pw, ph);
			Plugin.Log.LogInfo("Blue calendar: copied " + s.name + " from " + source.name + " (" + source.width + "x" + source.height + ") at " + packed + ", read-back " + ProbeText());
			Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false)
			{
				name = "Holy_BlueCalendar",
				wrapMode = TextureWrapMode.Clamp,
				filterMode = FilterMode.Bilinear,
				hideFlags = HideFlags.DontUnloadUnusedAsset
			};
			tex.SetPixels(new Color[width * height]);
			tex.SetPixels(Mathf.Clamp(Mathf.RoundToInt(offset.x), 0, width - pw), Mathf.Clamp(Mathf.RoundToInt(offset.y), 0, height - ph), pw, ph, read);
			Color[] px = tex.GetPixels();
			for (int i = 0; i < px.Length; i++)
			{
				Color c = px[i];
				if (c.a <= 0.01f)
				{
					continue;
				}
				Color.RGBToHSV(c, out float hue, out float sat, out float val);
				if (sat >= 0.25f && (hue <= 0.1f || hue >= 0.9f))
				{
					Color blue = Color.HSVToRGB(0.585f, sat, val);
					blue.a = c.a;
					px[i] = blue;
				}
			}
			tex.SetPixels(px);
			tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);
			Vector2 pivot = new Vector2(s.pivot.x / Mathf.Max(1f, s.rect.width), s.pivot.y / Mathf.Max(1f, s.rect.height));
			blueCalendar = Sprite.Create(tex, new Rect(0f, 0f, width, height), pivot, s.pixelsPerUnit, 0u, SpriteMeshType.FullRect, s.border);
			blueCalendar.name = "Holy_BlueCalendar";
			blueCalendar.hideFlags = HideFlags.DontUnloadUnusedAsset;
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Blue calendar: " + ex.Message + " (the copy is tinted instead)");
			blueCalendar = null;
		}
		return blueCalendar;
	}

	// Where the sprite sits in its texture, in pixels (bottom-left origin, as Unity counts texture pixels).
	private static Rect PackedRect(Sprite s)
	{
		try
		{
			return s.textureRect;
		}
		catch (Exception)
		{
			// Tightly packed: the bounds of its mesh's texture coordinates.
			Vector2[] uv = s.uv;
			float x0 = uv.Min((Vector2 u) => u.x);
			float y0 = uv.Min((Vector2 u) => u.y);
			float x1 = uv.Max((Vector2 u) => u.x);
			float y1 = uv.Max((Vector2 u) => u.y);
			return Rect.MinMaxRect(x0 * s.texture.width, y0 * s.texture.height, x1 * s.texture.width, y1 * s.texture.height);
		}
	}

	// A region of a texture as pixels, bottom row first. Readable textures are read directly; others are drawn
	// into a render texture the region's size and read back, corrected by what Probe found about this machine.
	private static Color[] ReadRegion(Texture source, Rect region, int w, int h)
	{
		if (source is Texture2D t2 && t2.isReadable)
		{
			return t2.GetPixels(Mathf.RoundToInt(region.x), Mathf.RoundToInt(region.y), w, h);
		}
		int mode = Probe();
		Vector2 scale = new Vector2(region.width / source.width, region.height / source.height);
		Vector2 offset = new Vector2(region.x / source.width, region.y / source.height);
		if ((mode & 2) != 0)
		{
			offset.y = 1f - offset.y - scale.y;
		}
		Color[] px = Blit(source, w, h, scale, offset);
		if ((mode & 1) != 0)
		{
			px = FlipRows(px, w, h);
		}
		return px;
	}

	private static Color[] Blit(Texture source, int w, int h, Vector2 scale, Vector2 offset)
	{
		RenderTexture previous = RenderTexture.active;
		RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
		Texture2D read = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false);
		try
		{
			rt.filterMode = FilterMode.Point;
			Graphics.Blit(source, rt, scale, offset);
			RenderTexture.active = rt;
			read.ReadPixels(new Rect(0f, 0f, w, h), 0, 0);
			read.Apply();
			return read.GetPixels();
		}
		finally
		{
			RenderTexture.active = previous;
			RenderTexture.ReleaseTemporary(rt);
			UnityEngine.Object.Destroy(read);
		}
	}

	private static Color[] FlipRows(Color[] px, int w, int h)
	{
		Color[] flipped = new Color[px.Length];
		for (int y = 0; y < h; y++)
		{
			Array.Copy(px, y * w, flipped, (h - 1 - y) * w, w);
		}
		return flipped;
	}

	// How this machine's graphics API hands back a texture region: bit 1 = rows come back upside down, bit 2 =
	// texture coordinates count from the top. Found by copying the bottom half of a 1x4 test strip (red, green,
	// blue, white from the bottom) and seeing what comes back.
	private static int probe = -1;

	private static string probeSeen = "";

	private static int Probe()
	{
		if (probe >= 0)
		{
			return probe;
		}
		probe = 0;
		Texture2D strip = new Texture2D(1, 4, TextureFormat.RGBA32, mipChain: false)
		{
			filterMode = FilterMode.Point,
			wrapMode = TextureWrapMode.Clamp
		};
		try
		{
			strip.SetPixels(new Color[4]
			{
				Color.red,
				Color.green,
				Color.blue,
				Color.white
			});
			strip.Apply();
			Color[] got = Blit(strip, 1, 2, new Vector2(1f, 0.5f), Vector2.zero);
			int a = Nearest(got[0]);
			int b = Nearest(got[1]);
			probeSeen = "test strip gave " + a + "," + b;
			bool mirrored = a >= 2 && b >= 2;
			bool flipped = a > b;
			probe = (flipped ? 1 : 0) | (mirrored ? 2 : 0);
		}
		catch (Exception ex)
		{
			probeSeen = "test failed: " + ex.Message;
		}
		finally
		{
			UnityEngine.Object.Destroy(strip);
		}
		return probe;
	}

	private static string ProbeText()
	{
		return (((probe & 1) != 0) ? "upside down" : "upright") + (((probe & 2) != 0) ? ", from the top" : ", from the bottom") + " (" + probeSeen + ")";
	}

	private static int Nearest(Color c)
	{
		Color[] options = new Color[4]
		{
			Color.red,
			Color.green,
			Color.blue,
			Color.white
		};
		int best = 0;
		float bestD = float.MaxValue;
		for (int i = 0; i < options.Length; i++)
		{
			float d = Mathf.Abs(c.r - options[i].r) + Mathf.Abs(c.g - options[i].g) + Mathf.Abs(c.b - options[i].b);
			if (d < bestD)
			{
				bestD = d;
				best = i;
			}
		}
		return best;
	}

	// ---- Bar chart: subscribers instead of sales ----

	private static readonly AccessTools.FieldRef<gameTab, float> fillBar = AccessTools.FieldRefAccess<gameTab, float>("fillBalken");

	private static readonly AccessTools.FieldRef<gameTab, float> fillBarOnline = AccessTools.FieldRefAccess<gameTab, float>("fillBalkenOnline");

	// The bars' own look, to put back if a card stops showing subscribers.
	private static readonly Dictionary<int, KeyValuePair<Sprite, Color>> barLooks = new Dictionary<int, KeyValuePair<Sprite, Color>>();

	private static Sprite white;

	// A plain white sprite: the blue goes on as the image colour, whatever the game's bar picture looks like.
	private static Sprite White()
	{
		if (!white)
		{
			Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, mipChain: false)
			{
				name = "Holy_White",
				hideFlags = HideFlags.DontUnloadUnusedAsset
			};
			Color[] px = new Color[16];
			for (int i = 0; i < px.Length; i++)
			{
				px[i] = Color.white;
			}
			tex.SetPixels(px);
			tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);
			white = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f);
			white.name = "Holy_White";
			white.hideFlags = HideFlags.DontUnloadUnusedAsset;
		}
		return white;
	}

	public static readonly Color BarBlue = new Color(0.2f, 0.52f, 0.84f);

	// The card draws one bar per week from the game's weekly sales (bar 0 = this week, growing in as it animates,
	// the online part drawn over it). For your MMOs it draws subscribers at the end of each week instead, from
	// MMO Management's weekly record, in blue.
	[HarmonyPostfix]
	[HarmonyPatch(typeof(gameTab), "UpdateData")]
	private static void SubscriberBars(gameTab __instance)
	{
		try
		{
			GameObject[] bars = __instance.uiBalken;
			GameObject[] online = __instance.uiBalkenOnline;
			if (bars == null || bars.Length == 0)
			{
				return;
			}
			gameScript g = __instance.gS_;
			MmoGame m = ((MmoOps.On && (bool)g && !g.typ_mmoaddon && MmoOps.IsMyMmo(g) && g.isOnMarket) ? MmoOps.Find(g.myID) : null);
			TintBars(bars, m != null);
			if (m == null)
			{
				return;
			}
			List<long> history = m.History ?? new List<long>();
			// During the week's sales the newest entry is still last week's; after MMO Management's weekly record
			// it's this week's.
			int newest = ((m.HistoryWeek >= 0 && m.HistoryWeek < MmoOps.Now) ? history.Count : (history.Count - 1));
			long[] subs = new long[bars.Length];
			subs[0] = g.abonnements;
			for (int j = 1; j < bars.Length; j++)
			{
				int k = newest - j;
				subs[j] = ((k >= 0 && k < history.Count) ? history[k] : 0);
			}
			float max = Mathf.Max(1f, subs.Max());
			for (int j = 1; j < bars.Length; j++)
			{
				SetFill(bars[j], subs[j] / max);
				if (online != null && j < online.Length)
				{
					SetFill(online[j], 0f);
				}
			}
			if (online != null && online.Length > 0)
			{
				SetFill(online[0], 0f);
			}
			fillBar(__instance) = subs[0] / max;
			fillBarOnline(__instance) = 0f;
			Transform area = bars[0] ? bars[0].transform.parent : null;
			if ((bool)area && (bool)area.GetComponent<Graphic>())
			{
				UIKit.SetTooltip(area.gameObject, "<color=#2a7ad0>Subscribers at the end of each week</color> (highest: " + MmoUiText.Num((long)max) + "). The weeks before MMO Management started recording stay empty.");
			}
		}
		catch (Exception ex)
		{
			if (!barsFailed)
			{
				barsFailed = true;
				Plugin.Log.LogError("MMO card subscriber bars: " + ex);
			}
		}
	}

	private static bool barsFailed;

	private static void SetFill(GameObject bar, float fill)
	{
		Image image = (bar ? bar.GetComponent<Image>() : null);
		if ((bool)image)
		{
			image.fillAmount = fill;
		}
	}

	private static void TintBars(GameObject[] bars, bool blue)
	{
		foreach (GameObject bar in bars)
		{
			Image image = (bar ? bar.GetComponent<Image>() : null);
			if (!image)
			{
				continue;
			}
			int id = image.GetInstanceID();
			if (blue)
			{
				if (!barLooks.ContainsKey(id))
				{
					barLooks[id] = new KeyValuePair<Sprite, Color>(image.sprite, image.color);
				}
				Sprite plain = White();
				if (image.sprite != plain)
				{
					image.sprite = plain;
				}
				if (image.color != BarBlue)
				{
					image.color = BarBlue;
				}
			}
			else if (barLooks.TryGetValue(id, out var original))
			{
				image.sprite = original.Key;
				image.color = original.Value;
				barLooks.Remove(id);
			}
		}
	}

	// MMO addons stay off the list (MMO Management shows them with their MMO).
	[HarmonyPrefix]
	[HarmonyPatch(typeof(GamesGroupContent), "SetTab")]
	private static bool HideAddons(GameObject go, gameTab script_)
	{
		if (!MmoOps.On || !script_ || !script_.gS_ || !script_.gS_.typ_mmoaddon || script_.gS_.inDevelopment)
		{
			return true;
		}
		if (go.activeSelf)
		{
			go.SetActive(value: false);
		}
		return false;
	}
}
