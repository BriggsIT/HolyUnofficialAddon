using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HolyAddon.Core.CopyProtection;
using HolyAddon.Core.Updates;
using HolyAddon.UI;
using Newtonsoft.Json;
using UnityEngine;

namespace HolyAddon.Core.Industry;

internal class RelationNote
{
	public int Week;

	public int Delta;

	public string Text = "";
}

// What an event is about: the studios, games and platform it involves, and its numbers.
internal class IndustryContext
{
	public int StudioId = -1;

	public int Studio2 = -1;

	public int GameId = -1;

	public int Game2 = -1;

	public int PlatformId = -1;

	public long Amount;

	public long Amount2;

	public float Share;

	public float Share2;

	public int Number;
}

internal class IndustryPending
{
	public string Id = "";

	public IndustryContext C = new IndustryContext();

	public int Week;
}

// Money coming in or going out each week: a fixed sum, or a share of a game's revenue (once it's out).
internal class IncomeStream
{
	public string Label = "";

	public int StudioId = -1;

	public int GameId = -1;

	public float Share;

	public long Fixed;

	public int WeeksLeft;

	public bool Out;

	public int Category = 2;

	public long LastRevenue = -1;

	public bool Started;
}

internal class SalesMod
{
	public int GameId;

	public float Amount;

	public int Weeks;

	public int WeeksLeft;
}

// "Release a game on it within a year, or pay for the kit."
internal class PlatformPromise
{
	public int PlatformId;

	public int Start;

	public int Deadline;

	public long Price;
}

internal class IndustryState
{
	// Your standing with each studio, -100 to 100 (0 = neutral), from what happened between you.
	public Dictionary<int, float> Standing = new Dictionary<int, float>();

	public Dictionary<int, List<RelationNote>> Notes = new Dictionary<int, List<RelationNote>>();

	public Dictionary<string, int> LastFired = new Dictionary<string, int>();

	public Dictionary<string, int> Cooldowns = new Dictionary<string, int>();

	public int LastDecision = -1;

	public int NextHeadline = -1;

	public IndustryPending Pending;

	public List<IncomeStream> Streams = new List<IncomeStream>();

	public List<SalesMod> Mods = new List<SalesMod>();

	public List<PlatformPromise> Promises = new List<PlatformPromise>();

	// The latest headlines and decisions, newest first.
	public List<string> Feed = new List<string>();

	public List<int> KnownOpen = new List<int>();

	public List<int> KnownClosed = new List<int>();

	public bool Seeded;

	public List<int> DealtGames = new List<int>();
}

// Industry events: the industry around you moves (headlines in the news), and things land on your desk (pop-up
// decisions with choices) that change your money and fans, the market, and your standing with other studios. The
// director paces decisions at random: the longer it's been quiet, the likelier one becomes, and a fitting moment
// (you just released a game) makes it likelier still. Events are in IndustryEvents.
[HarmonyPatch]
internal static class IndustryCore
{
	private const string SaveKey = "mgt2.editor.industry";

	private static IndustryState S = new IndustryState();

	private static string shownFor;

	public static IndustryState State => S;

	public static bool On
	{
		get
		{
			if (Plugin.IndustryEvents.Value)
			{
				return G.Singleplayer;
			}
			return false;
		}
	}

	public static int Now => CopyProtectDev.AbsWeek;

	public static int Year => G.MS ? G.MS.year : 1976;

	public static readonly string[] PaceNames = new string[3] { "Calm", "Normal", "Busy" };

	private static float Pace => Plugin.IndustryPace.Value switch
	{
		0 => 0.6f,
		2 => 1.6f,
		_ => 1f,
	};

	// ---- Studios and standing ----

	public static List<publisherScript> Studios()
	{
		publisherScript[] all = (G.MS ? G.MS.arrayPublisherScripts : null);
		if (all == null)
		{
			return new List<publisherScript>();
		}
		return all.Where((publisherScript s) => (bool)s && !s.isPlayer && s.isUnlocked && !s.Geschlossen() && !s.IsTochterfirma()).ToList();
	}

	public static publisherScript Studio(int id)
	{
		publisherScript[] all = (G.MS ? G.MS.arrayPublisherScripts : null);
		if (all == null || id < 0)
		{
			return null;
		}
		return all.FirstOrDefault((publisherScript s) => (bool)s && s.myID == id);
	}

	// Your events with them, plus a little from your publishing history (the game's own relation, 0-100).
	public static float Standing(publisherScript p)
	{
		if (!p)
		{
			return 0f;
		}
		float own = (S.Standing.TryGetValue(p.myID, out var v) ? v : 0f);
		return Mathf.Clamp(own + p.relation * 0.3f, -100f, 100f);
	}

