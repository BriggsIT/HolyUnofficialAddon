using System;
using System.Collections.Generic;
using System.Threading;
using HarmonyLib;
using UnityEngine;

namespace HolyAddon.Core.Performance;

// Small fixes to the game's own busiest code, found with the stutter finder. Same results, less work.
//
// Money text (mainScript.GetMoney): the HUD, cards and lists format money and numbers about 7,500 times a second.
// The game builds each one a character at a time ("1" + ",234" + ...), about a dozen throwaway strings a call, and
// all that garbage makes the garbage collector pause the game every few seconds. Here the text is built in one go
// and kept: the same numbers are shown frame after frame.
//
// Game list (games.FindGames): every new game, yours or a rival's, makes the game search the whole scene for all
// its games again (~13 times an in-game week, more with time). A new game is now added to the list instead.
[HarmonyPatch]
internal static class GameTweaks
{
	public static bool On => Plugin.PerformanceTweaks.Value;

	private static int mainThread = -1;

	public static void Init()
	{
		mainThread = Thread.CurrentThread.ManagedThreadId;
	}

	// ---- Money text ----

	private static readonly Dictionary<long, string> plain = new Dictionary<long, string>();

	private static readonly Dictionary<long, string> withSymbol = new Dictionary<long, string>();

	private static int cacheLanguage = int.MinValue;

	private static string cacheSymbol;

	private static readonly char[] buffer = new char[32];

	private const int CacheLimit = 4096;

	[HarmonyPrefix]
	[HarmonyPatch(typeof(mainScript), "GetMoney")]
	private static bool Money(mainScript __instance, long i, bool showDollar, ref string __result)
	{
		if (!On || Thread.CurrentThread.ManagedThreadId != mainThread || !__instance.tS_ || !__instance.settings_)
		{
			return true;
		}
		int language = __instance.settings_.language;
		if (language != cacheLanguage)
		{
			plain.Clear();
			withSymbol.Clear();
			cacheLanguage = language;
			cacheSymbol = null;
		}
		Dictionary<long, string> cache = (showDollar ? withSymbol : plain);
		if (cache.TryGetValue(i, out var text))
		{
			__result = text;
			return false;
		}
		text = Format(i, (language == 0) ? ',' : '.');
		if (showDollar)
		{
			if (cacheSymbol == null)
			{
				cacheSymbol = __instance.tS_.GetText(7);
			}
			text = cacheSymbol + text;
		}
		if (cache.Count >= CacheLimit)
		{
			cache.Clear();
		}
		cache[i] = text;
		__result = text;
		return false;
	}

	// The game's format: digits in groups of three from the right (1,234,567 in English, 1.234.567 otherwise),
	// a minus in front of negative amounts.
	public static string Format(long i, char separator)
	{
		ulong v = ((i < 0) ? ((ulong)(-(i + 1)) + 1) : ((ulong)i));
		int p = buffer.Length;
		int digits = 0;
		do
		{
			if (digits > 0 && digits % 3 == 0)
			{
				buffer[--p] = separator;
			}
			buffer[--p] = (char)(48 + (int)(v % 10));
			v /= 10;
			digits++;
		}
		while (v != 0);
		if (i < 0)
		{
			buffer[--p] = '-';
		}
		return new string(buffer, p, buffer.Length - p);
	}

	// ---- Game list ----

	private static int creating;

	[HarmonyPrefix]
	[HarmonyPatch(typeof(games), "CreateNewGame")]
	private static void BeforeCreate(bool fromSavegame, out bool __state)
	{
		__state = On && !fromSavegame && Thread.CurrentThread.ManagedThreadId == mainThread;
		if (__state)
		{
			creating++;
		}
	}

	// The scan the new game would have caused is skipped; the game is added below instead.
	[HarmonyPrefix]
	[HarmonyPatch(typeof(games), "FindGames")]
	private static bool Scan()
	{
		return creating == 0;
	}

	[HarmonyFinalizer]
	[HarmonyPatch(typeof(games), "CreateNewGame")]
	private static Exception AfterCreate(games __instance, gameScript __result, Exception __exception, bool __state)
	{
		if (!__state)
		{
			return __exception;
		}
		creating--;
		try
		{
			if (__exception != null || !Add(__instance, __result))
			{
				__instance.FindGames();
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Adding a new game to the game list failed, so the game searches for it as usual: " + ex.Message);
			__instance.FindGames();
		}
		return __exception;
	}

	private static bool Add(games list, gameScript g)
	{
		if (!g || list.arrayGames == null || list.arrayGamesScripts == null || list.arrayGames.Length != list.arrayGamesScripts.Length || !g.gameObject.CompareTag("Game"))
		{
			return false;
		}
		if (Array.IndexOf(list.arrayGamesScripts, g) >= 0)
		{
			return true;
		}
		int n = list.arrayGames.Length;
		GameObject[] objects = new GameObject[n + 1];
		gameScript[] scripts = new gameScript[n + 1];
		Array.Copy(list.arrayGames, objects, n);
		Array.Copy(list.arrayGamesScripts, scripts, n);
		objects[n] = g.gameObject;
		scripts[n] = g;
		list.arrayGames = objects;
		list.arrayGamesScripts = scripts;
		if ((bool)G.MS && g.ownerID == G.MS.myID && g.myID == g.mainIP && !list.arrayMyIpScripts.Contains(g))
		{
			list.arrayMyIpScripts.Add(g);
		}
		return true;
	}
}
