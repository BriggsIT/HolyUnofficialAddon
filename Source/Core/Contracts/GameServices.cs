using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.Core.Contracts;

// Work an outside studio can do on one of your games in development.
// Quality points are sized against the game's own review scale (CalcReview: review = points / (base * curve / 100)),
// so "+6 sound" really means about six more points on the sound review.
internal static class GameServices
{
	public static readonly string[] Kinds = new string[6] { "graphics", "sound", "gameplay", "tech", "bugs", "hands" };

	public static string Name(string kind)
	{
		return kind switch
		{
			"graphics" => "Graphics pass",
			"sound" => "Sound pass",
			"gameplay" => "Gameplay pass",
			"tech" => "Tech and controls pass",
			"bugs" => "Bug fixing",
			"hands" => "Extra hands",
			_ => kind,
		};
	}

	public static string Blurb(string kind)
	{
		return kind switch
		{
			"graphics" => "Artists polish the visuals: a better graphics score.",
			"sound" => "Composers and sound designers: a better sound score.",
			"gameplay" => "Designers tune and add content: a better gameplay score.",
			"tech" => "Programmers tighten things up: a better controls score.",
			"bugs" => "Testers and programmers squash bugs, including some you can't see yet.",
			"hands" => "Extra developers take on part of the remaining work, so it finishes sooner.",
			_ => "",
		};
	}

	public static List<gameScript> MyGamesInDevelopment()
	{
		games all = ((bool)G.MainObj) ? G.MainObj.GetComponent<games>() : null;
		if (!all || all.arrayGamesScripts == null || !G.MS)
		{
			return new List<gameScript>();
		}
		int me = G.MS.myID;
		return all.arrayGamesScripts.Where((gameScript g) => (bool)g && g.developerID == me && g.inDevelopment && !g.isOnMarket && !g.typ_contractGame).ToList();
	}

	public static gameScript Find(int id)
	{
		games all = ((bool)G.MainObj) ? G.MainObj.GetComponent<games>() : null;
		return all?.arrayGamesScripts?.FirstOrDefault((gameScript g) => (bool)g && g.myID == id);
	}

	// Points per review point for one aspect, following gameScript.CalcReview.
	public static float Divisor(gameScript g, string kind)
	{
		mainScript ms = G.MS;
		games all = G.MainObj.GetComponent<games>();
		float[] normal = new float[6] { 7000f, 10000f, 15000f, 18000f, 22000f, 30000f };
		float[] retro = new float[6] { 2500f, 3000f, 3500f, 4000f, 4200f, 4500f };
		int d = Mathf.Clamp(ms.difficulty, 0, 5);
		float num = (g.retro ? retro[d] : normal[d]) * all.GetReviewCurve();
		float scale = 100f;
		switch (kind)
		{
		case "gameplay":
			scale = ((ms.year < 1979) ? 90f : 100f);
			break;
		case "graphics":
			scale = ((ms.year < 1982) ? 90f : 100f);
			break;
		case "sound":
			scale = ((ms.year < 1985) ? 90f : 100f);
			break;
		case "tech":
		{
			unlockScript unlock = G.MainObj.GetComponent<unlockScript>();
			scale = (((bool)unlock && unlock.Get(8)) ? 100f : 80f);
			break;
		}
		}
		return Mathf.Max(1f, num / scale);
	}

	private static float Points(gameScript g, string kind)
	{
		return kind switch
		{
			"graphics" => g.points_grafik,
			"sound" => g.points_sound,
			"gameplay" => g.points_gameplay,
			"tech" => g.points_technik,
			_ => 0f,
		};
	}

	// Roughly what the aspect would score on release right now (before the genre, size and other modifiers).
	public static int Estimate(gameScript g, string kind)
	{
		return Mathf.Clamp(Mathf.RoundToInt(Points(g, kind) / Divisor(g, kind)), 0, 99);
	}

	public static string Status(gameScript g, string kind)
	{
		switch (kind)
		{
		case "bugs":
			return Mathf.RoundToInt(g.points_bugs) + " known bugs";
		case "hands":
			return Mathf.RoundToInt(g.GetProzentGesamt()) + "% developed";
		default:
			return "about " + Estimate(g, kind) + " / 99 so far";
		}
	}