	public static int Tier(float standing)
	{
		if (standing <= -40f)
		{
			return 0;
		}
		if (standing < 30f)
		{
			return 1;
		}
		if (standing < 70f)
		{
			return 2;
		}
		return 3;
	}

	public static readonly string[] TierNames = new string[4] { "Rival", "Neutral", "Friendly", "Ally" };

	public static Color TierColor(int tier)
	{
		return tier switch
		{
			0 => new Color(0.78f, 0.16f, 0.16f),
			2 => new Color(0.18f, 0.55f, 0.25f),
			3 => new Color(0.1f, 0.42f, 0.75f),
			_ => new Color(0.45f, 0.45f, 0.45f),
		};
	}

	public static string TierOf(publisherScript p)
	{
		return TierNames[Tier(Standing(p))];
	}

	// Changes your standing with a studio (good turns also improve the game's own relation: better publishing terms).
	public static void Change(publisherScript p, int delta, string why)
	{
		if (!p || delta == 0)
		{
			return;
		}
		int before = Tier(Standing(p));
		S.Standing[p.myID] = Mathf.Clamp((S.Standing.TryGetValue(p.myID, out var v) ? v : 0f) + (float)delta, -100f, 100f);
		if (delta > 0)
		{
			p.relation = Mathf.Min(100f, p.relation + (float)delta * 0.5f);
		}
		if (!S.Notes.TryGetValue(p.myID, out var notes))
		{
			notes = (S.Notes[p.myID] = new List<RelationNote>());
		}
		notes.Insert(0, new RelationNote
		{
			Week = Now,
			Delta = delta,
			Text = why
		});
		if (notes.Count > 12)
		{
			notes.RemoveAt(notes.Count - 1);
		}
		int after = Tier(Standing(p));
		if (after != before)
		{
			News("<b>" + p.GetName() + "</b> now sees you as " + ((after == 3) ? "an ally" : ("a " + TierNames[after].ToLower())) + ".");
		}
		GameUpdates.Touch();
	}

	// ---- Your games, the market ----

	public static List<gameScript> AllGames()
	{
		gameScript[] all = ((G.MS && (bool)G.MS.games_) ? G.MS.games_.arrayGamesScripts : null);
		if (all == null)
		{
			return new List<gameScript>();
		}
		return all.Where((gameScript g) => (bool)g).ToList();
	}

	public static List<gameScript> MyGamesOnMarket()
	{
		return AllGames().Where((gameScript g) => g.isOnMarket && !g.inDevelopment && g.IsMyGame() && !g.typ_bundle && !g.typ_bundleAddon && !g.typ_contractGame).ToList();
	}

	public static List<gameScript> MyGamesInDevelopment()
	{
		return AllGames().Where((gameScript g) => g.inDevelopment && g.IsMyGame() && !g.typ_contractGame && !g.auftragsspiel).ToList();
	}

	public static genres Genres => G.MainObj ? G.MainObj.GetComponent<genres>() : null;

	public static string GenreName(int g)
	{
		genres gen = Genres;
		if (!gen || g < 0)
		{
			return "games";
		}
		return gen.GetName(g);
	}

	// What a game was sent on, by year.
	public static string Media()
	{
		if (Year < 1983)
		{
			return "cassette";
		}
		if (Year < 1993)
		{
			return "floppy disk";
		}
		if (Year < 2003)
		{
			return "CD";
		}
		return "download link";
	}

	public static void Feed(string text)
	{
		S.Feed.Insert(0, ((G.Gui != null) ? (G.Gui.GetDate().Replace("\r", "").Replace("\n", " ") + ": ") : "") + text);
		if (S.Feed.Count > 40)
		{
			S.Feed.RemoveAt(S.Feed.Count - 1);
		}
	}

	public static void News(string text)
	{
		if ((bool)G.Gui)
		{
			G.Gui.CreateTopNewsInfo(text);
		}
	}

	// ---- Lasting effects ----

	public static void AddMod(gameScript g, float amount, int weeks)
	{
		if ((bool)g)
		{
			S.Mods.Add(new SalesMod
			{
				GameId = g.myID,
				Amount = amount,
				Weeks = weeks,
				WeeksLeft = weeks
			});
		}
	}

	// What events add to a game's sales (into Holy's total and cap with Holy, like the addon's other bonuses).
	public static float Bonus(gameScript g)
	{
		if (!On || !g || S.Mods.Count == 0)
		{
			return 0f;
		}
		float total = 0f;
		foreach (SalesMod m in S.Mods)
		{
			if (m.GameId == g.myID && m.WeeksLeft > 0)
			{
				total += m.Amount;
			}
		}
		return total;
	}

