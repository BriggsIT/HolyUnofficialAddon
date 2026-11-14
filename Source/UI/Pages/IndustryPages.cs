using System;
using System.Collections.Generic;
using System.Linq;
using HolyAddon.Core;
using HolyAddon.Core.Industry;
using HolyAddon.Core.Updates;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI.Pages;

internal static class IndustryUi
{
	public static readonly Color Navy = new Color(0.13f, 0.3f, 0.44f);

	public static Sprite Icon => UpdateIcons.Game("iconPublisher", UpdateIcons.Game("iconNPC", UpdateIcons.Game("iconDeveloper")));

	// A studio's colour (its logo's), for the pop-up's title bar and accents.
	public static Color StudioColor(publisherScript p)
	{
		if (!p)
		{
			return Navy;
		}
		return SpriteTools.MainColor(p.GetLogo(), Navy);
	}

	// "Friendly" in its colour, with the standing number.
	public static string TierText(publisherScript p)
	{
		float s = IndustryCore.Standing(p);
		int tier = IndustryCore.Tier(s);
		return "<color=#" + ColorUtility.ToHtmlStringRGB(IndustryCore.TierColor(tier)) + ">" + IndustryCore.TierNames[tier] + "</color> (" + ((s > 0f) ? "+" : "") + Mathf.RoundToInt(s) + ")";
	}

	// "3.5 stars" (the game's font may not have star symbols).
	// A fixed text size (the row prefab's best fit makes short lines big and long ones small), wrapping in its box.
	public static Text Fixed(Text t, int size)
	{
		t.resizeTextForBestFit = false;
		t.fontSize = size;
		t.horizontalOverflow = HorizontalWrapMode.Wrap;
		t.verticalOverflow = VerticalWrapMode.Truncate;
		return t;
	}

	public static string Stars(float stars)
	{
		// The game keeps stars as 0-100: 20 per star.
		return Mathf.Clamp(stars / 20f, 0f, 5f).ToString("0.#") + " stars";
	}
}

// A decision on your desk: what happened, who it's about, and your choices.
internal class IndustryEventPage : Page
{
	private string result;

	private int picked = -1;

	private static IndustryPending P => IndustryCore.State.Pending;

	public override string Title
	{
		get
		{
			IndustryPending p = P;
			IndustryEventDef def = ((p != null) ? IndustryEvents.Find(p.Id) : null);
			if (def == null)
			{
				return "Industry";
			}
			return def.Title(p.C);
		}
	}

	public override Sprite IconSprite => IndustryUi.Icon;

	public override float Width => 960f;

	// Fitted to its contents: banner, text, the choice cards and the button row.
	public override float Height => 515f;

