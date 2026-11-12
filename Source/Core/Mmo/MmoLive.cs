using System;
using System.Linq;
using HolyAddon.Core.Updates;
using UnityEngine;

namespace HolyAddon.Core.Mmo;

// Running an MMO from week to week: bot ban waves, stability patches, welcome-back campaigns, a free trial, and
// classic servers (the launch-day version, as realms in the same subscription or relaunched as its own game).
internal static class MmoLive
{
	public const float StableChurn = 0.1f;

	public const int StableWeeks = 6;

	public const int StabilityCooldown = 6;

	public const float TrialJoinBonus = 0.15f;

	public const int WelcomeCooldown = 26;

	// Classic servers need an MMO that's been out this long.
	public const int ClassicAgeWeeks = 260;

	public const int PhaseCooldown = 16;

	public const int Phases = 6;

	private static int Now => MmoOps.Now;

	private static float Points(gameScript g)
	{
		return GameUpdates.DevPoints(g);
	}

	private static string InWeeks(int weeks)
	{
		return (weeks == 1) ? "1 week" : (weeks + " weeks");
	}

	private static string Wait(string what, int weeks)
	{
		return what + " can start in " + InWeeks(weeks) + ".";
	}

	public static bool TrialActive(MmoGame m)
	{
		if (m != null && m.TrialBuilt)
		{
			return m.TrialOn;
		}
		return false;
	}

	public static float TrialJoin(MmoGame m)
	{
		if (!TrialActive(m))
		{
			return 1f;
		}
		return 1f + TrialJoinBonus;
	}

	// Players who had it once and left: its best subscriber count (or every copy sold, if fewer) minus today's.
	public static long Lapsed(gameScript g)
	{
		return Math.Max(0L, Math.Min(g.bestAbonnements, g.sellsTotal) - g.abonnements);
	}

	// What the game has had since launch that a classic version would leave out.
	public static int Changes(gameScript g, MmoGame m)
	{
		return m.Expansions + m.Overhauls + m.Migrations + g.amountMMOAddons;
	}

	public static string DefaultClassicName(gameScript g)
	{
		return g.GetNameSimple() + " Classic";
	}

	// ---- Plans ----

	public static UpdatePlan Plan(UpdateKind kind, gameScript g, int option)
	{
		return kind switch
		{
			UpdateKind.MmoBotBust => PlanBust(g),
			UpdateKind.MmoStability => PlanStability(g),
			UpdateKind.MmoWelcomeBack => PlanWelcome(g),
			UpdateKind.MmoFreeTrial => PlanTrial(g),
			UpdateKind.MmoClassic => PlanClassic(g, option),
			UpdateKind.MmoClassicPhase => PlanPhase(g),
			_ => null,
		};
	}

	public static UpdatePlan PlanBust(gameScript g)
	{
		MmoGame m = MmoOps.Get(g);
		UpdatePlan plan = new UpdatePlan();
		if (!MmoBots.On)
		{
			plan.Problem = "Bots are switched off in the addon's settings.";
			return plan;
		}
		int wait = MmoOps.WeeksLeft(m.BustReady);
		if (wait > 0)
		{
			plan.Problem = "The last ban wave is too recent: " + Wait("the next one", wait);
			return plan;
		}
		if (MmoBots.Total(m) < 50 || MmoBots.Share(g, m) < 0.005f)
		{
			plan.Problem = "There are hardly any bots in it right now.";
			return plan;
		}
		plan.Work = 12f + Points(g) * 0.05f;
		plan.Cost = 5000L + (long)((float)MmoOps.BaseCost(g) * 0.015f);
		return plan;
	}

	public static UpdatePlan PlanStability(gameScript g)
	{
		MmoGame m = MmoOps.Get(g);
		UpdatePlan plan = new UpdatePlan();
		int wait = MmoOps.WeeksLeft(m.StabilityReady);
		if (wait > 0)
		{
			plan.Problem = "The last stability patch is too recent: " + Wait("the next one", wait);
			return plan;
		}
		if (g.points_bugs <= 0f)
		{
			plan.Problem = "It has no bugs.";
			return plan;
		}
		// A few bugs make a small patch.
		float size = Mathf.Clamp01(g.points_bugs / 5f);
		plan.Work = 6f + Points(g) * 0.04f * size + g.points_bugs * 0.5f;
		plan.Cost = 2000L + (long)((float)MmoOps.BaseCost(g) * 0.01f * size);
		return plan;
	}

