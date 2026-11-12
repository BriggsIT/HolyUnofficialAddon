using System;
using System.Collections.Generic;
using System.Linq;
using HolyAddon.Core;
using HolyAddon.Core.Events;
using HolyAddon.Core.Mmo;
using HolyAddon.Core.Updates;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI.Pages;

internal static class ConUi
{
	public static Color Accent => EditorWindow.ConventionTitle()?.Key ?? EditorWindow.ConventionFallback;

	public static Sprite Icon => UpdateIcons.Game("iconMadGamesConvention", UpdateIcons.FanFest());

	// The convention's banner with your company logo on a glow in its colour.
	public static void Banner(EditorWindow w, float y, string title, string line1, string line2, Color line2Color)
	{
		float W = w.Width;
		RectTransform banner = w.Banner(UpdateIcons.Banner("convention"), 14f, y, W - 28f, 116f, full: true);
		Sprite logo = (((bool)G.Gui && (bool)G.MS) ? G.Gui.GetCompanyLogo(G.MS.GetCompanyLogoID()) : null);
		if ((bool)logo)
		{
			Color c = Accent;
			Image glow = MmoUi.Rect(banner, -30f, -32f, 180f, 180f, new Color(c.r, c.g, c.b, 0.75f));
			glow.sprite = SpriteTools.Glow();
			StoreUi.Picture(w.Body, logo, 34f, y + 20f, 76f);
		}
		float x = ((bool)logo ? 134f : 30f);
		w.BannerText("<b>" + title + "</b>", x, y + 12f, W - x - 300f, 32f, 21, Color.white);
		w.BannerText(line1, x, y + 48f, W - x - 300f, 24f, 14, new Color(0.84f, 0.88f, 0.94f));
		w.BannerText(line2, x, y + 78f, W - x - 300f, 24f, 14, line2Color, bold: true);
	}

	public static string Weeks(int n)
	{
		if (n == 1)
		{
			return "1 week";
		}
		return n + " weeks";
	}

	public static string Pct(float f)
	{
		return Mathf.RoundToInt(Mathf.Clamp01(f) * 100f) + "%";
	}
}

// The yearly convention: venue, what's on stage, extras, who prepares it, and what it should bring.
internal class ConventionPage : Page
{
	// The Support room the planner was opened from (its button), picked first to prepare it.
	public static roomScript FromRoom;

	private readonly bool pushed;

	private int seen = -1;

	private int pick = -1;

	private string status = "";

	private bool statusGood;

	public ConventionPage(bool pushed = false)
	{
		this.pushed = pushed;
	}

	public static Page Root()
	{
		ConRecord r = StudioCon.ResultPending;
		if (r != null)
		{
			StudioCon.ResultPending = null;
			return new ConventionResultPage(r);
		}
		return new ConventionPage();
	}

	public override string Title => StudioCon.FullName(StudioCon.PlanYear);

	public override Sprite IconSprite => ConUi.Icon;

	public override float Width => 1040f;

	public override float Height => 940f;

