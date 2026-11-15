using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using HolyAddon.Core.Industry;
using Newtonsoft.Json;
using UnityEngine;

namespace HolyAddon.Core.Landmarks;

// A game made as a later entry from the franchise list ("Metal Gear Solid"): the numbered sequels after it are named
// after it ("Metal Gear Solid 2", not "Metal Gear 3").
internal class SeriesStem
{
	public string Stem = "";

	public int Number = 1;
}

internal class SeriesState
{
	public Dictionary<int, SeriesStem> Stems = new Dictionary<int, SeriesStem>();
}

// Later entries of a series. The franchise list (Holy's real-world list) has them as franchises of their own (Mega Man
// X, Metal Gear Solid, Ocarina of Time), so their studio started each as a new series, even when someone else owned
// the original or you did. Now a later entry belongs to its series:
// - It's linked to the entry its name continues: "Metal Gear Solid" continues "Metal Gear". Across studios only when
//   the name says so, with a colon or a number ("Batman: Arkham Asylum", "Final Fantasy X"), not "Adventure Island"
//   after "Adventure". A landmark is never a later entry.
// - Whoever owns the series makes it, once its year comes: the owner's next new game is that entry, or the owner's
//   next sequel of that series takes its name. It's a sequel; a spin-off when it's another genre (and not numbered,
//   and not of a landmark series, where the addon's genre is the right one and the list's isn't).
// - Your series: no studio makes it.
// - The owner has closed: the entry's own studio makes it and takes the series over, once the series is free to change
//   hands (the game's IP market rule).
// - The series hasn't started yet: it waits, up to 3 years past its own year (then it's a series of its own, as before).
// - Only for entries not made yet: series started before this version stay as they are.
[HarmonyPatch]
internal static class Series
{
	private const string SaveKey = "mgt2.editor.series";

	private const int WaitYears = 3;

	private class Line
	{
		public string Name = "";

		public int Studio = -1;

		public int Genre = -1;

		public int SubGenre = -1;

		public int Year;

		public int Review;

		public int Base = -1;

		public bool Numbered;
	}

	private enum Kind
	{
		Free,
		Wait,
		Held,
		Owner,
		Orphan
	}

	private class Status
	{
		public Kind Kind;

		public gameScript Main;

		public int Studio = -1;
	}

	private class Pending
	{
		public int Line = -1;

		public int Main = -1;

		public bool Takeover;
	}

	// One look at the games, for one decision.
	private class Games
	{
		public readonly Dictionary<int, gameScript> ById = new Dictionary<int, gameScript>();

		public readonly Dictionary<string, List<gameScript>> ByName = new Dictionary<string, List<gameScript>>(StringComparer.OrdinalIgnoreCase);

		public readonly Dictionary<string, gameScript> Landmark = new Dictionary<string, gameScript>(StringComparer.OrdinalIgnoreCase);
	}

	private static readonly Regex StudioTag = new Regex("<P(\\d+)>");

	private static readonly Regex GenreTag = new Regex("<G(\\d+)>");

	private static readonly Regex SubGenreTag = new Regex("<SG(\\d+)>");

	private static readonly Regex YearTag = new Regex("<Y(\\d+)>");

	private static readonly Regex ReviewTag = new Regex("<%(\\d+)>");

	private static readonly string[] Roman = new string[20]
	{
		"I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X",
		"XI", "XII", "XIII", "XIV", "XV", "XVI", "XVII", "XVIII", "XIX", "XX"
	};

	private static SeriesState S = new SeriesState();

	private static string[] linkedArray;

	private static Line[] lines = new Line[0];

	private static readonly Dictionary<int, Pending> pending = new Dictionary<int, Pending>();

	// The pick in progress: the real "used" flags, what the game was shown, and the entry it was steered to.
	private static bool[] realUse;

	private static bool[] shownUse;

	private static int steered = -1;

	private static string steeredLine;

	private static Pending steeredTo;

	private static bool On
	{
		get
		{
			if (G.Singleplayer && (bool)G.MS)
			{
				return G.TS;
			}
			return false;
		}
	}

	// ---- The links ----

	private static string NameOf(string line)
	{
		int i = (line ?? "").IndexOf('<');
		return ((i < 0) ? (line ?? "") : line.Substring(0, i)).Trim();
	}

	private static int Tag(Regex r, string line, int none)
	{
		Match m = r.Match(line ?? "");
		if (!m.Success || !int.TryParse(m.Groups[1].Value, out var v))
		{
			return none;
		}
		return v;
	}

