using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HolyAddon.Core.CopyProtection;
using HolyAddon.Core.Updates;
using HolyAddon.UI;
using Newtonsoft.Json;
using UnityEngine;

namespace HolyAddon.Core.Mmo;

// What the addon keeps about one of your MMOs.
internal class MmoGame
{
	public int GameId;

	// Built with (or upgraded to) a 64-bit client. MMOs released from 2008 on start with one.
	public bool Client64;

	// The 64-bit client came from our project (it also keeps a few more players).
	public bool Client64Built;

	// Old-school theme-park design: no dated penalty, a smaller but more loyal audience.
	public bool OldSchool;

	public int StanceWeek = -1000;

	// Weeks taken off the game's age for wear by modernising it.
	public int Renewal;

	// Graphics and tech points when the addon first saw the game, and its release month (mainScript.PassedMonth).
	public float LaunchGfx = -1f;

	public float LaunchTech = -1f;

	public int LaunchMonth = -1;

	public int PatchUntil = -1000;

	public int PatchReady = -1000;

	public int OverhaulReady = -1000;

	public int ExpandReady = -1000;

	public int DipUntil = -1000;

	public int Overhauls;

	public int Migrations;

	public int Expansions;

	public int Patches;

	// Last month (mainScript.PassedMonth) its chart place earned IP points.
	public int IpMonth = -1;

	// Subscribers at the end of each week (newest last).
	public List<long> History = new List<long>();

	// The week (CopyProtectDev.AbsWeek) of History's newest entry; -1 = not known (saves before 0.11.5).
	public int HistoryWeek = -1;

	// Platforms beyond the game's four platform slots (the game saves only four per game).
	public List<int> ExtraPlatforms = new List<int>();
}

// A platform an MMO could launch on, or why not.
internal class PlatformOption
{
	public platformScript Platform;

	public string Blocked;
}

// An MMO's numbers at the last awards ceremony, to judge its year at the next one.
internal class AwardBase
{
	public long Subs;

	public int Updates;

	public int Addons;
}

// A Best Ongoing Game award.
internal class AwardWin
{
	public int Year;

	public int GameId;

	public string Game;

	public string Studio;

	public bool Mine;
}

internal class MmoState
{
	public Dictionary<int, MmoGame> Games = new Dictionary<int, MmoGame>();

	// Year of the last ceremony that judged the Best Ongoing Game award.
	public int AwardYear = -1;

	// Every MMO on the market at that ceremony (rivals' too), by game id.
	public Dictionary<int, AwardBase> AwardBase = new Dictionary<int, AwardBase>();

	public List<AwardWin> Wins = new List<AwardWin>();
}

internal static class MmoUiText
{
	public static string Num(long n)
	{
		if (!G.MS)
		{
			return n.ToString();
		}
		return G.MS.GetMoney(n, showDollar: false);
	}
}

// Everything the MMO management window does to your MMOs. The game's own MMO model is the base: interest (how many
// new players come), age wear (caps interest more each year after the first), churn (players leaving each week,
// faster with a low rating, low hype and age) and the points every game has. Projects are room tasks of the update
// system (UpdateKind.Mmo*); their effects go through those same numbers.
[HarmonyPatch]
internal static class MmoOps
{
	private const string SaveKey = "mgt2.editor.mmo";

	public const int HistoryWeeks = 156;

	// Last month of 2007: 32-bit clients can't show graphics past that year's standard.
	private const int Month2007 = 384;

	public const int StanceWeeks = 104;

	public const int OverhaulCooldown = 52;

	public const int PatchCooldown = 12;

	public const int PatchWeeks = 8;

	// World expansion, by the size it starts from (B, B+, A, AA, AAA).
	private static readonly float[] StepWork = new float[5] { 0.35f, 0.4f, 0.5f, 0.6f, 0.75f };

	private static readonly float[] StepCost = new float[5] { 0.25f, 0.3f, 0.4f, 0.5f, 0.6f };

	private static readonly float[] StepGain = new float[5] { 0.25f, 0.22f, 0.2f, 0.18f, 0.15f };

	// Weeks to wait after reaching a size before the next step can start (B+, A, AA, AAA).
	public static readonly int[] StepWait = new int[6] { 0, 26, 39, 52, 78, 0 };

	private static MmoState S = new MmoState();

	internal static MmoState State => S;

	public static bool On
	{
		get
		{
			if (Plugin.MmoManagement.Value)
			{
				return G.Singleplayer;
			}
			return false;
		}
	}

	public static bool PenaltyOn
	{
		get
		{
			if (On)
			{
				return Plugin.MmoDatedPenalty.Value;
			}
			return false;
		}
	}

	public static int Now => CopyProtectDev.AbsWeek;

	public static int NowMonth
	{
		get
		{
			if (!G.MS)
			{
				return 0;
			}
			return G.MS.PassedMonth();
		}
	}

	// ---- Which games ----

	public static bool IsMyMmo(gameScript g)
	{
		if (GameUpdates.BaseEligible(g) && g.gameTyp == 1)
		{
			return g.releaseDate <= 0;
		}
		return false;
	}

	public static List<gameScript> MyMmos()
	{
		gameScript[] all = ((G.MS && (bool)G.MS.games_) ? G.MS.games_.arrayGamesScripts : null);
		if (all == null)
		{
			return new List<gameScript>();
		}
		return (from g in all
			where (bool)g && IsMyMmo(g)
			orderby g.abonnements descending
			select g).ToList();
	}

	// Where each MMO sits in the subscription charts (MMOs on the market with subscribers, most first). Worked out
	// at most once a second: game cards ask every frame.
	private static readonly Dictionary<int, int> ranks = new Dictionary<int, int>();

	private static float nextRanks;

	public static int ChartRank(gameScript g)
	{
		if (Time.unscaledTime >= nextRanks)
		{
			nextRanks = Time.unscaledTime + 1f;
			ranks.Clear();
			gameScript[] all = ((G.MS && (bool)G.MS.games_) ? G.MS.games_.arrayGamesScripts : null);
			if (all != null)
			{
				int rank = 0;
				foreach (gameScript x in from x in all
					where (bool)x && x.gameTyp == 1 && !x.typ_mmoaddon && x.isOnMarket && !x.inDevelopment && x.abonnements > 0
					orderby x.abonnements descending
					select x)
				{
					ranks[x.myID] = ++rank;
				}
			}
		}
		if (!g || !ranks.TryGetValue(g.myID, out var r))
		{
			return -1;
		}
		return r;
	}

	// How many MMOs are in the subscription charts.
	public static int ChartSize()
	{
		ChartRank(null);
		return ranks.Count;
	}

	// Your MMO addons (expansion packs) for this MMO.
	public static List<gameScript> Addons(gameScript g)
	{
		gameScript[] all = ((G.MS && (bool)G.MS.games_) ? G.MS.games_.arrayGamesScripts : null);
		if (all == null || !g)
		{
			return new List<gameScript>();
		}
		return (from x in all
			where (bool)x && x.typ_mmoaddon && x.originalGameID == g.myID && !x.inDevelopment
			orderby x.date_year descending, x.date_month descending
			select x).ToList();
	}

	public static MmoGame Find(int gameId)
	{
		if (!S.Games.TryGetValue(gameId, out var m))
		{
			return null;
		}
		return m;
	}

