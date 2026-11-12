using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HolyAddon.Core.CopyProtection;
using HolyAddon.UI;
using Newtonsoft.Json;
using UnityEngine;

namespace HolyAddon.Core.Updates;

internal enum UpdateKind
{
	Port = 1,
	Engine = 2,
	Features = 3,
	Rating = 4,
	// MMO management projects (Core/Mmo).
	MmoGraphics = 10,
	MmoClient64 = 11,
	MmoMigration = 12,
	MmoExpansion = 13,
	MmoPatch = 14,
	// The normal update types, MMO versions (more effect on players), from MMO Management.
	MmoPort = 15,
	MmoFeatures = 16,
	MmoRating = 17,
	// Live operations for an MMO (Core/Mmo/MmoLive).
	MmoBotBust = 18,
	MmoWelcomeBack = 19,
	MmoClassic = 20,
	MmoFreeTrial = 21,
	MmoStability = 22,
	MmoClassicPhase = 23,
	// Your Digital Store (Core/Store): not tied to a game.
	StoreBuild = 30,
	StoreFeature = 31,
	StoreConsole = 32,
	// Anti-bot systems (Core/Mmo/MmoBots): studio-wide, not tied to a game.
	AntiBotBuild = 40,
	AntiBotUpdate = 41
}

// One update in development: a room works it off like the game's own free updates.
internal class UpdateProject
{
	public int TaskId;

	public int RoomId = -1;

	public int GameId;

	public UpdateKind Kind;

	public string Title;

	public float Needed;

	public float Progress;

	public long Cost;

	// Port: the game's full platform list once the port is done.
	public int[] Platforms;

	// MMO new platforms: ones beyond the game's four platform slots (kept by the addon).
	public List<int> ExtraPlatforms;

	// Building your digital store: its name and logo.
	public string StoreName;

	public int StoreLogo = 1;

	// Engine update: the engine and its four features (graphics, sound, AI, physics).
	public int EngineId = -1;

	public int[] EngineFeatures;

	// New gameplay features.
	public List<int> Features;

	// Age rating edit: the new rating (gameScript.usk).
	public int Usk = -1;

	// Classic servers: 0 = classic realms in the same subscription, 1 = relaunched as its own game; and its name.
	public int Option;

	public string NewName;

	// Anti-bot systems: the genre it's built for, its technology, and the system an update is for.
	public int Genre = -1;

	public int Tech;

	public int SystemId = -1;

	// Console storefront: the console it's for.
	public int PlatformId = -1;

	[JsonIgnore]
	public float Fraction => (Needed <= 0f) ? 1f : Mathf.Clamp01(Progress / Needed);
}

// What the addon's updates did to one of your games.
internal class GameExtras
{
	// Lasting sales bonus from engine and feature updates (capped at BonusCap).
	public float Bonus;

	// A port's launch boost: LaunchBoost in LaunchWeek, fading out over LaunchWeeks.
	public float LaunchBoost;

	public int LaunchWeek = -1000;

	public int Ports;

	public int EngineUpdates;

	public int FeatureUpdates;

	// Gameplay features added by updates (older saves only counted the updates, see Load).
	public int FeaturesAdded;

	public int RatingEdits;
}

internal class UpdatesState
{
	public List<UpdateProject> Projects = new List<UpdateProject>();

	public Dictionary<int, GameExtras> Games = new Dictionary<int, GameExtras>();
}

// What an update would do, worked out before it starts (shown on the pages, applied when it's done).
internal class UpdatePlan
{
	public string Problem;

	public float Work;

	public long Cost;

	public float Bonus;

	public float Gameplay;

	public float Graphics;

	public float Sound;

	public float Tech;

	public string Notes = "";
}

// Ports to more platforms of the same kind, engine updates, new gameplay features and age rating edits for games you
// have on the market. Costs and effects come from the game's own numbers: platform market share and genre fit, the
// engine and gameplay features' points, the game's development points and costs. Sales effects go through the
// game's weekly sales roll as a multiplier, so they scale with rating, hype, age and everything else the game uses.
[HarmonyPatch]
internal static class GameUpdates
{
	private const string SaveKey = "mgt2.editor.updates";

	public const float BonusCap = 0.3f;

	public const int LaunchWeeks = 10;

	public const float LaunchMax = 0.8f;

	private static UpdatesState S = new UpdatesState();

	private static float selling = 1f;

	public static int Version { get; private set; }

	public static bool On
	{
		get
		{
			if (Plugin.ExtraUpdates.Value)
			{
				return G.Singleplayer;
			}
			return false;
		}
	}

	public static IReadOnlyList<UpdateProject> Projects => S.Projects;

	public static void Touch()
	{
		Version++;
	}

	public static string KindName(UpdateKind k)
	{
		return k switch
		{
			UpdateKind.Port => "Port to More Platforms",
			UpdateKind.Engine => "Engine Update",
			UpdateKind.Features => "New Gameplay Features",
			UpdateKind.Rating => "Age Rating Edit",
			UpdateKind.MmoGraphics => "Graphics Overhaul",
			UpdateKind.MmoClient64 => "64-bit Client",
			UpdateKind.MmoMigration => "Engine Migration",
			UpdateKind.MmoExpansion => "World Expansion",
			UpdateKind.MmoPatch => "Content Patch",
			UpdateKind.MmoPort => "New Platforms",
			UpdateKind.MmoFeatures => "New Game Systems",
			UpdateKind.MmoRating => "Age Rating Change",
			UpdateKind.MmoBotBust => "Bot Ban Wave",
			UpdateKind.MmoWelcomeBack => "Welcome-Back Campaign",
			UpdateKind.MmoClassic => "Classic Servers",
			UpdateKind.MmoFreeTrial => "Free Trial",
			UpdateKind.MmoStability => "Stability Patch",
			UpdateKind.MmoClassicPhase => "Classic Phase",
			UpdateKind.StoreBuild => "Store Client",
			UpdateKind.StoreFeature => "Store Feature",
			UpdateKind.StoreConsole => "Console Storefront",
			UpdateKind.AntiBotBuild => "New Anti-Bot System",
			UpdateKind.AntiBotUpdate => "Anti-Bot Update",
			_ => "Update",
		};
	}

	public static bool IsMmo(UpdateKind k)
	{
		if (k >= UpdateKind.MmoGraphics)
		{
			return k < UpdateKind.StoreBuild;
		}
		return false;
	}

	public static bool IsStore(UpdateKind k)
	{
		if (k >= UpdateKind.StoreBuild)
		{
			return k < UpdateKind.AntiBotBuild;
		}
		return false;
	}