	private static void Effects()
	{
		foreach (SalesMod m in S.Mods)
		{
			m.WeeksLeft--;
		}
		S.Mods.RemoveAll((SalesMod m) => m.WeeksLeft <= 0);
		foreach (IncomeStream s in S.Streams.ToList())
		{
			if (s.GameId >= 0)
			{
				gameScript g = GameUpdates.FindGame(s.GameId);
				if (!g)
				{
					S.Streams.Remove(s);
					continue;
				}
				if (!g.isOnMarket)
				{
					// Not out yet: waits (a game that's gone without release ends it).
					if (!g.inDevelopment)
					{
						S.Streams.Remove(s);
					}
					continue;
				}
				if (!s.Started)
				{
					s.Started = true;
					s.LastRevenue = g.umsatzTotal;
					continue;
				}
				long amount = (long)((float)Math.Max(0L, g.umsatzTotal - s.LastRevenue) * s.Share);
				s.LastRevenue = g.umsatzTotal;
				Move(s, amount);
			}
			else
			{
				Move(s, s.Fixed);
			}
			s.WeeksLeft--;
			if (s.WeeksLeft <= 0)
			{
				S.Streams.Remove(s);
				Feed(s.Label + " has ended.");
			}
		}
		foreach (PlatformPromise p in S.Promises.ToList())
		{
			platformScript platform = GameUpdates.Platform(p.PlatformId);
			if (!platform)
			{
				S.Promises.Remove(p);
				continue;
			}
			gameScript kept = MyGamesOnMarket().FirstOrDefault((gameScript g) => g.gamePlatform != null && g.gamePlatform.Contains(p.PlatformId) && g.date_year * 48 + (g.date_month - 1) * 4 >= p.Start - 8);
			if ((bool)kept)
			{
				S.Promises.Remove(p);
				G.MS.AddFans(Mathf.RoundToInt(500f * (float)Math.Max(1, Year - 1975) * 0.1f + 300f), -1);
				News("<b>" + platform.GetName() + "</b>'s maker is pleased: " + kept.GetNameSimple() + " is out on its machine. The dev kit is yours to keep.");
				Feed("Kept the promise to release on " + platform.GetName() + ".");
			}
			else if (Now >= p.Deadline)
			{
				S.Promises.Remove(p);
				G.MS.Pay(p.Price, 3);
				News("No game of yours came out on <b>" + platform.GetName() + "</b> in time: you pay " + G.Money(p.Price) + " for the dev kit after all.");
				Feed("Paid " + G.Money(p.Price) + " for the " + platform.GetName() + " dev kit (promise not kept).");
			}
		}
	}

	private static void Move(IncomeStream s, long amount)
	{
		if (amount <= 0 || !G.MS)
		{
			return;
		}
		if (s.Out)
		{
			G.MS.Pay(amount, 7);
		}
		else
		{
			G.MS.Earn(amount, s.Category);
		}
	}

	// ---- The director ----

