using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HolyAddon.Core;
using HolyAddon.Core.Reviews;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI;

// Dresses up the game's review card (Menu_Review) with press reviews: the lead outlet's logo replaces the plain
// "Review" label, its review replaces the stock text, and a strip under the card lists every outlet's score.
// Click an outlet to read its review. The game's scores, hit/flop result and the big total are left untouched.
// The window grows downwards to make room for the strip; the OK button moves below it.
// Menu_Review.uiObjects: 3 review text, 6 genre line, 13 "Review" label, 14 the standard game card.
[HarmonyPatch]
internal static class ReviewCard
{
	private const string StripName = "Editor_Press";

	private const string LogoName = "Editor_OutletLogo";

	// Strip size and gaps, in the window's own units.
	private const float StripHeight = 70f;

	private const float Gap = 8f;

	private const float SideMargin = 26f;

	private static Menu_Review menu;

	private static List<Review> reviews = new List<Review>();

	private static string genreLine;

	private static readonly List<Button> tiles = new List<Button>();

	private static bool loggedLayout;

	private static bool English
	{
		get
		{
			if ((bool)G.MS && (bool)G.MS.settings_)
			{
				return G.MS.settings_.language == 0;
			}
			return false;
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_Review), "Init")]
	private static void Init(Menu_Review __instance, gameScript s_)
	{
		try
		{
			menu = __instance;
			GameObject[] ui = __instance.uiObjects;
			bool standardCard = ui != null && ui.Length > 14 && (bool)ui[14] && ui[14].activeSelf;
			reviews = ((Plugin.PressReviews.Value && English && standardCard && (bool)s_) ? ReviewWriter.For(s_, Mathf.Clamp(Plugin.OutletsPerGame.Value, 2, 5)) : new List<Review>());
			if (reviews.Count == 0)
			{
				Hide(__instance);
				return;
			}
			genreLine = ui[6].GetComponent<Text>().text;
			Text body = ui[3].GetComponent<Text>();
			if (!body.resizeTextForBestFit)
			{
				// Our reviews run a little longer than the stock ones; let the text shrink to fit the card.
				body.resizeTextMaxSize = body.fontSize;
				body.resizeTextMinSize = Mathf.Max(8, body.fontSize - 5);
				body.resizeTextForBestFit = true;
			}
			BuildStrip(__instance, s_);
			Show(0);
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Press reviews failed: " + ex);
			Hide(__instance);
		}
	}

	private static void Hide(Menu_Review m)
	{
		if (!m || m.uiObjects == null || m.uiObjects.Length <= 14)
		{
			return;
		}
		Transform logo = m.uiObjects[13] ? m.uiObjects[13].transform.parent.Find(LogoName) : null;
		if ((bool)logo)
		{
			logo.gameObject.SetActive(value: false);
		}
		if (!m.uiObjects[14])
		{
			return;
		}
		RectTransform window = (RectTransform)m.uiObjects[14].transform.parent;
		Transform strip = window.Find(StripName);
		if ((bool)strip)
		{
			strip.gameObject.SetActive(value: false);
		}
		WindowGrowth growth = window.GetComponent<WindowGrowth>();
		if ((bool)growth)
		{
			growth.Restore();
		}
	}

	private static void Show(int index)
	{
		if (!menu || index < 0 || index >= reviews.Count)
		{
			return;
		}
		Review r = reviews[index];
		GameObject[] ui = menu.uiObjects;
		ui[3].GetComponent<Text>().text = "<b><i>“" + r.Headline + "”</i></b>\n" + r.Body;
		ui[6].GetComponent<Text>().text = genreLine + "   ·   Reviewed by " + r.Reviewer;
		Text label = ui[13].GetComponent<Text>();
		Image logo = LogoImage(label);
		Sprite sprite = r.Outlet.Logo;
		if ((bool)sprite)
		{
			logo.sprite = sprite;
			logo.gameObject.SetActive(value: true);
			label.text = "";
		}
		else
		{
			logo.gameObject.SetActive(value: false);
			label.text = r.Outlet.Name;
		}
		for (int i = 0; i < tiles.Count; i++)
		{
			if ((bool)tiles[i])
			{
				tiles[i].GetComponent<Image>().color = (i == index) ? UIKit.SelectedRow : Color.white;
			}
		}
	}

	// The outlet logo sits exactly where the "Review" label was.
	private static Image LogoImage(Text label)
	{
		Transform existing = label.transform.parent.Find(LogoName);
		if ((bool)existing)
		{
			return existing.GetComponent<Image>();
		}
		GameObject go = new GameObject(LogoName, typeof(RectTransform), typeof(Image));
		RectTransform rt = (RectTransform)go.transform;
		RectTransform src = label.rectTransform;
		rt.SetParent(src.parent, worldPositionStays: false);
		rt.SetSiblingIndex(src.GetSiblingIndex() + 1);
		rt.anchorMin = src.anchorMin;
		rt.anchorMax = src.anchorMax;
		rt.pivot = src.pivot;
		rt.anchoredPosition = src.anchoredPosition;
		rt.sizeDelta = src.sizeDelta;
		rt.localScale = Vector3.one;
		Image image = go.GetComponent<Image>();
		image.preserveAspect = true;
		image.raycastTarget = false;
		return image;
	}

	// The strip of outlet scores runs the full width of the window, right under the card.
	private static void BuildStrip(Menu_Review m, gameScript g)
	{
		tiles.Clear();
		if (!UIKit.Init())
		{
			return;
		}
		RectTransform card = (RectTransform)m.uiObjects[14].transform;
		RectTransform window = (RectTransform)card.parent;
		Transform old = window.Find(StripName);
		if ((bool)old)
		{
			old.SetParent(null);
			UnityEngine.Object.Destroy(old.gameObject);
		}
		WindowGrowth growth = window.GetComponent<WindowGrowth>() ?? window.gameObject.AddComponent<WindowGrowth>();
		growth.Restore();
		float unit = Mathf.Max(0.0001f, window.lossyScale.y);
		Rect cardRect = WorldRect(card);
		Rect windowRect = WorldRect(window);
		float top = cardRect.yMin - Gap * unit;
		float bottom = top - StripHeight * unit;
		// Grow the window so the OK button (and anything else under the card) fits below the strip.
		float shift = 0f;
		foreach (RectTransform below in growth.BelowCard(cardRect))
		{
			shift = Mathf.Max(shift, WorldRect(below).yMax - (bottom - Gap * unit));
		}
		shift = Mathf.Max(shift, windowRect.yMin + 2f * Gap * unit - bottom);
		bool fullScreen = windowRect.width > (float)Screen.width * 0.9f;
		if (!fullScreen)
		{
			growth.Grow(shift / unit, cardRect);
		}
		if (!loggedLayout)
		{
			loggedLayout = true;
			string parts = string.Join(", ", from Transform t in window
				where t is RectTransform
				let rt = (RectTransform)t
				select t.name + " y-anchors " + rt.anchorMin.y.ToString("0.##") + ".." + rt.anchorMax.y.ToString("0.##"));
			Plugin.Log.LogInfo("Review card layout: window " + window.name + " " + Describe(windowRect) + ", card " + Describe(cardRect) + ", grown by " + Mathf.RoundToInt(shift / unit) + (fullScreen ? " (window is full screen, not grown)" : "") + "\n  parts: " + parts);
		}
		float left = (fullScreen ? cardRect.xMin : (windowRect.xMin + SideMargin * unit));
		float right = (fullScreen ? cardRect.xMax : (windowRect.xMax - SideMargin * unit));
		GameObject strip = new GameObject(StripName, typeof(RectTransform));
		RectTransform s = (RectTransform)strip.transform;
		s.SetParent(window, worldPositionStays: false);
		SetWorldRect(s, Rect.MinMaxRect(left, bottom, right, top));
		HorizontalLayoutGroup layout = strip.AddComponent<HorizontalLayoutGroup>();
		layout.spacing = 6f;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = true;
		bool modern = g.date_year >= 2001;
		GameObject average = UIKit.Make("Paragraph", s);
		Text averageText = average.GetComponent<Text>();
		averageText.text = modern ? ("Metacrittic\n<b>" + g.reviewTotal + "</b>") : ("Press average\n<b>" + g.reviewTotal + "%</b>");
		averageText.alignment = TextAnchor.MiddleCenter;
		averageText.resizeTextForBestFit = true;
		averageText.resizeTextMinSize = 9;
		averageText.resizeTextMaxSize = 16;
		LayoutElement averageLayout = average.AddComponent<LayoutElement>();
		averageLayout.flexibleWidth = 0.6f;
		UIKit.SetTooltip(average, "The game's real review score. The outlets' scores average out to it.");
		for (int i = 0; i < reviews.Count; i++)
		{
			tiles.Add(Tile(s, reviews[i], i));
		}
	}

	private static Button Tile(RectTransform strip, Review r, int index)
	{
		GameObject go = UIKit.Make("SmallButton", strip);
		go.name = "Outlet_" + r.Outlet.Id;
		go.AddComponent<LayoutElement>().flexibleWidth = 1f;
		Text score = go.transform.Find("Text").GetComponent<Text>();
		RectTransform scoreRt = score.rectTransform;
		scoreRt.anchorMin = new Vector2(0f, 0f);
		scoreRt.anchorMax = new Vector2(1f, 0.34f);
		scoreRt.offsetMin = new Vector2(2f, 2f);
		scoreRt.offsetMax = new Vector2(-2f, 0f);
		score.text = r.ScoreText;
		score.alignment = TextAnchor.MiddleCenter;
		score.resizeTextForBestFit = true;
		score.resizeTextMinSize = 9;
		score.resizeTextMaxSize = 15;
		Sprite sprite = r.Outlet.Logo;
		GameObject logo = new GameObject("Logo", typeof(RectTransform), typeof(Image));
		RectTransform logoRt = (RectTransform)logo.transform;
		logoRt.SetParent(go.transform, worldPositionStays: false);
		logoRt.anchorMin = new Vector2(0.04f, 0.36f);
		logoRt.anchorMax = new Vector2(0.96f, 0.95f);
		logoRt.offsetMin = Vector2.zero;
		logoRt.offsetMax = Vector2.zero;
		Image image = logo.GetComponent<Image>();
		image.preserveAspect = true;
		image.raycastTarget = false;
		if ((bool)sprite)
		{
			image.sprite = sprite;
		}
		else
		{
			image.enabled = false;
			score.text = r.Outlet.Name + "\n" + r.ScoreText;
		}
		string parody = string.IsNullOrEmpty(r.Outlet.ParodyOf) ? "" : ("\n<color=grey>(not to be confused with " + r.Outlet.ParodyOf + ")</color>");
		UIKit.SetTooltip(go, "<b>" + r.Outlet.Name + "</b>: " + r.ScoreText + "\nReviewed by " + r.Reviewer + parody);
		Button button = go.GetComponent<Button>();
		UIKit.OnClick(button, () =>
		{
			UIKit.Sound();
			Show(index);
		});
		return button;
	}

	private static string Describe(Rect r)
	{
		return "x " + Mathf.RoundToInt(r.xMin) + ".." + Mathf.RoundToInt(r.xMax) + " y " + Mathf.RoundToInt(r.yMin) + ".." + Mathf.RoundToInt(r.yMax);
	}

	internal static Rect WorldRect(RectTransform rt)
	{
		Vector3[] c = new Vector3[4];
		rt.GetWorldCorners(c);
		return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
	}

	private static void SetWorldRect(RectTransform rt, Rect world)
	{
		RectTransform parent = (RectTransform)rt.parent;
		rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
		rt.pivot = new Vector2(0.5f, 0.5f);
		rt.localScale = Vector3.one;
		Vector3 local = parent.InverseTransformPoint(world.center);
		rt.localPosition = new Vector3(local.x, local.y, 0f);
		Vector3 scale = parent.lossyScale;
		rt.sizeDelta = new Vector2(world.width / Mathf.Max(0.0001f, scale.x), world.height / Mathf.Max(0.0001f, scale.y));
	}
}

