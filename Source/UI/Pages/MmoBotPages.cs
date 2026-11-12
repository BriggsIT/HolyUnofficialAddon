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

internal static class BotUi
{
	public static string Pct(float f)
	{
		return Mathf.RoundToInt(f * 100f) + "%";
	}

	public static List<int> Genres()
	{
		genres gen = (G.MainObj ? G.MainObj.GetComponent<genres>() : null);
		List<int> list = new List<int>();
		if (!gen || gen.genres_LEVEL == null)
		{
			return list;
		}
		for (int i = 0; i < gen.genres_LEVEL.Length; i++)
		{
			if (gen.IsErforscht(i))
			{
				list.Add(i);
			}
		}
		return list;
	}

	public static void Grey(Image icon, Text top, Text bottom)
	{
		if ((bool)icon)
		{
			icon.color = new Color(1f, 1f, 1f, 0.45f);
		}
		if ((bool)top)
		{
			top.color = UIKit.Grey;
		}
		if ((bool)bottom)
		{
			bottom.color = UIKit.Grey;
		}
	}
}

// Your anti-bot systems and how well each MMO is protected.
internal class AntiBotHomePage : Page
{
	private int seen = -1;

	public override string Title => "Anti-Bot Systems";

	public override Sprite IconSprite => UpdateIcons.AntiBot();

	public override float Width => 1000f;

	public override float Height => 760f;

	public override void Build(EditorWindow w)
	{
		seen = GameUpdates.Version;
		BackButton(w);
		w.Label(G.Money(((bool)G.MS) ? G.MS.money : 0), w.Width - 274f, 10f, 260f, 28f, TextAnchor.MiddleRight, bold: true);
		if (!MmoBots.On)
		{
			w.Info("Bots are switched off. Turn on \"MMO bots and anti-bot\" under Game settings in the addon's window.", 14f, 50f, w.Width - 28f, 60f);
			return;
		}
		w.AboutLine("Bots farm gold and spoil your MMOs for real players. An anti-bot system keeps them out: build your own for a genre, or license a rival studio's. Each works fully on MMOs of its genre, partly where it's the second genre, and a little on the rest.");
		int best = MmoBots.BestTech();
		bool open = best >= 0;
		w.Header("Your anti-bot systems (" + MmoBots.Systems.Count + ")", 14f, 52f, w.Width - 28f - 460f);
		w.SmallButton("Develop a New System", w.Width - 474f, 50f, 220f, () => w.Push(new AntiBotNewPage()), open, open ? "Build an anti-bot system for one genre in a development room." : ("Anti-bot technology arrives in " + MmoBots.TechYear[0] + "."), null, 28f);
		w.SmallButton("License One from a Studio", w.Width - 234f, 50f, 220f, () => w.Push(new AntiBotBuyPage()), open, open ? "Rival studios with MMOs license their own systems: a price up front and a monthly fee, and they keep it up to date." : ("Anti-bot technology arrives in " + MmoBots.TechYear[0] + "."), null, 28f);
		// The autopilot's anti-bot rule, here too (it's also on each MMO's autopilot page).
		MmoAutoStudio rules = MmoAutopilot.Rules;
		bool any = MmoAutopilot.AnyOn();
		w.Label("Autopilot updates your own systems", 24f, 86f, 300f, 30f);
		Dropdown rule = w.Dropdown(330f, 86f, 240f, MmoAutopilot.AntiBotSteps.Select((int s) => (s == 0) ? "never" : ("below " + s + "% strength")).ToList(), Mathf.Max(0, Array.IndexOf(MmoAutopilot.AntiBotSteps, rules.AntiBotAt)), (int v) =>
		{
			UIKit.Sound();
			MmoAutopilot.Rules.AntiBotAt = MmoAutopilot.AntiBotSteps[Mathf.Clamp(v, 0, MmoAutopilot.AntiBotSteps.Length - 1)];
			GameUpdates.Touch();
		});
		w.Label(any ? "One update a week at most, in a free development room." : "Only while the autopilot runs at least one MMO (it's off for all of them now).", 584f, 86f, w.Width - 598f, 30f, TextAnchor.MiddleLeft, bold: false, null, 12).color = any ? UIKit.Grey : new Color(0.75f, 0.45f, 0.1f);
		float y = 124f;
		if (MmoBots.Systems.Count == 0)
		{
			w.Label(open ? "You have none yet. Until you do, only your MMOs' own anti-cheat holds bots back." : ("The first anti-bot technology (" + MmoBots.TechNames[0].ToLower() + ") arrives in " + MmoBots.TechYear[0] + ". Until then bots are fewer, and your MMOs' anti-cheat holds some back."), 24f, y, w.Width - 48f, 44f, TextAnchor.UpperLeft, bold: false, null, 13);
			y += 54f;
		}
		else
		{
			float h = Mathf.Min(300f, 6f + 60f * (float)MmoBots.Systems.Count);
			ListView list = w.List(13f, y, w.Width - 26f, h);
			foreach (AntiBotSystem a in MmoBots.Systems)
			{
				SystemRow(w, list, a);
			}
			y += h + 12f;
		}
		List<gameScript> mmos = MmoOps.MyMmos();
		w.Header("Protection of your MMOs (" + mmos.Count + ")", 14f, y, w.Width - 28f);
		y += 32f;
		if (mmos.Count == 0)
		{
			w.Label("You have no MMO on the market.", 24f, y, w.Width - 48f, 30f);
			return;
		}
		ListView games = w.List(13f, y, w.Width - 26f, w.Height - 75f - y - 10f);
		foreach (gameScript g in mmos)
		{
			MmoGame m = MmoOps.Get(g);
			int id = g.myID;
			RowView row = games.Row(50f, () => w.Push(new MmoDashboardPage(id)), "Open its page in MMO Management.");
			AntiBotSystem sys = MmoBots.BestSystem(g, out float power);
			row.Top(g.GetNameWithTag() + "  <color=#666666>" + MmoUi.Genre(g) + "</color>", 12f, row.W - 380f);
			row.Bottom("Bots " + MmoBots.BotsText(g, m) + ((sys != null) ? ("  ·  " + sys.Name + ", " + MmoBots.FitName(MmoBots.GenreFit(sys.Genre, g))) : "  ·  no anti-bot system"), 12f, row.W - 380f);
			float protection = MmoBots.Protection(g);
			row.Bar(row.W - 360f, 340f, 22f, protection, (protection >= 0.6f) ? UIKit.Green : ((protection >= 0.3f) ? new Color(0.9f, 0.6f, 0.1f) : UIKit.Red), "protection " + MmoBots.ProtectionText(g));
		}
	}

