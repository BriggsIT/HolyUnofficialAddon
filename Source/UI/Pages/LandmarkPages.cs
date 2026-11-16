using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HolyAddon.Core;
using HolyAddon.Core.Industry;
using HolyAddon.Core.Landmarks;
using HolyAddon.Core.Updates;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI.Pages;

internal static class LandmarkUi
{
	public static readonly Color Gold = new Color(0.85f, 0.62f, 0.12f);

	public static string Topic(int id)
	{
		if (!G.TS || id < 0)
		{
			return "";
		}
		return Regex.Replace(G.TS.GetThemes(id) ?? "", "<[^>]*>", "").Trim();
	}

	// "Platformer with Plumber", "Skill Game with Aliens (and Spaceships suit it)".
	public static string Recipe(LandmarkDef d)
	{
		string s = IndustryCore.GenreName(d.Genre) + (d.Mobile ? " (a mobile game)" : (d.Mmo ? " (an MMO)" : "")) + " with " + string.Join(" and ", d.Topics.Select(Topic));
		if (d.Extra.Length != 0)
		{
			s = s + " (" + string.Join(" and ", d.Extra.Select(Topic)) + " suit" + ((d.Extra.Length == 1) ? "s" : "") + " it)";
		}
		return s;
	}

	public static Sprite GenrePic(int genre)
	{
		genres gen = IndustryCore.Genres;
		if (!gen)
		{
			return null;
		}
		return gen.GetPic(genre);
	}

	// A 4:3 picture: the landmark's own, or its genre's.
	public static void Picture(Transform parent, LandmarkDef d, int seed, float x, float y, float w)
	{
		Image frame = MmoUi.Rect(parent, x - 3f, y - 3f, w + 6f, w * 0.75f + 6f, Gold);
		Image img = MmoUi.Rect(parent, x, y, w, w * 0.75f, Color.white);
		Sprite pic = Landmarks.Picture(d, seed);
		if ((bool)pic)
		{
			img.sprite = pic;
			return;
		}
		img.color = new Color(0.16f, 0.2f, 0.28f);
		Sprite g = GenrePic(d.Genre);
		if ((bool)g)
		{
			StoreUi.Picture(parent, g, x + w / 2f - 28f, y + w * 0.375f - 28f, 56f);
		}
		frame.color = new Color(0.5f, 0.5f, 0.5f);
	}
}

// A landmark game: the offer when one of your games is finished and fits one, and the moment yours comes out.
internal class LandmarkPage : Page
{
	private string result;

	public override string Title => "Landmark Game";

	public override Sprite IconSprite => IndustryUi.Icon;

	public override float Width => 940f;

	public override float Height
	{
		get
		{
			if (result != null)
			{
				return 300f;
			}
			Landmarks.DropStaleStart();
			if (Landmarks.Offer == null)
			{
				return (Landmarks.Showing != null && Landmarks.Showing.StartsWith(Landmarks.CompletePrefix)) ? 610f : 550f;
			}
			return 610f;
		}
	}

	// The landmark's own screenshot across the top, fading into the dark behind its title (the industry banner when it
	// has no pictures yet), a gold glow and a gold line under it.
	private static void Hero(EditorWindow w, LandmarkDef d, int seed, string kicker, string title, string sub)
	{
		Hero(w, Landmarks.Picture(d, seed), kicker, title, sub);
	}

	private static void Hero(EditorWindow w, Sprite pic, string kicker, string title, string sub)
	{
		float W = w.Width;
		RectTransform banner = (pic ? w.Banner(pic, 14f, 14f, W - 28f, 210f) : w.Banner(UpdateIcons.Banner("industry"), 14f, 14f, W - 28f, 210f, full: true));
		Image glow = MmoUi.Rect(banner, -120f, -110f, 520f, 330f, new Color(LandmarkUi.Gold.r, LandmarkUi.Gold.g, LandmarkUi.Gold.b, 0.28f));
		glow.sprite = SpriteTools.Glow();
		w.BannerText(kicker, 34f, 34f, W - 120f, 22f, 13, new Color(1f, 0.82f, 0.36f), bold: true);
		w.BannerText("<b>" + title + "</b>", 34f, 60f, W * 0.62f, 80f, 30, Color.white, bold: false, TextAnchor.UpperLeft);
		w.BannerText(sub, 34f, 176f, W * 0.62f, 26f, 15, new Color(0.86f, 0.9f, 0.96f));
		MmoUi.Rect(w.Body, 14f, 224f, W - 28f, 3f, LandmarkUi.Gold);
	}

