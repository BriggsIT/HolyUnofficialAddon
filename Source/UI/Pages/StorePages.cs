using System;
using System.Collections.Generic;
using System.Linq;
using HolyAddon.Core;
using HolyAddon.Core.CopyProtection;
using HolyAddon.Core.Store;
using HolyAddon.Core.Updates;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI.Pages;

internal static class StoreUi
{
	public static Image Picture(Transform parent, Sprite sprite, float x, float y, float size)
	{
		Image img = new GameObject("Logo", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
		img.transform.SetParent(parent, worldPositionStays: false);
		UIKit.Place(img.gameObject, x, y, size, size);
		img.sprite = sprite;
		img.preserveAspect = true;
		img.raycastTarget = false;
		return img;
	}

	public static string Pct(float f)
	{
		float p = f * 100f;
		return ((p < 10f) ? p.ToString("0.0") : Mathf.RoundToInt(p).ToString()) + "%";
	}

	public static readonly Color[] Colors = new Color[6]
	{
		new Color(0.42f, 0.3f, 0.78f),
		new Color(0.2f, 0.45f, 0.8f),
		new Color(0.15f, 0.62f, 0.6f),
		new Color(0.9f, 0.55f, 0.15f),
		new Color(0.75f, 0.3f, 0.45f),
		new Color(0.55f, 0.55f, 0.55f)
	};
}

// Your digital store: building it, then its dashboard.
internal class StoreHomePage : Page
{
	private string name;

	private int logo = 1;

	private int roomId = -1;

	private string status = "";

	private bool statusGood;

	private int seen = -1;

	private float nextTick;

	public override string Title => "Your digital store";

	public override Sprite IconSprite => UpdateIcons.Store();

	public override float Width => 1080f;

	public override float Height => 760f;

	public override void Build(EditorWindow w)
	{
		seen = GameUpdates.Version;
		BackButton(w);
		w.Label(G.Money(((bool)G.MS) ? G.MS.money : 0), w.Width - 274f, 10f, 260f, 28f, TextAnchor.MiddleRight, bold: true);
		string locked = StoreOps.LockReason();
		if (locked != null)
		{
			w.Info(locked, 14f, 50f, w.Width - 28f, 60f);
			return;
		}
		StoreState s = StoreOps.State;
		if (!s.Built)
		{
			UpdateProject running = StoreOps.Running();
			if (running != null)
			{
				Building(w, running);
			}
			else
			{
				BuildForm(w);
			}
			return;
		}
		Dashboard(w, s);
	}

	// ---- Before the store exists ----

	private void Building(EditorWindow w, UpdateProject p)
	{
		w.Label("<b>Your store is being built</b>", 24f, 52f, w.Width - 48f, 30f, TextAnchor.MiddleLeft, bold: false, null, 17);
		w.Bar(14f, 92f, w.Width - 28f, 26f, p.Fraction, Mathf.FloorToInt(p.Fraction * 100f) + "%", UIKit.Green);
		int task = p.TaskId;
		w.SmallButton("Open the project", 14f, 130f, 220f, () => w.Push(new UpdateProjectPage(task)), enabled: true, null, null, 30f);
		w.SmallButton("Rival stores", 248f, 130f, 220f, () => w.Push(new StoreChartsPage()), enabled: true, null, null, 30f);
	}