	// The game's record, made the first time the addon sees it: today's points become its launch baseline.
	public static MmoGame Get(gameScript g)
	{
		if (!S.Games.TryGetValue(g.myID, out var m) || m == null)
		{
			m = new MmoGame
			{
				GameId = g.myID
			};
			S.Games[g.myID] = m;
		}
		if (m.LaunchMonth < 0)
		{
			m.LaunchMonth = ((g.date_year > 0) ? ((g.date_year - 1976) * 12 + g.date_month) : NowMonth);
			m.LaunchGfx = Mathf.Max(1f, g.points_grafik);
			m.LaunchTech = Mathf.Max(1f, g.points_technik);
			m.Client64 = g.date_year >= 2008;
			m.OldSchool = g.retro;
		}
		m.History = m.History ?? new List<long>();
		m.ExtraPlatforms = m.ExtraPlatforms ?? new List<int>();
		return m;
	}

	// ---- Platforms (no four-slot limit for MMOs) ----

	// Every platform the MMO is on: its four slots, then the extra ones.
	public static List<int> AllPlatforms(gameScript g)
	{
		List<int> list = g.gamePlatform.Where((int id) => id != -1).ToList();
		MmoGame m = Find(g.myID);
		if (m?.ExtraPlatforms != null)
		{
			list.AddRange(m.ExtraPlatforms.Where((int id) => id != -1 && !list.Contains(id)));
		}
		return list;
	}

	public static bool Retired(int platformId)
	{
		platformScript p = GameUpdates.Platform(platformId);
		if ((bool)p)
		{
			return p.vomMarktGenommen;
		}
		return true;
	}

	// The tech level its engine features need from a platform (Menu_DevGame_Platform.GetMindestTechStufe).
	public static int NeededTech(gameScript g)
	{
		engineFeatures ef = GameUpdates.EF;
		int tech = 1;
		if (!ef)
		{
			return tech;
		}
		foreach (int f in g.gameEngineFeature)
		{
			if (f >= 0 && f < ef.engineFeatures_TECH.Length)
			{
				tech = Mathf.Max(tech, ef.engineFeatures_TECH[f]);
			}
		}
		return tech;
	}

	// Platforms on the market you can develop for that the MMO isn't on yet, by the game's rules for changing
	// platforms (dev kit, same kind, powerful enough for its engine), plus online. Ones missing a feature the
	// platform requires are listed with the reason.
	public static List<PlatformOption> PlatformOptions(gameScript g)
	{
		List<PlatformOption> list = new List<PlatformOption>();
		platformScript[] all = (G.MS ? G.MS.arrayPlatformsScripts : null);
		if (all == null)
		{
			return list;
		}
		List<int> on = AllPlatforms(g);
		int tech = NeededTech(g);
		gameplayFeatures gf = GameUpdates.GF;
		foreach (platformScript p in all)
		{
			if (!p || on.Contains(p.myID) || p.vomMarktGenommen || p.IsProConsoleInDev() || (!p.isUnlocked && p.ownerID != G.MS.myID) || !p.inBesitz || !p.internet || p.tech < tech)
			{
				continue;
			}
			if ((p.typ == 3) != g.handy || (p.typ == 4) != g.arcade)
			{
				continue;
			}
			string blocked = null;
			if (p.needFeatures != null && (bool)gf)
			{
				List<string> missing = (from need in p.needFeatures
					where need != -1 && need < g.gameGameplayFeatures.Length && !g.gameGameplayFeatures[need]
					select gf.GetName(need)).ToList();
				if (missing.Count > 0)
				{
					blocked = "Needs " + string.Join(", ", missing) + ".";
				}
			}
			list.Add(new PlatformOption
			{
				Platform = p,
				Blocked = blocked
			});
		}
		return list.OrderBy((PlatformOption o) => (o.Blocked != null) ? 1 : 0).ThenByDescending((PlatformOption o) => o.Platform.GetMarktanteil()).ToList();
	}

	public static string PortProblem(gameScript g)
	{
		if (TimedExclusives.Active(g) != null)
		{
			return "It's a timed exclusive: it comes to its other platforms when the deal ends.";
		}
		if (PlatformOptions(g).Any((PlatformOption o) => o.Blocked == null))
		{
			return null;
		}
		return "There's no other platform it can launch on right now: it needs a dev kit for a platform on the market with internet that can run its engine (tech " + NeededTech(g) + " or more).";
	}

	// Where the picked platforms go: free slots first (empty ones, then ones whose platform has retired), the
	// rest become extra platforms. Returns the four slots afterwards.
	public static int[] SlotsAfter(gameScript g, IList<int> picked, out List<int> extra)
	{
		int[] slots = (int[])g.gamePlatform.Clone();
		extra = new List<int>();
		Queue<int> todo = new Queue<int>(picked);
		foreach (int slot in GameUpdates.FreeSlots(g).OrderBy((int s) => (g.gamePlatform[s] == -1) ? 0 : 1))
		{
			if (todo.Count == 0)
			{
				break;
			}
			slots[slot] = todo.Dequeue();
		}
		extra.AddRange(todo);
		return slots;
	}

	// The launch boost for new platforms: their share of the MMO's whole reach afterwards (as ports work out theirs).
	public static float LaunchBoost(gameScript g, IList<int> picked)
	{
		List<int> now = AllPlatforms(g);
		float after = GameUpdates.Reach(g, now.Concat(picked.Where((int id) => !now.Contains(id))), exclusive: false);
		float fresh = GameUpdates.Reach(g, picked, exclusive: false) - 10f;
		return Mathf.Clamp(1.2f * fresh / Mathf.Max(1f, after), 0f, GameUpdates.LaunchMax);
	}

	// Costs and work per platform as for ports; players from its whole reach (slots and extra platforms).
	public static UpdatePlan PlanPlatforms(gameScript g, IList<int> picked)
	{
		UpdatePlan plan = new UpdatePlan();
		if (picked == null || picked.Count == 0)
		{
			plan.Problem = "Pick at least one platform.";
			return plan;
		}
		float dev = GameUpdates.DevPoints(g);
		long devCost = GameUpdates.DevCost(g);
		foreach (int id in picked)
		{
			platformScript p = GameUpdates.Platform(id);
			float complex = (p ? (1f + (float)Mathf.Clamp(p.complex, 0, 3) * 0.15f) : 1f);
			plan.Work += (0.15f * dev + 15f) * complex;
			plan.Cost += (long)(0.08f * (float)devCost) + (p ? p.GetDevCosts() : 0);
		}
		List<int> now = AllPlatforms(g);
		float before = GameUpdates.Reach(g, now, g.exklusiv && now.Count <= 1);
		float after = GameUpdates.Reach(g, now.Concat(picked.Where((int id) => !now.Contains(id))), exclusive: false);
		float boost = LaunchBoost(g, picked);
		plan.Notes = "New players from then on: " + GameUpdates.Pct(after / Mathf.Max(1f, before) - 1f) + ", plus a launch boost of up to " + GameUpdates.Pct(boost) + " fading over " + GameUpdates.LaunchWeeks + " weeks.";
		if (g.exklusiv)
		{
			plan.Notes += " It stops being an exclusive.";
		}
		if (g.herstellerExklusiv && picked.Any((int id) => GameUpdates.Platform(id)?.ownerID != G.MS.myID))
		{
			plan.Notes += " It stops being exclusive to your own platforms.";
		}
		return plan;
	}