	public override void Build(EditorWindow w)
	{
		seen = GameUpdates.Version;
		StudioCon.Ready();
		float W = w.Width;
		float o = 0f;
		if (pushed)
		{
			BackButton(w);
			w.MoneyLine();
			o = 44f;
		}
		ConState s = StudioCon.State;
		string locked = StudioCon.LockReason();
		ConPlan p = StudioCon.Plan;
		int weeks = StudioCon.WeeksToDoors(p.Year);
		float now = StudioCon.Readiness(p, out object task);
		string line1 = "Doors open in August, week " + StudioCon.DayWeek + ((p.Year != (G.MS ? G.MS.year : 0)) ? (" " + p.Year) : "") + "  ·  in " + ConUi.Weeks(Mathf.Max(0, weeks));
		int streak = Streak();
		string line2 = ((locked != null) ? "Not yet: see below" : ((streak > 1) ? ("Held " + streak + " years running: fans expect it") : ((streak == 1) ? "Held last year: fans expect it again" : "Your own convention for all your games")));
		ConUi.Banner(w, 14f + o, StudioCon.FullName(p.Year), line1, line2, new Color(1f, 0.8f, 0.3f));
		float y = 140f + o;
		if (locked != null)
		{
			w.Info(locked, 14f, y, W - 28f, 60f);
			w.SmallButton("Past Conventions...", 14f, y + 76f, 220f, () => w.Push(new ConventionHistoryPage()), enabled: true, null, null, 30f);
			return;
		}
		StudioCon.FillSpots(p, fill: false);
		bool started = p.Started;
		// Venue.
		w.Header("Venue", 14f, y, W - 28f);
		y += 32f;
		float cw = (W - 28f - 30f) / 4f;
		for (int i = 0; i < 4; i++)
		{
			int v = i;
			string seats = ((StudioCon.VenueSeats[i] > 0) ? (MmoUiText.Num(StudioCon.VenueSeats[i]) + " seats") : "Online only");
			SaleUi.Card(w, 14f + (float)i * (cw + 10f), y, cw, 92f, StudioCon.VenueNames[i], seats + "  ·  " + G.Money(StudioCon.VenuePrice(i)), StudioCon.Slots(i) + ((StudioCon.Slots(i) == 1) ? " spot" : " spots") + " on stage  ·  " + StudioCon.VenueWork[i] + " work", p.Venue == i, () =>
			{
				ConPlan cp = StudioCon.Plan;
				cp.Venue = v;
				StudioCon.FillSpots(cp);
				GameUpdates.Touch();
			}, !started, started ? "The venue is booked." : ("Prepared by a Support room: " + StudioCon.VenueWork[i] + " work points."), ConUi.Accent);
		}
		y += 104f;
		// The stage.
		List<ConSpot> cands = StudioCon.Candidates();
		float stageHype = StudioCon.Outcome(p, started ? StudioCon.ReadyOnTheDay(p) : 1f, apply: false).StageHype;
		w.Header("On stage (" + p.Spots.Count + ((p.Spots.Count == 1) ? " spot)" : " spots)") + ": any of your games, and your consoles", 14f, y, W - 28f);
		y += 32f;
		List<string> names = new List<string> { "Empty" };
		names.AddRange(cands.Select(StudioCon.SpotName));
		float half = (W - 28f - 10f) / 2f;
		for (int i2 = 0; i2 < p.Spots.Count; i2++)
		{
			int n = i2;
			float x = 14f + (float)(i2 % 2) * (half + 10f);
			float sy = y + (float)(i2 / 2) * 46f;
			ConSpot spot = p.Spots[i2];
			int index = cands.FindIndex((ConSpot c) => StudioCon.Same(c, spot)) + 1;
			List<string> second = StudioCon.AnnounceOptions(spot, stageHype);
			bool extra = second != null && second.Count > 1;
			float second_w = ((spot.Kind == ConSpot.Released) ? 250f : 190f);
			w.Label("Spot " + (i2 + 1), x, sy, 60f, 34f, TextAnchor.MiddleLeft, bold: true);
			w.Dropdown(x + 62f, sy + 2f, extra ? (half - 62f - second_w - 6f) : (half - 62f), names, Mathf.Max(0, index), (int v2) =>
			{
				UIKit.Sound();
				Choose(n, (v2 <= 0) ? null : cands[Mathf.Clamp(v2 - 1, 0, cands.Count - 1)]);
			});
			if (extra)
			{
				w.Dropdown(x + half - second_w, sy + 2f, second_w, second, Mathf.Clamp(spot.Announce, 0, second.Count - 1), (int v3) =>
				{
					UIKit.Sound();
					StudioCon.Plan.Spots[n].Announce = v3;
					GameUpdates.Touch();
				});
			}
			else if (second != null && second.Count == 1)
			{
				// One choice only (a classic, or a game with no series): say what it does.
				w.Label(second[0], x + 62f, sy + 32f, half - 62f, 14f, TextAnchor.MiddleLeft, bold: false, null, 10).color = UIKit.Grey;
			}
		}
		y += (float)((p.Spots.Count + 1) / 2) * 46f + 4f;
		// Extras.
		w.Header("Extras", 14f, y, W - 28f);
		y += 32f;
		bool hasMmo = StudioCon.MmosOnStage(p).Count > 0;
		int booths = StudioCon.BoothCount[p.Venue];
		Toggle t1 = w.Toggle("Merch store (" + G.Money(StudioCon.MerchPrice) + ")", 24f, y, half - 20f, p.Merch, (bool v4) =>
		{
			StudioCon.Plan.Merch = v4;
			GameUpdates.Touch();
		}, "Sell shirts, figures and art books to visitors and stream viewers (Merchandise income).");
		Toggle t2 = w.Toggle("Developer panels (" + G.Money(StudioCon.PanelsPrice) + ")", 24f + half + 10f, y, half - 20f, p.Panels, (bool v5) =>
		{
			StudioCon.Plan.Panels = v5;
			GameUpdates.Touch();
		}, "Your developers talk about their games: +2 hype for games in development, better user reviews for MMOs on stage, +1 studio point.");
		y += 34f;
		Toggle t3 = w.Toggle("Gifts for lapsed MMO players (" + (hasMmo ? G.Money(StudioCon.GiftsPrice(p)) : "needs an MMO on stage") + ")", 24f, y, half - 20f, p.Gifts && hasMmo, (bool v6) =>
		{
			StudioCon.Plan.Gifts = v6;
			GameUpdates.Touch();
		}, "In-game gifts for everyone who watches: some former players of the MMOs on stage come back.");
		Toggle t4 = w.Toggle((booths > 0) ? ("Booths for " + booths + " other studios (they pay)") : "Booths for other studios (not online)", 24f + half + 10f, y, half - 20f, p.Booths && booths > 0, (bool v7) =>
		{
			StudioCon.Plan.Booths = v7;
			GameUpdates.Touch();
		}, "Other studios pay for a booth to show their games, and like you more for it (relation +3). The crowd's attention is shared: your stage gets 10% less hype.");
		t1.interactable = !started;
		t2.interactable = !started;
		t3.interactable = !started && hasMmo;
		t4.interactable = !started && booths > 0;
		y += 42f;
		// Who prepares it.
		float readyOnDay;
		roomScript room = null;
		bool agency = false;
		if (!started)
		{
			w.Header("Who prepares it", 14f, y, W - 28f);
			y += 32f;
			List<roomScript> rooms = StudioCon_Rooms();
			int work = StudioCon.VenueWork[p.Venue];
			List<string> opts = rooms.Select((roomScript r) => Core.Events.HolyRooms.RoomName(r) + "  ·  " + Core.Events.HolyRooms.StaffCount(r) + " staff  ·  " + ((r.taskID == -1) ? Core.Events.HolyRooms.Duration(work, r) : "busy with other work")).ToList();
			opts.Add("Event agency: " + Mathf.RoundToInt(StudioCon.AgencyExtra * 100f) + "% more, no room needed, about " + ConUi.Pct(StudioCon.AgencyReady) + " ready");
			if (pick < 0 || pick >= opts.Count)
			{
				pick = DefaultPick(rooms, p);
			}
			w.Dropdown(14f, y, W - 28f, opts, pick, (int v8) =>
			{
				UIKit.Sound();
				pick = v8;
				w.Rebuild();
			});
			agency = pick >= rooms.Count;
			room = (agency ? null : rooms[pick]);
			readyOnDay = (agency ? StudioCon.AgencyReady : ((room && room.taskID == -1) ? Mathf.Clamp01(Core.Events.HolyRooms.Rate(room) * (float)Mathf.Max(0, weeks) / (float)Mathf.Max(1, work)) : 0f));
			y += 40f;
		}
		else
		{
			readyOnDay = StudioCon.ReadyOnTheDay(p);
			w.Header("Preparing", 14f, y, W - 28f);
			y += 32f;
			string prep;
			if (p.Agency)
			{
				prep = "An event agency runs it (about " + ConUi.Pct(StudioCon.AgencyReady) + " ready).";
			}
			else if (p.Prepared)
			{
				prep = "Ready: the room has finished and is back to its normal work.";
			}
			else if (task != null)
			{
				roomScript r2 = Core.Events.HolyRooms.RoomOf(task);
				prep = Core.Events.HolyRooms.RoomName(r2) + " is preparing it: " + ConUi.Pct(now) + " done. At its pace it'll be about " + ConUi.Pct(readyOnDay) + " ready when the doors open.";
			}
			else
			{
				prep = "No room is preparing it any more.";
			}
			w.Label(prep, 24f, y, W - 48f - 200f, 30f);
			MmoUi.Rect(w.Body, W - 214f, y + 8f, 200f, 14f, new Color(0f, 0f, 0f, 0.25f));
			MmoUi.Rect(w.Body, W - 213f, y + 9f, Mathf.Max(2f, 198f * now), 12f, (now >= 0.999f) ? UIKit.Green : UIKit.Blue);
			y += 40f;
		}
		// What it should bring.
		ConOutcome est = StudioCon.Outcome(p, readyOnDay, apply: false);
		long cost = (started ? p.Paid : StudioCon.Cost(p, agency));
		List<KeyValuePair<string, string>> facts = new List<KeyValuePair<string, string>>
		{
			EditorWindow.KV("Visitors", (p.Venue == 0) ? "online only" : (MmoUiText.Num(est.Visitors) + " (" + MmoUiText.Num(est.Demand) + " want tickets" + (est.SoldOut ? ", sold out)" : ")"))),
			EditorWindow.KV("Stream viewers", MmoUiText.Num(est.Viewers)),
			EditorWindow.KV("Stage hype per spot", "+" + Mathf.RoundToInt(est.StageHype) + " (premieres +" + Mathf.RoundToInt(StudioCon.PremiereHype) + ")"),
			EditorWindow.KV("New fans", MmoUiText.Num(est.Fans)),
			EditorWindow.KV("Tickets / merch / booths", G.Money(est.Tickets) + " / " + G.Money(est.Merch) + " / " + G.Money(est.Booths)),
			EditorWindow.KV(started ? "Paid" : "Cost", G.Money(cost)),
			EditorWindow.KV("Studio points", ((est.Points > 0) ? "+" : "") + est.Points),
			EditorWindow.KV("Ready when the doors open", "about " + ConUi.Pct(readyOnDay) + ((readyOnDay < 0.7f) ? " (queues and complaints)" : ""))
		};
		y += w.Facts(14f, y, W - 28f, facts, 2) + 12f;
		if (!started)
		{
			roomScript chosen = room;
			bool byAgency = agency;
			string problem = StudioCon.StartProblem(p, chosen, byAgency);
			w.BigButton("Start Preparing (" + G.Money(cost) + ")", 14f, y, 320f, 44f, () =>
			{
				string error = StudioCon.Start(StudioCon.Plan, chosen, byAgency);
				statusGood = error == null;
				status = error ?? (StudioCon.FullName(StudioCon.Plan.Year) + " is booked" + (byAgency ? " with an event agency." : (": " + Core.Events.HolyRooms.RoomName(chosen) + " starts preparing it.")));
				w.Rebuild();
			}, problem == null, problem);
		}
		else
		{
			w.SmallButton("Call It Off", 14f, y + 7f, 200f, () =>
			{
				status = StudioCon.CallOff(StudioCon.Plan) ?? "";
				statusGood = true;
				w.Rebuild();
			}, enabled: true, p.Agency ? "Half of what you paid comes back." : (p.Prepared ? "It's ready: nothing comes back." : "The share not prepared yet comes back (as for any room's work)."), null, 30f);
		}
		w.SmallButton("Past Conventions...", started ? 228f : 348f, y + 7f, 200f, () => w.Push(new ConventionHistoryPage()), enabled: true, null, null, 30f);
		Text line = w.Label(status, started ? 442f : 562f, y, W - (started ? 456f : 576f), 44f, TextAnchor.MiddleLeft, bold: false, null, 13);
		line.color = (statusGood ? UIKit.Green : UIKit.Red);
	}