	public static bool IsAntiBot(UpdateKind k)
	{
		return k >= UpdateKind.AntiBotBuild;
	}

	// Projects for the whole studio (the store, anti-bot systems) rather than one game.
	public static bool IsStudio(UpdateKind k)
	{
		return k >= UpdateKind.StoreBuild;
	}

	// The normal update type an MMO project is a version of (or the kind itself).
	public static UpdateKind BaseOf(UpdateKind k)
	{
		return k switch
		{
			UpdateKind.MmoPort => UpdateKind.Port,
			UpdateKind.MmoMigration => UpdateKind.Engine,
			UpdateKind.MmoFeatures => UpdateKind.Features,
			UpdateKind.MmoRating => UpdateKind.Rating,
			_ => k,
		};
	}

	// The MMO version of a normal update type.
	public static UpdateKind MmoOf(UpdateKind k)
	{
		return k switch
		{
			UpdateKind.Port => UpdateKind.MmoPort,
			UpdateKind.Engine => UpdateKind.MmoMigration,
			UpdateKind.Features => UpdateKind.MmoFeatures,
			UpdateKind.Rating => UpdateKind.MmoRating,
			_ => k,
		};
	}

	public static UpdateProject ProjectFor(int gameId)
	{
		return S.Projects.FirstOrDefault((UpdateProject p) => p.GameId == gameId);
	}

	public static UpdateProject ProjectInRoom(roomScript r)
	{
		if (!r)
		{
			return null;
		}
		return S.Projects.FirstOrDefault((UpdateProject p) => p.TaskId == r.taskID && p.TaskId != -1);
	}

	// How far an addon for this game may go over the size limit on gameplay features. The game's addon menus count
	// the base game's features too, so features added by updates would eat the addon's room; they don't count. A
	// base game that's over the limit anyway (older saves, other mods) always gets at least enough to make an addon.
	public static int AddonFeatureAllowance(gameScript g, int cap)
	{
		if (!g || g.gameGameplayFeatures == null)
		{
			return 0;
		}
		int have = g.gameGameplayFeatures.Count((bool b) => b);
		int added = Extras(g.myID)?.FeaturesAdded ?? 0;
		return Mathf.Max(0, Mathf.Max(added, have - cap));
	}

	public static GameExtras Extras(int gameId, bool create = false)
	{
		if (!S.Games.TryGetValue(gameId, out var e) && create)
		{
			e = new GameExtras();
			S.Games[gameId] = e;
		}
		return e;
	}

	public static gameScript FindGame(int id)
	{
		gameScript[] all = ((G.MS && (bool)G.MS.games_) ? G.MS.games_.arrayGamesScripts : null);
		if (all == null)
		{
			return null;
		}
		return all.FirstOrDefault((gameScript g) => (bool)g && g.myID == id);
	}

	private static T Main<T>() where T : Component
	{
		if (!G.MainObj)
		{
			return null;
		}
		return G.MainObj.GetComponent<T>();
	}

	public static engineFeatures EF => Main<engineFeatures>();

	public static gameplayFeatures GF => Main<gameplayFeatures>();

	// ---- Which games can get which update ----

	public static bool BaseEligible(gameScript g)
	{
		if (!g || !G.MS)
		{
			return false;
		}
		int me = G.MS.myID;
		if (g.ownerID == me && g.developerID == me && !g.inDevelopment && g.isOnMarket && !g.typ_bundle && !g.typ_budget && !g.typ_bundleAddon && !g.typ_goty && !g.typ_addon && !g.typ_addonStandalone && !g.typ_mmoaddon && !g.typ_contractGame)
		{
			return !IpLinks.Archived(g);
		}
		return false;
	}

	public static string Problem(UpdateKind kind, gameScript g)
	{
		if (!BaseEligible(g))
		{
			return "Only your own games on the market can get this update.";
		}
		UpdateProject running = ProjectFor(g.myID);
		if (running != null)
		{
			return "Your team is already working on " + running.Title + ".";
		}
		if (IsMmo(kind))
		{
			string mmo = Mmo.MmoOps.Problem(kind, g);
			if (mmo != null)
			{
				return mmo;
			}
			// MMOs aren't limited to the four platform slots (MmoOps.PortProblem has its own checks).
			if (kind == UpdateKind.MmoPort)
			{
				return Mmo.MmoOps.PortProblem(g);
			}
		}
		else if (g.gameTyp == 1 && Mmo.MmoOps.On)
		{
			return "MMOs get this in MMO Management (development room menu).";
		}
		switch (BaseOf(kind))
		{
		case UpdateKind.Port:
			if (TimedExclusives.Active(g) != null)
			{
				return "It's a timed exclusive: it comes to its other platforms when the deal ends.";
			}
			if (g.arcade || g.retro)
			{
				return "Arcade and retro games can't be ported this way. Use Develop a Game > Port.";
			}
			if (FreeSlots(g).Count == 0)
			{
				return "Every platform slot is in use. Research more platforms per game, or wait until one of its platforms leaves the market.";
			}
			return null;
		case UpdateKind.Engine:
			return (EngineCandidates(g).Count == 0) ? "None of your engines has newer features this game's platforms can run." : null;
		case UpdateKind.Features:
			return (FeatureCandidates(g, null).Count == 0) ? "There are no researched gameplay features this game doesn't have yet." : null;
		case UpdateKind.Rating:
			return (g.usk <= MinUsk(g)) ? "The topic doesn't allow a lower age rating." : null;
		default:
			return null;
		}
	}

	// ---- Shared numbers ----

	// The game's own development points (with platform complexity) and costs, as the free update uses them.
	public static float DevPoints(gameScript g)
	{
		return Mathf.Max(50f, g.GetGesamtDevPoints());
	}

	public static long DevCost(gameScript g)
	{
		return Math.Max(20000L, Math.Min(g.costs_entwicklung + g.costs_updates, 50000000L));
	}

	// GetGesamtDevPoints includes the platform complexity factor; this is that factor.
	private static float Complexity(gameScript g)
	{
		engineFeatures ef = EF;
		gameplayFeatures gf = GF;
		float raw = 0f;
		for (int i = 0; i < g.gameEngineFeature.Length; i++)
		{
			raw += ef.GetDevPointsForGame(g.gameEngineFeature[i]);
		}
		for (int j = 0; j < g.gameGameplayFeatures.Length; j++)
		{
			if (g.gameGameplayFeatures[j])
			{
				raw += gf.GetDevPoints(j);
			}
		}
		if (raw <= 0f)
		{
			return 1f;
		}
		return Mathf.Clamp((float)g.GetGesamtDevPoints() / raw, 0.5f, 3f);
	}

