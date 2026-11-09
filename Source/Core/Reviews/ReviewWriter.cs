using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace HolyAddon.Core.Reviews;

internal class Review
{
	public Outlet Outlet;

	public int Score;

	public string ScoreText;

	public string Headline;

	public string Body;

	public string Reviewer;
}

// Writes the press reviews for a game. Purely cosmetic: the game's own scores are never changed, and the outlets'
// scores average out to the game's real total. Everything is seeded from the game's ID, so a game always gets
// the same outlets, reviewers and text.
internal static class ReviewWriter
{
	private static readonly Dictionary<int, (string key, List<Review> reviews)> cache = new Dictionary<int, (string, List<Review>)>();

	public static List<Review> For(gameScript g, int count)
	{
		if (!ReviewContent.Ready())
		{
			return new List<Review>();
		}
		string key = g.reviewTotal + "/" + g.reviewGameplay + "/" + g.reviewGrafik + "/" + g.reviewSound + "/" + g.reviewSteuerung + "/" + count + "/" + ReviewContent.Version;
		if (cache.TryGetValue(g.myID, out (string key, List<Review> reviews) hit) && hit.key == key)
		{
			return hit.reviews;
		}
		List<Review> reviews = Write(g, count);
		cache[g.myID] = (key, reviews);
		return reviews;
	}

	private static List<Review> Write(gameScript g, int count)
	{
		int year = (g.date_year > 0) ? g.date_year : (((bool)G.MS) ? G.MS.year : 1976);
		string genre = Clean(g.GetGenreString());
		System.Random pickRng = new System.Random(Seed(g.myID, "outlets"));
		List<(Outlet outlet, double weight)> pool = (from o in ReviewContent.Outlets
			where o.ActiveIn(year)
			select (o, Weight(o, g, genre))).ToList();
		List<Outlet> picked = new List<Outlet>();
		while (picked.Count < count && pool.Count > 0)
		{
			double total = pool.Sum(p => p.weight);
			double roll = pickRng.NextDouble() * total;
			int index = 0;
			for (; index < pool.Count - 1; index++)
			{
				roll -= pool[index].weight;
				if (roll <= 0.0)
				{
					break;
				}
			}
			picked.Add(pool[index].outlet);
			pool.RemoveAt(index);
		}
		int[] scores = Scores(g, picked, pickRng);
		Dictionary<string, string> vars = Variables(g, year, genre);
		// Sentences already used for this game, so its outlets never read like copies of each other.
		HashSet<string> used = new HashSet<string>();
		List<Review> list = new List<Review>();
		for (int i = 0; i < picked.Count; i++)
		{
			list.Add(Compose(g, picked[i], scores[i], vars, used));
		}
		return list;
	}

	private static double Weight(Outlet o, gameScript g, string genre)
	{
		double weight = 1.0;
		if (o.Genres.Contains(genre))
		{
			weight += 2.0;
		}
		if (o.Retro)
		{
			weight = g.retro ? (weight + 4.0) : (weight * 0.3);
		}
		return weight;
	}

	// Each outlet's score: its own bias plus some noise, then shifted so the average is exactly the game's real score.
	private static int[] Scores(gameScript g, List<Outlet> outlets, System.Random rng)
	{
		int n = outlets.Count;
		float[] raw = new float[n];
		for (int i = 0; i < n; i++)
		{
			raw[i] = outlets[i].Bias + Gaussian(rng) * outlets[i].Spread;
		}
		float mean = (n > 0) ? raw.Average() : 0f;
		int[] scores = new int[n];
		for (int i = 0; i < n; i++)
		{
			scores[i] = Mathf.Clamp(Mathf.RoundToInt((float)g.reviewTotal + raw[i] - mean), 1, 100);
		}
		// Rounding can leave the sum a point or two off; nudge the outlets furthest from the average.
		int diff = g.reviewTotal * n - scores.Sum();
		for (int pass = 0; pass < 4 * n && diff != 0; pass++)
		{
			int i = pass % n;
			int next = scores[i] + Math.Sign(diff);
			if (next >= 1 && next <= 100)
			{
				scores[i] = next;
				diff -= Math.Sign(diff);
			}
		}
		return scores;
	}