	private static List<roomScript> StudioCon_Rooms()
	{
		if (!Core.Events.HolyRooms.Available)
		{
			return new List<roomScript>();
		}
		return Core.Events.HolyRooms.SupportRooms();
	}

	// The room it was opened from, else last year's, else the fastest free one, else the agency.
	private static int DefaultPick(List<roomScript> rooms, ConPlan p)
	{
		int i = (FromRoom ? rooms.FindIndex((roomScript r) => r == FromRoom) : (-1));
		if (i >= 0)
		{
			return i;
		}
		if (p.Agency && p.RoomId < 0)
		{
			return rooms.Count;
		}
		i = rooms.FindIndex((roomScript r) => r.myID == p.RoomId && r.taskID == -1);
		if (i >= 0)
		{
			return i;
		}
		i = rooms.FindIndex((roomScript r) => r.taskID == -1 && Core.Events.HolyRooms.StaffCount(r) > 0);
		if (i < 0)
		{
			return rooms.Count;
		}
		return i;
	}

	// Puts something on a spot; if it was on another spot, the two swap.
	private static void Choose(int n, ConSpot c)
	{
		ConPlan p = StudioCon.Plan;
		if (n < 0 || n >= p.Spots.Count)
		{
			return;
		}
		ConSpot old = p.Spots[n];
		if (c == null)
		{
			p.Spots[n] = new ConSpot();
		}
		else
		{
			int other = p.Spots.FindIndex((ConSpot s) => StudioCon.Same(s, c));
			if (other >= 0 && other != n)
			{
				p.Spots[other] = old;
			}
			p.Spots[n] = new ConSpot
			{
				Kind = c.Kind,
				Id = c.Id
			};
		}
		GameUpdates.Touch();
	}

