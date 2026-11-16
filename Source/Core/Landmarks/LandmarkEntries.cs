using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HolyAddon.Core.Industry;
using HolyAddon.Core.Updates;
using HolyAddon.UI;
using Newtonsoft.Json;
using UnityEngine;

namespace HolyAddon.Core.Landmarks;

// One of a landmark series' later games (Super Mario Bros. 3, Ocarina of Time, Final Fantasy VII), in landmarks.json
// under its landmark.
internal class LandmarkEntry
{
	public string Name = "";

	public int Year;

	public int Review;

	// A landmark in its own right (Super Mario 64, Final Fantasy VII): the full landmark rewards and the trend.
	public bool Milestone;

	// The genre it became (-1: the series' own).
	public int Genre = -1;

	// Its line in Holy's franchise list, if it has one under another name (renamed to Name).
	public string Holy;

	// Its developer, when not the landmark's studio (Donkey Kong Country: Rare).
	public string Studio;

	[JsonIgnore]
	public string Id = "";

	[JsonIgnore]
	public LandmarkDef Of;

	// The projected review it needs: its real score less 10, at least 70 (as a landmark).
	public int Bar => Mathf.Max(70, Review - 10);

	// From 2 years before its real year until 5 after.
	public int From => Year - 2;

	public string Key => ((Of != null) ? Of.Id : "?") + "/" + Id;
}

// Series that follow history (Docs/Landmarks-Release2-Plan.md, part B):
// - Every landmark's later games are franchise lines for its studio, linked to the landmark (Series), so a studio
//   that owns the series makes them on time as its sequels; Holy's own lines for them are renamed to the real titles.
// - A series you own: when a sequel of yours is finished in an entry's years with a good enough projected review, the
//   landmark pop-up offers to make it that entry (renamed, its pictures, rewards). Milestones get the full landmark
//   rewards; other entries smaller ones.
// - MMO landmarks' entries are their expansions: they name the series' MMO add-ons instead.
internal static partial class Landmarks
{
	// Line index in npcIPs > entry, and > the landmark line it follows (Series links them).
	private static readonly Dictionary<int, LandmarkEntry> entryAt = new Dictionary<int, LandmarkEntry>();

	public static readonly Dictionary<int, int> EntryBases = new Dictionary<int, int>();

	private static readonly Regex StudioTagRx = new Regex("<P(\\d+)>");

	private static readonly Regex YearTagRx = new Regex("<Y\\d+>");