	private void BuildForm(EditorWindow w)
	{
		if (name == null)
		{
			string company = (G.MS ? G.MS.GetCompanyName() : "");
			name = (string.IsNullOrWhiteSpace(company) ? "My Store" : (company.Trim() + " Launcher"));
		}
		w.Label("Open your own digital PC store. Studios sell their games through it and pay you a cut; your own games get its front page. It needs a development room to build, then servers to run, and grows with its features, catalogue and reputation.", 24f, 46f, w.Width - 48f, 44f, TextAnchor.UpperLeft, bold: false, null, 13);
		float y = 100f;
		w.Label("Name", 24f, y, 110f, 34f, TextAnchor.MiddleLeft, bold: true);
		w.Input(name, "Store name...", 140f, y, 420f, (string v) => name = v);
		w.SmallButton("Rival stores", w.Width - 234f, y + 3f, 220f, () => w.Push(new StoreChartsPage()), enabled: true, "The stores you'll be up against.", null, 28f);
		y += 48f;
		w.Header("Logo (only your store can use these)", 14f, y, w.Width - 28f);
		y += 32f;
		float size = 92f;
		float gap = (w.Width - 28f - size * 6f) / 5f;
		for (int i = 1; i <= StoreOps.LogoCount; i++)
		{
			int n = i;
			float x = 14f + (float)((i - 1) % 6) * (size + gap);
			float ly = y + (float)((i - 1) / 6) * (size + 10f);
			if (n == logo)
			{
				MmoUi.Rect(w.Body, x - 4f, ly - 4f, size + 8f, size + 8f, UIKit.SelectedRow);
			}
			GameObject b = new GameObject("StoreLogo" + n, typeof(RectTransform), typeof(Image), typeof(Button));
			b.transform.SetParent(w.Body, worldPositionStays: false);
			UIKit.Place(b, x, ly, size, size);
			Image img = b.GetComponent<Image>();
			img.sprite = UpdateIcons.StoreLogo(n);
			img.preserveAspect = true;
			UIKit.OnClick(b.GetComponent<Button>(), () =>
			{
				UIKit.Sound();
				logo = n;
				w.Rebuild();
			});
		}
		y += 2f * (size + 10f) + 8f;
		UpdatePlan plan = StoreOps.PlanBuild();
		w.Label("Room", 24f, y, 110f, 30f, TextAnchor.MiddleLeft, bold: true);
		roomId = MmoUi.RoomDropdown(w, 140f, y, w.Width - 154f, roomId, plan.Work, UpdateKind.StoreBuild, (int v) =>
		{
			roomId = v;
			w.Rebuild();
		});
		y += 44f;
		roomScript room = CopyProtectDev.FindRoom(roomId);
		RivalStore biggest = StoreOps.RivalStores.Where(StoreOps.IsOpen).OrderByDescending((RivalStore r) => StoreOps.RivalShare(r.Id)).FirstOrDefault();
		List<KeyValuePair<string, string>> facts = new List<KeyValuePair<string, string>>
		{
			EditorWindow.KV("Costs", G.Money(plan.Cost)),
			EditorWindow.KV("Time", room ? GameUpdates.Duration(plan.Work, room, UpdateKind.StoreBuild) : "-"),
			EditorWindow.KV("Digital PC buyers", MmoUi.Short(StoreOps.DigitalPcPlayers())),
			EditorWindow.KV("Biggest store now", (biggest != null) ? (biggest.Name + " (" + StoreUi.Pct(StoreOps.RivalShare(biggest.Id)) + ")") : "none yet")
		};
		y += w.Facts(14f, y, w.Width - 28f, facts, 2) + 10f;
		bool ok = (bool)room;
		w.BigButton("Build the store", 14f, y, 260f, 44f, () =>
		{
			UpdateProject p = new UpdateProject
			{
				Kind = UpdateKind.StoreBuild,
				Title = "Store client 1.0: " + (name ?? "").Trim(),
				StoreName = name,
				StoreLogo = logo
			};
			string error = GameUpdates.Start(p, StoreOps.PlanBuild(), CopyProtectDev.FindRoom(roomId), null);
			status = error ?? "";
			statusGood = error == null;
			UIKit.Sound();
			w.Rebuild();
		}, ok, ok ? ("Costs " + G.Money(plan.Cost) + ", paid now.") : "Pick a free development room with staff.");
		Text line = w.Label(status, 290f, y, w.Width - 304f, 44f, TextAnchor.MiddleLeft, bold: false, null, 13);
		line.color = (statusGood ? UIKit.Green : UIKit.Red);
	}

	// ---- The store ----

