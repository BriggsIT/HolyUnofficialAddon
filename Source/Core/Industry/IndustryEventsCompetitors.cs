using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HolyAddon.Core.Industry;

// Your competitors' fights (rival merge, step 2): market fights rather than feuds, from the studios that meet you most
// in the market (IndustryCompetitors). Picked at random like any decision, at most one every few weeks. Standing only
// moves a little, but fights that go badly add up: a competitor you keep clashing with can turn into a rival.
internal static partial class IndustryEvents
{
	private static IEnumerable<IndustryEventDef> CompetitorFights()
	{
		yield return CompetitorClash();
		yield return TalentRace();
		yield return PriceWar();
		yield return SameIdea();
	}

	private static bool Fired(string id, int weeks)
	{
		if (IndustryCore.State.LastFired.TryGetValue(id, out var last))
		{
			return Now - last < weeks;
		}
		return false;
	}

	private static bool Standalone(gameScript g)
	{
		if (!g.typ_addon && !g.typ_addonStandalone && !g.typ_mmoaddon && !g.typ_bundle && !g.typ_bundleAddon && !g.typ_budget)
		{
			return !g.typ_goty;
		}
		return false;
	}

	// "Action game", "Sports Game" (some genre names already end in "Game").
	private static string GenreGame(gameScript g)
	{
		string n = (g ? IndustryCore.GenreName(g.maingenre) : "games");
		if (!g || g.maingenre < 0 || n == "games")
		{
			return "game";
		}
		if (n.EndsWith("Game") || n.EndsWith("game"))
		{
			return n;
		}
		return n + " game";
	}

	private static string AOrAn(string word)
	{
		if (!string.IsNullOrEmpty(word) && "AEIOUaeiou".IndexOf(word[0]) >= 0)
		{
			return "an " + word;
		}
		return "a " + word;
	}

	private static long MarketingCost(gameScript g)
	{
		return OfRevenue(g, 0.015f, (long)((float)Scaled(20000.0) * (1f + (float)(g ? g.gameSize : 0))));
	}