	// Ratings scale how much an update helps: a well-reviewed game gets more out of new content.
	public static float ReviewFactor(gameScript g)
	{
		return 0.6f + Mathf.Clamp(g.reviewTotal, 0, 100) / 250f;
	}

	// The room's pace, with the rooms supporting it (their staff work on its project too).
	public static float RoomRate(roomScript r, UpdateKind kind)
	{
		float total = OwnRate(r, kind);
		foreach (roomScript s in RoomSupport.Supporters(r))
		{
			total += OwnRate(s, kind);
		}
		return total;
	}

	private static float OwnRate(roomScript r, UpdateKind kind)
	{
		if (!r || !G.MS)
		{
			return 0f;
		}
		float pressure = ((G.MS.personal_druck == 1) ? 0.8f : ((G.MS.personal_druck == 2) ? 0.6f : 1f));
		float total = 0f;
		foreach (characterScript c in CopyProtectDev.Staff(r))
		{
			float speed = UpdateRoom.Speed(c) / Mathf.Max(0.01f, G.MS.speedSetting);
			float desk = (c.objectUsingS_ ? (((float)c.objectUsingS_.qualitaet - 1f) / 10f + 1f) : 1f);
			total += speed * (0.1f + Skill(c, kind) * 0.1f) / 2f * desk * pressure;
		}
		return total * 0.95f;
	}

	// Ports and engines are programming work; features and content edits mix in game design.
	public static float Skill(characterScript c, UpdateKind kind)
	{
		return kind switch
		{
			UpdateKind.Features => (c.s_gamedesign + c.s_programmieren) * 0.5f,
			UpdateKind.MmoFeatures => (c.s_gamedesign + c.s_programmieren) * 0.5f,
			UpdateKind.Rating => c.s_gamedesign,
			UpdateKind.MmoRating => c.s_gamedesign,
			UpdateKind.MmoGraphics => c.s_grafik,
			UpdateKind.MmoExpansion => (c.s_gamedesign + c.s_programmieren + c.s_grafik) / 3f,
			UpdateKind.MmoPatch => (c.s_gamedesign + c.s_programmieren) * 0.5f,
			UpdateKind.MmoClassicPhase => (c.s_gamedesign + c.s_programmieren) * 0.5f,
			UpdateKind.MmoWelcomeBack => c.s_gamedesign,
			UpdateKind.MmoBotBust => (c.s_programmieren + c.s_gametests) * 0.5f,
			UpdateKind.MmoStability => (c.s_programmieren + c.s_gametests) * 0.5f,
			_ => c.s_programmieren,
		};
	}

	public static string Duration(float work, roomScript r, UpdateKind kind)
	{
		float rate = RoomRate(r, kind);
		if (rate <= 0f)
		{
			return "-";
		}
		int weeks = Mathf.CeilToInt(work / rate);
		return (weeks > 1) ? ("about " + weeks + " weeks") : "about 1 week";
	}

	public static string RoomProblem(roomScript room)
	{
		if (!room || room.typ != 1)
		{
			return "Updates are made in a development room.";
		}
		if (room.taskID != -1)
		{
			return CopyProtectDev.RoomName(room) + " is busy with other work.";
		}
		if (CopyProtectDev.Staff(room).Count == 0)
		{
			return CopyProtectDev.RoomName(room) + " has no staff.";
		}
		return null;
	}

	// ---- Ports ----

	public static bool SlotUnlocked(int slot)
	{
		if (slot == 0)
		{
			return true;
		}
		unlockScript u = Main<unlockScript>();
		if ((bool)u && slot <= 3)
		{
			return u.unlock[27 + slot];
		}
		return false;
	}

	// Slots a port can use: empty ones you've researched, and ones whose platform has left the market.
	public static List<int> FreeSlots(gameScript g)
	{
		List<int> list = new List<int>();
		g.FindMyPlatforms();
		for (int i = 0; i < g.gamePlatform.Length; i++)
		{
			if (g.gamePlatform[i] == -1)
			{
				if (SlotUnlocked(i))
				{
					list.Add(i);
				}
			}
			else if ((bool)g.gamePlatformScript[i] && g.gamePlatformScript[i].vomMarktGenommen)
			{
				list.Add(i);
			}
		}
		return list;
	}

	public static platformScript Platform(int id)
	{
		if (id < 0 || !G.MS || G.MS.arrayPlatformsScripts == null)
		{
			return null;
		}
		return G.MS.arrayPlatformsScripts.FirstOrDefault((platformScript p) => (bool)p && p.myID == id);
	}

	// The platform part of the game's weekly sales: base 10 (25 for an exclusive), plus market share x genre fit.
	public static float Reach(gameScript g, IEnumerable<int> platforms, bool exclusive)
	{
		genres gen = Main<genres>();
		float sum = (exclusive ? 25f : 10f);
		foreach (int id in platforms)
		{
			platformScript p = Platform(id);
			if ((bool)p)
			{
				sum += p.GetMarktanteil() * (gen ? gen.GetFloatPlatformSells(g.maingenre, p.typ) : 1f);
			}
		}
		return sum;
	}

	public static UpdatePlan PlanPort(gameScript g, int[] newList)
	{
		UpdatePlan plan = new UpdatePlan();
		List<int> added = newList.Where((int id) => id != -1 && !g.gamePlatform.Contains(id)).Distinct().ToList();
		if (added.Count == 0)
		{
			plan.Problem = "Pick at least one more platform.";
			return plan;
		}
		float dev = DevPoints(g);
		long devCost = DevCost(g);
		foreach (int id in added)
		{
			platformScript p = Platform(id);
			float complex = (p ? (1f + Mathf.Clamp(p.complex, 0, 3) * 0.15f) : 1f);
			plan.Work += (0.15f * dev + 15f) * complex;
			plan.Cost += (long)(0.08f * (float)devCost) + (p ? p.GetDevCosts() : 0);
		}
		int countAfter = newList.Count((int id) => id != -1);
		bool exclusiveAfter = g.exklusiv && countAfter <= 1;
		float before = Reach(g, g.gamePlatform.Where((int id) => id != -1), g.exklusiv);
		float after = Reach(g, newList.Where((int id) => id != -1), exclusiveAfter);
		plan.Bonus = LaunchBoostFor(g, newList);
		float lasting = after / Mathf.Max(1f, before) - 1f;
		plan.Notes = "Sales from then on: " + Pct(lasting) + ", plus a launch boost of up to " + Pct(plan.Bonus) + " fading over " + LaunchWeeks + " weeks.";
		if (g.exklusiv && !exclusiveAfter)
		{
			plan.Notes += " It stops being an exclusive, which costs it the exclusive sales bonus.";
		}
		if (g.sellsPerWeek != null && g.sellsPerWeek.Length != 0 && g.sellsPerWeek[0] < 200 && g.weeksOnMarket > 20)
		{
			plan.Notes += " It has nearly stopped selling, so a port won't bring in much.";
		}
		return plan;
	}

