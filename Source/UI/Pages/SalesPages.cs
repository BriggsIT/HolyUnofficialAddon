using System;
using System.Collections.Generic;
using System.Linq;
using HolyAddon.Core;
using HolyAddon.Core.Mmo;
using HolyAddon.Core.Store;
using HolyAddon.Core.Updates;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI.Pages;

// Shared pieces of the sales pages: choice cards, effect lines, the store's sale depths.
internal static class SaleUi
{
	public static readonly string[] Months = new string[12] { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };

	// The accent: your store's colour once you own one, sale red before that.
	public static Color Accent => EditorWindow.SaleTitle()?.Key ?? EditorWindow.SaleTitleBar;

	public static string When(StoreSaleEvent e)
	{
		int endWeek = e.Week + e.Weeks - 1;
		if (endWeek <= 4)
		{
			return Months[e.Month - 1] + ", week " + e.Week + ((e.Weeks > 1) ? ("-" + endWeek) : "");
		}
		return Months[e.Month - 1] + " week " + e.Week + " to " + Months[e.Month % 12] + " week " + (endWeek - 4);
	}

	public static string Who(StoreSaleEvent e)
	{
		if (!SalesEvents.Wide(e))
		{
			return "on " + StoreOps.State.Name + " only";
		}
		return "every big digital store";
	}

	// What a discount does for your games, given how much of their buyers the sale reaches.
	public static string GamesEffect(int discount, float reach)
	{
		if (discount == 0)
		{
			return "No spike, no lost money per copy";
		}
		int copies = Mathf.RoundToInt((float)SalesEvents.Discounts[discount] / 100f * SalesEvents.BonusPerDiscount * reach * 100f);
		return "Copies +" + copies + "%";
	}

	public static string MoneyEffect(int discount, float reach)
	{
		if (discount == 0)
		{
			return "Money as usual";
		}
		int money = Mathf.RoundToInt((SalesEvents.MoneyFactor(discount, reach) - 1f) * 100f);
		if (money == 0)
		{
			return "About the same money";
		}
		return "Money " + ((money > 0) ? ("+" + money) : money.ToString()) + "% a week";
	}

	public static readonly string[] StoreNames = new string[4] { "Sit It Out", "Light Sale", "Standard Sale", "Deep Sale" };

	public static readonly string[] StoreLine1 = new string[4] { "No sale on your store", "About 20% off, traffic +8%", "About 40% off, traffic +15%", "About 65% off, traffic +25%" };

	public static readonly string[] StoreLine2 = new string[4] { "Buyers may drift to rivals", "Commission x1.2", "Commission x1.3, sign-ups", "Commission x1.1, most sign-ups, less trust" };

	// One choice as a big button with two lines under its title; the picked one has a frame in the accent colour.
	public static void Card(EditorWindow w, float x, float y, float cw, float ch, string title, string line1, string line2, bool picked, Action pick, bool enabled = true, string tooltip = null, Color? accent = null)
	{
		if (picked)
		{
			MmoUi.Rect(w.Body, x - 5f, y - 5f, cw + 10f, ch + 10f, accent ?? Accent);
		}
		Button b = w.BigButton("", x, y, cw, ch, pick, enabled, tooltip);
		// The button's own text colour, so the lines read on it whatever its look.
		Text own = b.transform.Find("Text")?.GetComponent<Text>();
		Color ink = (own ? own.color : Color.black);
		Text t0 = w.Label("<b>" + title + "</b>", 6f, 6f, cw - 12f, 26f, TextAnchor.MiddleCenter, bold: false, b.transform, 16);
		Text t1 = w.Label(line1, 6f, 34f, cw - 12f, 20f, TextAnchor.MiddleCenter, bold: false, b.transform, 12);
		Text t2 = w.Label(line2, 6f, 54f, cw - 12f, 32f, TextAnchor.UpperCenter, bold: false, b.transform, 12);
		t0.color = ink;
		t1.color = ink;
		t2.color = new Color(ink.r, ink.g, ink.b, 0.75f);
	}
}