	public override void Build(EditorWindow w)
	{
		float W = w.Width;
		IndustryPending p = P;
		IndustryEventDef def = ((p != null) ? IndustryEvents.Find(p.Id) : null);
		if (result != null || def == null)
		{
			w.Label(result ?? "That's been dealt with.", 24f, 30f, W - 48f, 110f, TextAnchor.UpperLeft, bold: false, null, 15);
			w.BigButton("Close", 14f, 150f, 220f, 44f, w.Close);
			w.SmallButton("Open Industry...", 248f, 157f, 220f, delegate
			{
				w.Close();
				EditorWindow.Industry.Open();
			}, enabled: true, null, null, 30f);
			return;
		}
		publisherScript studio = IndustryCore.Studio(p.C.StudioId);
		publisherScript studio2 = IndustryCore.Studio(p.C.Studio2);
		RectTransform banner = w.Banner(UpdateIcons.Banner("industry"), 14f, 14f, W - 28f, 120f, full: true);
		float tx = 30f;
		if ((bool)studio && (bool)studio2)
		{
			// Two studios (a bidding war): both, side by side.
			StudioBlock(w, banner, studio, 20f);
			StudioBlock(w, banner, studio2, 20f + (W - 28f) * 0.36f);
		}
		else if ((bool)studio)
		{
			Color c = IndustryUi.StudioColor(studio);
			Image glow = MmoUi.Rect(banner, -30f, -30f, 180f, 180f, new Color(c.r, c.g, c.b, 0.75f));
			glow.sprite = SpriteTools.Glow();
			StoreUi.Picture(w.Body, studio.GetLogo(), 34f, 34f, 76f);
			tx = 134f;
			w.BannerText("<b>" + studio.GetName() + "</b>", tx, 24f, W - tx - 300f, 30f, 20, Color.white);
			w.BannerText(IndustryUi.Stars(studio.stars) + "  ·  " + (studio.publisher ? (studio.developer ? "developer and publisher" : "publisher") : "developer") + ((studio.fanGenre >= 0) ? ("  ·  loves " + IndustryCore.GenreName(studio.fanGenre)) : ""), tx, 58f, W - tx - 300f, 24f, 14, new Color(0.84f, 0.88f, 0.94f));
			w.BannerText("Standing: " + IndustryUi.TierText(studio), tx, 88f, W - tx - 300f, 24f, 14, new Color(1f, 0.85f, 0.4f), bold: true);
		}
		else
		{
			w.BannerText("<b>" + def.Title(p.C) + "</b>", tx, 30f, W - tx - 300f, 32f, 21, Color.white);
			w.BannerText(IndustryCore.Year + "  ·  industry news", tx, 70f, W - tx - 300f, 24f, 14, new Color(0.84f, 0.88f, 0.94f));
		}
		float y = 148f;
		w.Label(def.Text(p.C), 24f, y, W - 48f, 70f, TextAnchor.UpperLeft, bold: false, null, 15);
		y += 82f;
		List<IndustryChoice> choices = def.Choices(p.C);
		int n = choices.Count;
		float cw = (W - 28f - (float)(n - 1) * 10f) / (float)n;
		for (int i = 0; i < n; i++)
		{
			IndustryChoice ch = choices[i];
			string problem = ch.Problem?.Invoke(p.C);
			int idx = i;
			SaleUi.Card(w, 14f + (float)i * (cw + 10f), y, cw, 112f, ch.Title, ch.Line1, ch.Line2, picked == i, delegate
			{
				picked = idx;
				w.Rebuild();
			}, problem == null, problem ?? ch.Tooltip, (bool)studio ? IndustryUi.StudioColor(studio) : IndustryUi.Navy);
		}
		y += 128f;
		IndustryChoice chosen = ((picked >= 0 && picked < n) ? choices[picked] : null);
		string blocked = chosen?.Problem?.Invoke(p.C);
		w.BigButton((chosen != null) ? ("Go With \"" + chosen.Title + "\"") : "Pick One Above", 14f, y, 320f, 44f, delegate
		{
			result = IndustryCore.Decide(picked) ?? "Done.";
			w.Rebuild();
		}, chosen != null && blocked == null, blocked);
		w.Label("Closing this window goes with \"" + choices[Mathf.Clamp(def.Default, 0, n - 1)].Title + "\".", 348f, y + 7f, W - 362f, 30f, TextAnchor.MiddleLeft, bold: false, null, 12).color = UIKit.Grey;
	}

	// One studio in the banner: its logo on a glow in its colour, its name, stars and your standing.
	private static void StudioBlock(EditorWindow w, RectTransform banner, publisherScript s, float x)
	{
		Color c = IndustryUi.StudioColor(s);
		Image glow = MmoUi.Rect(banner, x - 36f, -24f, 140f, 140f, new Color(c.r, c.g, c.b, 0.7f));
		glow.sprite = SpriteTools.Glow();
		StoreUi.Picture(w.Body, s.GetLogo(), 14f + x + 4f, 46f, 60f);
		float tx = 14f + x + 74f;
		w.BannerText("<b>" + s.GetName() + "</b>", tx, 30f, 230f, 26f, 17, Color.white);
		w.BannerText(IndustryUi.Stars(s.stars) + "  ·  " + (s.publisher ? "publisher" : "developer"), tx, 58f, 230f, 22f, 13, new Color(0.84f, 0.88f, 0.94f));
		w.BannerText("Standing: " + IndustryUi.TierText(s), tx, 82f, 230f, 22f, 13, new Color(1f, 0.85f, 0.4f), bold: true);
	}
}

// Every studio and where you stand with it; what's going on in the industry; your deals.
internal class IndustryPage : Page
{
	private int tab;

