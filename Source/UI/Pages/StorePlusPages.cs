using System;
using System.Collections.Generic;
using System.Linq;
using HolyAddon.Core;
using HolyAddon.Core.CopyProtection;
using HolyAddon.Core.Industry;
using HolyAddon.Core.Mmo;
using HolyAddon.Core.Store;
using HolyAddon.Core.Updates;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI.Pages;

// Shared by the store's phase 2 pages: the store's banner across the top, and a line when the store isn't built.
internal abstract class StorePlusPage : Page
{
	protected int seen = -1;

	public override Sprite IconSprite => UpdateIcons.Store();

	protected bool Top(EditorWindow w, string about)
	{
		seen = GameUpdates.Version;
		BackButton(w);
		w.MoneyLine();
		StoreState s = StoreOps.State;
		if (!s.Built)
		{
			w.Info(StoreOps.LockReason() ?? "Build your store first (Management > Your Digital Store).", 14f, 50f, w.Width - 28f, 60f);
			return false;
		}
		w.AboutLine(about);
		return true;
	}

	public override void Tick(EditorWindow w)
	{
		if (GameUpdates.Version != seen)
		{
			w.Rebuild();
		}
	}
}

// The best sellers on your store this week.
internal class StoreTopSellersPage : StorePlusPage
{
	public override string Title => "Store Charts";

	public override Sprite IconSprite => UpdateIcons.Game("iconCharts", UpdateIcons.Store());

	public override float Width => 960f;

	public override float Height => 720f;

	public override void Build(EditorWindow w)
	{
		if (!Top(w, "What sells on your store this week: your PC games and the other studios' games it carries. Yours are in blue."))
		{
			return;
		}
		List<KeyValuePair<gameScript, long>> top = StorePlus.TopSellers(30);
		genres gen = (G.MainObj ? G.MainObj.GetComponent<genres>() : null);
		w.Header("Top sellers this week (" + top.Count + ")", 14f, 54f, w.Width - 28f);
		ListView list = w.List(13f, 84f, w.Width - 26f, w.Height - 75f - 94f);
		for (int i = 0; i < top.Count; i++)
		{
			gameScript g = top[i].Key;
			RowView row = list.Row(48f, null, g.GetTooltip());
			if (g.IsMyGame())
			{
				row.Highlight();
			}
			EditorWindow.RankBadge(row.T, i + 1, 8f, 10f, 28f);
			Sprite genre = (((bool)gen && g.maingenre >= 0) ? gen.GetPic(g.maingenre) : null);
			row.Picture(genre, 44f, 30f);
			row.Top(g.GetNameWithTag(), 84f, row.W - 360f);
			row.Bottom(g.GetDeveloperName() + "  ·  " + g.reviewTotal + "%  ·  " + MmoUi.Years((float)g.weeksOnMarket / 52f) + " on sale", 84f, row.W - 360f);
			row.Text(MmoUi.Short(top[i].Value) + " this week", row.W - 270f, 260f, bold: true, TextAnchor.MiddleRight, 4f, 22f);
			row.Text(g.IsMyGame() ? (StoreOps.IsExclusive(g) ? "yours, exclusive" : "yours") : (G.Money(StoreOps.CommissionOn(g)) + " to you"), row.W - 270f, 260f, bold: false, TextAnchor.MiddleRight, 24f, 22f);
		}
		if (top.Count == 0)
		{
			w.Label("Nothing sold on your store yet this week.", 24f, 90f, w.Width - 48f, 26f);
		}
	}
}

// Studios and your store: their requests to sign, who has signed, and who hasn't.
internal class StoreStudiosPage : StorePlusPage
{
	private string status = "";

	public override string Title => "Studios";

	public override Sprite IconSprite => UpdateIcons.Game("iconPublishingOfferVertrag", UpdateIcons.Store());

	public override float Width => 1000f;

	public override float Height => 780f;