// Sales Events: the year's sales, your games' discount and your store's sale for each, and how past sales went.
internal class SalesPage : Page
{
	private int seen = -1;

	public override string Title => "Sales Events";

	public override Sprite IconSprite => UpdateIcons.Game("iconMarketingSpecial", UpdateIcons.Game("iconDollar"));

	public override float Width => 1000f;

	public override float Height => 840f;

	public override void Build(EditorWindow w)
	{
		seen = GameUpdates.Version;
		SalesEvents.Ready();
		SalesState s = SalesEvents.State;
		bool root = w == EditorWindow.Sales;
		float o = (root ? 0f : 44f);
		if (!root)
		{
			BackButton(w);
			w.Label(G.Money(((bool)G.MS) ? G.MS.money : 0), w.Width - 274f, 10f, 260f, 28f, TextAnchor.MiddleRight, bold: true);
		}
		float W = w.Width;
		List<gameScript> games = SalesEvents.MyGames();
		List<int> away;
		List<KeyValuePair<StoreSaleEvent, int>> next = SalesEvents.Upcoming(out away);
		w.Banner(UpdateIcons.Banner("sale"), 14f, 14f + o, W - 28f, 110f, full: true);
		w.BannerText("<b>Sales Events</b>", 30f, 22f + o, W - 400f, 30f, 21, Color.white);
		string status;
		if (SalesEvents.Running)
		{
			status = s.Event + " on now: your games " + SalesEvents.Discounts[s.Discount] + "% off, " + s.WeeksLeft + ((s.WeeksLeft == 1) ? " week left" : " weeks left");
		}
		else if (next.Count > 0)
		{
			status = "No sale running  ·  next: " + next[0].Key.Name + ((away[0] == 0) ? " (on now)" : (" in " + away[0] + ((away[0] == 1) ? " week" : " weeks")));
		}
		else
		{
			status = "Sales start in " + HolyCompat.SeasonalYear() + ".";
		}
		w.BannerText(status, 30f, 58f + o, W - 400f, 24f, 14, new Color(0.84f, 0.88f, 0.94f));
		string mood = ((s.FatigueWeeks > 0) ? ("Players are waiting for the next sale: your games sell " + Mathf.RoundToInt((0f - SalesEvents.FatiguePenalty) * 100f) + "% less for " + s.FatigueWeeks + " more weeks") : ("Summer and Winter run in every big store" + (StoreOps.State.Built ? ("; " + StoreOps.State.Name + " adds Spring and Autumn") : "")));
		w.BannerText(mood, 30f, 86f + o, W - 400f, 24f, 14, (s.FatigueWeeks > 0) ? new Color(1f, 0.55f, 0.45f) : new Color(1f, 0.8f, 0.3f), bold: true);
		float y = 136f + o;
		float cw = (W - 28f - 30f) / 4f;
		long weekly = games.Sum(SalesEvents.Weekly);
		w.StatCard(UpdateIcons.Game("iconGames", UpdateIcons.Store()), "Your games that can join", games.Count.ToString(), MmoUi.Short(weekly) + " copies last week", 14f, y, cw, 80f, null, "Your own games out for " + SalesEvents.MinWeeksOnMarket + " weeks or more (not arcade or MMO).");
		float last = SalesEvents.SalesLastYear();
		bool tired = last >= SalesEvents.FatigueSales - 1f;
		w.StatCard(UpdateIcons.Game("iconDollar", UpdateIcons.Store()), "Sales joined this year", last.ToString("0.#"), (s.FatigueWeeks > 0) ? "Players are waiting" : (tired ? "One more and players wait" : ("Players wait after " + SalesEvents.FatigueSales.ToString("0"))), 14f + cw + 10f, y, cw, 80f, (s.FatigueWeeks > 0 || tired) ? new Color?(UIKit.Red) : null, "Join " + SalesEvents.FatigueSales.ToString("0") + " sales within a year and players start waiting for the next one: your games sell " + Mathf.RoundToInt((0f - SalesEvents.FatiguePenalty) * 100f) + "% less for " + SalesEvents.FatigueWeeks + " weeks after. A sale only your store runs counts for the share of buyers it reaches.");
		SaleRecord lastSale = s.History.LastOrDefault();
		w.StatCard(UpdateIcons.Game("iconCharts", UpdateIcons.Store()), "Last sale", (lastSale != null) ? MmoUi.Short(lastSale.Units) : "-", (lastSale != null) ? (lastSale.Name + " " + lastSale.Year + ", " + SalesEvents.Discounts[lastSale.Discount] + "% off") : "None yet", 14f + 2f * (cw + 10f), y, cw, 80f);
		StoreSaleRecord storeLast = (StoreOps.State.Built ? StoreOps.State.SaleHistory.LastOrDefault() : null);
		w.StatCard(UpdateIcons.Store(), StoreOps.State.Built ? (StoreOps.State.Name + "'s last sale") : "Your store", (storeLast != null) ? G.Money(storeLast.Commission) : "-", (storeLast != null) ? (storeLast.Name + ", " + MmoUiText.Num(storeLast.Accounts) + " sign-ups") : (StoreOps.State.Built ? "None yet" : "Build one to run your own sales"), 14f + 3f * (cw + 10f), y, cw, 80f);
		y += 92f;
		w.Toggle("Don't ask: join each sale with my plans", 24f, y, 420f, s.AutoJoin, (bool v) =>
		{
			SalesEvents.State.AutoJoin = v;
			GameUpdates.Touch();
		}, "No pop-up as a sale starts: your games and your store go with what's planned below.");
		w.Label("Usual discount (Summer, Winter)", W - 28f - 220f - 290f, y, 280f, 28f, TextAnchor.MiddleRight);
		w.Dropdown(W - 14f - 220f, y, 220f, SalesEvents.DiscountNames, Mathf.Clamp(s.AutoDiscount, 0, 3), (int v) =>
		{
			UIKit.Sound();
			SalesEvents.State.AutoDiscount = Mathf.Clamp(v, 0, 3);
			GameUpdates.Touch();
		});
		y += 40f;
		w.Header("The year's sales", 14f, y, W - 28f);
		y += 32f;
		bool store = StoreOps.State.Built;
		for (int i = 0; i < next.Count; i++)
		{
			StoreSaleEvent e = next[i].Key;
			int year = next[i].Value;
			string key = StorePlus.Key(e, year);
			bool running = away[i] == 0;
			bool decided = running && (s.Decided == key || s.Now == key);
			RectTransform panel = w.Panel(14f, y, W - 28f, 56f);
			w.Label("<b>" + e.Name + " " + year + "</b>", 14f, 5f, 330f, 24f, TextAnchor.MiddleLeft, bold: false, panel, 15);
			w.Label(SaleUi.When(e) + "  ·  " + SaleUi.Who(e) + "  ·  " + (running ? "on now" : ("in " + ((away[i] == 1) ? "1 week" : (away[i] + " weeks")))), 14f, 28f, 420f, 22f, TextAnchor.MiddleLeft, bold: false, panel, 12).color = UIKit.Grey;
			StoreSaleEvent ev = e;
			float dx = W - 28f - 10f;
			if (SalesEvents.StoreRuns(e, year))
			{
				dx -= 220f;
				w.Label("Store", dx - 52f, 14f, 50f, 28f, TextAnchor.MiddleRight, bold: false, panel, 12);
				Dropdown sd = w.Dropdown(dx, 14f, 220f, SaleUi.StoreNames, StorePlus.Depth(e, year), (int v) =>
				{
					UIKit.Sound();
					StorePlus.SetDepth(ev, year, v);
				}, panel);
				sd.interactable = !running;
				dx -= 70f;
			}
			dx -= 200f;
			w.Label(store ? "Your games" : "Your games' discount", dx - 160f, 14f, 156f, 28f, TextAnchor.MiddleRight, bold: false, panel, 12);
			Dropdown gd = w.Dropdown(dx, 14f, 200f, SalesEvents.DiscountNames, decided ? ((s.Now == key) ? s.Discount : 0) : SalesEvents.Planned(e, year), (int v) =>
			{
				UIKit.Sound();
				SalesEvents.Plan(ev, year, v);
			}, panel);
			gd.interactable = !decided;
			y += 62f;
		}
		if (next.Count == 0)
		{
			w.Label("No sale in the coming year. The Summer and Winter Sales start in " + HolyCompat.SeasonalYear() + ".", 24f, y, W - 48f, 26f);
			y += 32f;
		}
		y += 4f;
		w.Header("How past sales went", 14f, y, W - 28f);
		y += 32f;
		ListView list = w.List(13f, y, W - 26f, Mathf.Max(90f, w.Height - 75f - y - 10f));
		List<KeyValuePair<string, string>> past = Past(10);
		foreach (KeyValuePair<string, string> kv in past)
		{
			RowView row = list.Row(46f);
			row.Top(kv.Key, 12f, row.W - 24f);
			row.Bottom(kv.Value, 12f, row.W - 24f);
		}
		if (past.Count == 0)
		{
			RowView none = list.Row(34f);
			none.Text("None yet.", 12f, none.W - 24f);
		}
	}