// Makes the review window taller at the bottom while keeping its top (title bar) and the card where they are,
// and moves whatever sat under the card (the OK button) down by the same amount. Restore() puts it all back.
internal class WindowGrowth : MonoBehaviour
{
	private bool recorded;

	private Vector2 size;

	private Vector2 position;

	private readonly List<RectTransform> kids = new List<RectTransform>();

	private readonly List<Vector2> kidPositions = new List<Vector2>();

	private readonly List<Vector2> kidSizes = new List<Vector2>();

	private float extra;

	private void Record()
	{
		if (recorded)
		{
			return;
		}
		recorded = true;
		RectTransform w = (RectTransform)transform;
		size = w.sizeDelta;
		position = w.anchoredPosition;
		foreach (Transform child in w)
		{
			if (child is RectTransform rt && !child.name.StartsWith("Editor_"))
			{
				kids.Add(rt);
				kidPositions.Add(rt.anchoredPosition);
				kidSizes.Add(rt.sizeDelta);
			}
		}
	}

	// The window's own pieces that sit below the card (the OK button), which must move down with the bottom edge.
	public IEnumerable<RectTransform> BelowCard(Rect card)
	{
		Record();
		Rect window = ReviewCard.WorldRect((RectTransform)transform);
		return kids.Where((RectTransform k) => (bool)k && k.gameObject.activeSelf && !FillsWindow(ReviewCard.WorldRect(k), window) && ReviewCard.WorldRect(k).center.y < card.yMin);
	}