	// One outlet's review. The shape varies too: genre and general gameplay lines, an optional topic line, the
	// presentation sentences in a random order (two of them sometimes merged into one), and an aside in the
	// outlet's voice, before the verdict.
	private static Review Compose(gameScript g, Outlet o, int score, Dictionary<string, string> baseVars, HashSet<string> used)
	{
		System.Random rng = new System.Random(Seed(g.myID, o.Id));
		Dictionary<string, string> vars = new Dictionary<string, string>(baseVars) { ["outlet"] = o.Name };
		string voice = o.Voice;
		string band = Band(score);
		List<string> body = new List<string>();
		body.Add(Pick(ReviewContent.Pool("intros", IntroKind(g, vars)), rng, used) ?? Pick(ReviewContent.Pool("intros", "default"), rng, used));
		string gameplayBand = Band(g.reviewGameplay);
		string genreKind = (g.reviewGameplay >= 70) ? "good" : ((g.reviewGameplay < 50) ? "bad" : null);
		string genreLine = ((genreKind != null) ? Pick(ReviewContent.Pool("genres", vars["genre"], genreKind), rng, used) : null);
		string generalLine = Pick(ReviewContent.Pool("aspects", "gameplay", gameplayBand), rng, used);
		if (genreLine != null && (rng.NextDouble() < 0.75 || generalLine == null))
		{
			body.Add(genreLine);
			// Outlets that love this genre have more to say about it.
			if (o.Genres.Contains(vars["genre"]) && rng.NextDouble() < 0.5)
			{
				body.Add(generalLine);
			}
		}
		else
		{
			body.Add(generalLine);
		}
		if (rng.NextDouble() < 0.55)
		{
			body.Add(Pick(ReviewContent.Pool("topics", vars["topic"]), rng, used));
		}
		List<(string aspect, int value)> presentation = new List<(string, int)>
		{
			("graphics", g.reviewGrafik),
			("sound", g.reviewSound),
			("controls", g.reviewSteuerung)
		}.OrderBy(((string aspect, int value) _) => rng.Next()).ToList();
		string pair = ((rng.NextDouble() < 0.5) ? Pair(presentation[0], presentation[1], rng, used) : null);
		if (pair != null)
		{
			body.Add(pair);
			body.Add(Pick(ReviewContent.Pool("aspects", presentation[2].aspect, Band(presentation[2].value)), rng, used));
		}
		else
		{
			foreach ((string aspect, int value) in presentation)
			{
				body.Add(Pick(ReviewContent.Pool("aspects", aspect, Band(value)), rng, used));
			}
		}
		if (rng.NextDouble() < 0.35)
		{
			string aside = Pick(ReviewContent.Pool("asides", voice), rng, used);
			if (aside != null)
			{
				body.Insert(1 + rng.Next(body.Count), aside);
			}
		}
		if (g.finanzierung_Grundkosten < 75 || g.finanzierung_Kontent < 75 || g.finanzierung_Technology < 75)
		{
			body.Add(Pick(ReviewContent.Pool("budget"), rng, used));
		}
		body.Add(Pick(ReviewContent.Pool("verdicts", voice, band), rng, used));
		return new Review
		{
			Outlet = o,
			Score = score,
			ScoreText = FormatScore(o.Scale, score, rng),
			Headline = Fill(Pick(ReviewContent.Pool("headlines", voice, band), rng, used) ?? "", vars),
			Body = string.Join(" ", from s in body
				where !string.IsNullOrEmpty(s)
				select Sentence(Fill(s, vars))),
			Reviewer = Reviewer(rng)
		};
	}

