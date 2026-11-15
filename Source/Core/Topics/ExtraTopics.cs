using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using HolyAddon.Core.Updates;
using HolyAddon.UI;
using Newtonsoft.Json;
using UnityEngine;

namespace HolyAddon.Core.Topics;

internal class ExtraTopicsState
{
	// Your games' third and fourth topics (-1: none), by game id.
	public Dictionary<int, int[]> Games = new Dictionary<int, int[]>();

	// Research points left for Layered Settings and Rich Worlds (-1: not saved yet, so not started).
	public float[] ResearchLeft = new float[2] { -1f, -1f };

	// The "you can pick it now" news, once each.
	public bool[] Announced = new bool[2];
}

internal class TopicThemesFile
{
	public List<TopicThemes> Topics = new List<TopicThemes>();
}

internal class TopicThemes
{
	public int Id;

	public string Name = "";

	public string[] Themes = new string[0];
}

// Third and fourth topics (Docs/Topics-Plan.md): an addon layer on top of the game's two topics.
// - Research: Layered Settings (third topic, from 1988, after the game's Sub topic research) and Rich Worlds (fourth,
//   from 1996, after Layered Settings), in Other research slots 41 and 42 (the game's 41 slots are all taken, so the
//   lists are grown; the addon keeps their progress in its own save, as a save without them grows them as "done").
// - Picking: a button on the game design screen (UI/ExtraTopicsMenu) and the game's own topic picker. Only your games.
// - Effects: the review (each extra topic: does it suit the genre, does it go with the other topics, by topic themes
//   from topics/topic-themes.json) with a line about the mix in the review, and about 10% more development cost and
//   time per extra topic. Landmarks count all four topics.
[HarmonyPatch]
internal static class ExtraTopics
{
	public const int LayeredSlot = 41;

	public const int RichSlot = 42;

	public static readonly int[] Slots = new int[2] { LayeredSlot, RichSlot };

	public static readonly int[] Years = new int[2] { 1988, 1996 };

	public static readonly string[] ResearchNames = new string[2] { "Layered Settings", "Rich Worlds" };

	public static readonly string[] SlotNames = new string[2] { "Third Topic", "Fourth Topic" };

	// Extra development cost and time per extra topic.
	public const float WorkPerTopic = 0.1f;

	private const string SaveKey = "mgt2.editor.topics";

	private static ExtraTopicsState S = new ExtraTopicsState();

	public static ExtraTopicsState State => S;

	private static bool loggedResearch;

	public static bool On => G.Singleplayer;

	// ---- Research ----

	public static forschungSonstiges Other
	{
		get
		{
			if (!G.MainObj)
			{
				return null;
			}
			return G.MainObj.GetComponent<forschungSonstiges>();
		}
	}

	private static T[] Grow<T>(T[] a, int n)
	{
		if (a != null && a.Length >= n)
		{
			return a;
		}
		T[] b = new T[n];
		if (a != null)
		{
			Array.Copy(a, b, a.Length);
		}
		return b;
	}