	private int filter;

	private static readonly string[] Filters = new string[5] { "All", "Allies and Friends", "Rivals", "Developers", "Publishers" };

	public override string Title => "Industry";

	public override Sprite IconSprite => IndustryUi.Icon;

	public override float Width => 1100f;

	public override float Height => 840f;

	public override void Build(EditorWindow w)
	{
		float W = w.Width;
		IndustryState s = IndustryCore.State;
		List<publisherScript> studios = IndustryCore.Studios();
		int friends = studios.Count((publisherScript p) => IndustryCore.Tier(IndustryCore.Standing(p)) >= 2);
		int rivals = studios.Count((publisherScript p) => IndustryCore.Tier(IndustryCore.Standing(p)) == 0);
		float cw = (W - 28f - 30f) / 4f;
		w.StatCard(IndustryUi.Icon, "Studios", studios.Count.ToString(), studios.Count((publisherScript p) => p.publisher) + " publish", 14f, 14f, cw, 80f);
		w.StatCard(UpdateIcons.Game("iconHappy", IndustryUi.Icon), "Friends and allies", friends.ToString(), "better terms, favours", 14f + cw + 10f, 14f, cw, 80f, (friends > 0) ? new Color?(UIKit.Green) : null);
		w.StatCard(UpdateIcons.Game("iconUnhappy", IndustryUi.Icon), "Rivals", rivals.ToString(), (rivals > 0) ? "they may act against you" : "none yet", 14f + 2f * (cw + 10f), 14f, cw, 80f, (rivals > 0) ? new Color?(UIKit.Red) : null);
		w.StatCard(UpdateIcons.Game("iconDollar", IndustryUi.Icon), "Deals running", (s.Streams.Count + s.Promises.Count).ToString(), "shares, fees, promises", 14f + 3f * (cw + 10f), 14f, cw, 80f);
		string[] tabs = new string[3] { "Studios", "Recent News", "Your Deals" };
		float tw = (W - 28f - 20f) / 3f;
		for (int i = 0; i < 3; i++)
		{
			int t = i;
			w.IconButton(tabs[i], (i == 0) ? IndustryUi.Icon : ((i == 1) ? UpdateIcons.Game("iconPress", IndustryUi.Icon) : UpdateIcons.Game("iconDollar", IndustryUi.Icon)), 14f + (float)i * (tw + 10f), 104f, tw, 44f, delegate
			{
				tab = t;
				w.Rebuild();
			});
			if (tab == i)
			{
				MmoUi.Rect(w.Body, 14f + (float)i * (tw + 10f), 150f, tw, 3f, UIKit.Blue);
			}
		}
		float y = 162f;
		if (tab == 0)
		{
			StudiosTab(w, studios, y);
		}
		else if (tab == 1)
		{
			if (Plugin.EnableCheats.Value)
			{
				w.SmallButton("Test Events...", W - 14f - 180f, y, 180f, () => w.Push(new IndustryTestPage()), enabled: true, "Cheats: fire any industry event now, to see how it plays.", null, 28f);
				y += 38f;
			}
			ListView list = w.List(13f, y, W - 26f, w.Height - 75f - y - 10f);
			foreach (string line in s.Feed)
			{
				RowView row = list.Row(36f);
				row.Text(line, 12f, row.W - 24f);
			}
			if (s.Feed.Count == 0)
			{
				RowView none = list.Row(36f);
				none.Text("Nothing yet: the news will fill up as the weeks go by.", 12f, none.W - 24f);
			}
		}
		else
		{
			DealsTab(w, y);
		}
	}