	private static string Slug(string s)
	{
		string t = Regex.Replace((s ?? "").ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
		if (t.Length != 0)
		{
			return t;
		}
		return "entry";
	}

	private static void PrepareEntries(LandmarkDef d, int genres)
	{
		d.Entries = (d.Entries ?? new List<LandmarkEntry>()).Where((LandmarkEntry e) => e != null && !string.IsNullOrWhiteSpace(e.Name)).OrderBy((LandmarkEntry e) => e.Year).ToList();
		HashSet<string> ids = new HashSet<string>();
		foreach (LandmarkEntry e in d.Entries)
		{
			e.Of = d;
			string id = Slug(e.Name);
			string unique = id;
			int n = 2;
			while (!ids.Add(unique))
			{
				unique = id + "-" + n++;
			}
			e.Id = unique;
			if (e.Genre >= genres)
			{
				e.Genre = -1;
			}
		}
	}

	public static LandmarkEntry Entry(string key)
	{
		if (string.IsNullOrEmpty(key))
		{
			return null;
		}
		int slash = key.IndexOf('/');
		if (slash < 0)
		{
			return null;
		}
		return Def(key.Substring(0, slash))?.Entries.FirstOrDefault((LandmarkEntry e) => e.Id == key.Substring(slash + 1));
	}

	public static LandmarkRecord EntryRecord(LandmarkEntry e)
	{
		if (e == null || !S.Entries.TryGetValue(e.Key, out var value))
		{
			return null;
		}
		return value;
	}

	// The entry this game is (yours or a studio's).
	public static LandmarkEntry EntryOf(gameScript g)
	{
		if (!g || S.Entries.Count == 0)
		{
			return null;
		}
		foreach (LandmarkRecord r in S.Entries.Values)
		{
			if (r.GameId == g.myID)
			{
				return Entry(r.Id);
			}
		}
		return null;
	}

	public static string EntryFolder(LandmarkEntry e)
	{
		return Path.Combine(PictureFolder(e.Of), e.Id);
	}

	// ---- The franchise lines ----

	private static int EntryLine(LandmarkEntry e)
	{
		foreach (KeyValuePair<int, LandmarkEntry> kv in entryAt)
		{
			if (kv.Value == e)
			{
				return kv.Key;
			}
		}
		return -1;
	}

	// Each landmark's later games as lines for its studio (Holy's own line for one renamed to its real title), linked
	// to the landmark's line. MMO landmarks' entries (expansions) get no lines.
	private static int FixEntryLines(List<string> lines, List<bool> used, bool sized)
	{
		entryAt.Clear();
		EntryBases.Clear();
		Dictionary<LandmarkDef, int> roots = new Dictionary<LandmarkDef, int>();
		HashSet<int> taken = new HashSet<int>();
		string[] snapshot = lines.ToArray();
		foreach (LandmarkDef d in All)
		{
			int root = LineOf(snapshot, d);
			if (root >= 0)
			{
				roots[d] = root;
				taken.Add(root);
			}
		}
		int added = 0;
		foreach (LandmarkDef d2 in All)
		{
			if (d2.Mmo || d2.Entries.Count == 0 || !roots.TryGetValue(d2, out var root2))
			{
				continue;
			}
			Match pm = StudioTagRx.Match(lines[root2]);
			int seriesStudio = (pm.Success ? int.Parse(pm.Groups[1].Value) : (-1));
			int second = ((d2.Topics.Length > 1) ? d2.Topics[1] : ((d2.Extra.Length != 0) ? d2.Extra[0] : (-1)));
			foreach (LandmarkEntry e in d2.Entries)
			{
				int studio = ((!string.IsNullOrEmpty(e.Studio)) ? StudioByName(e.Studio) : (-1));
				if (studio < 0)
				{
					studio = seriesStudio;
				}
				if (studio < 0)
				{
					continue;
				}
				int genre = ((e.Genre >= 0) ? e.Genre : d2.Genre);
				string flags = "<TG4><NOSPIN>" + (d2.Mmo ? "<MMO>" : "");
				int i = FindLine(lines, e.Holy ?? e.Name, e.Year, taken);
				if (i < 0 && !string.IsNullOrEmpty(e.Holy))
				{
					i = FindLine(lines, e.Name, e.Year, taken);
				}
				if (i >= 0)
				{
					// Keep the line's numbering style (Roman or Arabic numerals for later sequels).
					string old = lines[i];
					flags = (old.Contains("<ROM>") ? "<ROM>" : "") + (old.Contains("<ARA>") ? "<ARA>" : "") + flags;
				}
				string line = e.Name + " <P" + studio + "><G" + genre + "><T" + d2.Topics[0] + ">" + ((second >= 0) ? ("<ST" + second + ">") : "") + "<%" + e.Review + "><Y" + e.Year + ">" + flags;
				if (i >= 0)
				{
					lines[i] = line;
				}
				else
				{
					lines.Add(line);
					if (sized)
					{
						used.Add(item: false);
					}
					i = lines.Count - 1;
					added++;
				}
				taken.Add(i);
				entryAt[i] = e;
				EntryBases[i] = root2;
			}
		}
		return added;
	}

	// A line by its name, the one nearest the year, not already a landmark's or an entry's.
	private static int FindLine(List<string> lines, string name, int year, HashSet<int> taken)
	{
		int best = -1;
		for (int i = 0; i < lines.Count; i++)
		{
			if (!taken.Contains(i) && string.Equals(LineName(lines[i]), name, StringComparison.OrdinalIgnoreCase) && (best < 0 || Math.Abs(LineYear(lines[i]) - year) < Math.Abs(LineYear(lines[best]) - year)))
			{
				best = i;
			}
		}
		return best;
	}

	// ---- A studio made one ----

	// A studio's game is one of a series' entries (picked from the franchise list, or its sequel named after one).
	public static void EntryMade(string name, gameScript g, int studio)
	{
		if (!On || !g || string.IsNullOrEmpty(name))
		{
			return;
		}
		LandmarkEntry e = entryAt.Values.FirstOrDefault((LandmarkEntry x) => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
		if (e != null && !S.Entries.ContainsKey(e.Key))
		{
			S.Entries[e.Key] = new LandmarkRecord
			{
				Id = e.Key,
				GameId = g.myID,
				StudioId = studio
			};
		}
	}

	// ---- Your sequel: does it become the series' next game? ----

	private static LandmarkOffer EntryCheck(gameScript g)
	{
		if (!On || !g || !G.MS || g.developerID != G.MS.myID || !g.typ_nachfolger || g.typ_contractGame || g.pubAngebot || g.auftragsspiel || g.portID >= 0 || S.Declined.Contains(g.myID) || EntryOf(g) != null)
		{
			return null;
		}
		gameScript main = GameUpdates.FindGame(g.mainIP);
		LandmarkDef d = Of(main);
		if (d == null || d.Mmo || d.Entries.Count == 0 || !main || main.ownerID != G.MS.myID)
		{
			return null;
		}
		FixLines();
		bool[] used = InUse();
		int year = G.MS.year;
		LandmarkEntry next = d.Entries.Where((LandmarkEntry e) => year >= e.From && year <= e.Year + Window && EntryOpen(e, used)).OrderBy((LandmarkEntry e) => e.Year).FirstOrDefault();
		if (next == null)
		{
			return null;
		}
		int review = g.reviewTotal;
		if (review <= 0)
		{
			g.CalcReview(entwicklungsbericht: true);
			review = g.reviewTotal;
			g.ClearReview();
		}
		if (review < next.Bar)
		{
			return null;
		}
		return new LandmarkOffer
		{
			Game = g,
			Def = d,
			Entry = next,
			Review = review
		};
	}

	private static bool EntryOpen(LandmarkEntry e, bool[] used)
	{
		LandmarkRecord r = EntryRecord(e);
		int i = EntryLine(e);
		if (r != null)
		{
			if (!Scrapped(r))
			{
				return false;
			}
			S.Entries.Remove(e.Key);
			if (used != null && i >= 0 && i < used.Length)
			{
				used[i] = false;
			}
			Plugin.Log.LogInfo("Landmarks: " + e.Key + " is open again (the sequel chosen for it was scrapped)");
		}
		if (i >= 0 && used != null && i < used.Length)
		{
			return !used[i];
		}
		return true;
	}

	// Your sequel becomes the entry: its name and pictures; the series' line is used.
	public static string ClaimEntry()
	{
		LandmarkOffer o = Offer;
		Offer = null;
		if (o == null || o.Entry == null || !o.Game)
		{
			return "Too late.";
		}
		LandmarkEntry e = o.Entry;
		gameScript g = o.Game;
		FixLines();
		bool[] used = InUse();
		if (!EntryOpen(e, used))
		{
			return "Too late: " + e.Name + " has been made.";
		}
		int i = EntryLine(e);
		if (used != null && i >= 0 && i < used.Length)
		{
			used[i] = true;
		}
		g.SetMyName(e.Name);
		g.myNameTeil1 = e.Name;
		S.Entries[e.Key] = new LandmarkRecord
		{
			Id = e.Key,
			GameId = g.myID,
			StudioId = G.MS.myID,
			Player = true
		};
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
		IndustryCore.Feed("You're making " + e.Name + ", the next " + e.Of.Name + " game.");
		return "Your sequel is <b>" + e.Name + "</b>, the next " + e.Of.Name + " game" + (e.Milestone ? ": a landmark in its own right when it comes out." : ".");
	}

	// ---- Release ----

	// An entry coming out (yours or a studio's). True if the game was an entry.
	private static bool EntryReleased(gameScript g)
	{
		LandmarkRecord r = S.Entries.Values.FirstOrDefault((LandmarkRecord x) => x.GameId == g.myID && !x.Released);
		LandmarkEntry e = ((r != null) ? Entry(r.Id) : null);
		if (e == null)
		{
			return false;
		}
		r.Released = true;
		r.Year = G.MS.year;
		genres gen = IndustryCore.Genres;
		int genre = g.maingenre;
		if (r.Player && r.Committed && (g.reviewTotal < e.Bar || r.Year > e.Year + Window))
		{
			// Chosen at the start: it's the series' next game whatever happens, but not received as the real one was.
			r.Short = true;
			cacheKey = int.MinValue;
			string why = ((g.reviewTotal < e.Bar) ? ("a review of " + g.reviewTotal + "%, where " + e.Bar + "% would have done it") : ("out after " + (e.Year + Window) + ", too late to matter as much"));
			string shortText = "<b>" + e.Name + "</b>, the next " + e.Of.Name + " game, is out, but it isn't what fans hoped for: " + why + ".";
			IndustryCore.News(shortText);
			IndustryCore.Feed(shortText.Replace("<b>", "").Replace("</b>", ""));
			return true;
		}
		if (r.Player)
		{
			gameScript ip = GameUpdates.FindGame((g.mainIP >= 0) ? g.mainIP : g.myID) ?? g;
			if (e.Milestone)
			{
				if ((bool)gen && genre >= 0 && genre < gen.genres_BELIEBTHEIT.Length)
				{
					gen.genres_BELIEBTHEIT[genre] = Mathf.Min(100f, gen.genres_BELIEBTHEIT[genre] + 10f);
				}
				IndustryCore.MakeTrend(genre);
				ip.ipPunkte = Mathf.Min(1000f, ip.ipPunkte + 150f);
				G.MS.AddFans((int)Mathf.Clamp((gen ? ((float)gen.GetAmountFans()) : 0f) * 0.05f, 2000f, 1000000f), genre);
				g.AddHype(25f);
				if (!S.Celebrate.Contains(e.Key))
				{
					S.Celebrate.Add(e.Key);
				}
			}
			else
			{
				ip.ipPunkte = Mathf.Min(1000f, ip.ipPunkte + 75f);
				G.MS.AddFans((int)Mathf.Clamp((gen ? ((float)gen.GetAmountFans()) : 0f) * 0.02f, 1000f, 300000f), genre);
				g.AddHype(15f);
			}
			string text = "<b>" + e.Name + "</b>, the next " + e.Of.Name + " game, is out" + (e.Milestone ? (": " + IndustryCore.GenreGames(genre) + " will never be the same.") : ".");
			IndustryCore.News(text);
			IndustryCore.Feed(text.Replace("<b>", "").Replace("</b>", ""));
			if (e.Milestone)
			{
				PlatformBonus(g, e.Name);
			}
		}
		else
		{
			publisherScript p = IndustryCore.Studio(g.developerID);
			if (e.Milestone)
			{
				if ((bool)gen && genre >= 0 && genre < gen.genres_BELIEBTHEIT.Length)
				{
					gen.genres_BELIEBTHEIT[genre] = Mathf.Min(100f, gen.genres_BELIEBTHEIT[genre] + 5f);
				}
				IndustryCore.MakeTrend(genre);
				if ((bool)p)
				{
					IndustryCore.AddStars(p, 2f);
				}
				string text2 = "<b>" + (p ? p.GetName() : "A studio") + "</b> releases <b>" + e.Name + "</b>, a landmark: " + IndustryCore.GenreGames(genre) + " will never be the same.";
				IndustryCore.News(text2);
				IndustryCore.Feed(text2.Replace("<b>", "").Replace("</b>", ""));
			}
			else
			{
				IndustryCore.Feed((p ? p.GetName() : "A studio") + " releases " + e.Name + ", the next " + e.Of.Name + " game.");
			}
		}
		if (e.Milestone)
		{
			StartCopycats(g, e.Name);
		}
		return true;
	}

	// ---- MMO expansions ----

	// An MMO landmark's add-on takes the name of the series' next expansion (yours when it comes out, a studio's when
	// it's made).
	public static void NameExpansion(gameScript addon, int studio)
	{
		if (!On || !addon || !addon.typ_mmoaddon || EntryOf(addon) != null)
		{
			return;
		}
		gameScript main = GameUpdates.FindGame(addon.mainIP);
		LandmarkDef d = Of(main);
		if (d == null || !d.Mmo || d.Entries.Count == 0)
		{
			return;
		}
		int year = G.MS.year;
		LandmarkEntry next = d.Entries.Where((LandmarkEntry e) => EntryRecord(e) == null && year >= e.From && year <= e.Year + Window).OrderBy((LandmarkEntry e) => e.Year).FirstOrDefault();
		if (next == null)
		{
			return;
		}
		addon.SetMyName(next.Name);
		S.Entries[next.Key] = new LandmarkRecord
		{
			Id = next.Key,
			GameId = addon.myID,
			StudioId = studio,
			Player = (bool)G.MS && studio == G.MS.myID
		};
	}

	// ---- Pictures ----

	private static List<string> EntryPictures(LandmarkEntry e)
	{
		string key = e.Key;
		if (pictureFiles.TryGetValue(key, out var cached))
		{
			return cached;
		}
		string dir = EntryFolder(e);
		List<string> files = ((!Directory.Exists(dir)) ? new List<string>() : (from f in Directory.GetFiles(dir)
			where f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
			orderby f
			select f).ToList());
		pictureFiles[key] = files;
		return files;
	}

	public static int EntryPictureCount(LandmarkEntry e)
	{
		try
		{
			return EntryPictures(e).Count;
		}
		catch
		{
			return 0;
		}
	}

	// An entry's picture: its own, else its landmark's.
	public static Sprite EntryPicture(LandmarkEntry e, int seed)
	{
		if (e == null)
		{
			return null;
		}
		List<string> files = EntryPictures(e);
		if (files.Count > 0)
		{
			return LoadPicture(files[(seed & 0x7FFFFFFF) % files.Count]);
		}
		return Picture(e.Of, seed);
	}

	// A game's landmark picture: an entry's own, else its landmark's; a landmark's.
	private static Sprite PictureFor(gameScript g)
	{
		LandmarkEntry e = ((S.Entries.Count > 0) ? EntryOf(g) : null);
		if (e != null)
		{
			List<string> files = EntryPictures(e);
			if (files.Count > 0)
			{
				return LoadPicture(files[Mathf.Abs(g.myID) % files.Count]);
			}
			return Picture(e.Of, g.myID);
		}
		LandmarkDef own = Of(g);
		if (own != null)
		{
			return Picture(own, g.myID);
		}
		return LaterGamePicture(g);
	}
}
