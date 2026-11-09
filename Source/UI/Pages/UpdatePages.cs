using System.Collections.Generic;
using System.Linq;
using HolyAddon.Core;
using HolyAddon.Core.CopyProtection;
using HolyAddon.Core.Updates;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI.Pages;

// Shared pieces of the update pages: the game's header, the room line, the start button and status line.
internal abstract class UpdatePageBase : Page
{
	protected readonly int gameId;

	protected int roomId;

	// The MMO version, opened from MMO Management: Back goes there, the room is picked here, effects on players shown.
	protected readonly bool mmo;

	protected string status = "";

	protected int seen = -1;

	protected UpdatePageBase(int gameId, int roomId, bool mmo)
	{
		this.gameId = gameId;
		this.roomId = roomId;
		this.mmo = mmo;
	}

	protected gameScript Game => GameUpdates.FindGame(gameId);

	protected roomScript Room => CopyProtectDev.FindRoom(roomId);

	protected abstract UpdateKind BaseKind { get; }

	protected UpdateKind Kind
	{
		get
		{
			if (!mmo)
			{
				return BaseKind;
			}
			return GameUpdates.MmoOf(BaseKind);
		}
	}

	// Room for the MMO version's room picker.
	protected float Extra
	{
		get
		{
			if (!mmo)
			{
				return 0f;
			}
			return 44f;
		}
	}

	public override Sprite IconSprite => UpdateIcons.For(Kind);

	public override string Title => GameUpdates.KindName(Kind);