	// A port launches the game on new platforms: a boost the size of the new platforms' share of the game's reach.
	public static float LaunchBoostFor(gameScript g, int[] newList)
	{
		List<int> added = newList.Where((int id) => id != -1 && !g.gamePlatform.Contains(id)).ToList();
		int countAfter = newList.Count((int id) => id != -1);
		float all = Reach(g, newList.Where((int id) => id != -1), g.exklusiv && countAfter <= 1);
		float fresh = Reach(g, added, exclusive: false) - 10f;
		return Mathf.Clamp(1.2f * fresh / Mathf.Max(1f, all), 0f, LaunchMax);
	}

	public static string Pct(float f)
	{
		int n = Mathf.RoundToInt(f * 100f);
		return ((n >= 0) ? "+" : "") + n + "%";
	}

	// ---- Engine updates ----

	public static int MinPlatformTech(gameScript g)
	{
		int min = int.MaxValue;
		g.FindMyPlatforms();
		for (int i = 0; i < g.gamePlatform.Length; i++)
		{
			if (g.gamePlatform[i] != -1 && (bool)g.gamePlatformScript[i])
			{
				min = Mathf.Min(min, g.gamePlatformScript[i].tech);
			}
		}
		return (min == int.MaxValue) ? 1 : min;
	}

	public static engineScript CurrentEngine(gameScript g)
	{
		g.FindMyEngineNew();
		return g.engineS_;
	}

	// Engines you own or have licensed that would give the game at least one better feature.
	public static List<engineScript> EngineCandidates(gameScript g)
	{
		List<engineScript> list = new List<engineScript>();
		engineScript[] all = (G.MS ? G.MS.arrayEnginesScripts : null);
		if (all == null || !EF)
		{
			return list;
		}
		foreach (engineScript e in all)
		{
			if ((bool)e && !e.archiv_engine && (e.ownerID == G.MS.myID || e.gekauft) && (e.spezialplatform == -1 || g.gamePlatform.Contains(e.spezialplatform)) && UpgradeSlots(g, e).Any((int f) => f != -1))
			{
				list.Add(e);
			}
		}
		return list.OrderByDescending((engineScript e) => (e.myID == g.engineID) ? 1 : 0).ThenByDescending((engineScript e) => e.GetTechLevel()).ToList();
	}

	// For each of the game's four engine feature slots: the better feature this engine offers, or -1.
	public static int[] UpgradeSlots(gameScript g, engineScript e)
	{
		engineFeatures ef = EF;
		int tech = MinPlatformTech(g);
		int[] result = new int[g.gameEngineFeature.Length];
		for (int i = 0; i < result.Length; i++)
		{
			result[i] = -1;
			int current = g.gameEngineFeature[i];
			int typ = ((current >= 0 && current < ef.engineFeatures_TYP.Length) ? ef.engineFeatures_TYP[current] : i);
			int currentPoints = ((current >= 0) ? ef.engineFeatures_RES_POINTS[current] : (-1));
			int best = -1;
			int bestPoints = currentPoints;
			for (int f = 0; f < e.features.Length && f < ef.engineFeatures_TYP.Length; f++)
			{
				if (e.features[f] && ef.engineFeatures_TYP[f] == typ && ef.engineFeatures_TECH[f] <= tech && ef.engineFeatures_RES_POINTS[f] > bestPoints)
				{
					best = f;
					bestPoints = ef.engineFeatures_RES_POINTS[f];
				}
			}
			result[i] = best;
		}
		return result;
	}

	public static UpdatePlan PlanEngine(gameScript g, engineScript e, int[] slots = null)
	{
		UpdatePlan plan = new UpdatePlan();
		if (!e)
		{
			plan.Problem = "Pick an engine.";
			return plan;
		}
		engineFeatures ef = EF;
		slots = slots ?? UpgradeSlots(g, e);
		float complexity = Complexity(g);
		float dev = DevPoints(g);
		long devCost = DevCost(g);
		int upgraded = 0;
		int techGain = 0;
		for (int i = 0; i < slots.Length; i++)
		{
			int f = slots[i];
			if (f == -1)
			{
				continue;
			}
			int old = g.gameEngineFeature[i];
			upgraded++;
			techGain += Mathf.Max(0, ef.engineFeatures_TECH[f] - ((old >= 0) ? ef.engineFeatures_TECH[old] : 0));
			plan.Work += ef.GetDevPointsForGame(f) * complexity + 0.03f * dev;
			plan.Cost += (long)(ef.GetDevCosts(f) * 1.5f) + (long)(0.03f * (float)devCost);
			plan.Gameplay += Mathf.Max(0, ef.GetGameplay(f) - ((old >= 0) ? ef.GetGameplay(old) : 0));
			plan.Graphics += Mathf.Max(0, ef.GetGraphic(f) - ((old >= 0) ? ef.GetGraphic(old) : 0));
			plan.Sound += Mathf.Max(0, ef.GetSound(f) - ((old >= 0) ? ef.GetSound(old) : 0));
			plan.Tech += Mathf.Max(0, ef.GetTechnik(f) - ((old >= 0) ? ef.GetTechnik(old) : 0));
		}
		if (upgraded == 0)
		{
			plan.Problem = "This engine has nothing newer for the game's platforms.";
			return plan;
		}
		plan.Bonus = BonusRoom(g, ReviewFactor(g) * (Mathf.Min(0.12f, 0.01f * techGain) + 0.008f * upgraded));
		if (e.myID != g.engineID && e.ownerID != G.MS.myID && e.gewinnbeteiligung > 0)
		{
			plan.Notes = "From then on the game pays " + e.gewinnbeteiligung + "% of its sales to the engine's owner.";
		}
		else if (e.myID != g.engineID && g.engineGewinnbeteiligung > 0 && e.ownerID == G.MS.myID)
		{
			plan.Notes = "Moving to your own engine ends the " + g.engineGewinnbeteiligung + "% engine licence fee.";
		}
		return plan;
	}

	// ---- New gameplay features ----