	public void Grow(float amount, Rect card)
	{
		Record();
		Restore();
		if (amount <= 0.5f)
		{
			return;
		}
		RectTransform w = (RectTransform)transform;
		Rect window = ReviewCard.WorldRect(w);
		List<Rect> before = kids.Select((RectTransform k) => ((bool)k) ? ReviewCard.WorldRect(k) : default(Rect)).ToList();
		List<bool> below = before.Select((Rect r) => r.center.y < card.yMin).ToList();
		w.sizeDelta = size + new Vector2(0f, amount);
		w.anchoredPosition = position - new Vector2(0f, (1f - w.pivot.y) * amount);
		float unit = Mathf.Max(0.0001f, w.lossyScale.y);
		float down = amount * unit;
		// Only the full-window background may stretch. Everything else keeps its exact size on screen, including
		// pieces anchored to a share of the window's height (the blue title band), and moves down only if it was
		// under the card.
		for (int i = 0; i < kids.Count; i++)
		{
			RectTransform k = kids[i];
			if (!k || FillsWindow(before[i], window))
			{
				continue;
			}
			Rect now = ReviewCard.WorldRect(k);
			float shift = (below[i] ? down : 0f);
			k.offsetMin += new Vector2(0f, (before[i].yMin - shift - now.yMin) / unit);
			k.offsetMax += new Vector2(0f, (before[i].yMax - shift - now.yMax) / unit);
		}
		extra = amount;
	}

	public void Restore()
	{
		Record();
		if (extra == 0f)
		{
			return;
		}
		RectTransform w = (RectTransform)transform;
		w.sizeDelta = size;
		w.anchoredPosition = position;
		for (int i = 0; i < kids.Count; i++)
		{
			if ((bool)kids[i])
			{
				kids[i].anchoredPosition = kidPositions[i];
				kids[i].sizeDelta = kidSizes[i];
			}
		}
		extra = 0f;
	}

	// Backgrounds that actually cover the window's full height grow with it; nothing else should. This goes by size
	// on screen, not anchors: the blue title band is anchored over the whole window but trimmed to the top by offsets.
	private static bool FillsWindow(Rect piece, Rect window)
	{
		return piece.height >= window.height * 0.9f;
	}
}