	// Bugs left after a stability patch: 30%, none once that's under one (or with only a few to start with).
	public static float BugsAfterStability(gameScript g)
	{
		float left = ((g.points_bugs < 3f) ? 0f : (g.points_bugs * 0.3f));
		if (!(left < 1f))
		{
			return left;
		}
		return 0f;
	}

	public static UpdatePlan PlanWelcome(gameScript g)
	{
		MmoGame m = MmoOps.Get(g);
		UpdatePlan plan = new UpdatePlan();
		int wait = MmoOps.WeeksLeft(m.WelcomeReady);
		if (wait > 0)
		{
			plan.Problem = "Former players heard from you recently: " + Wait("the next campaign", wait);
			return plan;
		}
		long pool = Lapsed(g);
		if (pool < Math.Max(2000L, g.abonnements / 20))
		{
			plan.Problem = "Not many former players to win back right now.";
			return plan;
		}
		plan.Work = 10f + Points(g) * 0.03f;
		plan.Cost = 5000L + Math.Min((long)((float)pool * 0.2f), (long)((float)MmoOps.BaseCost(g) * 0.15f));
		if (!FreshContent(g, m))
		{
			plan.Notes = "Tip: more of them come back right after a content patch or an expansion pack.";
		}
		return plan;
	}

	public static UpdatePlan PlanTrial(gameScript g)
	{
		MmoGame m = MmoOps.Get(g);
		UpdatePlan plan = new UpdatePlan();
		if (m.TrialBuilt)
		{
			plan.Problem = "It already has a free trial: switch it on or off on its page.";
			return plan;
		}
		plan.Work = 12f + Points(g) * 0.05f;
		plan.Cost = 10000L + (long)((float)MmoOps.BaseCost(g) * 0.02f);
		if (MmoBots.On)
		{
			plan.Notes = "Bots love free accounts: expect more of them while the trial runs.";
		}
		return plan;
	}

	// Option 0: classic realms in the same subscription; 1: relaunched as its own game (also from classic realms).
	public static UpdatePlan PlanClassic(gameScript g, int option)
	{
		MmoGame m = MmoOps.Get(g);
		UpdatePlan plan = new UpdatePlan();
		if (m.ClassicOf >= 0)
		{
			plan.Problem = "It's a classic version already.";
			return plan;
		}
		gameScript spinoff = ((m.ClassicGame >= 0) ? GameUpdates.FindGame(m.ClassicGame) : null);
		if ((bool)spinoff && spinoff.isOnMarket)
		{
			plan.Problem = "Its classic version is already out as its own game: " + spinoff.GetNameSimple() + ".";
			return plan;
		}
		if (m.ClassicOn)
		{
			plan.Work = 15f + Points(g) * 0.08f;
			plan.Cost = 15000L + (long)((float)MmoOps.BaseCost(g) * 0.04f);
			plan.Notes = "The classic realms' players move over to it.";
			return plan;
		}
		if (g.weeksOnMarket < ClassicAgeWeeks)
		{
			plan.Problem = "Classic servers need an MMO that's been out for at least 5 years (" + MmoUiYears(ClassicAgeWeeks - g.weeksOnMarket) + " to go).";
			return plan;
		}
		if (Changes(g, m) == 0)
		{
			plan.Problem = "It hasn't changed enough since launch for a classic version: release an expansion pack or a world expansion, or overhaul or migrate it first.";
			return plan;
		}
		if (option == 1)
		{
			plan.Work = 35f + Points(g) * 0.18f;
			plan.Cost = 30000L + (long)((float)MmoOps.BaseCost(g) * 0.1f);
		}
		else
		{
			plan.Work = 25f + Points(g) * 0.12f;
			plan.Cost = 20000L + (long)((float)MmoOps.BaseCost(g) * 0.06f);
		}
		return plan;
	}

	private static string MmoUiYears(int weeks)
	{
		if (weeks < 52)
		{
			return InWeeks(weeks);
		}
		float years = (float)weeks / 52f;
		return years.ToString("0.#") + " years";
	}