	public static bool AllPlatformsOnline(gameScript g)
	{
		g.FindMyPlatforms();
		for (int i = 0; i < g.gamePlatform.Length; i++)
		{
			if (g.gamePlatform[i] != -1 && (bool)g.gamePlatformScript[i] && !g.gamePlatformScript[i].internet)
			{
				return false;
			}
		}
		return true;
	}

	// Researched gameplay features the game doesn't have and could take (platform locks, online, prerequisites).
	public static List<int> FeatureCandidates(gameScript g, ICollection<int> picked)
	{
		List<int> list = new List<int>();
		gameplayFeatures gf = GF;
		if (!gf)
		{
			return list;
		}
		bool online = AllPlatformsOnline(g);
		for (int j = 0; j < g.gameGameplayFeatures.Length && j < gf.gameplayFeatures_TYP.Length; j++)
		{
			if (g.gameGameplayFeatures[j] || !gf.IsErforscht(j) || (g.arcade && gf.gameplayFeatures_LOCKPLATFORM[j, 4]) || (g.handy && gf.gameplayFeatures_LOCKPLATFORM[j, 3]) || (gf.gameplayFeatures_INTERNET[j] && !online))
			{
				continue;
			}
			int need = gf.gameplayFeatures_NEED_GAMEPLAY_FEATURE[j];
			if (need == -1 || g.gameGameplayFeatures[need] || (picked != null && picked.Contains(need)) || gf.IsErforscht(need))
			{
				list.Add(j);
			}
		}
		return list;
	}

	public static string FeatureNeed(gameScript g, int j, ICollection<int> picked)
	{
		gameplayFeatures gf = GF;
		int need = gf.gameplayFeatures_NEED_GAMEPLAY_FEATURE[j];
		if (need == -1 || g.gameGameplayFeatures[need] || (picked != null && picked.Contains(need)))
		{
			return null;
		}
		return gf.GetName(need);
	}

	public static UpdatePlan PlanFeatures(gameScript g, ICollection<int> picked)
	{
		UpdatePlan plan = new UpdatePlan();
		if (picked == null || picked.Count == 0)
		{
			plan.Problem = "Pick at least one feature.";
			return plan;
		}
		gameplayFeatures gf = GF;
		float complexity = Complexity(g);
		long devCost = DevCost(g);
		float fit = 0f;
		int poor = 0;
		foreach (int j in picked)
		{
			string need = FeatureNeed(g, j, picked);
			if (need != null)
			{
				plan.Problem = gf.GetName(j) + " needs " + need + " as well.";
				return plan;
			}
			plan.Work += gf.GetDevPoints(j) * complexity * 1.25f;
			plan.Cost += (long)(gf.GetDevCosts(j) * 1.25f) + (long)(0.015f * (float)devCost);
			plan.Gameplay += Mathf.Max(0, gf.GetGameplay(j, g.maingenre, g.subgenre));
			plan.Graphics += Mathf.Max(0, gf.GetGraphic(j, g.maingenre, g.subgenre));
			plan.Sound += Mathf.Max(0, gf.GetSound(j, g.maingenre, g.subgenre));
			plan.Tech += Mathf.Max(0, gf.GetTechnik(j, g.maingenre, g.subgenre));
			float b = gf.GetBonus(j, g.maingenre, g.subgenre);
			fit += 0.012f * b;
			if (b < 0.5f)
			{
				poor++;
			}
		}
		plan.Bonus = BonusRoom(g, ReviewFactor(g) * fit);
		if (poor > 0)
		{
			plan.Notes = ((poor == 1) ? "One feature doesn't suit the genre" : (poor + " features don't suit the genre")) + ": players will grumble about it in their reviews.";
		}
		return plan;
	}

	// ---- Age rating edits ----

	public static int MinUsk(gameScript g)
	{
		themes t = Main<themes>();
		int min = 0;
		if ((bool)t && t.themes_MGSR != null)
		{
			if (g.gameMainTheme >= 0 && g.gameMainTheme < t.themes_MGSR.Length)
			{
				min = Mathf.Max(min, t.themes_MGSR[g.gameMainTheme]);
			}
			if (g.gameSubTheme >= 0 && g.gameSubTheme < t.themes_MGSR.Length)
			{
				min = Mathf.Max(min, t.themes_MGSR[g.gameSubTheme]);
			}
		}
		return min;
	}

	public static string UskName(int usk)
	{
		return usk switch
		{
			0 => "0+",
			1 => "6+",
			2 => "12+",
			3 => "16+",
			4 => "18+",
			_ => G.Text(1306, "Not approved"),
		};
	}

	// The sales effect the game gives each age rating (gameScript.SellGame).
	public static float UskSales(int usk)
	{
		return usk switch
		{
			0 => 0.1f,
			1 => 0.1f,
			2 => 0.05f,
			3 => 0f,
			4 => -0.05f,
			5 => -0.2f,
			_ => 0f,
		};
	}

	public static UpdatePlan PlanRating(gameScript g, int usk)
	{
		UpdatePlan plan = new UpdatePlan();
		int steps = g.usk - usk;
		if (steps <= 0)
		{
			plan.Problem = "Pick a lower age rating.";
			return plan;
		}
		if (usk < MinUsk(g))
		{
			plan.Problem = "The topic doesn't allow an age rating below " + UskName(MinUsk(g)) + ".";
			return plan;
		}
		plan.Work = 0.06f * DevPoints(g) * steps + 10f;
		plan.Cost = (long)(0.03f * (float)DevCost(g) * (float)steps);
		plan.Gameplay = (0f - g.points_gameplay) * 0.03f * (float)steps;
		plan.Bonus = UskSales(usk) - UskSales(g.usk);
		plan.Notes = "Cutting content costs it a little gameplay, and some fans won't like the cut.";
		return plan;
	}

	// How much of a new lasting bonus still fits under the cap.
	public static float BonusRoom(gameScript g, float wanted)
	{
		float now = Extras(g.myID)?.Bonus ?? 0f;
		return Mathf.Clamp(wanted, 0f, Mathf.Max(0f, BonusCap - now));
	}

	// ---- Starting, cancelling, finishing ----

