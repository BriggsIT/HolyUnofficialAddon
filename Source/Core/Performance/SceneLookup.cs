using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HolyAddon.Core.Performance;

// GameObject.Find, remembered. GameObject.Find walks the whole scene every time; here what it found last time is
// used again while it's still an active object of that name, so the answer is the same as the game's own.
[HarmonyPatch]
internal static class SceneLookup
{
	private static readonly Dictionary<string, GameObject> found = new Dictionary<string, GameObject>(StringComparer.Ordinal);

	public static GameObject Find(string name)
	{
		if (name == null)
		{
			return GameObject.Find(name);
		}
		if (found.TryGetValue(name, out var go) && (bool)go && go.activeInHierarchy && go.name == name)
		{
			return go;
		}
		go = GameObject.Find(name);
		if ((bool)go)
		{
			found[name] = go;
		}
		else
		{
			found.Remove(name);
		}
		return go;
	}

	// A room by id from the game's room list (it refreshes the list the frame after rooms change). Null if it
	// isn't in the list.
	public static roomScript Room(int id)
	{
		roomScript[] all = (G.MS ? G.MS.arrayRoomScripts : null);
		if (all == null || id < 0)
		{
			return null;
		}
		foreach (roomScript r in all)
		{
			if ((bool)r && r.myID == id)
			{
				return r;
			}
		}
		return null;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(savegameScript), "Load")]
	private static void ForgetOnLoad()
	{
		found.Clear();
	}
}
