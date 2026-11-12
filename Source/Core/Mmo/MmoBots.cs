using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HolyAddon.Core.CopyProtection;
using HolyAddon.Core.Performance;
using HolyAddon.Core.Updates;
using UnityEngine;

namespace HolyAddon.Core.Mmo;

// A rival studio's anti-bot system you could license.
internal class AntiBotOffer
{
	public publisherScript Studio;

	public string Name;

	public int Genre;

	public int Tech;

	public float Strength;

	public long Price;

	public long Fee;
}

// Bots in MMOs, and the anti-bot systems that keep them out.
//
// Bots are gold farmers and levelling services. Most of them pay for their accounts like anyone else, so they're
// counted among an MMO's subscribers; with a free trial running, more come in on free accounts that pay nothing.
// They drive real players away (more of them leave each week, fewer join, user reviews sour). Updates break their
// programs (as an update breaks pirated copies), a ban wave closes most of them, and an anti-bot system keeps them
// down for good: the better it is, the fewer get in, and at full strength with the latest technology there are
// hardly any.
//
// Anti-bot systems belong to the whole studio, but each is built for one genre: it works fully on MMOs of that
// genre, partly on MMOs whose second genre it is, and only a little on the rest. An MMO's own anti-cheat gives it a
// head start. Rival studios' MMOs have bots too, and their own anti-bot systems: you can license yours to them, or
// license theirs.
[HarmonyPatch]
internal static class MmoBots
{
	public static readonly string[] TechNames = new string[5] { "Pattern detection", "Behaviour analysis", "Client integrity checks", "Machine learning", "Hardware bans" };

	public static readonly string[] TechInfo = new string[5]
	{
		"Server logs are scanned for players who repeat the same moves for hours on end.",
		"Watches how each account plays (routes, reaction times, trading) and flags the ones that play like a script.",
		"The game client checks itself and the computer for bot programs and memory editors.",
		"A trained model spots new kinds of bots from their play, without waiting for a rule to be written.",
		"Banned players lose the machine, not just the account: new accounts on the same hardware are closed at once."
	};

	public static readonly int[] TechYear = new int[5] { 1997, 2003, 2008, 2014, 2020 };

	// How much of the bots a system at full strength keeps out, by technology.
	public static readonly float[] TechPower = new float[5] { 0.6f, 0.72f, 0.83f, 0.92f, 1f };

	// Strength lost each week while it's in use, as bot makers find their way around it (newer is harder to beat).
	private static readonly float[] Decay = new float[5] { 1.1f, 0.95f, 0.8f, 0.65f, 0.55f };

	public static readonly float[] FeeSteps = new float[5] { 0.5f, 0.75f, 1f, 1.5f, 2f };

	public static readonly string[] LicensingNames = new string[2] { "Your MMOs only", "Sell licences to studios" };

	private static readonly string[] NameA = new string[10] { "Bot", "Farm", "Grind", "Gold", "Macro", "Script", "Watch", "Iron", "Clear", "True" };

	private static readonly string[] NameB = new string[10] { "Warden", "Sentinel", "Guard", "Bastion", "Aegis", "Lookout", "Shield", "Hunter", "Patrol", "Keeper" };

	// Share of subscribers that would be bots with no protection at all, in an MMO's prime years.
	public const float BaseShare = 0.08f;

	// Free trial bots, relative to the paying ones.
	public const float TrialShare = 0.6f;

	public const float UpdateCut = 0.2f;

	public const float AddonCut = 0.15f;

	public const float FreeUpdateCut = 0.25f;

	public const int BustCooldown = 8;

	public const int DeterWeeks = 12;

	// The most an MMO's anti-cheat keeps out on its own.
	public const float AntiCheatHeadStart = 0.35f;

	public const float WeakStrength = 40f;

	public const int UpdateCooldown = 8;

	// Vendors patch the systems they license out this often.
	public const int VendorUpdateWeeks = 26;

	private static MmoState S => MmoOps.State;

	public static bool On
	{
		get
		{
			if (MmoOps.On)
			{
				return Plugin.MmoBots.Value;
			}
			return false;
		}
	}

	private static int Now => MmoOps.Now;

	private static int Year => CopyProtectDev.Year;

	public static IReadOnlyList<AntiBotSystem> Systems => S.AntiBots;

	// ---- Technology ----

	public static string TechName(int t)
	{
		return TechNames[Mathf.Clamp(t, 0, TechNames.Length - 1)];
	}

