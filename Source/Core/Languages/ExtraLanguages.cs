using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using HolyAddon.Core.Updates;
using HolyAddon.UI;
using Newtonsoft.Json;
using UnityEngine;

namespace HolyAddon.Core.Languages;

internal class LanguageDef
{
	public string Name;

	public string Code;

	// The flag's country (the game's country ids, as in the company location list).
	public int Country;

	// Countries whose studios speak it: translating into it is free for them, as the game's Muttersprache.
	public int[] Native;

	public int Year;

	// Sales it adds, the mirror of what the game takes off for each of its own languages a game lacks.
	public float Share;
}

internal class LanguagesState
{
	// Your games' new languages, by game id.
	public Dictionary<int, bool[]> Games = new Dictionary<int, bool[]>();
}

// More languages (Docs/Languages-Plan.md): eleven beyond the game's eleven, for your games only. Each opens in its
// year; on the design screen they're a second row of flags (UI/LanguagesRow), and a released game can get them with a
// Translation update. Each costs the game's price for a language (development points x 5, free for your studio's own)
// and adds its share to the game's weekly sales, through the addon's sales multiplier (GameUpdates.OurSalesFactor).
// Add-ons, budget and GOTY editions and ports carry their main game's.
[HarmonyPatch]
internal static class ExtraLanguages
{
	private const string SaveKey = "mgt2.editor.languages";

	public static readonly LanguageDef[] Defs = new LanguageDef[11]
	{
		new LanguageDef
		{
			Name = "Portuguese",
			Code = "pt",
			Country = 24,
			Native = new int[2] { 24, 25 },
			Year = 1985,
			Share = 0.02f
		},
		new LanguageDef
		{
			Name = "Dutch",
			Code = "nl",
			Country = 18,
			Native = new int[2] { 18, 47 },
			Year = 1980,
			Share = 0.01f
		},
		new LanguageDef
		{
			Name = "Greek",
			Code = "el",
			Country = 33,
			Native = new int[1] { 33 },
			Year = 1990,
			Share = 0.01f
		},
		new LanguageDef
		{
			Name = "Hungarian",
			Code = "hu",
			Country = 23,
			Native = new int[1] { 23 },
			Year = 1990,
			Share = 0.01f
		},
		new LanguageDef
		{
			Name = "Czech",
			Code = "cs",
			Country = 20,
			Native = new int[2] { 20, 21 },
			Year = 1993,
			Share = 0.01f
		},
		new LanguageDef
		{
			Name = "Turkish",
			Code = "tr",
			Country = 17,
			Native = new int[1] { 17 },
			Year = 1993,
			Share = 0.01f
		},
		new LanguageDef
		{
			Name = "Romanian",
			Code = "ro",
			Country = 27,
			Native = new int[1] { 27 },
			Year = 1995,
			Share = 0.01f
		},
		new LanguageDef
		{
			Name = "Ukrainian",
			Code = "uk",
			Country = 19,
			Native = new int[1] { 19 },
			Year = 1995,
			Share = 0.01f
		},
		new LanguageDef
		{
			Name = "Arabic",
			Code = "ar",
			Country = 32,
			Native = new int[1] { 32 },
			Year = 1998,
			Share = 0.02f
		},
		new LanguageDef
		{
			Name = "Hindi",
			Code = "hi",
			Country = 34,
			Native = new int[1] { 34 },
			Year = 2005,
			Share = 0.02f
		},
		new LanguageDef
		{
			Name = "Indonesian",
			Code = "id",
			Country = 45,
			Native = new int[2] { 45, 43 },
			Year = 2005,
			Share = 0.02f
		}
	};

	public static int Count => Defs.Length;

	private static LanguagesState S = new LanguagesState();

	public static bool On => G.Singleplayer;

	public static bool Unlocked(int i)
	{
		if ((bool)G.MS)
		{
			return G.MS.year >= Defs[i].Year;
		}
		return false;
	}

	public static bool AnyUnlocked => Enumerable.Range(0, Count).Any(Unlocked);

	// The first to open, for "opens in ..." messages.
	public static LanguageDef NextToOpen => Defs.Where((LanguageDef d) => !G.MS || G.MS.year < d.Year).OrderBy((LanguageDef d) => d.Year).FirstOrDefault();

	public static bool Native(int i)
	{
		if ((bool)G.MS)
		{
			return Defs[i].Native.Contains(G.MS.GetCountryID());
		}
		return false;
	}

	public static Sprite Icon(int i)
	{
		Sprite s = UIKit.LoadSprite(Path.Combine(Path.Combine(Plugin.PluginDir ?? "", "icons"), "lang-" + Defs[i].Code + ".png"));
		if ((bool)s)
		{
			return s;
		}
		GUI_Main gui = G.Gui;
		if ((bool)gui && gui.flagSprites != null && Defs[i].Country < gui.flagSprites.Length)
		{
			return gui.flagSprites[Defs[i].Country];
		}
		return null;
	}

	public static bool[] New()
	{
		return new bool[Count];
	}