	public override void Build(EditorWindow w)
	{
		if (!Top(w, "Studios ask to sell their PC games on your store and pay your signing fee once. A higher fee means fewer of them ask, but each pays more; studios trust a store with a fair cut and plenty of players."))
		{
			return;
		}
		StoreState s = StoreOps.State;
		Dictionary<int, List<gameScript>> studios = StorePlus.StudiosOnStore();
		float y = 54f;
		w.Label("Signing fee", 24f, y, 150f, 30f, TextAnchor.MiddleLeft, bold: true);
		int index = Mathf.Max(0, Array.IndexOf(StorePlus.FeeSteps, s.SigningFee));
		w.Dropdown(180f, y, 260f, StorePlus.FeeSteps.Select((long f) => (f == 0) ? "Free" : G.Money(f)).ToList(), index, (int v) =>
		{
			UIKit.Sound();
			StoreOps.State.SigningFee = StorePlus.FeeSteps[Mathf.Clamp(v, 0, StorePlus.FeeSteps.Length - 1)];
			GameUpdates.Touch();
		});
		Text line = w.Label(status, 460f, y, w.Width - 474f, 30f, TextAnchor.MiddleLeft, bold: false, null, 13);
		line.color = UIKit.Green;
		y += 40f;
		// Signed studios with PC games out right now (some signed ones have nothing on sale at the moment).
		List<KeyValuePair<int, long>> signed = (from id in studios.Keys
			where StorePlus.IsSigned(id)
			select new KeyValuePair<int, long>(id, studios[id].Sum(StoreOps.CommissionOn)) into kv
			orderby kv.Value descending
			select kv).ToList();
		List<KeyValuePair<string, string>> facts = new List<KeyValuePair<string, string>>
		{
			EditorWindow.KV("Studios signed", StorePlus.StudiosSigned() + " (" + signed.Count + " with games out now)"),
			EditorWindow.KV("Asking now", s.Requests.Count.ToString()),
			EditorWindow.KV("Signing fees so far", G.Money(s.SigningTotal)),
			EditorWindow.KV("Your cut / studio trust", s.Commission + "% / " + Mathf.RoundToInt(s.Trust))
		};
		y += w.Facts(14f, y, w.Width - 28f, facts, 2) + 10f;
		ListView list = w.List(13f, y, w.Width - 26f, w.Height - 75f - y - 10f);
		Group(list, "Asking to sign (" + s.Requests.Count + ")");
		foreach (StudioRequest r in s.Requests.ToList())
		{
			RequestRow(w, list, r, studios);
		}
		if (s.Requests.Count == 0)
		{
			RowView none = list.Row(34f);
			none.Text("No studio is asking right now.", 12f, none.W - 24f);
		}
		Group(list, "Signed, with games out now (" + signed.Count + ")");
		foreach (KeyValuePair<int, long> kv2 in signed)
		{
			StudioRow(w, list, kv2.Key, studios[kv2.Key], signedNow: true, G.Money(kv2.Value) + " a week to you");
		}
		List<int> others = studios.Keys.Where((int id) => !StorePlus.IsSigned(id) && StorePlus.RequestFor(id) == null).ToList();
		Group(list, "Not on your store (" + others.Count + ")");
		foreach (int id2 in others)
		{
			StorePlus.StudioValue(studios[id2], out long _, out long yours);
			StudioRow(w, list, id2, studios[id2], signedNow: false, "would bring about " + G.Money(yours) + " a week");
		}
	}

	private static void Group(ListView list, string text)
	{
		RowView row = list.Row(30f);
		Image back = row.T.GetComponent<Image>();
		if ((bool)back)
		{
			back.color = new Color(0.82f, 0.86f, 0.92f);
		}
		row.Text("<b>" + text + "</b>", 10f, row.W - 20f);
	}

	private void RequestRow(EditorWindow w, ListView list, StudioRequest r, Dictionary<int, List<gameScript>> studios)
	{
		publisherScript p = MmoBots.FindStudio(r.StudioId);
		studios.TryGetValue(r.StudioId, out var games);
		games = games ?? new List<gameScript>();
		StorePlus.StudioValue(games, out long _, out long yours);
		RowView row = list.Row(56f, () => w.Push(new StudioRequestPage(r.StudioId)), "See the request: accept, ask for more, or decline.");
		row.Highlight();
		row.Picture(p ? p.GetLogo() : null, 8f, 42f);
		row.Top(StorePlus.StudioName(r.StudioId) + (r.Big ? "  <color=#1565c0>big studio</color>" : ""), 60f, row.W - 470f);
		row.Bottom(games.Count + ((games.Count == 1) ? " PC game" : " PC games") + "  ·  about " + G.Money(yours) + " a week to you  ·  offers " + ((r.Fee > 0) ? G.Money(r.Fee) : "nothing"), 60f, row.W - 470f);
		StudioRequest req = r;
		row.Button("Sign", row.W - 400f, 120f, () =>
		{
			status = StorePlus.Accept(req);
			UIKit.Sound();
		});
		row.Button("More...", row.W - 272f, 120f, () => w.Push(new StudioRequestPage(req.StudioId)), enabled: true, "Ask for a different fee.");
		row.Button("Decline", row.W - 144f, 120f, () =>
		{
			StorePlus.Decline(req);
			status = StorePlus.StudioName(req.StudioId) + " was turned down.";
		});
	}

