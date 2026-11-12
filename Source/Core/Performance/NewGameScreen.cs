using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.Core.Performance;

// A faster new-game screen (Menu_DevGame).
//
// The idea comes from Aerin the Lion's "Unofficial MGT2 Performance Patch" (MIT licence, 2024), which found that
// the screen's gameplay feature list is slow to build. This goes further:
//
// 1. Building the list (opening the screen, changing the platform type, each search) "clicks" every researched
//    feature twice, so that a feature the chosen platform type can't have gets switched off. Each click
//    recalculates every cost on the screen, so with a hundred or more features that's hundreds of full
//    recalculations. The list is now built without the clicks, with the same result, and recalculated once.
//    (The original patch kept the clicks and moved one of the recalculations.)
// 2. The cost and dev-point sums look each platform up by name with GameObject.Find, which searches the whole
//    scene: a few times per platform, and again for every extra language you've ticked. Those lookups now come
//    from a cache (checked on every use), which speeds up every click on the screen, not just the list.
//
// Skipped if the original patch is installed too: its list builder runs instead, and still gets the faster lookups.
[HarmonyPatch]
internal static class NewGameScreen
{
	public const string OriginalPatchGuid = "me.Aerin.MGT2mod.UnofficialMGT2PerformancePatch";

	private static readonly AccessTools.FieldRef<Menu_DevGame, gameplayFeatures> gF = AccessTools.FieldRefAccess<Menu_DevGame, gameplayFeatures>("gF_");

	private static readonly AccessTools.FieldRef<Menu_DevGame, mainScript> mS = AccessTools.FieldRefAccess<Menu_DevGame, mainScript>("mS_");

	private static readonly AccessTools.FieldRef<Menu_DevGame, textScript> tS = AccessTools.FieldRefAccess<Menu_DevGame, textScript>("tS_");

	private static readonly AccessTools.FieldRef<Menu_DevGame, sfxScript> sfx = AccessTools.FieldRefAccess<Menu_DevGame, sfxScript>("sfx_");

	private static readonly AccessTools.FieldRef<Menu_DevGame, GUI_Main> gui = AccessTools.FieldRefAccess<Menu_DevGame, GUI_Main>("guiMain_");

	private static readonly AccessTools.FieldRef<Menu_DevGame, string> search = AccessTools.FieldRefAccess<Menu_DevGame, string>("searchStringA");

	private static readonly Action<Menu_DevGame> findScripts = AccessTools.MethodDelegate<Action<Menu_DevGame>>(AccessTools.Method(typeof(Menu_DevGame), "FindScripts"));

	private static readonly Func<Menu_DevGame, int> calcDevCosts = AccessTools.MethodDelegate<Func<Menu_DevGame, int>>(AccessTools.Method(typeof(Menu_DevGame), "CalcDevCosts"));

	private static readonly Func<Menu_DevGame, int> updateFeatureCount = AccessTools.MethodDelegate<Func<Menu_DevGame, int>>(AccessTools.Method(typeof(Menu_DevGame), "UpdateGesamtGameplayFeatures"));

	private static bool? originalPatch;

	private static bool failed;

	private static int timed;

	private static bool OriginalPatchLoaded
	{
		get
		{
			if (!originalPatch.HasValue)
			{
				originalPatch = Chainloader.PluginInfos.ContainsKey(OriginalPatchGuid);
				if (originalPatch.Value)
				{
					Plugin.Log.LogInfo("Unofficial MGT2 Performance Patch is installed: it builds the gameplay feature list, the addon only speeds up the screen's platform lookups.");
				}
			}
			return originalPatch.Value;
		}
	}

	public static bool On => Plugin.FasterNewGameScreen.Value;

	// ---- 1. The gameplay feature list ----