	private static string IntroKind(gameScript g, Dictionary<string, string> vars)
	{
		if (g.typ_nachfolger && vars["original"] != "")
		{
			return "sequel";
		}
		if (g.typ_spinoff && vars["original"] != "")
		{
			return "spinoff";
		}
		if (g.portID != -1 && vars["original"] != "")
		{
			return "port";
		}
		if (g.typ_remaster)
		{
			return "remaster";
		}
		if (g.retro)
		{
			return "retro";
		}
		if (g.exklusiv)
		{
			return "exclusive";
		}
		if (g.typ_budget)
		{
			return "budget";
		}
		return "default";
	}

	private static Dictionary<string, string> Variables(gameScript g, int year, string genre)
	{
		string topic = (((bool)G.TS) ? Clean(G.TS.GetThemes(g.gameMainTheme)) : "");
		string genreLower = genre.ToLowerInvariant();
		if (!g.gamePlatformScript[0])
		{
			g.FindMyPlatforms();
		}
		string platform = (((bool)g.gamePlatformScript[0]) ? g.gamePlatformScript[0].GetName() : "every platform");
		int originalId = ((g.portID != -1) ? g.portID : g.originalGameID);
		gameScript original = FindGame(originalId);
		return new Dictionary<string, string>
		{
			["game"] = Clean(g.GetNameSimple()),
			["dev"] = Clean(g.GetDeveloperName()),
			["pub"] = Clean(g.GetPublisherName()),
			["genre"] = genre,
			["genre_l"] = genreLower,
			["a_genre_l"] = (("aeiou".IndexOf(genreLower.FirstOrDefault()) >= 0) ? "an " : "a ") + genreLower,
			["topic"] = topic,
			["topic_l"] = topic.ToLowerInvariant(),
			["platform"] = Clean(platform),
			["year"] = year.ToString(),
			["original"] = ((bool)original) ? Clean(original.GetNameSimple()) : ""
		};
	}

	private static gameScript FindGame(int id)
	{
		if (id < 0 || !G.MainObj)
		{
			return null;
		}
		games all = G.MainObj.GetComponent<games>();
		if (!all || all.arrayGamesScripts == null)
		{
			return null;
		}
		return all.arrayGamesScripts.FirstOrDefault((gameScript s) => (bool)s && s.myID == id);
	}

	public static string Band(int score)
	{
		if (score >= 85)
		{
			return "great";
		}
		if (score >= 70)
		{
			return "good";
		}
		if (score >= 50)
		{
			return "ok";
		}
		if (score >= 30)
		{
			return "poor";
		}
		return "awful";
	}

	private static string FormatScore(string scale, int score, System.Random rng)
	{
		switch (scale)
		{
		case "ten":
			return ((float)score / 10f).ToString("0.0") + "/10";
		case "tenInt":
			return Mathf.Clamp(Mathf.RoundToInt((float)score / 10f), 1, 10) + "/10";
		case "tenHalf":
			return (Mathf.Clamp(Mathf.Round((float)score / 5f), 1f, 20f) / 2f).ToString("0.#") + "/10";
		case "five":
			return (Mathf.Clamp(Mathf.Round((float)score / 10f), 1f, 10f) / 2f).ToString("0.#") + "/5";
		case "forty":
		{
			// Four reviewers, each scoring out of ten.
			int sum = 0;
			for (int i = 0; i < 4; i++)
			{
				sum += Mathf.Clamp(Mathf.RoundToInt((float)score / 10f + (float)(rng.NextDouble() - 0.5)), 1, 10);
			}
			return sum + "/40";
		}
		case "yesno":
			return (score >= 70) ? "Play it: Yes" : ((score >= 50) ? "Play it: Not yet" : "Play it: No");
		case "buy":
			return (score >= 75) ? "Buy it" : ((score >= 55) ? "Wait for a sale" : "Skip it");
		case "thumbs":
			return (score >= 65) ? "Thumbs up" : "Thumbs down";
		default:
			return score + "%";
		}
	}