	// One fact in a row of chips: a small caption over a bold value, with a coloured edge.
	private static void Chip(EditorWindow w, float x, float y, float cw, string caption, string value, Color edge)
	{
		RectTransform chip = w.Panel(x, y, cw, 60f);
		MmoUi.Rect(chip, 0f, 0f, 5f, 60f, edge);
		w.Label(caption, 16f, 7f, cw - 24f, 18f, TextAnchor.MiddleLeft, bold: false, chip, 12).color = UIKit.Grey;
		w.Label(value, 16f, 27f, cw - 24f, 26f, TextAnchor.MiddleLeft, bold: true, chip, 16);
	}

	private static void Chips(EditorWindow w, float y, params string[][] facts)
	{
		float W = w.Width;
		int n = facts.Length;
		float cw = (W - 28f - (float)(n - 1) * 10f) / (float)n;
		for (int i = 0; i < n; i++)
		{
			Chip(w, 14f + (float)i * (cw + 10f), y, cw, facts[i][0], facts[i][1], (facts[i].Length > 2 && facts[i][2] == "gold") ? LandmarkUi.Gold : UIKit.Green);
		}
	}

	public override void Build(EditorWindow w)
	{
		float W = w.Width;
		Landmarks.DropStaleStart();
		LandmarkOffer o = Landmarks.Offer;
		LandmarkDef shown =((Landmarks.Showing != null) ? Landmarks.Def(Landmarks.Showing) : null);
		LandmarkEntry shownEntry = ((Landmarks.Showing != null) ? Landmarks.Entry(Landmarks.Showing) : null);
		LandmarkDef completed = ((Landmarks.Showing != null && Landmarks.Showing.StartsWith(Landmarks.CompletePrefix)) ? Landmarks.Def(Landmarks.Showing.Substring(Landmarks.CompletePrefix.Length)) : null);
		if (result != null || (o == null && shown == null && shownEntry == null && completed == null))
		{
			w.Label(result ?? "That's been dealt with.", 24f, 30f, W - 48f, 110f, TextAnchor.UpperLeft, bold: false, null, 15);
			w.BigButton("Close", 14f, 150f, 220f, 44f, w.Close);
			w.SmallButton("Open Landmarks...", 248f, 157f, 220f, OpenList, enabled: true, null, null, 30f);
			return;
		}
		if (o != null && o.AtStart)
		{
			StartOffer(w, o);
		}
		else if (o != null && o.Entry != null)
		{
			EntryOffer(w, o);
		}
		else if (o != null)
		{
			Offer(w, o);
		}
		else if (completed != null)
		{
			SeriesDone(w, completed);
		}
		else if (shownEntry != null)
		{
			EntryMade(w, shownEntry);
		}
		else
		{
			Made(w, shown);
		}
	}