	// A competitor's game in your genre lands with yours. With a release date set (pre-orders), their game really is
	// moved onto your week, and the better-reviewed one wins the launch weeks once both are out. Released straight
	// away, it's a competitor's game that came out within two weeks of yours.
	private static IndustryEventDef CompetitorClash()
	{
		return new IndustryEventDef
		{
			Id = "cp_clash",
			Cooldown = 20,
			Setup = delegate(IndustryContext c)
			{
				if (!IndustryCompetitors.FightReady() || Fired("rv_clash", 8))
				{
					return false;
				}
				List<publisherScript> comps = IndustryCompetitors.All();
				gameScript coming = IndustryCore.MyGamesOnMarket().FirstOrDefault((gameScript x) => x.releaseDate >= 2 && x.releaseDate <= 6 && Standalone(x) && !IndustryCompetitors.Clashing(x));
				if ((bool)coming)
				{
					foreach (publisherScript p in comps.OrderBy((publisherScript _) => Random.value))
					{
						gameScript t = TheirGame(p);
						if ((bool)t && !t.auftragsspiel && !t.pubAngebot && Standalone(t) && IndustryCompetitors.SameAudience(coming, t))
						{
							c.GameId = coming.myID;
							c.Game2 = t.myID;
							c.StudioId = p.myID;
							c.Number2 = 0;
							c.Amount = MarketingCost(coming);
							return true;
						}
					}
				}
				gameScript launched = (from x in IndustryCore.MyGamesOnMarket()
					where x.releaseDate <= 0 && x.weeksOnMarket <= 2 && Standalone(x)
					orderby x.weeksOnMarket
					select x).FirstOrDefault();
				if (!launched)
				{
					return false;
				}
				HashSet<int> ids = new HashSet<int>(comps.Select((publisherScript p) => p.myID));
				gameScript theirs = (from x in IndustryCore.AllGames()
					where x.isOnMarket && x.releaseDate <= 0 && x.weeksOnMarket <= 2 && !x.IsMyGame() && ids.Contains(x.developerID) && Standalone(x) && IndustryCompetitors.SameAudience(launched, x)
					orderby Weekly(x) descending
					select x).FirstOrDefault();
				if (!theirs)
				{
					return false;
				}
				c.GameId = launched.myID;
				c.Game2 = theirs.myID;
				c.StudioId = theirs.developerID;
				c.Number2 = 1;
				c.Amount = MarketingCost(launched);
				return true;
			},
			Title = (IndustryContext c) => "Release Clash",
			Text = (IndustryContext c) => (c.Number2 == 0) ? (StudioName(c.StudioId) + ", one of your competitors, is bringing " + Name(c.Game2) + " out the same week as " + Name(c.GameId) + ": another " + GenreGame(Game(c.GameId)) + ", for the same players. Most of them will only buy one.") : (StudioName(c.StudioId) + "'s " + Name(c.Game2) + " came out the same fortnight as " + Name(c.GameId) + ": another " + GenreGame(Game(c.GameId)) + ", for the same players."),
			Default = 1,
			Choices = (IndustryContext c) => (c.Number2 == 0) ? new List<IndustryChoice>
			{
				Choice("Delay Ours 3 Weeks", "Avoid the fight", Name(c.GameId) + " comes out 3 weeks later", delegate(IndustryContext x)
				{
					gameScript g = Game(x.GameId);
					publisherScript p = IndustryCore.Studio(x.StudioId);
					if (!g || g.releaseDate <= 0)
					{
						return "Too late: it's already out.";
					}
					IndustryCompetitors.SetForYourWeek(p, g.releaseDate);
					g.releaseDate += 3;
					IndustryCore.Change(p, 2, "You moved " + g.GetNameSimple() + " out of their way");
					return "You moved " + g.GetNameSimple() + " three weeks back. " + Name(x.Game2) + " gets the week to itself.";
				}),
				Choice("Hold the Date", "The better game wins", "Once both are out: the better-reviewed one sells 10% more for 6 weeks, the other 10% less", delegate(IndustryContext x)
				{
					gameScript g = Game(x.GameId);
					gameScript t = Game(x.Game2);
					publisherScript p = IndustryCore.Studio(x.StudioId);
					if (!g || !t || !p)
					{
						return "The clash fizzled out.";
					}
					IndustryCompetitors.SetForYourWeek(p, g.releaseDate);
					IndustryCompetitors.AddClash(g, t, p, 0);
					return "You hold the date. Both games come out the same week, and the reviews will decide.";
				}),
				Choice("Out-Market It", "Costs " + Money(c.Amount), "Hype +12, and an 8-point edge when the reviews decide", delegate(IndustryContext x)
				{
					gameScript g = Game(x.GameId);
					gameScript t = Game(x.Game2);
					publisherScript p = IndustryCore.Studio(x.StudioId);
					if (!g || !t || !p)
					{
						return "The clash fizzled out.";
					}
					G.MS.Pay(x.Amount, 12);
					g.AddHype(12f);
					IndustryCompetitors.SetForYourWeek(p, g.releaseDate);
					IndustryCompetitors.AddClash(g, t, p, 8);
					IndustryCore.Change(p, -2, "You out-marketed them for the same release week");
					return "Your campaign is everywhere in the run-up. Both games come out the same week.";
				}, (IndustryContext x) => Need(x.Amount))
			} : new List<IndustryChoice>
			{
				Choice("Out-Market It", "Costs " + Money(c.Amount), "Hype +10, " + Name(c.GameId) + " sells 8% more for 6 weeks", delegate(IndustryContext x)
				{
					gameScript g = Game(x.GameId);
					if (!g)
					{
						return "The game is gone.";
					}
					G.MS.Pay(x.Amount, 12);
					g.AddHype(10f);
					IndustryCore.AddMod(g, 0.08f, 6);
					IndustryCore.Change(IndustryCore.Studio(x.StudioId), -2, "You out-marketed their launch");
					return "Your campaign drowns out theirs: " + g.GetNameSimple() + " sells 8% more for 6 weeks.";
				}, (IndustryContext x) => Need(x.Amount)),
				Choice("Let It Ride", "The better game wins", "The better-reviewed one sells 8% more for 6 weeks, the other 8% less", delegate(IndustryContext x)
				{
					gameScript g = Game(x.GameId);
					gameScript t = Game(x.Game2);
					if (!g || !t)
					{
						return "The clash fizzled out.";
					}
					string score = g.reviewTotal + "% against " + t.reviewTotal + "%";
					if (g.reviewTotal >= t.reviewTotal)
					{
						IndustryCore.AddMod(g, 0.08f, 6);
						IndustryCore.AddMod(t, -0.08f, 6);
						IndustryCore.Change(IndustryCore.Studio(x.StudioId), -2, g.GetNameSimple() + " beat their " + t.GetNameSimple());
						return "Yours reviewed better (" + score + "): players picked " + g.GetNameSimple() + ", which sells 8% more for 6 weeks.";
					}
					IndustryCore.AddMod(g, -0.08f, 6);
					IndustryCore.AddMod(t, 0.08f, 6);
					return "Theirs reviewed better (" + score + "): " + g.GetNameSimple() + " sells 8% less for 6 weeks.";
				})
			}
		};
	}