	private void StudiosTab(EditorWindow w, List<publisherScript> studios, float y)
	{
		float W = w.Width;
		float fw = (W - 28f - 40f) / 5f;
		for (int i = 0; i < Filters.Length; i++)
		{
			int f = i;
			w.SmallButton(Filters[i], 14f + (float)i * (fw + 10f), y, fw, delegate
			{
				filter = f;
				w.Rebuild();
			}, enabled: true, null, null, 28f);
			if (filter == i)
			{
				MmoUi.Rect(w.Body, 14f + (float)i * (fw + 10f), y + 30f, fw, 3f, UIKit.Blue);
			}
		}
		y += 40f;
		// Each studio's game in development, in one pass over the games.
		Dictionary<int, gameScript> developing = new Dictionary<int, gameScript>();
		foreach (gameScript g in IndustryCore.AllGames())
		{
			if (g.inDevelopment && !g.isOnMarket && g.developerID >= 0 && (!developing.TryGetValue(g.developerID, out var had) || (g.angekuendigt && !had.angekuendigt)))
			{
				developing[g.developerID] = g;
			}
		}
		IEnumerable<publisherScript> shown = studios;
		switch (filter)
		{
		case 1:
			shown = studios.Where((publisherScript p) => IndustryCore.Tier(IndustryCore.Standing(p)) >= 2);
			break;
		case 2:
			shown = studios.Where((publisherScript p) => IndustryCore.Tier(IndustryCore.Standing(p)) == 0);
			break;
		case 3:
			shown = studios.Where((publisherScript p) => p.developer);
			break;
		case 4:
			shown = studios.Where((publisherScript p) => p.publisher);
			break;
		}
		List<publisherScript> list2 = shown.OrderByDescending((publisherScript p) => Mathf.Abs(IndustryCore.Standing(p))).ThenByDescending((publisherScript p) => p.stars).ToList();
		ListView list = w.List(13f, y, W - 26f, w.Height - 75f - y - 10f);
		foreach (publisherScript p2 in list2)
		{
			int id = p2.myID;
			RowView row = list.Row(56f, delegate
			{
				w.Push(new StudioPage(id));
			}, p2.GetTooltip());
			row.Picture(p2.GetLogo(), 8f, 42f);
			IndustryUi.Fixed(row.Top(p2.GetName() + "  <color=#777777>" + IndustryUi.Stars(p2.stars) + "</color>", 60f, 330f), 15);
			IndustryUi.Fixed(row.Bottom((p2.publisher ? (p2.developer ? "Developer and publisher" : "Publisher") : "Developer") + ((p2.fanGenre >= 0) ? ("  ·  loves " + IndustryCore.GenreName(p2.fanGenre)) : ""), 60f, 330f), 13);
			developing.TryGetValue(p2.myID, out var dev);
			// Up to two lines, kept clear of the standing bar (which starts at W - 370).
			Text devText = row.Text((dev != null) ? ((dev.angekuendigt ? "Announced: " : "In development: ") + dev.GetNameSimple()) : "Nothing in development", 400f, row.W - 370f - 410f, bold: false, TextAnchor.MiddleLeft, 4f, 48f);
			devText.color = ((dev != null) ? Color.black : UIKit.Grey);
			devText.resizeTextForBestFit = false;
			devText.fontSize = 13;
			devText.horizontalOverflow = HorizontalWrapMode.Wrap;
			devText.verticalOverflow = VerticalWrapMode.Truncate;
			float st = IndustryCore.Standing(p2);
			int tier = IndustryCore.Tier(st);
			row.Bar(row.W - 370f, 200f, 20f, Mathf.InverseLerp(-100f, 100f, st), IndustryCore.TierColor(tier), IndustryCore.TierNames[tier] + " " + ((st > 0f) ? "+" : "") + Mathf.RoundToInt(st));
			IndustryCore.State.Notes.TryGetValue(p2.myID, out var notes);
			RelationNote last = notes?.FirstOrDefault();
			IndustryUi.Fixed(row.Text((last != null) ? last.Text : "", row.W - 160f, 150f, bold: false, TextAnchor.MiddleLeft, 4f, 48f), 12).color = UIKit.Grey;
		}
		if (list2.Count == 0)
		{
			RowView none = list.Row(36f);
			none.Text("No studios here.", 12f, none.W - 24f);
		}
	}

