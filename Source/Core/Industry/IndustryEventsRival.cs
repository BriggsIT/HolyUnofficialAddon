using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HolyAddon.Core.Industry;

// Your arch-rival's moves (The Holy Expansion's, merged): started on the rival's own clock by IndustryRivals, never
// at random. Each pops up like any industry decision, in the rival's colours.
internal static partial class IndustryEvents
{
	private static IEnumerable<IndustryEventDef> RivalMoves()
	{
		yield return RivalPoach();
		yield return RivalCopy();
		yield return RivalClash();
		yield return RivalTaunt();
	}

	private static characterScript Person(int id)
	{
		return Employees.All().FirstOrDefault((characterScript e) => e.myID == id);
	}

	// A raise on the base salary. GetGehalt() adds the sandbox salary setting on top, so raising from it compounded.
	private static void Raise(characterScript p, float factor)
	{
		p.GetGehalt();
		p.gehalt = Mathf.RoundToInt((float)p.gehalt * factor);
	}

	// Your best person (not you).
	private static characterScript BestEmployee()
	{
		return (from e in Employees.All()
			where e.myID != 1 && (e.perks == null || e.perks.Length == 0 || !e.perks[0])
			orderby Employees.BestSkill(e) descending
			select e).FirstOrDefault();
	}

	// It offers one of your best people a big salary.
	private static IndustryEventDef RivalPoach()
	{
		return new IndustryEventDef
		{
			Id = "rv_poach",
			Scripted = true,
			Setup = delegate(IndustryContext c)
			{
				publisherScript a = IndustryRivals.Arch;
				characterScript best = BestEmployee();
				if (!a || !best)
				{
					return false;
				}
				c.StudioId = a.myID;
				c.Number = best.myID;
				return true;
			},
			Title = (IndustryContext c) => "Poaching Attempt",
			Text = (IndustryContext c) => StudioName(c.StudioId) + " offered " + (Person(c.Number) ? Person(c.Number).myName : "one of your people") + ", one of your best people, a big salary to switch sides.",
			Default = 1,
			Choices = delegate(IndustryContext c)
			{
				characterScript who = Person(c.Number);
				int chance = (who ? Mathf.RoundToInt(Mathf.Clamp01(who.s_motivation / 100f * 0.8f + 0.1f) * 100f) : 0);
				return new List<IndustryChoice>
				{
					Choice("Counter-Offer", "A 25% raise", "They stay, but cost more every month", delegate(IndustryContext x)
					{
						characterScript p = Person(x.Number);
						if (!p)
						{
							return "They've already left.";
						}
						Raise(p, 1.25f);
						p.AddMotivation(15f);
						IndustryCore.State.RivalWins++;
						IndustryCore.Change(IndustryCore.Studio(x.StudioId), -4, "Their poaching attempt failed");
						return p.myName + " stays, with a nice raise.";
					}),
					Choice("Appeal to Their Loyalty", "Free", "Works if they're happy here (about " + chance + "%)", delegate(IndustryContext x)
					{
						characterScript p = Person(x.Number);
						if (!p)
						{
							return "They've already left.";
						}
						if (Random.value < p.s_motivation / 100f * 0.8f + 0.1f)
						{
							IndustryCore.State.RivalWins++;
							IndustryCore.Change(IndustryCore.Studio(x.StudioId), -2, "Their poaching attempt failed");
							return p.myName + " turned the offer down: \"This is my team.\"";
						}
						return Lost(x, p);
					}),
					Choice("Let Them Go", "Nothing to pay", "You lose them", (IndustryContext x) => Person(x.Number) ? Lost(x, Person(x.Number)) : "They've already left.")
				};
			}
		};
	}

	private static string Lost(IndustryContext c, characterScript p)
	{
		string name = p.myName;
		p.Entlassen(eventMitarbeiterMotivation: false);
		IndustryCore.State.PoachedFromUs++;
		IndustryCore.State.RivalLosses++;
		publisherScript a = IndustryCore.Studio(c.StudioId);
		IndustryCore.AddStars(a, 2f);
		IndustryRivals.Note(a, "Poached " + name + " from you");
		return name + " took the offer and left for " + StudioName(c.StudioId) + ".";
	}