	private void Dashboard(EditorWindow w, StoreState s)
	{
		StoreUi.Picture(w.Body, UpdateIcons.StoreLogo(s.Logo), 14f, 46f, 72f);
		w.Label("<b>" + s.Name + "</b>", 100f, 44f, 460f, 30f, TextAnchor.MiddleLeft, bold: false, null, 19);
		w.Label("Your PC store since " + s.BuiltYear + "  ·  " + s.Features.Count + " of " + StoreOps.Features.Count + " features  ·  " + (StoreOps.SaleRunning() ? "<color=#c26a00>store sale on now</color>" : "no sale running"), 100f, 76f, 520f, 24f, TextAnchor.MiddleLeft, bold: false, null, 13);
		w.Label("Commission", w.Width - 340f, 44f, 326f, 22f, TextAnchor.MiddleLeft, bold: true);
		for (int i = 0; i < StoreOps.Commissions.Length; i++)
		{
			int c = StoreOps.Commissions[i];
			bool on = s.Commission == c;
			w.SmallButton(on ? ("[" + c + "%]") : (c + "%"), w.Width - 340f + (float)i * 82f, 70f, 76f, () =>
			{
				StoreOps.State.Commission = c;
				status = "Studios now pay you " + c + "% of their sales. Studio trust moves over the next weeks.";
				statusGood = true;
				GameUpdates.Touch();
			}, !on, "Your cut of other studios' sales. Lower draws more studios (studio trust), higher earns more per sale.", null, 28f);
		}
		Text line = w.Label(status, 14f, 110f, w.Width - 28f, 22f, TextAnchor.MiddleLeft, bold: false, null, 13);
		line.color = (statusGood ? UIKit.Green : UIKit.Red);
		float leftW = 520f;
		float y = 138f;
		int others = StoreOps.OthersGames().Count;
		int mine = MyPcGames().Count;
		StoreOps.ServerSpace(out long free, out long capacity);
		List<KeyValuePair<string, string>> facts = new List<KeyValuePair<string, string>>
		{
			EditorWindow.KV("Accounts", MmoUi.Short(s.Accounts)),
			EditorWindow.KV("Active players", MmoUi.Short(s.Active)),
			EditorWindow.KV("Digital PC buyers", StoreUi.Pct(s.Share)),
			EditorWindow.KV("Catalogue", mine + " yours, " + Mathf.RoundToInt((float)others * s.Coverage) + " others"),
			EditorWindow.KV("Commission last month", G.Money(s.CommissionLast)),
			EditorWindow.KV("Paid to rival stores", G.Money(s.FeesLast)),
			EditorWindow.KV("Servers", (s.Outage > 0.01f) ? ("<color=#c62828>" + Mathf.RoundToInt(s.Outage * 100f) + "% short</color>") : ((capacity > 0) ? (MmoUi.Short(free) + " free") : "none needed yet")),
			EditorWindow.KV("Server costs last month", G.Money(s.ServerLast))
		};
		y += w.Facts(14f, y, leftW, facts, 2) + 10f;
		w.Label("Player reputation", 24f, y, 150f, 26f, TextAnchor.MiddleLeft, bold: true);
		w.Bar(180f, y + 2f, leftW - 176f, 22f, s.Reputation / 100f, Mathf.RoundToInt(s.Reputation).ToString(), UIKit.ValueColor(s.Reputation), null, "What players think of your store: features and sales help, outages and exclusives hurt.");
		y += 30f;
		w.Label("Studio trust", 24f, y, 150f, 26f, TextAnchor.MiddleLeft, bold: true);
		w.Bar(180f, y + 2f, leftW - 176f, 22f, s.Trust / 100f, Mathf.RoundToInt(s.Trust).ToString(), UIKit.ValueColor(s.Trust), null, "What other studios think of selling with you: a lower commission and more players raise it. It decides how much of their catalogue comes to your store (" + StoreUi.Pct(s.Coverage) + " now).");
		y += 36f;
		AccountsChart(w, s, 14f, y, leftW, w.Height - 75f - y - 12f);
		Market(w, s, 14f + leftW + 14f, 138f, w.Width - leftW - 42f);
	}