	// Prices are in 1990 money and follow Holy's price curve (cheaper in the early years, dearer later).
	public static Quote Quote(gameScript g, string kind, publisherScript firm)
	{
		float q = Firms.Quality(firm);
		float discount = Firms.Discount(firm);
		int size = Mathf.Clamp(g.gameSize, 0, 5);
		Quote quote = new Quote
		{
			Firm = firm,
			Quality = q
		};
		switch (kind)
		{
		case "bugs":
		{
			int known = Mathf.RoundToInt(g.points_bugs);
			int capacity = Mathf.CeilToInt((15f + 45f * q) * (float)(size + 1) * 0.5f);
			int fixes = Mathf.Min(capacity, Mathf.Max(known, 1));
			if (known <= 0 && g.points_bugsInvis < 1f)
			{
				quote.Unavailable = "No bugs to fix";
			}
			quote.Amount = fixes;
			quote.Price = Firms.Round(Mathf.Max(5000f, (float)fixes * 1500f * (0.75f + 0.75f * q) * (1f - discount)) * (float)Prices.Factor);
			quote.Weeks = 2 + Mathf.CeilToInt((float)size / 2f) + Mathf.CeilToInt(2f * (1f - q));
			quote.Effect = "Fixes up to " + fixes + " bugs";
			break;
		}
		case "hands":
		{
			float remaining = g.devPoints_Gesamt;
			float share = 0.1f + 0.15f * q;
			if (remaining < 30f)
			{
				quote.Unavailable = "Nearly finished";
			}
			quote.Amount = remaining * share;
			quote.Price = Firms.Round(share * 100f * 4000f * (float)(size + 1) * (0.75f + 0.75f * q) * (1f - discount) * (float)Prices.Factor);
			quote.Weeks = 3 + size;
			quote.Effect = "Takes on about " + Mathf.RoundToInt(share * 100f) + "% of the remaining work";
			break;
		}
		default:
		{
			float gain = (3f + 7f * q) * ((firm.fanGenre == g.maingenre) ? 1.15f : 1f);
			float headroom = 99f - (float)Estimate(g, kind);
			gain = Mathf.Min(gain, headroom);
			if (gain < 0.5f)
			{
				quote.Unavailable = "Already near the top";
				gain = 0f;
			}
			quote.Amount = gain * Divisor(g, kind);
			quote.Price = Firms.Round(Mathf.Max(5000f, gain * 5000f * (float)(size + 1) * (0.75f + 0.75f * q) * (1f - discount)) * (float)Prices.Factor);
			quote.Weeks = 2 + size + Mathf.CeilToInt(3f * (1f - q));
			quote.Effect = "About +" + Mathf.Max(1, Mathf.RoundToInt(gain)) + " " + Short(kind);
			break;
		}
		}
		return quote;
	}

	private static string Short(string kind)
	{
		return kind switch
		{
			"graphics" => "graphics",
			"sound" => "sound",
			"gameplay" => "gameplay",
			"tech" => "controls",
			_ => kind,
		};
	}

	// Deliver part of a contract to the game; returns how much was actually delivered.
	public static float Apply(Contract c, gameScript g, float amount)
	{
		switch (c.Kind)
		{
		case "graphics":
			g.points_grafik += amount;
			return amount;
		case "sound":
			g.points_sound += amount;
			return amount;
		case "gameplay":
			g.points_gameplay += amount;
			return amount;
		case "tech":
			g.points_technik += amount;
			return amount;
		case "bugs":
		{
			float fixedBugs = Mathf.Min(g.points_bugs, amount);
			g.points_bugs = Mathf.Max(0f, g.points_bugs - fixedBugs);
			g.points_bugsInvis = Mathf.Max(0f, g.points_bugsInvis - amount * 0.5f);
			return amount;
		}
		case "hands":
		{
			// Never take the current feature to 0: the game only finishes features inside its own work step,
			// so leave at least one point and carry the rest over to the following weeks.
			float want = amount + c.Carry;
			float take = Mathf.Max(0f, Mathf.Min(want, g.devPoints - 1f, g.devPoints_Gesamt - 1f));
			g.devPoints -= take;
			g.devPoints_Gesamt -= take;
			c.Carry = want - take;
			return take;
		}
		default:
			return 0f;
		}
	}

	// ---- Studio improvements ----
	// The improvements your QA, graphics, sound and motion capture rooms can add to a game in development
	// (Menu_QA_GameplayVerbessern and friends, GUI_Main.uiObjects 172/174/176/178). Each one finished sets the
	// game's gameplayStudio/grafikStudio/soundStudio/motionCaptureStudio flag, which spares the review the
	// penalty for leaving it out, and adds quality points to its aspect.

	public const int StudioCount = 4;

	private static readonly int[] studioMenus = new int[4] { 172, 174, 176, 178 };