	// A competitor offers one of your good people a job (lighter than the arch-rival's poaching: not your very best).
	private static IndustryEventDef TalentRace()
	{
		return new IndustryEventDef
		{
			Id = "cp_talent",
			Cooldown = 30,
			Weight = 0.9f,
			Setup = delegate(IndustryContext c)
			{
				if (!IndustryCompetitors.FightReady())
				{
					return false;
				}
				List<characterScript> staff = (from e in Employees.All()
					where e.myID != 1 && (e.perks == null || e.perks.Length == 0 || !e.perks[0])
					orderby Employees.BestSkill(e) descending
					select e).ToList();
				if (staff.Count < 3)
				{
					return false;
				}
				List<characterScript> good = staff.Skip(1).Take(Mathf.Max(1, Mathf.RoundToInt((float)staff.Count * 0.3f))).ToList();
				List<publisherScript> hirers = IndustryCompetitors.All().Where((publisherScript p) => p.developer).ToList();
				if (hirers.Count == 0)
				{
					return false;
				}
				publisherScript who = Pick(hirers);
				c.StudioId = who.myID;
				c.Number = Pick(good).myID;
				gameScript t = TheirGame(who);
				c.Number2 = (t ? t.maingenre : who.fanGenre);
				return true;
			},
			Title = (IndustryContext c) => "Talent Race",
			Text = delegate(IndustryContext c)
			{
				characterScript p = Person(c.Number);
				string name = (p ? (p.myName + " (" + Employees.Profession(p) + ")") : "one of your people");
				return StudioName(c.StudioId) + ", one of your competitors, offered " + name + " a job making " + IndustryCore.GenreGames(c.Number2) + ", for more money than you pay.";
			},
			Default = 0,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Match It", "A 15% raise", "They stay, but cost more every month", delegate(IndustryContext x)
				{
					characterScript p = Person(x.Number);
					if (!p)
					{
						return "They've already left.";
					}
					Raise(p, 1.15f);
					p.AddMotivation(10f);
					IndustryCore.Change(IndustryCore.Studio(x.StudioId), -1, "Tried to hire " + p.myName + " away");
					return p.myName + " stays, with a raise.";
				}),
				Choice("Let Them Go", "Nothing to pay", "You lose them to a competitor", delegate(IndustryContext x)
				{
					characterScript p = Person(x.Number);
					if (!p)
					{
						return "They've already left.";
					}
					string name = p.myName;
					p.Entlassen(eventMitarbeiterMotivation: false);
					publisherScript s = IndustryCore.Studio(x.StudioId);
					IndustryCore.AddStars(s, 1f);
					IndustryCore.Change(s, -2, "Hired " + name + " away from you");
					return name + " took the job at " + StudioName(x.StudioId) + ".";
				})
			}
		};
	}

	// A competitor cuts the price of the game fighting yours. Only for games you publish yourself: publishers set the
	// prices of the games they publish.
	private static IndustryEventDef PriceWar()
	{
		return new IndustryEventDef
		{
			Id = "cp_price",
			Cooldown = 26,
			Weight = 0.9f,
			Setup = delegate(IndustryContext c)
			{
				if (!IndustryCompetitors.FightReady() || !G.MS)
				{
					return false;
				}
				gameScript g = (from x in IndustryCore.MyGamesOnMarket()
					where x.publisherID == G.MS.myID && x.releaseDate <= 0 && x.weeksOnMarket >= 2 && x.weeksOnMarket <= 30 && Standalone(x) && !x.arcade && Weekly(x) > 0
					orderby Weekly(x) descending
					select x).FirstOrDefault();
				if (!g)
				{
					return false;
				}
				HashSet<int> ids = new HashSet<int>(from p in IndustryCompetitors.All()
					select p.myID);
				gameScript t = (from x in IndustryCore.AllGames()
					where x.isOnMarket && x.releaseDate <= 0 && x.weeksOnMarket <= 30 && !x.IsMyGame() && ids.Contains(x.developerID) && Standalone(x) && IndustryCompetitors.SameAudience(g, x) && Weekly(x) > 0
					orderby Weekly(x) descending
					select x).FirstOrDefault();
				if (!t)
				{
					return false;
				}
				c.GameId = g.myID;
				c.Game2 = t.myID;
				c.StudioId = t.developerID;
				c.Number = Random.Range(3, 7) * 5;
				return true;
			},
			Title = (IndustryContext c) => "Price War",
			Text = (IndustryContext c) => StudioName(c.StudioId) + " cut the price of " + Name(c.Game2) + " by " + c.Number + "%, right where " + Name(c.GameId) + " is selling. Players are noticing.",
			Default = 1,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Match It", "Price -" + c.Number + "% for 8 weeks", "Sales +" + c.Number / 2 + "% while it lasts; standing with them -2", delegate(IndustryContext x)
				{
					gameScript g = Game(x.GameId);
					if (!g)
					{
						return "The game is gone.";
					}
					IndustryCore.AddPriceCut(g, 1f - (float)x.Number / 100f, 8);
					IndustryCore.AddPriceCut(Game(x.Game2), 1f - (float)x.Number / 100f, 8);
					IndustryCore.AddMod(g, (float)x.Number / 200f, 8);
					IndustryCore.Change(IndustryCore.Studio(x.StudioId), -2, "You matched their price cut on " + Name(x.Game2));
					return "You matched their price: " + g.GetNameSimple() + " is " + x.Number + "% cheaper for 8 weeks, and the bargain hunters come back.";
				}),
				Choice("Hold Your Price", "Full price per copy", Name(c.GameId) + " sells 12% less for 6 weeks", delegate(IndustryContext x)
				{
					IndustryCore.AddPriceCut(Game(x.Game2), 1f - (float)x.Number / 100f, 8);
					IndustryCore.AddMod(Game(x.GameId), -0.12f, 6);
					IndustryCore.AddMod(Game(x.Game2), 0.06f, 6);
					return "You hold your price. Some buyers go for the cheaper game for a while.";
				})
			}
		};
	}

	// A competitor announces a game in the same genre (and maybe topic) as yours in development.
	private static IndustryEventDef SameIdea()
	{
		return new IndustryEventDef
		{
			Id = "cp_idea",
			Cooldown = 30,
			Weight = 0.9f,
			Setup = delegate(IndustryContext c)
			{
				if (!IndustryCompetitors.FightReady())
				{
					return false;
				}
				List<gameScript> mine = IndustryCore.MyGamesInDevelopment().Where((gameScript x) => Standalone(x) && DevProgress(x) >= 0.1f && DevProgress(x) <= 0.8f).ToList();
				if (mine.Count == 0)
				{
					return false;
				}
				List<KeyValuePair<gameScript, gameScript>> pairs = new List<KeyValuePair<gameScript, gameScript>>();
				foreach (publisherScript p in IndustryCompetitors.All())
				{
					gameScript t = TheirGame(p);
					if (!t || t.auftragsspiel || t.pubAngebot || !Standalone(t))
					{
						continue;
					}
					gameScript like = mine.Where((gameScript m) => m.maingenre == t.maingenre).OrderByDescending((gameScript m) => (m.gameMainTheme == t.gameMainTheme) ? 1 : 0).FirstOrDefault();
					if ((bool)like)
					{
						pairs.Add(new KeyValuePair<gameScript, gameScript>(like, t));
					}
				}
				if (pairs.Count == 0)
				{
					return false;
				}
				KeyValuePair<gameScript, gameScript> pair = pairs.OrderByDescending((KeyValuePair<gameScript, gameScript> kv) => (kv.Key.gameMainTheme == kv.Value.gameMainTheme) ? 1 : 0).ThenBy((KeyValuePair<gameScript, gameScript> _) => Random.value).First();
				c.GameId = pair.Key.myID;
				c.Game2 = pair.Value.myID;
				c.StudioId = pair.Value.developerID;
				c.Number = ((pair.Key.gameMainTheme == pair.Value.gameMainTheme) ? 1 : 0);
				c.Amount = MarketingCost(pair.Key);
				return true;
			},
			Title = (IndustryContext c) => "Same Idea",
			Text = delegate(IndustryContext c)
			{
				gameScript g = Game(c.GameId);
				string topic = ((c.Number == 1 && (bool)g && (bool)G.TS) ? System.Text.RegularExpressions.Regex.Replace(G.TS.GetThemes(g.gameMainTheme) ?? "", "<[^>]*>", "").Trim() : "");
				return StudioName(c.StudioId) + ", one of your competitors, just announced " + Name(c.Game2) + ": " + ((topic.Length > 0) ? (AOrAn(topic + " " + GenreGame(g)) + ", just like ") : ("another " + GenreGame(g) + ", like ")) + Name(c.GameId) + ", which you're still making. Players will compare them.";
			},
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Rush It", "Crunch to get there first", "Staff motivation -10; launch sales +10% for 8 weeks; their game slips 4 weeks", delegate(IndustryContext x)
				{
					gameScript g = Game(x.GameId);
					publisherScript p = IndustryCore.Studio(x.StudioId);
					if (!g)
					{
						return "The game is gone.";
					}
					foreach (characterScript e in Employees.All())
					{
						if (e.myID != 1)
						{
							e.AddMotivation(-10f);
						}
					}
					g.AddHype(5f);
					IndustryCore.AddModAfterRelease(g, 0.1f, 8);
					if ((bool)p)
					{
						p.newGameInWeeks += 4;
						p.newGameInWeeksORG = Mathf.Max(p.newGameInWeeksORG, p.newGameInWeeks);
					}
					Announce(x.Game2);
					IndustryCore.Change(p, -2, "You raced them to the shelves with " + g.GetNameSimple());
					return "The team is crunching. " + g.GetNameSimple() + " will be the first of the two in the shops.";
				}),
				Choice("Make It Stand Out", "Costs " + Money(c.Amount), "Hype +10; launch sales +6% for 12 weeks", delegate(IndustryContext x)
				{
					gameScript g = Game(x.GameId);
					if (!g)
					{
						return "The game is gone.";
					}
					G.MS.Pay(x.Amount, 12);
					g.AddHype(10f);
					IndustryCore.AddModAfterRelease(g, 0.06f, 12);
					Announce(x.Game2);
					return "Your campaign shows what makes " + g.GetNameSimple() + " different.";
				}, (IndustryContext x) => Need(x.Amount)),
				Choice("Ignore It", "Nothing to pay", "Launch sales -8% for 12 weeks", delegate(IndustryContext x)
				{
					IndustryCore.AddModAfterRelease(Game(x.GameId), -0.08f, 12);
					Announce(x.Game2);
					return "You carry on. Some players will wait to compare the two.";
				})
			}
		};
	}

	private static void Announce(int gameId)
	{
		gameScript t = Game(gameId);
		if ((bool)t && t.inDevelopment && !t.isOnMarket)
		{
			t.angekuendigt = true;
		}
	}
}