	private void StudioRow(EditorWindow w, ListView list, int id, List<gameScript> games, bool signedNow, string right)
	{
		publisherScript p = MmoBots.FindStudio(id);
		RowView row = list.Row(50f, null, p ? p.GetTooltip() : null);
		row.Picture(p ? p.GetLogo() : null, 8f, 36f);
		StoreState s = StoreOps.State;
		string since = ((signedNow && s.Signed.TryGetValue(id, out var deal)) ? ("  ·  signed in " + deal.Week / 48 + ((deal.Fee > 0) ? (" for " + G.Money(deal.Fee)) : "")) : "");
		Text top = row.Top(StorePlus.StudioName(id), 54f, row.W - 470f);
		Text bottom = row.Bottom(games.Count + ((games.Count == 1) ? " PC game" : " PC games") + " on the market" + since, 54f, row.W - 470f);
		row.Text(right, row.W - 460f, 300f, bold: signedNow, TextAnchor.MiddleRight);
		if (signedNow)
		{
			int sid = id;
			row.Button("End the Deal", row.W - 144f, 136f, () =>
			{
				StorePlus.Drop(sid);
				status = StorePlus.StudioName(sid) + "'s games leave your store. Other studios notice (trust -2).";
			}, enabled: true, "Take its games off your store.");
		}
		else
		{
			top.color = UIKit.Grey;
			bottom.color = UIKit.Grey;
		}
	}
}

// A studio asking to sell on your store: who it is, what it would bring, and your answer. Opens as a pop-up for
// big studios (its own window) or from the Studios page.
internal class StudioRequestPage : Page
{
	private readonly int studioId;

	private int counter = -1;

	private string result;

	// True in the pop-up (no Back button, so the content sits 34 higher).
	private readonly bool popup;

	public StudioRequestPage()
	{
		studioId = StudioRequests.Showing;
		popup = true;
	}

	public StudioRequestPage(int studioId, bool popup = false)
	{
		this.studioId = studioId;
		this.popup = popup;
	}

	public override string Title => "A Studio Wants to Sell on " + StoreOps.State.Name;

	public override Sprite IconSprite => UpdateIcons.Game("iconPublishingOfferVertrag", UpdateIcons.Store());

	public override float Width => 860f;

	// Fitted to the content: banner, best sellers, facts, buttons and the note.
	public override float Height => (result != null) ? 300f : (popup ? 575f : 610f);

	private bool Popup(EditorWindow w)
	{
		return w == EditorWindow.StudioRequest;
	}

	public override void Build(EditorWindow w)
	{
		if (!Popup(w))
		{
			BackButton(w);
		}
		StoreState s = StoreOps.State;
		StudioRequest r = StorePlus.RequestFor(studioId);
		publisherScript p = MmoBots.FindStudio(studioId);
		if (result != null || r == null)
		{
			w.Label(result ?? "That request is gone.", 24f, 60f, w.Width - 48f, 80f, TextAnchor.UpperLeft, bold: false, null, 15);
			Next(w, 160f);
			return;
		}
		List<gameScript> games = StorePlus.StudioGames(studioId).OrderByDescending((gameScript x) => CurrentChartsPage.Week(x)).ToList();
		StorePlus.StudioValue(games, out long theirYear, out long yours);
		float o = (Popup(w) ? 0f : 34f);
		RectTransform banner = w.Banner(UpdateIcons.Banner("store"), 14f, 14f + o, w.Width - 28f, 110f, full: true);
		if ((bool)p)
		{
			Image glow = MmoUi.Rect(banner, 12f, -20f, 150f, 150f, new Color(1f, 1f, 1f, 0.35f));
			glow.sprite = SpriteTools.Glow();
			StoreUi.Picture(w.Body, p.GetLogo(), 40f, 26f + o, 86f);
		}
		w.BannerText("<b>" + StorePlus.StudioName(studioId) + "</b>", 150f, 26f + o, w.Width - 180f, 30f, 20, Color.white);
		w.BannerText(games.Count + ((games.Count == 1) ? " PC game" : " PC games") + " on the market" + (p ? ("  ·  " + p.GetStarsAmount() + " stars") : ""), 150f, 60f + o, w.Width - 180f, 24f, 14, new Color(0.84f, 0.88f, 0.94f));
		w.BannerText("wants to sell its games on " + s.Name, 150f, 86f + o, w.Width - 180f, 24f, 14, new Color(1f, 0.82f, 0.36f), bold: true);
		float y = 136f + o;
		w.Header("Its best sellers", 14f, y, w.Width - 28f);
		y += 30f;
		float tw = (w.Width - 28f - 20f) / 3f;
		for (int i = 0; i < 3 && i < games.Count; i++)
		{
			StoreUi.Tile(w, w.Body, games[i], 14f + (float)i * (tw + 10f), y, tw, 150f, MmoUi.Short(CurrentChartsPage.Week(games[i])) + " sold this week");
		}
		if (games.Count == 0)
		{
			w.Label("It has no PC game on the market right now.", 24f, y, w.Width - 48f, 26f);
		}
		y += 160f;
		List<KeyValuePair<string, string>> facts = new List<KeyValuePair<string, string>>
		{
			EditorWindow.KV("It offers", (r.Fee > 0) ? (G.Money(r.Fee) + " (your fee)") : "nothing (your fee is free)"),
			EditorWindow.KV("It would bring you", "about " + G.Money(yours) + " a week (" + s.Commission + "% cut)"),
			EditorWindow.KV("It would make with you", "about " + G.Money(theirYear) + " a year"),
			EditorWindow.KV("Asked", (StoreOps.Year - r.Week / 48 <= 0) ? "this year" : ("in " + r.Week / 48))
		};
		y += w.Facts(14f, y, w.Width - 28f, facts, 2) + 12f;
		w.BigButton("Sign Them" + ((r.Fee > 0) ? (" for " + G.Money(r.Fee)) : ""), 14f, y, 270f, 44f, () =>
		{
			result = StorePlus.Accept(StorePlus.RequestFor(studioId));
			UIKit.Sound();
			w.Rebuild();
		});
		List<long> asks = StorePlus.FeeSteps.Where((long f) => f > r.Fee).ToList();
		if (asks.Count > 0)
		{
			if (counter < 0 || counter >= asks.Count)
			{
				counter = 0;
			}
			w.Dropdown(298f, y + 7f, 200f, asks.Select((long f) => G.Money(f)).ToList(), counter, (int v) =>
			{
				UIKit.Sound();
				counter = v;
				w.Rebuild();
			});
			long ask = asks[counter];
			int chance = Mathf.RoundToInt(StorePlus.AcceptChance(ask, theirYear) * 100f);
			w.SmallButton("Ask for " + G.Money(ask), 508f, y + 7f, 180f, () =>
			{
				result = StorePlus.Counter(StorePlus.RequestFor(studioId), ask);
				w.Rebuild();
			}, enabled: true, "They may pay it, or walk away (about " + chance + "% they pay).", null, 30f);
		}
		w.SmallButton("Decline", w.Width - 158f, y + 7f, 144f, () =>
		{
			StorePlus.Decline(StorePlus.RequestFor(studioId));
			result = StorePlus.StudioName(studioId) + " was turned down. It won't ask again for half a year.";
			w.Rebuild();
		}, enabled: true, null, null, 30f);
		y += 56f;
		w.Label(Popup(w) ? "Close this window to decide later: the request waits on the store's Studios page." : "Requests lapse after about three months.", 24f, y, w.Width - 48f, 24f, TextAnchor.MiddleLeft, bold: false, null, 12).color = UIKit.Grey;
	}