	public static string Start(UpdateProject p, UpdatePlan plan, roomScript room, gameScript g)
	{
		bool store = IsStudio(p.Kind);
		string problem = plan.Problem ?? (IsAntiBot(p.Kind) ? Mmo.MmoBots.ProjectProblem(p) : (store ? Store.StoreOps.ProjectProblem(p) : Problem(p.Kind, g))) ?? RoomProblem(room);
		if (problem != null)
		{
			return problem;
		}
		if (G.MS.NotEnoughMoney(plan.Cost))
		{
			return "You need " + G.Money(plan.Cost) + " for this.";
		}
		G.MS.Pay(plan.Cost, 15);
		p.GameId = (store ? (-1) : g.myID);
		p.Cost = plan.Cost;
		p.Needed = Mathf.Max(5f, plan.Work);
		p.Progress = 0f;
		p.TaskId = UnityEngine.Random.Range(1, 100000000);
		string title = (string.IsNullOrEmpty(p.Title) ? KindName(p.Kind) : p.Title);
		p.Title = (store ? title : (title + ": " + g.GetNameWithTag()));
		S.Projects.Add(p);
		UpdateRoom.Attach(p, room);
		Version++;
		return null;
	}

	public static long Refund(UpdateProject p)
	{
		return (long)((float)p.Cost * (1f - p.Fraction));
	}

	public static long Cancel(UpdateProject p)
	{
		if (p == null || !S.Projects.Contains(p))
		{
			return 0L;
		}
		long refund = Refund(p);
		S.Projects.Remove(p);
		UpdateRoom.Detach(p);
		if (refund > 0 && (bool)G.MS)
		{
			G.MS.Earn(refund, 1);
		}
		Version++;
		return refund;
	}