	// Past sales, newest first: your games' result and your store's, by event.
	private static List<KeyValuePair<string, string>> Past(int count)
	{
		SalesState s = SalesEvents.State;
		List<StoreSaleRecord> storeSales = StoreOps.State.SaleHistory ?? new List<StoreSaleRecord>();
		List<string> keys = s.History.Select((SaleRecord r) => r.Id).Concat(storeSales.Select((StoreSaleRecord r) => r.Id)).Distinct().ToList();
		List<KeyValuePair<string, string>> rows = new List<KeyValuePair<string, string>>();
		foreach (string key in Enumerable.Reverse(keys).Take(count))
		{
			SaleRecord mine = s.History.LastOrDefault((SaleRecord r) => r.Id == key);
			StoreSaleRecord theirs = storeSales.LastOrDefault((StoreSaleRecord r) => r.Id == key);
			string name = (mine != null) ? (mine.Name + " " + mine.Year) : (theirs.Name + " " + theirs.Year);
			List<string> parts = new List<string>();
			if (mine != null)
			{
				string normal = ((mine.BaseWeekly > 0 && mine.Weeks > 0) ? (" (about " + MmoUi.Short(mine.BaseWeekly * mine.Weeks) + " at full price)") : "");
				parts.Add("Your " + mine.Games + ((mine.Games == 1) ? " game: " : " games: ") + SalesEvents.Discounts[mine.Discount] + "% off, " + MmoUi.Short(mine.Units) + " copies" + normal + ", " + G.Money(mine.Revenue) + (mine.Fatigue ? ", then players waited" : ""));
			}
			if (theirs != null)
			{
				parts.Add(StoreOps.State.Name + ": " + StorePlus.DepthShort[Mathf.Clamp(theirs.Depth, 0, 3)] + " sale, " + G.Money(theirs.Commission) + " commission, " + MmoUiText.Num(theirs.Accounts) + " sign-ups");
			}
			rows.Add(new KeyValuePair<string, string>(name, string.Join("  ·  ", parts)));
		}
		return rows;
	}