	// "Final Fantasy X", "OutRun 2", "Battlefield 1942": what follows the series' name starts with a number.
	private static bool NumberAfter(string name, int at)
	{
		string rest = name.Substring(at).TrimStart(' ', ':', '-');
		if (rest.Length == 0)
		{
			return false;
		}
		if (char.IsDigit(rest[0]))
		{
			return true;
		}
		int end = rest.IndexOfAny(new char[2] { ' ', ':' });
		return Array.IndexOf(Roman, (end < 0) ? rest : rest.Substring(0, end)) >= 0;
	}

	// Every entry of the franchise list, and the earlier entry of its series it follows (worked out again whenever the
	// list is replaced: a load, a new game, the landmark lines).
	private static bool Links()
	{
		textScript ts = G.TS;
		if (!ts || ts.npcIPs == null)
		{
			return false;
		}
		if (ts.npcIPs == linkedArray)
		{
			return true;
		}
		string[] arr = ts.npcIPs;
		Line[] list = new Line[arr.Length];
		Dictionary<string, List<int>> byName = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
		for (int i = 0; i < arr.Length; i++)
		{
			string raw = arr[i] ?? "";
			Line l = new Line
			{
				Name = NameOf(raw),
				Studio = Tag(StudioTag, raw, -1),
				Genre = Tag(GenreTag, raw, -1),
				SubGenre = Tag(SubGenreTag, raw, -1),
				Year = Tag(YearTag, raw, 0),
				Review = Tag(ReviewTag, raw, 0)
			};
			list[i] = l;
			if (l.Name.Length > 0)
			{
				if (!byName.TryGetValue(l.Name, out var same))
				{
					same = (byName[l.Name] = new List<int>());
				}
				same.Add(i);
			}
		}
		HashSet<string> landmarks = new HashSet<string>(Landmarks.All.Select((LandmarkDef d) => d.Title), StringComparer.OrdinalIgnoreCase);
		int linked = 0;
		for (int j = 0; j < list.Length; j++)
		{
			Line b = list[j];
			if (b.Name.Length < 4 || b.Studio < 0 || landmarks.Contains(b.Name))
			{
				continue;
			}
			for (int at = 3; at < b.Name.Length; at++)
			{
				char c = b.Name[at];
				if ((c != ' ' && c != ':') || !byName.TryGetValue(b.Name.Substring(0, at), out var bases))
				{
					continue;
				}
				bool numbered = NumberAfter(b.Name, at);
				int found = -1;
				foreach (int a in bases)
				{
					Line l2 = list[a];
					if (a != j && l2.Year <= b.Year && (l2.Studio == b.Studio || c == ':' || numbered) && (found < 0 || l2.Year > list[found].Year))
					{
						found = a;
					}
				}
				if (found >= 0)
				{
					b.Base = found;
					b.Numbered = numbered;
				}
			}
			if (b.Base >= 0)
			{
				linked++;
			}
		}
		lines = list;
		linkedArray = arr;
		Plugin.Log.LogInfo("Series: " + linked + " later entries in the franchise list belong to their series");
		return true;
	}

	private static Games Look()
	{
		Games c = new Games();
		foreach (gameScript g in IndustryCore.AllGames())
		{
			c.ById[g.myID] = g;
			string n = g.GetNameSimple();
			if (!string.IsNullOrEmpty(n) && g.DeveloperIsNPC())
			{
				if (!c.ByName.TryGetValue(n, out var same))
				{
					same = (c.ByName[n] = new List<gameScript>());
				}
				same.Add(g);
			}
		}
		foreach (LandmarkDef d in Landmarks.All)
		{
			LandmarkRecord r = Landmarks.Record(d.Id);
			if (r != null && r.GameId >= 0 && c.ById.TryGetValue(r.GameId, out var lm))
			{
				c.Landmark[d.Title] = lm;
			}
		}
		return c;
	}

	// The game an entry was made as: a landmark's record (yours, if you made it), or the studio's game of that name
	// (games of yours with the same name don't count).
	private static gameScript GameOf(int i, Games c)
	{
		Line l = lines[i];
		if (c.Landmark.TryGetValue(l.Name, out var lm))
		{
			return lm;
		}
		if (!c.ByName.TryGetValue(l.Name, out var same))
		{
			return null;
		}
		return same.FirstOrDefault((gameScript g) => g.developerID == l.Studio) ?? same[0];
	}

	private static gameScript Main(gameScript g, Games c)
	{
		if (c.ById.TryGetValue(g.mainIP, out var m) && (bool)m)
		{
			return m;
		}
		return g;
	}