	// The two slots exist, with points, a price and a picture. RES_POINTS isn't saved, so this runs after every load.
	public static bool EnsureSlots()
	{
		forschungSonstiges fs = Other;
		if (!fs || fs.RES_POINTS == null || fs.RES_POINTS_LEFT == null || fs.RES_PRICE == null || fs.RES_POINTS.Length <= 36 || fs.RES_PRICE.Length <= 36)
		{
			return false;
		}
		int need = RichSlot + 1;
		if (fs.RES_POINTS.Length >= need && fs.RES_POINTS_LEFT.Length >= need && fs.RES_PRICE.Length >= need && fs.RES_SPRITE != null && fs.RES_SPRITE.Length >= need && fs.RES_POINTS[LayeredSlot] > 0f && fs.RES_POINTS[RichSlot] > 0f && (bool)fs.RES_SPRITE[LayeredSlot] && (bool)fs.RES_SPRITE[RichSlot])
		{
			return true;
		}
		int leftBefore = fs.RES_POINTS_LEFT.Length;
		fs.RES_POINTS = Grow(fs.RES_POINTS, need);
		fs.RES_POINTS_LEFT = Grow(fs.RES_POINTS_LEFT, need);
		fs.RES_PRICE = Grow(fs.RES_PRICE, need);
		fs.RES_SPRITE = Grow(fs.RES_SPRITE, need);
		double basePoints = Math.Max(fs.RES_POINTS[36], RoomAverage(fs.RES_POINTS.Select((float p) => (double)p), 60.0));
		double basePrice = Math.Max(fs.RES_PRICE[36], RoomAverage(fs.RES_PRICE.Select((int p) => (double)p), 25000.0));
		float[] factor = new float[2] { 1.5f, 2.5f };
		for (int i = 0; i < 2; i++)
		{
			int slot = Slots[i];
			if (fs.RES_POINTS[slot] <= 0f)
			{
				fs.RES_POINTS[slot] = (float)Math.Round(basePoints * (double)factor[i]);
			}
			if (fs.RES_PRICE[slot] <= 0)
			{
				fs.RES_PRICE[slot] = (int)Math.Round(basePrice * (double)factor[i] / 1000.0) * 1000;
			}
			if (!fs.RES_SPRITE[slot])
			{
				fs.RES_SPRITE[slot] = Icon(i);
			}
			if (slot >= leftBefore)
			{
				// New in this list: not researched.
				fs.RES_POINTS_LEFT[slot] = fs.RES_POINTS[slot];
			}
			else if (fs.RES_POINTS_LEFT[slot] > fs.RES_POINTS[slot] || float.IsNaN(fs.RES_POINTS_LEFT[slot]))
			{
				fs.RES_POINTS_LEFT[slot] = fs.RES_POINTS[slot];
			}
		}
		if (!loggedResearch)
		{
			loggedResearch = true;
			Plugin.Log.LogInfo("Topic research: Layered Settings " + fs.RES_POINTS[LayeredSlot] + " points, " + fs.RES_PRICE[LayeredSlot] + "; Rich Worlds " + fs.RES_POINTS[RichSlot] + " points, " + fs.RES_PRICE[RichSlot] + " (Sub topic: " + fs.RES_POINTS[36] + ", " + fs.RES_PRICE[36] + ")");
		}
		return true;
	}

	// The room research (slots 28 to 34) as a yardstick.
	private static double RoomAverage(IEnumerable<double> values, double fallback)
	{
		List<double> list = values.Skip(28).Take(7).Where((double v) => v > 0.0).ToList();
		if (list.Count != 0)
		{
			return list.Average();
		}
		return fallback;
	}

	public static Sprite Icon(int i)
	{
		return UIKit.LoadSprite(Path.Combine(Path.Combine(Plugin.PluginDir ?? "", "icons"), (i == 0) ? "research-layered.png" : "research-worlds.png")) ?? UIKit.Sprite("iconOptions");
	}

	public static bool Researched(int i)
	{
		forschungSonstiges fs = Other;
		int slot = Slots[i];
		if ((bool)fs && fs.RES_POINTS_LEFT != null && fs.RES_POINTS_LEFT.Length > slot && fs.RES_POINTS != null && fs.RES_POINTS.Length > slot && fs.RES_POINTS[slot] > 0f)
		{
			return fs.IsErforscht(slot);
		}
		return false;
	}

	private static bool Running(int i)
	{
		forschungSonstiges fs = Other;
		if ((bool)fs)
		{
			return fs.BereitsInAnderenRaumAktiv(Slots[i]);
		}
		return false;
	}

	// Shown in the research room's Other research list right now.
	public static bool Offered(int i)
	{
		if (!On || !G.MS || !EnsureSlots() || Researched(i) || Running(i) || G.MS.year < Years[i])
		{
			return false;
		}
		forschungSonstiges fs = Other;
		if (i == 0)
		{
			return fs.IsErforscht(36);
		}
		return Researched(0);
	}

	// Why the slot can't be picked yet, or null.
	public static string LockReason(int i)
	{
		if (Researched(i))
		{
			return null;
		}
		string when = ((G.MS && G.MS.year < Years[i]) ? (" It can be researched from " + Years[i] + ".") : "");
		if (i == 1 && !Researched(0))
		{
			return "Research \"" + ResearchNames[0] + "\" and then \"" + ResearchNames[1] + "\" first, in a research room under Other research." + when;
		}
		return "Research \"" + ResearchNames[i] + "\" first, in a research room under Other research." + when;
	}