	// Draws the game header and returns the y below it, or -1 if the game can't take this update any more.
	protected float Header(EditorWindow w)
	{
		seen = GameUpdates.Version;
		// Back leads to the game's update list this page was picked from (same update type, same room).
		w.SmallButton("< Back", 14f, 10f, 90f, () =>
		{
			roomScript room = Room;
			if (mmo || !room)
			{
				w.Back();
				return;
			}
			w.Close();
			UpdateMenus.OpenListFor(Kind, room);
		}, enabled: true, null, null, 28f);
		w.Label(G.Money(((bool)G.MS) ? G.MS.money : 0), w.Width - 274f, 10f, 260f, 28f, TextAnchor.MiddleRight, bold: true);
		gameScript g = Game;
		if (!g)
		{
			w.Label("This game no longer exists.", 24f, 60f, w.Width - 48f, 30f);
			return -1f;
		}
		Image shot = new GameObject("Shot", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
		shot.transform.SetParent(w.Body, worldPositionStays: false);
		UIKit.Place(shot.gameObject, 14f, 48f, 120f, 68f);
		shot.sprite = g.GetScreenshot();
		shot.preserveAspect = true;
		w.Label("<b>" + g.GetNameWithTag() + "</b>", 146f, 48f, w.Width - 160f, 28f, TextAnchor.MiddleLeft, bold: false, null, 17);
		w.Label(G.Text(277, "Review") + " " + g.reviewTotal + "%  ·  " + g.weeksOnMarket + " weeks on the market  ·  " + G.MS.GetMoney(g.sellsTotal, showDollar: false) + " sold", 146f, 78f, w.Width - 160f, 24f, TextAnchor.MiddleLeft, bold: false, null, 13);
		string problem = GameUpdates.Problem(Kind, g);
		if (problem != null)
		{
			w.Label(problem, 24f, 130f, w.Width - 48f, 44f);
			return -1f;
		}
		if (mmo)
		{
			w.Label("Room", 24f, 128f, 110f, 30f, TextAnchor.MiddleLeft, bold: true);
			roomId = MmoUi.RoomDropdown(w, 140f, 128f, w.Width - 154f, roomId, 0f, Kind, (int v) =>
			{
				roomId = v;
				w.Rebuild();
			});
			return 172f;
		}
		return 128f;
	}

	protected string RoomLine(float work)
	{
		roomScript r = Room;
		if (!r)
		{
			return "-";
		}
		return GameUpdates.Duration(work, r, Kind) + " in " + CopyProtectDev.RoomName(r);
	}

	protected void StartButton(EditorWindow w, float y, UpdatePlan plan, System.Func<UpdateProject> make)
	{
		bool ok = plan.Problem == null;
		w.BigButton("Start the update", 14f, y, 260f, 44f, () =>
		{
			gameScript g = Game;
			UpdateProject p = make();
			string error = GameUpdates.Start(p, plan, Room, g);
			if (error != null)
			{
				status = error;
				w.Rebuild();
				return;
			}
			UIKit.Sound();
			w.Replace(new UpdateProjectPage(p.TaskId));
		}, ok, ok ? ("Costs " + G.Money(plan.Cost) + ", paid now.") : plan.Problem);
		Text line = w.Label(ok ? status : (plan.Problem ?? status), 290f, y, w.Width - 304f, 44f, TextAnchor.MiddleLeft, bold: false, null, 13);
		line.color = UIKit.Red;
	}

	protected static string Points(UpdatePlan plan)
	{
		List<string> parts = new List<string>();
		if (plan.Gameplay >= 0.5f)
		{
			parts.Add("gameplay +" + Mathf.RoundToInt(plan.Gameplay));
		}
		if (plan.Gameplay <= -0.5f)
		{
			parts.Add("gameplay " + Mathf.RoundToInt(plan.Gameplay));
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

// Update the engine: move the game to better engine features from its own engine or another you own or license.
internal class EngineUpdatePage : UpdatePageBase
{
	private int engineId = -1;

	private static readonly string[] SlotNames = new string[4] { "Graphics", "Sound", "AI", "Physics" };

	public EngineUpdatePage(int gameId, int roomId, bool mmo = false)
		: base(gameId, roomId, mmo)
	{
	}

	protected override UpdateKind BaseKind => UpdateKind.Engine;

	public override string Title => mmo ? "Engine upgrade or migration" : base.Title;

	public override float Width => 900f;

	public override float Height => 720f + Extra;

	public override void Build(EditorWindow w)
	{
		float y = Header(w);
		if (y < 0f)
		{
			return;
		}
		gameScript g = Game;
		engineFeatures ef = GameUpdates.EF;
		List<engineScript> engines = GameUpdates.EngineCandidates(g);
		engineScript current = GameUpdates.CurrentEngine(g);
		if (!engines.Any((engineScript e) => e.myID == engineId))
		{
			engineId = engines[0].myID;
		}
		engineScript chosen = engines.First((engineScript e) => e.myID == engineId);
		w.Label("Engine", 24f, y, 110f, 30f, TextAnchor.MiddleLeft, bold: true);
		w.Dropdown(140f, y, w.Width - 154f, engines.Select((engineScript e) => e.GetName() + "  (tech " + e.GetTechLevel() + ((e.myID == g.engineID) ? ", its engine now" : ((e.ownerID == G.MS.myID) ? ", yours" : (", licensed, " + e.gewinnbeteiligung + "% fee"))) + ")" + (mmo ? ((e.myID == g.engineID) ? "  - upgrade" : "  - migration") : "")).ToList(), engines.IndexOf(chosen), (int v) =>
		{
			engineId = engines[Mathf.Clamp(v, 0, engines.Count - 1)].myID;
			w.Rebuild();
		});
		y += 44f;
		w.Label("Now: " + (current ? current.GetName() : "?") + ". Its platforms can run engine features up to tech " + GameUpdates.MinPlatformTech(g) + ".", 24f, y, w.Width - 48f, 24f, TextAnchor.MiddleLeft, bold: false, null, 13);
		y += 30f;
		int[] slots = GameUpdates.UpgradeSlots(g, chosen);
		ListView list = w.List(13f, y, w.Width - 26f, 4f * 56f + 10f);
		for (int i = 0; i < slots.Length && i < 4; i++)
		{
			int old = g.gameEngineFeature[i];
			int fresh = slots[i];
			RowView row = list.Row(48f, null, null, fresh != -1);
			Sprite pic = ((old >= 0) ? ef.GetTypPic(old) : null);
			if ((bool)pic)
			{
				row.Picture(pic, 10f, 34f);
			}
			row.Top(SlotNames[i], 56f, 160f);
			string oldName = ((old >= 0) ? ef.GetName(old) : "-");
			if (fresh == -1)
			{
				row.Bottom(oldName + "  (nothing newer in this engine)", 56f, row.W - 70f).color = UIKit.Grey;
				continue;
			}
			row.Bottom(oldName + "  >  <b>" + ef.GetName(fresh) + "</b>  (tech " + ((old >= 0) ? ef.engineFeatures_TECH[old] : 0) + " > " + ef.engineFeatures_TECH[fresh] + ")", 56f, row.W - 70f);
			row.Highlight();
		}
		y += 4f * 56f + 20f;
		UpdatePlan plan = (mmo ? Core.Mmo.MmoOps.PlanEngineWork(g, chosen) : GameUpdates.PlanEngine(g, chosen));
		List<KeyValuePair<string, string>> facts = new List<KeyValuePair<string, string>>
		{
			EditorWindow.KV("Costs", G.Money(plan.Cost)),
			EditorWindow.KV("Time", RoomLine(plan.Work)),
			EditorWindow.KV("Points", Points(plan)),
			EditorWindow.KV("Sales while on the market", (plan.Bonus > 0.004f) ? GameUpdates.Pct(plan.Bonus) : "no more (updates already maxed)")
		};
		y += w.Facts(14f, y, w.Width - 28f, facts, 2) + 6f;
		string notes = (mmo ? Core.Mmo.MmoOps.EngineEffects(g, chosen) : ("Rated games gain more; the bonus from all of these updates together stops at " + GameUpdates.Pct(GameUpdates.BonusCap) + "."));
		w.Label(((plan.Notes.Length > 0) ? (plan.Notes + " ") : "") + notes, 24f, y, w.Width - 48f, 40f, TextAnchor.UpperLeft, bold: false, null, 13);
		y += 46f;
		int chosenId = chosen.myID;
		UpdateKind kind = Kind;
		string title = (mmo ? (Core.Mmo.MmoOps.IsMigration(g, chosen) ? "Engine migration" : "Engine upgrade") : null);
		StartButton(w, y, plan, () => new UpdateProject
		{
			Kind = kind,
			Title = title,
			EngineId = chosenId,
			EngineFeatures = GameUpdates.UpgradeSlots(Game, chosen)
		});
	}
}

// Add researched gameplay features to a released game.
internal class FeatureUpdatePage : UpdatePageBase
{
	private readonly HashSet<int> picked = new HashSet<int>();

	public FeatureUpdatePage(int gameId, int roomId, bool mmo = false)
		: base(gameId, roomId, mmo)
	{
	}

	protected override UpdateKind BaseKind => UpdateKind.Features;

	public override float Width => 900f;

	public override float Height => 800f + Extra;

	public override void Build(EditorWindow w)
	{
		float y = Header(w);
		if (y < 0f)
		{
			return;
		}
		gameScript g = Game;
		gameplayFeatures gf = GameUpdates.GF;
		List<int> options = GameUpdates.FeatureCandidates(g, picked).OrderByDescending((int j) => gf.GetBonus(j, g.maingenre, g.subgenre)).ThenBy((int j) => gf.GetName(j)).ToList();
		picked.RemoveWhere((int j) => !options.Contains(j));
		w.Label("Pick the features to add. Ones that suit the genre are listed first.", 24f, y, w.Width - 48f, 24f, TextAnchor.MiddleLeft, bold: false, null, 13);
		y += 30f;
		float listHeight = w.Height - 75f - y - 210f;
		ListView list = w.List(13f, y, w.Width - 26f, listHeight);
		foreach (int j in options)
		{
			FeatureRow(w, list, g, gf, j);
		}
		y += listHeight + 10f;
		UpdatePlan plan = GameUpdates.PlanFeatures(g, picked);
		List<KeyValuePair<string, string>> facts = new List<KeyValuePair<string, string>>
		{
			EditorWindow.KV("Costs", G.Money(plan.Cost)),
			EditorWindow.KV("Time", (plan.Work > 0f) ? RoomLine(plan.Work) : "-"),
			EditorWindow.KV("Points", Points(plan)),
			EditorWindow.KV("Sales while on the market", (plan.Bonus > 0.004f) ? GameUpdates.Pct(plan.Bonus) : ((picked.Count > 0) ? "no more (updates already maxed)" : "-"))
		};
		y += w.Facts(14f, y, w.Width - 28f, facts, 2) + 6f;
		string notes = (mmo ? (Core.Mmo.MmoOps.SystemsEffects(g, picked) + ((plan.Notes.Length > 0) ? (" " + plan.Notes) : "")) : ((plan.Notes.Length > 0) ? plan.Notes : ("The bonus from all of these updates together stops at " + GameUpdates.Pct(GameUpdates.BonusCap) + ".")));
		w.Label(notes, 24f, y, w.Width - 48f, 36f, TextAnchor.UpperLeft, bold: false, null, 13);
		y += 42f;
		List<int> chosen = picked.ToList();
		UpdateKind kind = Kind;
		StartButton(w, y, plan, () => new UpdateProject
		{
			Kind = kind,
			Features = chosen
		});
	}

	private void FeatureRow(EditorWindow w, ListView list, gameScript g, gameplayFeatures gf, int j)
	{
		string need = GameUpdates.FeatureNeed(g, j, picked);
		bool on = picked.Contains(j);
		RowView row = list.Row(52f, () =>
		{
			if (!picked.Remove(j))
			{
				picked.Add(j);
			}
			status = "";
			UIKit.Sound();
			w.Rebuild();
		}, gf.GetDesc(j));
		if (on)
		{
			row.Highlight();
		}
		Sprite pic = gf.GetTypSprite(j);
		if ((bool)pic)
		{
			row.Picture(pic, 10f, 32f);
		}
		row.Top((on ? "[x] " : "[  ] ") + gf.GetName(j), 52f, row.W - 300f);
		float fit = gf.GetBonus(j, g.maingenre, g.subgenre);
		string fitText = ((fit >= 1.4f) ? "<color=#2e7d32>Suits the genre</color>" : ((fit < 0.5f) ? "<color=#c62828>Doesn't suit the genre</color>" : "Neutral for the genre"));
		row.Bottom(fitText + ((need != null) ? ("  ·  <color=#c62828>needs " + need + "</color>") : ""), 52f, row.W - 300f);
		UpdatePlan one = GameUpdates.PlanFeatures(g, new int[1] { j });
		row.Text(Points(one), row.W - 300f, 150f, bold: false, TextAnchor.MiddleRight);
		row.Text(G.Money(one.Cost), row.W - 140f, 128f, bold: true, TextAnchor.MiddleRight);
	}
}

// Cut content for a lower age rating.
internal class RatingUpdatePage : UpdatePageBase
{
	private int usk = -1;

	public RatingUpdatePage(int gameId, int roomId, bool mmo = false)
		: base(gameId, roomId, mmo)
	{
	}

	protected override UpdateKind BaseKind => UpdateKind.Rating;

	public override float Width => 760f;

	public override float Height => 520f + Extra;

	public override void Build(EditorWindow w)
	{
		float y = Header(w);
		if (y < 0f)
		{
			return;
		}
		gameScript g = Game;
		int min = GameUpdates.MinUsk(g);
		List<int> options = new List<int>();
		for (int u = g.usk - 1; u >= min; u--)
		{
			options.Add(u);
		}
		if (!options.Contains(usk))
		{
			usk = options[0];
		}
		w.Label("Now rated " + GameUpdates.UskName(g.usk) + ". Its topic allows " + GameUpdates.UskName(min) + " at the lowest.", 24f, y, w.Width - 48f, 26f);
		y += 34f;
		w.Label("New rating", 24f, y, 130f, 30f, TextAnchor.MiddleLeft, bold: true);
		w.Dropdown(160f, y, 260f, options.Select(GameUpdates.UskName).ToList(), options.IndexOf(usk), (int v) =>
		{
			usk = options[Mathf.Clamp(v, 0, options.Count - 1)];
			w.Rebuild();
		});
		y += 46f;
		UpdatePlan plan = GameUpdates.PlanRating(g, usk);
		List<KeyValuePair<string, string>> facts = new List<KeyValuePair<string, string>>
		{
			EditorWindow.KV("Costs", G.Money(plan.Cost)),
			EditorWindow.KV("Time", RoomLine(plan.Work)),
			EditorWindow.KV("Sales", GameUpdates.Pct(plan.Bonus)),
			EditorWindow.KV("Points", Points(plan))
		};
		y += w.Facts(14f, y, w.Width - 28f, facts, 2) + 6f;
		w.Label(mmo ? Core.Mmo.MmoOps.RatingEffects(g.usk - usk) : plan.Notes, 24f, y, w.Width - 48f, 36f, TextAnchor.UpperLeft, bold: false, null, 13);
		y += 42f;
		int target = usk;
		UpdateKind kind = Kind;
		StartButton(w, y, plan, () => new UpdateProject
		{
			Kind = kind,
			Usk = target
		});
	}
}

// One update in development: progress, room, cancel.
internal class UpdateProjectPage : Page
{
	private readonly int taskId;

	private bool confirmCancel;

	private string status = "";

	private int seen = -1;

	private float nextTick;

	public UpdateProjectPage(int taskId)
	{
		this.taskId = taskId;
	}

	private UpdateProject Project => GameUpdates.Projects.FirstOrDefault((UpdateProject p) => p.TaskId == taskId);

	public override string Title => (Project != null) ? GameUpdates.KindName(Project.Kind) : "Update";

	public override Sprite IconSprite => UpdateIcons.For((Project != null) ? Project.Kind : UpdateKind.Port);

	public override float Width => 760f;

	public override float Height => 420f;

	public override void Build(EditorWindow w)
	{
		seen = GameUpdates.Version;
		BackButton(w);
		UpdateProject p = Project;
		if (p == null)
		{
			w.Label(string.IsNullOrEmpty(status) ? "This update is done or was stopped." : status, 24f, 60f, w.Width - 48f, 60f);
			return;
		}
		roomScript room = CopyProtectDev.Rooms().FirstOrDefault((roomScript r) => r.taskID == p.TaskId);
		w.Label("<b>" + p.Title + "</b>", 24f, 52f, w.Width - 48f, 30f, TextAnchor.MiddleLeft, bold: false, null, 16);
		w.Bar(14f, 94f, w.Width - 28f, 26f, p.Fraction, Mathf.FloorToInt(p.Fraction * 100f) + "%", UIKit.Green);
		List<KeyValuePair<string, string>> facts = new List<KeyValuePair<string, string>>
		{
			EditorWindow.KV("Room", room ? CopyProtectDev.RoomName(room) : "-"),
			EditorWindow.KV("Time left", room ? GameUpdates.Duration(p.Needed - p.Progress, room, p.Kind) : "-"),
			EditorWindow.KV("Costs", G.Money(p.Cost)),
			EditorWindow.KV("Back if stopped now", G.Money(GameUpdates.Refund(p)))
		};
		w.Facts(14f, 134f, w.Width - 28f, facts, 2);
		w.SmallButton(confirmCancel ? "Really stop it?" : "Stop the update", 14f, w.Height - 75f - 84f, 220f, () =>
		{
			if (!confirmCancel)
			{
				confirmCancel = true;
				w.Rebuild();
				return;
			}
			long back = GameUpdates.Cancel(p);
			status = "Stopped." + ((back > 0) ? (" " + G.Money(back) + " came back.") : "");
			confirmCancel = false;
			w.Rebuild();
		}, enabled: true, "Stop the work. What wasn't used of the costs comes back.", null, 30f);
	}

	public override void Tick(EditorWindow w)
	{
		if (GameUpdates.Version != seen || Time.unscaledTime > nextTick)
		{
			nextTick = Time.unscaledTime + 1f;
			w.Rebuild();
		}
	}
}

// The addon's updates window: what's in development, and starting one without going through a room's menu.
internal class UpdatesHomePage : Page
{
	private int seen = -1;

	private float nextTick;

	public override string Title => "Game updates";

	public override Sprite IconSprite => UpdateIcons.For(UpdateKind.Engine);

	public override float Width => 820f;

	public override float Height => 600f;

	public override void Build(EditorWindow w)
	{
		seen = GameUpdates.Version;
		w.Label("Port released games to more platforms, update their engine, add gameplay features or change their age rating. Start one from a development room: Updates & Addons.", 14f, 10f, w.Width - 28f, 44f, TextAnchor.MiddleLeft, bold: false, null, 13);
		w.Header("In development (" + GameUpdates.Projects.Count + ")", 14f, 62f, w.Width - 28f);
		float y = 94f;
		if (GameUpdates.Projects.Count == 0)
		{
			w.Label("Nothing right now.", 24f, y, w.Width - 48f, 30f);
			return;
		}
		ListView list = w.List(13f, y, w.Width - 26f, w.Height - 75f - y - 10f);
		foreach (UpdateProject p in GameUpdates.Projects.ToList())
		{
			int id = p.TaskId;
			RowView row = list.Row(52f, () => w.Push(new UpdateProjectPage(id)));
			row.Picture(UpdateIcons.For(p.Kind), 10f, 34f);
			row.Top(p.Title, 54f, row.W - 300f);
			roomScript room = CopyProtectDev.Rooms().FirstOrDefault((roomScript r) => r.taskID == p.TaskId);
			row.Bottom((room ? (CopyProtectDev.RoomName(room) + "  ·  " + GameUpdates.Duration(p.Needed - p.Progress, room, p.Kind) + " left") : "no room"), 54f, row.W - 300f);
			row.Bar(row.W - 230f, 200f, 22f, p.Fraction, UIKit.Green, Mathf.FloorToInt(p.Fraction * 100f) + "%");
		}
	}

	public override void Tick(EditorWindow w)
	{
		if (GameUpdates.Version != seen || Time.unscaledTime > nextTick)
		{
			nextTick = Time.unscaledTime + 2f;
			w.Rebuild();
		}
	}
}