	private static List<gameScript> MyPcGames()
	{
		gameScript[] all = ((G.MS && (bool)G.MS.games_) ? G.MS.games_.arrayGamesScripts : new gameScript[0]);
		return all.Where((gameScript g) => (bool)g && g.isOnMarket && g.IsMyGame() && g.gameTyp == 0 && !g.arcade && !g.handy && g.digitalVersion && StoreOps.PcFraction(g) > 0f).ToList();
	}

	private static void AccountsChart(EditorWindow w, StoreState s, float x, float y, float width, float height)
	{
		w.Header("Accounts, last two years", x, y, width);
		y += 30f;
		height = Mathf.Max(60f, height - 30f);
		RectTransform panel = w.Panel(x, y, width, height);
		int count = Mathf.Min(104, s.AccountHistory.Count);
		if (count < 2)
		{
			w.Label("The chart fills in week by week.", 12f, height / 2f - 12f, width - 24f, 24f, TextAnchor.MiddleCenter, bold: false, panel, 13);
			return;
		}
		List<long> shown = s.AccountHistory.Skip(s.AccountHistory.Count - count).ToList();
		long max = Math.Max(1L, shown.Max());
		float top = 24f;
		float h = height - top - 8f;
		float step = (width - 20f) / 104f;
		float start = 10f + (float)(104 - count) * step;
		for (int i = 0; i < count; i++)
		{
			float bh = Mathf.Max(1f, h * (float)shown[i] / (float)max);
			MmoUi.Rect(panel, start + (float)i * step, top + h - bh, Mathf.Max(1f, step - 1f), bh, new Color(0.42f, 0.3f, 0.78f, 0.85f));
		}
		w.Label("max " + MmoUi.Short(max), 10f, 2f, 200f, 20f, TextAnchor.MiddleLeft, bold: false, panel, 12);
	}

	// Who digital PC buyers buy from: a bar split by store, and the biggest ones listed.
	private static void Market(EditorWindow w, StoreState s, float x, float y, float width)
	{
		w.Header("Where digital PC buyers shop", x, y, width);
		y += 32f;
		List<KeyValuePair<string, float>> parts = new List<KeyValuePair<string, float>>
		{
			new KeyValuePair<string, float>(s.Name, s.Share)
		};
		parts.AddRange(from r in StoreOps.RivalStores
			where StoreOps.IsOpen(r)
			orderby StoreOps.RivalShare(r.Id) descending
			select new KeyValuePair<string, float>(r.Name, StoreOps.RivalShare(r.Id)));
		parts.Add(new KeyValuePair<string, float>("Straight from studios", s.Direct));
		RectTransform bar = w.Panel(x, y, width, 30f);
		float bx = 2f;
		for (int i = 0; i < parts.Count; i++)
		{
			float pw = (width - 4f) * parts[i].Value;
			if (pw < 1f)
			{
				continue;
			}
			Color c = ((i == 0) ? StoreUi.Colors[0] : ((i == parts.Count - 1) ? StoreUi.Colors[5] : StoreUi.Colors[1 + (i - 1) % 4]));
			MmoUi.Rect(bar, bx, 2f, pw, 26f, c);
			bx += pw;
		}
		y += 40f;
		ListView list = w.List(x - 1f, y, width + 2f, 300f);
		Row(list, UpdateIcons.StoreLogo(s.Logo), s.Name + "  <color=#666666>(yours)</color>", s.Share, "your commission " + s.Commission + "%", highlight: true);
		foreach (RivalStore r in StoreOps.RivalStores.Where(StoreOps.IsOpen).OrderByDescending((RivalStore r) => StoreOps.RivalShare(r.Id)))
		{
			Row(list, UpdateIcons.RivalLogo(r.Id), r.Name, StoreOps.RivalShare(r.Id), r.OwnGamesOnly ? "own games only" : (Mathf.RoundToInt(StoreOps.RivalFee(r, null) * 100f) + "% cut"), highlight: false);
		}
		Row(list, null, "Straight from studios", s.Direct, "no store", highlight: false);
		y += 310f;
		w.SmallButton("Catalogue", x, y, (width - 20f) / 3f, () => w.Push(new StoreCatalogPage()), enabled: true, "Your games on the store (and exclusives), and other studios' games.", null, 30f);
		w.SmallButton("Features", x + (width - 20f) / 3f + 10f, y, (width - 20f) / 3f, () => w.Push(new StoreFeaturesPage()), enabled: true, "Build new store features in a development room.", null, 30f);
		w.SmallButton("Store charts", x + 2f * ((width - 20f) / 3f + 10f), y, (width - 20f) / 3f, () => w.Push(new StoreChartsPage()), enabled: true, "Every store, now and over the years.", null, 30f);
		y += 40f;
		UpdateProject running = StoreOps.Running();
		if (running != null)
		{
			int task = running.TaskId;
			w.SmallButton("In development: " + running.Title + "  " + Mathf.FloorToInt(running.Fraction * 100f) + "%", x, y, width, () => w.Push(new UpdateProjectPage(task)), enabled: true, "Open the project.", null, 30f);
		}
	}

