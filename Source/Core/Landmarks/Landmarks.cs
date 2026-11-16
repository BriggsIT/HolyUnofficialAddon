using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using HolyAddon.Core.Industry;
using HolyAddon.Core.Updates;
using HolyAddon.UI;
using Newtonsoft.Json;
using UnityEngine;

namespace HolyAddon.Core.Landmarks;

// A landmark: a historical game you can make first (Docs/Landmarks-Plan.md). Its Title is the franchise's line in the
// game's NPC IP list (Holy's real-world list); Genre and Topics use the game's numbers.
internal class LandmarkDef
{
	public string Id = "";

	public string Name = "";

	public string Title = "";

	public int Year;

	public string Studio = "";

	public int Genre;

	public int[] Topics = new int[0];

	public int[] Extra = new int[0];

	public int Review;

	public bool Add;

	// An MMO (Ultima Online, EverQuest, World of Warcraft): made as an MMO; its entries are its expansions.
	public bool Mmo;

	// A phone game (Angry Birds, Candy Crush Saga, Pokemon Go): yours has to be a mobile game to become it.
	public bool Mobile;

	// The series' later games, in order (Super Mario Bros. 3, Super Mario World...): see LandmarkEntries.
	public List<LandmarkEntry> Entries = new List<LandmarkEntry>();

	// The projected review it needs: its real score less 10, at least 70.
	public int Bar => Mathf.Max(70, Review - 10);

	// The first year you can make it: a few years before its real one.
	public int From => Year - Landmarks.Lead;
}

internal class LandmarkFile
{
	public List<LandmarkDef> Landmarks = new List<LandmarkDef>();
}

// Who made a landmark (or is making it).
internal class LandmarkRecord
{
	public string Id = "";

	public int GameId = -1;

	public int StudioId = -1;

	public bool Player;

	public bool Released;

	public int Year;

	// Your sequel chose to be this entry when it was started (LandmarkSequelStart): it's the entry whatever its review.
	public bool Committed;

	// That sequel came out below the entry's bar or too late: no rewards, and not a landmark game for the tie-ins.
	public bool Short;

	// A studio's game of it came out more than StudioYears after its real year (Fire Emblem in 2009): just a game, and
	// the landmark counts as missed (the user's rule, 16 Nov 2026).
	public bool Missed;
}

internal class LandmarkState
{
	public Dictionary<string, LandmarkRecord> Made = new Dictionary<string, LandmarkRecord>();

	// Your games you chose not to make landmarks (the pop-up doesn't ask again).
	public List<int> Declined = new List<int>();

	// Your landmarks out now, waiting for their pop-up.
	public List<string> Celebrate = new List<string>();

	// Series entries made (by you or a studio), by entry key ("super-mario-bros/super-mario-world").
	public Dictionary<string, LandmarkRecord> Entries = new Dictionary<string, LandmarkRecord>();

	// Anniversary remasters you planned: the series (its main game's id) and the last year it counts.
	public Dictionary<int, int> Remasters = new Dictionary<int, int>();

	// Studios copying the latest landmark's genre and topic, until this week.
	public int CopyGenre = -1;

	public int CopyTopic = -1;

	public int CopyUntil = -1;

	public string CopyOf = "";

	// Landmarks in the awards' Hall of Fame (landmark ids and entry keys).
	public List<string> HallOfFame = new List<string>();

	// Your landmark series whose real games have run out (LandmarkSeriesDone), and when: year * 12 + month (0-based).
	public Dictionary<string, int> Complete = new Dictionary<string, int>();
}

// The pop-up's question: this game can become this landmark.
internal class LandmarkOffer
{
	public gameScript Game;

	public LandmarkDef Def;

	public int Review;

	public bool OfferGenre;

	public publisherScript Rival;

	public Menu_DevGame_Complete Menu;

	// A sequel of your landmark series becoming the series' next real game (null: the landmark itself).
	public LandmarkEntry Entry;

	// Asked when the sequel is set up, before there's a game (LandmarkSequelStart): its design screen and the game it
	// follows.
	public bool AtStart;

	public Menu_DevGame DevMenu;

	public gameScript Original;
}

// Landmark games:
// - The franchise lines in the game's NPC IP list (textScript.npcIPs) for each landmark get the landmark's genre and
//   topics (Holy's list files many games loosely, some in genres that unlock decades later), and missing ones are
//   added for their studio. NPC studios then make them on time, as real games of that genre.
// - When one of your new games is finished and fits a landmark (its genre and topics, the game's own projected review,
//   the years), a pop-up offers to make it that landmark: it's renamed, shows the landmark's pictures, and the
//   franchise's line is marked used so its studio never makes it (if it was already making it, it shelves it).
// - At release, a landmark brings IP points, fans in its genre and hype, and its genre becomes the trend. An NPC studio
//   that makes one is known for its genre from then on.
// - Pictures: landmarks/pictures/<id>/ in the plugin folder (PNG or JPG, any size: cropped to 4:3), one per game.
[HarmonyPatch]
internal static partial class Landmarks
{
	public const int Lead = 3;