	private static void SystemRow(EditorWindow w, ListView list, AntiBotSystem a)
	{
		int id = a.Id;
		RowView row = list.Row(56f, () => w.Push(new AntiBotSystemPage(id)), MmoBots.TechInfo[Mathf.Clamp(a.Tech, 0, 4)]);
		Image icon = row.Picture(UpdateIcons.AntiBot(), 8f, 40f);
		string extra = ((a.Source == -1) ? ((a.Licensees.Count > 0) ? ("  ·  " + a.Licensees.Count + " licensees, " + G.Money(a.Revenue) + " earned") : "") : ("  ·  " + G.Money(a.Fee) + " a month"));
		Text top = row.Top(a.Name + "  <color=#666666>(" + MmoBots.SourceName(a) + ")</color>" + (a.Retired ? "  <color=#c62828>retired</color>" : ""), 58f, row.W - 330f);
		Text bottom = row.Bottom(MmoBots.GenreName(a.Genre) + " MMOs  ·  " + MmoBots.TechName(a.Tech) + extra, 58f, row.W - 330f);
		row.Bar(row.W - 260f, 240f, 22f, a.Strength / 100f, UIKit.ValueColor(Mathf.RoundToInt(a.Strength)), "strength " + Mathf.RoundToInt(a.Strength) + "%");
		if (a.Retired)
		{
			BotUi.Grey(icon, top, bottom);
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

// A new anti-bot system: its name, genre and technology, and the room that builds it.
internal class AntiBotNewPage : Page
{
	private string name = MmoBots.RandomName();

	private int genre = -1;

	private int tech = -1;

	private int roomId = MmoUi.PreferredRoom;

	private string status = "";

	public override string Title => "New Anti-Bot System";

	public override Sprite IconSprite => UpdateIcons.For(UpdateKind.AntiBotBuild);

	public override float Width => 900f;

	public override float Height => 720f;

	public override void Build(EditorWindow w)
	{
		BackButton(w);
		w.Label(G.Money(((bool)G.MS) ? G.MS.money : 0), w.Width - 274f, 10f, 260f, 28f, TextAnchor.MiddleRight, bold: true);
		List<int> genres = BotUi.Genres();
		List<gameScript> mmos = MmoOps.MyMmos();
		if (genre < 0)
		{
			genre = ((mmos.Count > 0) ? mmos[0].maingenre : (genres.FirstOrDefault()));
		}
		int best = MmoBots.BestTech();
		if (tech < 0 || tech > best)
		{
			tech = Mathf.Max(0, best);
		}
		w.Label("An anti-bot system for one genre: it works fully on your MMOs of that genre, partly where it's their second genre, and a little on the rest. Newer technology keeps more bots out and wears down more slowly.", 24f, 48f, w.Width - 48f, 44f, TextAnchor.UpperLeft, bold: false, null, 13);
		float y = 100f;
		w.Label("Name", 24f, y, 130f, 34f, TextAnchor.MiddleLeft, bold: true);
		w.Input(name, "Name", 160f, y, 360f, (string v) => name = v);
		w.SmallButton("Random Name", 532f, y + 3f, 160f, () =>
		{
			name = MmoBots.RandomName();
			w.Rebuild();
		}, enabled: true, null, null, 28f);
		y += 44f;
		w.Label("Genre", 24f, y, 130f, 30f, TextAnchor.MiddleLeft, bold: true);
		int gi = Mathf.Max(0, genres.IndexOf(genre));
		w.Dropdown(160f, y, 360f, genres.Select((int x) => MmoBots.GenreName(x) + (mmos.Any((gameScript g) => g.maingenre == x) ? "  (your MMOs)" : "")).ToList(), gi, (int v) =>
		{
			UIKit.Sound();
			genre = genres[Mathf.Clamp(v, 0, genres.Count - 1)];
			w.Rebuild();
		});
		y += 42f;
		w.Label("Technology", 24f, y, 130f, 30f, TextAnchor.MiddleLeft, bold: true);
		List<string> techs = new List<string>();
		for (int t = 0; t <= Mathf.Max(0, best); t++)
		{
			techs.Add(MmoBots.TechNames[t] + " (" + MmoBots.TechYear[t] + ", keeps out up to " + BotUi.Pct(MmoBots.TechPower[t]) + ")");
		}
		w.Dropdown(160f, y, w.Width - 174f, techs, tech, (int v) =>
		{
			UIKit.Sound();
			tech = v;
			w.Rebuild();
		});
		y += 36f;
		w.Label(MmoBots.TechInfo[Mathf.Clamp(tech, 0, 4)], 160f, y, w.Width - 174f, 36f, TextAnchor.UpperLeft, bold: false, null, 12);
		y += 42f;
		UpdatePlan plan = MmoBots.PlanNew(tech, genre);
		w.Label("Room", 24f, y, 130f, 30f, TextAnchor.MiddleLeft, bold: true);
		roomId = MmoUi.RoomDropdown(w, 160f, y, w.Width - 174f, roomId, plan.Work, UpdateKind.AntiBotBuild, (int v) =>
		{
			roomId = v;
			w.Rebuild();
		});
		y += 44f;
		roomScript room = CopyProtectDev.FindRoom(roomId);
		List<string> works = mmos.Select((gameScript g) => g.GetNameSimple() + " " + BotUi.Pct(MmoBots.GenreFit(genre, g))).ToList();
		List<KeyValuePair<string, string>> facts = new List<KeyValuePair<string, string>>
		{
			EditorWindow.KV("Costs", G.Money(plan.Cost)),
			EditorWindow.KV("Time", room ? GameUpdates.Duration(plan.Work, room, UpdateKind.AntiBotBuild) : "-"),
			EditorWindow.KV("Strength at release", room ? (Mathf.RoundToInt(MmoBots.ReleaseStrength(room)) + "%" + ((CopyProtectDev.TeamPenalty(room) > 0f) ? " (inexperienced programmers)" : "")) : "-"),
			EditorWindow.KV("Keeps out at full strength", BotUi.Pct(MmoBots.TechPower[Mathf.Clamp(tech, 0, 4)]) + " of the bots in its genre")
		};
		y += w.Facts(14f, y, w.Width - 28f, facts, 2) + 6f;
		w.Label((works.Count > 0) ? ("Works on your MMOs: " + string.Join(", ", works)) : "You have no MMO on the market yet.", 24f, y, w.Width - 48f, 40f, TextAnchor.UpperLeft, bold: false, null, 13);
		y += 46f;
		bool ok = plan.Problem == null && (bool)room;
		w.BigButton("Start Development", 14f, y, 260f, 44f, () =>
		{
			UpdateProject p = new UpdateProject
			{
				Kind = UpdateKind.AntiBotBuild,
				Genre = genre,
				Tech = tech,
				NewName = (string.IsNullOrEmpty(name?.Trim()) ? MmoBots.RandomName() : name.Trim())
			};
			p.Title = "Anti-bot system: " + p.NewName;
			string error = GameUpdates.Start(p, MmoBots.PlanNew(tech, genre), CopyProtectDev.FindRoom(roomId), null);
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
		Text line = w.Label(status ?? plan.Problem ?? "", 290f, y, w.Width - 304f, 44f, TextAnchor.MiddleLeft, bold: false, null, 13);
		line.color = UIKit.Red;
		if (string.IsNullOrEmpty(status) && plan.Problem != null)
		{
			line.text = plan.Problem;
		}
	}
}

// One anti-bot system: its state, updates and licensing (your own), or the licence (a rival studio's).
internal class AntiBotSystemPage : Page
{
	private readonly int id;

	private int roomId = MmoUi.PreferredRoom;

	private string status = "";

	private bool statusGood;

	private bool confirmEnd;

	private int seen = -1;

	public AntiBotSystemPage(int id)
	{
		this.id = id;
	}

	public override string Title => MmoBots.Find(id)?.Name ?? "Anti-bot system";

	public override Sprite IconSprite => UpdateIcons.AntiBot();

	public override float Width => 960f;

	public override float Height => 760f;

	public override void Build(EditorWindow w)
	{
		seen = GameUpdates.Version;
		BackButton(w);
		w.Label(G.Money(((bool)G.MS) ? G.MS.money : 0), w.Width - 274f, 10f, 260f, 28f, TextAnchor.MiddleRight, bold: true);
		AntiBotSystem a = MmoBots.Find(id);
		if (a == null)
		{
			w.Label("This system is gone.", 24f, 60f, w.Width - 48f, 30f);
			return;
		}
		bool own = a.Source == -1;
		w.Label("<b>" + a.Name + "</b>  <color=#666666>" + MmoBots.SourceName(a) + "</color>", 116f, 10f, w.Width - 400f, 28f, TextAnchor.MiddleLeft, bold: false, null, 16);
		w.Label(MmoBots.TechInfo[Mathf.Clamp(a.Tech, 0, 4)], 24f, 46f, w.Width - 48f, 36f, TextAnchor.UpperLeft, bold: false, null, 13);
		w.Bar(14f, 86f, w.Width - 28f, 24f, a.Strength / 100f, "Strength " + Mathf.RoundToInt(a.Strength) + "%  ·  keeps out up to " + BotUi.Pct(MmoBots.SystemPower(a)) + " of the bots in " + MmoBots.GenreName(a.Genre) + " MMOs", UIKit.ValueColor(Mathf.RoundToInt(a.Strength)));
		List<KeyValuePair<string, string>> facts = new List<KeyValuePair<string, string>>
		{
			EditorWindow.KV("Genre", MmoBots.GenreName(a.Genre)),
			EditorWindow.KV("Technology", MmoBots.TechName(a.Tech)),
			EditorWindow.KV("Last update", (a.Updates > 0) ? (MmoUi.Years((float)(MmoOps.Now - a.Updated) / 52f) + " ago") : "never"),
			EditorWindow.KV(own ? "Wear per week" : "Monthly fee", own ? (a.Retired ? "-" : (MmoBots.WeeklyWear(a).ToString("0.0") + " points")) : G.Money(a.Fee))
		};
		if (own)
		{
			facts.Add(EditorWindow.KV("Licensees", a.Licensees.Count.ToString()));
			facts.Add(EditorWindow.KV("Licence income", G.Money(a.Revenue)));
		}
		float y = 120f + w.Facts(14f, 120f, w.Width - 28f, facts, 2) + 6f;
		Text line = w.Label(status, 24f, y, w.Width - 48f, 24f, TextAnchor.MiddleLeft, bold: false, null, 13);
		line.color = statusGood ? UIKit.Green : UIKit.Red;
		y += 26f;
		y = (own ? OwnBlock(w, a, y) : LicensedBlock(w, a, y));
		List<gameScript> mmos = MmoOps.MyMmos();
		w.Header("On your MMOs", 14f, y, w.Width - 28f);
		y += 32f;
		ListView list = w.List(13f, y, w.Width - 26f, Mathf.Max(60f, w.Height - 75f - y - 10f));
		foreach (gameScript g in mmos)
		{
			float fit = MmoBots.GenreFit(a.Genre, g);
			bool used = MmoBots.BestSystem(g, out float _) == a;
			RowView row = list.Row(40f);
			row.Text(g.GetNameWithTag() + "  <color=#666666>" + MmoUi.Genre(g) + "</color>", 12f, row.W - 420f, bold: true);
			row.Text(BotUi.Pct(fit) + " effect (" + MmoBots.FitName(fit) + ")" + (used ? "  ·  <color=#2e7d32>in use</color>" : "  ·  another works better"), row.W - 410f, 400f, bold: false, TextAnchor.MiddleRight);
		}
	}

	private float OwnBlock(EditorWindow w, AntiBotSystem a, float y)
	{
		w.Header("Update", 14f, y, w.Width - 28f);
		y += 32f;
		UpdatePlan plan = MmoBots.PlanUpdate(a);
		if (plan.Problem != null)
		{
			w.Label(plan.Problem, 24f, y, w.Width - 48f, 28f, TextAnchor.MiddleLeft, bold: false, null, 13);
			y += 34f;
		}
		else
		{
			roomId = MmoUi.RoomDropdown(w, 24f, y, w.Width - 330f, roomId, plan.Work, UpdateKind.AntiBotUpdate, (int v) =>
			{
				roomId = v;
				w.Rebuild();
			});
			roomScript room = CopyProtectDev.FindRoom(roomId);
			int systemId = a.Id;
			string title = "Anti-bot update: " + a.Name;
			w.BigButton("Update for " + G.Money(plan.Cost), w.Width - 294f, y - 4f, 280f, 38f, () =>
			{
				UpdateProject p = new UpdateProject
				{
					Kind = UpdateKind.AntiBotUpdate,
					SystemId = systemId,
					Title = title
				};
				string error = GameUpdates.Start(p, MmoBots.PlanUpdate(MmoBots.Find(systemId)), CopyProtectDev.FindRoom(roomId), null);
				if (error != null)
				{
					status = error;
					statusGood = false;
					w.Rebuild();
					return;
				}
				UIKit.Sound();
				MmoUi.PreferredRoom = -1;
				w.Push(new UpdateProjectPage(p.TaskId));
			}, (bool)room, room ? ("Back to " + Mathf.RoundToInt(MmoBots.ReleaseStrength(room)) + "% strength, " + GameUpdates.Duration(plan.Work, room, UpdateKind.AntiBotUpdate) + ".") : "Pick a free development room with staff.");
			y += 44f;
		}
		w.Header("Licensing", 14f, y, w.Width - 28f);
		y += 32f;
		w.Dropdown(24f, y, 300f, MmoBots.LicensingNames, a.Licensing, (int v) =>
		{
			UIKit.Sound();
			string note = MmoBots.SetLicensing(a, v);
			status = note ?? "";
			statusGood = false;
			w.Rebuild();
		});
		if (a.Licensing == 1)
		{
			List<string> fees = MmoBots.FeeSteps.Select((float f) => G.Money((long)Mathf.Round((float)MmoBots.BaseFee(a.Tech) * f / 100f) * 100) + " a month" + ((Mathf.Abs(f - 1f) < 0.01f) ? " (usual)" : "")).ToList();
			w.Dropdown(336f, y, 280f, fees, a.FeeStep, (int v) =>
			{
				UIKit.Sound();
				a.FeeStep = Mathf.Clamp(v, 0, MmoBots.FeeSteps.Length - 1);
				GameUpdates.Touch();
			});
		}
		w.SmallButton(a.Retired ? "Bring It Back" : "Retire It", w.Width - 234f, y + 1f, 220f, () =>
		{
			MmoBots.SetRetired(a, !a.Retired);
			w.Rebuild();
		}, enabled: true, a.Retired ? "Use it on your MMOs again." : "Stop using it (licensees lose it too).", null, 28f);
		y += 38f;
		string who = string.Join(", ", a.Licensees.Select((int x) => MmoBots.FindStudio(x)).Where((publisherScript s) => (bool)s).Select((publisherScript s) => s.GetName()));
		string info = ((a.Licensing == 1) ? ("Rival studios with MMOs sign up when it beats their own system; a cheaper fee brings more of them. Licensees wear it down faster, and their MMOs keep bots out better, which helps them against yours." + ((who.Length > 0) ? (" Licensed by: " + who + ".") : "")) : "Only your own MMOs use it.");
		w.Label(info, 24f, y, w.Width - 48f, 40f, TextAnchor.UpperLeft, bold: false, null, 12);
		return y + 46f;
	}

	private float LicensedBlock(EditorWindow w, AntiBotSystem a, float y)
	{
		w.Header("Licence", 14f, y, w.Width - 28f);
		y += 32f;
		w.Label("You pay " + G.Money(a.Fee) + " a month. Its studio patches it twice a year and moves it to newer technology when they have it. It ends if the studio closes.", 24f, y, w.Width - 300f, 40f, TextAnchor.UpperLeft, bold: false, null, 13);
		w.SmallButton(confirmEnd ? "Really End It?" : "End the Licence", w.Width - 234f, y + 4f, 220f, () =>
		{
			if (!confirmEnd)
			{
				confirmEnd = true;
				w.Rebuild();
				return;
			}
			MmoBots.EndLicence(a);
			w.Back();
		}, enabled: true, "Stop paying for it: your MMOs lose its protection.", null, 30f);
		return y + 50f;
	}

	public override void Tick(EditorWindow w)
	{
		if (GameUpdates.Version != seen)
		{
			w.Rebuild();
		}
	}
}

// Rival studios' anti-bot systems you can license.
internal class AntiBotBuyPage : Page
{
	private string status = "";

	private int seen = -1;

	public override string Title => "License an Anti-Bot System";

	public override Sprite IconSprite => UpdateIcons.AntiBot();

	public override float Width => 980f;

	public override float Height => 680f;

	public override void Build(EditorWindow w)
	{
		seen = GameUpdates.Version;
		BackButton(w);
		w.Label(G.Money(((bool)G.MS) ? G.MS.money : 0), w.Width - 274f, 10f, 260f, 28f, TextAnchor.MiddleRight, bold: true);
		w.AboutLine("Studios with MMOs of their own license their anti-bot systems: a price up front, then a monthly fee, and they keep it up to date. Each is built for the genre the studio makes most.");
		Text line = w.Label(status, 24f, 50f, w.Width - 48f, 24f, TextAnchor.MiddleLeft, bold: false, null, 13);
		line.color = UIKit.Red;
		List<AntiBotOffer> offers = MmoBots.Offers();
		List<gameScript> mmos = MmoOps.MyMmos();
		if (offers.Count == 0)
		{
			w.Label((MmoBots.BestTech() < 0) ? ("Anti-bot technology arrives in " + MmoBots.TechYear[0] + ".") : "No other studio has an MMO on the market right now, or you already license theirs.", 24f, 84f, w.Width - 48f, 44f);
			return;
		}
		ListView list = w.List(13f, 80f, w.Width - 26f, w.Height - 75f - 90f);
		foreach (AntiBotOffer o in offers)
		{
			AntiBotOffer offer = o;
			RowView row = list.Row(60f, null, MmoBots.TechInfo[Mathf.Clamp(o.Tech, 0, 4)]);
			row.Picture(o.Studio.GetLogo(), 8f, 44f);
			int fits = mmos.Count((gameScript g) => g.maingenre == o.Genre);
			row.Top(o.Name + "  <color=#666666>by " + o.Studio.GetName() + "</color>", 62f, row.W - 400f);
			row.Bottom(MmoBots.GenreName(o.Genre) + " MMOs" + ((fits > 0) ? (" (" + fits + " of yours)") : " (none of yours)") + "  ·  " + MmoBots.TechName(o.Tech) + "  ·  " + Mathf.RoundToInt(o.Strength) + "%", 62f, row.W - 400f);
			row.Text(G.Money(o.Price) + " now", row.W - 330f, 180f, bold: true, TextAnchor.MiddleRight, 6f, 24f);
			row.Text("+ " + G.Money(o.Fee) + " a month", row.W - 330f, 180f, bold: false, TextAnchor.MiddleRight, 30f, 24f);
			row.Button("License", row.W - 136f, 124f, () =>
			{
				string error = MmoBots.Buy(offer);
				status = error ?? "";
				w.Rebuild();
			}, enabled: true, "Pay " + G.Money(o.Price) + " now and " + G.Money(o.Fee) + " each month.");
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

// What the autopilot may start for one MMO, and its rules for all of them.
internal class MmoAutopilotPage : Page
{
	private readonly int gameId;

	public MmoAutopilotPage(int gameId)
	{
		this.gameId = gameId;
	}

	public override string Title => "MMO Autopilot";

	public override Sprite IconSprite => UpdateIcons.Autopilot();

	public override float Width => 880f;

	public override float Height => 720f;

	public override void Build(EditorWindow w)
	{
		BackButton(w);
		gameScript g = GameUpdates.FindGame(gameId);
		if (!g || !MmoOps.IsMyMmo(g))
		{
			w.Label("This MMO is no longer on the market.", 24f, 60f, w.Width - 48f, 30f);
			return;
		}
		MmoGame m = MmoOps.Get(g);
		MmoAuto auto = MmoAutopilot.For(m);
		MmoAutoStudio rules = MmoAutopilot.Rules;
		w.Label("<b>" + g.GetNameWithTag() + "</b>", 116f, 10f, w.Width - 130f, 28f, TextAnchor.MiddleLeft, bold: false, null, 16);
		w.Label("Once a week the autopilot starts the routine projects you allow here, one at a time, in a free development room with staff, and tells you each time. It never goes below your money floor and leaves the rooms you ask for free.", 24f, 46f, w.Width - 48f, 44f, TextAnchor.UpperLeft, bold: false, null, 13);
		float y = 96f;
		w.Toggle("Autopilot on for " + g.GetNameSimple(), 24f, y, w.Width - 48f, auto.On, (bool v) =>
		{
			auto.On = v;
			GameUpdates.Touch();
			w.Rebuild();
		});
		y += 40f;
		w.Header("What it may start", 14f, y, w.Width - 28f);
		y += 34f;
		bool bots = MmoBots.On;
		w.Toggle("Bot ban waves, when bots reach", 24f, y, 400f, auto.Bust && bots, (bool v) => auto.Bust = v, bots ? null : "Bots are switched off in the addon's settings.");
		w.Dropdown(440f, y - 2f, 220f, MmoAutopilot.BustSteps.Select((int s) => s + "% of its players").ToList(), Mathf.Max(0, Array.IndexOf(MmoAutopilot.BustSteps, auto.BustAt)), (int v) =>
		{
			UIKit.Sound();
			auto.BustAt = MmoAutopilot.BustSteps[Mathf.Clamp(v, 0, MmoAutopilot.BustSteps.Length - 1)];
		});
		y += 38f;
		w.Toggle("Stability patches, at", 24f, y, 400f, auto.Stability, (bool v) => auto.Stability = v);
		w.Dropdown(440f, y - 2f, 220f, MmoAutopilot.BugSteps.Select((int s) => s + " bugs or more").ToList(), Mathf.Max(0, Array.IndexOf(MmoAutopilot.BugSteps, auto.StabilityAt)), (int v) =>
		{
			UIKit.Sound();
			auto.StabilityAt = MmoAutopilot.BugSteps[Mathf.Clamp(v, 0, MmoAutopilot.BugSteps.Length - 1)];
		});
		y += 38f;
		w.Toggle("Content patches, whenever the next one can start", 24f, y, w.Width - 48f, auto.Patch, (bool v) => auto.Patch = v);
		y += 34f;
		w.Toggle("Classic phases, whenever the next one can start" + (m.ClassicOn ? "" : " (once it has classic realms)"), 24f, y, w.Width - 48f, auto.ClassicPhase, (bool v) => auto.ClassicPhase = v);
		y += 34f;
		w.Toggle("Welcome-back campaigns, whenever enough former players can be won back", 24f, y, w.Width - 48f, auto.Welcome, (bool v) => auto.Welcome = v);
		y += 44f;
		w.Header("Rules for all your MMOs", 14f, y, w.Width - 28f);
		y += 34f;
		w.Label("Keep at least", 24f, y, 260f, 30f);
		w.Dropdown(290f, y, 260f, MmoAutopilot.MoneyFloors.Select((long x) => G.Money(x)).ToList(), Mathf.Max(0, Array.IndexOf(MmoAutopilot.MoneyFloors, rules.MoneyFloor)), (int v) =>
		{
			UIKit.Sound();
			rules.MoneyFloor = MmoAutopilot.MoneyFloors[Mathf.Clamp(v, 0, MmoAutopilot.MoneyFloors.Length - 1)];
		});
		y += 40f;
		w.Label("Leave free for you", 24f, y, 260f, 30f);
		w.Dropdown(290f, y, 260f, new List<string> { "no rooms", "1 development room", "2 development rooms", "3 development rooms" }, Mathf.Clamp(rules.KeepFree, 0, 3), (int v) =>
		{
			UIKit.Sound();
			rules.KeepFree = Mathf.Clamp(v, 0, 3);
		});
		y += 40f;
		w.Label("Update your anti-bot systems", 24f, y, 260f, 30f);
		w.Dropdown(290f, y, 260f, MmoAutopilot.AntiBotSteps.Select((int s) => (s == 0) ? "never" : ("below " + s + "% strength")).ToList(), Mathf.Max(0, Array.IndexOf(MmoAutopilot.AntiBotSteps, rules.AntiBotAt)), (int v) =>
		{
			UIKit.Sound();
			rules.AntiBotAt = MmoAutopilot.AntiBotSteps[Mathf.Clamp(v, 0, MmoAutopilot.AntiBotSteps.Length - 1)];
		});
		y += 44f;
		w.Label("Bigger projects (overhauls, expansions, migrations, new platforms, expansion packs) stay yours to start.", 24f, y, w.Width - 48f, 30f, TextAnchor.UpperLeft, bold: false, null, 12);
	}
}

// Classic servers: the MMO as it was at launch, as realms in the same subscription or relaunched as its own game.
internal class MmoClassicPage : Page
{
	private readonly int gameId;

	private int option;

	private string name;

	private int roomId = MmoUi.PreferredRoom;

	private string status = "";

	private int seen = -1;

	public MmoClassicPage(int gameId)
	{
		this.gameId = gameId;
	}

	private gameScript Game => GameUpdates.FindGame(gameId);

	public override string Title => GameUpdates.KindName(UpdateKind.MmoClassic);

	public override Sprite IconSprite => UpdateIcons.For(UpdateKind.MmoClassic);

	public override float Width => 900f;

	public override float Height => 700f;

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
		if (name == null)
		{
			name = m.ClassicName ?? MmoLive.DefaultClassicName(g);
		}
		if (m.ClassicOn)
		{
			option = 1;
		}
		MmoUi.Shot(w, g, 14f, 48f, 120f, 68f);
		w.Label("<b>" + g.GetNameWithTag() + "</b>", 146f, 48f, w.Width - 160f, 28f, TextAnchor.MiddleLeft, bold: false, null, 17);
		w.Label(MmoUi.Years(MmoOps.AgeYears(g)) + " on the market  ·  " + MmoUiText.Num(MmoLive.Lapsed(g)) + " former players  ·  " + MmoLive.Changes(g, m) + " big changes since launch", 146f, 78f, w.Width - 160f, 24f, TextAnchor.MiddleLeft, bold: false, null, 13);
		float y = 126f;
		w.Label(m.ClassicOn ? ("Relaunch " + m.ClassicName + " as its own MMO, with its own subscription, card and place in the charts. Its realms' players move over to it.") : "Bring back the MMO as it was at launch, for the players who miss it. Classic realms run inside the same subscription (switch between the live game and the classic realms on its page) and get content phases. Or relaunch it as its own MMO, run old-school, with its own subscription.", 24f, y, w.Width - 48f, 56f, TextAnchor.UpperLeft, bold: false, null, 13);
		y += 62f;
		string problem = GameUpdates.Problem(UpdateKind.MmoClassic, g);
		UpdatePlan plan = MmoLive.PlanClassic(g, option);
		problem = problem ?? plan.Problem;
		if (problem != null)
		{
			w.Info(problem, 14f, y, w.Width - 28f, 60f);
			return;
		}
		if (!m.ClassicOn)
		{
			w.Label("How", 24f, y, 130f, 30f, TextAnchor.MiddleLeft, bold: true);
			w.Dropdown(160f, y, w.Width - 174f, new List<string> { "Classic realms in the same subscription", "Relaunch it as its own game" }, option, (int v) =>
			{
				UIKit.Sound();
				option = Mathf.Clamp(v, 0, 1);
				w.Rebuild();
			});
			y += 42f;
		}
		w.Label("Name", 24f, y, 130f, 34f, TextAnchor.MiddleLeft, bold: true);
		w.Input(name, "Name", 160f, y, 420f, (string v) => name = v);
		y += 44f;
		w.Label("Room", 24f, y, 130f, 30f, TextAnchor.MiddleLeft, bold: true);
		roomId = MmoUi.RoomDropdown(w, 160f, y, w.Width - 174f, roomId, plan.Work, UpdateKind.MmoClassic, (int v) =>
		{
			roomId = v;
			w.Rebuild();
		});
		y += 44f;
		roomScript room = CopyProtectDev.FindRoom(roomId);
		List<KeyValuePair<string, string>> facts = new List<KeyValuePair<string, string>>
		{
			EditorWindow.KV("Costs", G.Money(plan.Cost)),
			EditorWindow.KV("Time", room ? GameUpdates.Duration(plan.Work, room, UpdateKind.MmoClassic) : "-"),
			EditorWindow.KV("Former players", MmoUiText.Num(MmoLive.Lapsed(g)) + " could come back"),
			EditorWindow.KV("Afterwards", (option == 0) ? "classic realms in its subscription, with phases" : "a separate old-school MMO")
		};
		y += w.Facts(14f, y, w.Width - 28f, facts, 2) + 8f;
		if (plan.Notes.Length > 0)
		{
			w.Label(plan.Notes, 24f, y, w.Width - 48f, 30f, TextAnchor.UpperLeft, bold: false, null, 13);
			y += 34f;
		}
		bool ok = (bool)room;
		w.BigButton("Start the Project", 14f, y, 260f, 44f, () =>
		{
			gameScript game = Game;
			UpdateProject p = new UpdateProject
			{
				Kind = UpdateKind.MmoClassic,
				Option = option,
				NewName = name
			};
			string error = GameUpdates.Start(p, MmoLive.PlanClassic(game, option), CopyProtectDev.FindRoom(roomId), game);
			if (error != null)
			{
				status = error;
				w.Rebuild();
				return;
			}
			UIKit.Sound();
			MmoUi.PreferredRoom = -1;
			w.Replace(new UpdateProjectPage(p.TaskId));
		}, ok, ok ? ("Costs " + G.Money(plan.Cost) + ", paid now.") : "Pick a free development room with staff.");
		Text line = w.Label(status, 290f, y, w.Width - 304f, 44f, TextAnchor.MiddleLeft, bold: false, null, 13);
		line.color = UIKit.Red;
	}

	public override void Tick(EditorWindow w)
	{
		if (GameUpdates.Version != seen)
		{
			w.Rebuild();
		}
	}
}

// The yearly fan festival pop-up.
internal class MmoFanFestPage : Page
{
	private string result;

	private string status = "";

	public override string Title => MmoFanFest.FestName();

	public override Sprite IconSprite => UpdateIcons.FanFest();

	public override float Width => 900f;

	public override float Height => 720f;

	public override void Build(EditorWindow w)
	{
		w.Label(G.Money(((bool)G.MS) ? G.MS.money : 0), w.Width - 274f, 10f, 260f, 28f, TextAnchor.MiddleRight, bold: true);
		if (result != null)
		{
			w.Label(result, 24f, 50f, w.Width - 48f, 300f, TextAnchor.UpperLeft, bold: false, null, 15);
			w.BigButton("Close", 14f, w.Height - 75f - 60f, 220f, 44f, w.Close);
			return;
		}
		List<gameScript> mmos = MmoOps.MyMmos();
		gameScript g = MmoFanFest.Featured();
		if (!g)
		{
			w.Label("You have no MMO on the market to feature this year.", 24f, 60f, w.Width - 48f, 30f);
			w.BigButton("Close", 14f, w.Height - 75f - 60f, 220f, 44f, () =>
			{
				MmoFanFest.Skip();
				w.Close();
			});
			return;
		}
		FanFestChoice c = MmoFanFest.Choice;
		c.GameId = g.myID;
		w.Label("Once a year your players gather to celebrate your MMOs: panels, a show floor, cosplay and the big announcements. Pick the MMO to feature and how big it gets, or skip it this year.", 24f, 46f, w.Width - 300f, 50f, TextAnchor.UpperLeft, bold: false, null, 13);
		float y = 104f;
		w.Label("Featured MMO", 24f, y, 180f, 30f, TextAnchor.MiddleLeft, bold: true);
		w.Dropdown(210f, y, w.Width - 224f, mmos.Select((gameScript x) => x.GetNameSimple() + "  (" + MmoUi.Short(x.abonnements) + " subscribers)").ToList(), Mathf.Max(0, mmos.IndexOf(g)), (int v) =>
		{
			UIKit.Sound();
			c.GameId = mmos[Mathf.Clamp(v, 0, mmos.Count - 1)].myID;
			if (c.Announce == 3 && !MmoFanFest.ClassicAnnounceable(MmoFanFest.Featured()))
			{
				c.Announce = 0;
			}
			w.Rebuild();
		});
		y += 42f;
		w.Label("Venue", 24f, y, 180f, 30f, TextAnchor.MiddleLeft, bold: true);
		List<string> venues = new List<string>();
		for (int i = 0; i < MmoFanFest.VenueNames.Length; i++)
		{
			FanFestChoice probe = new FanFestChoice
			{
				Venue = i,
				Merch = false,
				Panels = false
			};
			FanFestOutcome po = MmoFanFest.Estimate(probe, g);
			venues.Add(MmoFanFest.VenueNames[i] + ((po.Seats > 0) ? (" (" + MmoUiText.Num(po.Seats) + " seats") : " (online") + ", " + G.Money(po.Cost) + ")");
		}
		w.Dropdown(210f, y, w.Width - 224f, venues, c.Venue, (int v) =>
		{
			UIKit.Sound();
			c.Venue = Mathf.Clamp(v, 0, MmoFanFest.VenueNames.Length - 1);
			w.Rebuild();
		});
		y += 42f;
		w.Label("Announcement", 24f, y, 180f, 30f, TextAnchor.MiddleLeft, bold: true);
		int count = (MmoFanFest.ClassicAnnounceable(g) ? 4 : 3);
		w.Dropdown(210f, y, w.Width - 224f, MmoFanFest.AnnounceNames.Take(count).ToList(), Mathf.Clamp(c.Announce, 0, count - 1), (int v) =>
		{
			UIKit.Sound();
			c.Announce = v;
			w.Rebuild();
		});
		y += 44f;
		w.Toggle("In-game gifts for every player (brings former players back)", 24f, y, w.Width - 48f, c.Gifts, (bool v) =>
		{
			c.Gifts = v;
			w.Rebuild();
		});
		y += 34f;
		w.Toggle("Merch store", 24f, y, 400f, c.Merch, (bool v) =>
		{
			c.Merch = v;
			w.Rebuild();
		});
		w.Toggle("Developer panels and Q&A", 440f, y, w.Width - 464f, c.Panels, (bool v) =>
		{
			c.Panels = v;
			w.Rebuild();
		});
		y += 44f;
		FanFestOutcome o = MmoFanFest.Estimate(c, g);
		List<KeyValuePair<string, string>> facts = new List<KeyValuePair<string, string>>
		{
			EditorWindow.KV("Costs", G.Money(o.Cost)),
			EditorWindow.KV("Merch income", (o.Merch > 0) ? ("about " + G.Money(o.Merch)) : "-"),
			EditorWindow.KV("Hype", "+" + Mathf.RoundToInt(o.Hype)),
			EditorWindow.KV("Interest", "+" + Mathf.RoundToInt(o.Interest)),
			EditorWindow.KV("New fans", "about " + MmoUiText.Num(o.Fans)),
			EditorWindow.KV("Former players back", (o.Returning > 0) ? ("about " + MmoUiText.Num(o.Returning)) : "-"),
			EditorWindow.KV("User reviews", (o.Reviews > 0f) ? ("+" + Mathf.Max(1, Mathf.RoundToInt(o.Reviews * 100f)) + "%") : "-"),
			EditorWindow.KV("Studio reputation", (c.Venue >= 2) ? ("+" + (c.Venue - 1)) : "-")
		};
		y += w.Facts(14f, y, w.Width - 28f, facts, 2) + 10f;
		Text line = w.Label(status, 24f, y, w.Width - 48f, 24f, TextAnchor.MiddleLeft, bold: false, null, 13);
		line.color = UIKit.Red;
		y = w.Height - 75f - 60f;
		w.BigButton("Hold the FanFest", 14f, y, 280f, 44f, () =>
		{
			string error = MmoFanFest.Hold(out string report);
			if (error != null)
			{
				status = error;
				w.Rebuild();
				return;
			}
			UIKit.Sound();
			result = report;
			w.Rebuild();
		}, enabled: true, "Pay " + G.Money(o.Cost) + " now.");
		w.SmallButton("Skip This Year", w.Width - 234f, y + 8f, 220f, () =>
		{
			MmoFanFest.Skip();
			w.Close();
		}, enabled: true, "No fan festival this year.", null, 30f);
	}
}