	private static string Reviewer(System.Random rng)
	{
		textScript ts = G.TS;
		string[] first = ((rng.NextDouble() < 0.5) ? ts?.namesFemale : ts?.namesMale);
		string[] last = ts?.surname;
		string a = PickName(first, rng);
		string b = PickName(last, rng);
		if (a == null || b == null)
		{
			return "Staff writer";
		}
		return a + " " + b;
	}

	private static string PickName(string[] names, System.Random rng)
	{
		if (names == null || names.Length == 0)
		{
			return null;
		}
		for (int tries = 0; tries < 10; tries++)
		{
			string name = names[rng.Next(names.Length)]?.Trim();
			if (!string.IsNullOrEmpty(name) && !name.StartsWith("/") && !name.StartsWith("["))
			{
				return name;
			}
		}
		return null;
	}

	// A random line from the pool, preferring lines this game's other outlets haven't used yet.
	private static string Pick(IList<string> pool, System.Random rng, HashSet<string> used)
	{
		if (pool == null || pool.Count == 0)
		{
			return null;
		}
		List<string> fresh = pool.Where((string s) => !used.Contains(s)).ToList();
		IList<string> from = ((fresh.Count > 0) ? ((IList<string>)fresh) : pool);
		string pick = from[rng.Next(from.Count)];
		used.Add(pick);
		return pick;
	}

	// Two presentation aspects in one sentence, from short clauses: "{A}, and {B}" when both lean the same way,
	// "{A}, but {B}" when they don't.
	private static string Pair((string aspect, int value) a, (string aspect, int value) b, System.Random rng, HashSet<string> used)
	{
		string first = Pick(ReviewContent.Pool("bits", a.aspect, Band(a.value)), rng, used);
		string second = Pick(ReviewContent.Pool("bits", b.aspect, Band(b.value)), rng, used);
		if (first == null || second == null)
		{
			return null;
		}
		string kind = ((Lean(a.value) == Lean(b.value)) ? "same" : "mixed");
		string template = Pick(ReviewContent.Pool("pairs", kind), rng, used);
		if (template == null)
		{
			return null;
		}
		return template.Replace("{A}", first).Replace("{B}", second);
	}

	private static int Lean(int score)
	{
		if (score >= 70)
		{
			return 1;
		}
		return (score >= 50) ? 0 : (-1);
	}

	private static string Fill(string template, Dictionary<string, string> vars)
	{
		return Regex.Replace(template, "\\{([a-z_]+)\\}", (Match m) => vars.TryGetValue(m.Groups[1].Value, out string value) ? value : m.Value);
	}

	// Templates may start with a lowercase placeholder ("{a_genre_l} with..."), so capitalise the first letter.
	private static string Sentence(string text)
	{
		if (string.IsNullOrEmpty(text) || !char.IsLower(text[0]))
		{
			return text;
		}
		return char.ToUpperInvariant(text[0]) + text.Substring(1);
	}

	// Game names and topics can carry rich-text tags or the topic file's age/genre tags.
	private static string Clean(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return "";
		}
		return Regex.Replace(text, "<[^>]*>", "").Trim();
	}

	private static float Gaussian(System.Random rng)
	{
		double u = 1.0 - rng.NextDouble();
		double v = rng.NextDouble();
		return (float)(Math.Sqrt(-2.0 * Math.Log(u)) * Math.Cos(Math.PI * 2.0 * v));
	}

	// FNV-1a: string.GetHashCode isn't guaranteed to be stable, and a game must always get the same reviews.
	private static int Seed(int gameId, string salt)
	{
		uint hash = 2166136261u;
		foreach (char c in gameId + ":" + salt)
		{
			hash ^= c;
			hash *= 16777619u;
		}
		return (int)hash;
	}
}