	private static readonly int[] studioIcons = new int[4] { 0, 6, 12, 18 };

	private static readonly string[] studioAspects = new string[4] { "gameplay", "graphics", "sound", "tech" };

	public static string StudioName(int studio)
	{
		return studio switch
		{
			0 => "QA improvements",
			1 => "Graphics studio improvements",
			2 => "Sound studio improvements",
			3 => "Motion capture improvements",
			_ => "Improvements",
		};
	}

	private static bool[] Flags(gameScript g, int studio)
	{
		return studio switch
		{
			0 => g.gameplayStudio,
			1 => g.grafikStudio,
			2 => g.soundStudio,
			3 => g.motionCaptureStudio,
			_ => null,
		};
	}

	public static int AddonCount(int studio)
	{
		GameObject[] buttons = MenuButtons(studio);
		return (buttons == null) ? 6 : Mathf.Min(6, buttons.Length);
	}

	private static GameObject MenuObject(int studio)
	{
		GUI_Main gui = G.Gui;
		int index = studioMenus[studio];
		if (!gui || gui.uiObjects == null || gui.uiObjects.Length <= index)
		{
			return null;
		}
		return gui.uiObjects[index];
	}

	private static GameObject[] MenuButtons(int studio)
	{
		GameObject menu = MenuObject(studio);
		if (!menu)
		{
			return null;
		}
		return studio switch
		{
			0 => menu.GetComponent<Menu_QA_GameplayVerbessern>()?.uiObjects,
			1 => menu.GetComponent<Menu_GFX_GrafikVerbessern>()?.uiObjects,
			2 => menu.GetComponent<Menu_SFX_SoundVerbessern>()?.uiObjects,
			3 => menu.GetComponent<Menu_MOCAP_AnimationVerbessern>()?.uiObjects,
			_ => null,
		};
	}

	public static long AddonInHouseCost(int studio, int slot, gameScript g)
	{
		GameObject menu = MenuObject(studio);
		if (!menu)
		{
			return 10000L;
		}
		return studio switch
		{
			0 => menu.GetComponent<Menu_QA_GameplayVerbessern>().GetCosts(slot, g),
			1 => menu.GetComponent<Menu_GFX_GrafikVerbessern>().GetCosts(slot, g),
			2 => menu.GetComponent<Menu_SFX_SoundVerbessern>().GetCosts(slot, g),
			3 => menu.GetComponent<Menu_MOCAP_AnimationVerbessern>().GetCosts(slot, g),
			_ => 10000,
		};
	}

	// How many work steps the improvement takes in your own room.
	private static float AddonWork(int studio, int slot, gameScript g)
	{
		GameObject menu = MenuObject(studio);
		float[] percent = ((!menu) ? null : (studio switch
		{
			0 => menu.GetComponent<Menu_QA_GameplayVerbessern>().pointsInPercent,
			1 => menu.GetComponent<Menu_GFX_GrafikVerbessern>().pointsInPercent,
			2 => menu.GetComponent<Menu_SFX_SoundVerbessern>().pointsInPercent,
			3 => menu.GetComponent<Menu_MOCAP_AnimationVerbessern>().pointsInPercent,
			_ => null,
		}));
		float share = ((percent != null && slot < percent.Length) ? percent[slot] : 0.05f);
		return Mathf.Max(5f, (float)g.GetGesamtDevPoints() * share);
	}

	// The name on the room's own button, e.g. "QA [Balance]".
	public static string AddonName(int studio, int slot)
	{
		GameObject[] buttons = MenuButtons(studio);
		if (buttons != null && slot < buttons.Length && (bool)buttons[slot])
		{
			foreach (setText label in buttons[slot].GetComponentsInChildren<setText>(includeInactive: true))
			{
				if (label.name == "TextPreis")
				{
					continue;
				}
				if (label.textArray.Length > 0 && label.textID > -1 && (bool)G.TS)
				{
					return G.TS.GetText(label.textID);
				}
				if (!string.IsNullOrEmpty(label.c))
				{
					return label.c;
				}
			}
			foreach (Text text in buttons[slot].GetComponentsInChildren<Text>(includeInactive: true))
			{
				if (text.name != "TextPreis" && !string.IsNullOrEmpty(text.text))
				{
					return text.text;
				}
			}
		}
		return StudioName(studio).Replace(" improvements", "") + " " + (slot + 1);
	}

