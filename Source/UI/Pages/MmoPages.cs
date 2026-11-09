using System;
using System.Collections.Generic;
using System.Linq;
using HolyAddon.Core;
using HolyAddon.Core.CopyProtection;
using HolyAddon.Core.Mmo;
using HolyAddon.Core.Updates;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI.Pages;

// Bits shared by the MMO pages.
internal static class MmoUi
{
	// The room picked in a development room's menu (MMO Management), used first when it's free.
	public static int PreferredRoom = -1;

	public static Image Shot(EditorWindow w, gameScript g, float x, float y, float width, float height, Transform parent = null)
	{
		Image shot = new GameObject("Shot", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
		shot.transform.SetParent(parent ?? w.Body, worldPositionStays: false);
		UIKit.Place(shot.gameObject, x, y, width, height);
		shot.sprite = g.GetScreenshot();
		shot.preserveAspect = true;
		return shot;
	}

	public static Image Rect(Transform parent, float x, float y, float width, float height, Color color)
	{
		Image img = new GameObject("Rect", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
		img.transform.SetParent(parent, worldPositionStays: false);
		UIKit.Place(img.gameObject, x, y, width, height);
		img.color = color;
		img.raycastTarget = false;
		return img;
	}

	public static string Short(long n)
	{
		long a = Math.Abs(n);
		string s = (a >= 1000000) ? ((a / 1000000f).ToString((a >= 10000000) ? "0.0" : "0.00") + "M") : ((a >= 10000) ? ((a / 1000f).ToString("0") + "k") : a.ToString("N0"));
		return ((n < 0) ? "-" : "") + s;
	}

	public static string Years(float years)
	{
		if (years < 1f)
		{
			int weeks = Mathf.RoundToInt(years * 52f);
			return (weeks == 1) ? "1 week" : (weeks + " weeks");
		}
		float y = Mathf.Round(years * 10f) / 10f;
		return y.ToString("0.#") + ((y == 1f) ? " year" : " years");
	}

	public static string Genre(gameScript g)
	{
		genres gen = (G.MainObj ? G.MainObj.GetComponent<genres>() : null);
		if (!gen || g.maingenre < 0)
		{
			return "";
		}
		return gen.GetName(g.maingenre);
	}

	public static roomScript PickRoom(int wanted)
	{
		List<roomScript> free = CopyProtectDev.FreeRooms().Where((roomScript r) => CopyProtectDev.Staff(r).Count > 0).ToList();
		return free.FirstOrDefault((roomScript r) => r.myID == wanted) ?? free.FirstOrDefault();
	}

	// A dropdown of free development rooms with how long the work takes in each; returns the chosen room ID (or -1).
	public static int RoomDropdown(EditorWindow w, float x, float y, float width, int selected, float work, UpdateKind kind, Action<int> onChange)
	{
		List<roomScript> rooms = CopyProtectDev.FreeRooms();
		if (rooms.Count == 0)
		{
			w.Label("No free development room right now.", x, y, width, 30f).color = UIKit.Red;
			return -1;
		}
		int index = rooms.FindIndex((roomScript r) => r.myID == selected);
		if (index < 0)
		{
			roomScript best = PickRoom(selected);
			index = (best ? rooms.IndexOf(best) : 0);
		}
		List<string> options = rooms.Select((roomScript r) =>
		{
			int staff = CopyProtectDev.Staff(r).Count;
			return CopyProtectDev.RoomName(r) + ((staff == 0) ? "  (no staff)" : ((work > 0f) ? ("  (" + staff + " staff, " + GameUpdates.Duration(work, r, kind) + ")") : ("  (" + staff + " staff)")));
		}).ToList();
		w.Dropdown(x, y, width, options, index, (int v) =>
		{
			UIKit.Sound();
			onChange(rooms[Mathf.Clamp(v, 0, rooms.Count - 1)].myID);
		});
		return rooms[index].myID;
	}

	public static string Change(long n)
	{
		if (n > 0)
		{
			return "<color=#2e7d32>+" + Short(n) + "</color>";
		}
		if (n < 0)
		{
			return "<color=#c62828>" + Short(n) + "</color>";
		}
		return "0";
	}

	// One line on how the game is doing against today's games, and what that does to its players.
	public static string Standing(gameScript g, MmoGame m)
	{
		if (m.OldSchool)
		{
			return "Old-school design: dated looks don't cost it players. " + Mathf.RoundToInt(MmoOps.OldSchoolJoin * 100f) + "% fewer new players join, " + Mathf.RoundToInt(MmoOps.OldSchoolChurn * 100f) + "% fewer leave.";
		}
		float dated = MmoOps.Dated(g, m);
		if (dated <= 0.001f)
		{
			return "Holds up against today's games.";
		}
		if (!MmoOps.PenaltyOn)
		{
			return "Looks dated (the penalty for that is switched off in the addon's settings).";
		}
		return "<color=#c62828>Looks dated: " + Mathf.RoundToInt(MmoOps.DatedChurn * dated * 100f) + "% more players leave and " + Mathf.RoundToInt(MmoOps.DatedJoin * dated * 100f) + "% fewer join.</color> Modernise it, or make it old-school.";
	}
}

// New platforms for an MMO, with no four-platform limit: picks fill the game's free slots first (empty ones, then
// ones whose platform has retired), the rest become extra platforms the addon adds to its sales.
internal class MmoPlatformsPage : Page
{
	private readonly int gameId;

	private int roomId;

	private readonly HashSet<int> picked = new HashSet<int>();

	private string status = "";

	private int seen = -1;

	public MmoPlatformsPage(int gameId, int roomId)
	{
		this.gameId = gameId;
		this.roomId = roomId;
	}

	private gameScript Game => GameUpdates.FindGame(gameId);

	public override string Title => "New platforms";

	public override Sprite IconSprite => UpdateIcons.For(UpdateKind.MmoPort);

	public override float Width => 960f;

	public override float Height => 860f;

	private static Image PlatformPic(RowView row, platformScript p, float x, float size)
	{
		Image img = new GameObject("Platform", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
		img.transform.SetParent(row.T, worldPositionStays: false);
		UIKit.Place(img.gameObject, x, 3f, size, size);
		img.preserveAspect = true;
		try
		{
			p.SetPic(img.gameObject);
		}
		catch
		{
			img.enabled = false;
		}
		return img;
	}

	private static string Share(platformScript p)
	{
		return p.GetMarktanteil().ToString("0.0") + "% of the market";
	}

	public override void Build(EditorWindow w)
	{
		seen = GameUpdates.Version;
		BackButton(w);
		w.Label(G.Money(((bool)G.MS) ? G.MS.money : 0), w.Width - 274f, 10f, 260f, 28f, TextAnchor.MiddleRight, bold: true);
		gameScript g = Game;
		if (!g || !MmoOps.IsMyMmo(g))
		{
			w.Label("This MMO is no longer on the market.", 24f, 60f, w.Width - 48f, 30f);
			return;
		}
		MmoGame m = MmoOps.Get(g);
		MmoUi.Shot(w, g, 14f, 48f, 120f, 68f);
		w.Label("<b>" + g.GetNameWithTag() + "</b>", 146f, 48f, w.Width - 160f, 28f, TextAnchor.MiddleLeft, bold: false, null, 17);
		w.Label("MMOs aren't held to four platforms: new ones fill free slots first (empty, or a retired platform's), the rest are added on top.", 146f, 76f, w.Width - 160f, 40f, TextAnchor.UpperLeft, bold: false, null, 13);
		float y = 128f;
		List<int> on = MmoOps.AllPlatforms(g);
		w.Header("Now on (" + on.Count((int id) => !MmoOps.Retired(id)) + " active)", 14f, y, w.Width - 28f);
		y += 30f;
		ListView now = w.List(13f, y, w.Width - 26f, 112f);
		foreach (int id in on)
		{
			platformScript p = GameUpdates.Platform(id);
			if (!p)
			{
				continue;
			}
			bool extra = !g.gamePlatform.Contains(id);
			RowView row = now.Row(34f);
			PlatformPic(row, p, 8f, 28f);
			Text name = row.Text(p.GetName() + (extra ? "  <color=#1565c0>(extra platform)</color>" : ""), 46f, row.W - 300f, bold: true);
			Text info = row.Text(p.vomMarktGenommen ? (extra ? "retired" : "retired: its slot can take a new platform") : Share(p), row.W - 300f, 290f, bold: false, TextAnchor.MiddleRight);
			if (p.vomMarktGenommen)
			{
				name.color = UIKit.Grey;
				info.color = UIKit.Grey;
			}
		}
		y += 120f;
		string problem = GameUpdates.Problem(UpdateKind.MmoPort, g);
		if (problem != null)
		{
			w.Info(problem, 14f, y, w.Width - 28f, 60f);
			return;
		}
		List<PlatformOption> options = MmoOps.PlatformOptions(g);
		picked.RemoveWhere((int id) => !options.Any((PlatformOption o) => o.Platform.myID == id && o.Blocked == null));
		w.Header("Launch on (" + options.Count((PlatformOption o) => o.Blocked == null) + " possible)", 14f, y, w.Width - 28f);
		y += 30f;
		float listHeight = w.Height - 75f - y - 214f;
		ListView list = w.List(13f, y, w.Width - 26f, listHeight);
		foreach (PlatformOption o in options)
		{
			OptionRow(w, list, g, o);
		}
		y += listHeight + 8f;
		List<int> chosen = picked.OrderByDescending((int id) => GameUpdates.Platform(id)?.GetMarktanteil() ?? 0f).ToList();
		UpdatePlan plan = MmoOps.PlanPlatforms(g, chosen);
		w.Label("Room", 24f, y, 110f, 30f, TextAnchor.MiddleLeft, bold: true);
		roomId = MmoUi.RoomDropdown(w, 140f, y, w.Width - 154f, roomId, plan.Work, UpdateKind.MmoPort, (int v) =>
		{
			roomId = v;
			w.Rebuild();
		});
		y += 42f;
		roomScript room = CopyProtectDev.FindRoom(roomId);
		int[] slots = MmoOps.SlotsAfter(g, chosen, out List<int> extras);
		int intoSlots = chosen.Count - extras.Count;
		List<KeyValuePair<string, string>> facts = new List<KeyValuePair<string, string>>
		{
			EditorWindow.KV("Costs", (chosen.Count > 0) ? G.Money(plan.Cost) : "-"),
			EditorWindow.KV("Time", (chosen.Count > 0 && (bool)room) ? GameUpdates.Duration(plan.Work, room, UpdateKind.MmoPort) : "-"),
			EditorWindow.KV("Picked", (chosen.Count == 0) ? "-" : (intoSlots + " into slots, " + extras.Count + " extra")),
			EditorWindow.KV("Active platforms after", (on.Count((int id) => !MmoOps.Retired(id)) + chosen.Count).ToString())
		};
		y += w.Facts(14f, y, w.Width - 28f, facts, 2) + 6f;
		w.Label((chosen.Count > 0) ? (plan.Notes + " " + MmoOps.PortEffects(g, chosen)) : "Pick the platforms to launch on. Each one adds to the costs and the work.", 24f, y, w.Width - 48f, 40f, TextAnchor.UpperLeft, bold: false, null, 13);
		y += 46f;
		bool ok = plan.Problem == null && (bool)room;
		w.BigButton("Start the launch", 14f, y, 260f, 44f, () =>
		{
			gameScript game = Game;
			int[] after = MmoOps.SlotsAfter(game, chosen, out List<int> more);
			UpdateProject p = new UpdateProject
			{
				Kind = UpdateKind.MmoPort,
				Platforms = after,
				ExtraPlatforms = more
			};
			string error = GameUpdates.Start(p, MmoOps.PlanPlatforms(game, chosen), CopyProtectDev.FindRoom(roomId), game);
			if (error != null)
			{
				status = error;
				w.Rebuild();
				return;
			}
			UIKit.Sound();
			MmoUi.PreferredRoom = -1;
			w.Replace(new UpdateProjectPage(p.TaskId));
		}, ok, ok ? ("Costs " + G.Money(plan.Cost) + ", paid now.") : (plan.Problem ?? "Pick a free development room with staff."));
		Text line = w.Label(status, 290f, y, w.Width - 304f, 44f, TextAnchor.MiddleLeft, bold: false, null, 13);
		line.color = UIKit.Red;
	}

	private void OptionRow(EditorWindow w, ListView list, gameScript g, PlatformOption o)
	{
		platformScript p = o.Platform;
		int id = p.myID;
		bool on = picked.Contains(id);
		bool blocked = o.Blocked != null;
		RowView row = list.Row(46f, blocked ? null : ((Action)delegate
		{
			if (!picked.Remove(id))
			{
				picked.Add(id);
			}
			status = "";
			w.Rebuild();
		}), o.Blocked ?? p.GetTooltip(), !blocked);
		if (on)
		{
			row.Highlight();
		}
		Image pic = PlatformPic(row, p, 8f, 40f);
		Text top = row.Top((on ? "[x] " : "[  ] ") + p.GetName(), 56f, row.W - 330f);
		Text bottom = row.Bottom(blocked ? o.Blocked : (p.GetManufacturer() + "  ·  " + Share(p)), 56f, row.W - 330f);
		if (blocked)
		{
			pic.color = new Color(1f, 1f, 1f, 0.4f);
			top.color = UIKit.Grey;
			bottom.color = UIKit.Red;
			return;
		}
		row.Text(G.Money(MmoOps.PlanPlatforms(g, new List<int> { id }).Cost), row.W - 160f, 150f, bold: true, TextAnchor.MiddleRight);
	}

	public override void Tick(EditorWindow w)
	{
		if (GameUpdates.Version != seen)
		{
			w.Rebuild();
		}
	}
}

// The game's MMO subscription charts (Charts > MMO Subscription Charts) inside MMO Management: every MMO ranked by
// subscribers now or by its best ever, yours highlighted (click one to manage it).
internal class MmoChartPage : Page
{
	private static int sort;

	private int seen = -1;

	public override string Title => "MMO Subscription Charts";

	public override Sprite IconSprite => UpdateIcons.Mmo();

	public override float Width => 900f;

	public override float Height => 720f;

	public override void Build(EditorWindow w)
	{
		seen = GameUpdates.Version;
		BackButton(w);
		w.Dropdown(116f, 10f, 300f, new List<string>
		{
			G.Text(1906, "Subscriptions"),
			G.Text(1903, "Best subscriptions")
		}, sort, (int v) =>
		{
			UIKit.Sound();
			sort = Mathf.Clamp(v, 0, 1);
			w.Rebuild();
		});
		w.Label((sort == 0) ? "MMOs on the market by subscribers today. Yours are highlighted: click one to manage it." : "Every MMO by the most subscribers it ever had. Yours are highlighted: click one to manage it.", 430f, 6f, w.Width - 444f, 40f, TextAnchor.MiddleLeft, bold: false, null, 13);
		gameScript[] all = ((G.MS && (bool)G.MS.games_) ? G.MS.games_.arrayGamesScripts : new gameScript[0]);
		List<gameScript> games = (from g in all
			where (bool)g && g.gameTyp == 1 && !g.inDevelopment && ((sort == 0) ? (g.isOnMarket && g.abonnements > 0) : (g.bestAbonnements > 0))
			orderby (sort == 0) ? g.abonnements : g.bestAbonnements descending
			select g).ToList();
		w.Header(((sort == 0) ? "MMO subscriptions" : "Best MMO subscriptions") + " (" + games.Count + ")", 14f, 50f, w.Width - 28f);
		if (games.Count == 0)
		{
			w.Label("No MMOs yet.", 24f, 84f, w.Width - 48f, 30f);
			return;
		}
		genres gen = (G.MainObj ? G.MainObj.GetComponent<genres>() : null);
		ListView list = w.List(13f, 82f, w.Width - 26f, w.Height - 75f - 92f);
		for (int i = 0; i < games.Count; i++)
		{
			Row(w, list, games[i], i + 1, gen);
		}
	}

	private void Row(EditorWindow w, ListView list, gameScript g, int rank, genres gen)
	{
		bool mine = MmoOps.IsMyMmo(g);
		int id = g.myID;
		RowView row = list.Row(52f, mine ? (() => w.Push(new MmoDashboardPage(id))) : null, g.GetTooltip());
		if (mine)
		{
			row.Highlight();
		}
		row.Text(rank + ".", 6f, 44f, bold: true, TextAnchor.MiddleRight);
		Sprite genre = (((bool)gen && g.maingenre >= 0) ? gen.GetPic(g.maingenre) : null);
		row.Picture(genre, 58f, 32f);
		row.Picture(g.GetDeveloperLogo(), 98f, 36f);
		row.Top((mine ? "<color=#1565c0>" : "") + g.GetNameWithTag() + (mine ? "</color>" : ""), 144f, row.W - 400f);
		string developer = g.GetDeveloperName();
		string state = ((sort == 1) ? (g.isOnMarket ? "on the market" : "closed") : MmoUi.Years(MmoOps.AgeYears(g)));
		row.Bottom(((developer.Length > 0) ? (developer + "  ·  ") : "") + g.reviewTotal + "%  ·  " + state, 144f, row.W - 400f);
		long value = ((sort == 0) ? g.abonnements : g.bestAbonnements);
		row.Text(MmoUiText.Num(value), row.W - 250f, 240f, bold: true, TextAnchor.MiddleRight, 4f, 24f);
		row.Text((sort == 0) ? (MmoUi.Change(g.abonnementsWoche) + " this week") : (g.isOnMarket ? (MmoUiText.Num(g.abonnements) + " now") : ""), row.W - 250f, 240f, bold: false, TextAnchor.MiddleRight, 26f, 22f);
	}

	public override void Tick(EditorWindow w)
	{
		if (GameUpdates.Version != seen)
		{
			w.Rebuild();
		}
	}
}

// An MMO's expansion packs (the game's MMO addons): their cards are kept off the list on the right of the screen.
internal class MmoAddonsPage : Page
{
	private readonly int gameId;

	private int seen = -1;

	public MmoAddonsPage(int gameId)
	{
		this.gameId = gameId;
	}

	public override string Title => "Expansion packs";

	public override Sprite IconSprite => UpdateIcons.ExpansionPack();

	public override float Width => 960f;

	public override float Height => 620f;

	public override void Build(EditorWindow w)
	{
		seen = GameUpdates.Version;
		BackButton(w);
		gameScript g = GameUpdates.FindGame(gameId);
		if (!g)
		{
			w.Label("This MMO no longer exists.", 24f, 60f, w.Width - 48f, 30f);
			return;
		}
		List<gameScript> addons = MmoOps.Addons(g);
		w.Label("<b>" + g.GetNameWithTag() + "</b>: " + addons.Count + ((addons.Count == 1) ? " expansion pack" : " expansion packs") + ". Hover one for its full details.", 116f, 10f, w.Width - 130f, 28f, TextAnchor.MiddleLeft, bold: false, null, 13);
		ListView list = w.List(13f, 50f, w.Width - 26f, w.Height - 75f - 60f);
		foreach (gameScript a in addons)
		{
			RowView row = list.Row(58f, null, a.GetTooltip());
			MmoUi.Shot(w, a, 8f, 5f, 86f, 48f, row.T);
			row.Top(a.GetNameWithTag() + "  <color=#666666>" + a.reviewTotal + "%</color>", 104f, row.W - 470f);
			row.Bottom((a.isOnMarket ? "On sale" : "Off the market") + "  ·  released " + a.GetReleaseDateString(), 104f, row.W - 470f);
			long week = ((a.isOnMarket && a.sellsPerWeek != null && a.sellsPerWeek.Length != 0) ? a.sellsPerWeek[0] : 0);
			row.Text(MmoUiText.Num(a.sellsTotal) + " sold", row.W - 360f, 170f, bold: true, TextAnchor.MiddleRight, 6f, 22f);
			row.Text(a.isOnMarket ? (MmoUiText.Num(week) + " this week") : "", row.W - 360f, 170f, bold: false, TextAnchor.MiddleRight, 30f, 22f);
			long profit = a.GetGesamtGewinn();
			Text money = row.Text(G.Money(profit), row.W - 180f, 170f, bold: true, TextAnchor.MiddleRight);
			money.color = ((profit < 0) ? UIKit.Red : UIKit.Green);
		}
		if (addons.Count == 0)
		{
			w.Label("No expansion packs released yet.", 24f, 60f, w.Width - 48f, 30f);
		}
	}

	public override void Tick(EditorWindow w)
	{
		if (GameUpdates.Version != seen)
		{
			w.Rebuild();
		}
	}
}

// Your MMOs on the market.
internal class MmoListPage : Page
{
	private int seen = -1;

	public override string Title => "MMO Management";

	public override Sprite IconSprite => UpdateIcons.Mmo();

	public override float Width => 900f;

	public override float Height => 640f;

	public override void Build(EditorWindow w)
	{
		seen = GameUpdates.Version;
		BackButton(w);
		w.Label(G.Money(((bool)G.MS) ? G.MS.money : 0), w.Width - 274f, 10f, 260f, 28f, TextAnchor.MiddleRight, bold: true);
		if (!MmoOps.On)
		{
			w.Info("MMO Management is switched off. Turn it on under Game settings in the addon's window.", 14f, 50f, w.Width - 28f, 60f);
			return;
		}
		w.Label("Keep your MMOs alive for decades: modernise them, grow them, release expansion packs and keep their players.", 116f, 4f, w.Width - 400f, 40f, TextAnchor.MiddleLeft, bold: false, null, 13);
		List<gameScript> mmos = MmoOps.MyMmos();
		AwardWin last = MmoAward.LastWin();
		w.Header("Your MMOs on the market (" + mmos.Count + ")" + ((last != null) ? ("  ·  Best Ongoing Game " + last.Year + ": " + last.Game) : ""), 14f, 50f, w.Width - 28f - 230f);
		w.SmallButton("Subscription charts", w.Width - 234f, 48f, 220f, () => w.Push(new MmoChartPage()), enabled: true, "Every MMO ranked by subscribers, now or best ever (the game's MMO Subscription Charts).", null, 28f);
		if (mmos.Count == 0)
		{
			w.Label("You have no MMO on the market. Develop one (Develop a Game, game type MMO) and come back once it's out.", 24f, 84f, w.Width - 48f, 44f);
			return;
		}
		ListView list = w.List(13f, 82f, w.Width - 26f, w.Height - 75f - 92f);
		foreach (gameScript g in mmos)
		{
			Row(w, list, g);
		}
	}

	private void Row(EditorWindow w, ListView list, gameScript g)
	{
		int id = g.myID;
		MmoGame m = MmoOps.Get(g);
		RowView row = list.Row(70f, () => w.Push(new MmoDashboardPage(id)), g.GetTooltip());
		MmoUi.Shot(w, g, 8f, 7f, 100f, 56f, row.T);
		row.Top(g.GetNameWithTag() + "  <color=#666666>(" + MmoOps.SizeName(g.gameSize) + ")</color>", 120f, row.W - 400f);
		UpdateProject running = GameUpdates.ProjectFor(id);
		string state = ((running != null) ? ("<color=#1565c0>In development: " + GameUpdates.KindName(running.Kind) + " " + Mathf.FloorToInt(running.Fraction * 100f) + "%</color>") : (m.OldSchool ? "old-school" : ((MmoOps.Dated(g, m) > 0.001f) ? "<color=#c62828>looks dated</color>" : "holds up today")));
		row.Bottom(MmoUi.Years(MmoOps.AgeYears(g)) + " on the market  ·  " + (m.Client64 ? "64-bit" : "32-bit") + "  ·  " + state, 120f, row.W - 400f);
		row.Text(MmoUi.Short(g.abonnements) + " subscribers", row.W - 270f, 160f, bold: true, TextAnchor.MiddleRight, 10f, 24f);
		row.Text(MmoUi.Change(g.abonnementsWoche) + " this week", row.W - 270f, 160f, bold: false, TextAnchor.MiddleRight, 36f, 24f);
		row.Button("Manage", row.W - 98f, 90f, () => w.Push(new MmoDashboardPage(id)));
	}

	public override void Tick(EditorWindow w)
	{
		if (GameUpdates.Version != seen)
		{
			w.Rebuild();
		}
	}
}

// One MMO: how it's doing, how it holds up today, its players over time, and what you can do with it.
internal class MmoDashboardPage : Page
{
	private readonly int gameId;

	private int seen = -1;

	private float nextTick;

	private string status = "";

	private bool statusGood;

	public MmoDashboardPage(int gameId)
	{
		this.gameId = gameId;
	}

	private gameScript Game => GameUpdates.FindGame(gameId);

	public override string Title => "MMO Management";

	public override Sprite IconSprite => UpdateIcons.Mmo();

	public override float Width => 1100f;

	public override float Height => 720f;

	public override void Build(EditorWindow w)
	{
		seen = GameUpdates.Version;
		BackButton(w);
		w.Label(G.Money(((bool)G.MS) ? G.MS.money : 0), w.Width - 274f, 10f, 260f, 28f, TextAnchor.MiddleRight, bold: true);
		gameScript g = Game;
		if (!g || !MmoOps.IsMyMmo(g))
		{
			w.Label("This MMO is no longer on the market.", 24f, 60f, w.Width - 48f, 30f);
			return;
		}
		MmoGame m = MmoOps.Get(g);
		HeaderBlock(w, g, m);
		Text line = w.Label(status, 14f, 144f, w.Width - 28f, 22f, TextAnchor.MiddleLeft, bold: false, null, 13);
		line.color = statusGood ? UIKit.Green : UIKit.Red;
		float left = 14f;
		float leftW = 520f;
		float y = StatsBlock(w, g, m, left, 170f, leftW);
		y = StandingBlock(w, g, m, left, y + 10f, leftW);
		ChartBlock(w, m, left, y + 8f, leftW, w.Height - 75f - (y + 8f) - 12f);
		ProjectsBlock(w, g, m, left + leftW + 14f, 170f, w.Width - (left + leftW + 14f) - 14f);
	}

	private void HeaderBlock(EditorWindow w, gameScript g, MmoGame m)
	{
		MmoUi.Shot(w, g, 14f, 48f, 160f, 90f);
		w.Label("<b>" + g.GetNameWithTag() + "</b>", 188f, 46f, w.Width - 440f, 30f, TextAnchor.MiddleLeft, bold: false, null, 18);
		w.Label(MmoUi.Genre(g) + "  ·  released " + ((g.date_year > 0) ? g.date_year.ToString() : "?") + "  ·  " + MmoUi.Years(MmoOps.AgeYears(g)) + " on the market  ·  " + G.Text(277, "Review") + " " + g.reviewTotal + "%", 188f, 78f, w.Width - 440f, 24f, TextAnchor.MiddleLeft, bold: false, null, 13);
		engineScript e = GameUpdates.CurrentEngine(g);
		w.Label("Size " + MmoOps.SizeName(g.gameSize) + "  ·  " + (m.Client64 ? "64-bit client" : "32-bit client") + "  ·  " + (m.OldSchool ? "old-school design" : "modern design") + "  ·  " + (e ? (e.GetName() + " (tech " + e.GetTechLevel() + ")") : "no engine"), 188f, 102f, w.Width - 440f, 24f, TextAnchor.MiddleLeft, bold: false, null, 13);
		int wait = MmoOps.StanceWait(m);
		string tip = (m.OldSchool ? "Make it a modern MMO: more new players again, but dated graphics and tech cost it players." : "Make it an old-school theme-park MMO: dated looks no longer cost it players, and its smaller audience is more loyal (" + Mathf.RoundToInt(MmoOps.OldSchoolJoin * 100f) + "% fewer new players, " + Mathf.RoundToInt(MmoOps.OldSchoolChurn * 100f) + "% fewer leave).") + " You can change this once every two years." + ((wait > 0) ? (" Next change possible in " + wait + " weeks.") : "");
		w.SmallButton((wait > 0) ? ("Style locked for " + wait + " weeks") : (m.OldSchool ? "Make it modern" : "Make it old-school"), w.Width - 234f, 52f, 220f, () =>
		{
			gameScript game = Game;
			if (!game)
			{
				return;
			}
			MmoGame now = MmoOps.Get(game);
			string error = MmoOps.SetOldSchool(game, !now.OldSchool);
			status = error ?? (game.GetNameSimple() + " is now " + (now.OldSchool ? "an old-school MMO." : "a modern MMO."));
			statusGood = error == null;
			w.Rebuild();
		}, wait <= 0, tip, null, 30f);
		w.SmallButton("Subscription charts", w.Width - 234f, 90f, 220f, () => w.Push(new MmoChartPage()), enabled: true, "Every MMO ranked by subscribers, now or best ever (the game's MMO Subscription Charts).", null, 30f);
	}

	private float StatsBlock(EditorWindow w, gameScript g, MmoGame m, float x, float y, float width)
	{
		MmoOps.ServerLoad(out long used, out long capacity);
		float interestCap = 100f - g.GetMMOAbnutzung();
		List<KeyValuePair<string, string>> facts = new List<KeyValuePair<string, string>>
		{
			EditorWindow.KV(G.Text(1236, "Subscriptions"), MmoUi.Short(g.abonnements)),
			EditorWindow.KV(G.Text(1905, "Peak"), MmoUi.Short(g.bestAbonnements)),
			EditorWindow.KV("This week", MmoUi.Change(g.abonnementsWoche)),
			EditorWindow.KV("Income a month", G.Money(g.abonnements * g.aboPreis) + " ($" + g.aboPreis + " each)"),
			EditorWindow.KV("Interest", Mathf.RoundToInt(g.mmoInteresse) + " (max " + Mathf.RoundToInt(interestCap) + ")"),
			EditorWindow.KV("Age for wear", MmoUi.Years(MmoOps.WearAgeYears(g, m)) + ((m.Renewal > 0) ? " (renewed)" : "")),
			EditorWindow.KV("Servers", (capacity > 0) ? (Mathf.RoundToInt(100f * (float)used / (float)capacity) + "% of " + MmoUi.Short(capacity)) : "<color=#c62828>none</color>"),
			EditorWindow.KV("Copies sold", MmoUi.Short(g.sellsTotal)),
			EditorWindow.KV("Platforms", PlatformsText(g)),
			EditorWindow.KV("IP points", MmoOps.IpStatus(g)),
			EditorWindow.KV("Best ongoing", MmoAward.WinsText(g.myID))
		};
		return y + w.Facts(x, y, width, facts, 2);
	}

	private static string PlatformsText(gameScript g)
	{
		List<int> all = MmoOps.AllPlatforms(g);
		int retired = all.Count(MmoOps.Retired);
		int extra = all.Count((int id) => !g.gamePlatform.Contains(id));
		return (all.Count - retired) + " active" + ((extra > 0) ? (", " + extra + " extra") : "") + ((retired > 0) ? (", " + retired + " retired") : "");
	}

	private float StandingBlock(EditorWindow w, gameScript g, MmoGame m, float x, float y, float width)
	{
		w.Header("How it holds up today", x, y, width);
		y += 32f;
		int now = MmoOps.NowMonth;
		y = StandingBar(w, "Graphics", g.points_grafik, m.LaunchGfx, m.LaunchMonth, now, x, y, width);
		y = StandingBar(w, "Tech", g.points_technik, m.LaunchTech, m.LaunchMonth, now, x, y, width);
		w.Label(MmoUi.Standing(g, m), x + 10f, y, width - 20f, 40f, TextAnchor.UpperLeft, bold: false, null, 13);
		return y + 42f;
	}

	private static float StandingBar(EditorWindow w, string name, float points, float launchPoints, int launchMonth, int now, float x, float y, float width)
	{
		int today = MmoOps.Score(points, now);
		int atLaunch = MmoOps.Score(launchPoints, launchMonth);
		w.Label(name, x + 10f, y, 90f, 26f, TextAnchor.MiddleLeft, bold: true);
		w.Bar(x + 100f, y + 2f, width - 110f, 22f, today / 100f, today + " today  ·  " + atLaunch + " at launch", UIKit.ValueColor(today), null, "What a review would give its " + name.ToLower() + " today, against what it got at launch. Today's games set the bar higher every year.");
		return y + 30f;
	}

	private static void ChartBlock(EditorWindow w, MmoGame m, float x, float y, float width, float height)
	{
		w.Header("Subscribers, last two years", x, y, width);
		y += 30f;
		height = Mathf.Max(60f, height - 30f);
		RectTransform panel = w.Panel(x, y, width, height);
		List<long> history = m.History ?? new List<long>();
		int count = Mathf.Min(104, history.Count);
		if (count < 2)
		{
			w.Label("The chart fills in week by week from now on.", 12f, height / 2f - 12f, width - 24f, 24f, TextAnchor.MiddleCenter, bold: false, panel, 13);
			return;
		}
		List<long> shown = history.Skip(history.Count - count).ToList();
		long max = Math.Max(1L, shown.Max());
		float chartTop = 24f;
		float chartH = height - chartTop - 8f;
		float step = (width - 20f) / 104f;
		float startX = 10f + (104 - count) * step;
		Color bar = new Color(0.16f, 0.45f, 0.8f, 0.85f);
		for (int i = 0; i < count; i++)
		{
			float h = Mathf.Max(1f, chartH * (float)shown[i] / (float)max);
			MmoUi.Rect(panel, startX + (float)i * step, chartTop + chartH - h, Mathf.Max(1f, step - 1f), h, bar);
		}
		w.Label("max " + MmoUi.Short(max), 10f, 2f, 200f, 20f, TextAnchor.MiddleLeft, bold: false, panel, 12);
		w.Label("now " + MmoUi.Short(shown[count - 1]), width - 210f, 2f, 200f, 20f, TextAnchor.MiddleRight, bold: false, panel, 12);
	}

	private void ProjectsBlock(EditorWindow w, gameScript g, MmoGame m, float x, float y, float width)
	{
		w.Header("Projects", x, y, width);
		y += 32f;
		ListView list = w.List(x - 1f, y, width + 2f, w.Height - 75f - y - 12f);
		UpdateProject running = GameUpdates.ProjectFor(g.myID);
		if (running != null)
		{
			int task = running.TaskId;
			RowView row = list.Row(64f, () => w.Push(new UpdateProjectPage(task)), "Open the project: progress, room, stop it.");
			row.Highlight();
			row.Picture(UpdateIcons.For(running.Kind), 10f, 44f);
			row.Top("In development: " + GameUpdates.KindName(running.Kind), 64f, row.W - 250f);
			roomScript room = CopyProtectDev.Rooms().FirstOrDefault((roomScript r) => r.taskID == running.TaskId);
			row.Bottom(room ? (CopyProtectDev.RoomName(room) + "  ·  " + GameUpdates.Duration(running.Needed - running.Progress, room, running.Kind) + " left") : "no room", 64f, row.W - 250f);
			row.Bar(row.W - 176f, 166f, 22f, running.Fraction, UIKit.Green, Mathf.FloorToInt(running.Fraction * 100f) + "%");
		}
		ProjectRow(w, list, g, m, UpdateKind.MmoGraphics);
		ProjectRow(w, list, g, m, UpdateKind.MmoClient64);
		ProjectRow(w, list, g, m, UpdateKind.MmoMigration);
		ProjectRow(w, list, g, m, UpdateKind.MmoExpansion);
		ProjectRow(w, list, g, m, UpdateKind.MmoFeatures);
		ProjectRow(w, list, g, m, UpdateKind.MmoPort);
		ExpansionPackRow(w, list, g);
		AddonsRow(w, list, g);
		ProjectRow(w, list, g, m, UpdateKind.MmoPatch);
		FreeUpdateRow(w, list, g);
		ProjectRow(w, list, g, m, UpdateKind.MmoRating);
	}

	// The expansion packs already made for it (their game cards are kept off the list on the right).
	private void AddonsRow(EditorWindow w, ListView list, gameScript g)
	{
		List<gameScript> addons = MmoOps.Addons(g);
		int id = g.myID;
		RowView row = list.Row(64f, (addons.Count > 0) ? ((Action)delegate
		{
			w.Push(new MmoAddonsPage(id));
		}) : null, (addons.Count > 0) ? "Sales and money of each expansion pack." : "No expansion packs released yet.");
		Image icon = row.Picture(UpdateIcons.ExpansionPack(), 10f, 44f);
		Text top = row.Top("Released expansion packs (" + addons.Count + ")", 64f, row.W - 74f);
		int onSale = addons.Count((gameScript a) => a.isOnMarket);
		long week = addons.Where((gameScript a) => a.isOnMarket && a.sellsPerWeek != null && a.sellsPerWeek.Length != 0).Sum((gameScript a) => (long)a.sellsPerWeek[0]);
		Text bottom = row.Bottom((addons.Count > 0) ? (onSale + " on sale  ·  " + MmoUi.Short(week) + " sold this week  ·  " + MmoUi.Short(addons.Sum((gameScript a) => a.sellsTotal)) + " in total") : "Their sales show here once you release one.", 64f, row.W - 74f);
		if (addons.Count == 0)
		{
			icon.color = new Color(1f, 1f, 1f, 0.45f);
			top.color = UIKit.Grey;
			bottom.color = UIKit.Grey;
		}
	}

	// The game's own free update (bug fixes, points, languages), opened for this MMO.
	private void FreeUpdateRow(EditorWindow w, ListView list, gameScript g)
	{
		string problem = MmoOps.FreeUpdateProblem(g);
		RowView row = list.Row(64f, () =>
		{
			gameScript game = Game;
			roomScript room = MmoUi.PickRoom(MmoUi.PreferredRoom);
			if (!game)
			{
				return;
			}
			string error = MmoOps.FreeUpdateProblem(game) ?? ((!room) ? "A free update needs a free development room with staff." : GameUpdates.RoomProblem(room));
			if (error != null)
			{
				status = error;
				statusGood = false;
				w.Rebuild();
				return;
			}
			w.Close();
			error = MmoOps.OpenFreeUpdate(game, room);
			if (error != null)
			{
				CopyProtectDev.Report(error);
			}
		}, problem ?? "The game's own free update for this MMO: bug fixes, more points and languages, and a lasting boost to sales. Closing its screen brings you back here.");
		Image icon = row.Picture(UIKit.Sprite("iconUpdate") ?? UpdateIcons.For(UpdateKind.MmoPatch), 10f, 44f);
		Text top = row.Top("Free update (patch)", 64f, row.W - 74f);
		Text bottom = row.Bottom(problem ?? ("Bug fixes, points and languages  ·  " + g.amountUpdates + " so far  ·  " + Mathf.RoundToInt(g.points_bugs) + " bugs now"), 64f, row.W - 74f);
		if (problem != null)
		{
			icon.color = new Color(1f, 1f, 1f, 0.45f);
			top.color = UIKit.Grey;
			bottom.color = UIKit.Grey;
		}
	}

	private static string Capital(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return s;
		}
		return char.ToUpper(s[0]) + s.Substring(1);
	}

	public static string RowName(UpdateKind kind)
	{
		if (kind != UpdateKind.MmoMigration)
		{
			return GameUpdates.KindName(kind);
		}
		return "Engine upgrade or migration";
	}

	private void Open(EditorWindow w, UpdateKind kind, int id)
	{
		int room = MmoUi.PreferredRoom;
		switch (kind)
		{
		case UpdateKind.MmoMigration:
			w.Push(new EngineUpdatePage(id, room, mmo: true));
			break;
		case UpdateKind.MmoFeatures:
			w.Push(new FeatureUpdatePage(id, room, mmo: true));
			break;
		case UpdateKind.MmoRating:
			w.Push(new RatingUpdatePage(id, room, mmo: true));
			break;
		case UpdateKind.MmoPort:
			w.Push(new MmoPlatformsPage(id, room));
			break;
		default:
			w.Push(new MmoProjectPage(kind, id));
			break;
		}
	}

	private void ProjectRow(EditorWindow w, ListView list, gameScript g, MmoGame m, UpdateKind kind)
	{
		string problem = GameUpdates.Problem(kind, g);
		int id = g.myID;
		RowView row = list.Row(64f, () => Open(w, kind, id), problem ?? MmoProjectPage.Describe(kind));
		Image icon = row.Picture(UpdateIcons.For(kind), 10f, 44f);
		Text top = row.Top(RowName(kind), 64f, row.W - 74f);
		Text bottom = row.Bottom(problem ?? Summary(kind, g, m), 64f, row.W - 74f);
		if (problem != null)
		{
			icon.color = new Color(1f, 1f, 1f, 0.45f);
			top.color = UIKit.Grey;
			bottom.color = UIKit.Grey;
		}
	}

	private static string Summary(UpdateKind kind, gameScript g, MmoGame m)
	{
		switch (kind)
		{
		case UpdateKind.MmoGraphics:
		{
			UpdatePlan plan = MmoOps.PlanOverhaul(g);
			int now = MmoOps.NowMonth;
			return "Graphics " + MmoOps.Score(g.points_grafik, now) + " > " + MmoOps.Score(g.points_grafik + plan.Graphics, now) + ", interest +" + MmoOps.OverhaulInterest(g) + ", 1.5 years younger  ·  " + G.Money(plan.Cost);
		}
		case UpdateKind.MmoClient64:
			return "More tech, fewer crashes, no 2007 graphics limit  ·  " + G.Money(MmoOps.PlanClient64(g).Cost);
		case UpdateKind.MmoMigration:
		{
			List<engineScript> engines = GameUpdates.EngineCandidates(g);
			engineScript own = engines.FirstOrDefault((engineScript e) => e.myID == g.engineID);
			engineScript other = engines.Where((engineScript e) => e.myID != g.engineID).OrderByDescending((engineScript e) => e.GetTechLevel()).FirstOrDefault();
			List<string> parts = new List<string>();
			if ((bool)own)
			{
				parts.Add("newer " + own.GetName() + " features");
			}
			if ((bool)other)
			{
				parts.Add("move to " + other.GetName() + " (tech " + other.GetTechLevel() + ")");
			}
			return Capital(string.Join(", or ", parts)) + "  ·  interest up, counts as younger";
		}
		case UpdateKind.MmoFeatures:
		{
			int count = GameUpdates.FeatureCandidates(g, null).Count;
			return count + " researched features it doesn't have yet  ·  interest up, counts as younger";
		}
		case UpdateKind.MmoPort:
		{
			int active = MmoOps.AllPlatforms(g).Count((int id) => !MmoOps.Retired(id));
			int possible = MmoOps.PlatformOptions(g).Count((PlatformOption o) => o.Blocked == null);
			return "On " + active + ((active == 1) ? " platform" : " platforms") + ", " + possible + " more possible (no limit for MMOs)  ·  new players, a launch boost and interest";
		}
		case UpdateKind.MmoRating:
			return "Rated " + GameUpdates.UskName(g.usk) + ", its topic allows " + GameUpdates.UskName(GameUpdates.MinUsk(g)) + "  ·  a wider audience, some subscribers leave";
		case UpdateKind.MmoExpansion:
		{
			UpdatePlan plan = MmoOps.PlanExpansion(g);
			return MmoOps.SizeName(g.gameSize) + " > " + MmoOps.SizeName(g.gameSize + 1) + ": all points up, interest +10, a year younger  ·  " + G.Money(plan.Cost);
		}
		case UpdateKind.MmoPatch:
			return "Interest +" + MmoOps.PatchInterest(g) + ", " + Mathf.RoundToInt(MmoOps.PatchChurn * 100f) + "% fewer leave for " + MmoOps.PatchWeeks + " weeks  ·  " + G.Money(MmoOps.PlanPatch(g).Cost);
		default:
			return "";
		}
	}

	private void ExpansionPackRow(EditorWindow w, ListView list, gameScript g)
	{
		string problem = MmoOps.ExpansionPackProblem(g);
		int id = g.myID;
		RowView row = list.Row(64f, () =>
		{
			gameScript game = Game;
			roomScript room = MmoUi.PickRoom(MmoUi.PreferredRoom);
			if (!game)
			{
				return;
			}
			if (!room)
			{
				status = "An expansion pack needs a free development room with staff.";
				statusGood = false;
				w.Rebuild();
				return;
			}
			string error = MmoOps.ExpansionPackProblem(game) ?? GameUpdates.RoomProblem(room);
			if (error != null)
			{
				status = error;
				statusGood = false;
				w.Rebuild();
				return;
			}
			w.Close();
			error = MmoOps.OpenExpansionPack(game, room);
			if (error != null)
			{
				CopyProtectDev.Report(error);
			}
		}, problem ?? ("Develop an MMO addon for this MMO, on the game's own addon screen, in a free development room. When it's released, the MMO's interest goes up by the addon's rating. " + MmoOps.AddonEffects() + " Closing the addon screen brings you back here."));
		Image icon = row.Picture(UpdateIcons.ExpansionPack(), 10f, 44f);
		Text top = row.Top("Expansion pack (MMO addon)", 64f, row.W - 74f);
		Text bottom = row.Bottom(problem ?? ("Big content drop: interest up by its rating, more players for good  ·  " + g.amountMMOAddons + " released so far"), 64f, row.W - 74f);
		if (problem != null)
		{
			icon.color = new Color(1f, 1f, 1f, 0.45f);
			top.color = UIKit.Grey;
			bottom.color = UIKit.Grey;
		}
	}

	public override void Tick(EditorWindow w)
	{
		if (GameUpdates.Version != seen || (GameUpdates.ProjectFor(gameId) != null && Time.unscaledTime > nextTick))
		{
			nextTick = Time.unscaledTime + 2f;
			w.Rebuild();
		}
	}
}

// One MMO project: what it does, what it costs, how long it takes in which room, and the start button.
internal class MmoProjectPage : Page
{
	private readonly UpdateKind kind;

	private readonly int gameId;

	private int roomId = MmoUi.PreferredRoom;


	private string status = "";

	private int seen = -1;

	public MmoProjectPage(UpdateKind kind, int gameId)
	{
		this.kind = kind;
		this.gameId = gameId;
	}

	private gameScript Game => GameUpdates.FindGame(gameId);

	public override string Title => GameUpdates.KindName(kind);

	public override Sprite IconSprite => UpdateIcons.For(kind);

	public override float Width => 900f;

	public override float Height => 660f;

	public static string Describe(UpdateKind kind)
	{
		return kind switch
		{
			UpdateKind.MmoGraphics => "Rework the art, models and effects so it holds up against today's games again, as well as it did at launch. Its picture moves on with its graphics.",
			UpdateKind.MmoClient64 => "Rebuild the game client for 64-bit systems: more memory, fewer crashes, and room for graphics past 2007's standard.",
			UpdateKind.MmoMigration => "Give it newer features from its own engine (an upgrade), or move the whole MMO to another engine you own or license (a migration: a bigger job and a bigger renewal, with a short dip in players while everyone adjusts).",
			UpdateKind.MmoExpansion => "Grow the world a size up: more zones, systems and content. Every area gains points, and bigger games draw more players.",
			UpdateKind.MmoPatch => "A smaller drop between expansion packs: new quests, a dungeon and class balance. Keeps players busy for a while.",
			UpdateKind.MmoFeatures => "Add researched gameplay features as new game systems. Interest goes up and it counts as younger; systems that don't suit the genre drive a few players away.",
			UpdateKind.MmoPort => "Launch it on more platforms with internet, with no four-platform limit: new ones fill free slots first (empty, or a retired platform's), the rest are added on top. New players, a launch boost and more interest.",
			UpdateKind.MmoRating => "Cut content for a lower age rating: more people can play it, but some subscribers liked what gets cut.",
			_ => "",
		};
	}

	public override void Build(EditorWindow w)
	{
		seen = GameUpdates.Version;
		BackButton(w);
		w.Label(G.Money(((bool)G.MS) ? G.MS.money : 0), w.Width - 274f, 10f, 260f, 28f, TextAnchor.MiddleRight, bold: true);
		gameScript g = Game;
		if (!g || !MmoOps.IsMyMmo(g))
		{
			w.Label("This MMO is no longer on the market.", 24f, 60f, w.Width - 48f, 30f);
			return;
		}
		MmoGame m = MmoOps.Get(g);
		MmoUi.Shot(w, g, 14f, 48f, 120f, 68f);
		w.Label("<b>" + g.GetNameWithTag() + "</b>", 146f, 48f, w.Width - 160f, 28f, TextAnchor.MiddleLeft, bold: false, null, 17);
		w.Label(MmoUi.Short(g.abonnements) + " subscribers  ·  " + MmoUi.Years(MmoOps.AgeYears(g)) + " on the market  ·  size " + MmoOps.SizeName(g.gameSize) + "  ·  " + (m.Client64 ? "64-bit" : "32-bit") + " client", 146f, 78f, w.Width - 160f, 24f, TextAnchor.MiddleLeft, bold: false, null, 13);
		float y = 128f;
		w.Label(Describe(kind), 24f, y, w.Width - 48f, 44f, TextAnchor.UpperLeft, bold: false, null, 13);
		y += 52f;
		string problem = GameUpdates.Problem(kind, g);
		if (problem != null)
		{
			w.Info(problem, 14f, y, w.Width - 28f, 60f);
			return;
		}
		UpdatePlan plan = Plan(g);
		w.Label("Room", 24f, y, 110f, 30f, TextAnchor.MiddleLeft, bold: true);
		roomId = MmoUi.RoomDropdown(w, 140f, y, w.Width - 154f, roomId, plan.Work, kind, (int v) =>
		{
			roomId = v;
			w.Rebuild();
		});
		y += 44f;
		roomScript room = CopyProtectDev.FindRoom(roomId);
		List<KeyValuePair<string, string>> facts = new List<KeyValuePair<string, string>>
		{
			EditorWindow.KV("Costs", G.Money(plan.Cost)),
			EditorWindow.KV("Time", room ? GameUpdates.Duration(plan.Work, room, kind) : "-")
		};
		facts.AddRange(Effects(g, m, plan));
		y += w.Facts(14f, y, w.Width - 28f, facts, facts.Any((KeyValuePair<string, string> f) => f.Value.Length > 34) ? 1 : 2) + 8f;
		if (plan.Notes.Length > 0)
		{
			w.Label(plan.Notes, 24f, y, w.Width - 48f, 40f, TextAnchor.UpperLeft, bold: false, null, 13);
			y += 46f;
		}
		bool ok = plan.Problem == null && (bool)room;
		w.BigButton("Start the project", 14f, y, 260f, 44f, () =>
		{
			gameScript game = Game;
			UpdateProject p = new UpdateProject
			{
				Kind = kind
			};
			string error = GameUpdates.Start(p, Plan(game), CopyProtectDev.FindRoom(roomId), game);
			if (error != null)
			{
				status = error;
				w.Rebuild();
				return;
			}
			UIKit.Sound();
			MmoUi.PreferredRoom = -1;
			w.Replace(new UpdateProjectPage(p.TaskId));
		}, ok, ok ? ("Costs " + G.Money(plan.Cost) + ", paid now.") : (plan.Problem ?? "Pick a free development room with staff."));
		Text line = w.Label(status, 290f, y, w.Width - 304f, 44f, TextAnchor.MiddleLeft, bold: false, null, 13);
		line.color = UIKit.Red;
	}

	private UpdatePlan Plan(gameScript g)
	{
		return kind switch
		{
			UpdateKind.MmoGraphics => MmoOps.PlanOverhaul(g),
			UpdateKind.MmoClient64 => MmoOps.PlanClient64(g),
			UpdateKind.MmoExpansion => MmoOps.PlanExpansion(g),
			UpdateKind.MmoPatch => MmoOps.PlanPatch(g),
			_ => new UpdatePlan
			{
				Problem = "Unknown project."
			},
		};
	}

	private IEnumerable<KeyValuePair<string, string>> Effects(gameScript g, MmoGame m, UpdatePlan plan)
	{
		int now = MmoOps.NowMonth;
		switch (kind)
		{
		case UpdateKind.MmoGraphics:
			yield return EditorWindow.KV("Graphics", "+" + Mathf.RoundToInt(plan.Graphics) + " (score " + MmoOps.Score(g.points_grafik, now) + " > " + MmoOps.Score(g.points_grafik + plan.Graphics, now) + ")");
			yield return EditorWindow.KV("Interest", "+" + MmoOps.OverhaulInterest(g));
			yield return EditorWindow.KV("Age for wear", "1.5 years younger");
			yield return EditorWindow.KV("Next overhaul", "a year after this one");
			break;
		case UpdateKind.MmoClient64:
			yield return EditorWindow.KV("Tech", "+" + Mathf.RoundToInt(plan.Tech));
			yield return EditorWindow.KV("Bugs", "-30%");
			yield return EditorWindow.KV("Players leaving", "-" + Mathf.RoundToInt(MmoOps.ClientChurn * 100f) + "% for good");
			yield return EditorWindow.KV("Interest", "+5, half a year younger");
			break;
		case UpdateKind.MmoExpansion:
			yield return EditorWindow.KV("Size", MmoOps.SizeName(g.gameSize) + " > " + MmoOps.SizeName(g.gameSize + 1));
			yield return EditorWindow.KV("Points", PointsText(plan));
			yield return EditorWindow.KV("Interest", "+10, hype +10");
			yield return EditorWindow.KV("Age for wear", "a year younger");
			break;
		case UpdateKind.MmoPatch:
			yield return EditorWindow.KV("Interest", "+" + MmoOps.PatchInterest(g));
			yield return EditorWindow.KV("Players leaving", "-" + Mathf.RoundToInt(MmoOps.PatchChurn * 100f) + "% for " + MmoOps.PatchWeeks + " weeks");
			yield return EditorWindow.KV("Gameplay", "+" + Mathf.RoundToInt(plan.Gameplay));
			yield return EditorWindow.KV("Next patch", MmoOps.PatchCooldown + " weeks after this one");
			break;
		}
	}

	private static string PointsText(UpdatePlan plan)
	{
		List<string> parts = new List<string>();
		if (plan.Gameplay >= 0.5f)
		{
			parts.Add("gameplay +" + Mathf.RoundToInt(plan.Gameplay));
		}
		if (plan.Graphics >= 0.5f)
		{
			parts.Add("graphics +" + Mathf.RoundToInt(plan.Graphics));
		}
		if (plan.Sound >= 0.5f)
		{
			parts.Add("sound +" + Mathf.RoundToInt(plan.Sound));
		}
		if (plan.Tech >= 0.5f)
		{
			parts.Add("tech +" + Mathf.RoundToInt(plan.Tech));
		}
		if (parts.Count != 0)
		{
			return string.Join(", ", parts);
		}
		return "-";
	}

	public override void Tick(EditorWindow w)
	{
		if (GameUpdates.Version != seen)
		{
			w.Rebuild();
		}
	}
}