	// After answering: the next big studio waiting, or close / go back.
	private void Next(EditorWindow w, float y)
	{
		StudioRequest next = StorePlus.NextPopup();
		if (Popup(w) && next != null)
		{
			w.BigButton("Next Request", 14f, y, 240f, 44f, () =>
			{
				next.Seen = true;
				w.Replace(new StudioRequestPage(next.StudioId, popup: true));
			});
			w.SmallButton("Later", 268f, y + 7f, 160f, w.Close, enabled: true, null, null, 30f);
			return;
		}
		w.BigButton(Popup(w) ? "Close" : "Back", 14f, y, 240f, 44f, Popup(w) ? new Action(w.Close) : new Action(w.Back));
	}

	public override void Tick(EditorWindow w)
	{
	}
}

// Opens the pop-up for a big studio's request once nothing else is on screen.
internal static class StudioRequests
{
	// The studio whose request the pop-up shows.
	public static int Showing = -1;

	public static void Tick()
	{
		if (!StoreOps.On || !StoreOps.State.Built || !G.Gui || G.Gui.menuOpen || EditorWindow.StudioRequest.IsOpen)
		{
			return;
		}
		StudioRequest next = StorePlus.NextPopup();
		if (next != null)
		{
			next.Seen = true;
			Showing = next.StudioId;
			EditorWindow.StudioRequest.Open();
			Core.Awards.AwardsShow.Sound(50);
		}
	}
}

// The front page: one featured spot and three spotlights, for your games or sold to studios.
internal class StoreFrontPage : StorePlusPage
{
	public override string Title => "Front Page";

	public override float Width => 1040f;

	public override float Height => 760f;

