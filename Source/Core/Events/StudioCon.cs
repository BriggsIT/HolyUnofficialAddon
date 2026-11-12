using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HolyAddon.Core.CopyProtection;
using HolyAddon.Core.Mmo;
using HolyAddon.Core.Store;
using HolyAddon.Core.Updates;
using HolyAddon.UI;
using Newtonsoft.Json;
using UnityEngine;

namespace HolyAddon.Core.Events;

// One spot on the convention's stage: a game in development, an MMO, or a console of yours.
internal class ConSpot
{
	public const int Empty = -1;

	public const int Game = 0;

	public const int Mmo = 1;

	public const int Console = 2;

	// A game you've released (on the market, or a classic that has left it).
	public const int Released = 3;

	public int Kind = Empty;

	public int Id = -1;

	// For an MMO: none, next content patch, a new expansion pack, classic servers.
	// For a released game: showcase it, or tease a sequel.
	public int Announce;
}

// A released game's sales boost after it was shown on stage.
internal class ConBoost
{
	public int GameId;

	public int Week;

	public float Amount;
}

// This year's convention as planned (and, once started, being prepared).
internal class ConPlan
{
	public int Year;

	public int Venue = 1;

	public List<ConSpot> Spots = new List<ConSpot>();

	public bool Merch = true;

	public bool Panels = true;

	public bool Gifts;

	public bool Booths = true;

	public bool Started;

	public bool Agency;

	public int RoomId = -1;

	public long Paid;

	// The room finished preparing early (or an agency runs it).
	public bool Prepared;

	public ConPlan CopyForNextYear(int year)
	{
		return new ConPlan
		{
			Year = year,
			Venue = Venue,
			Merch = Merch,
			Panels = Panels,
			Gifts = Gifts,
			Booths = Booths,
			Agency = Agency,
			RoomId = RoomId
		};
	}
}

// How a convention went.
internal class ConRecord
{
	public int Year;

	public string Name = "";

	public int Venue;

	public long Demand;

	public long Visitors;

	public long Viewers;

	public bool SoldOut;

	public long Cost;

	public long Tickets;

	public long Merch;

	public long Booths;

	public int Fans;

	public int Points;

	public float Ready = 1f;

	public List<string> Lines = new List<string>();

	// "" for this addon's, "holy" for one held by The Holy Expansion before.
	public string From = "";
}

internal class ConState
{
	public ConPlan Plan;

	public ConPlan Last;

	public int HeldYear = -1;

	public int SkippedYear = -1;

	public int AskedYear = -1;

	// Games shown before: their first time on stage is a premiere.
	public List<int> Premiered = new List<int>();

	public bool Imported;

	public List<ConRecord> History = new List<ConRecord>();

	public List<ConBoost> Boosts = new List<ConBoost>();
}

// What a convention would bring (an estimate) or brought.
internal class ConOutcome
{
	public long Cost;

	public long Demand;

	public long Visitors;

	public long Viewers;

	public bool SoldOut;

	public long Tickets;

	public long Merch;

	public long Booths;

	public float StageHype;

	public int Fans;

	public int Points;

	public long Returning;

	public List<string> BoothStudios = new List<string>();
}

// The yearly convention: one event for all your games, in place of The Holy Expansion's Fan Convention and the
// addon's MMO FanFest.
// - Plan it from a Support room (Holy's button there) or the pop-up in August's first week: the venue, what goes on
//   stage (games in development, MMOs with an announcement, consoles), and extras.
// - A Support room prepares it (Holy's own room task: the staff's skill, motivation and anything supporting the room
//   count), or an event agency does for 50% more. The doors open in August's last week; how ready it is then sets
//   how well it goes.
// - Visitors: as many fans as want tickets (Holy's numbers) up to the venue's seats; everyone else watches the stream.
// - Prices follow Holy's curve (Core/Prices).
[HarmonyPatch]
internal static class StudioCon
{
	private const string SaveKey = "mgt2.editor.convention";

	public const int Month = 8;

	public const int PlanWeek = 1;

	public const int DayWeek = 4;

	public static readonly string[] VenueNames = new string[4] { "Livestream Only", "Fan Meetup", "Convention", "Mega Event" };

	public static readonly string[] VenuePlaces = new string[4] { "online", "in a hotel ballroom", "in a convention centre", "in an arena" };

	private static readonly double[] VenueCost = new double[4] { 6000.0, 60000.0, 300000.0, 900000.0 };

	public static readonly int[] VenueSeats = new int[4] { 0, 3000, 20000, 50000 };

	public static readonly int[] VenueWork = new int[4] { 100, 250, 450, 700 };

	private static readonly float[] VenueDemand = new float[4] { 0f, 0.004f, 0.012f, 0.03f };

	private static readonly float[] StreamShare = new float[4] { 0.015f, 0.006f, 0.008f, 0.01f };

	private static readonly float[] VenueHype = new float[4] { 4f, 6f, 10f, 14f };

	private static readonly float[] VenueInterest = new float[4] { 2f, 4f, 6f, 8f };

	private static readonly float[] VenueReach = new float[4] { 0.35f, 0.6f, 1f, 1.4f };

	private static readonly int[] VenuePoints = new int[4] { 1, 4, 8, 12 };

	public static readonly int[] BoothCount = new int[4] { 0, 3, 8, 16 };

	private static readonly double[] BoothPrice = new double[4] { 0.0, 6000.0, 15000.0, 30000.0 };

	public static readonly string[] AnnounceNames = new string[4] { "No announcement", "Next content patch", "A new expansion pack", "Classic servers" };

	private static readonly float[] AnnounceHype = new float[4] { 0f, 2f, 10f, 6f };

	private static readonly float[] AnnounceInterest = new float[4] { 0f, 3f, 2f, 4f };

