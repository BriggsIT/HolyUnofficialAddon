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

// Current charts (Statistics > Current Charts): what sells right now, where the game's own Charts menu mostly
// keeps all-time lists. Top sellers this week, genres, consoles, studios, new releases and the games coming soon.
internal class CurrentChartsPage : Page
{
	private static int tab;

	private static int filter;

	private static int genreFilter = -1;

	private static int platformFilter;

	private static int releaseSort;

	private int seen = -1;

	private float nextTick;

	private static readonly string[] Tabs = new string[6] { "Top Sellers", "Genres", "Consoles", "Studios", "New Releases", "Coming Soon" };

	public override string Title => "Current Charts";

	public override Sprite IconSprite => UpdateIcons.Game("iconCharts", UpdateIcons.Store());

	public override float Width => 1100f;

	public override float Height => 860f;

	private static gameScript[] All
	{
		get
		{
			if (!G.MS || !G.MS.games_ || G.MS.games_.arrayGamesScripts == null)
			{
				return new gameScript[0];
			}
			return G.MS.games_.arrayGamesScripts;
		}
	}

	private static genres Genres
	{
		get
		{
			if (!G.MainObj)
			{
				return null;
			}
			return G.MainObj.GetComponent<genres>();
		}
	}

	public static long Week(gameScript g)
	{
		if (!g || g.sellsPerWeek == null || g.sellsPerWeek.Length == 0)
		{
			return 0L;
		}
		return Math.Max(0, g.sellsPerWeek[0]);
	}

	public static long Week(platformScript p)
	{
		if (!p || p.sellsPerWeek == null || p.sellsPerWeek.Length == 0)
		{
			return 0L;
		}
		return Math.Max(0, p.sellsPerWeek[0]);
	}

	// Games that sell copies: on the market, no bundles, not free-to-play or arcade machines.
	public static bool Selling(gameScript g)
	{
		if ((bool)g && g.isOnMarket && !g.inDevelopment && g.gameTyp != 2 && !g.arcade && !g.typ_bundle && !g.typ_bundleAddon)
		{
			return !g.schublade;
		}
		return false;
	}

	public static List<gameScript> TopSellers()
	{
		return (from g in All
			where Selling(g) && Week(g) > 0
			orderby Week(g) descending
			select g).ToList();
	}

	public static List<platformScript> Platforms()
	{
		platformScript[] all = (G.MS ? G.MS.arrayPlatformsScripts : null);
		if (all == null)
		{
			return new List<platformScript>();
		}
		return (from p in all
			where (bool)p && p.isUnlocked && !p.vomMarktGenommen
			orderby Week(p) descending
			select p).ToList();
	}

	public static Dictionary<int, long> GenreUnits()
	{
		Dictionary<int, long> map = new Dictionary<int, long>();
		foreach (gameScript g in All)
		{
			if (Selling(g) && g.maingenre >= 0)
			{
				map.TryGetValue(g.maingenre, out var n);
				map[g.maingenre] = n + Week(g);
			}
		}
		return map;
	}

	// How far a game in development is (0-1, or -1 if unknown), and roughly how many weeks until it's out. The
	// game only counts development points for your own games; a rival studio's game comes out when the studio's
	// countdown (publisherScript.newGameInWeeks) runs out.
	public static float DevProgress(gameScript g, out int weeksLeft)
	{
		weeksLeft = 0;
		if (!g)
		{
			return -1f;
		}
		if (g.devPointsStart_Gesamt > 0.01f)
		{
			return Mathf.Clamp01((g.devPointsStart_Gesamt - g.devPoints_Gesamt) / g.devPointsStart_Gesamt);
		}
		publisherScript studio = MmoBots.FindStudio(g.developerID);
		if (!studio || studio.newGameInWeeksORG <= 0)
		{
			return -1f;
		}
		weeksLeft = Mathf.Max(0, studio.newGameInWeeks);
		return Mathf.Clamp01(1f - (float)studio.newGameInWeeks / (float)studio.newGameInWeeksORG);
	}

	public static string GenreName(int i)
	{
		genres gen = Genres;
		if (!gen || i < 0)
		{
			return "?";
		}
		return gen.GetName(i);
	}

	public static Sprite GenrePic(int i)
	{
		genres gen = Genres;
		if (!gen || i < 0)
		{
			return null;
		}
		return gen.GetPic(i);
	}

	private static string PlatformType(int typ)
	{
		return typ switch
		{
			0 => "PC",
			1 => "Console",
			2 => "Handheld",
			3 => "Mobile",
			4 => "Arcade",
			_ => "",
		};
	}