	public override void Build(EditorWindow w)
	{
		if (!Top(w, "What players see first. Put your own games here for more sales, or sell the spots to other studios: they pay by the week and their game sells more through you."))
		{
			return;
		}
		StoreState s = StoreOps.State;
		StorePlus.EnsureSlots();
		List<gameScript> mine = StorePlus.FrontCandidates();
		float bigW = 600f;
		float smallX = 14f + bigW + 14f;
		float smallW = w.Width - smallX - 14f;
		Slot(w, 0, 14f, 56f, bigW, 380f, mine);
		for (int i = 1; i < StorePlus.Slots; i++)
		{
			Slot(w, i, smallX, 56f + (float)(i - 1) * 130f, smallW, 120f, mine);
		}
		float y = 56f + 3f * 130f + 10f;
		w.Toggle("Sell empty spots to studios each month", 24f, y, 460f, s.SellSlots, (bool v) =>
		{
			StoreOps.State.SellSlots = v;
			GameUpdates.Touch();
		}, "Each month, studios may buy the spots you leave empty (more of them when they trust your store).");
		y += 36f;
		List<KeyValuePair<string, string>> facts = new List<KeyValuePair<string, string>>
		{
			EditorWindow.KV("Featured spot", G.Money(StorePlus.SlotPrice(0)) + " a week"),
			EditorWindow.KV("Spotlight", G.Money(StorePlus.SlotPrice(1)) + " a week"),
			EditorWindow.KV("Sold spots last month", G.Money(s.PlacementLast)),
			EditorWindow.KV("All time", G.Money(s.PlacementTotal)),
			EditorWindow.KV("Spots sold now", StorePlus.SoldSlots() + " of " + StorePlus.Slots),
			EditorWindow.KV("Reputation", (StorePlus.SoldSlots() > 0) ? ("-" + (1.5f * (float)StorePlus.SoldSlots()).ToString("0.#") + " (paid spots look like adverts)") : "-")
		};
		w.Facts(14f, y, w.Width - 28f, facts, 2);
	}

	private void Slot(EditorWindow w, int i, float x, float y, float width, float height, List<gameScript> mine)
	{
		StoreState s = StoreOps.State;
		FrontSlot slot = s.FrontPage[i];
		gameScript g = ((slot.Mode != 0) ? GameUpdates.FindGame(slot.GameId) : null);
		string line = ((slot.Mode == 1) ? ("your game: PC sales +" + Mathf.RoundToInt(50f * ((i == 0) ? 1f : 0.5f) * s.Share) + "%") : ((slot.Mode == 2) ? ("sold to " + StorePlus.StudioName(slot.StudioId) + ": " + G.Money(StorePlus.SlotPrice(i)) + " a week") : (s.SellSlots ? "empty: may sell next month" : "empty")));
		float dropH = 34f;
		StoreUi.Tile(w, w.Body, g, x, y, width, height - dropH, StorePlus.SlotNames[i] + "  ·  " + line, slot.Mode == 1);
		if (slot.Mode == 2)
		{
			w.Label("Sold until the end of the month.", x + 4f, y + height - dropH + 4f, width - 8f, 28f, TextAnchor.MiddleLeft, bold: false, null, 12);
			return;
		}
		List<string> options = new List<string> { "Empty" };
		options.AddRange(mine.Select((gameScript m) => m.GetNameSimple()));
		int index = ((slot.Mode == 1) ? (mine.FindIndex((gameScript m) => m.myID == slot.GameId) + 1) : 0);
		int n = i;
		w.Dropdown(x, y + height - dropH + 2f, width, options, Mathf.Max(0, index), (int v) =>
		{
			UIKit.Sound();
			string error = ((v <= 0) ? StorePlus.SetSlot(n, 0, -1) : StorePlus.SetSlot(n, 1, mine[Mathf.Clamp(v - 1, 0, mine.Count - 1)].myID));
			if (error != null)
			{
				CopyProtectDev.Report(error);
			}
		});
	}
}

// Wishlists for your games in development, and early access.
internal class StoreWishlistPage : StorePlusPage
{
	private string status = "";

	private bool statusGood;

	public override string Title => "Wishlists and Early Access";

	public override float Width => 1000f;

	public override float Height => 720f;