	// A released game on stage: a showcase (sales boost while on the market, its series better known), or a sequel
	// teaser (its series much better known). Series fame is the game's IP points (0-1000; a good release adds ~60).
	public static readonly string[] ReleasedNames = new string[2] { "Showcase", "Tease a sequel" };

	public const float ShowcaseSales = 0.02f;

	public const int ShowcaseWeeks = 4;

	public const float SequelFame = 2f;

	private const double TicketPrice = 20.0;

	private const double MerchPerVisitor = 12.0;

	private const double MerchPerViewer = 0.15;

	private const double MerchCost = 8000.0;

	private const double PanelsCost = 4000.0;

	private const double GiftsPerPlayer = 0.02;

	public const float PremiereHype = 6f;

	public const float SoldOutHype = 2f;

	public const float AgencyExtra = 0.5f;

	public const float AgencyReady = 0.9f;

	public const float BoothShare = 0.9f;

	public const int SkipPoints = -3;

	private static ConState S = new ConState();

	private static bool askPending;

	public static ConRecord ResultPending;

	public static ConState State => S;

	public static bool On
	{
		get
		{
			if (Plugin.Convention.Value)
			{
				return G.Singleplayer;
			}
			return false;
		}
	}

	private static int Year => G.MS ? G.MS.year : 0;

	private static int WeekOfYear(int month, int week)
	{
		return (month - 1) * 4 + (week - 1);
	}

	private static int NowIndex => G.MS ? (G.MS.year * 48 + WeekOfYear(G.MS.month, G.MS.week)) : 0;

	public static string Name()
	{
		string studio = (G.MS ? G.MS.GetCompanyName() : "");
		return (string.IsNullOrEmpty(studio) ? "Studio" : studio) + "Con";
	}

	public static string FullName(int year)
	{
		return Name() + " " + year;
	}

	// The year being planned: this year's until its doors open, then next year's.
	public static int PlanYear
	{
		get
		{
			if (!G.MS)
			{
				return 0;
			}
			int y = ((G.MS.month < Month || (G.MS.month == Month && G.MS.week < DayWeek)) ? G.MS.year : (G.MS.year + 1));
			if (S.HeldYear >= y)
			{
				y = S.HeldYear + 1;
			}
			return y;
		}
	}

	public static int WeeksToDoors(int year)
	{
		return year * 48 + WeekOfYear(Month, DayWeek) - NowIndex;
	}

	public static long Fans()
	{
		genres gen = (G.MainObj ? G.MainObj.GetComponent<genres>() : null);
		if (!gen)
		{
			return 0L;
		}
		return gen.GetAmountFans();
	}

	// Who can hold one: studio level 5, 500,000 fans, or an MMO on the market (its players want one).
	public static string LockReason()
	{
		if (!G.MS)
		{
			return "No game loaded.";
		}
		if (G.MS.GetStudioLevel(G.MS.studioPoints) >= 5 || Fans() >= 500000 || MmoOps.MyMmos().Count > 0)
		{
			return null;
		}
		return "Reach studio level 5, 500,000 fans, or have an MMO on the market to hold your own convention.";
	}

	// ---- Planning ----

	public static ConPlan Plan
	{
		get
		{
			int year = PlanYear;
			// One booked for a year whose doors have already closed (the convention was off that August) is gone.
			if (S.Plan != null && S.Plan.Started && S.Plan.Year < year && WeeksToDoors(S.Plan.Year) < 0)
			{
				S.Last = S.Plan;
				S.Plan = null;
			}
			if (S.Plan == null || (S.Plan.Year != year && !S.Plan.Started))
			{
				S.Plan = S.Last?.CopyForNextYear(year) ?? new ConPlan
				{
					Year = year
				};
				FillSpots(S.Plan);
			}
			return S.Plan;
		}
	}

	public static int Slots(int venue)
	{
		return Mathf.Clamp(venue, 0, 3) + 1;
	}

	public static List<gameScript> GamesInDevelopment()
	{
		gameScript[] all = ((G.MS && (bool)G.MS.games_) ? G.MS.games_.arrayGamesScripts : null);
		if (all == null)
		{
			return new List<gameScript>();
		}
		return all.Where((gameScript g) => (bool)g && g.inDevelopment && g.IsMyGame() && !g.typ_contractGame && !g.auftragsspiel).OrderByDescending((gameScript g) => g.hype).ToList();
	}

	// Your released games: on the market first (best selling first), then the classics (newest first).
	public static List<gameScript> ReleasedGames()
	{
		gameScript[] all = ((G.MS && (bool)G.MS.games_) ? G.MS.games_.arrayGamesScripts : null);
		if (all == null)
		{
			return new List<gameScript>();
		}
		return (from g in all
			where (bool)g && !g.inDevelopment && g.IsMyGame() && (g.isOnMarket || g.sellsTotal > 0) && !g.typ_bundle && !g.typ_bundleAddon && !g.typ_contractGame && !g.auftragsspiel && !(g.gameTyp == 1 && g.isOnMarket && !g.typ_mmoaddon)
			orderby g.isOnMarket descending, g.isOnMarket ? Store.SalesEvents.Weekly(g) : 0 descending, g.date_year * 12 + g.date_month descending
			select g).ToList();
	}

	public static bool HasSeries(gameScript g)
	{
		if ((bool)g)
		{
			return g.mainIP != -1;
		}
		return false;
	}

	public static List<platformScript> Consoles()
	{
		platformScript[] all = (G.MS ? G.MS.arrayPlatformsScripts : null);
		if (all == null)
		{
			return new List<platformScript>();
		}
		return all.Where((platformScript p) => (bool)p && p.ownerID == G.MS.myID && !p.vomMarktGenommen).ToList();
	}

