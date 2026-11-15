using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HolyAddon.Core.CopyProtection;
using HolyAddon.Core.Updates;
using HolyAddon.UI;
using Newtonsoft.Json;
using UnityEngine;

namespace HolyAddon.Core.Industry;

internal class RelationNote
{
	public int Week;

	public int Delta;

	public string Text = "";
}

// What an event is about: the studios, games and platform it involves, and its numbers.
internal class IndustryContext
{
	public int StudioId = -1;

	public int Studio2 = -1;

	public int GameId = -1;

	public int Game2 = -1;

	public int PlatformId = -1;

	public long Amount;

	public long Amount2;

	public float Share;

	public float Share2;

	public int Number;

	public int Number2;

	// A name the event made up or was handed (a fan mod's name).
	public string Text = "";

	// More text the event worked out when it happened (what a console maker's exit ended).
	public string Text2 = "";
}

internal class IndustryPending
{
	public string Id = "";

	public IndustryContext C = new IndustryContext();

	public int Week;
}

// Money coming in or going out each week: a fixed sum, or a share of a game's revenue (once it's out).
internal class IncomeStream
{
	public string Label = "";

	public int StudioId = -1;

	public int GameId = -1;

	public float Share;

	public long Fixed;

	public int WeeksLeft;

	public bool Out;

	public int Category = 2;

	// Where money going out shows in the finances (mainScript.Pay; 7 = licences, 8 = misc).
	public int PayCategory = 7;

	public long LastRevenue = -1;

	public bool Started;
}

internal class SalesMod
{
	public int GameId;

	public float Amount;

	public int Weeks;

	public int WeeksLeft;

	// Starts counting down only once the game is out (set for games still in development).
	public bool Wait;
}

// "Release a game on it within a year, or pay for the kit."
internal class PlatformPromise
{
	public int PlatformId;

	public int Start;

	public int Deadline;

	public long Price;
}

// A competitor's game set to come out the same week as yours (Release Clash): once both are out, the better-reviewed
// one wins the launch weeks (IndustryCompetitors). Edge: review points your marketing adds.
internal class ReleaseClash
{
	public int GameId;

	public int TheirGameId;

	public int StudioId;

	public int Edge;

	public int Expires;

	// "clash" (both come out the same week) or "copy" (the arch-rival's copy of your hit comes out).
	public string Kind = "clash";
}

// How a release clash or a copycat turned out, waiting to be shown in its pop-up (UI: ClashResultPage).
internal class ClashOutcome
{
	public string Kind = "clash";

	public int GameId;

	public int TheirGameId;

	public int StudioId;

	public string MyName = "";

	public string TheirName = "";

	public int Mine;

	public int Theirs;

	public int Edge;

	public bool Won;

	public string Effect = "";

	public string Quote = "";

	public string OutletId = "";

	public string OutletName = "";
}

// A price war's cut (Price War): the game sells for Factor of its price while it lasts (applied in
// Store/SalesEvents' price path, so Holy and the store's sales handle it like a sale's discount).
internal class PriceCut
{
	public int GameId;

	public float Factor = 1f;

	public int WeeksLeft;
}

// Publishers Bid: your game in development is promised to the publisher you went with (Core/Industry/PublishDeals).
internal class PublishDeal
{
	public int GameId;

	public int StudioId;

	public long Advance;
}

internal class IndustryState
{
	// Your standing with each studio, -100 to 100 (0 = neutral), from what happened between you.
	public Dictionary<int, float> Standing = new Dictionary<int, float>();

	public Dictionary<int, List<RelationNote>> Notes = new Dictionary<int, List<RelationNote>>();

	public Dictionary<string, int> LastFired = new Dictionary<string, int>();

	public Dictionary<string, int> Cooldowns = new Dictionary<string, int>();

	public int LastDecision = -1;

	public int NextHeadline = -1;

	public IndustryPending Pending;

	public List<IncomeStream> Streams = new List<IncomeStream>();

	public List<SalesMod> Mods = new List<SalesMod>();

	public List<PlatformPromise> Promises = new List<PlatformPromise>();

	public List<PublishDeal> Deals = new List<PublishDeal>();

	public List<PriceCut> PriceCuts = new List<PriceCut>();

	// The latest headlines and decisions, newest first.
	public List<string> Feed = new List<string>();

	public List<int> KnownOpen = new List<int>();

	public List<int> KnownClosed = new List<int>();

	public bool Seeded;

	public List<int> DealtGames = new List<int>();

	// Studios knocked down by the 0.20-0.22 stars bug have been put back (once per save).
	public bool StarsChecked;

	// ---- Rivals (Core/Industry/IndustryRivals): your arch-rival and your record against rivals ----

	public int ArchRival = -1;

	public int RivalNextMove = -1;