	private static string Cap(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return s;
		}
		return char.ToUpper(s[0]) + s.Substring(1);
	}

	private static void OpenList()
	{
		EditorWindow.Landmark.Close();
		EditorWindow.LandmarkMade.Close();
		IndustryPage.StartTab = 3;
		EditorWindow.Industry.Open();
	}

	private void Offer(EditorWindow w, LandmarkOffer o)
	{
		float W = w.Width;
		LandmarkDef d = o.Def;
		Hero(w, d, o.Game.myID, "A LANDMARK GAME WITHIN REACH", d.Title, d.Year + "  ·  originally by " + d.Studio);
		float y = 240f;
		Chips(w, y, new string[2]
		{
			"Genre",
			IndustryCore.GenreName(d.Genre)
		}, new string[2]
		{
			(d.Topics.Length == 1) ? "Topic" : "Topics",
			string.Join(", ", d.Topics.Select(LandmarkUi.Topic))
		}, new string[2]
		{
			"Projected review",
			o.Review + "%  (needs " + d.Bar + "%)"
		});
		y += 76f;
		string rival = (o.Rival ? (" <color=#c62828>" + o.Rival.GetName() + " is making it right now: claim it and they shelve theirs.</color>") : "");
		bool same = SameName(o.Game, d.Title);
		w.Label((same ? ("Your game can become the real <b>" + d.Title + "</b>.") : ("<b>" + o.Game.GetNameSimple() + "</b> can become <b>" + d.Title + "</b>.")) + rival, 24f, y, W - 48f, 46f, TextAnchor.UpperLeft, bold: false, null, 16);
		y += 50f;
		w.Label((same ? "It keeps its name and shows " : "It's renamed and shows ") + d.Name + "'s pictures. When it comes out: IP points, fans of " + IndustryCore.GenreGames(d.Genre) + ", hype, and " + IndustryCore.GenrePlural(d.Genre) + " become the trend. " + d.Studio + " will never make it.", 24f, y, W - 48f, 44f, TextAnchor.UpperLeft, bold: false, null, 13).color = UIKit.Grey;
		y += 56f;
		w.BigButton("Make It " + d.Name, 14f, y, 300f, 46f, delegate
		{
			result = Landmarks.Claim(specialGenre: false);
			w.Rebuild();
		});
		float x = 324f;
		if (o.OfferGenre)
		{
			string genre = IndustryCore.GenrePlural(d.Genre);
			w.BigButton("Make It " + d.Name + " and Make " + genre + " My Special Genre", x, y, 340f, 46f, delegate
			{
				result = Landmarks.Claim(specialGenre: true);
				w.Rebuild();
			}, enabled: true, "You're known for " + IndustryCore.GenreGames(d.Genre) + " now. Your special genre is " + IndustryCore.GenreName(G.MS.GetFanGenreID()) + ": games in it review a little better.");
			x += 350f;
		}
		w.BigButton("Keep It As It Is", x, y, 220f, 46f, delegate
		{
			Landmarks.Decline();
			result = "It stays " + o.Game.GetNameSimple() + ".";
			w.Rebuild();
		});
		w.Label("Closing this window asks again when it's finished next time.", 14f, y + 54f, W - 28f, 22f, TextAnchor.MiddleLeft, bold: false, null, 12).color = UIKit.Grey;
	}

	// The game already has the landmark's name (you named it that yourself).
	private static bool SameName(gameScript g, string name)
	{
		return (bool)g && string.Equals((g.GetNameSimple() ?? "").Trim(), (name ?? "").Trim(), System.StringComparison.OrdinalIgnoreCase);
	}

	// Your sequel of a landmark series can become the series' next real game.
	private void EntryOffer(EditorWindow w, LandmarkOffer o)
	{
		float W = w.Width;
		LandmarkEntry e = o.Entry;
		LandmarkDef d = o.Def;
		Hero(w, Landmarks.EntryPicture(e, o.Game.myID), "THE NEXT " + d.Name.ToUpperInvariant() + " GAME", e.Name, e.Year + "  ·  in the " + d.Name + " series");
		float y = 240f;
		Chips(w, y, new string[2] { "Series", d.Name }, new string[3]
		{
			"Its kind",
			e.Milestone ? "A landmark in its own right" : "The next in the series",
			e.Milestone ? "gold" : "green"
		}, new string[2]
		{
			"Projected review",
			o.Review + "%  (needs " + e.Bar + "%)"
		});
		y += 76f;
		bool same = SameName(o.Game, e.Name);
		w.Label(same ? ("Your game can become the real <b>" + e.Name + "</b>.") : ("<b>" + o.Game.GetNameSimple() + "</b> can become <b>" + e.Name + "</b>."), 24f, y, W - 48f, 46f, TextAnchor.UpperLeft, bold: false, null, 16);
		y += 50f;
		w.Label((same ? "It keeps its name and shows its pictures." : "It's renamed and shows its pictures.") + (e.Milestone ? " When it comes out: IP points, fans, hype, and its genre becomes the trend, as for a landmark." : " When it comes out: IP points +75, fans of its genre and hype +15."), 24f, y, W - 48f, 44f, TextAnchor.UpperLeft, bold: false, null, 13).color = UIKit.Grey;
		y += 56f;
		w.BigButton("Make It " + e.Name, 14f, y, 420f, 46f, delegate
		{
			result = Landmarks.ClaimEntry();
			w.Rebuild();
		});
		w.BigButton("Keep It As It Is", 444f, y, 220f, 46f, delegate
		{
			Landmarks.Decline();
			result = "It stays " + o.Game.GetNameSimple() + ".";
			w.Rebuild();
		});
		w.Label("Closing this window asks again when it's finished next time.", 14f, y + 54f, W - 28f, 22f, TextAnchor.MiddleLeft, bold: false, null, 12).color = UIKit.Grey;
	}

	// Starting a sequel of your landmark series: it can be the series' next real game from the start.
	private void StartOffer(EditorWindow w, LandmarkOffer o)
	{
		float W = w.Width;
		LandmarkEntry e = o.Entry;
		LandmarkDef d = o.Def;
		int by = e.Year + Landmarks.Window;
		Hero(w, Landmarks.EntryPicture(e, o.Original ? o.Original.myID : 0), "THE NEXT " + d.Name.ToUpperInvariant() + " GAME", e.Name, e.Year + "  ·  in the " + d.Name + " series");
		float y = 240f;
		Chips(w, y, new string[2] { "Series", d.Name }, new string[3]
		{
			"Its kind",
			e.Milestone ? "A landmark in its own right" : "The next in the series",
			e.Milestone ? "gold" : "green"
		}, new string[2]
		{
			"Review needed",
			e.Bar + "% or more"
		}, new string[2]
		{
			"Out by",
			by.ToString()
		});
		y += 76f;
		w.Label("Your sequel can be the real <b>" + e.Name + "</b>, from the start.", 24f, y, W - 48f, 30f, TextAnchor.UpperLeft, bold: false, null, 16);
		y += 34f;
		string rewards = (e.Milestone ? "IP points, fans, hype, and its genre becomes the trend, as for a landmark" : "IP points +75, fans of its genre and hype +15");
		bool otherGenre = e.Genre >= 0 && (bool)o.DevMenu && e.Genre != o.DevMenu.g_GameMainGenre;
		w.Label("It's named " + e.Name + " and shows its pictures while you make it. Received as it was (a review of " + e.Bar + "% or more, out by " + by + "): " + rewards + ". Otherwise it's still " + e.Name + ", without those, and the series goes on." + (otherGenre ? (" The real one was in the " + IndustryCore.GenreName(e.Genre) + " genre.") : ""), 24f, y, W - 48f, 60f, TextAnchor.UpperLeft, bold: false, null, 13).color = UIKit.Grey;
		y += 72f;
		w.BigButton("Make It " + e.Name, 14f, y, 420f, 46f, delegate
		{
			result = Landmarks.CommitAtStart();
			w.Rebuild();
		});
		w.BigButton("Just a Sequel", 444f, y, 220f, 46f, delegate
		{
			result = Landmarks.DeclineAtStart();
			w.Rebuild();
		});
		w.Label("Closing this window leaves the choice for when it's finished.", 14f, y + 54f, W - 28f, 22f, TextAnchor.MiddleLeft, bold: false, null, 12).color = UIKit.Grey;
	}

	// Your landmark series' real games have run out: the congratulations, and what changes now (LandmarkSeriesDone).
	private void SeriesDone(EditorWindow w, LandmarkDef d)
	{
		float W = w.Width;
		Landmarks.Tally(d, out var yours, out var made, out var total);
		bool all = yours == total;
		LandmarkEntry last = d.Entries.OrderBy((LandmarkEntry e) => e.Year).LastOrDefault();
		int lastYear = ((last != null) ? last.Year : d.Year);
		Hero(w, d, d.Year, all ? "THE WHOLE SERIES, RECREATED" : "THE REAL SERIES HAS RUN ITS COURSE", d.Name, d.Year + "-" + lastYear + "  ·  " + total + " real games");
		float y = 240f;
		Chips(w, y, new string[3]
		{
			"You made",
			yours + " of " + total,
			"gold"
		}, new string[3]
		{
			"Made in all",
			made + " of " + total,
			"gold"
		}, new string[3]
		{
			"Its years",
			d.Year + "-" + lastYear,
			"gold"
		});
		y += 76f;
		string praise = (all ? ("Congratulations: you've recreated the whole " + d.Name + " series, every real game from " + d.Title + " to " + ((last != null) ? last.Name : d.Title) + ", just as history had it.") : ("Congratulations: the real " + d.Name + " series has run its course, and you made " + yours + " of its " + total + " games" + ((made > yours) ? (" (other studios made " + (made - yours) + ")") : "") + "."));
		w.Label(praise, 24f, y, W - 48f, 52f, TextAnchor.UpperLeft, bold: false, null, 16);
		y += 58f;
		w.Label("From now on " + d.Name + " is yours to take anywhere. New sequels and spin-offs are your own games: there's no real game left for them to become, sequels don't start with the series' extra hype, and they show pictures from the series' real games. Its IP keeps its higher value and its fan shop bonus.", 24f, y, W - 48f, 60f, TextAnchor.UpperLeft, bold: false, null, 13).color = UIKit.Grey;
		y += 70f;
		w.BigButton("Close", 14f, y, 220f, 44f, delegate
		{
			Landmarks.DoneShowing();
			EditorWindow.LandmarkMade.Close();
		});
		w.SmallButton("Open Landmarks...", 248f, y + 7f, 220f, delegate
		{
			Landmarks.DoneShowing();
			OpenList();
		}, enabled: true, null, null, 30f);
	}

	// Your milestone entry is out.
	private void EntryMade(EditorWindow w, LandmarkEntry e)
	{
		float W = w.Width;
		LandmarkRecord r = Landmarks.EntryRecord(e);
		gameScript g = ((r != null) ? GameUpdates.FindGame(r.GameId) : null);
		int genre = (g ? g.maingenre : e.Of.Genre);
		Hero(w, Landmarks.EntryPicture(e, g ? g.myID : 0), "YOU MADE A LANDMARK GAME", e.Name, "Out now  ·  the next " + e.Of.Name + " game");
		float y = 240f;
		Chips(w, y, new string[3] { "IP points", "+150", "gold" }, new string[3]
		{
			"New fans",
			IndustryCore.GenrePlural(genre),
			"gold"
		}, new string[3] { "Hype", "+25", "gold" }, new string[3]
		{
			"The trend",
			IndustryCore.GenrePlural(genre),
			"gold"
		});
		y += 76f;
		w.Label("Players know " + e.Name + " is something special. " + IndustryCore.GenrePlural(genre) + " are the trend, and fans of " + IndustryCore.GenreGames(genre) + " flock to your studio.", 24f, y, W - 48f, 48f, TextAnchor.UpperLeft, bold: false, null, 16);
		y += 64f;
		w.BigButton("Close", 14f, y, 220f, 44f, delegate
		{
			Landmarks.DoneShowing();
			EditorWindow.LandmarkMade.Close();
		});
		w.SmallButton("Open Landmarks...", 248f, y + 7f, 220f, delegate
		{
			Landmarks.DoneShowing();
			OpenList();
		}, enabled: true, null, null, 30f);
	}

	private void Made(EditorWindow w, LandmarkDef d)
	{
		float W = w.Width;
		LandmarkRecord r = Landmarks.Record(d.Id);
		gameScript g = ((r != null) ? GameUpdates.FindGame(r.GameId) : null);
		string when = ((g && g.releaseDate > 0) ? ("out in " + g.releaseDate + ((g.releaseDate == 1) ? " week" : " weeks")) : "out now");
		Hero(w, d, g ? g.myID : 0, "YOU MADE A LANDMARK GAME", d.Title, Cap(when) + "  ·  " + IndustryCore.GenreName(d.Genre));
		float y = 240f;
		Chips(w, y, new string[3] { "IP points", "+150", "gold" }, new string[3]
		{
			"New fans",
			IndustryCore.GenrePlural(d.Genre),
			"gold"
		}, new string[3] { "Hype", "+25", "gold" }, new string[3]
		{
			"The trend",
			IndustryCore.GenrePlural(d.Genre),
			"gold"
		});
		y += 76f;
		w.Label("Players know " + d.Title + " is something special. " + IndustryCore.GenrePlural(d.Genre) + " are the trend, and fans of " + IndustryCore.GenreGames(d.Genre) + " flock to your studio.", 24f, y, W - 48f, 48f, TextAnchor.UpperLeft, bold: false, null, 16);
		y += 64f;
		w.BigButton("Close", 14f, y, 220f, 44f, delegate
		{
			Landmarks.DoneShowing();
			EditorWindow.LandmarkMade.Close();
		});
		w.SmallButton("Open Landmarks...", 248f, y + 7f, 220f, delegate
		{
			Landmarks.DoneShowing();
			OpenList();
		}, enabled: true, null, null, 30f);
	}
}