	// Everything that can go on stage.
	public static List<ConSpot> Candidates()
	{
		List<ConSpot> list = new List<ConSpot>();
		list.AddRange(GamesInDevelopment().Select((gameScript g) => new ConSpot
		{
			Kind = ConSpot.Game,
			Id = g.myID
		}));
		list.AddRange(from g in MmoOps.MyMmos()
			orderby g.abonnements descending
			select new ConSpot
			{
				Kind = ConSpot.Mmo,
				Id = g.myID
			});
		list.AddRange(Consoles().Select((platformScript p) => new ConSpot
		{
			Kind = ConSpot.Console,
			Id = p.myID
		}));
		list.AddRange(ReleasedGames().Select((gameScript g) => new ConSpot
		{
			Kind = ConSpot.Released,
			Id = g.myID
		}));
		return list;
	}

	public static bool Same(ConSpot a, ConSpot b)
	{
		if (a != null && b != null && a.Kind == b.Kind)
		{
			return a.Id == b.Id;
		}
		return false;
	}

	public static gameScript GameOf(ConSpot s)
	{
		if (s == null || (s.Kind != ConSpot.Game && s.Kind != ConSpot.Mmo && s.Kind != ConSpot.Released))
		{
			return null;
		}
		return GameUpdates.FindGame(s.Id);
	}

	public static platformScript ConsoleOf(ConSpot s)
	{
		if (s == null || s.Kind != ConSpot.Console)
		{
			return null;
		}
		return GameUpdates.Platform(s.Id);
	}

	public static bool Valid(ConSpot s)
	{
		if (s == null || s.Kind == ConSpot.Empty)
		{
			return false;
		}
		return Candidates().Any((ConSpot c) => Same(c, s));
	}

	public static string SpotName(ConSpot s)
	{
		if (s == null || s.Kind == ConSpot.Empty)
		{
			return "Empty";
		}
		if (s.Kind == ConSpot.Console)
		{
			platformScript p = ConsoleOf(s);
			return "Console: " + (p ? p.GetName() : "?");
		}
		gameScript g = GameOf(s);
		string name = (g ? g.GetNameSimple() : "?");
		if (s.Kind == ConSpot.Released)
		{
			if (!g || g.isOnMarket)
			{
				return "Released: " + name;
			}
			return "Classic: " + name + " (" + g.date_year + ")";
		}
		if (s.Kind != ConSpot.Mmo)
		{
			return "In development: " + name + (S.Premiered.Contains(s.Id) ? "" : " (premiere)");
		}
		return "MMO: " + name;
	}

	// The choices for a spot's second dropdown (none for games in development and consoles).
	public static List<string> AnnounceOptions(ConSpot s, float stageHype)
	{
		gameScript g = GameOf(s);
		if (s.Kind == ConSpot.Mmo)
		{
			return AnnounceNames.Take(MmoFanFest.ClassicAnnounceable(g) ? 4 : 3).ToList();
		}
		if (s.Kind != ConSpot.Released || !g)
		{
			return null;
		}
		List<string> list = new List<string> { g.isOnMarket ? ("Showcase: sales +" + Mathf.RoundToInt(stageHype * ShowcaseSales * 100f) + "% for " + ShowcaseWeeks + " weeks") : ("Showcase: series fame +" + Mathf.RoundToInt(stageHype)) };
		if (HasSeries(g))
		{
			list.Add("Tease a sequel: series fame +" + Mathf.RoundToInt(stageHype * SequelFame));
		}
		return list;
	}

	// The extra sales a showcase gives a released game, fading over a few weeks (into Holy's total, like the rest).
	public static float Bonus(gameScript g)
	{
		if (!On || !g || S.Boosts.Count == 0)
		{
			return 0f;
		}
		float total = 0f;
		int now = CopyProtectDev.AbsWeek;
		foreach (ConBoost b in S.Boosts)
		{
			int weeks = now - b.Week;
			if (b.GameId == g.myID && weeks >= 0 && weeks < ShowcaseWeeks)
			{
				total += b.Amount * (1f - (float)weeks / (float)ShowcaseWeeks);
			}
		}
		return total;
	}

	// Keeps the stage to the venue's spots (anything no longer showable leaves its spot empty); with fill, empty spots
	// take what's left, most hyped first (a new plan, a bigger venue), otherwise they stay as you left them.
	public static void FillSpots(ConPlan p, bool fill = true)
	{
		int n = Slots(p.Venue);
		List<ConSpot> cands = Candidates();
		for (int i = 0; i < p.Spots.Count; i++)
		{
			ConSpot s = p.Spots[i];
			if (s == null || (s.Kind != ConSpot.Empty && !cands.Any((ConSpot c) => Same(c, s))))
			{
				p.Spots[i] = new ConSpot();
			}
		}
		while (p.Spots.Count > n)
		{
			p.Spots.RemoveAt(p.Spots.Count - 1);
		}
		while (p.Spots.Count < n)
		{
			p.Spots.Add(new ConSpot());
		}
		if (!fill)
		{
			return;
		}
		List<ConSpot> left = cands.Where((ConSpot c) => !p.Spots.Any((ConSpot s) => Same(s, c))).ToList();
		for (int j = 0; j < p.Spots.Count && left.Count > 0; j++)
		{
			if (p.Spots[j].Kind == ConSpot.Empty)
			{
				p.Spots[j] = left[0];
				left.RemoveAt(0);
			}
		}
	}

	public static List<gameScript> MmosOnStage(ConPlan p)
	{
		return (from s in p.Spots
			where s.Kind == ConSpot.Mmo
			select GameOf(s) into g
			where (bool)g
			select g).ToList();
	}

	// ---- Money ----