	public static bool TechUnlocked(int t)
	{
		return Year >= TechYear[Mathf.Clamp(t, 0, TechYear.Length - 1)];
	}

	// The newest technology available, or -1 before the first one (1997).
	public static int BestTech()
	{
		int best = -1;
		for (int i = 0; i < TechYear.Length; i++)
		{
			if (TechUnlocked(i))
			{
				best = i;
			}
		}
		return best;
	}

	// Prices grow with the years.
	public static float Inflation()
	{
		return 1f + (float)Mathf.Max(0, Year - 1997) * 0.05f;
	}

	// Botting grows with the years: few bots before the late 90s.
	public static float Era()
	{
		return Mathf.Clamp01(0.35f + (float)(Year - 1993) * 0.08f);
	}

	public static string GenreName(int genre)
	{
		genres gen = (G.MainObj ? G.MainObj.GetComponent<genres>() : null);
		if (!gen || genre < 0)
		{
			return "?";
		}
		return gen.GetName(genre);
	}

	// How well a system built for one genre works on this MMO.
	public static float GenreFit(int systemGenre, gameScript g)
	{
		if (systemGenre == g.maingenre)
		{
			return 1f;
		}
		if (systemGenre == g.subgenre)
		{
			return 0.6f;
		}
		return 0.3f;
	}

	public static string FitName(float fit)
	{
		if (fit >= 0.99f)
		{
			return "built for its genre";
		}
		if (fit >= 0.59f)
		{
			return "built for its second genre";
		}
		return "built for another genre";
	}

	public static float SystemPower(AntiBotSystem a)
	{
		if (a == null || a.Retired)
		{
			return 0f;
		}
		return TechPower[Mathf.Clamp(a.Tech, 0, TechPower.Length - 1)] * Mathf.Clamp01(a.Strength / 100f);
	}

	// ---- Protection ----

	// The head start from the MMO's own anti-cheat (it only works with multiplayer, as the game counts it).
	public static float AntiCheatPower(gameScript g)
	{
		if (!g || g.gameAntiCheat == -1 || g.gameplayFeatures_DevDone == null || g.gameplayFeatures_DevDone.Length <= 23 || (!g.gameplayFeatures_DevDone[21] && !g.gameplayFeatures_DevDone[23]))
		{
			return 0f;
		}
		antiCheatScript ac = g.gameAntiCheatScript_;
		if (!ac)
		{
			GameObject go = SceneLookup.Find("ANTICHEAT_" + g.gameAntiCheat);
			ac = (go ? go.GetComponent<antiCheatScript>() : null);
		}
		if (!ac)
		{
			return 0f;
		}
		return AntiCheatHeadStart * Mathf.Clamp01(ac.effekt / 100f);
	}

	// Your system that works best on this MMO (own or licensed), and how well.
	public static AntiBotSystem BestSystem(gameScript g, out float power)
	{
		power = 0f;
		AntiBotSystem best = null;
		foreach (AntiBotSystem a in S.AntiBots)
		{
			float p = SystemPower(a) * GenreFit(a.Genre, g);
			if (p > power)
			{
				power = p;
				best = a;
			}
		}
		return best;
	}

	private static float Combine(float a, float b)
	{
		return 1f - (1f - Mathf.Clamp01(a)) * (1f - Mathf.Clamp01(b));
	}

	// How much of the bots your MMO keeps out (0-1).
	public static float Protection(gameScript g)
	{
		BestSystem(g, out float power);
		return Combine(AntiCheatPower(g), power);
	}

	public static string ProtectionText(gameScript g)
	{
		AntiBotSystem a = BestSystem(g, out float power);
		float ac = AntiCheatPower(g);
		string text = Mathf.RoundToInt(Combine(ac, power) * 100f) + "%";
		List<string> parts = new List<string>();
		if (a != null && power > 0.001f)
		{
			parts.Add(a.Name);
		}
		if (ac > 0.001f)
		{
			parts.Add("anti-cheat");
		}
		if (parts.Count == 0)
		{
			return text + " (none)";
		}
		return text + " (" + string.Join(" + ", parts) + ")";
	}

	// A rival studio's own anti-bot system: the latest technology (one behind for weaker studios), kept fairly strong.
	public static int VendorTech(publisherScript s)
	{
		int best = BestTech();
		if (best < 0 || !s)
		{
			return -1;
		}
		return Mathf.Max(0, best - ((Stars(s) < 0.5f) ? 1 : 0));
	}