// The Industry window's Landmarks tab: the landmarks made so far (all of them with "Show upcoming landmarks"), who made
// them, which are open now, and their pictures.
internal static class LandmarksTab
{
	// Made by anyone, or yours in the making.
	private static bool Known(LandmarkRecord r)
	{
		if (r != null)
		{
			if (!r.Player)
			{
				return r.Released;
			}
			return true;
		}
		return false;
	}

	public static void Build(EditorWindow w, float y)
	{
		float W = w.Width;
		if (!Landmarks.On)
		{
			w.Label("No landmarks in this game (the landmark list wasn't found, or this is multiplayer).", 24f, y + 10f, W - 48f, 40f, TextAnchor.UpperLeft, bold: false, null, 14);
			return;
		}
		Landmarks.Sync();
		Landmarks.RefreshPictures();
		int year = (G.MS ? G.MS.year : 1976);
		int mine = Landmarks.State.Made.Values.Count((LandmarkRecord r) => r.Player);
		bool all = Plugin.LandmarksShowUpcoming.Value;
		// Made ones (by anyone) and yours in the making; the rest only with "Show upcoming landmarks" (Game settings).
		List<LandmarkDef> shown = Landmarks.All.Where((LandmarkDef d) => all || Known(Landmarks.Record(d.Id))).OrderBy((LandmarkDef x) => x.Year).ToList();
		string intro = "Historical games you can make first: the right genre and topics, and a good enough game, from " + Landmarks.Lead + " years before the real one came out until its studio releases it. Yours: " + mine + ".";
		if (!all)
		{
			intro = intro + " Showing the " + shown.Count + " made so far; the rest are for you to discover (or turn on \"Show upcoming landmarks\" in Game settings).";
		}
		w.Label(intro, 24f, y, W - 48f, 40f, TextAnchor.UpperLeft, bold: false, null, 13).color = UIKit.Grey;
		y += 44f;
		ListView list = w.List(13f, y, W - 26f, w.Height - 75f - y - 10f);
		if (shown.Count == 0)
		{
			RowView none = list.Row(48f);
			none.Text("No landmark games have been made yet. Make one first, and it's yours.", 12f, none.W - 24f);
		}
		foreach (LandmarkDef d in shown)
		{
			LandmarkRecord r = Landmarks.Record(d.Id);
			gameScript g = ((r != null) ? GameUpdates.FindGame(r.GameId) : null);
			int pics = Landmarks.PictureCount(d);
			// Its series so far: the later games made (by you or a studio).
			List<string> madeEntries = new List<string>();
			foreach (LandmarkEntry e in d.Entries)
			{
				LandmarkRecord er = Landmarks.EntryRecord(e);
				if (er != null && er.Released)
				{
					publisherScript ep = IndustryCore.Studio(er.StudioId);
					madeEntries.Add(e.Name + " (" + er.Year + ", " + (er.Player ? "you" : (ep ? ep.GetName() : "a studio")) + (er.Short ? ", fell short" : "") + ")");
				}
			}
			string series = ((d.Entries.Count == 0) ? "" : (all ? ("  ·  series: " + madeEntries.Count + " of " + d.Entries.Count) : ((madeEntries.Count > 0) ? ("  ·  series: " + madeEntries.Count + " made") : "")));
			string seriesTip = ((madeEntries.Count > 0) ? ("\n\nThe series so far:\n" + string.Join("\n", madeEntries.Take(8)) + ((madeEntries.Count > 8) ? ("\n... and " + (madeEntries.Count - 8) + " more") : "")) : "");
			RowView row = list.Row(64f, null, "<b>" + d.Title + "</b>\n" + LandmarkUi.Recipe(d) + "\nProjected review " + d.Bar + "% or more." + seriesTip + "\n\nPictures: " + pics + " (landmarks/pictures/" + d.Id + ")");
			Sprite pic = Landmarks.Picture(d, g ? g.myID : 0);
			row.Picture(pic ? pic : LandmarkUi.GenrePic(d.Genre), 8f, 52f);
			row.Top(d.Name + "  (" + d.Year + ", " + d.Studio + ")", 72f, row.W - 380f);
			row.Bottom(LandmarkUi.Recipe(d) + "  ·  " + d.Bar + "%+" + series, 72f, row.W - 380f);
			string status;
			Color c;
			if (r != null && r.Player)
			{
				status = (r.Released ? ("Yours, " + r.Year) : "Yours, in the making");
				c = LandmarkUi.Gold;
			}
			else if (r != null && (r.Missed || (!r.Released && year > d.Year + Landmarks.StudioYears)))
			{
				// A studio made it (or is making it) too late to count: Landmarks.StudioYears.
				publisherScript pm = IndustryCore.Studio(r.StudioId);
				status = ((r.GameId < 0) ? "Missed: nobody made it" : ("Missed: " + (pm ? pm.GetName() : d.Studio) + (r.Released ? (" made it in " + r.Year) : " is making it") + ", too late"));
				c = UIKit.Grey;
			}
			else if (r != null && r.Released)
			{
				publisherScript p = IndustryCore.Studio(r.StudioId);
				status = "Made by " + (p ? p.GetName() : d.Studio) + ((r.Year > 0) ? (", " + r.Year) : "");
				c = UIKit.Grey;
			}
			else if (r != null)
			{
				publisherScript p2 = IndustryCore.Studio(r.StudioId);
				status = (p2 ? p2.GetName() : d.Studio) + " is making it: beat them to it";
				c = UIKit.Red;
			}
			else if (year < d.From)
			{
				status = "Opens in " + d.From;
				c = UIKit.Grey;
			}
			else if (Landmarks.Missed(d))
			{
				status = "Missed: nobody made it";
				c = UIKit.Grey;
			}
			else
			{
				status = "Open now";
				c = UIKit.Green;
			}
			row.Text(status, row.W - 300f, 200f, bold: true, TextAnchor.MiddleRight).color = c;
			row.Text((pics > 0) ? (pics + ((pics == 1) ? " picture" : " pictures")) : "no pictures", row.W - 92f, 84f, bold: false, TextAnchor.MiddleRight).color = UIKit.Grey;
		}
	}
}