	private static void Row(ListView list, Sprite logo, string name, float share, string note, bool highlight)
	{
		RowView row = list.Row(40f);
		if (highlight)
		{
			row.Highlight();
		}
		if ((bool)logo)
		{
			StoreUi.Picture(row.T, logo, 8f, 4f, 32f);
		}
		row.Text(name, 48f, row.W - 250f, bold: true);
		row.Text(note, row.W - 250f, 140f, bold: false, TextAnchor.MiddleRight);
		row.Text(StoreUi.Pct(share), row.W - 100f, 90f, bold: true, TextAnchor.MiddleRight);
	}

	public override void Tick(EditorWindow w)
	{
		if (GameUpdates.Version != seen || (StoreOps.Running() != null && Time.unscaledTime > nextTick))
		{
			nextTick = Time.unscaledTime + 2f;
			w.Rebuild();
		}
	}
}

// Your games on the store (exclusives) and other studios' games selling through it.
internal class StoreCatalogPage : Page
{
	private int seen = -1;

	public override string Title => "Store catalogue";

	public override Sprite IconSprite => UpdateIcons.Store();

	public override float Width => 960f;

	public override float Height => 760f;

	public override void Build(EditorWindow w)
	{
		seen = GameUpdates.Version;
		BackButton(w);
		StoreState s = StoreOps.State;
		w.Label("Your PC games with a digital version sell on your store's front page. Make one exclusive and it skips the rival stores: no fees to them and new accounts for you, but buyers who only shop elsewhere won't buy it.", 116f, 4f, w.Width - 130f, 44f, TextAnchor.MiddleLeft, bold: false, null, 13);
		gameScript[] all = ((G.MS && (bool)G.MS.games_) ? G.MS.games_.arrayGamesScripts : new gameScript[0]);
		List<gameScript> mine = all.Where((gameScript g) => (bool)g && g.isOnMarket && g.IsMyGame() && g.gameTyp == 0 && !g.arcade && !g.handy && StoreOps.PcFraction(g) > 0f).OrderByDescending((gameScript g) => (g.sellsPerWeek != null && g.sellsPerWeek.Length != 0) ? g.sellsPerWeek[0] : 0).ToList();
		w.Header("Your PC games (" + mine.Count + ")", 14f, 54f, w.Width - 28f);
		ListView list = w.List(13f, 84f, w.Width - 26f, 300f);
		foreach (gameScript g in mine)
		{
			MyRow(w, list, g);
		}
		if (mine.Count == 0)
		{
			w.Label("No PC games of yours on the market.", 24f, 90f, w.Width - 48f, 26f);
		}
		List<gameScript> others = StoreOps.OthersGames().OrderByDescending(StoreOps.CommissionOn).ToList();
		w.Header("Other studios: " + Mathf.RoundToInt((float)others.Count * s.Coverage) + " of " + others.Count + " PC games on the market sell on your store", 14f, 396f, w.Width - 28f);
		ListView top = w.List(13f, 426f, w.Width - 26f, w.Height - 75f - 436f);
		foreach (gameScript g in others.Take(25))
		{
			RowView row = top.Row(40f, null, g.GetTooltip());
			row.Text(g.GetNameWithTag(), 10f, row.W - 360f, bold: true);
			row.Text(g.GetDeveloperName(), row.W - 360f, 180f);
			row.Text(G.Money(StoreOps.CommissionOn(g)) + " a week", row.W - 170f, 160f, bold: false, TextAnchor.MiddleRight);
		}
	}