	// Years in a row a convention was held, up to last year.
	private static int Streak()
	{
		ConState s = StudioCon.State;
		int year = (G.MS ? G.MS.year : 0);
		HashSet<int> held = new HashSet<int>(s.History.Select((ConRecord r) => r.Year));
		int y = ((s.HeldYear == year) ? year : (year - 1));
		int n = 0;
		while (held.Contains(y))
		{
			n++;
			y--;
		}
		return n;
	}

	public override void Tick(EditorWindow w)
	{
		if (GameUpdates.Version != seen)
		{
			w.Rebuild();
		}
	}
}

// How this year's convention went.
internal class ConventionResultPage : Page
{
	private readonly ConRecord r;

	public ConventionResultPage(ConRecord r)
	{
		this.r = r;
	}

	public override string Title => r.Name;

	public override Sprite IconSprite => ConUi.Icon;

	public override float Width => 960f;

	public override float Height => 780f;

	public override void Build(EditorWindow w)
	{
		float W = w.Width;
		string where = "Held " + StudioCon.VenuePlaces[Mathf.Clamp(r.Venue, 0, 3)] + ((r.Venue > 0) ? (" for " + MmoUiText.Num(r.Visitors) + " fans") : "") + "  ·  " + MmoUiText.Num(r.Viewers) + " watched the stream";
		string mood = ((r.Ready >= 0.95f) ? "Everything ran smoothly" : ((r.Ready >= 0.7f) ? ("Mostly smooth: " + ConUi.Pct(r.Ready) + " prepared") : ((r.Ready >= 0.4f) ? ("Queues and complaints: only " + ConUi.Pct(r.Ready) + " prepared") : ("Chaos at the doors: only " + ConUi.Pct(r.Ready) + " prepared"))));
		ConUi.Banner(w, 14f, r.Name + (r.SoldOut ? "  ·  SOLD OUT" : ""), where, mood, (r.Ready >= 0.7f) ? new Color(1f, 0.8f, 0.3f) : new Color(1f, 0.55f, 0.45f));
		float y = 142f;
		float cw = (W - 28f - 30f) / 4f;
		w.StatCard(UpdateIcons.Game("IconFans", UpdateIcons.Game("iconPlayer")), "Visitors", (r.Venue > 0) ? MmoUiText.Num(r.Visitors) : "-", (r.Venue > 0) ? (MmoUiText.Num(r.Demand) + " wanted tickets") : "Online only", 14f, y, cw, 86f);
		w.StatCard(UpdateIcons.Game("iconTotalPlatformUser", UpdateIcons.Game("iconPlayer")), "Stream viewers", MmoUiText.Num(r.Viewers), "", 14f + cw + 10f, y, cw, 86f);
		w.StatCard(UpdateIcons.Game("iconHype", ConUi.Icon), "New fans", "+" + MmoUiText.Num(r.Fans), "", 14f + 2f * (cw + 10f), y, cw, 86f);
		w.StatCard(UpdateIcons.Game("iconStar", ConUi.Icon), "Studio points", ((r.Points > 0) ? "+" : "") + r.Points, (r.Points < 0) ? "The queues hurt" : "", 14f + 3f * (cw + 10f), y, cw, 86f, (r.Points < 0) ? new Color?(UIKit.Red) : null);
		y += 100f;
		w.Header("On stage", 14f, y, W - 28f);
		y += 32f;
		ListView list = w.List(13f, y, W - 26f, 230f);
		foreach (string line in r.Lines)
		{
			RowView row = list.Row(34f);
			row.Text(line, 12f, row.W - 24f);
		}
		if (r.Lines.Count == 0)
		{
			RowView none = list.Row(34f);
			none.Text("Nothing was shown.", 12f, none.W - 24f);
		}
		y += 242f;
		long income = r.Tickets + r.Merch + r.Booths;
		List<KeyValuePair<string, string>> facts = new List<KeyValuePair<string, string>>
		{
			EditorWindow.KV("Tickets", G.Money(r.Tickets)),
			EditorWindow.KV("Merch", G.Money(r.Merch)),
			EditorWindow.KV("Booths", G.Money(r.Booths)),
			EditorWindow.KV("Cost", G.Money(r.Cost)),
			EditorWindow.KV("Money in", G.Money(income)),
			EditorWindow.KV("Balance", G.Money(income - r.Cost))
		};
		y += w.Facts(14f, y, W - 28f, facts, 2) + 14f;
		w.BigButton("Close", 14f, y, 220f, 44f, w.Close);
		w.SmallButton("Plan Next Year's...", 248f, y + 7f, 220f, () => w.Replace(new ConventionPage()), enabled: true, null, null, 30f);
	}
}