	public static UpdatePlan PlanPhase(gameScript g)
	{
		MmoGame m = MmoOps.Get(g);
		UpdatePlan plan = new UpdatePlan();
		if (!m.ClassicOn)
		{
			plan.Problem = "Only classic realms get phases.";
			return plan;
		}
		int wait = MmoOps.WeeksLeft(m.ClassicPhaseReady);
		if (wait > 0)
		{
			plan.Problem = "Classic players are still busy with phase " + m.ClassicPhase + ": " + Wait("the next one", wait);
			return plan;
		}
		plan.Work = 12f + Points(g) * 0.05f;
		plan.Cost = 6000L + (long)((float)MmoOps.BaseCost(g) * 0.02f);
		plan.Notes = ((m.ClassicPhase >= Phases) ? "Every phase is out: this opens fresh classic realms that start again from phase 1." : ("Phase " + (m.ClassicPhase + 1) + " of " + Phases + "."));
		return plan;
	}

	private static bool FreshContent(gameScript g, MmoGame m)
	{
		if (m.PatchUntil > Now)
		{
			return true;
		}
		return MmoOps.Addons(g).Any((gameScript a) => a.isOnMarket && a.weeksOnMarket < 12);
	}

	private static float ReviewFactor(gameScript g)
	{
		return 0.5f + Mathf.Clamp(g.reviewTotal, 0, 100) / 200f;
	}

	// ---- Finishing ----

	public static string Complete(UpdateProject p, gameScript g, MmoGame m, string name)
	{
		switch (p.Kind)
		{
		case UpdateKind.MmoBotBust:
		{
			long paying;
			float share;
			long closed = MmoBots.BanWave(g, m, out paying, out share);
			m.BustReady = Now + MmoBots.BustCooldown;
			m.DeterUntil = Now + MmoBots.DeterWeeks;
			m.Busts++;
			g.AddMMOInteresse(3f);
			string text = name + "'s ban wave closed " + MmoUiText.Num(closed) + " bot accounts (" + Mathf.RoundToInt(share * 100f) + "% of its bots).";
			if (paying > 0)
			{
				text = text + "\n\n" + MmoUiText.Num(paying) + " of them paid a subscription: " + G.Money(paying * g.aboPreis) + " a month less income, but real players stay longer and the economy recovers.";
			}
			return text + " Bot makers stay away for the next " + MmoBots.DeterWeeks + " weeks, so fewer come back. Interest +3.";
		}
		case UpdateKind.MmoStability:
		{
			int before = LeftoverBugs.Shown(g);
			g.points_bugs = BugsAfterStability(g);
			int after = LeftoverBugs.Shown(g);
			g.points_technik *= 1.01f;
			m.StableUntil = Now + StableWeeks;
			m.StabilityReady = Now + StabilityCooldown;
			return name + " runs smoothly again.\n\n" + ((after == 0) ? ("All " + ((before == 1) ? "its bugs" : (before + " bugs")) + " fixed") : ((before - after) + " bugs fixed, " + after + " left")) + ", and " + Mathf.RoundToInt(StableChurn * 100f) + "% fewer players leave for the next " + StableWeeks + " weeks.";
		}
		case UpdateKind.MmoWelcomeBack:
		{
			long pool = Lapsed(g);
			bool fresh = FreshContent(g, m);
			float rate = 0.05f + 0.05f * Mathf.Clamp(g.reviewTotal, 0, 100) / 100f + (fresh ? 0.04f : 0f);
			long want = (long)((float)pool * rate);
			long placed = MmoOps.PlaceOnServers(want);
			AddPlayers(g, placed);
			g.AddMMOInteresse(4f);
			m.WelcomeReady = Now + WelcomeCooldown;
			m.Welcomes++;
			string text = name + "'s welcome-back campaign brought " + MmoUiText.Num(placed) + " former players back" + (fresh ? " (the new content helped)." : ".") + " Interest +4.";
			if (placed < want)
			{
				text = text + "\n\n" + MmoUiText.Num(want - placed) + " more wanted to come back, but your servers are full.";
			}
			return text;
		}
		case UpdateKind.MmoFreeTrial:
			m.TrialBuilt = true;
			m.TrialOn = true;
			g.AddMMOInteresse(5f);
			return name + " has a free trial: anyone can play the first levels for free.\n\n" + Mathf.RoundToInt(TrialJoinBonus * 100f) + "% more new players while it runs, interest +5." + (MmoBots.On ? " Bots will use free accounts too: keep an eye on them." : "") + " Switch it on or off any time on its page in MMO Management.";
		case UpdateKind.MmoClassic:
			return CompleteClassic(p, g, m, name);
		case UpdateKind.MmoClassicPhase:
		{
			long pool = Lapsed(g);
			bool fresh = m.ClassicPhase >= Phases;
			long wave = (fresh ? ((long)((float)pool * 0.05f + (float)m.ClassicSubs * 0.1f)) : ((long)((float)m.ClassicSubs * 0.12f + (float)pool * 0.015f)));
			long placed = MmoOps.PlaceOnServers(wave);
			AddPlayers(g, placed);
			m.ClassicSubs += placed;
			m.ClassicPeak = Math.Max(m.ClassicPeak, m.ClassicSubs);
			m.ClassicPhase = (fresh ? 1 : (m.ClassicPhase + 1));
			m.ClassicPhaseReady = Now + PhaseCooldown;
			g.AddMMOInteresse(4f);
			g.AddHype(4f);
			return (fresh ? (m.ClassicName + " opens fresh realms: everyone starts again from phase 1.") : (m.ClassicName + " moves on to phase " + m.ClassicPhase + " of " + Phases + ".")) + "\n\n" + MmoUiText.Num(placed) + " players join, interest +4.";
		}
		default:
			return null;
		}
	}