	private void MyRow(EditorWindow w, ListView list, gameScript g)
	{
		bool can = StoreOps.CanBeExclusive(g);
		bool exclusive = StoreOps.IsExclusive(g);
		RowView row = list.Row(48f, null, g.GetTooltip());
		if (exclusive)
		{
			row.Highlight();
		}
		row.Top(g.GetNameWithTag(), 10f, row.W - 360f);
		string info = ((!g.digitalVersion) ? "No digital version: sells in shops only." : (exclusive ? "Exclusive to your store: no fees, new accounts." : ("On your front page; rival stores take " + StoreUi.Pct(StoreOps.FeeRate(g)) + " of its digital PC money.")));
		row.Bottom(info, 10f, row.W - 200f).color = (can ? Color.black : UIKit.Grey);
		int id = g.myID;
		row.Button(exclusive ? "Sell everywhere" : "Make exclusive", row.W - 180f, 170f, () =>
		{
			StoreOps.SetExclusive(GameUpdates.FindGame(id), !exclusive);
			w.Rebuild();
		}, can, exclusive ? "Back on the rival stores too." : "Only on your store: no rival store fees, and its buyers sign up with you. Players don't love exclusives (reputation).");
	}

	public override void Tick(EditorWindow w)
	{
		if (GameUpdates.Version != seen)
		{
			w.Rebuild();
		}
	}
}

// Store features: development room projects.
internal class StoreFeaturesPage : Page
{
	private int seen = -1;

	public override string Title => "Store features";

	public override Sprite IconSprite => UpdateIcons.For(UpdateKind.StoreFeature);

	public override float Width => 900f;

	public override float Height => 640f;