	// How much more of the market its extra platforms reach (the game's own sales count only the four slots).
	public static float ExtraReachFactor(gameScript g, MmoGame m)
	{
		if (m.ExtraPlatforms == null || m.ExtraPlatforms.Count == 0)
		{
			return 1f;
		}
		List<int> main = g.gamePlatform.Where((int id) => id != -1).ToList();
		float before = GameUpdates.Reach(g, main, g.exklusiv);
		float after = GameUpdates.Reach(g, main.Concat(m.ExtraPlatforms.Where((int id) => !main.Contains(id))), exclusive: false);
		return Mathf.Max(1f, after / Mathf.Max(1f, before));
	}

	public static string PlatformNames(IEnumerable<int> ids)
	{
		return string.Join(", ", from id in ids
			select GameUpdates.Platform(id) into p
			where (bool)p
			select p.GetName());
	}

	// ---- Today's standards ----

	// The game's review standard over time (games.curveReview, as gameScript.CalcReview uses it).
	public static float Curve(int month)
	{
		games gm = (G.MS ? G.MS.games_ : null);
		if (!gm || gm.curveReview == null)
		{
			return 1f;
		}
		return Mathf.Max(0.0001f, gm.curveReview.Evaluate((float)Mathf.Clamp(month, 0, 600) / 600f));
	}

	// The points a game needs in one area for a top score, at your difficulty (gameScript.CalcReview).
	public static float Standard(int month)
	{
		int difficulty = (G.MS ? G.MS.difficulty : 2);
		float k = difficulty switch
		{
			0 => 7000f,
			1 => 10000f,
			2 => 15000f,
			3 => 18000f,
			4 => 22000f,
			5 => 30000f,
			_ => 15000f,
		};
		return k * Curve(month);
	}

	// What a review would give these points in one area today, or at the given month (0-100).
	public static int Score(float points, int month)
	{
		return Mathf.Clamp(Mathf.RoundToInt(points / (Standard(month) / 100f)), 0, 100);
	}

	// How well an area holds up against today's games compared with launch day: 1 = as good as at launch.
	public static float Freshness(float points, float launchPoints, int launchMonth)
	{
		if (launchPoints <= 0f)
		{
			return 1f;
		}
		return Mathf.Clamp(points / launchPoints * Curve(launchMonth) / Curve(NowMonth), 0f, 1.5f);
	}

	public static float GraphicsFresh(gameScript g, MmoGame m)
	{
		return Freshness(g.points_grafik, m.LaunchGfx, m.LaunchMonth);
	}

	public static float TechFresh(gameScript g, MmoGame m)
	{
		return Freshness(g.points_technik, m.LaunchTech, m.LaunchMonth);
	}

	// 0 = holds up, 1 = badly dated. Starts once graphics and tech together fall below 80% of launch standing.
	public static float Dated(gameScript g, MmoGame m)
	{
		float fresh = (Mathf.Min(1f, GraphicsFresh(g, m)) + Mathf.Min(1f, TechFresh(g, m))) * 0.5f;
		return Mathf.Clamp01((0.8f - fresh) / 0.4f);
	}

	public const float DatedChurn = 0.4f;

	public const float DatedJoin = 0.2f;

	public const float OldSchoolJoin = 0.25f;

	public const float OldSchoolChurn = 0.25f;

	public const float PatchChurn = 0.2f;

	public const float ClientChurn = 0.05f;

	// Extra share of the game's own weekly churn (negative = fewer players leave).
	public static float ChurnExtra(gameScript g, MmoGame m)
	{
		float extra = 0f;
		if (m.OldSchool)
		{
			extra -= OldSchoolChurn;
		}
		else if (PenaltyOn)
		{
			extra += DatedChurn * Dated(g, m);
		}
		if (m.PatchUntil > Now)
		{
			extra -= PatchChurn;
		}
		if (m.Client64Built)
		{
			extra -= ClientChurn;
		}
		return extra;
	}

	// Multiplier on new players (through the game's weekly sales roll).
	public static float NewPlayerFactor(gameScript g)
	{
		if (!On || !g || g.gameTyp != 1 || !g.IsMyGame())
		{
			return 1f;
		}
		MmoGame m = Find(g.myID);
		if (m == null)
		{
			return 1f;
		}
		float reach = ExtraReachFactor(g, m);
		if (m.OldSchool)
		{
			return reach * (1f - OldSchoolJoin);
		}
		if (PenaltyOn)
		{
			return reach * (1f - DatedJoin * Dated(g, m));
		}
		return reach;
	}

	// ---- Shared numbers ----

	public static string SizeName(int size)
	{
		return size switch
		{
			0 => G.Text(329, "B"),
			1 => G.Text(330, "B+"),
			2 => G.Text(331, "A"),
			3 => G.Text(332, "AA"),
			4 => G.Text(333, "AAA"),
			5 => G.Text(2193, "AAAA"),
			_ => "?",
		};
	}

	// What the game charges for a game of this size today (Menu_DevGame.costs_gameSize, as new games pay it).
	public static long SizeCost(int size)
	{
		try
		{
			Menu_DevGame dev = (((bool)G.Gui && G.Gui.uiObjects != null && G.Gui.uiObjects.Length > 56 && (bool)G.Gui.uiObjects[56]) ? G.Gui.uiObjects[56].GetComponent<Menu_DevGame>() : null);
			if (!dev || dev.costs_gameSize == null || size < 0 || size >= dev.costs_gameSize.Length)
			{
				return 0L;
			}
			long cost = dev.costs_gameSize[size];
			return (size == 0) ? cost : (cost * (G.MS.difficulty + 1));
		}
		catch
		{
			return 0L;
		}
	}

	// Old MMOs had small budgets: projects cost at least what a game of their size costs today.
	public static long BaseCost(gameScript g)
	{
		return Math.Max(GameUpdates.DevCost(g), SizeCost(g.gameSize));
	}

	private static float Points(gameScript g)
	{
		return GameUpdates.DevPoints(g);
	}

	public static int WeeksLeft(int readyWeek)
	{
		return Mathf.Max(0, readyWeek - Now);
	}

	private static string InWeeks(int weeks)
	{
		return (weeks == 1) ? "1 week" : (weeks + " weeks");
	}

	// ---- Graphics overhaul ----

	// Graphics points that would match its launch-day standing among today's games (32-bit clients stop at 2007).
	public static float OverhaulTarget(gameScript g, MmoGame m, out bool capped)
	{
		float target = m.LaunchGfx * Curve(NowMonth) / Curve(m.LaunchMonth);
		capped = false;
		if (!m.Client64 && G.MS.year >= 2008 && m.LaunchMonth < Month2007)
		{
			float ceiling = m.LaunchGfx * Curve(Month2007) / Curve(m.LaunchMonth);
			if (ceiling < target)
			{
				target = ceiling;
				capped = true;
			}
		}
		return target;
	}

