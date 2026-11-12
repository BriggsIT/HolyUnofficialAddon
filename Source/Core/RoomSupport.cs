using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HolyAddon.Core.CopyProtection;
using HolyAddon.Core.Updates;
using UnityEngine;

namespace HolyAddon.Core;

// Rooms that support another room (the game's "Support another room": same room type, each supporting one room).
// Their staff already work on the supported room's job, including Holy's room tasks and the addon's projects; this
// makes the estimates count them, and shows those jobs' progress in the supporting room's window.
[HarmonyPatch]
internal static class RoomSupport
{
	private static readonly AccessTools.FieldRef<roomScript, roomButtonScript> RoomButtons = AccessTools.FieldRefAccess<roomScript, roomButtonScript>("rbS_");

	private static MethodInfo holyRate;

	private static bool inHolyRate;

	// Who supports whom, worked out once per frame for all rooms (estimates and windows ask often).
	private static readonly Dictionary<int, List<roomScript>> supporters = new Dictionary<int, List<roomScript>>();

	private static int mappedFrame = -1;

	private static readonly List<roomScript> none = new List<roomScript>();

	public static List<roomScript> Supporters(roomScript target)
	{
		if (!target)
		{
			return none;
		}
		if (mappedFrame != Time.frameCount)
		{
			mappedFrame = Time.frameCount;
			supporters.Clear();
			roomScript[] rooms = (G.MS ? G.MS.arrayRoomScripts : null);
			if (rooms != null)
			{
				foreach (roomScript r in rooms)
				{
					if (!r || !r.taskGameObject)
					{
						continue;
					}
					taskUnterstuetzen t = r.taskGameObject.GetComponent<taskUnterstuetzen>();
					if ((bool)t && (bool)t.rS_ && t.rS_ != r && t.rS_.typ == r.typ && t.myID == r.taskID)
					{
						if (!supporters.TryGetValue(t.rS_.myID, out var list))
						{
							list = (supporters[t.rS_.myID] = new List<roomScript>());
						}
						list.Add(r);
					}
				}
			}
		}
		if (!supporters.TryGetValue(target.myID, out var found))
		{
			return none;
		}
		return found;
	}

	// Holy's estimates (RoomTasks.Rate, behind every "about N weeks" of its room tasks and the convention's) count
	// the rooms supporting the room too.
	public static void Apply(Harmony harmony)
	{
		try
		{
			holyRate = AccessTools.Method("HolyExpansion.Systems.RoomTasks:Rate", new Type[1] { typeof(roomScript) });
			if (holyRate != null)
			{
				harmony.Patch(holyRate, null, new HarmonyMethod(typeof(RoomSupport), "HolyRate"));
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Could not count supporting rooms in Holy's estimates: " + ex.Message);
		}
	}

	private static void HolyRate(roomScript r, ref float __result)
	{
		if (inHolyRate || !r)
		{
			return;
		}
		inHolyRate = true;
		try
		{
			foreach (roomScript s in Supporters(r))
			{
				__result += (float)holyRate.Invoke(null, new object[1] { s });
			}
		}
		catch
		{
		}
		finally
		{
			inHolyRate = false;
		}
	}

	// How far the room's own job is (0-1) when it's one of the addon's projects or a Holy room task; null otherwise.
	public static float? Progress(roomScript room)
	{
		if (!room || !room.taskGameObject || room.taskID == -1)
		{
			return null;
		}
		UpdateProject p = GameUpdates.ProjectInRoom(room);
		if (p != null)
		{
			return p.Fraction;
		}
		CopyProtectProject cp = CopyProtectDev.Project;
		if (cp != null && cp.TaskId == room.taskID)
		{
			return cp.Fraction;
		}
		return HolyProgress(room);
	}

	// Holy's room task for the room (its RoomTaskData), or null.
	public static object HolyTask(roomScript room)
	{
		if (!room || room.taskID == -1 || !(HolyCompat.HolyState("RoomTasks") is IList all))
		{
			return null;
		}
		foreach (object d in all)
		{
			if (HolyCompat.Get<int>(d, "TaskId") == room.taskID)
			{
				return d;
			}
		}
		return null;
	}

	private static float? HolyProgress(roomScript room)
	{
		try
		{
			object d = HolyTask(room);
			if (d != null)
			{
				return Mathf.Clamp01(HolyCompat.Get<float>(d, "Progress") / Mathf.Max(1f, HolyCompat.Get<float>(d, "Needed")));
			}
		}
		catch
		{
		}
		return null;
	}

	// The supporting room's window shows "-" for jobs the game doesn't know; show their progress instead. This runs
	// every frame for every supporting room, so it only goes further for a job the game can't measure (the addon's
	// projects and Holy's room tasks), found by the task object's component.
	[HarmonyPostfix]
	[HarmonyPatch(typeof(roomScript), "UpdateWindowUnterstuetzen")]
	private static void SupportWindow(roomScript __instance, bool show)
	{
		if (!show || !__instance.taskGameObject)
		{
			return;
		}
		try
		{
			taskUnterstuetzen t = __instance.taskGameObject.GetComponent<taskUnterstuetzen>();
			roomScript target = (t ? t.rS_ : null);
			if (!target || !target.taskGameObject)
			{
				return;
			}
			GameObject job = target.taskGameObject;
			Type holyTask = HolyCompat.HolyType("HolyExpansion.Systems.HolyRoomTask");
			if (!job.GetComponent<UpdateTask>() && !job.GetComponent<CopyProtectTask>() && (holyTask == null || !job.GetComponent(holyTask)))
			{
				return;
			}
			float? progress = Progress(target);
			if (!progress.HasValue)
			{
				return;
			}
			roomButtonScript rb = RoomButtons(__instance);
			GameObject window = ((rb && rb.uiWindows != null && rb.uiWindows.Length > 3) ? rb.uiWindows[3] : null);
			roomWindow rw = (window ? window.GetComponent<roomWindow>() : null);
			if (!rw)
			{
				return;
			}
			roomDataScript rd = (G.MainObj ? G.MainObj.GetComponent<roomDataScript>() : null);
			string name = ((target.myName.Length > 0) ? target.myName : (rd ? rd.GetName(target.typ) : ""));
			rw.Window_Unterstuetzen(name, progress.Value * 100f);
		}
		catch
		{
		}
	}
}
