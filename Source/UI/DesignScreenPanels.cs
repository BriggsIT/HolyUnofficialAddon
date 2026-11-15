using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HolyAddon.Core;
using HolyAddon.Core.Industry;
using HolyAddon.Core.Landmarks;
using HolyAddon.Core.Topics;
using HolyAddon.Core.Updates;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI;

// Two panels in the space the taller design screen (ExtraTopicsRow) leaves at the bottom of two of its pages:
// - "Market Check" on the design focus page (the one with the Design Priority sliders): the trend, your genre's
//   popularity and whether your genre and topics are on trend, and which studios are making something similar now.
// - "Project Overview" on the financing page (the last): the cost and the work with the extra topics, roughly how
//   long it'll take (from your last games), the review change from the topic mix, a landmark it could become, and
//   the timed exclusive.
// Each copies the page's own lowest section header (and its box, if the page has boxes), so it looks like the page.
internal static class DesignScreenPanels
{
	private class Panel
	{
		public GameObject Root;

		public Text Body;

		public RectTransform Page;
	}

	private static Panel market;

	private static Panel overview;

	private static bool failedMarket;

	private static bool failedOverview;

	private static float next;

	private static readonly Func<Menu_DevGame, int> DevCosts = AccessTools.MethodDelegate<Func<Menu_DevGame, int>>(AccessTools.Method(typeof(Menu_DevGame), "CalcDevCosts"));

	public static void Tick()
	{
		Menu_DevGame menu = ExtraTopicsMenu.Menu;
		if (!menu || !menu.gameObject.activeInHierarchy || !ExtraTopics.On)
		{
			return;
		}
		if (!ExtraTopicsRow.In)
		{
			Destroy(ref market);
			Destroy(ref overview);
			return;
		}
		if (Time.unscaledTime < next)
		{
			return;
		}
		next = Time.unscaledTime + 0.5f;
		try
		{
			if ((market == null || !market.Root) && !failedMarket)
			{
				market = Make(menu, menu.uiObjects[97], "Market Check", "Addon_MarketCheck");
				failedMarket = market == null;
			}
			if ((overview == null || !overview.Root) && !failedOverview)
			{
				overview = Make(menu, menu.uiObjects[159], "Project Overview", "Addon_ProjectOverview");
				failedOverview = overview == null;
			}
			if (market != null && (bool)market.Root && market.Page.gameObject.activeInHierarchy)
			{
				market.Body.text = MarketText(menu);
			}
			if (overview != null && (bool)overview.Root && overview.Page.gameObject.activeInHierarchy)
			{
				overview.Body.text = OverviewText(menu);
			}
		}
		catch (Exception ex)
		{
			failedMarket = (failedOverview = true);
			Plugin.Log.LogError("Design screen panels failed: " + ex);
		}
	}

	private static void Destroy(ref Panel p)
	{
		if (p != null && (bool)p.Root)
		{
			UnityEngine.Object.Destroy(p.Root);
		}
		p = null;
	}

	// ---- Market Check ----