	public int RivalQuietUntil = -1;

	public int RivalDeclareAfter = -1;

	public int RivalWins;

	public int RivalLosses;

	public int PoachedFromThem;

	public int PoachedFromUs;

	public int RivalsDrivenOut;

	public int ComebackId = -1;

	public int ComebackWeek = -1;

	public bool HolyRivalCarried;

	// ---- Competitors (Core/Industry/IndustryCompetitors): the studios you meet most in the market ----

	public List<int> Competitors = new List<int>();

	// The yearly re-pick.
	public int CompetitorsNext = -1;

	// One of your games came out: pick again next week.
	public bool CompetitorsStale;

	// No competitor fight before this week (at most one every 8 weeks or so).
	public int CompetitorFightAfter = -1;

	public List<ReleaseClash> Clashes = new List<ReleaseClash>();

	// Results waiting for their pop-up, oldest first.
	public List<ClashOutcome> Outcomes = new List<ClashOutcome>();

	// Share a Stand?: a studio pays half your stand at this year's summer trade fair (IndustryHooks).
	public StandShare Stand;

	// Same Idea's Rush It: your game -> the competitor's. Out first, it gets the launch buzz (IndustryCompetitors).
	public Dictionary<int, int> Rushed = new Dictionary<int, int>();

	// Cover Disc: games whose unfinished demo went out; at release it may cost them a few review points (IndustryHooks).
	public List<int> CoverDiscs = new List<int>();

	// Consoles The Holy Expansion gave a weak launch (its early dev kit flop): never "sold out at launch".
	public List<int> FloppedConsoles = new List<int>();

	// Holy's "Mod of the month" picks for your games, newest last (the Mod Team decision; IndustryCommunity).
	public List<ModHit> ModHits = new List<ModHit>();

	// Support rooms set to do community work by themselves, and those whose calls are paused for it.
	public List<int> AutoSupport = new List<int>();

	public List<int> PausedCalls = new List<int>();

	// Fund a Port: a platform maker pays a share of porting one of your games to its machine (GameUpdates.PlanPort).
	public List<PortDeal> PortDeals = new List<PortDeal>();

	// Engine Request: a studio's next game is built on your engine (IndustryHooks, publisherScript.FindEngineForGameNew).
	public List<EngineDeal> EngineDeals = new List<EngineDeal>();

	// Studios that closed recently (studio id -> week): their IPs may come up at auction.
	public Dictionary<int, int> RecentClosures = new Dictionary<int, int>();

	// A Cartridge Shortage: the production room makes no copies of a game, or makes them dearer, for a while
	// (IndustryReal, taskProduction.Work).
	public List<ProductionLimit> ProductionLimits = new List<ProductionLimit>();

	// A Cartoon: animated series of your IPs, in production or out (IndustryReal; with Holy, its Movie & TV deal).
	public List<CartoonDeal> CartoonDeals = new List<CartoonDeal>();
}

internal class ProductionLimit
{
	public int GameId;

	public int Expires;

	// No copies at all while it lasts.
	public bool Block;

	// Extra cost per copy, as a share of the game's own production cost.
	public float Surcharge;
}

internal class CartoonDeal
{
	// The IP's main game.
	public int IpGameId;

	public string Studio = "";

	public int ReleaseWeek;

	// You put money in: its merchandise comes with it.
	public bool CoProduced;

	// Holy runs the premiere (its Movie & TV deal); otherwise the addon does.
	public bool Holy;

	// 0 in production, 1 hit, 2 solid, 3 flop.
	public int Result;
}

internal class PortDeal
{
	public int GameId;

	public int PlatformId;

	public float Share;

	public int Expires;
}

internal class EngineDeal
{
	public int StudioId;

	public int EngineId;

	public int Share;
}

internal class ModHit
{
	public int GameId;

	public string Name = "";

	public int Week;
}

internal class StandShare
{
	public int StudioId;

	public int GameId;

	public int Year;
}

// Industry events: the industry around you moves (headlines in the news), and things land on your desk (pop-up
// decisions with choices) that change your money and fans, the market, and your standing with other studios. The
// director paces decisions at random: the longer it's been quiet, the likelier one becomes, and a fitting moment
// (you just released a game) makes it likelier still. Events are in IndustryEvents.
[HarmonyPatch]
internal static class IndustryCore
{
	private const string SaveKey = "mgt2.editor.industry";

	private static IndustryState S = new IndustryState();

	private static string shownFor;

	public static IndustryState State => S;

	public static bool On
	{
		get
		{
			if (Plugin.IndustryEvents.Value)
			{
				return G.Singleplayer;
			}
			return false;
		}
	}

	public static int Now => CopyProtectDev.AbsWeek;

	public static int Year => G.MS ? G.MS.year : 1976;