	public static UpdatePlan PlanOverhaul(gameScript g)
	{
		MmoGame m = Get(g);
		UpdatePlan plan = new UpdatePlan();
		int wait = WeeksLeft(m.OverhaulReady);
		if (wait > 0)
		{
			plan.Problem = "The art team needs a break after the last overhaul: the next one can start in " + InWeeks(wait) + ".";
			return plan;
		}
		bool capped;
		float gain = OverhaulTarget(g, m, out capped) - g.points_grafik;
		if (gain < Mathf.Max(5f, g.points_grafik * 0.05f))
		{
			plan.Problem = (capped ? "Its 32-bit client can't show anything better. Build the 64-bit client first." : "Its graphics still hold up against today's games.");
			return plan;
		}
		float share = Mathf.Min(3f, gain / Mathf.Max(1f, m.LaunchGfx));
		plan.Work = 20f + Points(g) * 0.35f * share;
		plan.Cost = 10000L + (long)((float)BaseCost(g) * 0.3f * share);
		plan.Graphics = gain;
		if (capped)
		{
			plan.Notes = "The 32-bit client holds it to 2007-level graphics. A 64-bit client lifts that.";
		}
		if (m.OldSchool)
		{
			plan.Notes = ((plan.Notes.Length > 0) ? (plan.Notes + " ") : "") + "Old-school players don't mind dated graphics, so this mostly draws in new players.";
		}
		return plan;
	}

	public static int OverhaulInterest(gameScript g)
	{
		return 8 + Mathf.RoundToInt(Mathf.Clamp(g.reviewTotal, 0, 100) * 0.1f);
	}

	// ---- 64-bit client ----

	public static UpdatePlan PlanClient64(gameScript g)
	{
		MmoGame m = Get(g);
		UpdatePlan plan = new UpdatePlan();
		if (m.Client64)
		{
			plan.Problem = "It already has a 64-bit client.";
			return plan;
		}
		if (G.MS.year < 2005)
		{
			plan.Problem = "64-bit clients become possible in 2005.";
			return plan;
		}
		plan.Work = 20f + Points(g) * 0.25f;
		plan.Cost = 10000L + (long)((float)BaseCost(g) * 0.1f);
		plan.Tech = Mathf.Max(5f, g.points_technik * 0.1f);
		return plan;
	}

	// ---- Engine migration ----

	public static int EngineTech(engineScript e)
	{
		if (!e)
		{
			return 0;
		}
		return e.GetTechLevel();
	}

	// Engine work on an MMO: newer features from its own engine (an upgrade), or moving it to another engine you own
	// or license (a migration: a bigger job, a bigger renewal, and a short dip in players).
	public static bool IsMigration(gameScript g, engineScript e)
	{
		if ((bool)e)
		{
			return e.myID != g.engineID;
		}
		return false;
	}

	public static UpdatePlan PlanEngineWork(gameScript g, engineScript e)
	{
		UpdatePlan plan = GameUpdates.PlanEngine(g, e);
		if (plan.Problem != null || !IsMigration(g, e))
		{
			return plan;
		}
		plan.Work = plan.Work * 1.5f + Points(g) * 0.25f;
		plan.Cost = (long)((float)plan.Cost * 1.5f) + (long)((float)BaseCost(g) * 0.1f);
		return plan;
	}

	// How many weeks younger engine work makes it: a migration by how far ahead the new engine is.
	public static int EngineRenewal(gameScript g, engineScript e, int upgradedSlots)
	{
		if (IsMigration(g, e))
		{
			int jump = EngineTech(e) - EngineTech(GameUpdates.CurrentEngine(g));
			return Mathf.Clamp(26 * jump, 26, 156);
		}
		return Mathf.Clamp(13 * upgradedSlots, 13, 52);
	}

	public static int EngineInterest(gameScript g, engineScript e)
	{
		if (!IsMigration(g, e))
		{
			return 8;
		}
		return 15;
	}

	public static string EngineEffects(gameScript g, engineScript e)
	{
		int slots = GameUpdates.UpgradeSlots(g, e).Count((int f) => f != -1);
		string text = "For the MMO: interest +" + EngineInterest(g, e) + ", and it counts as " + YoungerText(EngineRenewal(g, e, slots)) + " for age wear.";
		if (IsMigration(g, e))
		{
			text += " Players dip a little for two weeks while everyone gets used to the new engine.";
		}
		return text;
	}

	// ---- Normal update types, MMO versions ----

	// New platforms: interest by how much of the game's reach the new platforms add.
	public static int PortInterest(float launchBoost)
	{
		return 5 + Mathf.RoundToInt(20f * Mathf.Clamp(launchBoost, 0f, 1f));
	}

	public static string PortEffects(gameScript g, IList<int> picked)
	{
		return "For the MMO: interest +" + PortInterest(LaunchBoost(g, picked)) + " and half a year younger for age wear. Make sure your servers have room for the new players.";
	}

	// New game systems: features that suit the genre bring players back; ones that don't drive a few away.
	public static void SystemsFit(gameScript g, IEnumerable<int> features, out int fit, out int poor)
	{
		fit = 0;
		poor = 0;
		gameplayFeatures gf = GameUpdates.GF;
		foreach (int j in features)
		{
			float bonus = gf.GetBonus(j, g.maingenre, g.subgenre);
			if (bonus >= 1f)
			{
				fit++;
			}
			else if (bonus < 0.5f)
			{
				poor++;
			}
		}
	}

	public static int SystemsInterest(int count, int fit)
	{
		return Mathf.Min(15, 2 + count + 2 * fit);
	}

	public static int SystemsRenewal(int count)
	{
		return Mathf.Min(52, 8 * count);
	}

	public const float PoorSystemLoss = 0.01f;

	public static string SystemsEffects(gameScript g, ICollection<int> picked)
	{
		if (picked == null || picked.Count == 0)
		{
			return "For the MMO: new systems raise interest and make it count as younger for age wear; ones that suit the genre help most.";
		}
		SystemsFit(g, picked, out int fit, out int poor);
		return "For the MMO: interest +" + SystemsInterest(picked.Count, fit) + ", " + YoungerText(SystemsRenewal(picked.Count)) + " for age wear" + ((poor > 0) ? (", but " + Mathf.RoundToInt(PoorSystemLoss * 100f * (float)poor) + "% of its players quit over systems that don't suit it.") : ".");
	}

	// Age rating: a wider audience, but some subscribers liked what gets cut.
	public const float RatingLossPerStep = 0.02f;

	public static string RatingEffects(int steps)
	{
		if (steps <= 0)
		{
			return "";
		}
		return "For the MMO: " + Mathf.RoundToInt(RatingLossPerStep * 100f * (float)steps) + "% of its subscribers leave over the cut content, while the wider audience brings interest +" + 3 * steps + ".";
	}

	public static string YoungerText(int weeks)
	{
		if (weeks < 52)
		{
			return ((weeks >= 26) ? "half a year" : (weeks + " weeks")) + " younger";
		}
		float years = (float)weeks / 52f;
		return ((Mathf.Abs(years - Mathf.Round(years)) < 0.05f) ? Mathf.RoundToInt(years).ToString() : years.ToString("0.#")) + ((years < 1.05f) ? " year" : " years") + " younger";
	}

	// ---- World expansion ----