	[HarmonyPrefix]
	[HarmonyPriority(Priority.High)]
	[HarmonyPatch(typeof(Menu_DevGame), "Init_GameplayFeatures")]
	private static bool BuildFeatureList(Menu_DevGame __instance, bool __runOriginal)
	{
		if (!__runOriginal)
		{
			return false;
		}
		if (!On || failed || OriginalPatchLoaded)
		{
			return true;
		}
		try
		{
			Stopwatch watch = Stopwatch.StartNew();
			Menu_DevGame m = __instance;
			findScripts(m);
			gameplayFeatures features = gF(m);
			GUI_Main g = gui(m);
			if (m.g_GameGameplayFeatures.Length == 0)
			{
				m.g_GameGameplayFeatures = new bool[features.gameplayFeatures_LEVEL.Length];
			}
			Transform list = m.uiObjects[120].transform;
			for (int i = 0; i < list.childCount; i++)
			{
				UnityEngine.Object.Destroy(list.GetChild(i).gameObject);
			}
			bool filter = m.uiObjects[204].GetComponent<InputField>().text.Length > 0;
			string wanted = (search(m) ?? "").ToLower();
			search(m) = wanted;
			int platformType = m.uiObjects[146].GetComponent<Dropdown>().value;
			int shown = 0;
			for (int j = 0; j < features.gameplayFeatures_LEVEL.Length; j++)
			{
				if (!features.IsErforscht(j) || (filter && !features.GetName(j).ToLower().Contains(wanted)))
				{
					continue;
				}
				Item_DevGame_GameplayFeature item = UnityEngine.Object.Instantiate(m.uiPrefabs[0], Vector3.zero, Quaternion.identity, list).GetComponent<Item_DevGame_GameplayFeature>();
				item.myID = j;
				item.mS_ = mS(m);
				item.tS_ = tS(m);
				item.sfx_ = sfx(m);
				item.guiMain_ = g;
				item.gF_ = features;
				item.menuDevGame_ = m;
				// What the game's two clicks per feature came to: a feature this platform type (arcade, handheld)
				// can't have is switched off, anything else stays as it was.
				if ((platformType == 4 && features.gameplayFeatures_LOCKPLATFORM[j, 4]) || (platformType == 5 && features.gameplayFeatures_LOCKPLATFORM[j, 3]))
				{
					m.g_GameGameplayFeatures[j] = false;
				}
				item.GetComponent<Image>().color = (m.g_GameGameplayFeatures[j] ? g.colors[4] : Color.white);
				shown++;
			}
			m.DROPDOWN_SortGameplayFeatures();
			g.KeinEintrag(m.uiObjects[120], m.uiObjects[121]);
			calcDevCosts(m);
			m.GetGesamtDevPoints();
			updateFeatureCount(m);
			if (timed < 3)
			{
				timed++;
				Plugin.Log.LogInfo("New-game screen: listed " + shown + " gameplay features in " + watch.ElapsedMilliseconds + " ms");
			}
			return false;
		}
		catch (Exception ex)
		{
			failed = true;
			Plugin.Log.LogError("Faster gameplay feature list failed, the game's own is used from now on: " + ex);
			return true;
		}
	}

	// ---- 2. Platform lookups by name ----

	// The screen's methods that look platforms up by name (GameObject.Find("PLATFORM_" + id)) and run on every click.
	[HarmonyPatch]
	private static class Lookups
	{
		private static readonly string[] Names = new string[7] { "CalcDevCosts_Sonstiges", "GetGesamtDevPoints", "GetGesamtMarktanteil", "GetPlatformTechLevel", "GetExklusivGameBonus", "SetPlatform", "DROPDOWN_PlattformTyp" };

		private static IEnumerable<MethodBase> TargetMethods()
		{
			foreach (string name in Names)
			{
				MethodInfo method = AccessTools.GetDeclaredMethods(typeof(Menu_DevGame)).FirstOrDefault((MethodInfo x) => x.Name == name);
				if (method != null)
				{
					yield return method;
				}
				else
				{
					Plugin.Log.LogWarning("New-game screen: " + name + " not found, its lookups stay as they are");
				}
			}
		}

		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			MethodInfo find = AccessTools.Method(typeof(GameObject), "Find", new Type[1] { typeof(string) });
			MethodInfo cached = AccessTools.Method(typeof(NewGameScreen), "Find");
			foreach (CodeInstruction ci in instructions)
			{
				if (ci.Calls(find))
				{
					ci.opcode = System.Reflection.Emit.OpCodes.Call;
					ci.operand = cached;
				}
				yield return ci;
			}
		}
	}

	// GameObject.Find, remembered (SceneLookup).
	public static GameObject Find(string name)
	{
		if (!On)
		{
			return GameObject.Find(name);
		}
		return SceneLookup.Find(name);
	}
}