	public override void Build(EditorWindow w)
	{
		if (HolyLaunch.On)
		{
			BuildHoly(w);
			return;
		}
		if (!Top(w, "Players wishlist your PC games in development (more with hype), and wishlists make a bigger launch. Early access sells a game before it's done: money and bug reports now, but those players won't buy it again at launch."))
		{
			return;
		}
		StoreState s = StoreOps.State;
		Text line = w.Label(status, 24f, 50f, w.Width - 48f, 22f, TextAnchor.MiddleLeft, bold: false, null, 13);
		line.color = UIKit.Red;
		List<gameScript> dev = StorePlus.InDevelopment();
		w.Header("In development (" + dev.Count + ")", 14f, 76f, w.Width - 28f);
		ListView list = w.List(13f, 106f, w.Width - 26f, 330f);
		foreach (gameScript g in dev)
		{
			StoreGame d = StorePlus.Data(g) ?? new StoreGame();
			string problem = StorePlus.EarlyAccessProblem(g);
			int id = g.myID;
			RowView row = list.Row(60f, null, g.GetTooltip());
			if (d.EarlyAccess)
			{
				row.Highlight();
			}
			row.Top(g.GetNameWithTag(), 12f, row.W - 470f);
			row.Bottom(Mathf.FloorToInt(Mathf.Max(0f, CurrentChartsPage.DevProgress(g, out int _)) * 100f) + "% done  ·  hype " + Mathf.RoundToInt(g.hype) + (d.EarlyAccess ? ("  ·  early access: " + MmoUiText.Num(d.EaUnits) + " players, " + G.Money(d.EaRevenue)) : ""), 12f, row.W - 470f);
			row.Text(MmoUiText.Num(d.Wishlists) + " wishlists", row.W - 450f, 200f, bold: true, TextAnchor.MiddleRight);
			bool on = d.EarlyAccess;
			row.Button(on ? "End Early Access" : "Early Access", row.W - 230f, 220f, () =>
			{
				string error = StorePlus.SetEarlyAccess(GameUpdates.FindGame(id), !on);
				status = error ?? "";
				w.Rebuild();
			}, on || problem == null, on ? "Stop selling it until launch." : (problem ?? ("Sell it now on " + s.Name + " for about " + G.Money(StorePlus.EarlyAccessPrice(g)) + ".")));
		}
		if (dev.Count == 0)
		{
			w.Label("None of your games in development is coming to PC.", 24f, 112f, w.Width - 48f, 26f);
		}
		w.Header("Launched with wishlists", 14f, 450f, w.Width - 28f);
		ListView done = w.List(13f, 480f, w.Width - 26f, w.Height - 75f - 490f);
		foreach (KeyValuePair<int, StoreGame> kv in s.Games.Where((KeyValuePair<int, StoreGame> x) => x.Value.Released).OrderByDescending((KeyValuePair<int, StoreGame> x) => x.Value.LaunchWeek).Take(15))
		{
			gameScript g2 = GameUpdates.FindGame(kv.Key);
			if ((bool)g2)
			{
				StoreGame d2 = kv.Value;
				float boost = StorePlus.LaunchNow(d2);
				RowView row2 = done.Row(40f, null, g2.GetTooltip());
				row2.Text(g2.GetNameWithTag(), 12f, row2.W - 520f, bold: true);
				row2.Text(MmoUiText.Num(d2.Wishlists) + " wishlists" + ((d2.EaUnits > 0) ? (", " + MmoUiText.Num(d2.EaUnits) + " early access") : ""), row2.W - 510f, 300f, bold: false, TextAnchor.MiddleRight);
				row2.Text((boost > 0.001f) ? ("launch boost +" + Mathf.RoundToInt(boost * 100f) + "%") : "launch boost over", row2.W - 200f, 190f, bold: false, TextAnchor.MiddleRight);
			}
		}
	}