	// It turns its next game into a copy of your hit, and brings it out within a couple of months: a real game, judged
	// against yours by the reviews when it lands (IndustryCompetitors settles it, and its pop-up shows how it went).
	private static IndustryEventDef RivalCopy()
	{
		return new IndustryEventDef
		{
			Id = "rv_copy",
			Scripted = true,
			Setup = delegate(IndustryContext c)
			{
				publisherScript a = IndustryRivals.Arch;
				gameScript g = (from x in IndustryCore.MyGamesOnMarket()
					where x.weeksOnMarket < 40 && Standalone(x) && x.reviewTotal >= 70 && x.releaseDate <= 0
					orderby Weekly(x) descending
					select x).FirstOrDefault();
				gameScript theirs = TheirGame(a);
				// Not in the same weeks as A Copy of Your Game (both would hit one game). Needs their real next game.
				if (!a || !g || !theirs || !Standalone(theirs) || Fired("clone", 12) || IndustryCore.State.Clashes.Any((ReleaseClash k) => k.GameId == g.myID && k.Kind == "copy"))
				{
					return false;
				}
				c.StudioId = a.myID;
				c.GameId = g.myID;
				c.Game2 = theirs.myID;
				c.Amount = OfRevenue(g, 0.01f, Scaled(80000.0));
				c.Amount2 = OfRevenue(g, 0.015f, Scaled(40000.0));
				return true;
			},
			Title = (IndustryContext c) => "Copycat",
			Text = (IndustryContext c) => StudioName(c.StudioId) + " announced " + Name(c.Game2) + ": " + AOrAn(GenreGame(Game(c.GameId))) + " that looks a lot like " + Name(c.GameId) + ", same topic and all. It's out in a couple of months, and players are calling it a cheap clone.",
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Sue Them", "Costs " + Money(c.Amount), "About 50% to stop it: they'd have to start again", delegate(IndustryContext x)
				{
					G.MS.Pay(x.Amount, 13);
					publisherScript a = IndustryCore.Studio(x.StudioId);
					if (Random.value < 0.5f)
					{
						IndustryCore.AddStars(a, -3f);
						IndustryCore.State.RivalWins++;
						if ((bool)a)
						{
							a.newGameInWeeks += 12;
						}
						IndustryCore.Change(a, -10, "You sued them over a copy of " + Name(x.GameId) + " and won");
						return "The court sided with you. " + StudioName(x.StudioId) + " has to rework " + Name(x.Game2) + " into something else, and it's months late.";
					}
					IndustryCore.State.RivalLosses++;
					IndustryCore.Change(a, -6, "You sued them over a copy of " + Name(x.GameId) + " and lost");
					string lost = MakeCopy(x, 0);
					return "You lost the case. " + lost;
				}, (IndustryContext x) => Need(x.Amount)),
				Choice("Out-Market Them", "Costs " + Money(c.Amount2), "Hype +15, and an 8-point edge when the reviews compare them", delegate(IndustryContext x)
				{
					G.MS.Pay(x.Amount2, 12);
					Game(x.GameId)?.AddHype(15f);
					return "Your campaign reminds everyone who did it first. " + MakeCopy(x, 8);
				}, (IndustryContext x) => Need(x.Amount2)),
				Choice("Ignore It", "Nothing to pay", "The reviews will decide which one players want", (IndustryContext x) => "You let your game speak for itself. " + MakeCopy(x, 0))
			}
		};
	}

	// Their next game becomes a copy of your hit (genre and topic) and comes out within 4-10 weeks.
	private static string MakeCopy(IndustryContext c, int edge)
	{
		gameScript mine = Game(c.GameId);
		gameScript theirs = Game(c.Game2);
		publisherScript a = IndustryCore.Studio(c.StudioId);
		if (!mine || !theirs || !a || !theirs.inDevelopment || theirs.isOnMarket)
		{
			return "Their copy never came out.";
		}
		theirs.maingenre = mine.maingenre;
		theirs.subgenre = mine.subgenre;
		theirs.gameMainTheme = mine.gameMainTheme;
		theirs.gameSubTheme = mine.gameSubTheme;
		theirs.angekuendigt = true;
		a.newGameInWeeks = Mathf.Clamp(a.newGameInWeeks, 4, 10);
		a.newGameInWeeksORG = Mathf.Max(a.newGameInWeeksORG, a.newGameInWeeks);
		IndustryCompetitors.AddClash(mine, theirs, a, edge, "copy");
		return theirs.GetNameSimple() + " comes out in about " + a.newGameInWeeks + " weeks, and the reviews will compare them.";
	}

	// It moves its next big game onto your release week.
	private static IndustryEventDef RivalClash()
	{
		return new IndustryEventDef
		{
			Id = "rv_clash",
			Scripted = true,
			Setup = delegate(IndustryContext c)
			{
				publisherScript a = IndustryRivals.Arch;
				gameScript g = IndustryCore.AllGames().FirstOrDefault((gameScript x) => x.IsMyGame() && x.isOnMarket && x.releaseDate >= 2 && x.releaseDate <= 6 && Standalone(x) && !IndustryCompetitors.Clashing(x));
				gameScript theirs = TheirGame(a);
				// Needs their real next game; not in the same weeks as a competitor's release clash.
				if (!a || !g || !theirs || !Standalone(theirs) || Fired("cp_clash", 8))
				{
					return false;
				}
				c.StudioId = a.myID;
				c.GameId = g.myID;
				c.Game2 = theirs.myID;
				c.Amount = MarketingCost(g);
				return true;
			},
			Title = (IndustryContext c) => "Release Clash",
			Text = (IndustryContext c) => StudioName(c.StudioId) + " has moved " + Name(c.Game2) + " to the same week as " + Name(c.GameId) + ", on purpose. Most players will only buy one of them.",
			// Left alone, you hold your date: ignoring a pop-up shouldn't delay your game.
			Default = 1,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Delay Ours 3 Weeks", "Avoid the fight", Name(c.GameId) + " comes out 3 weeks later; theirs gets the week", delegate(IndustryContext x)
				{
					gameScript g = Game(x.GameId);
					if (!g || g.releaseDate <= 0)
					{
						return "Too late: it's already out.";
					}
					IndustryCompetitors.SetForYourWeek(IndustryCore.Studio(x.StudioId), g.releaseDate);
					g.releaseDate += 3;
					return "You moved " + Name(x.GameId) + " three weeks back. " + Name(x.Game2) + " has the week to itself.";
				}),
				Choice("Hold the Date", "The better game wins", "Once both are out: the better-reviewed one sells 10% more for 6 weeks, the other 10% less", (IndustryContext x) => RivalHold(x, 0)),
				Choice("Out-Market Them", "Costs " + Money(c.Amount), "Hype +12, and an 8-point edge when the reviews decide", delegate(IndustryContext x)
				{
					G.MS.Pay(x.Amount, 12);
					Game(x.GameId)?.AddHype(12f);
					return RivalHold(x, 8);
				}, (IndustryContext x) => Need(x.Amount))
			}
		};
	}

	// Both come out the same week: their real game is moved onto your release date, and the reviews decide.
	private static string RivalHold(IndustryContext c, int edge)
	{
		gameScript g = Game(c.GameId);
		gameScript theirs = Game(c.Game2);
		publisherScript a = IndustryCore.Studio(c.StudioId);
		if (!g || !theirs || !a || g.releaseDate <= 0)
		{
			return "The clash fizzled out.";
		}
		IndustryCompetitors.SetForYourWeek(a, g.releaseDate);
		IndustryCompetitors.AddClash(g, theirs, a, edge);
		return "You hold the date. " + Name(c.GameId) + " and " + Name(c.Game2) + " come out the same week, and the reviews will decide.";
	}

	// It mocks you in an interview.
	private static IndustryEventDef RivalTaunt()
	{
		return new IndustryEventDef
		{
			Id = "rv_taunt",
			Scripted = true,
			Setup = delegate(IndustryContext c)
			{
				publisherScript a = IndustryRivals.Arch;
				if (!a)
				{
					return false;
				}
				c.StudioId = a.myID;
				return true;
			},
			Title = (IndustryContext c) => "Trash Talk",
			Text = (IndustryContext c) => "The head of " + StudioName(c.StudioId) + " mocked your studio in an interview: \"They make games for people who can't afford good ones.\"",
			Default = 1,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Fire Back With a Joke", "New fans love a good roast", "Standing with them -8", delegate(IndustryContext x)
				{
					int fans = IndustryRivals.Fans(0.005f);
					IndustryCore.Change(IndustryCore.Studio(x.StudioId), -8, "You roasted them back in public");
					return "Your reply went viral: " + fans + " new fans are loving the feud.";
				}),
				Choice("Stay Classy", "Studio points +6", "Standing with them +4", delegate(IndustryContext x)
				{
					G.MS.AddStudioPoints(6);
					IndustryCore.Change(IndustryCore.Studio(x.StudioId), 4, "You didn't rise to their trash talk");
					return "The press praised your calm response.";
				})
			}
		};
	}
}