	private static void AddPlayers(gameScript g, long n)
	{
		if (n <= 0)
		{
			return;
		}
		g.abonnements += n;
		g.abonnementsWoche += n;
		if (g.bestAbonnements < g.abonnements)
		{
			g.bestAbonnements = g.abonnements;
		}
	}

	private static string CompleteClassic(UpdateProject p, gameScript g, MmoGame m, string name)
	{
		string classicName = (string.IsNullOrEmpty(p.NewName?.Trim()) ? (m.ClassicName ?? DefaultClassicName(g)) : p.NewName.Trim());
		long pool = Lapsed(g);
		if (!m.ClassicOn && p.Option == 0)
		{
			long wave = (long)((float)pool * 0.08f * ReviewFactor(g));
			long placed = MmoOps.PlaceOnServers(wave);
			AddPlayers(g, placed);
			long moved = g.abonnements / 25;
			m.ClassicOn = true;
			m.ClassicName = classicName;
			m.ClassicSubs = placed + moved;
			m.ClassicPeak = m.ClassicSubs;
			m.ClassicPhase = 1;
			m.ClassicPhaseReady = Now + PhaseCooldown;
			m.ClassicWeek = Now;
			m.ClassicHistory.Clear();
			g.AddMMOInteresse(6f);
			g.AddHype(8f);
			return "<b>" + classicName + "</b> is open: " + name + " as it was at launch, in the same subscription.\n\n" + MmoUiText.Num(placed) + " former players came back for it and " + MmoUiText.Num(moved) + " current players moved over. Interest +6, hype +8. Release its next phase every few months to keep classic players busy; switch between the live game and the classic realms on its page.";
		}
		gameScript c = CreateClassicGame(g, m, classicName, p.Cost);
		if (!c)
		{
			G.MS.Earn(p.Cost, 1);
			return "The classic relaunch of " + name + " failed (see the log). " + G.Money(p.Cost) + " came back.";
		}
		long fromRealms = 0L;
		if (m.ClassicOn)
		{
			fromRealms = Math.Min(m.ClassicSubs, g.abonnements);
			m.ClassicOn = false;
			m.ClassicSubs = 0L;
		}
		else
		{
			fromRealms = g.abonnements / 30;
		}
		g.abonnements -= fromRealms;
		long back = MmoOps.PlaceOnServers((long)((float)pool * 0.12f * ReviewFactor(g)));
		c.abonnements = fromRealms + back;
		c.sellsTotal = c.abonnements;
		c.bestAbonnements = c.abonnements;
		MmoGame cm = MmoOps.Get(c);
		cm.OldSchool = true;
		cm.ClassicOf = g.myID;
		cm.LaunchGfx = Mathf.Max(1f, c.points_grafik);
		cm.LaunchTech = Mathf.Max(1f, c.points_technik);
		m.ClassicGame = c.myID;
		if ((bool)c.gameTab_)
		{
			c.gameTab_.UpdateData();
		}
		return "<b>" + classicName + "</b> is out as its own MMO: " + name + " as it was at launch, with its own subscription.\n\n" + MmoUiText.Num(c.abonnements) + " players start in it (" + MmoUiText.Num(back) + " came back for it). It's run as an old-school MMO and has its own page in MMO Management.";
	}

