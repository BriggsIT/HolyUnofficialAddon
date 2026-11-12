using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HolyAddon.Core.CopyProtection;
using HolyAddon.Core.Updates;
using UnityEngine;

namespace HolyAddon.Core.Mmo;

// The MMO autopilot: once a week it starts the routine projects you allowed for each MMO (ban waves, stability
// patches, content patches, classic phases, welcome-back campaigns) and updates your anti-bot systems, in free
// development rooms with staff, never going below your money floor and leaving the rooms you asked for free.
// One project per MMO at a time, and a news line each time it starts one.
[HarmonyPatch]
internal static class MmoAutopilot
{
	public static readonly long[] MoneyFloors = new long[7] { 0L, 100000L, 500000L, 1000000L, 5000000L, 10000000L, 50000000L };

	public static readonly int[] BustSteps = new int[5] { 1, 2, 4, 6, 8 };

	public static readonly int[] BugSteps = new int[5] { 3, 5, 10, 25, 50 };

	public static readonly int[] AntiBotSteps = new int[5] { 0, 50, 60, 70, 80 };

	private static MmoState S => MmoOps.State;

	public static MmoAutoStudio Rules => S.Autopilot ?? (S.Autopilot = new MmoAutoStudio());

	public static bool IsOn(MmoGame m)
	{
		if (m?.Auto != null)
		{
			return m.Auto.On;
		}
		return false;
	}

	public static MmoAuto For(MmoGame m)
	{
		return m.Auto ?? (m.Auto = new MmoAuto());
	}

	[HarmonyPostfix]
	[HarmonyPriority(Priority.Low)]
	[HarmonyPatch(typeof(mainScript), "WochenUpdates")]
	private static void Weekly()
	{
		if (!MmoOps.On)
		{
			return;
		}
		try
		{
			Run();
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("MMO autopilot: " + ex);
		}
	}

	private static void Run()
	{
		MmoAutoStudio rules = Rules;
		foreach (gameScript g in MmoOps.MyMmos())
		{
			MmoGame m = MmoOps.Get(g);
			if (!IsOn(m) || GameUpdates.ProjectFor(g.myID) != null)
			{
				continue;
			}
			foreach (UpdateKind kind in Wanted(g, m))
			{
				UpdatePlan plan = PlanFor(kind, g);
				if (plan == null || plan.Problem != null)
				{
					continue;
				}
				TryStart(new UpdateProject
				{
					Kind = kind
				}, plan, g, rules);
				break;
			}
		}
		// Your anti-bot systems: only while the autopilot runs at least one MMO, and one update a week at most
		// (the weakest system first), so it never fills your development rooms by surprise.
		if (rules.AntiBotAt <= 0 || !MmoBots.On || !AnyOn())
		{
			return;
		}
		foreach (AntiBotSystem a in MmoBots.Systems.OrderBy((AntiBotSystem x) => x.Strength).ToList())
		{
			if (a.Source == -1 && !a.Retired && a.Strength < (float)rules.AntiBotAt)
			{
				UpdatePlan plan2 = MmoBots.PlanUpdate(a);
				if (plan2.Problem == null && TryStart(new UpdateProject
				{
					Kind = UpdateKind.AntiBotUpdate,
					SystemId = a.Id,
					Title = "Anti-bot update: " + a.Name
				}, plan2, null, rules))
				{
					break;
				}
			}
		}
	}

	// The autopilot runs at least one of your MMOs.
	public static bool AnyOn()
	{
		return MmoOps.MyMmos().Any((gameScript g) => IsOn(MmoOps.Get(g)));
	}

	// What this MMO needs, most urgent first.
	private static IEnumerable<UpdateKind> Wanted(gameScript g, MmoGame m)
	{
		MmoAuto auto = m.Auto;
		if (auto.Bust && MmoBots.On && MmoBots.Share(g, m) * 100f >= (float)auto.BustAt)
		{
			yield return UpdateKind.MmoBotBust;
		}
		if (auto.Stability && g.points_bugs >= (float)auto.StabilityAt)
		{
			yield return UpdateKind.MmoStability;
		}
		if (auto.Patch)
		{
			yield return UpdateKind.MmoPatch;
		}
		if (auto.ClassicPhase && m.ClassicOn)
		{
			yield return UpdateKind.MmoClassicPhase;
		}
		if (auto.Welcome)
		{
			yield return UpdateKind.MmoWelcomeBack;
		}
	}

	private static UpdatePlan PlanFor(UpdateKind kind, gameScript g)
	{
		if (kind == UpdateKind.MmoPatch)
		{
			return MmoOps.PlanPatch(g);
		}
		return MmoLive.Plan(kind, g, 0);
	}

	// The free rooms it may use: free development rooms with staff, fastest first, after the ones it leaves for you.
	private static roomScript PickRoom(MmoAutoStudio rules)
	{
		List<roomScript> free = (from r in CopyProtectDev.FreeRooms()
			where CopyProtectDev.Staff(r).Count > 0
			select r).ToList();
		if (free.Count <= rules.KeepFree)
		{
			return null;
		}
		return free[Mathf.Clamp(rules.KeepFree, 0, free.Count - 1)];
	}

	private static bool TryStart(UpdateProject p, UpdatePlan plan, gameScript g, MmoAutoStudio rules)
	{
		if (!G.MS || G.MS.money - plan.Cost < rules.MoneyFloor)
		{
			return false;
		}
		roomScript room = PickRoom(rules);
		if (!room)
		{
			return false;
		}
		string error = GameUpdates.Start(p, plan, room, g);
		if (error != null)
		{
			return false;
		}
		if ((bool)G.Gui)
		{
			G.Gui.CreateTopNewsInfo("Autopilot: " + p.Title + " started in " + CopyProtectDev.RoomName(room) + " (" + G.Money(plan.Cost) + ").");
		}
		return true;
	}
}
