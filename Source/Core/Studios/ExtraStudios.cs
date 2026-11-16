using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace HolyAddon.Core.Studios;

// Real studios The Holy Expansion doesn't have, added so the 2010s landmarks have someone to make them (the user's
// decision, 16 Nov 2026; Docs/Landmarks-2010s-Plan.md, "Studios"). Data in studios/studios.json, logos in studios/logos.
// - New games: added right after the game reads its studio list (publisher.LoadPublisher, which Holy feeds its merged
//   list), locked until their founding month like any other.
// - Saves: added when the save's studios are read (savegameScript.LoadPublisher) if they aren't there; one whose month
//   has come opens at once. Once saved, they're the save's own studios.
// - Ids follow the highest NPC studio id (the game numbers them 0..n-1; your company is 100000), so the game's arrays
//   kept per studio id (engines' and platforms' publisherBuyed, sized by the studio count) only need growing, which
//   happens after they're added, after a save loads and before engines are saved (SaveEngines writes one entry per
//   studio for each engine and would fail on a short one).
// - The game opens a studio only in its exact month and only within its cap on new rivals, so these are also opened
//   weekly once their month has passed.
// - Logos: fixed numbers from 2000 (2000 + place in studios.json), loaded after the addon's own company logos, and
//   kept out of the company logo pickers.
// Single player only.
// (The class-level [HarmonyPatch] is what makes PatchAll look at its methods' patches.)
[HarmonyPatch]
internal static class ExtraStudios
{
	public const int LogoBase = 2000;

	private class StudioDef
	{
		public string Name { get; set; }

		public string Date { get; set; }

		public int Country { get; set; }

		public int Genre { get; set; }

		public bool OnlyMobile { get; set; }

		public bool Publisher { get; set; }

		public int Market { get; set; } = 70;

		public int Share { get; set; } = 11;

		public int Comval { get; set; } = 12200000;

		public string Logo { get; set; }

		[JsonIgnore]
		public int Year;

		[JsonIgnore]
		public int Month;
	}

	private class StudioFile
	{
		public List<StudioDef> Studios { get; set; }
	}

	private static readonly string[] Months = new string[12] { "JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC" };

	private static List<StudioDef> defs;

	private static int logoFirst = LogoBase;

	private static int logoCount;

	private static string Folder => Path.Combine(Plugin.PluginDir ?? "", "studios");

	private static List<StudioDef> Defs
	{
		get
		{
			if (defs != null)
			{
				return defs;
			}
			defs = new List<StudioDef>();
			try
			{
				string file = Path.Combine(Folder, "studios.json");
				if (File.Exists(file))
				{
					foreach (StudioDef d in JsonConvert.DeserializeObject<StudioFile>(File.ReadAllText(file))?.Studios ?? new List<StudioDef>())
					{
						string[] parts = (d.Date ?? "").Trim().Split(' ');
						if (string.IsNullOrEmpty(d.Name) || parts.Length != 2 || !int.TryParse(parts[1], out d.Year))
						{
							Plugin.Log.LogWarning("Studios: skipped one without a name or date (" + d.Name + ")");
							continue;
						}
						d.Month = Array.IndexOf(Months, parts[0].ToUpperInvariant()) + 1;
						if (d.Month < 1)
						{
							d.Month = 1;
						}
						defs.Add(d);
					}
				}
			}
			catch (Exception ex)
			{
				Plugin.Log.LogError("Studios: reading studios.json failed: " + ex.Message);
			}
			return defs;
		}
	}

	private static bool Single
	{
		get
		{
			if ((bool)G.MS)
			{
				return !G.MS.multiplayer;
			}
			return false;
		}
	}

	public static bool IsStudioLogo(int id)
	{
		if (id >= logoFirst)
		{
			return id < logoFirst + logoCount;
		}
		return false;
	}

	// ---- Logos (called by CompanyLogos once the game's, Holy's and the addon's own are in) ----