	private static string MarketText(Menu_DevGame menu)
	{
		mainScript ms = G.MS;
		genres gen = IndustryCore.Genres;
		if (!ms || !gen)
		{
			return "";
		}
		List<string> lines = new List<string>();
		lines.Add("<b>Trend:</b> " + IndustryCore.GenreGames(ms.trendGenre) + " and " + ExtraTopics.TopicName(ms.trendTheme) + ". <b>Out of fashion:</b> " + IndustryCore.GenreGames(ms.trendAntiGenre) + " and " + ExtraTopics.TopicName(ms.trendAntiTheme) + ".");
		int genre = menu.g_GameMainGenre;
		List<int> topics = ExtraTopicsMenu.Others(menu).Where((int t) => t >= 0).Distinct().ToList();
		if (genre < 0)
		{
			lines.Add("Pick a genre to see how it stands.");
			return string.Join("\n", lines);
		}
		float pop = ((gen.genres_BELIEBTHEIT != null && genre < gen.genres_BELIEBTHEIT.Length) ? gen.genres_BELIEBTHEIT[genre] : 0f);
		string mine = "<b>Your game:</b> " + IndustryCore.GenreName(genre) + ", " + Mathf.RoundToInt(pop) + "% popular";
		List<string> notes = new List<string>();
		if (genre == ms.trendGenre || menu.g_GameSubGenre == ms.trendGenre)
		{
			notes.Add("<color=#2f8a1c>the genre is on trend</color>");
		}
		if (genre == ms.trendAntiGenre || menu.g_GameSubGenre == ms.trendAntiGenre)
		{
			notes.Add("<color=#c4161c>the genre is out of fashion</color>");
		}
		if (topics.Contains(ms.trendTheme))
		{
			notes.Add("<color=#2f8a1c>" + ExtraTopics.TopicName(ms.trendTheme) + " is on trend</color>");
		}
		if (topics.Contains(ms.trendAntiTheme))
		{
			notes.Add("<color=#c4161c>" + ExtraTopics.TopicName(ms.trendAntiTheme) + " is out of fashion</color>");
		}
		lines.Add(mine + ((notes.Count > 0) ? ("; " + string.Join(", ", notes)) : "") + ".");
		// Studios making something like it now: the same genre, or a topic in common.
		List<string> rivals = (from g in IndustryCore.AllGames()
			where g.inDevelopment && !g.isOnMarket && g.DeveloperIsNPC() && (g.maingenre == genre || (topics.Count > 0 && (topics.Contains(g.gameMainTheme) || topics.Contains(g.gameSubTheme))))
			orderby (g.maingenre == genre && (topics.Contains(g.gameMainTheme) || topics.Contains(g.gameSubTheme))) descending, g.hype descending
			select g).Take(3).Select(Rival).ToList();
		lines.Add((rivals.Count == 0) ? ("<b>Also in development:</b> no other studio is making " + IndustryCore.GenreGames(genre).ToLowerInvariant() + " or games with your topics right now.") : ("<b>Also in development:</b> " + string.Join("; ", rivals) + "."));
		return string.Join("\n", lines);
	}

	private static string Rival(gameScript g)
	{
		publisherScript p = IndustryCore.Studio(g.developerID);
		return ((bool)p ? (p.GetName() + "'s ") : "") + g.GetNameSimple() + " (" + IndustryCore.GenreName(g.maingenre) + ", " + ExtraTopics.TopicName(g.gameMainTheme) + ")";
	}

	// ---- Project Overview ----

	private static string OverviewText(Menu_DevGame menu)
	{
		mainScript ms = G.MS;
		if (!ms)
		{
			return "";
		}
		List<string> lines = new List<string>();
		int cost = DevCosts(menu);
		int work = ExtraTopicsMenu.WorkWithTopics(menu.GetGesamtDevPoints());
		int extra = ExtraTopics.Count(ExtraTopicsMenu.Pending);
		string time = Duration(work);
		lines.Add("<b>Cost:</b> " + ms.GetMoney(cost, showDollar: true) + ((extra > 0) ? (" (extra topics +" + Mathf.RoundToInt(ExtraTopics.WorkPerTopic * 100f * (float)extra) + "%)") : "") + "   <b>Work:</b> " + work + " points" + ((time != null) ? (", " + time) : ""));
		List<string> second = new List<string>();
		if (extra > 0 && menu.g_GameMainGenre >= 0)
		{
			int change = ExtraTopics.ReviewChange(menu.g_GameMainGenre, menu.g_GameMainTheme, menu.g_GameSubTheme, ExtraTopicsMenu.Pending, out var _);
			second.Add("<b>Topic mix:</b> review " + ((change > 0) ? ("<color=#2f8a1c>+" + change + "%</color>") : ((change < 0) ? ("<color=#c4161c>" + change + "%</color>") : "unchanged")));
		}
		LandmarkDef d = Landmarks.ForDesign(menu.g_GameMainGenre, ExtraTopicsMenu.Others(menu));
		if (d != null && !menu.typ_contractGame && !menu.typ_nachfolger && !menu.typ_remaster)
		{
			second.Add(Plugin.LandmarksShowUpcoming.Value ? ("<b>Landmark:</b> it could become " + d.Name + " (needs a " + d.Bar + "% review)") : "<b>Landmark:</b> this mix could make history, if the game is good enough");
		}
		int langs = Core.Languages.ExtraLanguages.Picked(LanguagesRow.Pending);
		if (langs > 0 && !menu.typ_contractGame)
		{
			second.Add("<b>New languages:</b> " + langs + ", sales +" + Mathf.RoundToInt(Core.Languages.ExtraLanguages.Share(LanguagesRow.Pending) * 100f) + "%");
		}
		if (second.Count > 0)
		{
			lines.Add(string.Join("   ", second));
		}
		lines.Add(Exclusive(menu));
		return string.Join("\n", lines);
	}