	public override void Tick(EditorWindow w)
	{
		if (GameUpdates.Version != seen)
		{
			w.Rebuild();
		}
	}
}

// The pop-up as a sale starts: your games' discount and your store's sale, with what each would do.
internal class SalePopupPage : Page
{
	// What the pop-up shows as picked (closing it goes ahead with these).
	public static int? PickedGames;

	public static int? PickedStore;

	private static string pickedFor;

	private string result;

	// The pop-up's picks for this event, if it showed them.
	public static bool Picks(string key, out int games, out int store)
	{
		games = PickedGames ?? 0;
		store = PickedStore ?? 0;
		if (pickedFor == key && PickedGames.HasValue)
		{
			return PickedStore.HasValue;
		}
		return false;
	}

	public override string Title
	{
		get
		{
			StoreSaleEvent e = SalesEvents.Find(Key, out int year);
			if (e == null)
			{
				return "Sale";
			}
			return e.Name + " " + year;
		}
	}

	public override Sprite IconSprite => UpdateIcons.Game("iconMarketingSpecial", UpdateIcons.Game("iconDollar"));

	public override float Width => 960f;

	// Fitted to what Build shows: the banner, your games' cards and/or the store's cards, the toggle and the buttons.
	public override float Height
	{
		get
		{
			string key = Key;
			StoreSaleEvent e = SalesEvents.Find(key, out int year);
			if (result != null || e == null || SalesEvents.State.Decided == key)
			{
				return 290f;
			}
			int games = SalesEvents.MyGames().Count;
			float y = 144f;
			if (games > 0)
			{
				y += 136f + (SalesEvents.Wide(e) ? 0f : 24f);
			}
			if (SalesEvents.StoreRuns(e, year))
			{
				y += 136f;
			}
			else if (games == 0)
			{
				y += 34f;
			}
			return 75f + y + 104f;
		}
	}