	public static UpdatePlan PlanExpansion(gameScript g)
	{
		MmoGame m = Get(g);
		UpdatePlan plan = new UpdatePlan();
		int s = g.gameSize;
		if (s >= 5)
		{
			plan.Problem = "It's already the biggest size (" + SizeName(5) + ").";
			return plan;
		}
		if (s < 0)
		{
			plan.Problem = "Its size is unknown.";
			return plan;
		}
		int wait = WeeksLeft(m.ExpandReady);
		if (wait > 0)
		{
			plan.Problem = "The team needs time after the last expansion: the next step can start in " + InWeeks(wait) + ".";
			return plan;
		}
		plan.Work = 30f + Points(g) * StepWork[s];
		long sizeDelta = Math.Max(0L, SizeCost(s + 1) - SizeCost(s));
		plan.Cost = 20000L + Math.Max((long)((float)GameUpdates.DevCost(g) * StepCost[s]), sizeDelta);
		float gain = StepGain[s];
		plan.Gameplay = g.points_gameplay * gain;
		plan.Graphics = g.points_grafik * gain;
		plan.Sound = g.points_sound * gain;
		plan.Tech = g.points_technik * gain;
		int nextWait = StepWait[s + 1];
		plan.Notes = (nextWait > 0) ? ("After it's done, the next step can start " + InWeeks(nextWait) + " later.") : "That's the biggest size there is.";
		return plan;
	}

	// ---- Content patch ----

	public static UpdatePlan PlanPatch(gameScript g)
	{
		MmoGame m = Get(g);
		UpdatePlan plan = new UpdatePlan();
		int wait = WeeksLeft(m.PatchReady);
		if (wait > 0)
		{
			plan.Problem = "Players are still busy with the last patch: the next one can start in " + InWeeks(wait) + ".";
			return plan;
		}
		plan.Work = 15f + Points(g) * 0.12f;
		plan.Cost = 5000L + (long)((float)BaseCost(g) * 0.03f);
		plan.Gameplay = g.points_gameplay * 0.02f;
		return plan;
	}

	public static int PatchInterest(gameScript g)
	{
		return 5 + Mathf.RoundToInt(Mathf.Clamp(g.reviewTotal, 0, 100) * 0.05f);
	}

	// ---- The MMO's IP ----

	// The game fades an IP that has had no release for 250 weeks (games.SellAllGames: -20 to -100 IP points by
	// difficulty, then the clock starts again). A running MMO keeps its IP alive: projects and a healthy player base
	// hold the clock at this many weeks. That's past the weeks in which the game gives a new sequel fewer IP points
	// (gameScript.AddIpPointsRelease), so releasing one still counts in full.
	public const int ActiveIpWeeks = 30;

	// Weeks without a release after which the game starts to fade an IP.
	public const int IpFadeWeeks = 250;

	// The IP's own record (its first game), or null.
	public static gameScript IpOf(gameScript g)
	{
		return IpLinks.Holder(g);
	}

	// IP points the game gives a sequel with this game's rating (gameScript.AddIpPointsRelease).
	public static float ReleaseIp(gameScript g)
	{
		if (!G.MS || g.reviewTotal < 40)
		{
			return 0f;
		}
		return (g.developerID == G.MS.myID) ? ((float)(g.reviewTotal * 2 / (G.MS.difficulty + 1))) : ((float)g.reviewTotal);
	}

	// Adds IP points without restarting the release clock and keeps the IP active. Returns the points added.
	public static float KeepIpAlive(gameScript g, float points)
	{
		gameScript ip = IpOf(g);
		if (!ip)
		{
			return 0f;
		}
		if (ip.ipTime > ActiveIpWeeks)
		{
			ip.ipTime = ActiveIpWeeks;
		}
		if (points <= 0f)
		{
			return 0f;
		}
		ip.ipPunkte += points;
		return points;
	}

	// Enough players to keep the IP alive: a fifth of its best, or a top-10 place in the subscription charts.
	public static bool IpHealthy(gameScript g)
	{
		if (!g.isOnMarket || g.abonnements <= 0)
		{
			return false;
		}
		if (g.abonnements * 5 >= g.bestAbonnements)
		{
			return true;
		}
		int rank = ChartRank(g);
		if (rank > 0)
		{
			return rank <= 10;
		}
		return false;
	}

	// Share of a sequel's IP points a top chart place earns each month.
	public static float RankIpShare(int rank)
	{
		if (rank == 1)
		{
			return 0.06f;
		}
		if (rank == 2)
		{
			return 0.045f;
		}
		if (rank == 3)
		{
			return 0.035f;
		}
		if (rank >= 4 && rank <= 10)
		{
			return 0.02f;
		}
		return 0f;
	}

	// Share of a sequel's IP points a finished project earns.
	public static float ProjectIpShare(UpdateKind kind, bool migration)
	{
		return kind switch
		{
			UpdateKind.MmoPatch => 0.03f,
			UpdateKind.MmoClient64 => 0.03f,
			UpdateKind.MmoRating => 0.02f,
			UpdateKind.MmoGraphics => 0.06f,
			UpdateKind.MmoFeatures => 0.05f,
			UpdateKind.MmoPort => 0.05f,
			UpdateKind.MmoMigration => migration ? 0.08f : 0.04f,
			UpdateKind.MmoExpansion => 0.15f,
			_ => 0f,
		};
	}

	public static string IpStatus(gameScript g)
	{
		gameScript ip = IpOf(g);
		if (!ip)
		{
			return "none";
		}
		string points = Mathf.RoundToInt(ip.ipPunkte).ToString();
		if (IpHealthy(g))
		{
			return points + "  ·  active";
		}
		return points + "  ·  " + Mathf.Max(0, IpFadeWeeks + 1 - ip.ipTime) + " wks left";
	}

	// ---- Shared by all projects ----

	public static string Problem(UpdateKind kind, gameScript g)
	{
		if (!On)
		{
			return "MMO management is switched off in the addon's settings.";
		}
		if (g.gameTyp != 1)
		{
			return "Only MMOs can get this.";
		}
		UpdatePlan plan = kind switch
		{
			UpdateKind.MmoGraphics => PlanOverhaul(g),
			UpdateKind.MmoClient64 => PlanClient64(g),
			UpdateKind.MmoExpansion => PlanExpansion(g),
			UpdateKind.MmoPatch => PlanPatch(g),
			_ => null,
		};
		return plan?.Problem;
	}