	private static string Exclusive(Menu_DevGame menu)
	{
		if (!TimedExclusives.On || !TimedExclusiveMenu.Wanted(menu))
		{
			return "<b>Timed exclusive:</b> none (it needs a multiplatform game on two or more platforms).";
		}
		if (TimedExclusiveMenu.PickedFirst < 0)
		{
			return "<b>Timed exclusive:</b> none; pick one on the platform page.";
		}
		platformScript p = GameUpdates.Platform(TimedExclusiveMenu.PickedFirst);
		float rate = TimedExclusives.DealRate(p, TimedExclusiveMenu.PickedMonths);
		string pays = ((rate > 0f && (bool)p) ? (p.GetManufacturer() + " pays " + Mathf.RoundToInt(rate * 100f) + "% of the development costs") : "your own platform");
		return "<b>Timed exclusive:</b> " + TimedExclusiveMenu.Name(TimedExclusiveMenu.PickedFirst) + " first for " + TimedExclusiveMenu.PickedMonths + " months; " + pays + ".";
	}

	// Roughly how long, from how fast your last games were made (points per week from start to release).
	private static string Duration(int points)
	{
		mainScript ms = G.MS;
		if (!ms || points <= 0)
		{
			return null;
		}
		List<float> rates = (from g in IndustryCore.AllGames()
			where g.developerID == ms.myID && !g.inDevelopment && !g.typ_contractGame && g.portID == -1 && g.devPointsStart_Gesamt > 0f && (g.typ_standard || g.typ_nachfolger || g.typ_spinoff)
			let weeks = ((g.date_year - g.date_start_year) * 12 + (g.date_month - g.date_start_month)) * 4
			where weeks >= 2
			orderby g.myID descending
			select g.devPointsStart_Gesamt / (float)weeks).Take(5).ToList();
		if (rates.Count == 0)
		{
			return null;
		}
		rates.Sort();
		float rate = rates[rates.Count / 2];
		if (rate <= 0f)
		{
			return null;
		}
		int months = Mathf.Max(1, Mathf.RoundToInt((float)points / rate / 4f));
		return "about " + months + ((months == 1) ? " month" : " months") + " at your recent pace";
	}

	// ---- Building a panel in a page's free space ----