	public static void Work(float points, roomScript room)
	{
		UpdateProject p = ProjectInRoom(room);
		if (p == null)
		{
			return;
		}
		p.Progress += points;
		if (p.Progress < p.Needed)
		{
			return;
		}
		S.Projects.Remove(p);
		UpdateRoom.Detach(p);
		Version++;
		bool store = IsStudio(p.Kind);
		gameScript g = FindGame(p.GameId);
		if (!g && !store)
		{
			return;
		}
		try
		{
			roomDataScript rd = Main<roomDataScript>();
			if ((bool)room && (bool)G.Gui && (bool)rd)
			{
				G.Gui.CreateLeftNews(room.myID, UpdateIcons.For(p.Kind), "<b>" + p.Title + "</b>", rd.roomData_SPRITE[Mathf.Clamp(room.typ, 0, rd.roomData_SPRITE.Length - 1)]);
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Update news: " + ex.Message);
		}
		try
		{
			string message = (IsAntiBot(p.Kind) ? Mmo.MmoBots.Complete(p, room) : (store ? Store.StoreOps.Complete(p) : Complete(p, g)));
			if (!store && !string.IsNullOrEmpty(message))
			{
				message += Aftermath(p, g, room);
			}
			if (!string.IsNullOrEmpty(message))
			{
				CopyProtectDev.Report(message);
				if ((bool)G.Gui)
				{
					G.Gui.AddHistory(p.Title + (store ? " is done." : " is out."));
				}
			}
		}
		catch (Exception ex2)
		{
			Plugin.Log.LogError("Finishing " + p.Title + " failed: " + ex2);
		}
	}

	// What every finished update does besides its own effect, as the game's free updates and Holy treat them:
	// it counts as one of the game's updates (later free updates add a little less), a patch breaks pirated
	// copies (Holy), new code brings a few bugs (fewer with good testers), and good updates please players.
	private static string Aftermath(UpdateProject p, gameScript g, roomScript room)
	{
		List<string> notes = new List<string>();
		UpdateKind b = BaseOf(p.Kind);
		// A campaign, a free trial or classic servers aren't patches of the game.
		if (p.Kind != UpdateKind.MmoWelcomeBack && p.Kind != UpdateKind.MmoFreeTrial && p.Kind != UpdateKind.MmoClassic)
		{
			g.amountUpdates++;
		}
		if (b == UpdateKind.Engine || b == UpdateKind.Features || b == UpdateKind.Rating)
		{
			long gone = HolyCompat.BreakPirates(g, 0.4f, 0.3f);
			if (gone > 0)
			{
				notes.Add("The update broke the pirated copies going around: pirates -" + G.MS.GetMoney(gone, showDollar: false) + ".");
			}
		}
		if (b == UpdateKind.Engine || b == UpdateKind.Features)
		{
			int bugs = Mathf.RoundToInt(p.Needed * 0.06f * (1f - 0.8f * TestSkill(room) / 100f));
			if (bugs >= 1)
			{
				g.points_bugs += bugs;
				notes.Add("The new code brought about " + bugs + " bugs; a free update can fix them.");
			}
		}
		float nudge = 0f;
		switch (p.Kind)
		{
		case UpdateKind.Engine:
		case UpdateKind.MmoMigration:
		case UpdateKind.MmoGraphics:
		case UpdateKind.MmoPatch:
			nudge = 0.01f;
			break;
		case UpdateKind.Features:
		case UpdateKind.MmoFeatures:
		{
			gameplayFeatures gf = GF;
			bool poor = (bool)gf && p.Features != null && p.Features.Any((int j) => gf.GetBonus(j, g.maingenre, g.subgenre) < 0.5f);
			nudge = (poor ? 0f : 0.02f);
			break;
		}
		case UpdateKind.MmoExpansion:
			nudge = 0.02f;
			break;
		case UpdateKind.MmoBotBust:
		case UpdateKind.MmoStability:
		case UpdateKind.MmoClassicPhase:
			nudge = 0.01f;
			break;
		}
		if (nudge > 0f)
		{
			HolyCompat.ShiftUserReviews(g, nudge);
			notes.Add("Players like it: user reviews +" + Mathf.RoundToInt(nudge * 100f) + "%.");
		}
		if (notes.Count == 0)
		{
			return "";
		}
		return "\n\n" + string.Join(" ", notes);
	}

	// The room's average testing skill (0-100).
	private static float TestSkill(roomScript room)
	{
		List<characterScript> staff = (room ? CopyProtectDev.Staff(room) : new List<characterScript>());
		if (staff.Count == 0)
		{
			return 0f;
		}
		return staff.Average((characterScript c) => c.s_gametests);
	}

	private static string Complete(UpdateProject p, gameScript g)
	{
		GameExtras extras = Extras(g.myID, create: true);
		g.costs_updates += p.Cost;
		string name = "<b>" + g.GetNameWithTag() + "</b>";
		switch (p.Kind)
		{
		case UpdateKind.Port:
			return CompletePort(p, g, extras, name);
		case UpdateKind.Engine:
			return CompleteEngine(p, g, extras, name);
		case UpdateKind.Features:
			return CompleteFeatures(p, g, extras, name);
		case UpdateKind.Rating:
			return CompleteRating(p, g, extras, name);
		default:
			return IsMmo(p.Kind) ? Mmo.MmoOps.Complete(p, g, extras, name) : null;
		}
	}

	internal static string CompletePort(UpdateProject p, gameScript g, GameExtras extras, string name)
	{
		if (p.Platforms == null || p.Platforms.Length != g.gamePlatform.Length)
		{
			return null;
		}
		float boost = LaunchBoostFor(g, p.Platforms);
		List<int> before = g.gamePlatform.Where((int id) => id != -1).ToList();
		List<platformScript> added = new List<platformScript>();
		for (int i = 0; i < g.gamePlatform.Length; i++)
		{
			int id = p.Platforms[i];
			if (id != -1 && !before.Contains(id))
			{
				platformScript ps = Platform(id);
				if ((bool)ps)
				{
					added.Add(ps);
				}
			}
			g.gamePlatform[i] = id;
			g.gamePlatformScript[i] = null;
		}
		g.FindMyPlatforms();
		foreach (platformScript ps2 in added)
		{
			ps2.games++;
		}
		bool wasExclusive = g.exklusiv;
		int count = g.gamePlatform.Count((int id) => id != -1);
		if (g.exklusiv && count > 1)
		{
			g.exklusiv = false;
			foreach (int id2 in before)
			{
				platformScript old = Platform(id2);
				if ((bool)old && old.exklusivGames > 0)
				{
					old.exklusivGames--;
				}
			}
		}
		if (g.herstellerExklusiv && added.Any((platformScript ps3) => ps3.ownerID != G.MS.myID))
		{
			g.herstellerExklusiv = false;
		}
		extras.LaunchBoost = boost;
		extras.LaunchWeek = CopyProtectDev.AbsWeek;
		extras.Ports++;
		if ((bool)g.gameTab_)
		{
			g.gameTab_.UpdateData();
		}
		string list = string.Join(", ", added.Select((platformScript ps4) => ps4.GetName()));
		return name + " is out on " + list + ".\n\nSales get a launch boost of " + Pct(boost) + " that fades over " + LaunchWeeks + " weeks, and the extra platforms keep adding to its sales from now on." + ((wasExclusive && !g.exklusiv) ? " It's no longer an exclusive." : "");
	}

	private static string CompleteEngine(UpdateProject p, gameScript g, GameExtras extras, string name)
	{
		UpdatePlan plan = ApplyEngine(p, g, extras, out engineScript e);
		if (plan == null)
		{
			return null;
		}
		extras.EngineUpdates++;
		return name + " now runs on " + e.GetName() + " with newer engine features.\n\nGraphics +" + Mathf.RoundToInt(plan.Graphics) + ", sound +" + Mathf.RoundToInt(plan.Sound) + ", tech +" + Mathf.RoundToInt(plan.Tech) + ", and its sales " + Pct(plan.Bonus) + " for as long as it's on the market.";
	}

	// Moves the game to the project's engine and its features (those that are still an improvement), with the points
	// and lasting sales bonus they bring. Returns what was applied, or null if nothing could be.
	public static UpdatePlan ApplyEngine(UpdateProject p, gameScript g, GameExtras extras, out engineScript e)
	{
		int engineId = p.EngineId;
		e = G.MS.arrayEnginesScripts?.FirstOrDefault((engineScript x) => (bool)x && x.myID == engineId);
		if (!e || p.EngineFeatures == null)
		{
			return null;
		}
		engineFeatures ef = EF;
		// Only slots that are still an improvement (the game could have changed since the update started).
		int[] slots = new int[g.gameEngineFeature.Length];
		for (int i = 0; i < slots.Length; i++)
		{
			int f = ((i < p.EngineFeatures.Length) ? p.EngineFeatures[i] : (-1));
			int old = g.gameEngineFeature[i];
			slots[i] = ((f != -1 && (old < 0 || ef.engineFeatures_RES_POINTS[f] > ef.engineFeatures_RES_POINTS[old])) ? f : (-1));
		}
		UpdatePlan plan = PlanEngine(g, e, slots);
		if (plan.Problem != null)
		{
			return null;
		}
		for (int j = 0; j < slots.Length; j++)
		{
			if (slots[j] != -1)
			{
				g.gameEngineFeature[j] = slots[j];
			}
		}
		g.points_gameplay += plan.Gameplay;
		g.points_grafik += plan.Graphics;
		g.points_sound += plan.Sound;
		g.points_technik += plan.Tech;
		if (e.myID != g.engineID)
		{
			g.engineID = e.myID;
			g.engineS_ = null;
			g.engineGewinnbeteiligung = ((e.ownerID == G.MS.myID) ? 0 : e.gewinnbeteiligung);
			g.FindMyEngineNew();
		}
		extras.Bonus = Mathf.Min(BonusCap, extras.Bonus + plan.Bonus);
		return plan;
	}

	internal static string CompleteFeatures(UpdateProject p, gameScript g, GameExtras extras, string name)
	{
		if (p.Features == null || p.Features.Count == 0)
		{
			return null;
		}
		List<int> fresh = p.Features.Where((int j) => j >= 0 && j < g.gameGameplayFeatures.Length && !g.gameGameplayFeatures[j]).ToList();
		UpdatePlan plan = PlanFeatures(g, fresh);
		gameplayFeatures gf = GF;
		int poor = 0;
		foreach (int j in fresh)
		{
			g.gameGameplayFeatures[j] = true;
			if (g.gameplayFeatures_DevDone != null && j < g.gameplayFeatures_DevDone.Length)
			{
				g.gameplayFeatures_DevDone[j] = true;
			}
			if (gf.GetBonus(j, g.maingenre, g.subgenre) < 0.5f)
			{
				poor++;
			}
		}
		g.points_gameplay += plan.Gameplay;
		g.points_grafik += plan.Graphics;
		g.points_sound += plan.Sound;
		g.points_technik += plan.Tech;
		if (poor > 0)
		{
			g.userNegativ += Mathf.RoundToInt((float)(g.userPositiv + g.userNegativ) * 0.02f * (float)poor);
		}
		extras.Bonus = Mathf.Min(BonusCap, extras.Bonus + plan.Bonus);
		extras.FeatureUpdates++;
		extras.FeaturesAdded += fresh.Count;
		return name + " gets " + string.Join(", ", fresh.Select((int j) => gf.GetName(j))) + ".\n\nGameplay +" + Mathf.RoundToInt(plan.Gameplay) + ", and its sales " + Pct(plan.Bonus) + " for as long as it's on the market." + ((poor > 0) ? " Some players think it doesn't suit the genre." : "");
	}

	internal static string CompleteRating(UpdateProject p, gameScript g, GameExtras extras, string name)
	{
		if (p.Usk < 0 || p.Usk >= g.usk)
		{
			return null;
		}
		int steps = g.usk - p.Usk;
		int old = g.usk;
		g.usk = p.Usk;
		g.points_gameplay = Mathf.Max(0f, g.points_gameplay * (1f - 0.03f * (float)steps));
		g.userNegativ += Mathf.RoundToInt((float)(g.userPositiv + g.userNegativ) * 0.03f * (float)steps);
		extras.RatingEdits++;
		return name + " is now rated " + UskName(g.usk) + " (was " + UskName(old) + ").\n\nMore players can buy it: sales " + Pct(UskSales(g.usk) - UskSales(old)) + ". Some fans don't like the cut content.";
	}

	// ---- Weekly ----

	[HarmonyPostfix]
	[HarmonyPatch(typeof(mainScript), "WochenUpdates")]
	private static void Weekly()
	{
		if (!G.Singleplayer || S.Projects.Count == 0)
		{
			return;
		}
		try
		{
			List<roomScript> rooms = CopyProtectDev.Rooms();
			foreach (UpdateProject p in S.Projects.ToList())
			{
				gameScript g = FindGame(p.GameId);
				bool roomOk = rooms.Any((roomScript r) => r.taskID == p.TaskId);
				bool gameOk = IsStudio(p.Kind) || ((bool)g && g.isOnMarket);
				if (!gameOk || !roomOk)
				{
					long refund = Cancel(p);
					if ((bool)G.Gui)
					{
						G.Gui.CreateTopNewsInfo(p.Title + " stopped: " + ((!gameOk) ? "the game is off the market." : "its room is gone.") + ((refund > 0) ? (" " + G.Money(refund) + " came back.") : ""));
					}
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Weekly update check failed: " + ex);
		}
	}

	// ---- Sales ----

	public static float SalesFactor(gameScript g)
	{
		if (!g || !g.isOnMarket || !g.IsMyGame())
		{
			return 1f;
		}
		GameExtras e = Extras(g.myID);
		if (e == null)
		{
			return 1f;
		}
		if ((g.gameTyp == 1) ? Mmo.MmoOps.On : On)
		{
			return 1f + e.Bonus + LaunchNow(e);
		}
		// A timed exclusive's second launch keeps its boost even with the extra update types switched off.
		if (TimedExclusives.On)
		{
			return 1f + LaunchNow(e);
		}
		return 1f;
	}

	public static float LaunchNow(GameExtras e)
	{
		int weeks = CopyProtectDev.AbsWeek - e.LaunchWeek;
		if (weeks < 0 || weeks >= LaunchWeeks)
		{
			return 0f;
		}
		return e.LaunchBoost * (1f - (float)weeks / (float)LaunchWeeks);
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(gameScript), "SellGame")]
	private static void BeforeSell(gameScript __instance)
	{
		selling = (HolyCompat.SalesBonusShared ? 1f : OurSalesFactor(__instance));
	}

	// All of the addon's sales effects on one game: update bonuses and launch boosts, MMO players, the store, and
	// the sales events (added, as Holy adds its own seasonal sale). With Holy installed they go into Holy's sales
	// bonus total instead (and its cap), see HolyCompat.
	public static float OurSalesFactor(gameScript g)
	{
		return SalesFactor(g) * Mmo.MmoOps.NewPlayerFactor(g) * Store.StoreOps.SalesFactor(g) + Store.SalesEvents.Bonus(g) + Events.StudioCon.Bonus(g) + Industry.IndustryCore.Bonus(g);
	}

	[HarmonyFinalizer]
	[HarmonyPatch(typeof(gameScript), "SellGame")]
	private static Exception AfterSell(Exception __exception)
	{
		selling = 1f;
		return __exception;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(games), "GetSells")]
	private static void Sells(ref float __result)
	{
		__result *= selling;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(games), "GetSellsArcade")]
	private static void SellsArcade(ref float __result)
	{
		__result *= selling;
	}