	private const string SaveKey = "mgt2.editor.landmarks";

	private static LandmarkState S = new LandmarkState();

	private static List<LandmarkDef> defs;

	private static string[] fixedArray;

	private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

	public static LandmarkState State => S;

	public static LandmarkOffer Offer { get; private set; }

	public static string Showing { get; private set; }

	public static bool On
	{
		get
		{
			if (G.Singleplayer)
			{
				return All.Count > 0;
			}
			return false;
		}
	}

	public static string Folder => Path.Combine(Plugin.PluginDir ?? "", "landmarks");

	public static string PictureFolder(LandmarkDef d)
	{
		return Path.Combine(Path.Combine(Folder, "pictures"), d.Id);
	}

	// ---- Data ----

	// A landmark nobody has made by this many years after its real year is gone (no Pac-Man in 2005).
	public const int Window = 5;

	// The landmarks this game can have: their genre and required topics exist. Read once the game's genres and topics
	// are loaded (empty before that).
	public static List<LandmarkDef> All
	{
		get
		{
			if (defs == null)
			{
				Load();
			}
			return defs ?? new List<LandmarkDef>();
		}
	}

	// A studio's game is the landmark only if it comes out by this many years after its real year (1997: by 1999). Its
	// line only opens to studios in its real year, so one can't be early. Later, it's just a game and the landmark is
	// missed. (You can still claim one nobody made until Window years after.)
	public const int StudioYears = 2;

	public static bool Missed(LandmarkDef d)
	{
		LandmarkRecord r = Record(d.Id);
		if (r != null)
		{
			return r.Missed;
		}
		if ((bool)G.MS)
		{
			return G.MS.year > d.Year + Window;
		}
		return false;
	}

	// Its line is hidden from studios once a game started now would most likely come out too late (they take a year or
	// so): picks stop a year before StudioYears runs out.
	internal static bool ClosedToStudios(int line, int year)
	{
		if (landmarkAt.TryGetValue(line, out var d))
		{
			return year > d.Year + StudioYears - 1;
		}
		return false;
	}

	// Line index in npcIPs > the landmark it is (FixLines).
	private static readonly Dictionary<int, LandmarkDef> landmarkAt = new Dictionary<int, LandmarkDef>();

	public static LandmarkDef Def(string id)
	{
		return All.FirstOrDefault((LandmarkDef d) => d.Id == id);
	}

	// Read once the game's genres and topics are loaded (in the main menu they aren't, and a list read there came out
	// empty for the whole session): an empty result is never kept, and the list is read again for every save and new
	// game. Not tied to the office being loaded: studios pick their franchises while a new game is set up.
	private static void Load()
	{
		genres gen = IndustryCore.Genres;
		themes th = Themes;
		if (!G.MS || !gen || !th || gen.genres_UNLOCK == null || th.themes_LEVEL == null || gen.genres_UNLOCK.Length == 0 || th.themes_LEVEL.Length == 0)
		{
			return;
		}
		try
		{
			string file = Path.Combine(Folder, "landmarks.json");
			if (!File.Exists(file))
			{
				defs = new List<LandmarkDef>();
				Plugin.Log.LogWarning("Landmarks: " + file + " is missing");
				return;
			}
			List<LandmarkDef> list = JsonConvert.DeserializeObject<LandmarkFile>(File.ReadAllText(file))?.Landmarks ?? new List<LandmarkDef>();
			List<LandmarkDef> loaded = new List<LandmarkDef>();
			List<string> skipped = new List<string>();
			foreach (LandmarkDef d in list)
			{
				d.Topics = d.Topics ?? new int[0];
				d.Extra = d.Extra ?? new int[0];
				bool genreOk = d.Genre >= 0 && d.Genre < gen.genres_UNLOCK.Length;
				bool topicsOk = d.Topics.Length > 0 && d.Topics.All((int t) => t >= 0 && t < th.themes_LEVEL.Length);
				if (!genreOk || !topicsOk)
				{
					skipped.Add(d.Id);
					continue;
				}
				d.Extra = d.Extra.Where((int t) => t >= 0 && t < th.themes_LEVEL.Length).ToArray();
				PrepareEntries(d, gen.genres_UNLOCK.Length);
				loaded.Add(d);
			}
			if (loaded.Count == 0 && list.Count > 0)
			{
				// Nothing fits: the game's lists aren't complete yet; try again later.
				return;
			}
			if (skipped.Count > 0)
			{
				Plugin.Log.LogWarning("Landmarks skipped (their genre or topics aren't in this game): " + string.Join(", ", skipped));
			}
			defs = loaded;
			Plugin.Log.LogInfo("Landmarks: " + defs.Count + " loaded");
			Folders();
		}
		catch (Exception ex)
		{
			defs = new List<LandmarkDef>();
			Plugin.Log.LogError("Loading landmarks failed: " + ex);
		}
	}