	public static void AddLogos(GUI_Main gui)
	{
		logoCount = 0;
		try
		{
			List<StudioDef> list = Defs;
			if (!gui || gui.logoSprites == null || list.Count == 0)
			{
				return;
			}
			Sprite[] old = gui.logoSprites;
			logoFirst = Math.Max(LogoBase, old.Length);
			if (logoFirst != LogoBase)
			{
				Plugin.Log.LogWarning("Studios: there are already " + old.Length + " logos, so the studios' start at " + logoFirst + ": saves may show other logos for them.");
			}
			Sprite[] sprites = new Sprite[logoFirst + list.Count];
			Array.Copy(old, sprites, old.Length);
			for (int i = 0; i < list.Count; i++)
			{
				// The real logo where there is one (logos/real: kept out of the public repo), else the name tile. With
				// mipmaps, as the addon's own company logos: logos are shown small.
				string dir = Path.Combine(Folder, "logos");
				string realLogo = Path.Combine(Path.Combine(dir, "real"), list[i].Logo ?? "");
				sprites[logoFirst + i] = Logos.CompanyLogos.LoadSprite(File.Exists(realLogo) ? realLogo : Path.Combine(dir, list[i].Logo ?? ""));
			}
			gui.logoSprites = sprites;
			logoCount = list.Count;
			Plugin.Log.LogInfo("Studios: " + list.Count + " logos (" + logoFirst + "-" + (logoFirst + list.Count - 1) + ")");
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Studios: logos failed: " + ex);
		}
	}