	public static long VenuePrice(int venue)
	{
		return Prices.Scaled(VenueCost[Mathf.Clamp(venue, 0, 3)]);
	}

	public static long MerchPrice => Prices.Scaled(MerchCost);

	public static long PanelsPrice => Prices.Scaled(PanelsCost);

	public static long GiftsPrice(ConPlan p)
	{
		return Prices.Scaled(GiftsPerPlayer * (double)MmosOnStage(p).Sum((gameScript g) => g.abonnements));
	}

	public static long Cost(ConPlan p, bool agency)
	{
		long c = VenuePrice(p.Venue);
		if (p.Merch)
		{
			c += MerchPrice;
		}
		if (p.Panels)
		{
			c += PanelsPrice;
		}
		if (p.Gifts && MmosOnStage(p).Count > 0)
		{
			c += GiftsPrice(p);
		}
		if (agency)
		{
			c += (long)((float)c * AgencyExtra);
		}
		return c;
	}

	// ---- What it brings ----

	// The outcome at a given readiness (1 = fully prepared). With apply, it happens: money, hype, fans and the rest.
	public static ConOutcome Outcome(ConPlan p, float ready, bool apply, ConRecord record = null)
	{
		ConOutcome o = new ConOutcome();
		int v = Mathf.Clamp(p.Venue, 0, 3);
		float q = 0.4f + 0.6f * Mathf.Clamp01(ready);
		long fans = Fans();
		List<gameScript> mmos = MmosOnStage(p);
		double demand = ((v == 0) ? 0.0 : ((double)fans * (double)VenueDemand[v] + (double)(500 * v) + (double)mmos.Sum((gameScript g) => g.abonnements) * 0.004));
		if (apply)
		{
			demand *= UnityEngine.Random.Range(0.85f, 1.15f);
		}
		o.Demand = (long)demand;
		o.Visitors = Math.Min(VenueSeats[v], o.Demand);
		o.SoldOut = v > 0 && o.Demand > VenueSeats[v];
		o.Viewers = (long)((double)(o.Demand - o.Visitors + (long)((float)fans * StreamShare[v])) * (double)(0.7f + 0.3f * Mathf.Clamp01(ready)));
		o.Tickets = o.Visitors * Prices.Scaled(TicketPrice);
		if (p.Merch)
		{
			o.Merch = (long)(((double)o.Visitors * MerchPerVisitor + (double)o.Viewers * MerchPerViewer) * Prices.Factor * (double)q);
		}
		if (p.Booths && BoothCount[v] > 0)
		{
			List<publisherScript> studios = BoothStudios(BoothCount[v], apply);
			o.Booths = studios.Count * Prices.Scaled(BoothPrice[v]);
			o.BoothStudios = studios.Select((publisherScript s) => s.GetName()).ToList();
			if (apply)
			{
				foreach (publisherScript s2 in studios)
				{
					s2.relation = Mathf.Min(100f, s2.relation + 3f);
				}
			}
		}
		o.StageHype = VenueHype[v] * q * ((p.Booths && BoothCount[v] > 0) ? BoothShare : 1f) + (o.SoldOut ? SoldOutHype : 0f);
		o.Fans = (int)Math.Min(2000000.0, ((double)o.Visitors * 0.5 + (double)o.Viewers * 0.05) * (double)q);
		o.Points = Mathf.RoundToInt((float)VenuePoints[v] * q) + (p.Panels ? 1 : 0);
		if (ready < 0.4f)
		{
			o.Points = -2;
		}
		o.Cost = p.Paid;
		foreach (ConSpot s3 in p.Spots)
		{
			Stage(p, s3, o, q, apply, record);
		}
		if (!apply)
		{
			return o;
		}
		if (o.Tickets > 0)
		{
			G.MS.Earn(o.Tickets, 1);
		}
		if (o.Booths > 0)
		{
			G.MS.Earn(o.Booths, 1);
		}
		if (o.Merch > 0)
		{
			G.MS.Earn(o.Merch, 11);
		}
		if (o.Fans > 0)
		{
			G.MS.AddFans(o.Fans, -1);
		}
		if (o.Points != 0)
		{
			G.MS.AddStudioPoints(o.Points);
		}
		EsportsBoost(0.05f * (float)v);
		return o;
	}