	private static Panel Make(Menu_DevGame menu, GameObject piece, string title, string name)
	{
		if (!piece || !UIKit.Init())
		{
			return null;
		}
		Transform window = menu.uiObjects[16].transform.parent.parent;
		Transform t = piece.transform;
		while ((bool)t && t.parent != window)
		{
			t = t.parent;
		}
		RectTransform page = t as RectTransform;
		if (!page)
		{
			Plugin.Log.LogWarning(title + ": its page wasn't found");
			return null;
		}
		Transform old = page.Find(name);
		if ((bool)old)
		{
			UnityEngine.Object.Destroy(old.gameObject);
		}
		// What's on the page: its lowest thing and how wide it is (backgrounds aside).
		float bottom = float.MaxValue;
		float left = float.MaxValue;
		float right = float.MinValue;
		Image header = null;
		Rect headerRect = Rect.zero;
		List<KeyValuePair<Image, Rect>> boxes = new List<KeyValuePair<Image, Rect>>();
		foreach (Graphic g in page.GetComponentsInChildren<Graphic>(includeInactive: true))
		{
			if (!g.enabled || !OnWithin(g.transform, page) || g.transform.name.StartsWith("Addon_") || HasAddonParent(g.transform, page))
			{
				continue;
			}
			Rect r = Local(g.rectTransform, page);
			if (r.width < 2f || r.height < 2f || r.height > page.rect.height * 0.5f)
			{
				continue;
			}
			bottom = Mathf.Min(bottom, r.yMin);
			left = Mathf.Min(left, r.xMin);
			right = Mathf.Max(right, r.xMax);
			// A card's box: a wide panel picture behind a section (In-App Purchases, Financing).
			if (g is Image box && r.width >= 300f && r.height >= 50f && box.GetComponentInParent<Selectable>() == null)
			{
				boxes.Add(new KeyValuePair<Image, Rect>(box, r));
			}
			// A section header: a bar with a light label on it ("Design Priority", "Financing"), not a button or a
			// value box (those have dark text); the lowest one is copied.
			if (g is Image img && r.height >= 18f && r.height <= 36f && r.width >= 120f && img.GetComponentInParent<Selectable>() == null && img.transform.childCount <= 2 && (!header || r.yMin < headerRect.yMin))
			{
				Text[] labels = img.transform.GetComponentsInChildren<Text>(includeInactive: true);
				if (labels.Length == 1 && Mathf.Min(labels[0].color.r, Mathf.Min(labels[0].color.g, labels[0].color.b)) > 0.8f)
				{
					header = img;
					headerRect = r;
				}
			}
		}
		// The lowest header's card, if the page draws its sections as cards: the box whose top meets the header.
		KeyValuePair<Image, Rect> card = default(KeyValuePair<Image, Rect>);
		// (Only where headers are tabs on cards, like Financing; full-width header bars, like Design Priority, have
		// their sections on the page itself.)
		if ((bool)header && headerRect.width < (right - left) * 0.8f)
		{
			card = (from b in boxes
				where b.Value.yMax <= headerRect.yMax && b.Value.yMax >= headerRect.yMin - 14f && b.Value.center.y < headerRect.yMin && b.Value.xMin < headerRect.xMax && b.Value.xMax > headerRect.xMin
				orderby Mathf.Abs(b.Value.yMax - headerRect.yMin)
				select b).FirstOrDefault();
		}
		Transform bar = window.Find("Line") ?? window.Find("PanelSeiten");
		float floor = ((bar is RectTransform brt) ? (Local(brt, page).yMax + 12f) : (page.rect.yMin + 12f));
		float top = bottom - (((bool)card.Key) ? 16f : 12f);
		float h = top - floor;
		if (h < 64f || bottom == float.MaxValue)
		{
			Plugin.Log.LogInfo(title + ": no room on " + page.name + " (" + h.ToString("0") + ")");
			return null;
		}
		h = Mathf.Min(h, 150f);
		float w = right - left;
		GameObject root = new GameObject(name, typeof(RectTransform));
		root.transform.SetParent(page, worldPositionStays: false);
		RectTransform rt = (RectTransform)root.transform;
		rt.anchorMin = (rt.anchorMax = new Vector2(0.5f, 0.5f));
		rt.pivot = new Vector2(0.5f, 0.5f);
		rt.sizeDelta = new Vector2(w, h);
		rt.anchoredPosition = new Vector2((left + right) / 2f, top - h / 2f) - page.rect.center;
		float headerH = (header ? headerRect.height : 24f);
		float headerX = (header ? Mathf.Max(0f, headerRect.xMin - left) : 0f);
		// The card's box behind it all, placed against the header as the page's own card is.
		float textX = 10f;
		float textY = headerH + 6f;
		float textW = w - 20f;
		float textH = h - headerH - 8f;
		if ((bool)card.Key)
		{
			Image src = card.Key;
			Image bg = new GameObject("Box", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
			bg.transform.SetParent(root.transform, worldPositionStays: false);
			bg.sprite = src.sprite;
			bg.type = src.type;
			bg.color = src.color;
			bg.material = src.material;
			bg.fillCenter = src.fillCenter;
			bg.pixelsPerUnitMultiplier = src.pixelsPerUnitMultiplier;
			bg.raycastTarget = false;
			float boxX = Mathf.Max(0f, card.Value.xMin - left);
			float boxY = headerH + (headerRect.yMin - card.Value.yMax);
			float boxW = Mathf.Min(card.Value.width, w - boxX);
			float boxH = h - boxY - 2f;
			UIKit.Place(bg.gameObject, boxX, boxY, boxW, boxH);
			textX = boxX + 12f;
			textY = Mathf.Max(boxY, headerH) + 8f;
			textW = boxW - 24f;
			textH = boxY + boxH - textY - 4f;
		}
		if ((bool)header)
		{
			GameObject copy = UnityEngine.Object.Instantiate(header.gameObject, root.transform, worldPositionStays: false);
			copy.name = "Header";
			Strip(copy);
			UIKit.Place(copy, headerX, 0f, Mathf.Min(headerRect.width, w - headerX), headerH);
			Text label = copy.GetComponentInChildren<Text>(includeInactive: true);
			if ((bool)label)
			{
				label.text = title;
			}
		}
		else
		{
			GameObject hdr = UIKit.Make("Header", root.transform);
			UIKit.Place(hdr, 0f, 0f, Mathf.Min(260f, w), headerH);
			Transform lt = hdr.transform.Find("Text");
			if ((bool)lt)
			{
				lt.GetComponent<Text>().text = title;
			}
		}
		GameObject body = UIKit.Make("Paragraph", root.transform);
		UIKit.Place(body, textX, textY, textW, Mathf.Max(20f, textH));
		Text text = body.GetComponent<Text>();
		text.supportRichText = true;
		text.alignment = TextAnchor.UpperLeft;
		text.horizontalOverflow = HorizontalWrapMode.Wrap;
		text.verticalOverflow = VerticalWrapMode.Truncate;
		text.resizeTextForBestFit = true;
		text.resizeTextMinSize = 9;
		text.resizeTextMaxSize = 13;
		text.raycastTarget = false;
		Plugin.Log.LogInfo(title + " added to " + page.name + " (" + w.ToString("0") + " x " + h.ToString("0") + ", header " + ((bool)header ? header.name : "addon's") + ", card " + ((bool)card.Key ? card.Key.name : "none") + ")");
		return new Panel
		{
			Root = root,
			Body = text,
			Page = page
		};
	}

	private static void Strip(GameObject go)
	{
		foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(includeInactive: true))
		{
			if (mb != null && !(mb.GetType().Namespace ?? "").StartsWith("UnityEngine"))
			{
				UnityEngine.Object.DestroyImmediate(mb);
			}
		}
	}

	private static bool OnWithin(Transform t, Transform page)
	{
		for (Transform x = t; (bool)x && x != page; x = x.parent)
		{
			if (!x.gameObject.activeSelf)
			{
				return false;
			}
		}
		return true;
	}

	private static bool HasAddonParent(Transform t, Transform page)
	{
		for (Transform x = t; (bool)x && x != page; x = x.parent)
		{
			if (x.name.StartsWith("Addon_"))
			{
				return true;
			}
		}
		return false;
	}

	private static Rect Local(RectTransform rt, RectTransform page)
	{
		Vector3[] c = new Vector3[4];
		rt.GetWorldCorners(c);
		Vector3 a = page.InverseTransformPoint(c[0]);
		Vector3 b = page.InverseTransformPoint(c[2]);
		return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
	}
}