	private static void DealsTab(EditorWindow w, float y)
	{
		IndustryState s = IndustryCore.State;
		ListView list = w.List(13f, y, w.Width - 26f, w.Height - 75f - y - 10f);
		foreach (IncomeStream d in s.Streams)
		{
			RowView row = list.Row(46f);
			row.Top(d.Label, 12f, row.W - 24f);
			string what = ((d.GameId >= 0) ? (Mathf.RoundToInt(d.Share * 100f) + "% of its revenue " + (d.Out ? "goes out" : "comes in")) : (G.Money(d.Fixed) + " a week " + (d.Out ? "goes out" : "comes in")));
			string when = ((d.GameId >= 0 && !d.Started) ? "starts when the game is out" : (d.WeeksLeft + ((d.WeeksLeft == 1) ? " week left" : " weeks left")));
			row.Bottom(what + "  ·  " + when, 12f, row.W - 24f).color = (d.Out ? UIKit.Red : UIKit.Green);
		}
		foreach (PlatformPromise p in s.Promises)
		{
			platformScript pl = GameUpdates.Platform(p.PlatformId);
			RowView row2 = list.Row(46f);
			row2.Top("Promise: a game on " + (pl ? pl.GetName() : "a new machine"), 12f, row2.W - 24f);
			row2.Bottom(Mathf.Max(0, p.Deadline - IndustryCore.Now) + " weeks left, or pay " + G.Money(p.Price) + " for the dev kit", 12f, row2.W - 24f).color = new Color(0.8f, 0.45f, 0.05f);
		}
		if (s.Streams.Count == 0 && s.Promises.Count == 0)
		{
			RowView none = list.Row(36f);
			none.Text("No deals running.", 12f, none.W - 24f);
		}
	}
}

// One studio: who they are, where you stand, what you've been through together, and their games.
// Cheats on: every industry event, click one to fire it now (if its conditions are met; years and cooldowns ignored).
internal class IndustryTestPage : Page
{
	private string result;

	public override string Title => "Test Industry Events";

	public override Sprite IconSprite => IndustryUi.Icon;

	public override float Width => 820f;

	public override float Height => 800f;

	public override void Build(EditorWindow w)
	{
		BackButton(w);
		float W = w.Width;
		w.Label("Click an event to fire it now. Its years and cooldown are ignored, but it still needs what it's about (a game on sale, a studio with a game in development...). Decisions pop up when you close this window.", 120f, 8f, W - 134f, 52f, TextAnchor.UpperLeft, bold: false, null, 12).color = UIKit.Grey;
		Text status = w.Label(result ?? "", 14f, 64f, W - 28f, 40f, TextAnchor.MiddleLeft, bold: true, null, 13);
		float y = 110f;
		ListView list = w.List(13f, y, W - 26f, w.Height - 75f - y - 10f);
		foreach (IndustryEventDef d in IndustryEvents.Defs.OrderBy((IndustryEventDef x) => x.Headline).ThenBy((IndustryEventDef x) => x.From).ThenBy((IndustryEventDef x) => x.To))
		{
			string id = d.Id;
			string name = (IndustryEvents.Names.TryGetValue(id, out var n) ? n : id);
			string years = ((d.To >= 9999) ? (d.From + " on") : (d.From + "-" + d.To));
			bool inEra = IndustryCore.Year >= d.From && IndustryCore.Year <= d.To;
			RowView row = list.Row(44f, delegate
			{
				result = IndustryEvents.Trigger(id);
				UIKit.Sound();
				w.Rebuild();
			}, "Click to fire it now.");
			row.Top(name, 12f, row.W - 24f);
			row.Bottom((d.Headline ? "Headline" : "Decision") + "  ·  " + years + (inEra ? "" : "  ·  outside its years now"), 12f, row.W - 24f).color = (inEra ? UIKit.Grey : new Color(0.75f, 0.45f, 0.1f));
		}
	}
}

internal class StudioPage : Page
{
	private readonly int id;

	public StudioPage(int id)
	{
		this.id = id;
	}

	public override string Title
	{
		get
		{
			publisherScript p = IndustryCore.Studio(id);
			if (!p)
			{
				return "Studio";
			}
			return p.GetName();
		}
	}

	public override Sprite IconSprite => IndustryUi.Icon;

	public override float Width => 960f;

	public override float Height => 760f;