	private static void Stage(ConPlan p, ConSpot s, ConOutcome o, float q, bool apply, ConRecord record)
	{
		int v = Mathf.Clamp(p.Venue, 0, 3);
		if (s.Kind == ConSpot.Console)
		{
			platformScript c = ConsoleOf(s);
			if ((bool)c && apply)
			{
				c.AddHype(o.StageHype);
				record?.Lines.Add("<b>" + c.GetName() + "</b>: hype +" + Mathf.RoundToInt(o.StageHype));
			}
			return;
		}
		gameScript g = GameOf(s);
		if (!g || !apply)
		{
			return;
		}
		if (s.Kind == ConSpot.Game)
		{
			bool premiere = !S.Premiered.Contains(g.myID);
			float hype = o.StageHype + (premiere ? PremiereHype : 0f) + (p.Panels ? 2f : 0f);
			g.AddHype(hype);
			if (premiere)
			{
				S.Premiered.Add(g.myID);
			}
			record?.Lines.Add("<b>" + g.GetNameSimple() + "</b>" + (premiere ? " (world premiere)" : "") + ": hype +" + Mathf.RoundToInt(hype));
			return;
		}
		if (s.Kind == ConSpot.Released)
		{
			bool sequel = s.Announce == 1 && HasSeries(g);
			float fame = o.StageHype * (sequel ? SequelFame : 1f);
			List<string> parts = new List<string>();
			if (HasSeries(g))
			{
				g.AddIpPoints(fame);
				parts.Add("series fame +" + Mathf.RoundToInt(fame));
			}
			if (!sequel && g.isOnMarket)
			{
				float boost = o.StageHype * ShowcaseSales;
				S.Boosts.Add(new ConBoost
				{
					GameId = g.myID,
					Week = CopyProtectDev.AbsWeek,
					Amount = boost
				});
				parts.Add("sales +" + Mathf.RoundToInt(boost * 100f) + "% for " + ShowcaseWeeks + " weeks");
			}
			if (p.Panels && g.isOnMarket)
			{
				float reviews2 = 0.01f * VenueReach[v] * q;
				HolyCompat.ShiftUserReviews(g, reviews2);
				parts.Add("user reviews +" + Mathf.Max(1, Mathf.RoundToInt(reviews2 * 100f)) + "%");
			}
			if (parts.Count == 0)
			{
				parts.Add("fans enjoyed the look back");
			}
			record?.Lines.Add("<b>" + g.GetNameSimple() + "</b>" + (sequel ? " (sequel teased)" : (g.isOnMarket ? " (showcase)" : " (classic)")) + ": " + string.Join(", ", parts));
			return;
		}
		int a = Mathf.Clamp(s.Announce, 0, 3);
		if (a == 3 && !MmoFanFest.ClassicAnnounceable(g))
		{
			a = 0;
		}
		float mmoHype = o.StageHype + AnnounceHype[a];
		float interest = (VenueInterest[v] + AnnounceInterest[a]) * q;
		g.AddHype(mmoHype);
		g.AddMMOInteresse(interest);
		string line = "<b>" + g.GetNameSimple() + "</b>" + ((a > 0) ? (" (" + AnnounceNames[a].ToLower() + ")") : "") + ": hype +" + Mathf.RoundToInt(mmoHype) + ", interest +" + Mathf.RoundToInt(interest);
		if (p.Gifts)
		{
			long back = (long)((float)MmoLive.Lapsed(g) * 0.02f * VenueReach[v] * q);
			if (back > 0)
			{
				back = MmoOps.PlaceOnServers(back);
				g.abonnements += back;
				g.abonnementsWoche += back;
				if (g.bestAbonnements < g.abonnements)
				{
					g.bestAbonnements = g.abonnements;
				}
				o.Returning += back;
				line = line + ", " + MmoUiText.Num(back) + " players back";
			}
		}
		if (p.Panels)
		{
			float reviews = 0.01f * VenueReach[v] * q;
			HolyCompat.ShiftUserReviews(g, reviews);
			line = line + ", user reviews +" + Mathf.Max(1, Mathf.RoundToInt(reviews * 100f)) + "%";
		}
		record?.Lines.Add(line);
	}

	// Rival studios with something to show take the booths (the same ones for an estimate).
	private static List<publisherScript> BoothStudios(int count, bool random)
	{
		publisherScript[] all = (G.MS ? G.MS.arrayPublisherScripts : null);
		if (all == null)
		{
			return new List<publisherScript>();
		}
		List<publisherScript> list = all.Where((publisherScript s) => (bool)s && !s.isPlayer && s.isUnlocked && !s.IsTochterfirma() && !s.Geschlossen() && s.developer).ToList();
		IEnumerable<publisherScript> ordered = (random ? list.OrderByDescending((publisherScript s) => s.relation + UnityEngine.Random.Range(0f, 60f)) : list.OrderByDescending((publisherScript s) => s.relation));
		return ordered.Take(count).ToList();
	}