	public override void Build(EditorWindow w)
	{
		seen = GameUpdates.Version;
		BackButton(w);
		w.Label("Each feature makes your store more attractive to players. Build them in a development room.", 116f, 10f, w.Width - 130f, 28f, TextAnchor.MiddleLeft, bold: false, null, 13);
		ListView list = w.List(13f, 50f, w.Width - 26f, w.Height - 75f - 60f);
		UpdateProject running = StoreOps.Running();
		foreach (StoreFeature f in StoreOps.Features)
		{
			bool done = StoreOps.HasFeature(f.Id);
			bool building = running != null && running.Kind == UpdateKind.StoreFeature && running.Usk == f.Id;
			string problem = StoreOps.FeatureProblem(f);
			int id = f.Id;
			RowView row = list.Row(64f, (done || building) ? ((Action)delegate
			{
				if (building)
				{
					w.Push(new UpdateProjectPage(running.TaskId));
				}
			}) : ((Action)delegate
			{
				w.Push(new StoreFeaturePage(id));
			}), f.About);
			if (done)
			{
				row.Highlight();
			}
			Image icon = row.Picture(UpdateIcons.StoreFeature(f.Id), 10f, 44f);
			Text top = row.Top(f.Name, 64f, row.W - 300f);
			row.Bottom(f.About, 64f, row.W - 300f);
			string state = (done ? "<color=#2e7d32>Done</color>" : (building ? ("In development " + Mathf.FloorToInt(running.Fraction * 100f) + "%") : ((problem != null && StoreOps.Year < f.Year) ? ("From " + f.Year) : G.Money(f.Cost))));
			row.Text(state, row.W - 220f, 210f, bold: true, TextAnchor.MiddleRight);
			if (!done && !building && StoreOps.Year < f.Year)
			{
				icon.color = new Color(1f, 1f, 1f, 0.4f);
				top.color = UIKit.Grey;
			}
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

internal class StoreFeaturePage : Page
{
	private readonly int featureId;

	private int roomId = -1;

	private string status = "";

	public StoreFeaturePage(int featureId)
	{
		this.featureId = featureId;
	}

	private StoreFeature Feature => StoreOps.Feature(featureId);

	public override string Title => Feature?.Name ?? "Store feature";

	public override Sprite IconSprite => UpdateIcons.StoreFeature(featureId);

	public override float Width => 760f;

	public override float Height => 470f;

	public override void Build(EditorWindow w)
	{
		BackButton(w);
		w.Label(G.Money(((bool)G.MS) ? G.MS.money : 0), w.Width - 274f, 10f, 260f, 28f, TextAnchor.MiddleRight, bold: true);
		StoreFeature f = Feature;
		if (f == null)
		{
			return;
		}
		StoreUi.Picture(w.Body, UpdateIcons.StoreFeature(f.Id), 14f, 48f, 64f);
		w.Label("<b>" + f.Name + "</b>", 92f, 46f, w.Width - 106f, 28f, TextAnchor.MiddleLeft, bold: false, null, 17);
		w.Label(f.About, 92f, 74f, w.Width - 106f, 40f, TextAnchor.UpperLeft, bold: false, null, 13);
		float y = 126f;
		UpdatePlan plan = StoreOps.PlanFeature(f);
		if (plan.Problem != null)
		{
			w.Info(plan.Problem, 14f, y, w.Width - 28f, 56f);
			return;
		}
		w.Label("Room", 24f, y, 110f, 30f, TextAnchor.MiddleLeft, bold: true);
		roomId = MmoUi.RoomDropdown(w, 140f, y, w.Width - 154f, roomId, plan.Work, UpdateKind.StoreFeature, (int v) =>
		{
			roomId = v;
			w.Rebuild();
		});
		y += 44f;
		roomScript room = CopyProtectDev.FindRoom(roomId);
		List<string> effects = new List<string>();
		effects.Add("store appeal +" + Mathf.RoundToInt(f.Appeal));
		if (f.Reputation > 0f)
		{
			effects.Add("reputation +" + Mathf.RoundToInt(f.Reputation));
		}
		if (f.Active > 0f)
		{
			effects.Add("active players +" + Mathf.RoundToInt(f.Active * 100f) + "%");
		}
		List<KeyValuePair<string, string>> facts = new List<KeyValuePair<string, string>>
		{
			EditorWindow.KV("Costs", G.Money(plan.Cost)),
			EditorWindow.KV("Time", room ? GameUpdates.Duration(plan.Work, room, UpdateKind.StoreFeature) : "-"),
			EditorWindow.KV("Effect", string.Join(", ", effects))
		};
		y += w.Facts(14f, y, w.Width - 28f, facts, 1) + 10f;
		bool ok = (bool)room;
		w.BigButton("Start building it", 14f, y, 260f, 44f, () =>
		{
			UpdateProject p = new UpdateProject
			{
				Kind = UpdateKind.StoreFeature,
				Usk = f.Id,
				Title = f.Name + ": " + StoreOps.State.Name
			};
			string error = GameUpdates.Start(p, StoreOps.PlanFeature(f), CopyProtectDev.FindRoom(roomId), null);
			if (error != null)
			{
				status = error;
				w.Rebuild();
				return;
			}
			UIKit.Sound();
			w.Replace(new UpdateProjectPage(p.TaskId));
		}, ok, ok ? ("Costs " + G.Money(plan.Cost) + ", paid now.") : "Pick a free development room with staff.");
		Text line = w.Label(status, 290f, y, w.Width - 304f, 44f, TextAnchor.MiddleLeft, bold: false, null, 13);
		line.color = UIKit.Red;
	}
}

// Every store: yours, the rivals (open and closed) and buying straight from the studios.
internal class StoreChartsPage : Page
{
	private int seen = -1;

	public override string Title => "Store charts";

	public override Sprite IconSprite => UpdateIcons.Store();

	public override float Width => 920f;

	public override float Height => 720f;

	public override void Build(EditorWindow w)
	{
		seen = GameUpdates.Version;
		BackButton(w);
		StoreState s = StoreOps.State;
		w.Label("Where PC players buy their digital games. Stores open (and close) over the years.", 116f, 10f, w.Width - 130f, 28f, TextAnchor.MiddleLeft, bold: false, null, 13);
		List<KeyValuePair<float, Action<RowView>>> rows = new List<KeyValuePair<float, Action<RowView>>>();
		if (s.Built)
		{
			rows.Add(new KeyValuePair<float, Action<RowView>>(s.Share, (RowView row) =>
			{
				row.Highlight();
				Fill(row, UpdateIcons.StoreLogo(s.Logo), s.Name + "  <color=#666666>(yours)</color>", "Since " + s.BuiltYear + "  ·  " + MmoUi.Short(s.Accounts) + " accounts", s.Commission + "% commission", s.Share, closed: false);
			}));
		}
		foreach (RivalStore r in StoreOps.RivalStores)
		{
			bool open = StoreOps.IsOpen(r);
			bool closed = StoreOps.HasClosed(r);
			if (!open && !closed)
			{
				continue;
			}
			RivalStore rr = r;
			rows.Add(new KeyValuePair<float, Action<RowView>>(closed ? (-1f) : StoreOps.RivalShare(r.Id), (RowView row) =>
			{
				Fill(row, UpdateIcons.RivalLogo(rr.Id), rr.Name, (closed ? ("Closed in " + rr.CloseYear + ". ") : ("Since " + rr.OpenYear + ". ")) + rr.About, rr.OwnGamesOnly ? "own games only" : (Mathf.RoundToInt(StoreOps.RivalFee(rr, null) * 100f) + "% cut"), StoreOps.RivalShare(rr.Id), closed);
			}));
		}
		rows.Add(new KeyValuePair<float, Action<RowView>>(s.Direct, (RowView row) =>
		{
			Fill(row, null, "Straight from the studios", "Studios' own websites and boxed copies with a digital key. Fades as stores take over.", "no store", s.Direct, closed: false);
		}));
		ListView list = w.List(13f, 50f, w.Width - 26f, w.Height - 75f - 60f);
		int rank = 0;
		foreach (KeyValuePair<float, Action<RowView>> r2 in rows.OrderByDescending((KeyValuePair<float, Action<RowView>> x) => x.Key))
		{
			RowView row = list.Row(56f);
			rank++;
			if (r2.Key >= 0f)
			{
				row.Text(rank + ".", 4f, 34f, bold: true, TextAnchor.MiddleRight);
			}
			r2.Value(row);
		}
		if (!StoreOps.Open)
		{
			w.Label("Digital stores arrive in " + StoreOps.OpenYear + ".", 24f, 60f, w.Width - 48f, 30f);
		}
	}

	private static void Fill(RowView row, Sprite logo, string name, string about, string fee, float share, bool closed)
	{
		Image img = ((bool)logo ? StoreUi.Picture(row.T, logo, 44f, 6f, 44f) : null);
		Text top = row.Top(name, 98f, row.W - 360f);
		Text bottom = row.Bottom(about, 98f, row.W - 360f);
		Text f = row.Text(fee, row.W - 250f, 140f, bold: false, TextAnchor.MiddleRight);
		Text s = row.Text(closed ? "closed" : StoreUi.Pct(share), row.W - 100f, 90f, bold: true, TextAnchor.MiddleRight);
		if (closed)
		{
			if ((bool)img)
			{
				img.color = new Color(1f, 1f, 1f, 0.4f);
			}
			top.color = UIKit.Grey;
			bottom.color = UIKit.Grey;
			f.color = UIKit.Grey;
			s.color = UIKit.Grey;
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