	public static readonly string[] PaceNames = new string[3] { "Calm", "Normal", "Busy" };

	private static float Pace => Plugin.IndustryPace.Value switch
	{
		0 => 0.6f,
		2 => 1.6f,
		_ => 1f,
	};

	// ---- Studios and standing ----

	public static List<publisherScript> Studios()
	{
		publisherScript[] all = (G.MS ? G.MS.arrayPublisherScripts : null);
		if (all == null)
		{
			return new List<publisherScript>();
		}
		return all.Where((publisherScript s) => (bool)s && !s.isPlayer && s.isUnlocked && !s.Geschlossen() && !s.IsTochterfirma()).ToList();
	}

	public static publisherScript Studio(int id)
	{
		publisherScript[] all = (G.MS ? G.MS.arrayPublisherScripts : null);
		if (all == null || id < 0)
		{
			return null;
		}
		return all.FirstOrDefault((publisherScript s) => (bool)s && s.myID == id);
	}

	// Your events with them, plus a little from your publishing history (the game's own relation, 0-100).
	public static float Standing(publisherScript p)
	{
		if (!p)
		{
			return 0f;
		}
		float own = (S.Standing.TryGetValue(p.myID, out var v) ? v : 0f);
		return Mathf.Clamp(own + p.relation * 0.3f, -100f, 100f);
	}

	public static int Tier(float standing)
	{
		if (standing <= -40f)
		{
			return 0;
		}
		if (standing < 30f)
		{
			return 1;
		}
		if (standing < 70f)
		{
			return 2;
		}
		return 3;
	}

	public static readonly string[] TierNames = new string[4] { "Rival", "Neutral", "Friendly", "Ally" };

	// A studio's stars are 0-100 in the game (20 per star shown; the data's studios start at 15-100). Events nudge
	// them by a couple of points, never below 10.
	public static void AddStars(publisherScript p, float delta)
	{
		if ((bool)p)
		{
			p.stars = Mathf.Clamp(p.stars + delta, 10f, 100f);
		}
	}

	// Versions 0.20.0-0.22.0 treated stars as 0-5, which dropped studios to 5 (out of 100) after a hit, a star hire or
	// a staff swap. Puts any studio below 10 back to its stars in the game's studio data (Holy's merged list when
	// Holy made one). Once per save.
	private static void RepairStars()
	{
		publisherScript[] all = (G.MS ? G.MS.arrayPublisherScripts : null);
		if (all == null)
		{
			return;
		}
		Dictionary<int, float> data = null;
		foreach (publisherScript p in all)
		{
			if (!p || p.isPlayer || p.stars >= 10f)
			{
				continue;
			}
			data = data ?? DataStars();
			float before = p.stars;
			p.stars = (data.TryGetValue(p.myID, out var s) ? s : 40f);
			Plugin.Log.LogInfo("Industry: restored " + p.GetName() + "'s stars from " + before.ToString("0.#") + " to " + p.stars.ToString("0") + " (out of 100)");
		}
	}

	private static Dictionary<int, float> DataStars()
	{
		Dictionary<int, float> map = new Dictionary<int, float>();
		string[] files = new string[2]
		{
			System.IO.Path.Combine(BepInEx.Paths.PluginPath, "HolyExpansion", "cache", "DATA_Publisher.txt"),
			System.IO.Path.Combine(Application.dataPath, "Extern", "Text", "DATA", "Publisher.txt")
		};
		foreach (string f in files)
		{
			try
			{
				if (!System.IO.File.Exists(f))
				{
					continue;
				}
				string text = System.IO.File.ReadAllText(f);
				if (!text.Contains("[ID]"))
				{
					text = System.IO.File.ReadAllText(f, System.Text.Encoding.Unicode);
				}
				int id = -1;
				foreach (string raw in text.Split('\n'))
				{
					string line = raw.Trim();
					if (line.StartsWith("[ID]") && int.TryParse(line.Substring(4).Trim(), out var n))
					{
						id = n;
					}
					else if (line.StartsWith("[MARKET]") && id >= 0 && float.TryParse(line.Substring(8).Trim(), out var m))
					{
						map[id] = Mathf.Clamp(m, 10f, 100f);
					}
				}
				if (map.Count > 0)
				{
					return map;
				}
			}
			catch (Exception ex)
			{
				Plugin.Log.LogWarning("Industry: could not read " + f + ": " + ex.Message);
			}
		}
		return map;
	}

	public static Color TierColor(int tier)
	{
		return tier switch
		{
			0 => new Color(0.78f, 0.16f, 0.16f),
			2 => new Color(0.18f, 0.55f, 0.25f),
			3 => new Color(0.1f, 0.42f, 0.75f),
			_ => new Color(0.45f, 0.45f, 0.45f),
		};
	}

	public static string TierOf(publisherScript p)
	{
		return TierNames[Tier(Standing(p))];
	}