	// Holy's esports leagues get more popular, as Holy's convention did.
	private static void EsportsBoost(float add)
	{
		if (add <= 0f)
		{
			return;
		}
		try
		{
			Type state = AccessTools.TypeByName("HolyExpansion.Core.HolyState");
			object current = state?.GetField("Current", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null);
			object esports = ((current != null) ? state.GetField("Esports")?.GetValue(current) : null);
			if (!(Traverse.Create(esports).Field("Leagues").GetValue() is IList leagues))
			{
				return;
			}
			foreach (object league in leagues)
			{
				Traverse t = Traverse.Create(league).Field("Popularity");
				t.SetValue(Mathf.Min(3f, t.GetValue<float>() + add));
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Convention esports: " + ex.Message);
		}
	}

	// ---- Preparing ----

	// How ready it is now (0-1), and the room's task while one prepares it.
	public static float Readiness(ConPlan p, out object task)
	{
		task = null;
		if (p == null || !p.Started)
		{
			return 0f;
		}
		if (p.Agency)
		{
			return AgencyReady;
		}
		if (p.Prepared)
		{
			return 1f;
		}
		task = HolyRooms.Find(p.Year);
		if (task == null)
		{
			return 0f;
		}
		return HolyRooms.Progress(task);
	}

	// About how ready it will be when the doors open, at the room's pace.
	public static float ReadyOnTheDay(ConPlan p)
	{
		float now = Readiness(p, out object task);
		if (task == null)
		{
			return now;
		}
		roomScript room = HolyRooms.RoomOf(task);
		float needed = Mathf.Max(1f, HolyCompat.Get<float>(task, "Needed"));
		float rate = HolyRooms.Rate(room);
		return Mathf.Clamp01(now + rate * (float)Mathf.Max(0, WeeksToDoors(p.Year)) / needed);
	}

	public static string StartProblem(ConPlan p, roomScript room, bool agency)
	{
		string locked = LockReason();
		if (locked != null)
		{
			return locked;
		}
		if (p.Started)
		{
			return FullName(p.Year) + " is already being prepared.";
		}
		if (!agency)
		{
			if (!HolyRooms.Available)
			{
				return "Support rooms prepare it with The Holy Expansion installed. Book an event agency instead.";
			}
			if (!room)
			{
				return "Pick a Support room to prepare it, or book an event agency.";
			}
			string problem = HolyRooms.Problem(room);
			if (problem != null)
			{
				return problem;
			}
		}
		long cost = Cost(p, agency);
		if (G.MS.NotEnoughMoney(cost))
		{
			return "You need " + G.Money(cost) + " for this.";
		}
		return null;
	}

	public static string Start(ConPlan p, roomScript room, bool agency)
	{
		string problem = StartProblem(p, room, agency);
		if (problem != null)
		{
			return problem;
		}
		long cost = Cost(p, agency);
		if (!agency && HolyRooms.Start(room, VenueWork[Mathf.Clamp(p.Venue, 0, 3)], FullName(p.Year), cost, p.Venue, p.Year) == null)
		{
			return "The room couldn't start on it.";
		}
		G.MS.Pay(cost, 12);
		p.Started = true;
		p.Agency = agency;
		p.Prepared = agency;
		p.Paid = cost;
		p.RoomId = (agency ? (-1) : room.myID);
		GameUpdates.Touch();
		return null;
	}

	// Calls it off: a room's share not prepared yet comes back (Holy's rule for room work), an agency gives half.
	public static string CallOff(ConPlan p)
	{
		if (!p.Started)
		{
			return null;
		}
		long back = 0L;
		Readiness(p, out object task);
		if (task != null)
		{
			back = HolyRooms.Cancel(task);
			p = S.Plan;
		}
		else if (p.Agency)
		{
			back = p.Paid / 2;
			if (back > 0)
			{
				G.MS.Earn(back, 1);
			}
		}
		Reset(p);
		GameUpdates.Touch();
		return FullName(p.Year) + " is called off." + ((back > 0) ? (" " + G.Money(back) + " came back.") : "");
	}

	private static void Reset(ConPlan p)
	{
		if (p != null)
		{
			p.Started = false;
			p.Prepared = false;
			p.Paid = 0L;
		}
	}

	// Holy's room task finished: the convention waits for its date.
	public static void PrepDone(object task)
	{
		int year = Traverse.Create(task).Field("Number").GetValue<int>();
		ConPlan p = S.Plan;
		if (p == null || !p.Started || p.Year != year)
		{
			return;
		}
		p.Prepared = true;
		GameUpdates.Touch();
		StoreOps.News("<b>" + FullName(year) + "</b> is ready: the doors open in August, week " + DayWeek + ((year > Year) ? (" " + year) : "") + ".");
	}

	// Holy's room task was cancelled (from its room, or the room went): the plan is no longer started.
	public static void PrepCancelled(object task)
	{
		int year = Traverse.Create(task).Field("Number").GetValue<int>();
		ConPlan p = S.Plan;
		if (p != null && p.Started && p.Year == year && !p.Agency)
		{
			Reset(p);
			GameUpdates.Touch();
		}
	}

	// ---- The year ----

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
			Ready();
			if (__instance.month != Month)
			{
				return;
			}
			int year = __instance.year;
			if (__instance.week == PlanWeek && LockReason() == null && PlanYear == year && !Plan.Started && S.AskedYear != year && S.SkippedYear != year)
			{
				S.AskedYear = year;
				askPending = true;
			}
			if (__instance.week == DayWeek && S.HeldYear != year)
			{
				ConPlan p = S.Plan;
				if (p != null && p.Started && p.Year == year)
				{
					Hold(p);
				}
				else if (LockReason() == null && S.SkippedYear != year)
				{
					Skip(year);
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Convention: " + ex);
		}
	}

	private static void Skip(int year)
	{
		S.SkippedYear = year;
		if (S.HeldYear == year - 1)
		{
			G.MS.AddStudioPoints(SkipPoints);
			StoreOps.News("Fans waited for <b>" + FullName(year) + "</b> in vain: no convention this year (" + SkipPoints + " studio points).");
		}
	}

	private static void Hold(ConPlan p)
	{
		float ready = Readiness(p, out object task);
		if (task != null)
		{
			HolyRooms.Remove(task);
		}
		FillSpots(p, fill: false);
		ConRecord r = new ConRecord
		{
			Year = p.Year,
			Name = FullName(p.Year),
			Venue = p.Venue,
			Ready = ready
		};
		ConOutcome o = Outcome(p, ready, apply: true, r);
		r.Demand = o.Demand;
		r.Visitors = o.Visitors;
		r.Viewers = o.Viewers;
		r.SoldOut = o.SoldOut;
		r.Cost = p.Paid;
		r.Tickets = o.Tickets;
		r.Merch = o.Merch;
		r.Booths = o.Booths;
		r.Fans = o.Fans;
		r.Points = o.Points;
		if (o.BoothStudios.Count > 0)
		{
			r.Lines.Add("Booths: " + string.Join(", ", o.BoothStudios.Take(6)) + ((o.BoothStudios.Count > 6) ? (" and " + (o.BoothStudios.Count - 6) + " more") : ""));
		}
		S.History.Add(r);
		if (S.History.Count > 30)
		{
			S.History.RemoveAt(0);
		}
		S.Boosts.RemoveAll((ConBoost b) => CopyProtectDev.AbsWeek - b.Week >= ShowcaseWeeks && b.Week != CopyProtectDev.AbsWeek);
		S.HeldYear = p.Year;
		S.Last = p;
		S.Plan = null;
		ResultPending = r;
		if ((bool)G.Gui)
		{
			G.Gui.AddHistory(r.Name);
		}
		GameUpdates.Touch();
	}

	// Opens the planning pop-up in August's first week, and the results when the doors have closed.
	public static void Tick()
	{
		if (!On || !G.Gui || G.Gui.menuOpen || EditorWindow.Convention.IsOpen)
		{
			return;
		}
		if (ResultPending != null || askPending)
		{
			askPending = false;
			UI.Pages.ConventionPage.FromRoom = null;
			EditorWindow.Convention.Open();
			Awards.AwardsShow.Sound(50);
		}
	}

	// ---- Taking over Holy's convention and the FanFest (once per save) ----

	public static void Ready()
	{
		if (S.Imported || !On || !G.MS)
		{
			return;
		}
		S.Imported = true;
		try
		{
			Type state = AccessTools.TypeByName("HolyExpansion.Core.HolyState");
			object current = state?.GetField("Current", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null);
			object con = ((current != null) ? state.GetField("Convention")?.GetValue(current) : null);
			if (con != null)
			{
				Traverse t = Traverse.Create(con);
				int last = t.Field("LastYear").GetValue<int>();
				if (t.Field("History").GetValue() is IList history)
				{
					foreach (object h in history)
					{
						Traverse hr = Traverse.Create(h);
						int size = hr.Field("Size").GetValue<int>();
						S.History.Add(new ConRecord
						{
							Year = hr.Field("Year").GetValue<int>(),
							Name = FullName(hr.Field("Year").GetValue<int>()),
							Venue = Mathf.Clamp(size + 1, 1, 3),
							Visitors = hr.Field("Visitors").GetValue<long>(),
							Tickets = hr.Field("Revenue").GetValue<long>(),
							From = "holy"
						});
					}
					S.History = S.History.OrderBy((ConRecord r) => r.Year).ToList();
				}
				if (t.Field("AutoHost").GetValue<bool>())
				{
					S.Last = new ConPlan
					{
						Venue = Mathf.Clamp(t.Field("AutoSize").GetValue<int>() + 1, 1, 3)
					};
				}
				object running = HolyRooms.Find(0);
				if (running != null)
				{
					// A Holy convention being prepared right now becomes this year's.
					Traverse rt = Traverse.Create(running);
					int year = PlanYear;
					rt.Field("Number").SetValue(year);
					ConPlan p = Plan;
					p.Venue = Mathf.Clamp(rt.Field("Option").GetValue<int>() + 1, 1, 3);
					p.Started = true;
					p.Paid = rt.Field("Cost").GetValue<long>();
					roomScript room = HolyRooms.RoomOf(running);
					p.RoomId = (room ? room.myID : (-1));
					FillSpots(p);
				}
				else if (last == Year)
				{
					S.HeldYear = Math.Max(S.HeldYear, last);
				}
			}
			MmoState mmo = MmoOps.State;
			if (mmo != null && mmo.FanFestYear == Year)
			{
				S.HeldYear = Math.Max(S.HeldYear, Year);
			}
			GameUpdates.Touch();
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Convention: taking over Holy's: " + ex.Message);
		}
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
			Plugin.Log.LogError("Saving the convention failed: " + ex);
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(savegameScript), "Load")]
	private static void BeforeLoad()
	{
		S = new ConState();
		askPending = false;
		ResultPending = null;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(savegameScript), "LoadHistory")]
	private static void Load(ES3File ___es3file)
	{
		try
		{
			S = ((___es3file != null && ___es3file.KeyExists(SaveKey)) ? JsonConvert.DeserializeObject<ConState>(___es3file.Load<string>(SaveKey)) : null) ?? new ConState();
			S.Premiered = S.Premiered ?? new List<int>();
			S.History = S.History ?? new List<ConRecord>();
			S.Boosts = S.Boosts ?? new List<ConBoost>();
			if (S.Plan != null)
			{
				S.Plan.Spots = S.Plan.Spots ?? new List<ConSpot>();
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Loading the convention failed: " + ex);
			S = new ConState();
		}
		askPending = false;
		ResultPending = null;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(mainScript), "InitNewGame")]
	private static void NewGame()
	{
		S = new ConState();
		askPending = false;
		ResultPending = null;
	}
}