	[HarmonyPostfix]
	[HarmonyPatch(typeof(mainScript), "WochenUpdates")]
	private static void Weekly(mainScript __instance)
	{
		try
		{
			if (!On)
			{
				return;
			}
			Effects();
			StudioChanges();
			if (S.NextHeadline < 0)
			{
				S.NextHeadline = Now + UnityEngine.Random.Range(2, 5);
			}
			if (Now >= S.NextHeadline)
			{
				IndustryEvents.Headline();
				S.NextHeadline = Now + Mathf.RoundToInt((float)UnityEngine.Random.Range(3, 8) / Pace);
			}
			if (S.Pending == null && Quiet())
			{
				int since = ((S.LastDecision < 0) ? 6 : (Now - S.LastDecision));
				float chance = ((since < 4) ? 0f : Mathf.Min(0.3f, 0.035f + 0.012f * (float)(since - 4))) * Pace;
				if (MyGamesOnMarket().Any((gameScript g) => g.weeksOnMarket <= 1))
				{
					chance *= 1.5f;
				}
				if (UnityEngine.Random.value < chance)
				{
					IndustryPending p = IndustryEvents.PickDecision();
					if (p != null)
					{
						S.Pending = p;
						S.LastDecision = Now;
						S.LastFired[p.Id] = Now;
						shownFor = null;
					}
				}
			}
			else if (S.Pending != null && Now - S.Pending.Week > 8)
			{
				// Left unanswered for two months: it goes the default way.
				Decide(-1);
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Industry events: " + ex);
		}
	}

	// Not in a week when another big event is on screen or waiting (the game's world events, Holy's PR events).
	private static bool Quiet()
	{
		if (!G.Gui || G.Gui.menuOpen)
		{
			return false;
		}
		try
		{
			if (G.Gui.uiObjects != null && G.Gui.uiObjects.Length > 216 && (bool)G.Gui.uiObjects[216] && G.Gui.uiObjects[216].activeSelf)
			{
				return false;
			}
		}
		catch
		{
		}
		object pr = HolyCompat.HolyState("Pr");
		if (pr != null && HolyCompat.Get<object>(pr, "Pending") != null)
		{
			return false;
		}
		return true;
	}

	// New studios and closures make the news (the first look at a save only takes note of them).
	private static void StudioChanges()
	{
		publisherScript[] all = (G.MS ? G.MS.arrayPublisherScripts : null);
		if (all == null)
		{
			return;
		}
		foreach (publisherScript p in all)
		{
			if (!p || p.isPlayer || p.IsTochterfirma())
			{
				continue;
			}
			bool closed = p.Geschlossen();
			if (p.isUnlocked && !closed && !S.KnownOpen.Contains(p.myID))
			{
				S.KnownOpen.Add(p.myID);
				if (S.Seeded)
				{
					string text = "<b>" + p.GetName() + "</b> opens its doors" + ((p.fanGenre >= 0) ? (": a new studio with a love of " + GenreName(p.fanGenre) + " games.") : ".");
					News(text);
					Feed(text);
				}
			}
			if (closed && !S.KnownClosed.Contains(p.myID))
			{
				S.KnownClosed.Add(p.myID);
				if (S.Seeded)
				{
					string text2 = "<b>" + p.GetName() + "</b> has closed its doors.";
					News(text2);
					Feed(text2);
				}
			}
		}
		S.Seeded = true;
	}

	// Opens the decision's pop-up once nothing else is on screen; closing it goes the default way.
	public static void Tick()
	{
		if (!On || S.Pending == null || !G.Gui)
		{
			return;
		}
		string key = S.Pending.Id + "@" + S.Pending.Week;
		if (shownFor != key)
		{
			if (!G.Gui.menuOpen && !EditorWindow.IndustryEvent.IsOpen)
			{
				shownFor = key;
				EditorWindow.IndustryEvent.Open();
				Awards.AwardsShow.Sound(50);
			}
		}
		else if (!EditorWindow.IndustryEvent.IsOpen)
		{
			Decide(-1);
		}
	}

	// Applies a choice (-1 = the event's default) and returns what happened.
	public static string Decide(int choice)
	{
		IndustryPending p = S.Pending;
		if (p == null)
		{
			return null;
		}
		S.Pending = null;
		shownFor = null;
		IndustryEventDef def = IndustryEvents.Find(p.Id);
		if (def == null)
		{
			return null;
		}
		List<IndustryChoice> choices = def.Choices(p.C);
		int i = ((choice < 0 || choice >= choices.Count) ? Mathf.Clamp(def.Default, 0, choices.Count - 1) : choice);
		string result;
		try
		{
			result = choices[i].Apply(p.C);
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Industry event " + p.Id + ": " + ex);
			result = "Something went wrong.";
		}
		Feed(def.Title(p.C) + ": " + choices[i].Title + ".");
		GameUpdates.Touch();
		return result;
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
			Plugin.Log.LogError("Saving the industry failed: " + ex);
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(savegameScript), "Load")]
	private static void BeforeLoad()
	{
		S = new IndustryState();
		shownFor = null;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(savegameScript), "LoadHistory")]
	private static void Load(ES3File ___es3file)
	{
		try
		{
			S = ((___es3file != null && ___es3file.KeyExists(SaveKey)) ? JsonConvert.DeserializeObject<IndustryState>(___es3file.Load<string>(SaveKey)) : null) ?? new IndustryState();
			S.Standing = S.Standing ?? new Dictionary<int, float>();
			S.Notes = S.Notes ?? new Dictionary<int, List<RelationNote>>();
			S.LastFired = S.LastFired ?? new Dictionary<string, int>();
			S.Cooldowns = S.Cooldowns ?? new Dictionary<string, int>();
			S.Streams = S.Streams ?? new List<IncomeStream>();
			S.Mods = S.Mods ?? new List<SalesMod>();
			S.Promises = S.Promises ?? new List<PlatformPromise>();
			S.Feed = S.Feed ?? new List<string>();
			S.KnownOpen = S.KnownOpen ?? new List<int>();
			S.KnownClosed = S.KnownClosed ?? new List<int>();
			S.DealtGames = S.DealtGames ?? new List<int>();
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Loading the industry failed: " + ex);
			S = new IndustryState();
		}
		shownFor = null;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(mainScript), "InitNewGame")]
	private static void NewGame()
	{
		S = new IndustryState();
		shownFor = null;
	}
}