	public static string ResearchTooltip(int i)
	{
		if (i == 0)
		{
			return "Games can have a third topic: pick it on the game design screen. Topics that suit the genre and go with the others lift the review; each extra topic adds about 10% to the development cost and time. (Holy Unofficial Addon)";
		}
		return "Games can have a fourth topic, on top of the third. (Holy Unofficial Addon)";
	}

	private static readonly AccessTools.FieldRef<Menu_Forschung, string> SearchText = AccessTools.FieldRefAccess<Menu_Forschung, string>("searchStringA");

	private static readonly System.Reflection.MethodInfo CreateItem = AccessTools.Method(typeof(Menu_Forschung), "CreateItem");

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_Forschung), "Init")]
	private static void ResearchList(Menu_Forschung __instance, int i)
	{
		if (i != 5)
		{
			return;
		}
		try
		{
			bool added = false;
			string search = (SearchText(__instance) ?? "").ToLower();
			for (int k = 0; k < 2; k++)
			{
				if (Offered(k) && (!(bool)__instance.myInputField || __instance.myInputField.text.Length == 0 || ResearchNames[k].ToLower().Contains(search)))
				{
					CreateItem.Invoke(__instance, new object[1] { Slots[k] });
					added = true;
				}
			}
			if (added)
			{
				__instance.DROPDOWN_Sort();
				if ((bool)G.Gui)
				{
					G.Gui.KeinEintrag(__instance.uiObjects[0], __instance.uiObjects[5]);
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Adding topic research failed: " + ex);
		}
	}

	// The count on the Other research button, and automatic research picking the next item.
	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_Forschung), "GetAmountForschung")]
	private static void ResearchCount(int i, bool getUnerforschtesObjekt, ref int __result)
	{
		if (i != 5)
		{
			return;
		}
		for (int k = 0; k < 2; k++)
		{
			if (!Offered(k))
			{
				continue;
			}
			if (getUnerforschtesObjekt)
			{
				if (__result == -1)
				{
					__result = Slots[k];
				}
			}
			else
			{
				__result++;
			}
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(forschungSonstiges), "GetName")]
	private static void ResearchName(int i, ref string __result)
	{
		if (i == LayeredSlot)
		{
			__result = ResearchNames[0];
		}
		else if (i == RichSlot)
		{
			__result = ResearchNames[1];
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(forschungSonstiges), "GetTooltip")]
	private static void ResearchInfo(int i, ref string __result)
	{
		if (i == LayeredSlot)
		{
			__result = ResearchTooltip(0);
		}
		else if (i == RichSlot)
		{
			__result = ResearchTooltip(1);
		}
	}

	// Once a week: the news when a research is done.
	public static void Weekly()
	{
		if (!On || !EnsureSlots())
		{
			return;
		}
		for (int i = 0; i < 2; i++)
		{
			if (Researched(i) && !S.Announced[i])
			{
				S.Announced[i] = true;
				if ((bool)G.Gui)
				{
					G.Gui.CreateTopNewsInfo("<b>" + ResearchNames[i] + "</b>: your games can have a " + ((i == 0) ? "third" : "fourth") + " topic. Pick it with More Topics on the game design screen.");
				}
			}
		}
	}

	// ---- Topic data ----

	private static Dictionary<int, string[]> themesOf;

	private static string Key(string a, string b)
	{
		if (string.CompareOrdinal(a, b) >= 0)
		{
			return b + "|" + a;
		}
		return a + "|" + b;
	}

	private static readonly HashSet<string> GoesWith = Pairs(new string[30]
	{
		"Fantasy: Mythology History Exploration Horror Kids Asia",
		"SciFi: Space Military Apocalypse Science City Espionage Mystery Superheroes",
		"Space: Exploration Science Military",
		"Horror: Mystery Apocalypse Mythology",
		"Crime: City Vehicles Mystery Espionage Mature Business Asia Superheroes",
		"City: Vehicles Everyday Business Superheroes Music",
		"Military: History Espionage Vehicles Apocalypse Sea",
		"History: Mythology Asia Western Sea Exploration Mystery",
		"Mythology: Exploration Asia",
		"Wildlife: Exploration Sea Science Pets Kids",
		"Pets: Kids Everyday Comedy Food",
		"Kids: Comedy Food Music Abstract",
		"Sports: Vehicles Everyday Business Comedy Music",
		"Vehicles: Exploration Espionage",
		"Sea: Exploration Wildlife Mystery",
		"Mystery: Espionage Exploration",
		"Espionage: Asia",
		"Apocalypse: Exploration",
		"Superheroes: Comedy",
		"Comedy: Everyday Food Music",
		"Music: Everyday",
		"Everyday: Food Business",
		"Business: Science Food",
		"Science: Mystery Abstract",
		"Western: Exploration",
		"Asia: Food",
		"Exploration: Science",
		"Abstract: Science",
		"Mature: Horror City",
		"Food: Everyday"
	});

	private static readonly HashSet<string> Clashes = Pairs(new string[6]
	{
		"Kids: Horror Mature Crime Military Apocalypse Espionage",
		"Pets: Horror Mature Military Apocalypse",
		"Sports: Horror Apocalypse Mythology",
		"Food: Horror Apocalypse Military",
		"Comedy: Apocalypse",
		"Everyday: Mythology Space"
	});

	private static HashSet<string> Pairs(string[] lines)
	{
		HashSet<string> set = new HashSet<string>();
		foreach (string line in lines)
		{
			string[] parts = line.Split(':');
			string a = parts[0].Trim();
			foreach (string b in parts[1].Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
			{
				set.Add(Key(a, b));
			}
		}
		return set;
	}

	private static string[] ThemesOf(int topic)
	{
		if (themesOf == null)
		{
			themesOf = new Dictionary<int, string[]>();
			try
			{
				string file = Path.Combine(Path.Combine(Plugin.PluginDir ?? "", "topics"), "topic-themes.json");
				if (File.Exists(file))
				{
					foreach (TopicThemes t in JsonConvert.DeserializeObject<TopicThemesFile>(File.ReadAllText(file))?.Topics ?? new List<TopicThemes>())
					{
						themesOf[t.Id] = t.Themes ?? new string[0];
					}
					Plugin.Log.LogInfo("Topic themes: " + themesOf.Count + " topics");
				}
				else
				{
					Plugin.Log.LogWarning("Topic themes: " + file + " is missing");
				}
			}
			catch (Exception ex)
			{
				Plugin.Log.LogError("Loading topic themes failed: " + ex);
			}
		}
		if (!themesOf.TryGetValue(topic, out var value))
		{
			return new string[0];
		}
		return value;
	}

	// 1: they go together (a theme in common, or themes that go together), -1: they clash, 0: neither.
	public static int Relation(int a, int b)
	{
		string[] ta = ThemesOf(a);
		string[] tb = ThemesOf(b);
		bool clash = false;
		bool good = false;
		foreach (string x in ta)
		{
			foreach (string y in tb)
			{
				if (x == y || GoesWith.Contains(Key(x, y)))
				{
					good = true;
				}
				else if (Clashes.Contains(Key(x, y)))
				{
					clash = true;
				}
			}
		}
		if (clash && !good)
		{
			return -1;
		}
		if (good)
		{
			return 1;
		}
		return 0;
	}

	private static themes Themes
	{
		get
		{
			if (!G.MainObj)
			{
				return null;
			}
			return G.MainObj.GetComponent<themes>();
		}
	}

	public static bool SuitsGenre(int topic, int genre)
	{
		themes th = Themes;
		if ((bool)th && genre >= 0 && topic >= 0)
		{
			return th.IsThemesFitWithGenre(topic, genre);
		}
		return false;
	}

	public static string TopicName(int topic)
	{
		if (!G.TS || topic < 0)
		{
			return "?";
		}
		return G.TS.GetThemes(topic);
	}

	// One extra topic, judged: does it suit the genre (+1 / -1.5), does it go with the game's other topics
	// (+1.5 / 0 / -2: a clash with any of them counts).
	public class Verdict
	{
		public int Topic;

		public bool Suits;

		public List<int> GoesWith = new List<int>();

		public List<int> ClashesWith = new List<int>();

		public float Score;
	}

	public static Verdict Judge(int topic, int genre, IEnumerable<int> others)
	{
		Verdict v = new Verdict
		{
			Topic = topic,
			Suits = SuitsGenre(topic, genre)
		};
		foreach (int o in others)
		{
			if (o < 0 || o == topic)
			{
				continue;
			}
			int r = Relation(topic, o);
			if (r > 0)
			{
				v.GoesWith.Add(o);
			}
			else if (r < 0)
			{
				v.ClashesWith.Add(o);
			}
		}
		v.Score = (v.Suits ? 1f : (-1.5f)) + ((v.ClashesWith.Count > 0) ? (-2f) : ((v.GoesWith.Count > 0) ? 1.5f : 0f));
		return v;
	}

	// The review change for a game's extra topics: about -6 to +5 points.
	public static int ReviewChange(int genre, int main, int sub, int[] extra, out List<Verdict> verdicts)
	{
		verdicts = new List<Verdict>();
		if (extra == null)
		{
			return 0;
		}
		List<int> all = new List<int> { main, sub };
		all.AddRange(extra);
		float total = 0f;
		foreach (int t in extra)
		{
			if (t >= 0)
			{
				Verdict v = Judge(t, genre, all);
				verdicts.Add(v);
				total += v.Score;
			}
		}
		return Mathf.Clamp(Mathf.RoundToInt(total), -6, 5);
	}

	public static int Count(int[] extra)
	{
		return extra?.Count((int t) => t >= 0) ?? 0;
	}

	public static float WorkFactor(int[] extra)
	{
		return 1f + WorkPerTopic * (float)Count(extra);
	}

	// ---- Games ----

	public static int[] Of(gameScript g)
	{
		if (!g || !S.Games.TryGetValue(g.myID, out var value))
		{
			return null;
		}
		return value;
	}

	// A topic among all four of a game's.
	public static bool Has(gameScript g, int topic)
	{
		if (!g)
		{
			return false;
		}
		if (g.gameMainTheme == topic || g.gameSubTheme == topic)
		{
			return true;
		}
		int[] extra = Of(g);
		if (extra != null)
		{
			return extra.Contains(topic);
		}
		return false;
	}

	public static void Attach(gameScript g, int[] extra)
	{
		if ((bool)g && Count(extra) > 0)
		{
			S.Games[g.myID] = (int[])extra.Clone();
		}
	}

	// The review: each time the game works it out (the projection and the real one).
	[HarmonyPostfix]
	[HarmonyPatch(typeof(gameScript), "CalcReview")]
	private static void Review(gameScript __instance)
	{
		try
		{
			int[] extra = Of(__instance);
			if (extra != null)
			{
				int change = ReviewChange(__instance.maingenre, __instance.gameMainTheme, __instance.gameSubTheme, extra, out var _);
				if (change != 0)
				{
					__instance.reviewTotal = Mathf.Clamp(__instance.reviewTotal + change, 1, 100);
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Extra topics review: " + ex.Message);
		}
	}

	private static readonly AccessTools.FieldRef<reviewText, List<string>> TotalList = AccessTools.FieldRefAccess<reviewText, List<string>>("totalList");

	// A line about the mix, before the review's verdict.
	[HarmonyPostfix]
	[HarmonyPatch(typeof(reviewText), "GetReviewText")]
	private static void ReviewLine(reviewText __instance, gameScript game_, ref string __result)
	{
		try
		{
			int[] extra = Of(game_);
			if (extra == null || string.IsNullOrEmpty(__result))
			{
				return;
			}
			string line = MixLine(game_, extra);
			if (string.IsNullOrEmpty(line))
			{
				return;
			}
			List<string> totals = TotalList(__instance);
			string verdict = ((totals != null && game_.reviewTotalText >= 0 && game_.reviewTotalText < totals.Count) ? totals[game_.reviewTotalText] : null);
			int at = (string.IsNullOrEmpty(verdict) ? (-1) : __result.LastIndexOf(verdict, StringComparison.Ordinal));
			__result = ((at > 0) ? (__result.Substring(0, at) + line + " " + __result.Substring(at)) : (__result + " " + line));
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Extra topics review text: " + ex.Message);
		}
	}

	public static string List(IEnumerable<int> topics)
	{
		List<string> names = topics.Where((int t) => t >= 0).Select(TopicName).ToList();
		if (names.Count == 0)
		{
			return "";
		}
		if (names.Count == 1)
		{
			return names[0];
		}
		return string.Join(", ", names.Take(names.Count - 1)) + " and " + names[names.Count - 1];
	}

	private static string MixLine(gameScript g, int[] extra)
	{
		int change = ReviewChange(g.maingenre, g.gameMainTheme, g.gameSubTheme, extra, out var verdicts);
		List<int> all = new List<int> { g.gameMainTheme, g.gameSubTheme };
		all.AddRange(extra);
		string mix = List(all);
		string added = List(extra);
		int pick = Mathf.Abs(g.myID) % 2;
		if (change >= 3)
		{
			return (pick == 0) ? ("Its mix of " + mix + " works brilliantly.") : ("Blending " + mix + " makes for a world worth getting lost in.");
		}
		if (change >= 1)
		{
			return (pick == 0) ? ("The extra layers of " + added + " add welcome depth.") : ("Weaving in " + added + " gives it more to discover.");
		}
		if (change == 0)
		{
			return added + ((Count(extra) > 1) ? " are" : " is") + " in there too, though " + ((Count(extra) > 1) ? "they don't" : "it doesn't") + " add much.";
		}
		Verdict worst = verdicts.OrderBy((Verdict v) => v.Score).FirstOrDefault();
		string odd = ((worst != null) ? TopicName(worst.Topic) : added);
		return (pick == 0) ? (odd + " feels out of place here.") : ("Throwing " + odd + " into the mix doesn't quite work.");
	}

	// More work: each feature takes longer (the game develops feature by feature).
	[HarmonyPostfix]
	[HarmonyPatch(typeof(gameScript), "FindNextFeatureForDevelopment")]
	private static void LongerFeature(gameScript __instance)
	{
		int[] extra = Of(__instance);
		if (extra != null && __instance.devPointsStart > 0f)
		{
			__instance.devPointsStart *= WorkFactor(extra);
			__instance.devPoints = __instance.devPointsStart;
		}
	}

	// A game just started on the design screen: its topics, and the work they add (the first feature was set up
	// before the topics were attached).
	public static void Started(gameScript g, int[] extra)
	{
		if (!g || Count(extra) == 0)
		{
			return;
		}
		Attach(g, extra);
		float f = WorkFactor(extra);
		g.devPointsStart *= f;
		g.devPoints = g.devPointsStart;
		g.devPointsStart_Gesamt = Mathf.Round(g.devPointsStart_Gesamt * f);
		g.devPoints_Gesamt = g.devPointsStart_Gesamt;
		Plugin.Log.LogInfo("Extra topics for " + g.GetNameSimple() + ": " + List(extra));
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
			forschungSonstiges fs = Other;
			for (int i = 0; i < 2; i++)
			{
				if ((bool)fs && fs.RES_POINTS_LEFT != null && fs.RES_POINTS_LEFT.Length > Slots[i])
				{
					S.ResearchLeft[i] = fs.RES_POINTS_LEFT[Slots[i]];
				}
			}
			writer.Write<string>(SaveKey, JsonConvert.SerializeObject(S));
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Saving extra topics failed: " + ex);
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(savegameScript), "Load")]
	private static void BeforeLoad()
	{
		S = new ExtraTopicsState();
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(savegameScript), "LoadHistory")]
	private static void AfterLoad(ES3File ___es3file)
	{
		try
		{
			S = ((___es3file != null && ___es3file.KeyExists(SaveKey)) ? JsonConvert.DeserializeObject<ExtraTopicsState>(___es3file.Load<string>(SaveKey)) : null) ?? new ExtraTopicsState();
			S.Games = S.Games ?? new Dictionary<int, int[]>();
			if (S.ResearchLeft == null || S.ResearchLeft.Length < 2)
			{
				S.ResearchLeft = new float[2] { -1f, -1f };
			}
			if (S.Announced == null || S.Announced.Length < 2)
			{
				S.Announced = new bool[2];
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Loading extra topics failed: " + ex);
			S = new ExtraTopicsState();
		}
		RestoreResearch();
	}

	// The research's progress from the addon's save: a save without it (older, or from before this version) has
	// nothing researched.
	private static void RestoreResearch()
	{
		try
		{
			if (!EnsureSlots())
			{
				return;
			}
			forschungSonstiges fs = Other;
			for (int i = 0; i < 2; i++)
			{
				int slot = Slots[i];
				float left = S.ResearchLeft[i];
				fs.RES_POINTS_LEFT[slot] = ((left < 0f) ? fs.RES_POINTS[slot] : Mathf.Min(left, fs.RES_POINTS[slot]));
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Topic research after loading: " + ex);
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(mainScript), "InitNewGame")]
	private static void NewGame()
	{
		S = new ExtraTopicsState();
		RestoreResearch();
	}
}