	// Changes your standing with a studio (good turns also improve the game's own relation: better publishing terms).
	public static void Change(publisherScript p, int delta, string why, bool quiet = false)
	{
		if (!p || delta == 0)
		{
			return;
		}
		int before = Tier(Standing(p));
		S.Standing[p.myID] = Mathf.Clamp((S.Standing.TryGetValue(p.myID, out var v) ? v : 0f) + (float)delta, -100f, 100f);
		if (delta > 0)
		{
			p.relation = Mathf.Min(100f, p.relation + (float)delta * 0.5f);
		}
		if (!S.Notes.TryGetValue(p.myID, out var notes))
		{
			notes = (S.Notes[p.myID] = new List<RelationNote>());
		}
		notes.Insert(0, new RelationNote
		{
			Week = Now,
			Delta = delta,
			Text = why
		});
		if (notes.Count > 12)
		{
			notes.RemoveAt(notes.Count - 1);
		}
		int after = Tier(Standing(p));
		if (after != before && !quiet)
		{
			News("<b>" + p.GetName() + "</b> now sees you as " + ((after == 3) ? "an ally" : ("a " + TierNames[after].ToLower())) + ".");
		}
		GameUpdates.Touch();
	}

	// ---- Your games, the market ----

	public static List<gameScript> AllGames()
	{
		gameScript[] all = ((G.MS && (bool)G.MS.games_) ? G.MS.games_.arrayGamesScripts : null);
		if (all == null)
		{
			return new List<gameScript>();
		}
		return all.Where((gameScript g) => (bool)g).ToList();
	}

	public static List<gameScript> MyGamesOnMarket()
	{
		return AllGames().Where((gameScript g) => g.isOnMarket && !g.inDevelopment && g.IsMyGame() && !g.typ_bundle && !g.typ_bundleAddon && !g.typ_contractGame).ToList();
	}

	public static List<gameScript> MyGamesInDevelopment()
	{
		return AllGames().Where((gameScript g) => g.inDevelopment && g.IsMyGame() && !g.typ_contractGame && !g.auftragsspiel).ToList();
	}

	public static genres Genres => G.MainObj ? G.MainObj.GetComponent<genres>() : null;

	public static string GenreName(int g)
	{
		genres gen = Genres;
		if (!gen || g < 0)
		{
			return "games";
		}
		return gen.GetName(g);
	}

	// A craze becomes the game's trend genre (the top bar, and the game's own trend bonus), like the game's trendsetter
	// hits do. It holds for the rest of the current trend period, at least 12 weeks. The timing and the trend topic
	// stay the game's own, so topics keep changing as usual. Returns the weeks left (0: nothing changed).
	public static int MakeTrend(int genre)
	{
		mainScript ms = G.MS;
		genres gen = Genres;
		if (!ms || !gen || ms.multiplayer || genre < 0 || genre >= gen.genres_UNLOCK.Length || !gen.genres_UNLOCK[genre] || ms.trendGenre == genre)
		{
			return 0;
		}
		ms.trendGenre = genre;
		if (ms.trendAntiGenre == genre)
		{
			List<int> others = Enumerable.Range(0, gen.genres_UNLOCK.Length).Where((int i) => gen.genres_UNLOCK[i] && i != genre).ToList();
			if (others.Count > 0)
			{
				ms.trendAntiGenre = others[UnityEngine.Random.Range(0, others.Count)];
			}
		}
		ms.trendWeeks = Mathf.Max(ms.trendWeeks, 12);
		RefreshTrend();
		return ms.trendWeeks;
	}

	// Genre fatigue makes it the trend's "out of fashion" genre (unless it's the trend itself).
	public static bool MakeAntiTrend(int genre)
	{
		mainScript ms = G.MS;
		genres gen = Genres;
		if (!ms || !gen || ms.multiplayer || genre < 0 || genre >= gen.genres_UNLOCK.Length || ms.trendGenre == genre || ms.trendAntiGenre == genre)
		{
			return false;
		}
		ms.trendAntiGenre = genre;
		RefreshTrend();
		return true;
	}