	private static float Stars(publisherScript s)
	{
		return Mathf.Clamp01(s.stars / 100f);
	}

	public static float NpcPower(publisherScript s)
	{
		int t = VendorTech(s);
		if (t < 0)
		{
			return 0f;
		}
		return TechPower[t] * (0.55f + 0.35f * Stars(s));
	}

	// A rival MMO's protection: its studio's own system, or one of yours it licenses, plus its anti-cheat.
	public static float RivalProtection(gameScript g, publisherScript studio)
	{
		float own = (studio ? NpcPower(studio) : 0f);
		if ((bool)studio)
		{
			foreach (AntiBotSystem a in S.AntiBots)
			{
				if (a.Source == -1 && a.Licensees.Contains(studio.myID))
				{
					own = Mathf.Max(own, SystemPower(a) * GenreFit(a.Genre, g));
				}
			}
		}
		return Combine(AntiCheatPower(g), own);
	}

	// Bots as a share of all accounts this MMO would settle at.
	public static float TargetShare(gameScript g, float protection, bool deterred)
	{
		float size = 0.6f + 0.4f * Mathf.Clamp01(Mathf.Log10(Mathf.Max(10f, g.abonnements)) / 6f);
		return BaseShare * Era() * size * (1f - 0.95f * Mathf.Clamp01(protection)) * (deterred ? 0.5f : 1f);
	}

	// Bots (paying and free trial) as a share of all accounts.
	public static float Share(gameScript g, MmoGame m)
	{
		if (m == null || !g)
		{
			return 0f;
		}
		return (float)(m.Bots + m.TrialBots) / (float)Math.Max(1L, g.abonnements + m.TrialBots);
	}

	public static long Total(MmoGame m)
	{
		if (m != null)
		{
			return m.Bots + m.TrialBots;
		}
		return 0L;
	}

	public static long RivalBots(gameScript g)
	{
		if (!g || !S.RivalBots.TryGetValue(g.myID, out var n))
		{
			return 0L;
		}
		return n;
	}

	// Extra share of the game's weekly churn from bots.
	public static float ChurnFromBots(gameScript g, MmoGame m)
	{
		if (!On)
		{
			return 0f;
		}
		return Mathf.Min(0.4f, Share(g, m) * 3f);
	}

	// Fewer new players join a game full of bots.
	public static float JoinFactor(gameScript g, MmoGame m)
	{
		if (!On)
		{
			return 1f;
		}
		return 1f - Mathf.Min(0.3f, Share(g, m) * 2f);
	}

	// ---- Removing bots ----

	// An update or addon breaks the bots' programs: that share of them stops working (paying ones cancel).
	public static long BreakBots(gameScript g, MmoGame m, float cut)
	{
		if (!On || m == null || !g)
		{
			return 0L;
		}
		long paying = (long)((float)m.Bots * cut);
		long trial = (long)((float)m.TrialBots * cut);
		m.Bots -= paying;
		m.TrialBots -= trial;
		g.abonnements = Math.Max(0L, g.abonnements - paying);
		return paying + trial;
	}

	public static string UpdateLine(long broken)
	{
		if (broken <= 0)
		{
			return "";
		}
		return "\n\nThe new code broke the bots' programs: " + MmoUiText.Num(broken) + " bot accounts stopped working.";
	}

	// A ban wave: most bots go, more with a strong anti-bot system. Returns the accounts closed.
	public static long BanWave(gameScript g, MmoGame m, out long paying, out float share)
	{
		share = Mathf.Min(0.95f, 0.6f + 0.3f * Protection(g));
		paying = (long)((float)m.Bots * share);
		long trial = (long)((float)m.TrialBots * share);
		m.Bots -= paying;
		m.TrialBots -= trial;
		g.abonnements = Math.Max(0L, g.abonnements - paying);
		g.abonnementsWoche -= paying;
		m.Banned += paying + trial;
		return paying + trial;
	}