	private static string Key => SalesEvents.Waiting ?? SalesEvents.State.Asked;

	public override void Build(EditorWindow w)
	{
		float W = w.Width;
		string key = Key;
		StoreSaleEvent e = SalesEvents.Find(key, out int year);
		if (result != null || e == null || SalesEvents.State.Decided == key)
		{
			w.Label(result ?? "That sale has been decided.", 24f, 40f, W - 48f, 90f, TextAnchor.UpperLeft, bold: false, null, 15);
			w.BigButton("Close", 14f, 150f, 220f, 44f, w.Close);
			w.SmallButton("Plan the Year's Sales...", 248f, 157f, 240f, () => w.Push(new SalesPage()), enabled: true, null, null, 30f);
			return;
		}
		if (pickedFor != key)
		{
			pickedFor = key;
			PickedGames = SalesEvents.Planned(e, year);
			PickedStore = StorePlus.Depth(e, year);
		}
		List<gameScript> games = SalesEvents.MyGames();
		bool wide = SalesEvents.Wide(e);
		bool store = SalesEvents.StoreRuns(e, year);
		w.Banner(UpdateIcons.Banner("sale"), 14f, 14f, W - 28f, 116f, full: true);
		w.BannerText("<b>The " + e.Name + " starts</b>", 30f, 22f, W - 420f, 32f, 22, Color.white);
		w.BannerText(SaleUi.When(e) + "  ·  " + e.Weeks + ((e.Weeks == 1) ? " week" : " weeks") + "  ·  " + SaleUi.Who(e), 30f, 60f, W - 420f, 24f, 14, new Color(0.84f, 0.88f, 0.94f));
		float last = SalesEvents.SalesLastYear();
		string mood = ((SalesEvents.State.FatigueWeeks > 0) ? "Players are still waiting for sales from last time" : ((last >= SalesEvents.FatigueSales - 1f && games.Count > 0) ? ("You've joined " + last.ToString("0.#") + " sales this year: one more and players start waiting") : (wide ? "Buyers everywhere are looking for a bargain" : "A sale on your store only")));
		w.BannerText(mood, 30f, 90f, W - 420f, 24f, 14, (SalesEvents.State.FatigueWeeks > 0 || last >= SalesEvents.FatigueSales - 1f) ? new Color(1f, 0.55f, 0.45f) : new Color(1f, 0.8f, 0.3f), bold: true);
		float y = 144f;
		float cw = (W - 28f - 30f) / 4f;
		if (games.Count > 0)
		{
			float reach = SalesEvents.AverageReach(games, wide);
			long weekly = games.Sum(SalesEvents.Weekly);
			w.Header("Your games: " + games.Count + " can join (" + MmoUi.Short(weekly) + " copies last week)", 14f, y, W - 28f);
			y += 32f;
			if (!wide)
			{
				w.Label("Only buyers on " + StoreOps.State.Name + " see this sale: about " + Mathf.RoundToInt(reach * 100f) + "% of your games' buyers (all of them for your store exclusives).", 18f, y, W - 36f, 22f, TextAnchor.MiddleLeft, bold: false, null, 12).color = UIKit.Grey;
				y += 24f;
			}
			for (int i = 0; i < 4; i++)
			{
				int d = i;
				SaleUi.Card(w, 14f + (float)i * (cw + 10f), y, cw, 92f, SalesEvents.DiscountNames[i], SaleUi.GamesEffect(i, reach), SaleUi.MoneyEffect(i, reach), PickedGames == i, () =>
				{
					PickedGames = d;
					w.Rebuild();
				}, enabled: true, (i == 0) ? "Your games stay at full price." : ("Each copy sells for " + SalesEvents.Discounts[i] + "% less" + (wide ? "" : " on your store") + ", and many more sell for the sale's " + e.Weeks + ((e.Weeks == 1) ? " week" : " weeks") + ". All sales bonuses together are capped (+250% with The Holy Expansion)."));
			}
			y += 104f;
		}
		if (store)
		{
			w.Header(StoreOps.State.Name + "'s sale for every game it sells", 14f, y, W - 28f);
			y += 32f;
			for (int i = 0; i < 4; i++)
			{
				int d = i;
				string line2 = ((i == 0 && !wide) ? "Nothing changes" : SaleUi.StoreLine2[i]);
				SaleUi.Card(w, 14f + (float)i * (cw + 10f), y, cw, 92f, SaleUi.StoreNames[i], SaleUi.StoreLine1[i], line2, PickedStore == i, () =>
				{
					PickedStore = d;
					w.Rebuild();
				}, enabled: true, (i == 0) ? (wide ? "The rival stores run theirs anyway: buyers drift their way while yours doesn't." : "No Spring or Autumn sale this year.") : ("Other studios' games on your store are discounted too: more traffic and new accounts, and more of their copies sell through you." + ((i == 3) ? " Studios dislike deep cuts: their trust drops 1.2 a week while it runs." : "")));
			}
			y += 104f;
		}
		else if (games.Count == 0)
		{
			w.Label("None of your games can join yet (they need to be out " + SalesEvents.MinWeeksOnMarket + " weeks).", 24f, y, W - 48f, 26f);
			y += 34f;
		}
		y += 4f;
		w.Toggle("Don't ask again: join each sale with my plans", 24f, y, 460f, SalesEvents.State.AutoJoin, (bool v) =>
		{
			SalesEvents.State.AutoJoin = v;
			GameUpdates.Touch();
		}, "Set your plans for the year on the Sales Events page (Price & Packaging menu, or your store's Sales button).");
		y += 40f;
		w.BigButton("Go Ahead", 14f, y, 240f, 44f, () =>
		{
			StoreSaleEvent ev = SalesEvents.Find(key, out int yr);
			result = SalesEvents.Decide(key, PickedGames ?? 0, PickedStore ?? ((ev != null) ? StorePlus.Depth(ev, yr) : 0)) ?? "Done.";
			w.Rebuild();
		});
		w.SmallButton("Plan the Year's Sales...", 268f, y + 7f, 240f, () => w.Push(new SalesPage()), enabled: true, null, null, 30f);
		w.Label("Closing this window goes ahead with what's picked.", 524f, y + 7f, W - 538f, 30f, TextAnchor.MiddleLeft, bold: false, null, 12).color = UIKit.Grey;
	}
}