	// The company logo pickers (yours, a subsidiary's) don't offer real studios' logos.
	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_Firmenlogo), "Init")]
	private static void HideInPicker(Menu_Firmenlogo __instance)
	{
		foreach (Transform child in __instance.uiObjects[0].transform)
		{
			Item_Firmenlogo item = child.GetComponent<Item_Firmenlogo>();
			if ((bool)item && IsStudioLogo(item.myID))
			{
				UnityEngine.Object.Destroy(child.gameObject);
			}
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_TochterfirmaLogo), "Init")]
	private static void HideInSubsidiaryPicker(Menu_TochterfirmaLogo __instance)
	{
		foreach (Transform child in __instance.uiObjects[0].transform)
		{
			Item_FirmenlogoTochterfirma item = child.GetComponent<Item_FirmenlogoTochterfirma>();
			if ((bool)item && IsStudioLogo(item.myID))
			{
				UnityEngine.Object.Destroy(child.gameObject);
			}
		}
	}

	// ---- Adding them ----

	[HarmonyPostfix]
	[HarmonyPatch(typeof(publisher), "LoadPublisher")]
	private static void NewGame(publisher __instance)
	{
		Add(__instance, openDue: false, "new game");
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(savegameScript), "LoadPublisher")]
	private static void SaveLoaded()
	{
		Add(G.MainObj ? G.MainObj.GetComponent<publisher>() : null, openDue: true, "save");
	}

	// After everything is loaded (engines come after studios).
	[HarmonyPostfix]
	[HarmonyPatch(typeof(savegameScript), "Load")]
	private static void LoadDone()
	{
		GrowArrays("after loading");
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(savegameScript), "SaveEngines")]
	private static void BeforeSavingEngines()
	{
		GrowArrays("before saving");
	}

	private static void Add(publisher factory, bool openDue, string when)
	{
		try
		{
			mainScript ms = G.MS;
			if (!ms || !factory || !Single || Defs.Count == 0)
			{
				return;
			}
			ms.FindPublishers();
			publisherScript[] all = ms.arrayPublisherScripts ?? new publisherScript[0];
			HashSet<string> have = new HashSet<string>(from p in all
				where (bool)p && !string.IsNullOrEmpty(p.name_EN)
				select p.name_EN.Trim(), StringComparer.OrdinalIgnoreCase);
			int next = 1 + all.Where((publisherScript p) => (bool)p && !p.isPlayer && p.myID < 100000).Select((publisherScript p) => p.myID).DefaultIfEmpty(-1).Max();
			int now = ms.year * 12 + ms.month;
			int added = 0;
			int opened = 0;
			for (int i = 0; i < Defs.Count; i++)
			{
				StudioDef d = Defs[i];
				if (have.Contains(d.Name))
				{
					continue;
				}
				publisherScript p = factory.CreatePublisher();
				p.myID = next++;
				p.newGameInWeeks = 1;
				p.newGameInWeeksORG = 1;
				p.Init();
				p.name_EN = d.Name;
				p.name_GE = (p.name_TU = (p.name_CH = (p.name_FR = (p.name_JA = (p.name_UA = (p.name_TH = ""))))));
				p.date_year = d.Year;
				p.date_month = d.Month;
				p.logoID = logoFirst + i;
				p.fanGenre = d.Genre;
				p.stars = d.Market;
				p.share = d.Share;
				p.firmenwert = d.Comval;
				p.country = d.Country;
				p.developer = true;
				p.publisher = d.Publisher || d.OnlyMobile;
				p.onlyMobile = d.OnlyMobile;
				added++;
				if (openDue && d.Year * 12 + d.Month <= now)
				{
					p.Unlock();
					opened++;
				}
			}
			if (added > 0)
			{
				ms.FindPublishers();
				GrowArrays("after adding");
				// Their landmarks' franchise lines can be added now that the studios exist.
				Landmarks.Landmarks.RefreshLines();
				Plugin.Log.LogInfo("Studios: added " + added + " (" + when + ")" + ((opened > 0) ? (", " + opened + " open now") : "") + ", ids up to " + (next - 1));
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Studios: adding failed: " + ex);
		}
	}

	// Weekly: one whose month has passed opens (the game's own unlock only checks the exact month, under its cap).
	[HarmonyPostfix]
	[HarmonyPatch(typeof(mainScript), "WochenUpdates")]
	private static void Weekly(mainScript __instance)
	{
		try
		{
			if (!Single || Defs.Count == 0)
			{
				return;
			}
			int now = __instance.year * 12 + __instance.month;
			HashSet<string> ours = new HashSet<string>(Defs.Select((StudioDef d) => d.Name), StringComparer.OrdinalIgnoreCase);
			foreach (publisherScript p in __instance.arrayPublisherScripts ?? new publisherScript[0])
			{
				if ((bool)p && !p.isUnlocked && !p.isPlayer && p.name_EN != null && ours.Contains(p.name_EN) && p.date_year * 12 + p.date_month <= now)
				{
					p.Unlock();
					Plugin.Log.LogInfo("Studios: " + p.name_EN + " opens");
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Studios weekly: " + ex.Message);
		}
	}

	// The game's arrays kept per studio id: long enough for every studio (the studio count, and past the highest id).
	private static void GrowArrays(string when)
	{
		try
		{
			mainScript ms = G.MS;
			if (!ms)
			{
				return;
			}
			ms.FindPublishers();
			ms.FindEngines();
			ms.FindPlatforms();
			publisherScript[] all = ms.arrayPublisherScripts ?? new publisherScript[0];
			int need =Math.Max(GameObject.FindGameObjectsWithTag("Publisher").Length, 1 + all.Where((publisherScript p) => (bool)p && p.myID < 100000).Select((publisherScript p) => p.myID).DefaultIfEmpty(-1).Max());
			int grown = 0;
			foreach (engineScript e in ms.arrayEnginesScripts ?? new engineScript[0])
			{
				if ((bool)e && e.publisherBuyed != null && e.publisherBuyed.Length > 0 && e.publisherBuyed.Length < need)
				{
					Array.Resize(ref e.publisherBuyed, need);
					grown++;
				}
			}
			foreach (platformScript pl in ms.arrayPlatformsScripts ?? new platformScript[0])
			{
				if ((bool)pl && pl.publisherBuyed != null && pl.publisherBuyed.Length > 0 && pl.publisherBuyed.Length < need)
				{
					Array.Resize(ref pl.publisherBuyed, need);
					grown++;
				}
			}
			if (grown > 0)
			{
				Plugin.Log.LogInfo("Studios: grew " + grown + " engine and platform lists to " + need + " studios (" + when + ")");
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Studios: growing the per-studio lists failed (" + when + "): " + ex.Message);
		}
	}
}
