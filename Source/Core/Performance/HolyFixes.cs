using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HolyAddon.Core.Performance;

// Speed-ups for The Holy Expansion, found with the stutter finder (jay is happy for the addon to adjust Holy).
//
// Studio support (HolyExpansion.Systems.StudioSupport): once a second, for every studio-support link, Holy looks
// up both rooms with RoomTasks.FindRoom, which searches the whole scene (FindObjectsOfType<roomScript>), and the
// studio's and team's tasks with GameObject.Find. With four links that took 80-370 ms every second, even while
// paused: most of the game's regular stutter. Rooms now come from the game's room list and tasks from a lookup
// that remembers them; the answers are the same, so studio support works exactly as before.
//
// Everything is looked up by name, so nothing happens without Holy (or if a later Holy renames these).
internal static class HolyFixes
{
	public static bool On => Plugin.PerformanceTweaks.Value;

	[HarmonyPatch]
	private static class FindRoom
	{
		private static MethodBase Target => AccessTools.Method("HolyExpansion.Systems.RoomTasks:FindRoom", new System.Type[1] { typeof(int) });

		private static bool Prepare()
		{
			return Target != null;
		}

		private static MethodBase TargetMethod()
		{
			return Target;
		}

		private static bool Prefix(int id, ref roomScript __result)
		{
			if (!On || id < 0)
			{
				return true;
			}
			roomScript r = SceneLookup.Room(id);
			if (!r)
			{
				// Not in the room list (built this very frame, or gone): Holy's own search decides.
				return true;
			}
			__result = r;
			return false;
		}
	}

	[HarmonyPatch]
	private static class TaskObject
	{
		private static MethodBase Target => AccessTools.Method("HolyExpansion.Systems.StudioSupport:TaskObject", new System.Type[1] { typeof(roomScript) });

		private static bool Prepare()
		{
			return Target != null;
		}

		private static MethodBase TargetMethod()
		{
			return Target;
		}

		private static bool Prefix(roomScript r, ref GameObject __result)
		{
			if (!On)
			{
				return true;
			}
			__result = ((!r || r.taskID == -1) ? null : SceneLookup.Find("Task_" + r.taskID));
			return false;
		}
	}
}