// Every convention so far: this addon's, Holy's before it, and the MMO FanFests.
internal class ConventionHistoryPage : Page
{
	public override string Title => "Past Conventions";

	public override Sprite IconSprite => ConUi.Icon;

	public override float Width => 900f;

	public override float Height => 700f;

	public override void Build(EditorWindow w)
	{
		BackButton(w);
		List<KeyValuePair<int, KeyValuePair<string, string>>> rows = new List<KeyValuePair<int, KeyValuePair<string, string>>>();
		foreach (ConRecord r in StudioCon.State.History)
		{
			string top = r.Name + "  ·  " + StudioCon.VenueNames[Mathf.Clamp(r.Venue, 0, 3)] + ((r.From == "holy") ? " (The Holy Expansion)" : "");
			string bottom = ((r.From == "holy") ? (MmoUiText.Num(r.Visitors) + " visitors, " + G.Money(r.Tickets) + " after costs") : (MmoUiText.Num(r.Visitors) + " visitors" + (r.SoldOut ? " (sold out)" : "") + ", " + MmoUiText.Num(r.Viewers) + " on the stream, +" + MmoUiText.Num(r.Fans) + " fans, " + ConUi.Pct(r.Ready) + " prepared, balance " + G.Money(r.Tickets + r.Merch + r.Booths - r.Cost)));
			rows.Add(new KeyValuePair<int, KeyValuePair<string, string>>(r.Year, new KeyValuePair<string, string>(top, bottom)));
		}
		MmoState mmo = MmoOps.State;
		if (mmo?.FanFests != null)
		{
			foreach (FanFestRecord f in mmo.FanFests)
			{
				rows.Add(new KeyValuePair<int, KeyValuePair<string, string>>(f.Year, new KeyValuePair<string, string>("FanFest " + f.Year + "  ·  " + MmoFanFest.VenueNames[Mathf.Clamp(f.Venue, 0, MmoFanFest.VenueNames.Length - 1)] + " (MMO FanFest)", f.Game + ", cost " + G.Money(f.Cost) + ", merch " + G.Money(f.Income))));
			}
		}
		ListView list = w.List(13f, 54f, w.Width - 26f, w.Height - 75f - 64f);
		foreach (KeyValuePair<int, KeyValuePair<string, string>> kv in rows.OrderByDescending((KeyValuePair<int, KeyValuePair<string, string>> x) => x.Key))
		{
			RowView row = list.Row(46f);
			row.Top(kv.Value.Key, 12f, row.W - 24f);
			row.Bottom(kv.Value.Value, 12f, row.W - 24f);
		}
		if (rows.Count == 0)
		{
			RowView none = list.Row(34f);
			none.Text("None yet.", 12f, none.W - 24f);
		}
	}
}