	// With Holy's Launch Planner on: Holy's wishlists (your store adds its users' to them) and Holy's early access.
	private void BuildHoly(EditorWindow w)
	{
		if (!Top(w, "Wishlists for your games in development grow with hype, fans and studio level (Holy's Launch Planner), and your store adds its users' wishlists for PC games. At launch they boost sales by up to +120%, fading over 4 weeks."))
		{
			return;
		}
		StoreState s = StoreOps.State;
		Text line = w.Label(status, 24f, 50f, w.Width - 48f, 22f, TextAnchor.MiddleLeft, bold: false, null, 13);
		line.color = statusGood ? UIKit.Grey : UIKit.Red;
		List<gameScript> dev = IndustryCore.MyGamesInDevelopment().Where((gameScript g) => !g.typ_addon && !g.typ_mmoaddon).OrderByDescending(HolyLaunch.Wishlists).ToList();
		roomScript room = HolyLaunch.FreeRoom();
		w.Header("In development (" + dev.Count + ")", 14f, 76f, w.Width - 28f);
		ListView list = w.List(13f, 106f, w.Width - 26f, 330f);
		foreach (gameScript g in dev)
		{
			StoreGame d = StorePlus.Data(g);
			int id = g.myID;
			bool inEa = HolyLaunch.InEarlyAccess(g);
			bool preparing = !inEa && HolyLaunch.Preparing(g);
			RowView row = list.Row(60f, null, g.GetTooltip());
			if (inEa)
			{
				row.Highlight();
			}
			string bottom = Mathf.FloorToInt(HolyLaunch.Progress(g)) + "% done  ·  hype " + Mathf.RoundToInt(g.hype);
			if (d != null && d.Wishlists > 0)
			{
				bottom = bottom + "  ·  " + MmoUiText.Num(d.Wishlists) + " from " + s.Name;
			}
			if (inEa)
			{
				bottom = bottom + "  ·  early access: " + MmoUiText.Num(HolyLaunch.EarlyAccessPlayers(g)) + " players, " + G.Money(HolyLaunch.EarlyAccessRevenue(g));
			}
			else if (preparing)
			{
				bottom += "  ·  preparing early access";
			}
			row.Top(g.GetNameWithTag(), 12f, row.W - 470f);
			row.Bottom(bottom, 12f, row.W - 470f);
			row.Text(MmoUiText.Num(HolyLaunch.Wishlists(g)) + " wishlists", row.W - 450f, 200f, bold: true, TextAnchor.MiddleRight);
			string problem = HolyLaunch.EarlyAccessProblem(g, room);
			string label = (inEa ? "In Early Access" : (preparing ? "Preparing..." : "Start Early Access"));
			string about = "A free Marketing room prepares it (" + Mathf.RoundToInt(HolyLaunch.EarlyAccessWork) + " work). Then players buy the unfinished game every week until launch, for " + G.Money(HolyLaunch.EarlyAccessPrice) + " each, and their feedback adds gameplay points. Stay in early access over a year and they lose interest.";
			row.Button(label, row.W - 230f, 220f, () =>
			{
				status = HolyLaunch.StartEarlyAccess(GameUpdates.FindGame(id), out statusGood);
				w.Rebuild();
			}, problem == null, problem ?? about);
		}
		if (dev.Count == 0)
		{
			w.Label("You have no games in development.", 24f, 112f, w.Width - 48f, 26f);
		}
		w.Header("Launched with wishlists", 14f, 450f, w.Width - 28f);
		ListView done = w.List(13f, 480f, w.Width - 26f, w.Height - 75f - 490f);
		foreach (KeyValuePair<gameScript, object> kv in HolyLaunch.Launched(15))
		{
			gameScript g2 = kv.Key;
			long wish = HolyCompat.Get<long>(kv.Value, "Wishlists");
			long ea = HolyCompat.Get<long>(kv.Value, "EaPlayers");
			float boost = HolyLaunch.BoostNow(kv.Value);
			RowView row2 = done.Row(40f, null, g2.GetTooltip());
			row2.Text(g2.GetNameWithTag(), 12f, row2.W - 520f, bold: true);
			row2.Text(MmoUiText.Num(wish) + " wishlists" + ((ea > 0) ? (", " + MmoUiText.Num(ea) + " early access") : ""), row2.W - 510f, 300f, bold: false, TextAnchor.MiddleRight);
			row2.Text((boost > 0.001f) ? ("launch boost +" + Mathf.RoundToInt(boost * 100f) + "%") : "launch boost over", row2.W - 200f, 190f, bold: false, TextAnchor.MiddleRight);
		}
	}
}

// Your MMOs in the launcher.
internal class StoreLauncherPage : StorePlusPage
{
	public override string Title => "MMO Launcher";

	public override Sprite IconSprite => UpdateIcons.Mmo();

	public override float Width => 960f;

	public override float Height => 620f;

	public override void Build(EditorWindow w)
	{
		if (!Top(w, "Your MMOs start from your store's launcher: their PC players get an account with you (and see what else you sell), and players browsing your store find your MMOs."))
		{
			return;
		}
		StoreState s = StoreOps.State;
		List<KeyValuePair<string, string>> facts = new List<KeyValuePair<string, string>>
		{
			EditorWindow.KV("Accounts from MMO players", MmoUi.Short(s.LauncherAccounts)),
			EditorWindow.KV("Store accounts", MmoUi.Short(s.Accounts))
		};
		float y = 54f + w.Facts(14f, 54f, w.Width - 28f, facts, 2) + 10f;
		List<gameScript> mmos = MmoOps.MyMmos();
		w.Header("Your MMOs (" + mmos.Count + ")", 14f, y, w.Width - 28f);
		y += 32f;
		ListView list = w.List(13f, y, w.Width - 26f, w.Height - 75f - y - 10f);
		foreach (gameScript g in mmos)
		{
			float pc = StoreOps.PcFraction(g);
			bool on = StorePlus.InLauncher(g);
			int id = g.myID;
			RowView row = list.Row(56f, null, g.GetTooltip());
			if (on)
			{
				row.Highlight();
			}
			row.Top(g.GetNameWithTag(), 12f, row.W - 420f);
			row.Bottom((pc <= 0f) ? "Not on PC: it can't use the launcher." : (MmoUi.Short(g.abonnements) + " subscribers  ·  " + StoreUi.Pct(pc) + " of its market on PC" + (on ? ("  ·  new players +" + Mathf.RoundToInt((StorePlus.MmoFactor(g) - 1f) * 100f) + "%") : "")), 12f, row.W - 420f);
			row.Button(on ? "Leave the Launcher" : "Use the Launcher", row.W - 230f, 220f, () =>
			{
				gameScript game = GameUpdates.FindGame(id);
				if ((bool)game)
				{
					StorePlus.SetLauncher(game, !on);
				}
			}, pc > 0f, on ? "Players launch it on its own again." : "Its PC players start it from your store.");
		}
		if (mmos.Count == 0)
		{
			w.Label("You have no MMO on the market.", 24f, y + 6f, w.Width - 48f, 26f);
		}
	}
}

