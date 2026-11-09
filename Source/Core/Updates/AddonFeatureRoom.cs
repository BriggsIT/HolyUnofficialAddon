using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace HolyAddon.Core.Updates;

// The paid addon and MMO addon menus check base game features + new ones against the game size's limit
// (Menu_DevGame.maxFeatures_gameSize). While they count and when you press start, that limit is raised by the
// features the addon's updates added to the base game, so those don't use up the addon's room.
[HarmonyPatch]
internal static class AddonFeatureRoom
{
	private class Raised
	{
		public int[] Caps;

		public int Index;

		public int Amount;
	}

	// BUTTON_Start calls the counting method itself; only the outermost call raises the limit.
	private static int depth;

	private static IEnumerable<MethodBase> TargetMethods()
	{
		foreach (Type t in new Type[2]
		{
			typeof(Menu_Dev_AddonDo),
			typeof(Menu_Dev_MMOAddon)
		})
		{
			yield return AccessTools.Method(t, "UpdateGesamtGameplayFeatures");
			yield return AccessTools.Method(t, "BUTTON_Start");
		}
	}

	private static void Prefix(object __instance, out Raised __state)
	{
		__state = null;
		if (depth++ > 0)
		{
			return;
		}
		try
		{
			Traverse menu = Traverse.Create(__instance);
			gameScript g = menu.Field("gS_").GetValue<gameScript>();
			Menu_DevGame dev = menu.Field("menuDevGame_").GetValue<Menu_DevGame>();
			if (!g || !dev || dev.maxFeatures_gameSize == null || g.gameSize < 0 || g.gameSize >= dev.maxFeatures_gameSize.Length)
			{
				return;
			}
			int extra = GameUpdates.AddonFeatureAllowance(g, dev.maxFeatures_gameSize[g.gameSize]);
			if (extra > 0)
			{
				dev.maxFeatures_gameSize[g.gameSize] += extra;
				__state = new Raised
				{
					Caps = dev.maxFeatures_gameSize,
					Index = g.gameSize,
					Amount = extra
				};
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Addon feature limit: " + ex);
		}
	}

	private static Exception Finalizer(Exception __exception, Raised __state)
	{
		depth = Math.Max(0, depth - 1);
		if (__state != null)
		{
			__state.Caps[__state.Index] -= __state.Amount;
		}
		return __exception;
	}
}