	// A copy of the MMO as its own game, made the way the game turns an MMO into a free-to-play game
	// (Menu_W_MMOtoF2P.CreateGame): its launch-day graphics and tech, no sales yet, released today.
	private static gameScript CreateClassicGame(gameScript g, MmoGame m, string name, long cost)
	{
		try
		{
			gameScript c = UnityEngine.Object.Instantiate(g.gameObject).GetComponent<gameScript>();
			G.MS.games_.InitMMOtoF2PGame(c);
			// The copy still points at the original's card: SetOnMarket would remove that one.
			c.gameTab_ = null;
			c.SetMyName(name);
			c.gameTyp = 1;
			c.publisherID = G.MS.myID;
			c.pS_ = null;
			c.warBeiAwards = true;
			c.weeksOnMarket = 0;
			c.releaseDate = 0;
			c.vorbestellungen = 0;
			c.date_year = G.MS.year;
			c.date_month = G.MS.month;
			c.spielbericht_favorit = false;
			c.mmoTOf2p_created = false;
			c.f2pConverted = false;
			c.commercialHit = false;
			c.commercialFlop = false;
			c.abonnements = 0L;
			c.abonnementsWoche = 0L;
			c.bestAbonnements = 0L;
			c.sellsTotal = 0L;
			c.sellsTotalOnline = 0L;
			c.sellsTotalStandard = 0L;
			c.sellsTotalDeluxe = 0L;
			c.sellsTotalCollectors = 0L;
			c.umsatzTotal = 0L;
			c.umsatzInApp = 0L;
			c.umsatzAbos = 0L;
			c.inAppPurchaseWeek = 0;
			c.costs_entwicklung = cost;
			c.costs_mitarbeiter = 0L;
			c.costs_marketing = 0L;
			c.costs_enginegebuehren = 0L;
			c.costs_server = 0L;
			c.costs_production = 0L;
			if (c.sellsPerWeek != null)
			{
				for (int i = 0; i < c.sellsPerWeek.Length; i++)
				{
					c.sellsPerWeek[i] = 0;
				}
			}
			if (c.lagerbestand != null)
			{
				for (int j = 0; j < c.lagerbestand.Length; j++)
				{
					c.lagerbestand[j] = 0L;
				}
			}
			if (m.LaunchGfx > 0f)
			{
				c.points_grafik = m.LaunchGfx;
			}
			if (m.LaunchTech > 0f)
			{
				c.points_technik = m.LaunchTech;
			}
			c.points_bugs = 0f;
			c.points_bugsInvis = 0f;
			c.amountUpdates = 0;
			c.amountMMOAddons = 0;
			c.bonusSellsAddons = 0f;
			c.mmoInteresse = Mathf.Max(c.mmoInteresse, 50f);
			c.hype = Mathf.Max(c.hype, 40f);
			c.userPositiv /= 10;
			c.userNegativ /= 10;
			c.SetOnMarket();
			GameUpdates.Touch();
			Plugin.Log.LogInfo("Classic relaunch: " + name + " (game " + c.myID + ") from " + g.GetNameSimple() + " (game " + g.myID + ")");
			return c;
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Classic relaunch failed: " + ex);
			return null;
		}
	}

	// ---- Weekly ----

	// Classic players are part of the game's subscribers: they come and go with the rest (the game's own weekly
	// numbers), and on top of that nostalgia fades the longer a phase lasts.
	public static void AfterSales(gameScript g, MmoGame m, long before)
	{
		if (!m.ClassicOn)
		{
			return;
		}
		if (before > 0)
		{
			m.ClassicSubs = (long)((double)m.ClassicSubs * (double)g.abonnements / (double)before);
		}
		int since = Now - (m.ClassicPhaseReady - PhaseCooldown);
		float fade = Mathf.Clamp(0.0005f * (float)since, 0f, 0.015f);
		long lost = (long)((float)m.ClassicSubs * fade);
		m.ClassicSubs -= lost;
		g.abonnements = Math.Max(0L, g.abonnements - lost);
		g.abonnementsWoche -= lost;
		m.ClassicSubs = Math.Max(0L, Math.Min(m.ClassicSubs, g.abonnements));
		m.ClassicPeak = Math.Max(m.ClassicPeak, m.ClassicSubs);
	}

	public static string SetTrial(gameScript g, bool on)
	{
		MmoGame m = MmoOps.Get(g);
		if (!m.TrialBuilt)
		{
			return "It has no free trial yet.";
		}
		m.TrialOn = on;
		GameUpdates.Touch();
		return null;
	}
}