	// The game's tooltip mentions what the addon's updates did.
	[HarmonyPostfix]
	[HarmonyPatch(typeof(gameScript), "GetTooltip")]
	private static void Tooltip(gameScript __instance, ref string __result)
	{
		try
		{
			GameExtras e = (On ? Extras(__instance.myID) : null);
			if (e == null)
			{
				return;
			}
			float total = e.Bonus + LaunchNow(e);
			List<string> done = new List<string>();
			if (e.Ports > 0)
			{
				done.Add((e.Ports == 1) ? "ported" : ("ported " + e.Ports + "x"));
			}
			if (e.EngineUpdates > 0)
			{
				done.Add("engine updated");
			}
			if (e.FeatureUpdates > 0)
			{
				done.Add("new features");
			}
			if (e.RatingEdits > 0)
			{
				done.Add("rating edited");
			}
			if (done.Count > 0 || total > 0f)
			{
				__result = __result + "\n<color=#2e7d32>" + string.Join(", ", done) + ((total > 0.005f) ? (" (sales " + Pct(total) + ")") : "") + "</color>";
			}
		}
		catch (Exception)
		{
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
			Plugin.Log.LogError("Saving updates failed: " + ex);
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(savegameScript), "Load")]
	private static void BeforeLoad()
	{
		S = new UpdatesState();
		UpdateRoom.ClearTasks();
		Version++;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(savegameScript), "LoadHistory")]
	private static void Load(ES3File ___es3file)
	{
		try
		{
			S = ((___es3file != null && ___es3file.KeyExists(SaveKey)) ? JsonConvert.DeserializeObject<UpdatesState>(___es3file.Load<string>(SaveKey)) : null) ?? new UpdatesState();
			S.Projects = S.Projects ?? new List<UpdateProject>();
			S.Games = S.Games ?? new Dictionary<int, GameExtras>();
			// Saves from 0.8.0 only counted feature updates; each one added at least one feature.
			foreach (GameExtras e in S.Games.Values)
			{
				if (e != null && e.FeatureUpdates > 0 && e.FeaturesAdded <= 0)
				{
					e.FeaturesAdded = e.FeatureUpdates;
				}
			}
			foreach (UpdateProject p in S.Projects)
			{
				UpdateRoom.Restore(p);
			}
			if (S.Projects.Count > 0 || S.Games.Count > 0)
			{
				Plugin.Log.LogInfo("Updates loaded: " + S.Projects.Count + " running, " + S.Games.Count + " games updated");
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Loading updates failed: " + ex);
			S = new UpdatesState();
		}
		Version++;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(mainScript), "InitNewGame")]
	private static void NewGame()
	{
		S = new UpdatesState();
		UpdateRoom.ClearTasks();
		Version++;
	}
}