	private static void RefreshTrend()
	{
		try
		{
			if ((bool)G.Gui)
			{
				G.Gui.UpdateTrend();
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Industry: trend display: " + ex.Message);
		}
	}

	// A genre as a plural, where its name stands for its games ("loves Role-Playing Games", "best sellers in Sports
	// Games"). Names that don't end in "Game" read fine as they are ("loves Simulation").
	public static string Plural(string genre)
	{
		if (genre != null && (genre.EndsWith("Game") || genre.EndsWith("game")))
		{
			return genre + "s";
		}
		return genre;
	}

	public static string GenrePlural(int g)
	{
		return Plural(GenreName(g));
	}

	// Some genres' names already end in "Game" ("Sports Game", "Role-Playing Game"): "Sports Games", "Action games".
	public static string GenreGames(int g)
	{
		string n = GenreName(g);
		if (n.EndsWith("Game") || n.EndsWith("game"))
		{
			return n + "s";
		}
		if (n.EndsWith("Games") || n.EndsWith("games"))
		{
			return n;
		}
		return n + " games";
	}

	// "a Sports Game", "an Action game".
	public static string AGenreGame(int g)
	{
		string n = GenreName(g);
		if (g < 0 || n == "games")
		{
			return "a game";
		}
		string game = ((n.EndsWith("Game") || n.EndsWith("game")) ? n : (n + " game"));
		return (("AEIOUaeiou".IndexOf(game[0]) >= 0) ? "an " : "a ") + game;
	}

	// What a game was sent on, by year.
	public static string Media()
	{
		if (Year < 1983)
		{
			return "cassette";
		}
		if (Year < 1993)
		{
			return "floppy disk";
		}
		if (Year < 2003)
		{
			return "CD";
		}
		return "download link";
	}

	public static void Feed(string text)
	{
		S.Feed.Insert(0, ((G.Gui != null) ? (G.Gui.GetDate().Replace("\r", "").Replace("\n", " ") + ": ") : "") + text);
		if (S.Feed.Count > 40)
		{
			S.Feed.RemoveAt(S.Feed.Count - 1);
		}
	}

	public static void News(string text)
	{
		if ((bool)G.Gui)
		{
			G.Gui.CreateTopNewsInfo(text);
		}
	}

	// ---- Lasting effects ----

	public static void AddMod(gameScript g, float amount, int weeks)
	{
		if ((bool)g)
		{
			S.Mods.Add(new SalesMod
			{
				GameId = g.myID,
				Amount = amount,
				Weeks = weeks,
				WeeksLeft = weeks
			});
		}
	}

	// A sales change that starts when the game comes out (straight away if it's already out).
	public static void AddModAfterRelease(gameScript g, float amount, int weeks)
	{
		if ((bool)g)
		{
			S.Mods.Add(new SalesMod
			{
				GameId = g.myID,
				Amount = amount,
				Weeks = weeks,
				WeeksLeft = weeks,
				Wait = !g.isOnMarket || g.releaseDate > 0
			});
		}
	}

	// What events add to a game's sales (into Holy's total and cap with Holy, like the addon's other bonuses).
	public static float Bonus(gameScript g)
	{
		if (!On || !g || S.Mods.Count == 0)
		{
			return 0f;
		}
		float total = 0f;
		foreach (SalesMod m in S.Mods)
		{
			if (m.GameId == g.myID && m.WeeksLeft > 0 && !m.Wait)
			{
				total += m.Amount;
			}
		}
		// Events add at most +-40% to one game, so they can't fill Holy's whole cap (paid choices still buy something).
		return Mathf.Clamp(total, -0.4f, 0.4f);
	}

	// Fund a Port: the share of porting this game to this platform its maker pays (0 = none, or the deal has run out).
	public static float PortShare(gameScript g, int platformId)
	{
		if (!g || S.PortDeals == null)
		{
			return 0f;
		}
		PortDeal d = S.PortDeals.FirstOrDefault((PortDeal x) => x.GameId == g.myID && x.PlatformId == platformId && Now <= x.Expires);
		if (d == null)
		{
			return 0f;
		}
		return Mathf.Clamp01(d.Share);
	}

	public static void UsePortDeals(gameScript g, int[] platforms)
	{
		if (!g || S.PortDeals == null || platforms == null)
		{
			return;
		}
		foreach (PortDeal d in S.PortDeals.Where((PortDeal x) => x.GameId == g.myID && platforms.Contains(x.PlatformId)).ToList())
		{
			S.PortDeals.Remove(d);
			platformScript p = GameUpdates.Platform(d.PlatformId);
			Change(p ? Studio(p.ownerID) : null, 6, "You ported " + g.GetNameSimple() + " to their machine");
			Feed("The port of " + g.GetNameSimple() + " to " + (p ? p.GetName() : "the new machine") + " has started, " + Mathf.RoundToInt(d.Share * 100f) + "% paid by its maker.");
		}
	}

	// The share of its price a game sells for this week because of a price war (1 = full price).
	public static float PriceCutFactor(gameScript g)
	{
		if (!On || !g || S.PriceCuts == null || S.PriceCuts.Count == 0)
		{
			return 1f;
		}
		float f = 1f;
		foreach (PriceCut c in S.PriceCuts)
		{
			if (c.GameId == g.myID && c.WeeksLeft > 0)
			{
				f *= Mathf.Clamp(c.Factor, 0.3f, 1f);
			}
		}
		return f;
	}

	public static void AddPriceCut(gameScript g, float factor, int weeks)
	{
		if ((bool)g)
		{
			S.PriceCuts.RemoveAll((PriceCut c) => c.GameId == g.myID);
			S.PriceCuts.Add(new PriceCut
			{
				GameId = g.myID,
				Factor = factor,
				WeeksLeft = weeks
			});
		}
	}

	private static void Effects()
	{
		foreach (PriceCut cut in S.PriceCuts)
		{
			cut.WeeksLeft--;
		}
		S.PriceCuts.RemoveAll((PriceCut cut) => cut.WeeksLeft <= 0);
		foreach (SalesMod m in S.Mods)
		{
			if (m.Wait)
			{
				// Waits until the game ships: still in development, or on pre-order (on the market, release date ahead).
				gameScript waiting = GameUpdates.FindGame(m.GameId);
				if ((bool)waiting && ((waiting.inDevelopment && !waiting.isOnMarket) || (waiting.isOnMarket && waiting.releaseDate > 0)))
				{
					continue;
				}
				m.Wait = false;
			}
			m.WeeksLeft--;
		}
		S.Mods.RemoveAll((SalesMod m) => m.WeeksLeft <= 0);
		foreach (IncomeStream s in S.Streams.ToList())
		{
			if (s.GameId >= 0)
			{
				gameScript g = GameUpdates.FindGame(s.GameId);
				if (!g)
				{
					S.Streams.Remove(s);
					continue;
				}
				if (!g.isOnMarket || g.releaseDate > 0)
				{
					// Not out yet, or on pre-order: waits (a game that's gone without release ends it).
					if (!g.isOnMarket && !g.inDevelopment)
					{
						S.Streams.Remove(s);
					}
					continue;
				}
				if (!s.Started)
				{
					s.Started = true;
					s.LastRevenue = g.umsatzTotal;
					continue;
				}
				long amount = (long)((float)Math.Max(0L, g.umsatzTotal - s.LastRevenue) * s.Share);
				s.LastRevenue = g.umsatzTotal;
				Move(s, amount);
			}
			else
			{
				Move(s, s.Fixed);
			}
			s.WeeksLeft--;
			if (s.WeeksLeft <= 0)
			{
				S.Streams.Remove(s);
				Feed(s.Label + " has ended.");
			}
		}
		foreach (PlatformPromise p in S.Promises.ToList())
		{
			platformScript platform = GameUpdates.Platform(p.PlatformId);
			if (!platform)
			{
				S.Promises.Remove(p);
				continue;
			}
			gameScript kept = MyGamesOnMarket().FirstOrDefault((gameScript g) => g.gamePlatform != null && g.gamePlatform.Contains(p.PlatformId) && g.date_year * 48 + (g.date_month - 1) * 4 >= p.Start - 8);
			if ((bool)kept)
			{
				S.Promises.Remove(p);
				G.MS.AddFans(Mathf.RoundToInt(500f * (float)Math.Max(1, Year - 1975) * 0.1f + 300f), -1);
				News("<b>" + platform.GetName() + "</b>'s maker is pleased: " + kept.GetNameSimple() + " is out on its machine. The dev kit is yours to keep.");
				Feed("Kept the promise to release on " + platform.GetName() + ".");
			}
			else if (Now >= p.Deadline)
			{
				S.Promises.Remove(p);
				G.MS.Pay(p.Price, 3);
				News("No game of yours came out on <b>" + platform.GetName() + "</b> in time: you pay " + G.Money(p.Price) + " for the dev kit after all.");
				Feed("Paid " + G.Money(p.Price) + " for the " + platform.GetName() + " dev kit (promise not kept).");
			}
		}
	}

	private static void Move(IncomeStream s, long amount)
	{
		if (amount <= 0 || !G.MS)
		{
			return;
		}
		if (s.Out)
		{
			G.MS.Pay(amount, s.PayCategory);
		}
		else
		{
			G.MS.Earn(amount, s.Category);
		}
	}

	// ---- The director ----

	[HarmonyPostfix]
	[HarmonyPatch(typeof(mainScript), "WochenUpdates")]
	private static void Weekly(mainScript __instance)
	{
		try
		{
			if (!On)
			{
				return;
			}
			if (!S.StarsChecked)
			{
				S.StarsChecked = true;
				RepairStars();
			}
			Effects();
			StudioChanges();
			IndustryRivals.Weekly();
			IndustryCompetitors.Weekly();
			IndustryHooks.Weekly();
			if (S.NextHeadline < 0)
			{
				S.NextHeadline = Now + UnityEngine.Random.Range(2, 5);
			}
			if (Now >= S.NextHeadline)
			{
				IndustryEvents.Headline();
				S.NextHeadline = Now + Mathf.RoundToInt((float)UnityEngine.Random.Range(3, 8) / Pace);
			}
			if (S.Pending == null && Quiet())
			{
				int since = ((S.LastDecision < 0) ? 6 : (Now - S.LastDecision));
				float chance = ((since < 4) ? 0f : Mathf.Min(0.3f, 0.035f + 0.012f * (float)(since - 4))) * Pace;
				if (MyGamesOnMarket().Any((gameScript g) => g.weeksOnMarket <= 1))
				{
					chance *= 1.5f;
				}
				if (UnityEngine.Random.value < chance)
				{
					IndustryPending p = IndustryEvents.PickDecision();
					if (p != null)
					{
						S.Pending = p;
						S.LastDecision = Now;
						S.LastFired[p.Id] = Now;
						if (p.Id.StartsWith("cp_"))
						{
							S.CompetitorFightAfter = Now + IndustryCompetitors.FightGap;
						}
						shownFor = null;
					}
				}
			}
			else if (S.Pending != null && Now - S.Pending.Week > 8)
			{
				// Left unanswered for two months: it goes the default way.
				Decide(-1);
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Industry events: " + ex);
		}
	}

	// Not in a week when another big event is on screen or waiting (the game's world events, Holy's PR events).
	internal static bool Quiet()
	{
		if (!G.Gui || G.Gui.menuOpen)
		{
			return false;
		}
		try
		{
			if (G.Gui.uiObjects != null && G.Gui.uiObjects.Length > 216 && (bool)G.Gui.uiObjects[216] && G.Gui.uiObjects[216].activeSelf)
			{
				return false;
			}
		}
		catch
		{
		}
		object pr = HolyCompat.HolyState("Pr");
		if (pr != null && HolyCompat.Get<object>(pr, "Pending") != null)
		{
			return false;
		}
		return true;
	}

	// New studios and closures make the news (the first look at a save only takes note of them).
	private static void StudioChanges()
	{
		publisherScript[] all = (G.MS ? G.MS.arrayPublisherScripts : null);
		if (all == null)
		{
			return;
		}
		foreach (publisherScript p in all)
		{
			if (!p || p.isPlayer || p.IsTochterfirma())
			{
				continue;
			}
			bool closed = p.Geschlossen();
			if (p.isUnlocked && !closed && !S.KnownOpen.Contains(p.myID))
			{
				S.KnownOpen.Add(p.myID);
				if (S.Seeded)
				{
					string text = "<b>" + p.GetName() + "</b> opens its doors" + ((p.fanGenre >= 0) ? (": a new studio with a love of " + GenreGames(p.fanGenre) + ".") : ".");
					News(text);
					Feed(text);
				}
			}
			if (closed && !S.KnownClosed.Contains(p.myID))
			{
				S.KnownClosed.Add(p.myID);
				if (S.Seeded)
				{
					S.RecentClosures[p.myID] = Now;
				}
				if (S.Seeded)
				{
					string text2 = "<b>" + p.GetName() + "</b> has closed its doors.";
					News(text2);
					Feed(text2);
				}
			}
		}
		S.Seeded = true;
		try
		{
			IndustryEvents.CancelOrphans();
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Industry: cancelling closed studios' games: " + ex.Message);
		}
	}

	// Opens the decision's pop-up once nothing else is on screen; closing it goes the default way.
	// The clash or copycat result on screen (it leaves the queue once its pop-up is closed).
	private static ClashOutcome shownOutcome;

	public static void Tick()
	{
		if (!On || !G.Gui)
		{
			return;
		}
		if (shownOutcome != null && !EditorWindow.ClashResult.IsOpen)
		{
			S.Outcomes.Remove(shownOutcome);
			shownOutcome = null;
		}
		if (S.Pending == null)
		{
			if (shownOutcome == null && S.Outcomes.Count > 0 && !G.Gui.menuOpen && !EditorWindow.IndustryEvent.IsOpen && !EditorWindow.ClashResult.IsOpen)
			{
				shownOutcome = S.Outcomes[0];
				EditorWindow.ClashResult.Open();
				Awards.AwardsShow.Sound(50);
			}
			return;
		}
		string key = S.Pending.Id + "@" + S.Pending.Week;
		if (shownFor != key)
		{
			if (!G.Gui.menuOpen && !EditorWindow.IndustryEvent.IsOpen)
			{
				shownFor = key;
				EditorWindow.IndustryEvent.Open();
				Awards.AwardsShow.Sound(50);
			}
		}
		else if (!EditorWindow.IndustryEvent.IsOpen)
		{
			Decide(-1);
		}
	}

	// Testing (cheats on): makes a decision pending now; it pops up once no window is open.
	// The result the pop-up shows (null when none).
	public static ClashOutcome ShownOutcome => shownOutcome;

	public static void Force(IndustryPending p)
	{
		S.Pending = p;
		S.LastDecision = Now;
		S.LastFired[p.Id] = Now;
		shownFor = null;
	}

	// Applies a choice (-1 = the event's default) and returns what happened.
	public static string Decide(int choice)
	{
		IndustryPending p = S.Pending;
		if (p == null)
		{
			return null;
		}
		S.Pending = null;
		shownFor = null;
		IndustryEventDef def = IndustryEvents.Find(p.Id);
		if (def == null)
		{
			return null;
		}
		List<IndustryChoice> choices = def.Choices(p.C);
		int i = ((choice < 0 || choice >= choices.Count) ? Mathf.Clamp(def.Default, 0, choices.Count - 1) : choice);
		if (choice < 0 && choices[i].Problem != null && choices[i].Problem(p.C) != null)
		{
			// Going the default way (the pop-up was closed or left) never takes a choice that can't be made, such as
			// spending money you don't have: the last one that can (usually the free "no" choice).
			int ok = choices.FindLastIndex((IndustryChoice ch) => ch.Problem == null || ch.Problem(p.C) == null);
			if (ok >= 0)
			{
				i = ok;
			}
		}
		string result;
		try
		{
			result = choices[i].Apply(p.C);
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Industry event " + p.Id + ": " + ex);
			result = "Something went wrong.";
		}
		Feed(def.Title(p.C) + ": " + choices[i].Title + ".");
		GameUpdates.Touch();
		return result;
	}

	// ---- Saving ----

	[HarmonyPostfix]
	[HarmonyPatch(typeof(savegameScript), "SaveHistory")]
	private static void Save(ES3Writer writer)
	{
		if (!G.Singleplayer)
		{
			return;
		}
		try
		{
			writer.Write<string>(SaveKey, JsonConvert.SerializeObject(S));
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Saving the industry failed: " + ex);
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(savegameScript), "Load")]
	private static void BeforeLoad()
	{
		S = new IndustryState();
		shownFor = null;
		shownOutcome = null;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(savegameScript), "LoadHistory")]
	private static void Load(ES3File ___es3file)
	{
		try
		{
			S = ((___es3file != null && ___es3file.KeyExists(SaveKey)) ? JsonConvert.DeserializeObject<IndustryState>(___es3file.Load<string>(SaveKey)) : null) ?? new IndustryState();
			S.Standing = S.Standing ?? new Dictionary<int, float>();
			S.Notes = S.Notes ?? new Dictionary<int, List<RelationNote>>();
			S.LastFired = S.LastFired ?? new Dictionary<string, int>();
			S.Cooldowns = S.Cooldowns ?? new Dictionary<string, int>();
			S.Streams = S.Streams ?? new List<IncomeStream>();
			S.Mods = S.Mods ?? new List<SalesMod>();
			S.Promises = S.Promises ?? new List<PlatformPromise>();
			S.Deals = S.Deals ?? new List<PublishDeal>();
			S.PriceCuts = S.PriceCuts ?? new List<PriceCut>();
			S.Competitors = S.Competitors ?? new List<int>();
			S.Clashes = S.Clashes ?? new List<ReleaseClash>();
			S.Outcomes = S.Outcomes ?? new List<ClashOutcome>();
			S.Rushed = S.Rushed ?? new Dictionary<int, int>();
			S.CoverDiscs = S.CoverDiscs ?? new List<int>();
			S.FloppedConsoles = S.FloppedConsoles ?? new List<int>();
			S.ModHits = S.ModHits ?? new List<ModHit>();
			S.AutoSupport = S.AutoSupport ?? new List<int>();
			S.PausedCalls = S.PausedCalls ?? new List<int>();
			S.PortDeals = S.PortDeals ?? new List<PortDeal>();
			S.EngineDeals = S.EngineDeals ?? new List<EngineDeal>();
			S.RecentClosures = S.RecentClosures ?? new Dictionary<int, int>();
			S.ProductionLimits = S.ProductionLimits ?? new List<ProductionLimit>();
			S.CartoonDeals = S.CartoonDeals ?? new List<CartoonDeal>();
			S.Feed = S.Feed ?? new List<string>();
			// Headlines saved before 0.23.1 said "Sports Game games".
			S.Feed = S.Feed.Select((string f) => (f ?? "").Replace("Game games", "Games")).ToList();
			S.KnownOpen = S.KnownOpen ?? new List<int>();
			S.KnownClosed = S.KnownClosed ?? new List<int>();
			S.DealtGames = S.DealtGames ?? new List<int>();
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Loading the industry failed: " + ex);
			S = new IndustryState();
		}
		shownFor = null;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(mainScript), "InitNewGame")]
	private static void NewGame()
	{
		S = new IndustryState();
		shownFor = null;
		shownOutcome = null;
	}
}