	private static Status StatusOf(int i, Games c, bool[] used)
	{
		Line b = lines[i];
		int root = i;
		for (int a = b.Base; a >= 0; a = lines[a].Base)
		{
			gameScript g = GameOf(a, c);
			if ((bool)g)
			{
				return ByOwner(b, Main(g, c));
			}
			root = a;
		}
		if (root == i || (root < used.Length && used[root]))
		{
			// Not a later entry, or its series was started and is gone.
			return new Status
			{
				Kind = Kind.Free
			};
		}
		publisherScript first = IndustryCore.Studio(lines[root].Studio);
		if ((bool)first && !first.Geschlossen() && G.MS.year <= b.Year + WaitYears)
		{
			return new Status
			{
				Kind = Kind.Wait
			};
		}
		return new Status
		{
			Kind = Kind.Free
		};
	}

	private static Status ByOwner(Line b, gameScript main)
	{
		if (main.inDevelopment || main.schublade)
		{
			return new Status
			{
				Kind = Kind.Wait,
				Main = main
			};
		}
		if (!main.OwnerIsNPC())
		{
			return new Status
			{
				Kind = Kind.Held,
				Main = main
			};
		}
		publisherScript owner = IndustryCore.Studio(main.ownerID);
		if ((bool)owner && !owner.Geschlossen())
		{
			return new Status
			{
				Kind = Kind.Owner,
				Main = main,
				Studio = owner.myID
			};
		}
		return new Status
		{
			Kind = Kind.Orphan,
			Main = main,
			Studio = b.Studio
		};
	}

	private static bool Unlocked(genres gen, Line l)
	{
		if (!gen || gen.genres_UNLOCK == null || l.Genre < 0 || l.Genre >= gen.genres_UNLOCK.Length || !gen.genres_UNLOCK[l.Genre])
		{
			return false;
		}
		if (l.SubGenre >= 0)
		{
			if (l.SubGenre < gen.genres_UNLOCK.Length)
			{
				return gen.genres_UNLOCK[l.SubGenre];
			}
			return false;
		}
		return true;
	}

	private static bool CanTakeOver(gameScript main, int studio)
	{
		publisherScript p = IndustryCore.Studio(studio);
		if ((bool)p && !p.Geschlossen() && (bool)G.MS.games_)
		{
			return G.MS.games_.IsIpFree(main, messageBox_: false);
		}
		return false;
	}

	// ---- A studio picks a franchise for its new game ----

	// Later entries aren't new series: the game doesn't see them, except the one this studio makes now as part of a
	// series it owns (or takes over), which it's steered to.
	[HarmonyPrefix]
	[HarmonyPriority(200)]
	[HarmonyPatch(typeof(textScript), "GetRandomNpcIP")]
	private static void BeforePick(int publisher_)
	{
		Clear();
		if (!On)
		{
			return;
		}
		try
		{
			Landmarks.FixLines();
			if (!Links())
			{
				return;
			}
			textScript ts = G.TS;
			bool[] used = Landmarks.InUse();
			if (used == null || used.Length < lines.Length)
			{
				return;
			}
			Games c = Look();
			genres gen = IndustryCore.Genres;
			int year = G.MS.year;
			bool[] shown = (bool[])used.Clone();
			bool hid = false;
			int pick = -1;
			Status pickStatus = null;
			for (int i = 0; i < lines.Length; i++)
			{
				Line l = lines[i];
				if (l.Base < 0 || used[i])
				{
					continue;
				}
				Status st = StatusOf(i, c, used);
				if (st.Kind == Kind.Free)
				{
					continue;
				}
				shown[i] = true;
				hid = true;
				if (((st.Kind == Kind.Owner && st.Studio == publisher_) || (st.Kind == Kind.Orphan && l.Studio == publisher_)) && l.Year <= year && Unlocked(gen, l) && (pick < 0 || l.Year < lines[pick].Year) && (st.Kind != Kind.Orphan || CanTakeOver(st.Main, publisher_)))
				{
					pick = i;
					pickStatus = st;
				}
			}
			if (!hid)
			{
				return;
			}
			if (pick >= 0)
			{
				for (int k = 0; k < shown.Length; k++)
				{
					shown[k] = true;
				}
				shown[pick] = false;
				steered = pick;
				steeredLine = ts.npcIPs[pick];
				if (lines[pick].Studio != publisher_)
				{
					ts.npcIPs[pick] = StudioTag.Replace(steeredLine, "<P" + publisher_ + ">", 1);
				}
				steeredTo = new Pending
				{
					Line = pick,
					Main = pickStatus.Main.myID,
					Takeover = (pickStatus.Kind == Kind.Orphan)
				};
			}
			realUse = used;
			shownUse = (bool[])shown.Clone();
			ts.npcIPsInUse = shown;
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Series (a studio picking a franchise): " + ex);
			Restore(null);
		}
	}