	// ---- The page ----

	public override void Build(EditorWindow w)
	{
		seen = GameUpdates.Version;
		BackButton(w);
		Banner(w);
		Cards(w, 180f);
		float bw = (w.Width - 28f - 5f * 8f) / 6f;
		string[] icons = new string[6] { "iconWochencharts", "iconGenre", "iconP_Konsole", "iconPublisher", "iconStar", "iconHype" };
		for (int i = 0; i < Tabs.Length; i++)
		{
			int n = i;
			float x = 14f + (float)i * (bw + 8f);
			w.IconButton(Tabs[i], UpdateIcons.Game(icons[i], UpdateIcons.Store()), x, 270f, bw, 46f, () =>
			{
				tab = n;
				w.Rebuild();
			});
			if (i == tab)
			{
				MmoUi.Rect(w.Body, x + 6f, 318f, bw - 12f, 4f, new Color(0.18f, 0.5f, 0.86f));
			}
		}
		float y = 330f;
		float h = w.Height - 75f - y - 10f;
		switch (tab)
		{
		case 0:
			TopSellersTab(w, y, h);
			break;
		case 1:
			GenresTab(w, y, h);
			break;
		case 2:
			ConsolesTab(w, y, h);
			break;
		case 3:
			StudiosTab(w, y, h);
			break;
		case 4:
			NewReleasesTab(w, y, h);
			break;
		default:
			ComingSoonTab(w, y, h);
			break;
		}
	}