// The Holy Expansion's room tasks (HolyExpansion.Systems.RoomTasks), reached by reflection: a Support room prepares
// the convention with Holy's own "convention" task (its progress panel, its staff working with the game's work rules,
// rooms supporting it helping). Its finish, cancel and details now lead to the addon's convention.
internal static class HolyRooms
{
	private static bool looked;

	private static Type tasks;

	private static Type dataType;

	private static MethodInfo start;

	private static MethodInfo remove;

	private static MethodInfo cancel;

	private static MethodInfo rate;

	private static MethodInfo duration;

	private static MethodInfo problem;

	private static MethodInfo roomOf;

	private static MethodInfo roomName;

	private static MethodInfo staff;

	private static FieldInfo kinds;

	private static Delegate holyDone;

	private static Delegate holyCancelled;

	private static Delegate holyDetails;

	private static Func<string> holyDetailsLabel;

	private static void Look()
	{
		if (looked)
		{
			return;
		}
		looked = true;
		tasks = AccessTools.TypeByName("HolyExpansion.Systems.RoomTasks");
		dataType = AccessTools.TypeByName("HolyExpansion.Systems.RoomTaskData");
		if (tasks == null || dataType == null)
		{
			return;
		}
		start = AccessTools.Method(tasks, "Start");
		remove = AccessTools.Method(tasks, "Remove");
		cancel = AccessTools.Method(tasks, "Cancel");
		rate = AccessTools.Method(tasks, "Rate");
		duration = AccessTools.Method(tasks, "Duration");
		problem = AccessTools.Method(tasks, "Problem");
		roomOf = AccessTools.Method(tasks, "RoomOf");
		roomName = AccessTools.Method(tasks, "RoomName");
		staff = AccessTools.Method(tasks, "Staff");
		kinds = AccessTools.Field(tasks, "kinds");
	}