	private static themes Themes => G.MainObj ? G.MainObj.GetComponent<themes>() : null;

	// A folder for every landmark's pictures, and a checklist of what goes where.
	private static void Folders()
	{
		try
		{
			string root = Path.Combine(Folder, "pictures");
			Directory.CreateDirectory(root);
			List<string> lines = new List<string>
			{
				"Landmark pictures",
				"=================",
				"Put a few gameplay screenshots of each landmark in its folder (PNG or JPG, any size: they're cropped to 4:3 and",
				"scaled). Each landmark game of yours or another studio's shows one of them, the same one every time. A landmark",
				"without pictures shows the game's usual screenshot.",
				""
			};
			foreach (LandmarkDef d in defs.OrderBy((LandmarkDef x) => x.Year))
			{
				Directory.CreateDirectory(PictureFolder(d));
				lines.Add(d.Id.PadRight(24) + d.Name + " (" + d.Year + ", " + d.Studio + ")");
				// The series' later games: a folder each inside the landmark's (an empty one shows the landmark's).
				foreach (LandmarkEntry e in d.Entries)
				{
					Directory.CreateDirectory(EntryFolder(e));
					lines.Add("  " + (d.Id + "/" + e.Id).PadRight(46) + e.Name + " (" + e.Year + ")" + (e.Milestone ? "  milestone" : ""));
				}
			}
			File.WriteAllLines(Path.Combine(root, "README.txt"), lines);
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Landmark picture folders: " + ex.Message);
		}
	}

	public static int PictureCount(LandmarkDef d)
	{
		try
		{
			return Pictures(d).Count;
		}
		catch
		{
			return 0;
		}
	}

	private static readonly Dictionary<string, List<string>> pictureFiles = new Dictionary<string, List<string>>();

	// Pictures added while the game runs show up after the Landmarks tab is opened again.
	public static void RefreshPictures()
	{
		pictureFiles.Clear();
	}

	private static List<string> Pictures(LandmarkDef d)
	{
		if (pictureFiles.TryGetValue(d.Id, out var cached))
		{
			return cached;
		}
		string dir = PictureFolder(d);
		List<string> files = ((!Directory.Exists(dir)) ? new List<string>() : (from f in Directory.GetFiles(dir)
			where f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
			orderby f
			select f).ToList());
		pictureFiles[d.Id] = files;
		return files;
	}

	// ---- The franchise lines ----

	private static readonly Regex Genre = new Regex("<G\\d+>");

	private static readonly Regex SubGenre = new Regex("<SG\\d+>");

	private static readonly Regex Topic = new Regex("<T\\d+>");

	private static readonly Regex SubTopic = new Regex("<ST\\d+>");

	private static readonly Regex YearTag = new Regex("<Y(\\d+)>");

	private static string LineName(string line)
	{
		int i = (line ?? "").IndexOf('<');
		return ((i < 0) ? (line ?? "") : line.Substring(0, i)).Trim();
	}

	private static int LineYear(string line)
	{
		Match m = YearTag.Match(line ?? "");
		if (!m.Success)
		{
			return 0;
		}
		return int.Parse(m.Groups[1].Value);
	}