	public static int Picked(bool[] langs)
	{
		return langs?.Count((bool b) => b) ?? 0;
	}

	public static float Share(bool[] langs)
	{
		if (langs == null)
		{
			return 0f;
		}
		float s = 0f;
		for (int i = 0; i < Mathf.Min(langs.Length, Count); i++)
		{
			if (langs[i])
			{
				s += Defs[i].Share;
			}
		}
		return s;
	}

	// The game's price for translating a game of this many development points (free for your studio's own).
	public static long Cost(bool[] langs, float points)
	{
		if (langs == null)
		{
			return 0L;
		}
		long total = 0L;
		for (int i = 0; i < Mathf.Min(langs.Length, Count); i++)
		{
			if (langs[i] && !Native(i))
			{
				total += (long)Mathf.Round(points * 5f);
			}
		}
		return total;
	}

	public static string Names(IEnumerable<int> ids)
	{
		List<string> n = ids.Select((int i) => Defs[i].Name).ToList();
		if (n.Count == 0)
		{
			return "";
		}
		if (n.Count == 1)
		{
			return n[0];
		}
		return string.Join(", ", n.Take(n.Count - 1)) + " and " + n[n.Count - 1];
	}

	public static List<int> Ids(bool[] langs)
	{
		List<int> list = new List<int>();
		if (langs != null)
		{
			for (int i = 0; i < Mathf.Min(langs.Length, Count); i++)
			{
				if (langs[i])
				{
					list.Add(i);
				}
			}
		}
		return list;
	}

	// ---- Games ----

	public static bool[] Own(gameScript g)
	{
		if (!g || !S.Games.TryGetValue(g.myID, out var value))
		{
			return null;
		}
		return value;
	}

	// A game's new languages: its own, or (add-ons, budget and GOTY editions, ports without their own) its main game's.
	public static bool[] Of(gameScript g)
	{
		bool[] own = Own(g);
		if (own != null || !g)
		{
			return own;
		}
		if (g.typ_addon || g.typ_addonStandalone || g.typ_mmoaddon || g.typ_budget || g.typ_goty || g.portID != -1)
		{
			int source = ((g.portID != -1) ? g.portID : ((g.originalGameID >= 0) ? g.originalGameID : g.mainIP));
			if (source >= 0 && source != g.myID && S.Games.TryGetValue(source, out var value))
			{
				return value;
			}
		}
		return null;
	}

	public static void Attach(gameScript g, bool[] langs)
	{
		if ((bool)g && Picked(langs) > 0)
		{
			S.Games[g.myID] = (bool[])langs.Clone();
		}
	}

	public static void Add(gameScript g, IEnumerable<int> ids)
	{
		if (!g)
		{
			return;
		}
		bool[] langs = (Own(g) ?? Of(g) ?? New()).ToArray();
		if (langs.Length < Count)
		{
			Array.Resize(ref langs, Count);
		}
		foreach (int i in ids)
		{
			if (i >= 0 && i < Count)
			{
				langs[i] = true;
			}
		}
		S.Games[g.myID] = langs;
	}

	// The weekly sales multiplier (GameUpdates.OurSalesFactor).
	public static float SalesFactor(gameScript g)
	{
		if (!On || !g || !g.IsMyGame())
		{
			return 1f;
		}
		bool[] langs = Of(g);
		if (langs == null)
		{
			return 1f;
		}
		return 1f + Share(langs);
	}

	// The new languages a released game of yours could still get.
	public static List<int> Missing(gameScript g)
	{
		bool[] langs = Of(g);
		return (from i in Enumerable.Range(0, Count)
			where Unlocked(i) && (langs == null || i >= langs.Length || !langs[i])
			select i).ToList();
	}

	// The game's tooltip lists them.
	[HarmonyPostfix]
	[HarmonyPatch(typeof(gameScript), "GetTooltip")]
	private static void Tooltip(gameScript __instance, ref string __result)
	{
		try
		{
			bool[] langs = (On ? Of(__instance) : null);
			if (langs != null && Picked(langs) > 0 && !string.IsNullOrEmpty(__result))
			{
				__result = __result + "\n<color=#5f6878>Also in " + Names(Ids(langs)) + "</color>";
			}
		}
		catch
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
			Plugin.Log.LogError("Saving languages failed: " + ex);
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(savegameScript), "Load")]
	private static void BeforeLoad()
	{
		S = new LanguagesState();
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(savegameScript), "LoadHistory")]
	private static void AfterLoad(ES3File ___es3file)
	{
		try
		{
			S = ((___es3file != null && ___es3file.KeyExists(SaveKey)) ? JsonConvert.DeserializeObject<LanguagesState>(___es3file.Load<string>(SaveKey)) : null) ?? new LanguagesState();
			S.Games = S.Games ?? new Dictionary<int, bool[]>();
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Loading languages failed: " + ex);
			S = new LanguagesState();
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(mainScript), "InitNewGame")]
	private static void NewGame()
	{
		S = new LanguagesState();
	}
}