	public static Sprite AddonIcon(int studio, int slot)
	{
		games all = ((bool)G.MainObj) ? G.MainObj.GetComponent<games>() : null;
		int index = studioIcons[studio] + slot;
		if (!all || all.gameAdds == null || index >= all.gameAdds.Length)
		{
			return null;
		}
		return all.gameAdds[index];
	}

	public static bool AddonDone(gameScript g, int studio, int slot)
	{
		bool[] flags = Flags(g, studio);
		if (flags != null && slot < flags.Length)
		{
			return flags[slot];
		}
		return true;
	}

	// One of your own rooms is already working on it.
	public static bool AddonInRoom(gameScript g, int studio, int slot)
	{
		switch (studio)
		{
		case 0:
			return Object.FindObjectsOfType<taskGameplayVerbessern>().Any((taskGameplayVerbessern t) => t.targetID == g.myID && slot < t.adds.Length && t.adds[slot]);
		case 1:
			return Object.FindObjectsOfType<taskGrafikVerbessern>().Any((taskGrafikVerbessern t) => t.targetID == g.myID && slot < t.adds.Length && t.adds[slot]);
		case 2:
			return Object.FindObjectsOfType<taskSoundVerbessern>().Any((taskSoundVerbessern t) => t.targetID == g.myID && slot < t.adds.Length && t.adds[slot]);
		case 3:
			return Object.FindObjectsOfType<taskAnimationVerbessern>().Any((taskAnimationVerbessern t) => t.targetID == g.myID && slot < t.adds.Length && t.adds[slot]);
		default:
			return false;
		}
	}

	public static bool AddonContracted(gameScript g, int studio, int slot)
	{
		return ContractBook.Active.Any((Contract c) => c.IsAddons && c.GameId == g.myID && c.Studio == studio && c.Addons.Contains(slot) && !c.AddonsDone.Contains(slot));
	}

	// Quality points one improvement brings: the room's work steps times what a worker of the studio's level adds per step.
	private static float AddonPoints(int studio, int slot, gameScript g, float q)
	{
		return AddonWork(studio, slot, g) * (1.5f + 3.5f * q);
	}

	public static Quote QuoteAddons(gameScript g, int studio, IList<int> slots, publisherScript firm)
	{
		float q = Firms.Quality(firm);
		float discount = Firms.Discount(firm);
		float speciality = ((firm.fanGenre == g.maingenre) ? 1.15f : 1f);
		double inHouse = slots.Sum((int s) => (double)AddonInHouseCost(studio, s, g));
		float points = slots.Sum((int s) => AddonPoints(studio, s, g, q)) * speciality;
		int weeks = Mathf.Clamp(Mathf.CeilToInt((float)slots.Count * (1f + 2f * (1f - q))) + 1, 2, 26);
		string aspect = studioAspects[studio];
		int gain = Mathf.Max(1, Mathf.RoundToInt(points / Divisor(g, aspect)));
		return new Quote
		{
			Firm = firm,
			Quality = q,
			Amount = points,
			Weeks = weeks,
			Price = Firms.Round(System.Math.Max(5000.0, inHouse * (1.3 + 0.7 * (double)q) * (1.0 - (double)discount))),
			Effect = slots.Count + ((slots.Count == 1) ? " improvement" : " improvements") + ", about +" + gain + " " + Short(aspect)
		};
	}

	// Finish one improvement the way the room's own task does: set its flag, add its quality points and book its cost.
	public static void ApplyAddon(Contract c, gameScript g, int slot)
	{
		bool[] flags = Flags(g, c.Studio);
		if (flags == null || slot >= flags.Length)
		{
			return;
		}
		flags[slot] = true;
		float share = c.Amount * c.Multiplier / (float)Mathf.Max(1, c.Addons.Count);
		switch (studioAspects[c.Studio])
		{
		case "gameplay":
			g.points_gameplay += share;
			break;
		case "graphics":
			g.points_grafik += share;
			break;
		case "sound":
			g.points_sound += share;
			break;
		default:
			g.points_technik += share;
			break;
		}
		g.costs_entwicklung += c.Price / Mathf.Max(1, c.Addons.Count);
		c.Applied += share;
	}

	public static string Describe(string kind, float delivered, gameScript g)
	{
		switch (kind)
		{
		case "bugs":
			return "up to " + Mathf.RoundToInt(delivered) + " bugs fixed";
		case "hands":
			return "development is now " + Mathf.RoundToInt(g.GetProzentGesamt()) + "% done";
		default:
			return "about +" + Mathf.Max(1, Mathf.RoundToInt(delivered / Divisor(g, kind))) + " " + Short(kind);
		}
	}
}
