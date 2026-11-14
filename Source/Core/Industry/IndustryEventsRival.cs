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
						p.gehalt = (int)((float)p.GetGehalt() * 1.25f);
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

	// It announces a copy of your hit.
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
					where x.weeksOnMarket < 40 && !x.typ_addon && !x.typ_mmoaddon && x.reviewTotal >= 70
					orderby Weekly(x) descending
					select x).FirstOrDefault();
				if (!a || !g)
				{
					return false;
				}
				c.StudioId = a.myID;
				c.GameId = g.myID;
				c.Amount = Scaled(80000.0);
				c.Amount2 = Scaled(40000.0);
				return true;
			},
			Title = (IndustryContext c) => "Copycat",
			Text = (IndustryContext c) => StudioName(c.StudioId) + " announced a game that looks a lot like " + Name(c.GameId) + ". Players are calling it a cheap clone.",
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Sue Them", "Costs " + Money(c.Amount), "About 50% to stop the clone", delegate(IndustryContext x)
				{
					G.MS.Pay(x.Amount, 13);
					publisherScript a = IndustryCore.Studio(x.StudioId);
					if (Random.value < 0.5f)
					{
						IndustryCore.AddStars(a, -3f);
						IndustryCore.State.RivalWins++;
						IndustryCore.Change(a, -10, "You sued them over a copy of " + Name(x.GameId) + " and won");
						return "The court sided with you. The clone was cancelled.";
					}
					IndustryCore.AddMod(Game(x.GameId), -0.15f, 12);
					IndustryCore.State.RivalLosses++;
					IndustryCore.Change(a, -6, "You sued them over a copy of " + Name(x.GameId) + " and lost");
					return "You lost the case. The clone is coming anyway: " + Name(x.GameId) + " sells 15% less for 12 weeks.";
				}, (IndustryContext x) => Need(x.Amount)),
				Choice("Out-Market Them", "Costs " + Money(c.Amount2), "Hype +15; the clone takes less (6 weeks)", delegate(IndustryContext x)
				{
					G.MS.Pay(x.Amount2, 12);
					Game(x.GameId)?.AddHype(15f);
					IndustryCore.AddMod(Game(x.GameId), -0.15f, 6);
					return "Your campaign reminded everyone who did it first.";
				}, (IndustryContext x) => Need(x.Amount2)),
				Choice("Ignore It", "Nothing to pay", Name(c.GameId) + " sells 15% less for 12 weeks", delegate(IndustryContext x)
				{
					IndustryCore.AddMod(Game(x.GameId), -0.15f, 12);
					return "The clone launched and took some of your players.";
				})
			}
		};
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
				gameScript g = IndustryCore.AllGames().FirstOrDefault((gameScript x) => x.IsMyGame() && x.isOnMarket && x.releaseDate > 0 && x.releaseDate <= 6);
				if (!a || !g)
				{
					return false;
				}
				gameScript theirs = a.FindGameInDevelopment();
				c.StudioId = a.myID;
				c.GameId = g.myID;
				c.Game2 = (theirs ? theirs.myID : (-1));
				c.Number = Random.Range(55, 92);
				return true;
			},
			Title = (IndustryContext c) => "Release Clash",
			Text = (IndustryContext c) => StudioName(c.StudioId) + " moved " + ((c.Game2 >= 0) ? Name(c.Game2) : "its next big game") + " to the same week as " + Name(c.GameId) + ".",
			Default = 0,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Delay Ours 3 Weeks", "Avoid the fight", Name(c.GameId) + " comes out 3 weeks later", delegate(IndustryContext x)
				{
					gameScript g = Game(x.GameId);
					if ((bool)g && g.releaseDate > 0)
					{
						g.releaseDate += 3;
					}
					return "You moved " + Name(x.GameId) + " three weeks back.";
				}),
				Choice("Hold the Date", "The better game wins", "Launch sales +12% if yours reviews better, -15% if not", delegate(IndustryContext x)
				{
					gameScript g = Game(x.GameId);
					if (!g)
					{
						return "The game is gone.";
					}
					int weeks = Mathf.Max(1, g.releaseDate) + 3;
					if (g.reviewTotal >= x.Number)
					{
						IndustryCore.AddMod(g, 0.12f, weeks);
						IndustryCore.State.RivalWins++;
						return "Your game reviewed better (" + g.reviewTotal + "% against " + x.Number + "%). Players picked yours: launch sales +12%.";
					}
					IndustryCore.AddMod(g, -0.15f, weeks);
					IndustryCore.State.RivalLosses++;
					return "Their game reviewed better (" + x.Number + "% against " + g.reviewTotal + "%). Launch sales -15%.";
				})
			}
		};
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