	public static string Complete(UpdateProject p, gameScript g, GameExtras extras, string name)
	{
		MmoGame m = Get(g);
		string text;
		float ipShare = ProjectIpShare(p.Kind, migration: false);
		switch (p.Kind)
		{
		case UpdateKind.MmoGraphics:
		{
			bool capped;
			float gain = Mathf.Max(0f, OverhaulTarget(g, m, out capped) - g.points_grafik);
			g.points_grafik += gain;
			int interest = OverhaulInterest(g);
			g.AddMMOInteresse(interest);
			g.AddHype(5f);
			m.Renewal += 78;
			m.OverhaulReady = Now + OverhaulCooldown;
			m.Overhauls++;
			text = name + " has a fresh new look.\n\nGraphics +" + Mathf.RoundToInt(gain) + ", interest +" + interest + ", and it counts as 1.5 years younger for age wear.";
			break;
		}
		case UpdateKind.MmoClient64:
		{
			float tech = Mathf.Max(5f, g.points_technik * 0.1f);
			g.points_technik += tech;
			g.points_bugs *= 0.7f;
			m.Client64 = true;
			m.Client64Built = true;
			g.AddMMOInteresse(5f);
			m.Renewal += 26;
			text = name + " now runs a 64-bit client.\n\nTech +" + Mathf.RoundToInt(tech) + ", fewer bugs and crashes, a few more players stay, and graphics overhauls can go past 2007's standard.";
			break;
		}
		case UpdateKind.MmoMigration:
		{
			engineScript target = G.MS.arrayEnginesScripts?.FirstOrDefault((engineScript x) => (bool)x && x.myID == p.EngineId);
			if (!target)
			{
				return null;
			}
			bool migration = IsMigration(g, target);
			ipShare = ProjectIpShare(p.Kind, migration);
			int renewal = EngineRenewal(g, target, (p.EngineFeatures ?? new int[0]).Count((int f) => f != -1));
			int interest = EngineInterest(g, target);
			UpdatePlan plan = GameUpdates.ApplyEngine(p, g, extras, out engineScript e);
			if (plan == null)
			{
				return null;
			}
			g.AddMMOInteresse(interest);
			g.AddHype(migration ? 10f : 4f);
			m.Renewal += renewal;
			if (migration)
			{
				m.DipUntil = Now + 2;
				m.Migrations++;
			}
			extras.EngineUpdates++;
			string points = "Graphics +" + Mathf.RoundToInt(plan.Graphics) + ", sound +" + Mathf.RoundToInt(plan.Sound) + ", tech +" + Mathf.RoundToInt(plan.Tech) + ", interest +" + interest + ", and it counts as " + YoungerText(renewal) + " for age wear.";
			text = (migration ? (name + " has moved to " + e.GetName() + ".\n\n" + points + " Expect a small dip in players for two weeks while everyone gets used to it.") : (name + " runs on newer " + e.GetName() + " features.\n\n" + points));
			break;
		}
		case UpdateKind.MmoPort:
		{
			text = CompletePlatforms(p, g, m, extras, name);
			if (text == null)
			{
				return null;
			}
			break;
		}
		case UpdateKind.MmoFeatures:
		{
			List<int> fresh = (p.Features ?? new List<int>()).Where((int j) => j >= 0 && j < g.gameGameplayFeatures.Length && !g.gameGameplayFeatures[j]).ToList();
			SystemsFit(g, fresh, out int fit, out int poor);
			string done = GameUpdates.CompleteFeatures(p, g, extras, name);
			if (done == null)
			{
				return null;
			}
			int interest = SystemsInterest(fresh.Count, fit);
			int renewal = SystemsRenewal(fresh.Count);
			g.AddMMOInteresse(interest);
			m.Renewal += renewal;
			long lost = 0L;
			if (poor > 0)
			{
				lost = (long)((float)g.abonnements * PoorSystemLoss * (float)poor);
				g.abonnements -= lost;
			}
			text = done + "\n\nFor the MMO: interest +" + interest + ", and it counts as " + YoungerText(renewal) + " for age wear." + ((lost > 0) ? (" " + MmoUiText.Num(lost) + " players quit over systems that don't suit it.") : "");
			break;
		}
		case UpdateKind.MmoRating:
		{
			int steps = g.usk - p.Usk;
			string done = GameUpdates.CompleteRating(p, g, extras, name);
			if (done == null)
			{
				return null;
			}
			long lost = (long)((float)g.abonnements * RatingLossPerStep * (float)steps);
			g.abonnements -= lost;
			g.AddMMOInteresse(3 * steps);
			text = done + "\n\nFor the MMO: " + MmoUiText.Num(lost) + " subscribers left over the cut content, and the wider audience brings interest +" + 3 * steps + ".";
			break;
		}
		case UpdateKind.MmoExpansion:
		{
			int s = g.gameSize;
			if (s < 0 || s >= 5)
			{
				return null;
			}
			float gain = StepGain[s];
			g.points_gameplay *= 1f + gain;
			g.points_grafik *= 1f + gain;
			g.points_sound *= 1f + gain;
			g.points_technik *= 1f + gain;
			g.gameSize = s + 1;
			g.AddMMOInteresse(10f);
			g.AddHype(10f);
			m.Renewal += 52;
			m.ExpandReady = Now + StepWait[s + 1];
			m.Expansions++;
			text = name + " has grown from " + SizeName(s) + " to " + SizeName(s + 1) + ".\n\nAll points +" + Mathf.RoundToInt(gain * 100f) + "%, interest +10, hype +10, and it counts as a year younger for age wear." + ((StepWait[s + 1] > 0) ? (" The next step can start in " + InWeeks(StepWait[s + 1]) + ".") : "");
			break;
		}
		case UpdateKind.MmoPatch:
		{
			int interest = PatchInterest(g);
			g.points_gameplay *= 1.02f;
			g.AddMMOInteresse(interest);
			g.AddHype(3f);
			m.PatchUntil = Now + PatchWeeks;
			m.PatchReady = Now + PatchCooldown;
			m.Patches++;
			text = name + " has a new content patch.\n\nInterest +" + interest + ", and " + Mathf.RoundToInt(PatchChurn * 100f) + "% fewer players leave for the next " + PatchWeeks + " weeks.";
			break;
		}
		default:
			return null;
		}
		text += IpLine(g, KeepIpAlive(g, ReleaseIp(g) * ipShare));
		if ((bool)g.gameTab_)
		{
			g.gameTab_.UpdateData();
		}
		GameUpdates.Touch();
		return text;
	}

	private static string IpLine(gameScript g, float points)
	{
		gameScript ip = IpOf(g);
		if (!ip)
		{
			return "";
		}
		return "\n\nIts IP " + ((points >= 0.5f) ? ("gains " + Mathf.RoundToInt(points) + " points and ") : "") + "stays active (" + ip.GetNameSimple() + ": " + Mathf.RoundToInt(ip.ipPunkte) + " IP points).";
	}

	// New platforms: the four slots change as for a port, the rest are added as extra platforms.
	private static string CompletePlatforms(UpdateProject p, gameScript g, MmoGame m, GameExtras extras, string name)
	{
		if (p.Platforms == null || p.Platforms.Length != g.gamePlatform.Length)
		{
			return null;
		}
		List<int> now = AllPlatforms(g);
		List<int> picked = p.Platforms.Where((int id) => id != -1 && !g.gamePlatform.Contains(id)).Concat(p.ExtraPlatforms ?? new List<int>()).Where((int id) => !now.Contains(id)).Distinct().ToList();
		if (picked.Count == 0)
		{
			return null;
		}
		float boost = LaunchBoost(g, picked);
		List<int> mainBefore = g.gamePlatform.Where((int id) => id != -1).ToList();
		bool slotsChange = false;
		for (int i = 0; i < g.gamePlatform.Length; i++)
		{
			if (p.Platforms[i] != g.gamePlatform[i])
			{
				slotsChange = true;
			}
		}
		if (slotsChange)
		{
			GameUpdates.CompletePort(p, g, extras, name);
		}
		else
		{
			extras.Ports++;
		}
		foreach (int id in p.ExtraPlatforms ?? new List<int>())
		{
			platformScript ps = GameUpdates.Platform(id);
			if ((bool)ps && !g.gamePlatform.Contains(id) && !m.ExtraPlatforms.Contains(id))
			{
				m.ExtraPlatforms.Add(id);
				ps.games++;
				if (ps.ownerID != G.MS.myID)
				{
					g.herstellerExklusiv = false;
				}
			}
		}
		if (g.exklusiv && AllPlatforms(g).Count > 1)
		{
			g.exklusiv = false;
			foreach (int id2 in mainBefore)
			{
				platformScript old = GameUpdates.Platform(id2);
				if ((bool)old && old.exklusivGames > 0)
				{
					old.exklusivGames--;
				}
			}
		}
		extras.LaunchBoost = boost;
		extras.LaunchWeek = CopyProtectDev.AbsWeek;
		int interest = PortInterest(boost);
		g.AddMMOInteresse(interest);
		g.AddHype(5f);
		m.Renewal += 26;
		if ((bool)g.gameTab_)
		{
			g.gameTab_.UpdateData();
		}
		int total = AllPlatforms(g).Count((int id) => !Retired(id));
		return name + " is out on " + PlatformNames(picked) + ".\n\nIt's now on " + total + " platforms. New players get a launch boost of " + GameUpdates.Pct(boost) + " that fades over " + GameUpdates.LaunchWeeks + " weeks, interest +" + interest + ", and it counts as half a year younger for age wear. Make sure your servers have room for the new players.";
	}