	// The landmark's line: its title; with several (a publisher's and a developer's, or two years), its own studio's,
	// then the one nearest its year.
	private static int LineOf(string[] lines, LandmarkDef d)
	{
		int studio = (((bool)G.MS && !string.IsNullOrEmpty(d.Studio)) ? StudioByName(d.Studio) : (-1));
		string tag = ((studio >= 0) ? ("<P" + studio + ">") : null);
		int best = -1;
		bool bestOwn = false;
		for (int i = 0; i < lines.Length; i++)
		{
			if (!string.Equals(LineName(lines[i]), d.Title, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			bool own = tag != null && lines[i].Contains(tag);
			if (best < 0 || (own && !bestOwn) || (own == bestOwn && Math.Abs(LineYear(lines[i]) - d.Year) < Math.Abs(LineYear(lines[best]) - d.Year)))
			{
				best = i;
				bestOwn = own;
			}
		}
		return best;
	}

	private static string FixLine(string line, LandmarkDef d)
	{
		string fixedLine = Genre.Replace(line, "<G" + d.Genre + ">", 1);
		fixedLine = SubGenre.Replace(fixedLine, "");
		fixedLine = Topic.Replace(fixedLine, "<T" + d.Topics[0] + ">", 1);
		int second = ((d.Topics.Length > 1) ? d.Topics[1] : ((d.Extra.Length != 0) ? d.Extra[0] : (-1)));
		fixedLine = SubTopic.Replace(fixedLine, "");
		if (second >= 0)
		{
			fixedLine = fixedLine.Replace("<T" + d.Topics[0] + ">", "<T" + d.Topics[0] + "><ST" + second + ">");
		}
		if (d.Mmo && !fixedLine.Contains("<MMO>"))
		{
			fixedLine = fixedLine.TrimEnd() + "<MMO>";
		}
		// Studios can start it in its year (Holy's own year can differ: Rust's line is the 2018 release, the landmark the
		// 2013 early access).
		return YearTagRx.Replace(fixedLine, "<Y" + d.Year + ">", 1);
	}

	internal static int StudioByName(string name)
	{
		publisherScript p = (G.MS.arrayPublisherScripts ?? new publisherScript[0]).FirstOrDefault((publisherScript x) => (bool)x && !x.isPlayer && string.Equals(x.name_EN, name, StringComparison.OrdinalIgnoreCase));
		return p ? p.myID : (-1);
	}

	// The lines are set again on next use (studios were added, so lines for their landmarks can be too).
	public static void RefreshLines()
	{
		fixedArray = null;
	}

	// Every landmark's line gets its genre and topics, and missing ones are added (the arrays are saved with the game,
	// so this runs again whenever they've been replaced: a load, a new game).
	public static void FixLines()
	{
		textScript ts = G.TS;
		if (!On || !ts || ts.npcIPs == null || ts.npcIPs == fixedArray)
		{
			return;
		}
		try
		{
			List<string> lines = ts.npcIPs.ToList();
			List<bool> used = (ts.npcIPsInUse ?? new bool[0]).ToList();
			bool sized = used.Count > 0;
			int added = 0;
			landmarkAt.Clear();
			foreach (LandmarkDef d in All)
			{
				int i = LineOf(lines.ToArray(), d);
				if (i < 0 && d.Add)
				{
					int studio = StudioByName(d.Studio);
					if (studio < 0)
					{
						continue;
					}
					lines.Add(d.Title + " <P" + studio + "><G" + d.Genre + "><T" + d.Topics[0] + "><%" + d.Review + "><Y" + d.Year + "><TG4><NOSPIN>");
					if (sized)
					{
						// (A save already past its years: studios never see it, ClosedToStudios.)
						used.Add(item: false);
					}
					i = lines.Count - 1;
					added++;
				}
				if (i >= 0)
				{
					lines[i] = FixLine(lines[i], d);
					landmarkAt[i] = d;
				}
			}
			added += FixEntryLines(lines, used, sized);
			ts.npcIPs = lines.ToArray();
			if (sized)
			{
				ts.npcIPsInUse = used.ToArray();
			}
			fixedArray = ts.npcIPs;
			Plugin.Log.LogInfo("Landmarks: franchise lines set" + ((added > 0) ? (", " + added + " added") : ""));
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Landmark franchise lines: " + ex);
		}
	}

	// The game sizes the "used" list when an NPC studio first picks a franchise.
	internal static bool[] InUse()
	{
		textScript ts = G.TS;
		if (!ts || ts.npcIPs == null)
		{
			return null;
		}
		if (ts.npcIPsInUse == null || ts.npcIPsInUse.Length < ts.npcIPs.Length)
		{
			bool[] grown = new bool[ts.npcIPs.Length];
			if (ts.npcIPsInUse != null)
			{
				Array.Copy(ts.npcIPsInUse, grown, ts.npcIPsInUse.Length);
			}
			ts.npcIPsInUse = grown;
		}
		return ts.npcIPsInUse;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(textScript), "GetRandomNpcIP")]
	private static void BeforeNpcIP()
	{
		FixLines();
	}

	// An NPC studio starts one of the landmarks: it's theirs to make now.
	[HarmonyPostfix]
	[HarmonyPatch(typeof(textScript), "GetRandomNpcIP")]
	private static void NpcPicked(int publisher_, gameScript game_, string __result)
	{
		try
		{
			if (!On || string.IsNullOrEmpty(__result) || !game_)
			{
				return;
			}
			LandmarkDef d = All.FirstOrDefault((LandmarkDef x) => string.Equals(x.Title, __result.Trim(), StringComparison.OrdinalIgnoreCase));
			// Only in its years: another line of the same name long before (Infocom's Journey of 1989) isn't the landmark,
			// and after them its line is hidden (ClosedToStudios).
			if (d != null && (G.MS.year < d.From - 1 || G.MS.year > d.Year + StudioYears))
			{
				return;
			}
			if (d != null && !S.Made.ContainsKey(d.Id))
			{
				S.Made[d.Id] = new LandmarkRecord
				{
					Id = d.Id,
					GameId = game_.myID,
					StudioId = publisher_
				};
			}
			else if (d == null)
			{
				// One of a series' later games.
				EntryMade(__result.Trim(), game_, publisher_);
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Landmark picked by a studio: " + ex.Message);
		}
	}

	// ---- Who has which ----

	// Landmarks an NPC studio already made or started before this version (or before the record existed).
	public static void Sync()
	{
		if (!On || !G.MS)
		{
			return;
		}
		FixLines();
		bool[] used = InUse();
		string[] lines = G.TS.npcIPs;
		List<gameScript> games = null;
		foreach (LandmarkDef d in All)
		{
			if (S.Made.TryGetValue(d.Id, out var r))
			{
				gameScript g = GameUpdates.FindGame(r.GameId);
				if (!r.Released && (bool)g && !g.inDevelopment && (g.isOnMarket || g.weeksOnMarket > 0))
				{
					r.Released = true;
					r.Year = g.date_year;
				}
				// Saves from before the studio rule: a studio's that came out late is missed, and so is one added to the
				// list already past its years (0.33.0 marked those lines used, so they read as made with no game).
				if (!r.Player && !r.Missed && ((r.Released && r.Year > d.Year + StudioYears) || (r.GameId < 0 && d.Add)))
				{
					r.Missed = true;
					cacheKey = int.MinValue;
				}
				continue;
			}
			int i = LineOf(lines, d);
			if (i < 0 || used == null || !used[i])
			{
				continue;
			}
			games = games ?? IndustryCore.AllGames();
			// The line's own studio's game, not an older one of the same name (Holy has Infocom's Journey of 1989 too).
			Match lm = StudioTagRx.Match(lines[i]);
			int lineStudio = (lm.Success ? int.Parse(lm.Groups[1].Value) : (-1));
			gameScript theirs = (from x in games
				where !x.IsMyGame() && string.Equals(x.GetNameSimple(), d.Title, StringComparison.OrdinalIgnoreCase) && (x.developerID == lineStudio || x.inDevelopment || x.date_year >= d.From - 1)
				orderby x.developerID == lineStudio descending
				select x).FirstOrDefault();
			S.Made[d.Id] = new LandmarkRecord
			{
				Id = d.Id,
				GameId = (theirs ? theirs.myID : (-1)),
				StudioId = (theirs ? theirs.developerID : (-1)),
				Released = (!theirs || !theirs.inDevelopment),
				Year = (theirs ? theirs.date_year : d.Year),
				Missed = (theirs ? (!theirs.inDevelopment && theirs.date_year > d.Year + StudioYears) : d.Add)
			};
		}
		TidyNames(games ?? IndustryCore.AllGames());
	}

	// The game reads a franchise line's name by taking off its tags and then its last character, the space before the
	// tags. 0.33.0's series lines also ended in a space, so studios' games made from them kept one ("Battlefield 2 ").
	// Fixed in the lines (FixEntryLines); this tidies the games already named that way.
	private static void TidyNames(List<gameScript> games)
	{
		foreach (gameScript g in games)
		{
			string name = (g ? g.GetNameSimple() : null);
			if (!string.IsNullOrEmpty(name) && name != name.TrimEnd())
			{
				g.SetMyName(name.TrimEnd());
			}
		}
	}

	public static LandmarkRecord Record(string id)
	{
		if (!S.Made.TryGetValue(id, out var r))
		{
			return null;
		}
		return r;
	}

	// The landmark this game is (yours or a studio's).
	public static LandmarkDef Of(gameScript g)
	{
		if (!g || S.Made.Count == 0)
		{
			return null;
		}
		foreach (LandmarkRecord r in S.Made.Values)
		{
			if (r.GameId == g.myID)
			{
				return Def(r.Id);
			}
		}
		return null;
	}

	// A studio that's making it right now (it would shelve it if you got there first).
	private static gameScript Theirs(LandmarkDef d)
	{
		LandmarkRecord r = Record(d.Id);
		if (r == null || r.Player || r.Released)
		{
			return null;
		}
		gameScript g = GameUpdates.FindGame(r.GameId);
		if ((bool)g && g.inDevelopment && !g.isOnMarket)
		{
			return g;
		}
		return null;
	}

	public static bool Open(LandmarkDef d)
	{
		LandmarkRecord r = Record(d.Id);
		if (r == null)
		{
			return true;
		}
		return (bool)Theirs(d);
	}

	// ---- Your game: does it qualify? ----

	private static bool NewGameOfYours(gameScript g)
	{
		if ((bool)g && (bool)G.MS && g.developerID == G.MS.myID && !g.typ_contractGame && !g.pubAngebot && !g.auftragsspiel && !g.typ_nachfolger && !g.typ_remaster && !g.typ_spinoff && !g.typ_addon && !g.typ_addonStandalone && !g.typ_mmoaddon && !g.typ_budget && !g.typ_goty && !g.typ_bundle && !g.typ_bundleAddon && g.portID < 0)
		{
			if (g.mainIP != g.myID)
			{
				return g.mainIP < 0;
			}
			return true;
		}
		return false;
	}

	// The landmark a game being designed (its genre and topics, now) could become, or null: the same checks as the
	// pop-up, short of the review.
	public static LandmarkDef ForDesign(int genre, IEnumerable<int> topics)
	{
		if (!On || !G.MS || genre < 0)
		{
			return null;
		}
		HashSet<int> set = new HashSet<int>(topics.Where((int t) => t >= 0));
		int year = G.MS.year;
		return (from d in All
			where d.Genre == genre && d.Topics.All(set.Contains) && year >= d.From && year <= d.Year + Window && Open(d)
			orderby d.Extra.Count(set.Contains) descending, Math.Abs(d.Year - year)
			select d).FirstOrDefault();
	}

	public static bool Fits(gameScript g, LandmarkDef d)
	{
		if (g.maingenre != d.Genre || (d.Mmo && g.gameTyp != 1) || (d.Mobile && !g.handy))
		{
			return false;
		}
		return d.Topics.All((int t) => Topics.ExtraTopics.Has(g, t));
	}

	private static LandmarkOffer Check(gameScript g)
	{
		if (!On || !NewGameOfYours(g) || Of(g) != null || S.Declined.Contains(g.myID))
		{
			return null;
		}
		Sync();
		List<LandmarkDef> fits = All.Where((LandmarkDef d) => G.MS.year >= d.From && G.MS.year <= d.Year + Window && Fits(g, d) && Open(d)).ToList();
		if (fits.Count == 0)
		{
			return null;
		}
		// The game's own projected review (what its completion window shows as a range).
		int review = g.reviewTotal;
		if (review <= 0)
		{
			g.CalcReview(entwicklungsbericht: true);
			review = g.reviewTotal;
			g.ClearReview();
		}
		LandmarkDef best = (from d in fits
			where review >= d.Bar
			orderby d.Extra.Count((int t) => Topics.ExtraTopics.Has(g, t)) descending, Math.Abs(d.Year - G.MS.year)
			select d).FirstOrDefault();
		if (best == null)
		{
			return null;
		}
		gameScript theirs = Theirs(best);
		int myGenre = G.MS.GetFanGenreID();
		int sameGenre = S.Made.Values.Count((LandmarkRecord r) => r.Player && Def(r.Id)?.Genre == best.Genre);
		return new LandmarkOffer
		{
			Game = g,
			Def = best,
			Review = review,
			OfferGenre = (best.Genre != myGenre && sameGenre >= 1),
			Rival = (theirs ? IndustryCore.Studio(theirs.developerID) : null)
		};
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_DevGame_Complete), "Init")]
	private static void Completed(Menu_DevGame_Complete __instance, gameScript s1_)
	{
		try
		{
			LandmarkOffer o = Check(s1_) ?? EntryCheck(s1_);
			if (o != null)
			{
				o.Menu = __instance;
				Offer = o;
				EditorWindow.Landmark.Open();
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Landmark check failed: " + ex);
		}
	}

	public static void Decline()
	{
		if (Offer != null && (bool)Offer.Game && !S.Declined.Contains(Offer.Game.myID))
		{
			S.Declined.Add(Offer.Game.myID);
		}
		Offer = null;
	}

	// Your game becomes the landmark: its name, its pictures, and the franchise is yours (its studio never makes it).
	public static string Claim(bool specialGenre)
	{
		LandmarkOffer o = Offer;
		Offer = null;
		if (o == null || !o.Game || !Open(o.Def))
		{
			return "Too late: someone else got there first.";
		}
		gameScript g = o.Game;
		LandmarkDef d = o.Def;
		string shelved = "";
		gameScript theirs = Theirs(d);
		if ((bool)theirs)
		{
			publisherScript p = IndustryCore.Studio(theirs.developerID);
			shelved = " " + (p ? p.GetName() : "Its studio") + " shelves its own " + d.Name + ".";
			theirs.gameObject.tag = "GameRemoved";
			UnityEngine.Object.Destroy(theirs.gameObject);
			G.MS.games_.FindGames();
			if ((bool)p)
			{
				p.newGameInWeeks = Mathf.Max(p.newGameInWeeks, 8);
			}
		}
		FixLines();
		bool[] used = InUse();
		int i = LineOf(G.TS.npcIPs, d);
		if (used != null && i >= 0)
		{
			used[i] = true;
		}
		g.SetMyName(d.Title);
		S.Made[d.Id] = new LandmarkRecord
		{
			Id = d.Id,
			GameId = g.myID,
			StudioId = G.MS.myID,
			Player = true
		};
		if (specialGenre)
		{
			G.MS.SetFanGenreID(d.Genre);
		}
		try
		{
			if ((bool)o.Menu && o.Menu.gameObject.activeSelf)
			{
				o.Menu.uiObjects[0].GetComponent<UnityEngine.UI.Text>().text = g.GetNameWithTag();
				o.Menu.uiObjects[6].GetComponent<UnityEngine.UI.Image>().sprite = g.GetScreenshot();
				LandmarkFrame.Apply(o.Menu.uiObjects[6].transform, IsLandmark(g));
			}
		}
		catch
		{
		}
		string text = "<b>" + d.Title + "</b> is yours to make: you got there before " + d.Studio + "." + shelved;
		IndustryCore.Feed(text.Replace("<b>", "").Replace("</b>", ""));
		return text + (specialGenre ? (" " + IndustryCore.GenrePlural(d.Genre) + " are your special genre now.") : "") + " It's a landmark when it comes out.";
	}

	// ---- Release ----

	[HarmonyPostfix]
	[HarmonyPatch(typeof(gameScript), "SetOnMarket")]
	private static void Released(gameScript __instance)
	{
		try
		{
			if (!On || !__instance || S.Made.Count == 0)
			{
				return;
			}
			// Your MMO landmark's expansion comes out as the series' next one.
			if (__instance.typ_mmoaddon && __instance.developerID == G.MS.myID)
			{
				NameExpansion(__instance, G.MS.myID);
			}
			AnniversaryRemaster(__instance);
			if (EntryReleased(__instance))
			{
				return;
			}
			LandmarkRecord r = S.Made.Values.FirstOrDefault((LandmarkRecord x) => x.GameId == __instance.myID && !x.Released);
			LandmarkDef d = ((r != null) ? Def(r.Id) : null);
			if (d == null)
			{
				return;
			}
			r.Released = true;
			r.Year = G.MS.year;
			// A studio's that comes out too late is just a game: no trend, stars or copycats, and the landmark is missed.
			if (!r.Player && r.Year > d.Year + StudioYears)
			{
				r.Missed = true;
				cacheKey = int.MinValue;
				publisherScript late = IndustryCore.Studio(__instance.developerID);
				IndustryCore.Feed((late ? late.GetName() : d.Studio) + " releases " + d.Title + " in " + r.Year + ", too late to be the landmark it was in " + d.Year + ".");
				return;
			}
			genres gen = IndustryCore.Genres;
			if ((bool)gen && d.Genre < gen.genres_BELIEBTHEIT.Length)
			{
				gen.genres_BELIEBTHEIT[d.Genre] = Mathf.Min(100f, gen.genres_BELIEBTHEIT[d.Genre] + (r.Player ? 10f : 5f));
			}
			IndustryCore.MakeTrend(d.Genre);
			if (r.Player)
			{
				gameScript ip = GameUpdates.FindGame((__instance.mainIP >= 0) ? __instance.mainIP : __instance.myID) ?? __instance;
				ip.ipPunkte = Mathf.Min(1000f, ip.ipPunkte + 150f);
				int fans = (int)Mathf.Clamp((gen ? ((float)gen.GetAmountFans()) : 0f) * 0.05f, 2000f, 1000000f);
				G.MS.AddFans(fans, d.Genre);
				__instance.AddHype(25f);
				if (!S.Celebrate.Contains(d.Id))
				{
					S.Celebrate.Add(d.Id);
				}
				string text = "<b>" + d.Title + "</b> is a landmark: " + IndustryCore.GenreGames(d.Genre) + " will never be the same.";
				IndustryCore.News(text);
				IndustryCore.Feed(text.Replace("<b>", "").Replace("</b>", ""));
				PlatformBonus(__instance, d.Title);
			}
			else
			{
				publisherScript p = IndustryCore.Studio(__instance.developerID);
				if ((bool)p)
				{
					IndustryCore.AddStars(p, 4f);
					p.fanGenre = d.Genre;
				}
				string text2 = "<b>" + (p ? p.GetName() : d.Studio) + "</b> releases <b>" + d.Title + "</b>, a landmark: " + IndustryCore.GenreGames(d.Genre) + " will never be the same.";
				IndustryCore.News(text2);
				IndustryCore.Feed(text2.Replace("<b>", "").Replace("</b>", ""));
			}
			StartCopycats(__instance, d.Title);
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Landmark release failed: " + ex);
		}
	}

	// Your landmark's pop-up, once nothing else is on screen.
	public static void Tick()
	{
		if (On && (bool)G.MS && IndustryCore.Now != seriesChecked)
		{
			seriesChecked = IndustryCore.Now;
			CheckSeriesDone();
		}
		if (!On || S.Celebrate.Count == 0 || !G.Gui || G.Gui.menuOpen || EditorWindow.Landmark.IsOpen || EditorWindow.LandmarkMade.IsOpen || EditorWindow.IndustryEvent.IsOpen || EditorWindow.ClashResult.IsOpen)
		{
			return;
		}
		Showing = S.Celebrate[0];
		S.Celebrate.RemoveAt(0);
		EditorWindow.LandmarkMade.Open();
		Awards.AwardsShow.Sound(50);
	}

	public static void DoneShowing()
	{
		Showing = null;
	}

	// ---- Pictures ----

	public static Sprite Picture(LandmarkDef d, int seed)
	{
		if (d == null)
		{
			return null;
		}
		List<string> files = Pictures(d);
		if (files.Count == 0)
		{
			return null;
		}
		return LoadPicture(files[(seed & 0x7FFFFFFF) % files.Count]);
	}

	// A picture file as a 4:3 sprite, loaded once.
	private static Sprite LoadPicture(string file)
	{
		if (sprites.TryGetValue(file, out var s) && (bool)s)
		{
			return s;
		}
		try
		{
			Texture2D src = new Texture2D(2, 2);
			if (!src.LoadImage(File.ReadAllBytes(file)))
			{
				return null;
			}
			Texture2D fit = Fit(src);
			s = Sprite.Create(fit, new Rect(0f, 0f, fit.width, fit.height), new Vector2(0.5f, 0.5f));
			sprites[file] = s;
			return s;
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Landmark picture " + file + ": " + ex.Message);
			return null;
		}
	}

	// The middle 4:3 of the picture, at 640x480 (the game's screenshots are 4:3).
	private static Texture2D Fit(Texture2D src)
	{
		float w = src.width;
		float h = src.height;
		float cw = w;
		float ch = h;
		if (w / h > 4f / 3f)
		{
			cw = h * 4f / 3f;
		}
		else
		{
			ch = w * 3f / 4f;
		}
		RenderTexture rt = RenderTexture.GetTemporary(640, 480, 0);
		Graphics.Blit(src, rt, new Vector2(cw / w, ch / h), new Vector2((w - cw) / 2f / w, (h - ch) / 2f / h));
		RenderTexture prev = RenderTexture.active;
		RenderTexture.active = rt;
		Texture2D dst = new Texture2D(640, 480, TextureFormat.RGB24, mipChain: true);
		dst.ReadPixels(new Rect(0f, 0f, 640f, 480f), 0, 0);
		dst.Apply(updateMipmaps: true, makeNoLongerReadable: false);
		dst.filterMode = FilterMode.Trilinear;
		RenderTexture.active = prev;
		RenderTexture.ReleaseTemporary(rt);
		UnityEngine.Object.Destroy(src);
		return dst;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(gameScript), "GetScreenshot")]
	private static void Screenshot(gameScript __instance, ref Sprite __result)
	{
		try
		{
			if (S.Made.Count == 0)
			{
				return;
			}
			Sprite s = PictureFor(__instance);
			if ((bool)s)
			{
				__result = s;
			}
		}
		catch
		{
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(gameScript), "GetScreenshotTexture2D")]
	private static void ScreenshotTexture(gameScript __instance, ref Texture2D __result)
	{
		try
		{
			if (S.Made.Count == 0)
			{
				return;
			}
			Sprite s = PictureFor(__instance);
			if ((bool)s)
			{
				__result = s.texture;
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
			Plugin.Log.LogError("Saving landmarks failed: " + ex);
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(savegameScript), "Load")]
	private static void BeforeLoad()
	{
		S = new LandmarkState();
		Offer = null;
		Showing = null;
		fixedArray = null;
		defs = null;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(savegameScript), "LoadHistory")]
	private static void AfterLoad(ES3File ___es3file)
	{
		try
		{
			S = ((___es3file != null && ___es3file.KeyExists(SaveKey)) ? JsonConvert.DeserializeObject<LandmarkState>(___es3file.Load<string>(SaveKey)) : null) ?? new LandmarkState();
			S.Made = S.Made ?? new Dictionary<string, LandmarkRecord>();
			S.Declined = S.Declined ?? new List<int>();
			S.Celebrate = S.Celebrate ?? new List<string>();
			S.Entries = S.Entries ?? new Dictionary<string, LandmarkRecord>();
			S.Remasters = S.Remasters ?? new Dictionary<int, int>();
			S.HallOfFame = S.HallOfFame ?? new List<string>();
			S.Complete = S.Complete ?? new Dictionary<string, int>();
			S.CopyOf = S.CopyOf ?? "";
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Loading landmarks failed: " + ex);
			S = new LandmarkState();
		}
		fixedArray = null;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(mainScript), "InitNewGame")]
	private static void NewGame()
	{
		S = new LandmarkState();
		Offer = null;
		Showing = null;
		fixedArray = null;
		defs = null;
	}
}