	// Only once its task finishes through the addon: otherwise Holy would hold its own convention at the end of it.
	private static bool takenOver;

	public static bool Available
	{
		get
		{
			Look();
			if (takenOver && start != null && remove != null)
			{
				return roomOf != null;
			}
			return false;
		}
	}

	private static IList All()
	{
		return HolyCompat.HolyState("RoomTasks") as IList;
	}

	// The convention task for a year (0 = one Holy started before the addon took over).
	public static object Find(int year)
	{
		if (!Available)
		{
			return null;
		}
		IList all = All();
		if (all == null)
		{
			return null;
		}
		foreach (object d in all)
		{
			if (HolyCompat.Get<string>(d, "Kind") == "convention" && HolyCompat.Get<int>(d, "Number") == year)
			{
				return d;
			}
		}
		return null;
	}

	public static float Progress(object d)
	{
		return Mathf.Clamp01(HolyCompat.Get<float>(d, "Progress") / Mathf.Max(1f, HolyCompat.Get<float>(d, "Needed")));
	}

	public static roomScript RoomOf(object d)
	{
		try
		{
			return (d != null && roomOf != null) ? (roomOf.Invoke(null, new object[1] { d }) as roomScript) : null;
		}
		catch
		{
			return null;
		}
	}

	public static object Start(roomScript room, float work, string name, long cost, int venue, int year)
	{
		if (!Available)
		{
			return null;
		}
		try
		{
			return start.Invoke(null, new object[9] { "convention", room, work, name, cost, -1, venue, year, null });
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Convention: starting the room's work: " + ex);
			return null;
		}
	}

	public static void Remove(object d)
	{
		try
		{
			remove?.Invoke(null, new object[1] { d });
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Convention: ending the room's work: " + ex.Message);
		}
	}

	public static long Cancel(object d)
	{
		try
		{
			return (cancel != null) ? ((long)cancel.Invoke(null, new object[1] { d })) : 0;
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Convention: cancelling the room's work: " + ex.Message);
			return 0L;
		}
	}

	public static float Rate(roomScript r)
	{
		try
		{
			return ((bool)r && rate != null) ? ((float)rate.Invoke(null, new object[1] { r })) : 0f;
		}
		catch
		{
			return 0f;
		}
	}

	public static string Duration(float work, roomScript r)
	{
		try
		{
			return ((bool)r && duration != null) ? ((string)duration.Invoke(null, new object[2] { work, r })) : "-";
		}
		catch
		{
			return "-";
		}
	}

	public static string Problem(roomScript r)
	{
		try
		{
			return (problem != null) ? ((string)problem.Invoke(null, new object[2] { r, 7 })) : "No room.";
		}
		catch (Exception ex)
		{
			return ex.Message;
		}
	}

	public static string RoomName(roomScript r)
	{
		try
		{
			return ((bool)r && roomName != null) ? ((string)roomName.Invoke(null, new object[1] { r })) : "Support room";
		}
		catch
		{
			return "Support room";
		}
	}

	public static int StaffCount(roomScript r)
	{
		try
		{
			return ((bool)r && staff != null && staff.Invoke(null, new object[1] { r }) is IList list) ? list.Count : 0;
		}
		catch
		{
			return 0;
		}
	}

	public static List<roomScript> SupportRooms()
	{
		roomScript[] rooms = (G.MS ? G.MS.arrayRoomScripts : null);
		if (rooms == null)
		{
			return new List<roomScript>();
		}
		return rooms.Where((roomScript r) => (bool)r && r.typ == 7).OrderByDescending(Rate).ToList();
	}

	// Holy's "convention" task kind now finishes, cancels and shows details through the addon (while it's on).
	public static void TakeOver()
	{
		Look();
		if (kinds == null || !(kinds.GetValue(null) is IDictionary dict) || !dict.Contains("convention"))
		{
			return;
		}
		try
		{
			Traverse t = Traverse.Create(dict["convention"]);
			holyDone = t.Field("Done").GetValue<Delegate>();
			holyCancelled = t.Field("Cancelled").GetValue<Delegate>();
			holyDetails = t.Field("Details").GetValue<Delegate>();
			holyDetailsLabel = t.Field("DetailsLabel").GetValue<Func<string>>();
			Type action = typeof(Action<>).MakeGenericType(dataType);
			t.Field("Done").SetValue(Delegate.CreateDelegate(action, AccessTools.Method(typeof(HolyRooms), "Done")));
			t.Field("Cancelled").SetValue(Delegate.CreateDelegate(action, AccessTools.Method(typeof(HolyRooms), "Cancelled")));
			t.Field("Details").SetValue(Delegate.CreateDelegate(action, AccessTools.Method(typeof(HolyRooms), "Details")));
			t.Field("DetailsLabel").SetValue(new Func<string>(() => StudioCon.On ? StudioCon.Name() : (holyDetailsLabel?.Invoke() ?? "Fan Convention")));
			takenOver = true;
			Plugin.Log.LogInfo("Holy's convention room task now prepares the yearly convention");
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Could not take over Holy's convention room task: " + ex.Message);
		}
	}

	private static void Done(object d)
	{
		if (StudioCon.On)
		{
			StudioCon.PrepDone(d);
		}
		else
		{
			holyDone?.DynamicInvoke(d);
		}
	}

	private static void Cancelled(object d)
	{
		if (StudioCon.On)
		{
			StudioCon.PrepCancelled(d);
		}
		else
		{
			holyCancelled?.DynamicInvoke(d);
		}
	}

	private static void Details(object d)
	{
		if (StudioCon.On)
		{
			UI.Pages.ConventionPage.FromRoom = RoomOf(d);
			EditorWindow.Convention.Open();
		}
		else
		{
			holyDetails?.DynamicInvoke(d);
		}
	}
}