	[HarmonyFinalizer]
	[HarmonyPatch(typeof(textScript), "GetRandomNpcIP")]
	private static Exception AfterPick(Exception __exception, gameScript game_)
	{
		Restore(game_);
		return __exception;
	}

	private static void Restore(gameScript game)
	{
		try
		{
			textScript ts = G.TS;
			if (!ts)
			{
				return;
			}
			int picked = -1;
			if (realUse != null)
			{
				bool[] now = ts.npcIPsInUse;
				if (now != null && shownUse != null)
				{
					for (int i = 0; i < Math.Min(now.Length, shownUse.Length); i++)
					{
						if (now[i] && !shownUse[i])
						{
							picked = i;
							break;
						}
					}
				}
				ts.npcIPsInUse = realUse;
				if (picked >= 0 && picked < realUse.Length)
				{
					realUse[picked] = true;
				}
			}
			if (steered >= 0 && steeredLine != null && ts.npcIPs != null && steered < ts.npcIPs.Length)
			{
				ts.npcIPs[steered] = steeredLine;
			}
			if (picked >= 0 && picked == steered && (bool)game && steeredTo != null)
			{
				pending[game.myID] = steeredTo;
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Series (after a studio picked a franchise): " + ex);
		}
		finally
		{
			Clear();
		}
	}

	private static void Clear()
	{
		realUse = null;
		shownUse = null;
		steered = -1;
		steeredLine = null;
		steeredTo = null;
	}

	// ---- The studio's new game ----

	[HarmonyPostfix]
	[HarmonyPatch(typeof(publisherScript), "CreateNewGame2")]
	private static void Created(publisherScript __instance, gameScript __result)
	{
		try
		{
			if (On && (bool)__result && (bool)__instance)
			{
				if (pending.TryGetValue(__result.myID, out var p))
				{
					Join(__result, p, __instance);
				}
				else if (__result.typ_nachfolger)
				{
					NextEntry(__result, __instance);
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Series (a studio's new game): " + ex);
		}
		finally
		{
			pending.Clear();
		}
	}

	// The entry the studio was steered to joins its series.
	private static void Join(gameScript g, Pending p, publisherScript studio)
	{
		Games c = Look();
		if (!c.ById.TryGetValue(p.Main, out var main) || !main)
		{
			return;
		}
		if (p.Takeover)
		{
			if (!G.MS.games_.IsIpFree(main, messageBox_: false))
			{
				return;
			}
			foreach (gameScript x in c.ById.Values)
			{
				if (x.mainIP == main.mainIP)
				{
					x.ipToSell = false;
					x.ownerS_ = null;
					x.ownerID = studio.myID;
				}
			}
		}
		gameScript last = Latest(main, c);
		if (!last)
		{
			return;
		}
		string name = g.GetNameSimple();
		Line l = ((p.Line >= 0 && p.Line < lines.Length) ? lines[p.Line] : null);
		bool numbered = l?.Numbered ?? false;
		bool sequel = numbered || g.maingenre == main.maingenre || Landmarks.Of(main) != null;
		g.mainIP = main.mainIP;
		g.script_mainIP = null;
		g.typ_standard = false;
		if (sequel)
		{
			if (numbered)
			{
				// "Final Fantasy X" is a game of the series' genre, whatever the list says.
				g.maingenre = main.maingenre;
			}
			g.typ_nachfolger = true;
			g.teile = Math.Max(2, last.teile + 1);
			g.originalGameID = last.myID;
			g.script_vorgaenger = last;
			last.nachfolger_created = true;
			g.myNameTeil1 = name;
			g.points_gameplay = Mathf.Max(g.points_gameplay, last.points_gameplay);
			g.points_grafik = Mathf.Max(g.points_grafik, last.points_grafik);
			g.points_sound = Mathf.Max(g.points_sound, last.points_sound);
			g.points_technik = Mathf.Max(g.points_technik, last.points_technik);
		}
		else
		{
			g.typ_spinoff = true;
			g.teile = 1;
			g.originalGameID = main.myID;
		}
		S.Stems[g.myID] = new SeriesStem
		{
			Stem = name
		};
		Told(studio, name, main, sequel, p.Takeover);
	}

	// The studio's own sequel of a series: a later entry that's due takes its name, or a numbered sequel follows one.
	private static void NextEntry(gameScript g, publisherScript studio)
	{
		bool[] used = Landmarks.InUse();
		if (Links() && used != null && used.Length >= lines.Length)
		{
			Games c = Look();
			genres gen = IndustryCore.Genres;
			int pick = -1;
			gameScript main = null;
			for (int i = 0; i < lines.Length; i++)
			{
				Line l = lines[i];
				if (l.Base < 0 || used[i] || l.Year > G.MS.year || (l.Genre != g.maingenre && !l.Numbered) || !Unlocked(gen, l) || (pick >= 0 && lines[pick].Year <= l.Year))
				{
					continue;
				}
				Status st = StatusOf(i, c, used);
				if (st.Kind == Kind.Owner && st.Studio == studio.myID && (bool)st.Main && st.Main.mainIP == g.mainIP)
				{
					pick = i;
					main = st.Main;
				}
			}
			if (pick >= 0)
			{
				Line l2 = lines[pick];
				used[pick] = true;
				g.SetMyName(l2.Name);
				g.myNameTeil1 = l2.Name;
				if (l2.Review > 0)
				{
					g.sonderIPMindestreview = l2.Review;
				}
				S.Stems[g.myID] = new SeriesStem
				{
					Stem = l2.Name
				};
				Told(studio, l2.Name, main, sequel: true, takeover: false);
				return;
			}
		}
		gameScript last = g.script_vorgaenger;
		if ((bool)last && S.Stems.TryGetValue(last.myID, out var stem) && !string.IsNullOrEmpty(stem.Stem))
		{
			int n = stem.Number + 1;
			g.SetMyName(Numbered(stem.Stem, n, g));
			g.myNameTeil1 = stem.Stem;
			S.Stems[g.myID] = new SeriesStem
			{
				Stem = stem.Stem,
				Number = n
			};
		}
	}

	// The series' newest entry (its sequels' predecessor).
	private static gameScript Latest(gameScript main, Games c)
	{
		gameScript best = null;
		foreach (gameScript x in c.ById.Values)
		{
			if (x.mainIP == main.mainIP && !x.inDevelopment && !x.schublade && !x.pubAngebot && x.portID == -1 && (x.typ_standard || x.typ_nachfolger) && (!best || x.teile > best.teile || (x.teile == best.teile && x.myID > best.myID)))
			{
				best = x;
			}
		}
		return best;
	}

	// Numbered like the game's own sequels: a year for yearly genres, Roman numerals for studios that use them.
	private static string Numbered(string stem, int n, gameScript g)
	{
		genres gen = IndustryCore.Genres;
		if ((bool)gen && gen.genres_SUC_YEAR != null && g.maingenre >= 0 && g.maingenre < gen.genres_SUC_YEAR.Length && gen.genres_SUC_YEAR[g.maingenre])
		{
			return stem + " " + ((G.MS.year < 2000) ? (G.MS.year - 1900) : G.MS.year);
		}
		if (g.npcLateinNumbers && n >= 1 && n <= Roman.Length)
		{
			return stem + " " + Roman[n - 1];
		}
		return stem + " " + n;
	}

	private static void Told(publisherScript studio, string name, gameScript main, bool sequel, bool takeover)
	{
		string series = ((bool)main ? main.GetIpName() : "");
		string what = (sequel ? ("the next " + series + " game") : ("a " + series + " spin-off"));
		Plugin.Log.LogInfo("Series: " + studio.GetName() + " makes " + name + ", " + what + (takeover ? " (taking the series over)" : ""));
		IndustryCore.Feed("<b>" + studio.GetName() + "</b> is making " + name + ", " + what + (takeover ? ", and the series is theirs now" : "") + ".");
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
			Plugin.Log.LogError("Saving series failed: " + ex);
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(savegameScript), "Load")]
	private static void BeforeLoad()
	{
		S = new SeriesState();
		linkedArray = null;
		pending.Clear();
		Clear();
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(savegameScript), "LoadHistory")]
	private static void AfterLoad(ES3File ___es3file)
	{
		try
		{
			S = ((___es3file != null && ___es3file.KeyExists(SaveKey)) ? JsonConvert.DeserializeObject<SeriesState>(___es3file.Load<string>(SaveKey)) : null) ?? new SeriesState();
			S.Stems = S.Stems ?? new Dictionary<int, SeriesStem>();
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Loading series failed: " + ex);
			S = new SeriesState();
		}
		linkedArray = null;
	}

	// Before the studios pick their first franchises (they do while a new game is set up).
	[HarmonyPrefix]
	[HarmonyPatch(typeof(mainScript), "InitNewGame")]
	private static void NewGame()
	{
		S = new SeriesState();
		linkedArray = null;
		pending.Clear();
		Clear();
	}
}