	// The game's tooltip lists the four slots only; add the extra platforms.
	[HarmonyPostfix]
	[HarmonyPatch(typeof(gameScript), "GetTooltip")]
	private static void Tooltip(gameScript __instance, ref string __result)
	{
		try
		{
			MmoGame m = (On ? Find(__instance.myID) : null);
			if (m?.ExtraPlatforms != null && m.ExtraPlatforms.Count > 0)
			{
				__result = __result + "\n<color=#1565c0>Also on: " + PlatformNames(m.ExtraPlatforms) + "</color>";
			}
		}
		catch (Exception)
		{
		}
	}

	// ---- Old-school or modern ----

	public static int StanceWait(MmoGame m)
	{
		return WeeksLeft(m.StanceWeek + StanceWeeks);
	}

	public static string SetOldSchool(gameScript g, bool on)
	{
		MmoGame m = Get(g);
		if (m.OldSchool == on)
		{
			return null;
		}
		int wait = StanceWait(m);
		if (wait > 0)
		{
			return "Players need time to settle: you can change its style again in " + InWeeks(wait) + ".";
		}
		m.OldSchool = on;
		m.StanceWeek = Now;
		GameUpdates.Touch();
		return null;
	}

	// ---- Servers and the game's MMO addon ----

	public static void ServerLoad(out long used, out long capacity)
	{
		used = 0L;
		capacity = 0L;
		roomScript[] rooms = (G.MS ? G.MS.arrayRoomScripts : null);
		if (rooms == null)
		{
			return;
		}
		foreach (roomScript r in rooms)
		{
			if ((bool)r && r.typ == 15 && (r.serverReservieren == 0 || r.serverReservieren == 1))
			{
				capacity += r.GetServerplatz();
				used += r.serverplatzUsed;
			}
		}
	}

	// Puts players on your MMO servers; returns how many fit.
	private static long PlaceOnServers(long players)
	{
		long left = players;
		roomScript[] rooms = (G.MS ? G.MS.arrayRoomScripts : null);
		if (rooms == null)
		{
			return 0L;
		}
		foreach (roomScript r in rooms)
		{
			if (left <= 0)
			{
				break;
			}
			if ((bool)r && r.typ == 15 && (r.serverReservieren == 0 || r.serverReservieren == 1))
			{
				left = r.SetAbos(left);
			}
		}
		return players - left;
	}

	public static string ExpansionPackProblem(gameScript g)
	{
		unlockScript u = (G.MainObj ? G.MainObj.GetComponent<unlockScript>() : null);
		if ((bool)u && !u.unlock[21])
		{
			return "MMO addons aren't available yet.";
		}
		if (g.pubOffer || g.handy || g.arcade || (!g.typ_standard && !g.typ_nachfolger && !g.typ_spinoff))
		{
			return "The game doesn't take MMO addons for this MMO.";
		}
		return null;
	}

	// Opens the game's own "MMO addon" development screen for this game in that room.
	public static string OpenExpansionPack(gameScript g, roomScript room)
	{
		string problem = ExpansionPackProblem(g) ?? GameUpdates.RoomProblem(room);
		if (problem != null)
		{
			return problem;
		}
		GUI_Main gui = G.Gui;
		GameObject menu = (((bool)gui && gui.uiObjects != null && gui.uiObjects.Length > 247) ? gui.uiObjects[247] : null);
		Menu_Dev_MMOAddon addon = (menu ? menu.GetComponent<Menu_Dev_MMOAddon>() : null);
		if (!addon)
		{
			return "The game's MMO addon screen couldn't be found.";
		}
		gui.OpenMenu(hideChars: false);
		UI.MmoAddonScreen.FromGame = g.myID;
		gui.ActivateMenu(menu);
		addon.Init(room, g);
		return null;
	}

	// The game's own free update (Menu_Dev_Update) for this MMO, in that room. Same rules as the game's update list.
	public static string FreeUpdateProblem(gameScript g)
	{
		if (!g || !g.isOnMarket || g.inDevelopment || g.ownerID != G.MS.myID || g.developerID != G.MS.myID || IpLinks.Archived(g))
		{
			return "Only your own MMOs on the market can get a free update.";
		}
		UpdateProject running = GameUpdates.ProjectFor(g.myID);
		if (running != null)
		{
			return "Your team is already working on " + running.Title + ".";
		}
		return null;
	}

	public static string OpenFreeUpdate(gameScript g, roomScript room)
	{
		string problem = FreeUpdateProblem(g) ?? GameUpdates.RoomProblem(room);
		if (problem != null)
		{
			return problem;
		}
		GUI_Main gui = G.Gui;
		GameObject menu = (((bool)gui && gui.uiObjects != null && gui.uiObjects.Length > 108) ? gui.uiObjects[108] : null);
		Menu_Dev_Update update = (menu ? menu.GetComponent<Menu_Dev_Update>() : null);
		if (!update)
		{
			return "The game's update screen couldn't be found.";
		}
		gui.OpenMenu(hideChars: false);
		MmoAddonScreen.UpdateFromGame = g.myID;
		gui.ActivateMenu(menu);
		update.Init(room, g);
		return null;
	}

	// ---- Weekly ----

	private struct Before
	{
		public bool Ok;

		public long Abos;

		public float Hype;