	// The game's own free update breaks bots too.
	[HarmonyPostfix]
	[HarmonyPatch(typeof(taskUpdate), "Complete")]
	private static void FreeUpdate(taskUpdate __instance)
	{
		try
		{
			gameScript g = __instance.gS_;
			if (On && (bool)g && g.gameTyp == 1 && MmoOps.IsMyMmo(g))
			{
				MmoGame m = MmoOps.Find(g.myID);
				long broken = BreakBots(g, m, FreeUpdateCut);
				if (broken > 0 && (bool)G.Gui)
				{
					G.Gui.CreateTopNewsInfo("The update for " + g.GetNameSimple() + " broke the bots' programs: " + MmoUiText.Num(broken) + " bot accounts stopped working.");
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Bots after a free update: " + ex.Message);
		}
	}

	// ---- Weekly ----

	[HarmonyPostfix]
	[HarmonyPatch(typeof(mainScript), "WochenUpdates")]
	private static void Weekly(mainScript __instance)
	{
		if (!On)
		{
			return;
		}
		try
		{
			bool monthly = __instance.week == 1;
			Dictionary<int, publisherScript> studios = Studios();
			StepSystems(monthly, studios);
			gameScript[] all = (__instance.games_ ? __instance.games_.arrayGamesScripts : null);
			if (all == null)
			{
				return;
			}
			HashSet<int> live = new HashSet<int>();
			foreach (gameScript g in all)
			{
				if (!g || g.gameTyp != 1 || g.typ_mmoaddon || !g.isOnMarket || g.inDevelopment)
				{
					continue;
				}
				if (MmoOps.IsMyMmo(g))
				{
					StepOwn(g, MmoOps.Get(g), monthly);
				}
				else if (!g.IsMyGame())
				{
					live.Add(g.myID);
					studios.TryGetValue(g.developerID, out var studio);
					StepRival(g, studio);
				}
			}
			foreach (int id in S.RivalBots.Keys.Where((int id) => !live.Contains(id)).ToList())
			{
				S.RivalBots.Remove(id);
			}
			GameUpdates.Touch();
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("MMO bots weekly: " + ex);
		}
	}

	// Bots come in a little each week until they reach what the protection lets through; above that, the anti-bot
	// system catches and bans some each week (more the stronger it is).
	private static void StepOwn(gameScript g, MmoGame m, bool monthly)
	{
		float protection = Protection(g);
		bool deterred = m.DeterUntil > Now;
		long subs = g.abonnements;
		float share = TargetShare(g, protection, deterred);
		m.Bots = Math.Max(0L, Math.Min(m.Bots, subs));
		long target = (long)((float)subs * share);
		if (m.Bots < target)
		{
			m.Bots += Math.Max(1L, (long)((float)(target - m.Bots) * 0.1f));
		}
		else if (m.Bots > target)
		{
			long banned = (long)((float)(m.Bots - target) * (0.08f + 0.25f * protection));
			m.Bots -= banned;
			g.abonnements = Math.Max(0L, g.abonnements - banned);
			g.abonnementsWoche -= banned;
			m.Banned += banned;
		}
		long trialTarget = (MmoLive.TrialActive(m) ? ((long)((float)subs * share * TrialShare)) : 0);
		if (m.TrialBots < trialTarget)
		{
			m.TrialBots += Math.Max(1L, (long)((float)(trialTarget - m.TrialBots) * 0.15f));
		}
		else if (m.TrialBots > trialTarget)
		{
			// Trial accounts run out quickly.
			m.TrialBots -= Math.Max(1L, (long)((float)(m.TrialBots - trialTarget) * (0.3f + 0.3f * protection)));
		}
		m.TrialBots = Math.Max(0L, m.TrialBots);
		if (monthly)
		{
			float s = Share(g, m);
			if (s > 0.03f)
			{
				HolyCompat.ShiftUserReviews(g, 0f - Mathf.Min(0.01f, s * 0.1f));
			}
		}
	}

	// Rival MMOs: the same bots, held back by their studio's system (or yours, licensed). Bots cost them players.
	private static void StepRival(gameScript g, publisherScript studio)
	{
		long subs = g.abonnements;
		if (subs <= 0)
		{
			S.RivalBots.Remove(g.myID);
			return;
		}
		float protection = RivalProtection(g, studio);
		long bots = Math.Min(RivalBots(g), subs);
		long target = (long)((float)subs * TargetShare(g, protection, deterred: false));
		if (bots < target)
		{
			bots += Math.Max(1L, (long)((float)(target - bots) * 0.1f));
		}
		else if (bots > target)
		{
			long banned = (long)((float)(bots - target) * (0.08f + 0.25f * protection));
			bots -= banned;
			g.abonnements = Math.Max(0L, g.abonnements - banned);
		}
		float share = (float)bots / (float)Math.Max(1L, g.abonnements);
		long lost = (long)((float)g.abonnements * share * 0.015f);
		g.abonnements = Math.Max(0L, g.abonnements - lost);
		S.RivalBots[g.myID] = Math.Max(0L, Math.Min(bots, g.abonnements));
	}

	// ---- Your systems ----

	public static AntiBotSystem Find(int id)
	{
		return S.AntiBots.FirstOrDefault((AntiBotSystem a) => a.Id == id);
	}

	public static List<gameScript> MyMmosUsing(AntiBotSystem a)
	{
		return MmoOps.MyMmos().Where((gameScript g) => BestSystem(g, out float _) == a).ToList();
	}

	public static float ReleaseStrength(roomScript room)
	{
		return Mathf.Clamp(100f - CopyProtectDev.TeamPenalty(room), 1f, 100f);
	}

	public static long BaseFee(int tech)
	{
		return (long)Mathf.Round((4000f + 2500f * (float)tech) * Inflation() / 100f) * 100;
	}

	public static long LicenceFee(AntiBotSystem a)
	{
		return (long)Mathf.Round((float)BaseFee(a.Tech) * FeeSteps[Mathf.Clamp(a.FeeStep, 0, FeeSteps.Length - 1)] / 100f) * 100;
	}

	public static float WeeklyWear(AntiBotSystem a)
	{
		if (a.Retired || a.Source != -1)
		{
			return 0f;
		}
		bool used = a.Licensees.Count > 0 || MmoOps.MyMmos().Any((gameScript g) => GenreFit(a.Genre, g) >= 0.99f);
		return Decay[Mathf.Clamp(a.Tech, 0, Decay.Length - 1)] * (used ? 1f : 0.3f) * Mathf.Min(2f, 1f + 0.08f * (float)a.Licensees.Count);
	}

	public static string RandomName()
	{
		for (int i = 0; i < 20; i++)
		{
			string name = NameA[UnityEngine.Random.Range(0, NameA.Length)] + NameB[UnityEngine.Random.Range(0, NameB.Length)];
			if (!S.AntiBots.Any((AntiBotSystem a) => a.Name == name))
			{
				return name;
			}
		}
		return "BotWarden " + (S.AntiBots.Count + 1);
	}

	public static UpdatePlan PlanNew(int tech, int genre)
	{
		UpdatePlan plan = new UpdatePlan();
		if (BestTech() < 0)
		{
			plan.Problem = "Anti-bot technology arrives in " + TechYear[0] + " (" + TechNames[0].ToLower() + ").";
			return plan;
		}
		if (!TechUnlocked(tech))
		{
			plan.Problem = TechName(tech) + " arrives in " + TechYear[Mathf.Clamp(tech, 0, TechYear.Length - 1)] + ".";
			return plan;
		}
		if (genre < 0)
		{
			plan.Problem = "Pick the genre it's built for.";
			return plan;
		}
		plan.Work = 180f * (1f + 0.45f * (float)tech);
		plan.Cost = (long)Mathf.Round(40000f * (1f + 0.6f * (float)tech) * Inflation() / 1000f) * 1000;
		return plan;
	}

	public static int WeeksToUpdate(AntiBotSystem a)
	{
		return Mathf.Max(0, a.Updated + UpdateCooldown - Now);
	}

	public static UpdatePlan PlanUpdate(AntiBotSystem a)
	{
		UpdatePlan plan = new UpdatePlan();
		if (a == null)
		{
			plan.Problem = "That system is gone.";
			return plan;
		}
		if (a.Source != -1)
		{
			plan.Problem = "It's licensed: its studio keeps it up to date.";
			return plan;
		}
		if (a.Retired)
		{
			plan.Problem = "It's retired.";
			return plan;
		}
		if (GameUpdates.Projects.Any((UpdateProject p) => p.Kind == UpdateKind.AntiBotUpdate && p.SystemId == a.Id))
		{
			plan.Problem = "An update for it is already in development.";
			return plan;
		}
		int wait = WeeksToUpdate(a);
		if (wait > 0)
		{
			plan.Problem = "The last update is too recent: the next one can start in " + ((wait == 1) ? "1 week" : (wait + " weeks")) + ".";
			return plan;
		}
		if (a.Strength >= 95f)
		{
			plan.Problem = "It's at full strength.";
			return plan;
		}
		UpdatePlan full = PlanNew(a.Tech, a.Genre);
		plan.Work = full.Work * 0.35f;
		plan.Cost = (long)Mathf.Round((float)full.Cost * 0.35f / 1000f) * 1000;
		return plan;
	}

	public static string ProjectProblem(UpdateProject p)
	{
		if (!On)
		{
			return "Bots are switched off in the addon's settings.";
		}
		if (p.Kind == UpdateKind.AntiBotUpdate)
		{
			return PlanUpdate(Find(p.SystemId)).Problem;
		}
		return PlanNew(p.Tech, p.Genre).Problem;
	}

	public static string Complete(UpdateProject p, roomScript room)
	{
		float strength = ReleaseStrength(room);
		if (p.Kind == UpdateKind.AntiBotBuild)
		{
			AntiBotSystem a = new AntiBotSystem
			{
				Id = S.NextAntiBotId++,
				Name = (string.IsNullOrEmpty(p.NewName) ? RandomName() : p.NewName),
				Genre = p.Genre,
				Tech = p.Tech,
				Strength = strength,
				Built = Now,
				Updated = Now
			};
			S.AntiBots.Add(a);
			long caught = Sweep(a);
			GameUpdates.Touch();
			return "<b>" + a.Name + "</b> is ready: " + TechName(a.Tech).ToLower() + " against bots in " + GenreName(a.Genre) + " MMOs, at " + Mathf.RoundToInt(strength) + "% strength.\n\nIt protects every MMO of yours it suits: fully in its genre, partly where " + GenreName(a.Genre) + " is the second genre, a little elsewhere." + ((caught > 0) ? (" Its first sweep closed " + MmoUiText.Num(caught) + " bot accounts.") : "") + " Keep it strong with updates, or sell licences to other studios, under Anti-bot systems in MMO Management.";
		}
		AntiBotSystem s = Find(p.SystemId);
		if (s == null)
		{
			return null;
		}
		s.Strength = Mathf.Max(s.Strength, strength);
		s.Updated = Now;
		s.Updates++;
		long swept = Sweep(s);
		GameUpdates.Touch();
		return "<b>" + s.Name + "</b> is updated: strength " + Mathf.RoundToInt(s.Strength) + "%." + ((swept > 0) ? (" The update caught " + MmoUiText.Num(swept) + " bot accounts that had slipped through.") : "") + ((s.Licensees.Count > 0) ? (" The " + s.Licensees.Count + ((s.Licensees.Count == 1) ? " studio" : " studios") + " licensing it get the update too.") : "");
	}

	// A new or updated system closes some of the bots in the MMOs it now protects best.
	private static long Sweep(AntiBotSystem a)
	{
		if (!On)
		{
			return 0L;
		}
		long total = 0L;
		foreach (gameScript g in MmoOps.MyMmos())
		{
			if (BestSystem(g, out float _) == a)
			{
				total += BreakBots(g, MmoOps.Get(g), 0.15f * GenreFit(a.Genre, g));
			}
		}
		return total;
	}

	public static string SetLicensing(AntiBotSystem a, int mode)
	{
		if (a == null || a.Source != -1 || a.Licensing == mode)
		{
			return null;
		}
		a.Licensing = mode;
		if (mode == 0 && a.Licensees.Count > 0)
		{
			int n = a.Licensees.Count;
			a.Licensees.Clear();
			GameUpdates.Touch();
			return "The " + n + ((n == 1) ? " studio licensing it has" : " studios licensing it have") + " lost access.";
		}
		GameUpdates.Touch();
		return null;
	}

	public static void SetRetired(AntiBotSystem a, bool retired)
	{
		if (a == null || a.Source != -1)
		{
			return;
		}
		a.Retired = retired;
		if (retired)
		{
			a.Licensees.Clear();
		}
		GameUpdates.Touch();
	}

	// ---- Licences: yours to rival studios, theirs to you ----

	private static Dictionary<int, publisherScript> Studios()
	{
		Dictionary<int, publisherScript> map = new Dictionary<int, publisherScript>();
		publisherScript[] all = (G.MS ? G.MS.arrayPublisherScripts : null);
		if (all == null)
		{
			return map;
		}
		foreach (publisherScript s in all)
		{
			if ((bool)s)
			{
				map[s.myID] = s;
			}
		}
		return map;
	}

	public static publisherScript FindStudio(int id)
	{
		publisherScript[] all = (G.MS ? G.MS.arrayPublisherScripts : null);
		if (all == null)
		{
			return null;
		}
		return all.FirstOrDefault((publisherScript s) => (bool)s && s.myID == id);
	}

	private static bool Rival(publisherScript s)
	{
		if ((bool)s && !s.isPlayer && s.isUnlocked && !s.IsTochterfirma())
		{
			return !s.Geschlossen();
		}
		return false;
	}

	// Rival studios with MMOs on the market, with their MMOs.
	public static Dictionary<publisherScript, List<gameScript>> MmoStudios(Dictionary<int, publisherScript> studios = null)
	{
		studios = studios ?? Studios();
		Dictionary<publisherScript, List<gameScript>> map = new Dictionary<publisherScript, List<gameScript>>();
		gameScript[] all = ((G.MS && (bool)G.MS.games_) ? G.MS.games_.arrayGamesScripts : null);
		if (all == null)
		{
			return map;
		}
		foreach (gameScript g in all)
		{
			if ((bool)g && g.gameTyp == 1 && !g.typ_mmoaddon && g.isOnMarket && !g.inDevelopment && !g.IsMyGame() && studios.TryGetValue(g.developerID, out var s) && Rival(s))
			{
				if (!map.TryGetValue(s, out var list))
				{
					list = (map[s] = new List<gameScript>());
				}
				list.Add(g);
			}
		}
		return map;
	}

	// Each month: licensed systems cost their fee and get their studio's patches; your licensees pay you, a few
	// leave (more when it's weak), and studios whose own protection is worse may sign up.
	private static void StepSystems(bool monthly, Dictionary<int, publisherScript> studios)
	{
		foreach (AntiBotSystem a in S.AntiBots.ToList())
		{
			if (a.Source != -1)
			{
				studios.TryGetValue(a.Source, out var vendor);
				if (!vendor || vendor.Geschlossen())
				{
					S.AntiBots.Remove(a);
					News("Your licence for " + a.Name + " ended: its studio has closed.");
					continue;
				}
				a.Strength = Mathf.Max(0f, a.Strength - 0.4f);
				int tech = VendorTech(vendor);
				if (tech > a.Tech)
				{
					a.Tech = tech;
					a.Strength = 95f;
					a.Updated = Now;
					a.Updates++;
					News(vendor.GetName() + " moved " + a.Name + " to " + TechName(tech).ToLower() + ".");
				}
				else if (Now - a.Updated >= VendorUpdateWeeks)
				{
					a.Strength = UnityEngine.Random.Range(88f, 96f);
					a.Updated = Now;
					a.Updates++;
				}
				if (monthly && a.Fee > 0)
				{
					G.MS.Pay(a.Fee, 7);
				}
				continue;
			}
			a.Strength = Mathf.Max(0f, a.Strength - WeeklyWear(a));
			if (monthly && !a.Retired)
			{
				Licensing(a, studios);
			}
		}
	}

	private static void Licensing(AntiBotSystem a, Dictionary<int, publisherScript> studios)
	{
		a.Licensees.RemoveAll((int id) => !studios.TryGetValue(id, out var s) || !Rival(s));
		if (a.Licensees.Count > 0)
		{
			long fee = LicenceFee(a);
			long income = fee * a.Licensees.Count;
			G.MS.Earn(income, 2);
			a.Revenue += income;
			float leave = ((a.Strength < WeakStrength) ? 0.1f : 0.02f);
			List<int> leaving = a.Licensees.Where((int _) => UnityEngine.Random.value < leave).ToList();
			foreach (int id in leaving)
			{
				a.Licensees.Remove(id);
			}
			if (leaving.Count > 0 && studios.TryGetValue(leaving[0], out var first))
			{
				News(first.GetName() + " stopped licensing " + a.Name + ((a.Strength < WeakStrength) ? ": too many bots get past it." : "."));
			}
		}
		if (a.Licensing != 1 || a.Strength < WeakStrength)
		{
			return;
		}
		float chance = 0.12f / FeeSteps[Mathf.Clamp(a.FeeStep, 0, FeeSteps.Length - 1)];
		int signed = 0;
		foreach (KeyValuePair<publisherScript, List<gameScript>> kv in MmoStudios(studios))
		{
			if (signed >= 2)
			{
				break;
			}
			publisherScript s = kv.Key;
			if (a.Licensees.Contains(s.myID))
			{
				continue;
			}
			float mine = kv.Value.Max((gameScript g) => SystemPower(a) * GenreFit(a.Genre, g));
			if (mine > NpcPower(s) + 0.05f && UnityEngine.Random.value < chance)
			{
				a.Licensees.Add(s.myID);
				signed++;
				News(s.GetName() + " licensed " + a.Name + " for its MMOs: " + G.Money(LicenceFee(a)) + " a month.");
			}
		}
	}

	// What rival studios would license you: one system per studio with MMOs, for the genre it makes most.
	public static List<AntiBotOffer> Offers()
	{
		List<AntiBotOffer> list = new List<AntiBotOffer>();
		if (BestTech() < 0)
		{
			return list;
		}
		foreach (KeyValuePair<publisherScript, List<gameScript>> kv in MmoStudios())
		{
			publisherScript s = kv.Key;
			int genre = kv.Value.GroupBy((gameScript g) => g.maingenre).OrderByDescending((IGrouping<int, gameScript> x) => x.Count()).First()
				.Key;
			if (S.AntiBots.Any((AntiBotSystem a) => a.Source == s.myID))
			{
				continue;
			}
			int tech = VendorTech(s);
			int seed = Mathf.Abs(s.myID * 7 + 3);
			list.Add(new AntiBotOffer
			{
				Studio = s,
				Name = NameA[seed % NameA.Length] + NameB[seed / NameA.Length % NameB.Length],
				Genre = genre,
				Tech = tech,
				Strength = 85f + 10f * Stars(s),
				Price = (long)Mathf.Round(30000f * (float)(tech + 1) * Inflation() / 1000f) * 1000,
				Fee = (long)Mathf.Round((5000f + 3000f * (float)tech) * Inflation() / 100f) * 100
			});
		}
		return list.OrderByDescending((AntiBotOffer o) => o.Tech).ThenByDescending((AntiBotOffer o) => o.Strength).ToList();
	}

	public static string Buy(AntiBotOffer o)
	{
		if (o == null || !o.Studio)
		{
			return "That offer is gone.";
		}
		if (S.AntiBots.Any((AntiBotSystem a) => a.Source == o.Studio.myID))
		{
			return "You already license " + o.Studio.GetName() + "'s system.";
		}
		if (G.MS.NotEnoughMoney(o.Price))
		{
			return "You need " + G.Money(o.Price) + " for this.";
		}
		G.MS.Pay(o.Price, 7);
		AntiBotSystem a = new AntiBotSystem
		{
			Id = S.NextAntiBotId++,
			Name = o.Name,
			Genre = o.Genre,
			Tech = o.Tech,
			Strength = o.Strength,
			Built = Now,
			Updated = Now,
			Source = o.Studio.myID,
			Fee = o.Fee
		};
		S.AntiBots.Add(a);
		long caught = Sweep(a);
		GameUpdates.Touch();
		CopyProtectDev.Report("You now license <b>" + a.Name + "</b> from " + o.Studio.GetName() + " for " + GenreName(a.Genre) + " MMOs: " + G.Money(a.Fee) + " a month, and they keep it up to date." + ((caught > 0) ? (" Its first sweep closed " + MmoUiText.Num(caught) + " bot accounts.") : ""));
		return null;
	}

	public static void EndLicence(AntiBotSystem a)
	{
		if (a != null && a.Source != -1)
		{
			S.AntiBots.Remove(a);
			GameUpdates.Touch();
		}
	}

	public static string SourceName(AntiBotSystem a)
	{
		if (a.Source == -1)
		{
			return "your own";
		}
		publisherScript s = FindStudio(a.Source);
		return "licensed from " + (s ? s.GetName() : "another studio");
	}

	private static void News(string text)
	{
		if ((bool)G.Gui)
		{
			G.Gui.CreateTopNewsInfo(text);
		}
	}

	// ---- The game's tooltip ----

	[HarmonyPostfix]
	[HarmonyPatch(typeof(gameScript), "GetTooltip")]
	private static void Tooltip(gameScript __instance, ref string __result)
	{
		try
		{
			gameScript g = __instance;
			if (!On || g.gameTyp != 1 || g.typ_mmoaddon || !g.isOnMarket)
			{
				return;
			}
			long bots = (g.IsMyGame() ? Total(MmoOps.Find(g.myID)) : RivalBots(g));
			if (bots > 0)
			{
				__result = __result + "\n<color=#8e44ad>Bots: " + MmoUiText.Num(bots) + "</color>";
			}
		}
		catch (Exception)
		{
		}
	}

	// Bot accounts as the card and pages show them, with the share.
	public static string BotsText(gameScript g, MmoGame m)
	{
		long n = Total(m);
		return MmoUiText.Num(n) + " (" + (Share(g, m) * 100f).ToString((Share(g, m) < 0.1f) ? "0.0" : "0") + "%)";
	}
}