	private static void Banner(EditorWindow w)
	{
		w.Banner(UpdateIcons.Banner("charts"), 14f, 46f, w.Width - 28f, 124f, full: true);
		string[] months = new string[12] { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
		w.BannerText("<b>Current charts</b>", 30f, 58f, 560f, 34f, 24, Color.white);
		string date = (G.MS ? ("Week " + G.MS.week + " of " + months[Mathf.Clamp(G.MS.month - 1, 0, 11)] + " " + G.MS.year) : "");
		w.BannerText(date + "  ·  what sells right now", 30f, 94f, 560f, 24f, 14, new Color(0.84f, 0.88f, 0.94f));
		long market = All.Where(Selling).Sum(Week);
		w.BannerText("Games sold this week: <b>" + MmoUiText.Num(market) + "</b>", 30f, 122f, 560f, 24f, 14, new Color(1f, 0.82f, 0.36f));
	}

	private static void Cards(EditorWindow w, float y)
	{
		float cw = (w.Width - 28f - 30f) / 4f;
		float h = 80f;
		gameScript best = TopSellers().FirstOrDefault();
		w.StatCard(UpdateIcons.Game("iconBestGames", UpdateIcons.Game("iconWochencharts")), "Best seller this week", best ? best.GetNameSimple() : "-", best ? (MmoUiText.Num(Week(best)) + " copies  ·  " + best.GetDeveloperName()) : "", 14f, y, cw, h, null, best ? best.GetTooltip() : null);
		platformScript console = Platforms().FirstOrDefault((platformScript p) => p.typ != 0);
		w.StatCard(UpdateIcons.Game("iconKonsoleSells", UpdateIcons.Game("iconP_Konsole")), "Best-selling console", console ? console.GetName() : "-", console ? (MmoUiText.Num(Week(console)) + " sold this week") : "", 14f + (cw + 10f), y, cw, h, null, console ? console.GetTooltip() : null);
		KeyValuePair<int, long> genre = GenreUnits().OrderByDescending((KeyValuePair<int, long> kv) => kv.Value).FirstOrDefault();
		w.StatCard(GenrePic(genre.Key) ?? UpdateIcons.Game("iconGenre"), "Best-selling genre", (genre.Value > 0) ? GenreName(genre.Key) : "-", (genre.Value > 0) ? (MmoUiText.Num(genre.Value) + " copies this week") : "", 14f + 2f * (cw + 10f), y, cw, h);
		int trend = (G.MS ? G.MS.trendGenre : (-1));
		w.StatCard(UpdateIcons.Game("iconGenreTrend", UpdateIcons.Game("iconTrend")), "Trending genre", (trend >= 0) ? GenreName(trend) : "-", (G.MS && G.MS.trendAntiGenre >= 0) ? ("out of fashion: " + GenreName(G.MS.trendAntiGenre)) : "", 14f + 3f * (cw + 10f), y, cw, h, UIKit.Grey, "Games in the trending genre sell more for now; the one out of fashion sells less.");
	}

	// A game row: chart place, picture, name, its studio and genre, and its sales.
	private static void GameRow(EditorWindow w, ListView list, gameScript g, int rank, string right, string rightSmall)
	{
		RowView row = list.Row(52f, null, g.GetTooltip());
		if (g.IsMyGame())
		{
			row.Highlight();
		}
		EditorWindow.RankBadge(row.T, rank, 8f, 12f, 28f);
		MmoUi.Shot(w, g, 44f, 6f, 72f, 40f, row.T);
		row.Top(g.GetNameWithTag(), 126f, row.W - 400f);
		row.Bottom(g.GetDeveloperName() + "  ·  " + GenreName(g.maingenre) + "  ·  " + g.reviewTotal + "%  ·  " + ((g.weeksOnMarket == 1) ? "1 week" : (g.weeksOnMarket + " weeks")) + " on sale", 126f, row.W - 400f);
		row.Text(right, row.W - 270f, 260f, bold: true, TextAnchor.MiddleRight, 4f, 24f);
		row.Text(rightSmall, row.W - 270f, 260f, bold: false, TextAnchor.MiddleRight, 26f, 22f);
	}

	private void TopSellersTab(EditorWindow w, float y, float h)
	{
		List<string> options = new List<string> { "All games", "Your games", "Other studios' games", "Out less than 12 weeks" };
		w.Header("Top sellers this week", 14f, y, w.Width - 28f - 330f);
		w.Dropdown(w.Width - 334f, y - 3f, 320f, options, filter, (int v) =>
		{
			UIKit.Sound();
			filter = v;
			w.Rebuild();
		});
		IEnumerable<gameScript> games = TopSellers();
		switch (filter)
		{
		case 1:
			games = games.Where((gameScript g) => g.IsMyGame());
			break;
		case 2:
			games = games.Where((gameScript g) => !g.IsMyGame());
			break;
		case 3:
			games = games.Where((gameScript g) => g.weeksOnMarket < 12);
			break;
		}
		ListView list = w.List(13f, y + 32f, w.Width - 26f, h - 32f);
		int rank = 0;
		foreach (gameScript g in games.Take(40))
		{
			GameRow(w, list, g, ++rank, MmoUiText.Num(Week(g)) + " this week", MmoUiText.Num(g.sellsTotal) + " in all");
		}
		if (rank == 0)
		{
			w.Label("Nothing sold this week.", 24f, y + 40f, w.Width - 48f, 26f);
		}
	}

	private void GenresTab(EditorWindow w, float y, float h)
	{
		Dictionary<int, long> units = GenreUnits();
		List<KeyValuePair<int, long>> ranked = units.OrderByDescending((KeyValuePair<int, long> kv) => kv.Value).ToList();
		long total = Math.Max(1L, ranked.Sum((KeyValuePair<int, long> kv) => kv.Value));
		if (genreFilter < 0 || !units.ContainsKey(genreFilter))
		{
			genreFilter = ((ranked.Count > 0) ? ranked[0].Key : (-1));
		}
		float leftW = 440f;
		w.Header("Genres this week", 14f, y, leftW);
		ListView list = w.List(13f, y + 32f, leftW + 2f, h - 32f);
		genres gen = Genres;
		int trend = (G.MS ? G.MS.trendGenre : (-1));
		int anti = (G.MS ? G.MS.trendAntiGenre : (-1));
		for (int i = 0; i < ranked.Count; i++)
		{
			int id = ranked[i].Key;
			RowView row = list.Row(48f, () =>
			{
				genreFilter = id;
				w.Rebuild();
			}, "Show the best sellers in " + GenreName(id) + ".");
			if (id == genreFilter)
			{
				row.Highlight();
			}
			EditorWindow.RankBadge(row.T, i + 1, 6f, 11f, 26f);
			row.Picture(GenrePic(id), 38f, 30f);
			string mark = ((id == trend) ? "  <color=#2e7d32>trending</color>" : ((id == anti) ? "  <color=#c62828>out of fashion</color>" : ""));
			row.Top(GenreName(id) + mark, 76f, row.W - 150f);
			float pop = (((bool)gen && gen.genres_BELIEBTHEIT != null && id < gen.genres_BELIEBTHEIT.Length) ? gen.genres_BELIEBTHEIT[id] : 0f);
			row.Bottom(StoreUi.Pct((float)ranked[i].Value / (float)total) + " of sales  ·  popularity " + Mathf.RoundToInt(pop), 76f, row.W - 150f);
			row.Text(MmoUi.Short(ranked[i].Value), row.W - 140f, 130f, bold: true, TextAnchor.MiddleRight);
		}
		float rx = 14f + leftW + 14f;
		float rw = w.Width - rx - 14f;
		w.Header("Best sellers in " + ((genreFilter >= 0) ? GenreName(genreFilter) : "-"), rx, y, rw);
		ListView games = w.List(rx - 1f, y + 32f, rw + 2f, h - 32f);
		int rank = 0;
		foreach (gameScript g in TopSellers().Where((gameScript x) => x.maingenre == genreFilter).Take(25))
		{
			rank++;
			RowView row2 = games.Row(48f, null, g.GetTooltip());
			if (g.IsMyGame())
			{
				row2.Highlight();
			}
			EditorWindow.RankBadge(row2.T, rank, 6f, 11f, 26f);
			row2.Top(g.GetNameWithTag(), 40f, row2.W - 190f);
			row2.Bottom(g.GetDeveloperName() + "  ·  " + g.reviewTotal + "%", 40f, row2.W - 190f);
			row2.Text(MmoUi.Short(Week(g)), row2.W - 150f, 140f, bold: true, TextAnchor.MiddleRight);
		}
	}

	private void ConsolesTab(EditorWindow w, float y, float h)
	{
		List<string> options = new List<string> { "All platforms", "Consoles", "Handhelds", "PC", "Mobile", "Arcade" };
		w.Header("Platforms by sales this week", 14f, y, w.Width - 28f - 330f);
		w.Dropdown(w.Width - 334f, y - 3f, 320f, options, platformFilter, (int v) =>
		{
			UIKit.Sound();
			platformFilter = v;
			w.Rebuild();
		});
		int[] typeOf = new int[6] { -1, 1, 2, 0, 3, 4 };
		int wanted = typeOf[Mathf.Clamp(platformFilter, 0, 5)];
		List<platformScript> platforms = Platforms().Where((platformScript p) => wanted < 0 || p.typ == wanted).ToList();
		ListView list = w.List(13f, y + 32f, w.Width - 26f, h - 32f);
		int rank = 0;
		foreach (platformScript p in platforms.Take(40))
		{
			rank++;
			RowView row = list.Row(52f, null, p.GetTooltip());
			if (G.MS && p.ownerID == G.MS.myID)
			{
				row.Highlight();
			}
			EditorWindow.RankBadge(row.T, rank, 8f, 12f, 28f);
			Image pic = new GameObject("Platform", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
			pic.transform.SetParent(row.T, worldPositionStays: false);
			UIKit.Place(pic.gameObject, 44f, 4f, 44f, 44f);
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
			row.Top(p.GetName() + "  <color=#666666>" + PlatformType(p.typ) + "</color>", 98f, row.W - 400f);
			row.Bottom(p.GetManufacturer() + "  ·  " + p.GetMarktanteil().ToString("0.0") + "% of the market  ·  " + MmoUi.Short(p.GetAktiveNutzer()) + " players", 98f, row.W - 400f);
			row.Text(MmoUiText.Num(Week(p)) + " this week", row.W - 270f, 260f, bold: true, TextAnchor.MiddleRight, 4f, 24f);
			row.Text(MmoUiText.Num(p.units) + " in all", row.W - 270f, 260f, bold: false, TextAnchor.MiddleRight, 26f, 22f);
		}
		if (rank == 0)
		{
			w.Label("No platforms of this kind on the market.", 24f, y + 40f, w.Width - 48f, 26f);
		}
	}

	private void StudiosTab(EditorWindow w, float y, float h)
	{
		w.Header("Studios by sales this week (copies x price)", 14f, y, w.Width - 28f);
		Dictionary<int, long> money = new Dictionary<int, long>();
		Dictionary<int, long> copies = new Dictionary<int, long>();
		Dictionary<int, int> count = new Dictionary<int, int>();
		foreach (gameScript g in All)
		{
			if (Selling(g))
			{
				int id = ((g.publisherID >= 0) ? g.publisherID : g.developerID);
				long week = Week(g);
				money.TryGetValue(id, out var m);
				money[id] = m + week * StoreOps.Price(g);
				copies.TryGetValue(id, out var c);
				copies[id] = c + week;
				count.TryGetValue(id, out var n);
				count[id] = n + 1;
			}
		}
		ListView list = w.List(13f, y + 32f, w.Width - 26f, h - 32f);
		int rank = 0;
		foreach (KeyValuePair<int, long> kv in money.Where((KeyValuePair<int, long> x) => x.Value > 0).OrderByDescending((KeyValuePair<int, long> x) => x.Value).Take(40))
		{
			rank++;
			bool mine = (bool)G.MS && kv.Key == G.MS.myID;
			publisherScript p = MmoBots.FindStudio(kv.Key);
			RowView row = list.Row(52f, null, p ? p.GetTooltip() : null);
			if (mine)
			{
				row.Highlight();
			}
			EditorWindow.RankBadge(row.T, rank, 8f, 12f, 28f);
			row.Picture(p ? p.GetLogo() : null, 44f, 40f);
			row.Top(mine ? G.MS.GetCompanyName() : (p ? p.GetName() : "?"), 96f, row.W - 400f);
			row.Bottom(count[kv.Key] + ((count[kv.Key] == 1) ? " game" : " games") + " on the market", 96f, row.W - 400f);
			row.Text(G.Money(kv.Value) + " this week", row.W - 270f, 260f, bold: true, TextAnchor.MiddleRight, 4f, 24f);
			row.Text(MmoUiText.Num(copies[kv.Key]) + " copies", row.W - 270f, 260f, bold: false, TextAnchor.MiddleRight, 26f, 22f);
		}
	}

	private void NewReleasesTab(EditorWindow w, float y, float h)
	{
		List<string> options = new List<string> { "Most sold so far", "Best reviews" };
		w.Header("Out in the last 8 weeks", 14f, y, w.Width - 28f - 330f);
		w.Dropdown(w.Width - 334f, y - 3f, 320f, options, releaseSort, (int v) =>
		{
			UIKit.Sound();
			releaseSort = v;
			w.Rebuild();
		});
		IEnumerable<gameScript> games = All.Where((gameScript g) => Selling(g) && g.weeksOnMarket <= 8);
		games = ((releaseSort == 1) ? games.OrderByDescending((gameScript g) => g.reviewTotal).ThenByDescending((gameScript g) => g.sellsTotal) : games.OrderByDescending((gameScript g) => g.sellsTotal));
		ListView list = w.List(13f, y + 32f, w.Width - 26f, h - 32f);
		int rank = 0;
		foreach (gameScript g in games.Take(40))
		{
			GameRow(w, list, g, ++rank, MmoUiText.Num(g.sellsTotal) + " sold", MmoUiText.Num(Week(g)) + " this week");
		}
		if (rank == 0)
		{
			w.Label("Nothing new in the last 8 weeks.", 24f, y + 40f, w.Width - 48f, 26f);
		}
	}

	private void ComingSoonTab(EditorWindow w, float y, float h)
	{
		w.Header("Announced games, most anticipated first", 14f, y, w.Width - 28f);
		List<gameScript> games = (from g in All
			where (bool)g && g.inDevelopment && g.angekuendigt && !g.isOnMarket
			orderby g.hype descending
			select g).Take(40).ToList();
		ListView list = w.List(13f, y + 32f, w.Width - 26f, h - 32f);
		int rank = 0;
		foreach (gameScript g in games)
		{
			rank++;
			RowView row = list.Row(52f, null, g.GetTooltip());
			if (g.IsMyGame())
			{
				row.Highlight();
			}
			EditorWindow.RankBadge(row.T, rank, 8f, 12f, 28f);
			row.Picture(GenrePic(g.maingenre), 44f, 36f);
			row.Top(g.GetNameWithTag(), 92f, row.W - 420f);
			row.Bottom(g.GetDeveloperName() + "  ·  " + GenreName(g.maingenre), 92f, row.W - 420f);
			row.Bar(row.W - 310f, 300f, 20f, Mathf.Clamp01(g.hype / 100f), new Color(0.85f, 0.3f, 0.25f), "hype " + Mathf.RoundToInt(g.hype), null, 4f);
			float done = DevProgress(g, out int weeksLeft);
			string text = ((done < 0f) ? "progress unknown" : (Mathf.FloorToInt(done * 100f) + "% done" + ((weeksLeft > 0) ? ("  ·  out in about " + ((weeksLeft == 1) ? "1 week" : (weeksLeft + " weeks"))) : "")));
			row.Bar(row.W - 310f, 300f, 20f, Mathf.Max(0f, done), new Color(0.2f, 0.5f, 0.88f), text, null, 28f);
		}
		if (rank == 0)
		{
			w.Label("No games announced right now.", 24f, y + 40f, w.Width - 48f, 26f);
		}
	}

	public override void Tick(EditorWindow w)
	{
		if (GameUpdates.Version != seen && Time.unscaledTime > nextTick)
		{
			nextTick = Time.unscaledTime + 1f;
			w.Rebuild();
		}
	}
}