	public override void Build(EditorWindow w)
	{
		BackButton(w);
		float W = w.Width;
		publisherScript p = IndustryCore.Studio(id);
		if (!p)
		{
			w.Label("This studio is gone.", 24f, 60f, W - 48f, 30f);
			return;
		}
		RectTransform banner = w.Banner(UpdateIcons.Banner("industry"), 14f, 48f, W - 28f, 112f, full: true);
		Color c = IndustryUi.StudioColor(p);
		Image glow = MmoUi.Rect(banner, -30f, -34f, 180f, 180f, new Color(c.r, c.g, c.b, 0.75f));
		glow.sprite = SpriteTools.Glow();
		StoreUi.Picture(w.Body, p.GetLogo(), 34f, 66f, 76f);
		w.BannerText("<b>" + p.GetName() + "</b>", 134f, 58f, W - 460f, 30f, 20, Color.white);
		w.BannerText(IndustryUi.Stars(p.stars) + "  ·  founded " + p.date_year + ((p.fanGenre >= 0) ? ("  ·  loves " + IndustryCore.GenreName(p.fanGenre)) : ""), 134f, 92f, W - 460f, 24f, 14, new Color(0.84f, 0.88f, 0.94f));
		w.BannerText("Standing: " + IndustryUi.TierText(p), 134f, 120f, W - 460f, 24f, 14, new Color(1f, 0.85f, 0.4f), bold: true);
		float y = 172f;
		List<KeyValuePair<string, string>> facts = new List<KeyValuePair<string, string>>
		{
			EditorWindow.KV("Kind", p.publisher ? (p.developer ? "Developer and publisher" : "Publisher") : "Developer"),
			EditorWindow.KV("Company value", G.Money(p.GetFirmenwert())),
			EditorWindow.KV("Publishing relation", Mathf.RoundToInt(p.relation) + " / 100 (sets your terms)"),
			EditorWindow.KV("Games released", p.GetAmountGames().ToString())
		};
		y += w.Facts(14f, y, W - 28f, facts, 2) + 12f;
		w.Header("Between you", 14f, y, (W - 38f) / 2f);
		w.Header("Their games", 24f + (W - 38f) / 2f, y, (W - 38f) / 2f);
		y += 32f;
		float half = (W - 38f) / 2f;
		ListView notes = w.List(13f, y, half, w.Height - 75f - y - 10f);
		IndustryCore.State.Notes.TryGetValue(id, out var list);
		foreach (RelationNote n in list ?? new List<RelationNote>())
		{
			RowView row = notes.Row(44f);
			Fixed(row.Text(n.Text, 10f, row.W - 70f, bold: false, TextAnchor.MiddleLeft, 4f, 36f));
			Fixed(row.Text(((n.Delta > 0) ? "+" : "") + n.Delta, row.W - 60f, 50f, bold: true, TextAnchor.MiddleRight, 4f, 36f)).color = ((n.Delta > 0) ? UIKit.Green : UIKit.Red);
		}
		if (list == null || list.Count == 0)
		{
			RowView none = notes.Row(36f);
			Fixed(none.Text("Nothing yet.", 10f, none.W - 20f, bold: false, TextAnchor.MiddleLeft, 8f, 20f)).color = UIKit.Grey;
		}
		ListView games = w.List(23f + half, y, half, w.Height - 75f - y - 10f);
		foreach (gameScript g in (from x in IndustryCore.AllGames()
			where x.developerID == id && (x.isOnMarket || x.inDevelopment)
			orderby x.inDevelopment descending, x.isOnMarket ? CurrentChartsPage.Week(x) : 0 descending
			select x).Take(12))
		{
			RowView row2 = games.Row(40f, null, g.GetTooltip());
			row2.Top(g.GetNameSimple(), 10f, row2.W - 20f);
			row2.Bottom(g.inDevelopment ? (g.angekuendigt ? "Announced" : "In development") : (g.reviewTotal + "%  ·  " + MmoUi.Short(CurrentChartsPage.Week(g)) + " this week"), 10f, row2.W - 20f);
		}
	}

	// The same text size on every row (the row prefab's best fit would blow short lines up).
	private static Text Fixed(Text t)
	{
		t.resizeTextForBestFit = false;
		t.fontSize = 14;
		t.horizontalOverflow = HorizontalWrapMode.Wrap;
		t.verticalOverflow = VerticalWrapMode.Truncate;
		return t;
	}
}