		public int Review;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(gameScript), "SellGame")]
	private static void SellBefore(gameScript __instance, out Before __state)
	{
		__state = default(Before);
		gameScript g = __instance;
		if (On && g.gameTyp == 1 && g.isOnMarket && g.releaseDate <= 0 && !g.typ_mmoaddon && g.IsMyGame())
		{
			__state = new Before
			{
				Ok = true,
				Abos = g.abonnements,
				Hype = g.hype,
				Review = g.reviewTotal
			};
		}
	}

	// The game's weekly churn is (subscribers x (1 - rating/102) x 0.25 + weeks on the market), more with low hype.
	// On top of that: dated or old-school MMOs, patches and the 64-bit client change the first part, and renewal
	// gives back the part that comes from age.
	[HarmonyPostfix]
	[HarmonyPatch(typeof(gameScript), "SellGame")]
	private static void SellAfter(gameScript __instance, Before __state)
	{
		if (!__state.Ok)
		{
			return;
		}
		try
		{
			gameScript g = __instance;
			MmoGame m = Get(g);
			float hypeFactor = 1f + (100f - Mathf.Clamp(__state.Hype, 0f, 100f)) * 0.01f;
			float share = Mathf.Max(0f, ((float)__state.Abos - (float)__state.Abos / 102f * (float)__state.Review) * 0.25f) * hypeFactor;
			float keep = (0f - share) * ChurnExtra(g, m);
			if (m.Renewal > 0)
			{
				keep += (float)Mathf.Min(m.Renewal, g.weeksOnMarket) * hypeFactor;
			}
			if (m.DipUntil > Now)
			{
				keep -= (float)g.abonnements * 0.02f;
			}
			long change = (long)Mathf.Round(keep);
			if (change < 0)
			{
				long loss = Math.Min(-change, g.abonnements);
				g.abonnements -= loss;
				g.abonnementsWoche -= loss;
			}
			else if (change > 0)
			{
				long gain = PlaceOnServers(Math.Min(change, Math.Max(0L, g.sellsTotal - g.abonnements)));
				g.abonnements += gain;
				g.abonnementsWoche += gain;
				if (g.bestAbonnements < g.abonnements)
				{
					g.bestAbonnements = g.abonnements;
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("MMO weekly players: " + ex);
		}
	}

	// Renewal: a modernised MMO counts as younger for the game's age wear (which caps its interest).
	[HarmonyPostfix]
	[HarmonyPatch(typeof(gameScript), "GetMMOAbnutzung")]
	private static void Wear(gameScript __instance, ref float __result)
	{
		if (__result <= 0f || !On)
		{
			return;
		}
		MmoGame m = Find(__instance.myID);
		if (m != null && m.Renewal > 0)
		{
			int age = __instance.weeksOnMarket - m.Renewal;
			__result = ((age > 52) ? ((float)(age - 51) / 15f) : 0f);
		}
	}

	// ---- MMO addons (expansion packs) ----

	// An addon for a normal game lifts its sales for good (bonusSellsAddons, less with each addon), while the game
	// gives an MMO addon only a jump in interest and 5% of a sequel's IP points. With MMO management on, your MMO
	// addons also add half a normal addon's lasting sales bonus to the MMO, up to 3% more points (new content),
	// half a year off its age for wear, and IP points as for a normal addon (25% of a sequel's).
	public const float AddonBonusShare = 0.5f;

	public const float AddonPoints = 0.03f;

	public const int AddonRenewal = 26;

	public const float AddonIpShare = 0.2f;

	[HarmonyPrefix]
	[HarmonyPatch(typeof(gameScript), "SetOnMarket")]
	private static void AddonBefore(gameScript __instance, out int __state)
	{
		__state = -1;
		try
		{
			if (On && __instance.typ_mmoaddon)
			{
				gameScript ip = IpOf(__instance);
				__state = (ip ? ip.ipTime : (-1));
			}
		}
		catch (Exception)
		{
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(gameScript), "SetOnMarket")]
	private static void AddonReleased(gameScript __instance, int __state)
	{
		if (!On || !__instance.typ_mmoaddon || !__instance.IsMyGame())
		{
			return;
		}
		try
		{
			gameScript addon = __instance;
			gameScript b = addon.FindVorgaengerScript();
			if (!b || b.gameTyp != 1 || !IsMyMmo(b))
			{
				return;
			}
			int review = Mathf.Clamp(addon.reviewTotal, 0, 100);
			b.bonusSellsAddons += (float)review * 0.01f * AddonBonusShare / (float)Mathf.Max(1, b.amountMMOAddons);
			float bump = 1f + AddonPoints * (float)review / 100f;
			b.points_gameplay *= bump;
			b.points_grafik *= bump;
			b.points_sound *= bump;
			b.points_technik *= bump;
			MmoGame m = Get(b);
			m.Renewal += AddonRenewal;
			// As the game counts IP points for releases: less if the IP had another release in the last 8 weeks.
			gameScript ipGame = IpOf(addon);
			float ip = ReleaseIp(addon) * AddonIpShare;
			if ((bool)ipGame && __state >= 0 && __state < 8 && ipGame.developerID == G.MS.myID)
			{
				ip *= 0.2f;
			}
			if (ip > 0f && (bool)ipGame)
			{
				addon.AddIpPoints(ip);
			}
			if ((bool)b.gameTab_)
			{
				b.gameTab_.UpdateData();
			}
			GameUpdates.Touch();
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("MMO addon release: " + ex);
		}
	}

	public static string AddonEffects()
	{
		return "With MMO Management, an expansion pack also lifts the MMO for good: up to " + Mathf.RoundToInt(AddonBonusShare * 100f) + "% more new players at a 100% rating (less with each pack), up to " + Mathf.RoundToInt(AddonPoints * 100f) + "% more points, half a year younger for age wear, and IP points as for a normal addon.";
	}

	public static float AgeYears(gameScript g)
	{
		return (float)g.weeksOnMarket / 52f;
	}

	public static float WearAgeYears(gameScript g, MmoGame m)
	{
		return (float)Mathf.Max(0, g.weeksOnMarket - m.Renewal) / 52f;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(mainScript), "WochenUpdates")]
	private static void Weekly()
	{
		if (!On)
		{
			return;
		}
		try
		{
			foreach (gameScript g in MyMmos())
			{
				MmoGame m = Get(g);
				m.History.Add(g.abonnements);
				m.HistoryWeek = Now;
				if (m.History.Count > HistoryWeeks)
				{
					m.History.RemoveRange(0, m.History.Count - HistoryWeeks);
				}
				// The card redrew its bars during the week's sales, before this week's entry: redraw them.
				if ((bool)g.gameTab_)
				{
					g.gameTab_.UpdateData();
				}
				if (IpHealthy(g))
				{
					// A top chart place earns IP points once a month.
					float points = 0f;
					if (m.IpMonth != NowMonth)
					{
						m.IpMonth = NowMonth;
						points = ReleaseIp(g) * RankIpShare(ChartRank(g));
					}
					KeepIpAlive(g, points);
				}
			}
			GameUpdates.Touch();
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("MMO weekly record: " + ex);
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
			Plugin.Log.LogError("Saving MMOs failed: " + ex);
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(savegameScript), "Load")]
	private static void BeforeLoad()
	{
		S = new MmoState();
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(savegameScript), "LoadHistory")]
	private static void Load(ES3File ___es3file)
	{
		try
		{
			S = ((___es3file != null && ___es3file.KeyExists(SaveKey)) ? JsonConvert.DeserializeObject<MmoState>(___es3file.Load<string>(SaveKey)) : null) ?? new MmoState();
			S.Games = S.Games ?? new Dictionary<int, MmoGame>();
			S.AwardBase = S.AwardBase ?? new Dictionary<int, AwardBase>();
			S.Wins = S.Wins ?? new List<AwardWin>();
			if (S.Games.Count > 0)
			{
				Plugin.Log.LogInfo("MMOs loaded: " + S.Games.Count);
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Loading MMOs failed: " + ex);
			S = new MmoState();
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(mainScript), "InitNewGame")]
	private static void NewGame()
	{
		S = new MmoState();
	}
}
