using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.Core;

// Bug points are a fraction, and a game card shows its bug icon for anything above zero. The game's free update
// rounds the bugs it can fix to the nearest whole one, so a game left with less than half a bug (after a partial
// fix by an update, a contract or the addon's projects) kept its bug icon for good with nothing to fix. A bug
// that's left over now counts as one: the free update can fix it, and the addon shows it.
[HarmonyPatch]
internal static class LeftoverBugs
{
	private static readonly AccessTools.FieldRef<Menu_Dev_Update, gameScript> UpdateGame = AccessTools.FieldRefAccess<Menu_Dev_Update, gameScript>("gS_");

	private static readonly Func<Menu_Dev_Update, int> FixedBugs = AccessTools.MethodDelegate<Func<Menu_Dev_Update, int>>(AccessTools.Method(typeof(Menu_Dev_Update), "GetP_Bugs"));

	// The bugs as the player sees them: any fraction left counts as a bug (the card shows its icon for it).
	public static int Shown(gameScript g)
	{
		if (!g || g.points_bugs <= 0f)
		{
			return 0;
		}
		return Mathf.Max(1, Mathf.CeilToInt(g.points_bugs - 0.001f));
	}

	public static string Text(gameScript g)
	{
		int n = Shown(g);
		return n switch
		{
			0 => "no bugs",
			1 => "1 bug",
			_ => n + " bugs",
		};
	}

	// After a partial fix: less than one bug left means none.
	public static void Tidy(gameScript g)
	{
		if ((bool)g && g.points_bugs < 1f)
		{
			g.points_bugs = 0f;
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_Dev_Update), "GetP_Bugs")]
	private static void FreeUpdateFixes(Menu_Dev_Update __instance, ref int __result)
	{
		try
		{
			gameScript g = UpdateGame(__instance);
			if (!g || g.points_bugs <= 0f)
			{
				return;
			}
			Slider slider = ((__instance.uiObjects != null && __instance.uiObjects.Length > 6 && (bool)__instance.uiObjects[6]) ? __instance.uiObjects[6].GetComponent<Slider>() : null);
			float share = (slider ? (slider.value * 0.1f) : 1f);
			if (share <= 0f)
			{
				return;
			}
			int fix = Mathf.Min(Shown(g), Mathf.CeilToInt(g.points_bugs * share - 0.001f));
			if (fix > __result)
			{
				__result = fix;
			}
		}
		catch (Exception)
		{
		}
	}

	// The update screen's "bugs now - fixed" line rounds the bugs too.
	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_Dev_Update), "UpdateGUI")]
	private static void FreeUpdateText(Menu_Dev_Update __instance)
	{
		try
		{
			gameScript g = UpdateGame(__instance);
			if (!g || g.points_bugs <= 0f || Mathf.RoundToInt(g.points_bugs) == Shown(g))
			{
				return;
			}
			Text text = __instance.uiObjects[5].GetComponent<Text>();
			if ((bool)text)
			{
				text.text = Shown(g) + "-" + FixedBugs(__instance);
			}
		}
		catch (Exception)
		{
		}
	}
}