// Storefronts on your own consoles.
internal class StoreConsolesPage : StorePlusPage
{
	private int roomId = MmoUi.PreferredRoom;

	private string status = "";

	public override string Title => "Console Storefronts";

	public override float Width => 980f;

	public override float Height => 680f;

	public override void Build(EditorWindow w)
	{
		if (!Top(w, "Build your store into the consoles you make. Players buy and download games right on the console, and other studios' digital sales on it pay you " + StorePlus.ConsoleCommission + "% (fewer console players bought digitally in the early years)."))
		{
			return;
		}
		StoreState s = StoreOps.State;
		Text line = w.Label(status, 24f, 50f, w.Width - 48f, 22f, TextAnchor.MiddleLeft, bold: false, null, 13);
		line.color = UIKit.Red;
		if (StoreOps.Year < StorePlus.ConsoleYear)
		{
			w.Info("Console storefronts become possible in " + StorePlus.ConsoleYear + ", when consoles go online for good.", 14f, 80f, w.Width - 28f, 56f);
			return;
		}
		List<platformScript> consoles = StorePlus.MyConsoles();
		UpdateProject running = StoreOps.Running();
		float y = 80f;
		w.Label("Room", 24f, y, 110f, 30f, TextAnchor.MiddleLeft, bold: true);
		roomId = MmoUi.RoomDropdown(w, 140f, y, w.Width - 154f, roomId, 220f, UpdateKind.StoreConsole, (int v) =>
		{
			roomId = v;
			w.Rebuild();
		});
		y += 44f;
		List<KeyValuePair<string, string>> facts = new List<KeyValuePair<string, string>>
		{
			EditorWindow.KV("Commission last month", G.Money(s.ConsoleLast)),
			EditorWindow.KV("All time", G.Money(s.ConsoleTotal)),
			EditorWindow.KV("Console players buying digitally", StoreUi.Pct(StorePlus.ConsoleDigital())),
			EditorWindow.KV("In development", (running != null && running.Kind == UpdateKind.StoreConsole) ? (running.Title + " " + Mathf.FloorToInt(running.Fraction * 100f) + "%") : "-")
		};
		y += w.Facts(14f, y, w.Width - 28f, facts, 2) + 10f;
		w.Header("Your consoles (" + consoles.Count + ")", 14f, y, w.Width - 28f);
		y += 32f;
		ListView list = w.List(13f, y, w.Width - 26f, w.Height - 75f - y - 10f);
		foreach (platformScript p in consoles)
		{
			ConsoleStore c = StorePlus.ConsoleStoreFor(p.myID);
			RowView row = list.Row(56f);
			Image pic = new GameObject("Platform", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
			pic.transform.SetParent(row.T, worldPositionStays: false);
			UIKit.Place(pic.gameObject, 8f, 6f, 44f, 44f);
			pic.preserveAspect = true;
			pic.raycastTarget = false;
			try
			{
				p.SetPic(pic.gameObject);
			}
			catch
			{
				pic.enabled = false;
			}
			row.Top(p.GetName(), 62f, row.W - 400f);
			if (c != null)
			{
				row.Highlight();
				row.Bottom("Storefront since " + c.BuiltWeek / 48 + "  ·  about " + G.Money(StorePlus.ConsoleCommissionWeek(p)) + " a week now  ·  " + G.Money(c.CommissionTotal) + " in all", 62f, row.W - 140f);
				continue;
			}
			UpdatePlan plan = StorePlus.PlanConsole(p);
			row.Bottom(plan.Problem ?? (p.GetMarktanteil().ToString("0.0") + "% of the market  ·  " + G.Money(plan.Cost)), 62f, row.W - 400f);
			int pid = p.myID;
			row.Button("Build for " + G.Money(plan.Cost), row.W - 250f, 240f, () =>
			{
				platformScript target = GameUpdates.Platform(pid);
				UpdateProject proj = new UpdateProject
				{
					Kind = UpdateKind.StoreConsole,
					PlatformId = pid,
					Title = "Console storefront: " + (target ? target.GetName() : "?")
				};
				string error = GameUpdates.Start(proj, StorePlus.PlanConsole(target), CopyProtectDev.FindRoom(roomId), null);
				status = error ?? "";
				if (error == null)
				{
					UIKit.Sound();
					MmoUi.PreferredRoom = -1;
				}
				w.Rebuild();
			}, plan.Problem == null && running == null, plan.Problem ?? ((running != null) ? ("Your team is already working on " + running.Title + ".") : "Build it in the room picked above."));
		}
		if (consoles.Count == 0)
		{
			w.Label("You have no console of your own on the market.", 24f, y + 6f, w.Width - 48f, 26f);
		}
	}
}
