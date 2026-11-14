using System.Collections.Generic;
using System.Linq;
using HolyAddon.Core.Updates;
using UnityEngine;

namespace HolyAddon.Core.Industry;

// The 1985-1994 pack: the console and cartridge years. Licences, cartoons and hint lines for your hits, magazines and
// parents with opinions, rivals fighting for shelf space, friends who swap know-how. Some run on into the late 1990s.
// All names (films, TV studios, magazines, shops, rental chains, campaign groups) are made up.
internal static partial class IndustryEvents
{
	private static readonly string[] ConsoleMags = new string[5] { "Console Kids", "Cartridge Quarterly", "Two Player", "Pause Screen", "Button Basher" };

	private static readonly string[] FilmStudios = new string[4] { "Grand Marquee Pictures", "Silver Reel Studios", "Starlit Avenue Films", "Popcorn Hill Pictures" };

	private static readonly string[] Films = new string[8] { "Starfall Rangers", "Turbo Cop", "The Last Comet", "Dino Detectives", "Moon Base Panic", "Ghost Train Express", "Volcano Kids", "Skyline Heist" };

	private static readonly string[] TvStudios = new string[3] { "Brightside Animation", "Saturday Sky Cartoons", "Paperplane TV" };

	private static readonly string[] RentalChains = new string[4] { "Friday Night Video", "Corner Reel Rentals", "Rewind & Play", "Hometown Video" };

	private static readonly string[] GuidePublishers = new string[3] { "Dog-Ear Books", "Clever Fox Press", "Thumbtack Publishing" };

	private static readonly string[] ParentGroups = new string[3] { "Parents for Gentle Play", "the Family Screen Council", "the Wholesome Media League" };

	private static readonly string[] Shops = new string[3] { "Hi-Score Stores", "The Cartridge Cave", "Pocket Money Games" };

	// By profession: 0 game designer, 1 programmer, 2 graphics artist.
	private static readonly string[] StarRoles = new string[3] { "star game designer", "lead programmer", "best graphics artist" };

	private static IEnumerable<IndustryEventDef> Consoles()
	{
		yield return Cartridge();
		yield return Film();
		yield return Cartoon();
		yield return LowScore();
		yield return Poach();
		yield return Rental();
		yield return HintLine();
		yield return ExclusiveOffer();
		yield return Parents();
		yield return Swap();
		yield return ShelfSpace();
		yield return Launch();
		yield return ConsoleWar();
		yield return Reveal();
		yield return Budget();
		yield return NewShops();
	}

	// ---- Helpers ----

	private static float WeeklyRevenue(gameScript g)
	{
		if (g.sellsTotal <= 0)
		{
			return 0f;
		}
		return (float)Weekly(g) * ((float)g.umsatzTotal / (float)g.sellsTotal);
	}

	// Out on a console or handheld (cartridges, in these years).
	private static bool OnConsole(gameScript g)
	{
		if (g.gamePlatform == null)
		{
			return false;
		}
		foreach (int id in g.gamePlatform)
		{
			if (id >= 0)
			{
				platformScript p = GameUpdates.Platform(id);
				if ((bool)p && (p.typ == 1 || p.typ == 2))
				{
					return true;
				}
			}
		}
		return false;
	}

	private static int LaunchWeek(platformScript p)
	{
		return p.date_year * 48 + (p.date_month - 1) * 4;
	}

	// A strong applicant in the job market (profession 0 game designer, 1 programmer, 2 graphics artist).
	private static bool Applicant(int profession, float min, float max)
	{
		arbeitsmarkt market = Object.FindObjectOfType<arbeitsmarkt>();
		if (!market)
		{
			return false;
		}
		charArbeitsmarkt c = market.CreateArbeitsmarktItem();
		if (!c)
		{
			return false;
		}
		c.Create(null);
		c.beruf = profession;
		float v = R(min, max);
		switch (profession)
		{
		case 0:
			c.s_gamedesign = v;
			break;
		case 1:
			c.s_programmieren = v;
			break;
		default:
			c.s_grafik = v;
			break;
		}
		return true;
	}

	// Up to "count" of your designers, programmers, artists and sound people get better at their job.
	private static List<string> Train(int count, float min, float max)
	{
		List<characterScript> picked = (from e in Employees.All()
			where e.beruf >= 0 && e.beruf <= 3 && Employees.Skill(e, e.beruf) < Employees.NaturalCap(e, e.beruf)
			orderby R(0f, 1f)
			select e).Take(count).ToList();
		foreach (characterScript e in picked)
		{
			Employees.SetSkill(e, e.beruf, Mathf.Min(Employees.NaturalCap(e, e.beruf), Employees.Skill(e, e.beruf) + R(min, max)));
		}
		return picked.Select((characterScript e) => e.myName).ToList();
	}

	private static int Trainable()
	{
		return Employees.All().Count((characterScript e) => e.beruf >= 0 && e.beruf <= 3 && Employees.Skill(e, e.beruf) < Employees.NaturalCap(e, e.beruf));
	}

	// ---- Decisions ----

	// Cartridge chips run short: pay the factory, run out in the shops, or share your chips with a studio that has none.
	private static IndustryEventDef Cartridge()
	{
		return new IndustryEventDef
		{
			Id = "cartridge",
			From = 1985,
			To = 1996,
			Cooldown = 52,
			Setup = delegate(IndustryContext c)
			{
				gameScript g = (from x in IndustryCore.MyGamesOnMarket()
					where x.weeksOnMarket <= 52 && Weekly(x) > 0 && x.sellsTotal > 0 && OnConsole(x)
					orderby Weekly(x) descending
					select x).FirstOrDefault();
				if (!g)
				{
					return false;
				}
				publisherScript friend = (from p in IndustryCore.Studios()
					where p.developer && IndustryCore.Tier(IndustryCore.Standing(p)) > 0 && (bool)p.FindGameInDevelopment()
					orderby R(0f, 1f)
					select p).FirstOrDefault();
				if (!friend)
				{
					return false;
				}
				c.GameId = g.myID;
				c.StudioId = friend.myID;
				c.Game2 = friend.FindGameInDevelopment().myID;
				c.Amount = (long)Mathf.Clamp(WeeklyRevenue(g) * 1.5f, (float)Scaled(2000.0), (float)Scaled(60000.0));
				return true;
			},
			Title = (IndustryContext c) => "A Cartridge Shortage",
			Text = (IndustryContext c) => "The chip makers can't keep up, and cartridges are in short supply. " + Name(c.GameId) + " is running out in the shops: the factory will put you first, at a price. Meanwhile " + StudioName(c.StudioId) + " has no chips at all for " + Name(c.Game2) + ".",
			Default = 1,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Pay the Premium", "Costs " + Money(c.Amount), "Your shelves stay full", delegate(IndustryContext x)
				{
					G.MS.Pay(x.Amount, 21);
					return "You pay the premium, and " + Name(x.GameId) + " stays in the shops.";
				}, (IndustryContext x) => Need(x.Amount)),
				Choice("Ship What You Can", "Nothing to pay", Name(c.GameId) + " sells 20% less for 8 weeks", delegate(IndustryContext x)
				{
					IndustryCore.AddMod(Game(x.GameId), -0.2f, 8);
					return "Shops sell out of " + Name(x.GameId) + " and wait for more.";
				}),
				Choice("Share Your Chips", "Your sales -30% for 8 weeks", "Standing with " + StudioName(c.StudioId) + " +15", delegate(IndustryContext x)
				{
					IndustryCore.AddMod(Game(x.GameId), -0.3f, 8);
					Game(x.Game2)?.AddHype(3f);
					IndustryCore.Change(IndustryCore.Studio(x.StudioId), 15, "You shared your cartridge chips");
					return StudioName(x.StudioId) + " gets enough chips to launch " + Name(x.Game2) + ", and won't forget who helped.";
				})
			}
		};
	}

	// A film studio offers its summer film's licence for your game in development.
	private static IndustryEventDef Film()
	{
		return new IndustryEventDef
		{
			Id = "film",
			From = 1985,
			To = 2005,
			Cooldown = 60,
			Setup = delegate(IndustryContext c)
			{
				gameScript g = (from x in IndustryCore.MyGamesInDevelopment()
					where x.gameTyp == 0 && DevProgress(x) <= 0.5f && !IndustryCore.State.Cooldowns.ContainsKey("film:" + x.myID)
					orderby R(0f, 1f)
					select x).FirstOrDefault();
				if (!g)
				{
					return false;
				}
				c.GameId = g.myID;
				c.Number = Random.Range(0, Films.Length);
				c.Number2 = Random.Range(0, FilmStudios.Length);
				c.Amount = (long)((float)Scaled(12000.0) * (1f + (float)g.gameSize * 0.5f));
				c.Share = 0.15f;
				IndustryCore.State.Cooldowns["film:" + g.myID] = Now;
				return true;
			},
			Title = (IndustryContext c) => "The Game of the Film",
			Text = (IndustryContext c) => FilmStudios[c.Number2] + "'s big summer film, " + Films[c.Number] + ", opens soon. It offers you the licence: " + Name(c.GameId) + " would carry the film's name and heroes on the box. Films can flop, though.",
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Buy the Licence", "Costs " + Money(c.Amount), "If the film's a hit: hype +12, sales +25% for 12 weeks after release", delegate(IndustryContext x)
				{
					G.MS.Pay(x.Amount, 7);
					gameScript g2 = Game(x.GameId);
					if (Random.value < 0.6f)
					{
						g2?.AddHype(12f);
						IndustryCore.AddModAfterRelease(g2, 0.25f, 12);
						return Films[x.Number] + " is the hit of the summer. " + Name(x.GameId) + " will ride its wave: sales +25% for 12 weeks after release.";
					}
					g2?.AddHype(3f);
					IndustryCore.AddModAfterRelease(g2, 0.05f, 12);
					return Films[x.Number] + " flops at the box office. The licence still helps a little: sales +5% for 12 weeks after release.";
				}, (IndustryContext x) => Need(x.Amount)),
				Choice("Share the Revenue", "No fee; 15% of its revenue", "For 26 weeks after release; hype +8, sales +15% for 8 weeks", delegate(IndustryContext x)
				{
					gameScript g2 = Game(x.GameId);
					if (!g2)
					{
						return "The game is gone.";
					}
					g2.AddHype(8f);
					IndustryCore.AddModAfterRelease(g2, 0.15f, 8);
					IndustryCore.State.Streams.Add(new IncomeStream
					{
						Label = FilmStudios[x.Number2] + "'s share of " + g2.GetNameSimple(),
						GameId = g2.myID,
						Share = x.Share,
						WeeksLeft = 26,
						Out = true
					});
					return FilmStudios[x.Number2] + " puts its marketing behind " + g2.GetNameSimple() + ", for 15% of its revenue for 26 weeks after release.";
				}),
				Choice("No Thanks", "Nothing changes", "The film finds another studio", (IndustryContext x) => "You turn the licence down.")
			}
		};
	}

	// A TV studio wants a Saturday-morning cartoon of your hit.
	private static IndustryEventDef Cartoon()
	{
		return new IndustryEventDef
		{
			Id = "cartoon",
			From = 1985,
			To = 1996,
			Cooldown = 70,
			Setup = delegate(IndustryContext c)
			{
				gameScript g = (from x in IndustryCore.MyGamesOnMarket()
					where x.gameTyp == 0 && x.reviewTotal >= 70 && x.weeksOnMarket <= 156 && !IndustryCore.State.Cooldowns.ContainsKey("cartoon:" + x.myID)
					orderby x.reviewTotal descending
					select x).FirstOrDefault();
				if (!g)
				{
					return false;
				}
				c.GameId = g.myID;
				c.Number = Random.Range(0, TvStudios.Length);
				c.Amount = (long)((float)Scaled(10000.0) * (1f + (float)g.reviewTotal / 100f) * (1f + (float)g.gameSize * 0.3f));
				IndustryCore.State.Cooldowns["cartoon:" + g.myID] = Now;
				return true;
			},
			Title = (IndustryContext c) => "A Cartoon of " + Name(c.GameId),
			Text = (IndustryContext c) => TvStudios[c.Number] + " wants to make a Saturday-morning cartoon of " + Name(c.GameId) + ". Sell them the rights, or put your own money in and share in the toys and lunchboxes.",
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Sell the Rights", Money(c.Amount) + " now", "IP points +25, about 500 new fans", delegate(IndustryContext x)
				{
					G.MS.Earn(x.Amount, 2);
					Game(x.GameId)?.AddIpPoints(25f);
					int fans = AddFans(500f * R(0.8f, 1.3f));
					return TvStudios[x.Number] + " pays " + Money(x.Amount) + ". The cartoon airs on Saturday mornings: " + fans + " new fans.";
				}),
				Choice("Co-Produce It", "Costs " + Money(c.Amount), "A gamble: if it runs, merchandise, fans and sales", delegate(IndustryContext x)
				{
					G.MS.Pay(x.Amount, 13);
					gameScript g2 = Game(x.GameId);
					if (Random.value < 0.7f)
					{
						g2?.AddIpPoints(50f);
						int fans = AddFans(1500f * R(0.8f, 1.3f));
						IndustryCore.AddMod(g2, 0.15f, 12);
						IndustryCore.State.Streams.Add(new IncomeStream
						{
							Label = Name(x.GameId) + " merchandise",
							Fixed = x.Amount / 6,
							WeeksLeft = 16,
							Category = 11
						});
						return "The cartoon is a hit: " + fans + " new fans, sales +15% for 12 weeks, and about " + Money(x.Amount / 6) + " a week from merchandise for 16 weeks.";
					}
					g2?.AddIpPoints(15f);
					int few = AddFans(300f * R(0.8f, 1.3f));
					return "The cartoon is cancelled after one season. Still, " + few + " new fans.";
				}, (IndustryContext x) => Need(x.Amount)),
				Choice("Decline", "Nothing changes", "It stays a game", (IndustryContext x) => "You turn " + TvStudios[x.Number] + " down.")
			}
		};
	}

	// A magazine scores your new game far below everyone else.
	private static IndustryEventDef LowScore()
	{
		return new IndustryEventDef
		{
			Id = "lowscore",
			From = 1985,
			To = 2002,
			Cooldown = 40,
			Setup = delegate(IndustryContext c)
			{
				gameScript g = (from x in IndustryCore.MyGamesOnMarket()
					where x.weeksOnMarket >= 1 && x.weeksOnMarket <= 4 && x.reviewTotal >= 45 && x.reviewTotal <= 90 && !IndustryCore.State.Cooldowns.ContainsKey("lowscore:" + x.myID)
					orderby x.reviewTotal descending
					select x).FirstOrDefault();
				if (!g)
				{
					return false;
				}
				c.GameId = g.myID;
				c.Number = Random.Range(0, ConsoleMags.Length);
				c.Number2 = Mathf.Max(10, g.reviewTotal - Random.Range(22, 33));
				c.Amount = Scaled(6000.0);
				IndustryCore.State.Cooldowns["lowscore:" + g.myID] = Now;
				return true;
			},
			Title = (IndustryContext c) => "A Harsh Review",
			Text = (IndustryContext c) => ConsoleMags[c.Number] + " gives " + Name(c.GameId) + " just " + c.Number2 + "%, far below every other review, and calls it \"a waste of pocket money\". Players are writing in to argue about it.",
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Write to the Editor", "Free, but a gamble", "A correction (sales +8% for 6 weeks), or readers side with them", delegate(IndustryContext x)
				{
					if (Random.value < 0.5f)
					{
						IndustryCore.AddMod(Game(x.GameId), 0.08f, 6);
						int fans = AddFans(150f * R(0.8f, 1.3f));
						return ConsoleMags[x.Number] + " prints your letter and admits the review was rushed: " + fans + " new fans, and sales +8% for 6 weeks.";
					}
					IndustryCore.AddMod(Game(x.GameId), -0.05f, 6);
					return "Readers side with the magazine. The row does " + Name(x.GameId) + " no favours: sales -5% for 6 weeks.";
				}),
				Choice("Buy a Double-Page Advert", "Costs " + Money(c.Amount), "Sales +10% for 8 weeks: let the game speak", delegate(IndustryContext x)
				{
					G.MS.Pay(x.Amount, 12);
					IndustryCore.AddMod(Game(x.GameId), 0.1f, 8);
					return "Your advert runs two issues later, with quotes from every other review.";
				}, (IndustryContext x) => Need(x.Amount)),
				Choice("Let It Go", "Nothing to pay", "Sales -3% for 4 weeks", delegate(IndustryContext x)
				{
					IndustryCore.AddMod(Game(x.GameId), -0.03f, 4);
					return "You let the other reviews speak for themselves.";
				})
			}
		};
	}

	// A star at another studio wants to join you: hire them, tell their boss, or stay out of it.
	private static IndustryEventDef Poach()
	{
		return new IndustryEventDef
		{
			Id = "poach",
			From = 1985,
			Cooldown = 40,
			Setup = delegate(IndustryContext c)
			{
				List<publisherScript> pool = IndustryCore.Studios().Where((publisherScript p) => p.developer && p.stars >= 40f && IndustryCore.Tier(IndustryCore.Standing(p)) < 3).ToList();
				if (pool.Count == 0)
				{
					return false;
				}
				c.StudioId = Pick(pool).myID;
				c.Number = Random.Range(0, StarRoles.Length);
				return true;
			},
			Title = (IndustryContext c) => "A Star Wants to Join You",
			Text = (IndustryContext c) => "The " + StarRoles[c.Number] + " of " + StudioName(c.StudioId) + " is unhappy there, and asks quietly whether you'd take them on. They're one of the best in the business, and " + StudioName(c.StudioId) + " would be furious.",
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Hire Them", "They join the job market", "Their studio loses a little; standing with them -15", delegate(IndustryContext x)
				{
					if (!Applicant(x.Number, 58f, 68f))
					{
						return "They change their mind at the last minute.";
					}
					publisherScript p = IndustryCore.Studio(x.StudioId);
					IndustryCore.AddStars(p, -2f);
					IndustryCore.Change(p, -15, "You hired their " + StarRoles[x.Number]);
					return "They hand in their notice at " + StudioName(x.StudioId) + ". Find them in the job market (Staff > hire).";
				}),
				Choice("Tell Their Boss", "Nothing to pay", "Standing with " + StudioName(c.StudioId) + " +10", delegate(IndustryContext x)
				{
					IndustryCore.Change(IndustryCore.Studio(x.StudioId), 10, "You told them their " + StarRoles[x.Number] + " wanted to leave");
					return StudioName(x.StudioId) + " gives its " + StarRoles[x.Number] + " a pay rise, and thanks you for the warning.";
				}),
				Choice("Stay Out of It", "Nothing changes", "They stay where they are", (IndustryContext x) => "You wish them well, and stay out of it.")
			}
		};
	}

	// A video rental chain wants your game on its shelves.
	private static IndustryEventDef Rental()
	{
		return new IndustryEventDef
		{
			Id = "rental",
			From = 1986,
			To = 1999,
			Cooldown = 60,
			Setup = delegate(IndustryContext c)
			{
				gameScript g = (from x in IndustryCore.MyGamesOnMarket()
					where x.gameTyp == 0 && x.weeksOnMarket >= 8 && Weekly(x) > 0 && x.sellsTotal > 0
					orderby Weekly(x) descending
					select x).FirstOrDefault();
				if (!g)
				{
					return false;
				}
				c.GameId = g.myID;
				c.Number = Random.Range(0, RentalChains.Length);
				c.Amount = (long)Mathf.Clamp(WeeklyRevenue(g) * 0.25f, (float)Scaled(300.0), (float)Scaled(20000.0));
				return true;
			},
			Title = (IndustryContext c) => "Rent It Out?",
			Text = (IndustryContext c) => RentalChains[c.Number] + " wants " + Name(c.GameId) + " on its shelves next to the films, for players to rent for the weekend. It pays a weekly fee, but some players will rent it instead of buying.",
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Sign the Deal", Money(c.Amount) + " a week for 16 weeks", Name(c.GameId) + " sells 10% less for 16 weeks; about 300 new fans", (IndustryContext x) => Rent(x, x.Amount)),
				Choice("Ask for More", "A gamble", "Half as much again, or they walk away", delegate(IndustryContext x)
				{
					if (Random.value < 0.5f)
					{
						return "They agree. " + Rent(x, x.Amount * 3 / 2);
					}
					return RentalChains[x.Number] + " walks away and signs another studio's game instead.";
				}),
				Choice("Say No", "Nothing changes", "Players will have to buy it", (IndustryContext x) => "You keep " + Name(x.GameId) + " out of the rental shops.")
			}
		};
	}

	private static string Rent(IndustryContext c, long weekly)
	{
		IndustryCore.State.Streams.Add(new IncomeStream
		{
			Label = "Rentals of " + Name(c.GameId),
			Fixed = weekly,
			WeeksLeft = 16,
			Category = 2
		});
		IndustryCore.AddMod(Game(c.GameId), -0.1f, 16);
		int fans = AddFans(300f * R(0.8f, 1.3f));
		return Name(c.GameId) + " goes on the rental shelves: " + Money(weekly) + " a week for 16 weeks, and " + fans + " new fans.";
	}

	// Players are stuck: a strategy guide, a paid hint line, or free tips.
	private static IndustryEventDef HintLine()
	{
		return new IndustryEventDef
		{
			Id = "hintline",
			From = 1986,
			To = 1996,
			Cooldown = 50,
			Setup = delegate(IndustryContext c)
			{
				gameScript g = (from x in IndustryCore.MyGamesOnMarket()
					where x.gameTyp == 0 && x.weeksOnMarket <= 104 && x.reviewTotal >= 50 && Weekly(x) > 0 && !IndustryCore.State.Cooldowns.ContainsKey("hint:" + x.myID)
					orderby Weekly(x) descending
					select x).FirstOrDefault();
				if (!g)
				{
					return false;
				}
				c.GameId = g.myID;
				c.Number = Random.Range(0, GuidePublishers.Length);
				c.Amount = (long)((float)Scaled(4000.0) * (1f + (float)g.gameSize * 0.4f) * (1f + (float)g.reviewTotal / 100f));
				IndustryCore.State.Cooldowns["hint:" + g.myID] = Now;
				return true;
			},
			Title = (IndustryContext c) => "Stuck on " + Name(c.GameId),
			Text = (IndustryContext c) => "Players are stuck on " + Name(c.GameId) + ", and the letters keep coming. " + GuidePublishers[c.Number] + " wants to print the official strategy guide. Or you could run a phone line where players pay by the minute for tips.",
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Sell the Guide Rights", Money(c.Amount) + " now", "IP points +10", delegate(IndustryContext x)
				{
					G.MS.Earn(x.Amount, 2);
					Game(x.GameId)?.AddIpPoints(10f);
					return GuidePublishers[x.Number] + " pays " + Money(x.Amount) + ". The guide sells out at Christmas.";
				}),
				Choice("Run a Hint Line", "About " + Money(c.Amount / 6) + " a week for 12 weeks", "Twice the fee, but parents may complain about phone bills", delegate(IndustryContext x)
				{
					IndustryCore.State.Streams.Add(new IncomeStream
					{
						Label = Name(x.GameId) + " hint line",
						Fixed = x.Amount / 6,
						WeeksLeft = 12,
						Category = 1
					});
					if (Random.value < 0.3f)
					{
						IndustryCore.AddMod(Game(x.GameId), -0.06f, 8);
						return "The phones ring all day, but parents complain about their bills: sales -6% for 8 weeks.";
					}
					return "The phones ring all day: about " + Money(x.Amount / 6) + " a week for 12 weeks.";
				}),
				Choice("Print Free Tips", "Free: about 300 new fans", Name(c.GameId) + " sells 5% more for 8 weeks", delegate(IndustryContext x)
				{
					int fans = AddFans(300f * R(0.8f, 1.3f));
					IndustryCore.AddMod(Game(x.GameId), 0.05f, 8);
					return "Your tips run in every magazine: " + fans + " new fans.";
				})
			}
		};
	}

	// A console maker wants your game in development on its machine first: a timed exclusive, with a bonus now.
	private static IndustryEventDef ExclusiveOffer()
	{
		return new IndustryEventDef
		{
			Id = "exclusive",
			From = 1985,
			Cooldown = 52,
			Setup = delegate(IndustryContext c)
			{
				if (!TimedExclusives.On)
				{
					return false;
				}
				foreach (gameScript g in from x in IndustryCore.MyGamesInDevelopment()
					where x.gameTyp == 0 && x.gamePlatform != null && DevProgress(x) <= 0.8f && TimedExclusives.For(x.myID) == null && !IndustryCore.State.Cooldowns.ContainsKey("exclusive:" + x.myID) && x.gamePlatform.Count((int id) => id >= 0) >= 2
					orderby x.hype descending
					select x)
				{
					platformScript pick = (from id in g.gamePlatform
						where id >= 0
						select GameUpdates.Platform(id) into p
						where (bool)p && p.OwnerIsNPC() && (p.typ == 1 || p.typ == 2) && !p.vomMarktGenommen
						orderby p.marktanteil
						select p).FirstOrDefault();
					if ((bool)pick)
					{
						c.GameId = g.myID;
						c.PlatformId = pick.myID;
						c.Amount = (long)((float)Scaled(8000.0) * (1f + (float)g.gameSize));
						IndustryCore.State.Cooldowns["exclusive:" + g.myID] = Now;
						return true;
					}
				}
				return false;
			},
			Title = (IndustryContext c) => "An Exclusive Offer",
			Text = delegate(IndustryContext c)
			{
				platformScript p2 = GameUpdates.Platform(c.PlatformId);
				string machine = (p2 ? p2.GetName() : "its machine");
				return (p2 ? p2.GetManufacturer() : "A console maker") + " wants " + Name(c.GameId) + " on the " + machine + " first. It offers a signing bonus now, on top of the deal it pays when a timed exclusive comes out. Your other platforms would get the game later.";
			},
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("First for 6 Months", Money(c.Amount) + " now", "A 6-month timed exclusive; hype +5", (IndustryContext x) => Exclusive(x, 6, x.Amount, 5f)),
				Choice("First for 12 Months", Money(c.Amount * 2) + " now", "A 12-month timed exclusive; hype +8", (IndustryContext x) => Exclusive(x, 12, x.Amount * 2, 8f)),
				Choice("Keep It on All Platforms", "Nothing changes", "It comes out everywhere at once", (IndustryContext x) => "You keep " + Name(x.GameId) + " on all its platforms.")
			}
		};
	}

	private static string Exclusive(IndustryContext c, int months, long bonus, float hype)
	{
		gameScript g = Game(c.GameId);
		platformScript p = GameUpdates.Platform(c.PlatformId);
		if (!g || !p || !g.inDevelopment)
		{
			return "Too late: the offer has gone.";
		}
		TimedExclusives.Register(g, p.myID, months);
		if (TimedExclusives.For(g.myID) == null)
		{
			return "The deal falls through: " + Name(c.GameId) + " is no longer on the " + p.GetName() + " and another platform.";
		}
		G.MS.Earn(bonus, 1);
		g.AddHype(hype);
		return p.GetManufacturer() + " pays " + Money(bonus) + ". " + Name(c.GameId) + " comes out on the " + p.GetName() + " first, and on its other platforms " + months + " months later.";
	}

	// A campaign group says your game is too violent.
	private static IndustryEventDef Parents()
	{
		return new IndustryEventDef
		{
			Id = "parents",
			From = 1990,
			To = 1999,
			Cooldown = 60,
			Setup = delegate(IndustryContext c)
			{
				gameScript g = (from x in IndustryCore.MyGamesOnMarket()
					where x.usk >= 4 && x.weeksOnMarket <= 26 && !IndustryCore.State.Cooldowns.ContainsKey("parents:" + x.myID)
					orderby Weekly(x) descending
					select x).FirstOrDefault();
				if (!g)
				{
					return false;
				}
				c.GameId = g.myID;
				c.Number = Random.Range(0, ParentGroups.Length);
				c.Amount = Scaled(2000.0);
				c.Amount2 = Scaled(6000.0);
				IndustryCore.State.Cooldowns["parents:" + g.myID] = Now;
				return true;
			},
			Title = (IndustryContext c) => "Concerned Parents",
			Text = (IndustryContext c) => "A campaign group, " + ParentGroups[c.Number] + ", says " + Name(c.GameId) + " is far too violent for children. It's on the evening news, and some shops are thinking of taking it off their shelves.",
			Default = 1,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Defend It on TV", "About 600 new fans", "Sales +12% for 8 weeks, unless shops pull it", delegate(IndustryContext x)
				{
					int fans = AddFans(600f * R(0.8f, 1.3f));
					if (Random.value < 0.75f)
					{
						IndustryCore.AddMod(Game(x.GameId), 0.12f, 8);
						return "The row makes " + Name(x.GameId) + " the game every teenager wants: " + fans + " new fans, and sales +12% for 8 weeks.";
					}
					IndustryCore.AddMod(Game(x.GameId), -0.15f, 8);
					return fans + " new fans, but two big chains take " + Name(x.GameId) + " off their shelves: sales -15% for 8 weeks.";
				}),
				Choice("Add a Warning Sticker", "Costs " + Money(c.Amount), "The fuss dies down", delegate(IndustryContext x)
				{
					G.MS.Pay(x.Amount, 13);
					return "A bright warning sticker goes on every box, and the news moves on.";
				}, (IndustryContext x) => Need(x.Amount)),
				Choice("Make a Milder Version", "Costs " + Money(c.Amount2), "Shops that wouldn't stock it now do: sales +6% for 12 weeks", delegate(IndustryContext x)
				{
					G.MS.Pay(x.Amount2, 4);
					IndustryCore.AddMod(Game(x.GameId), 0.06f, 12);
					return "A milder version goes out to the family shops.";
				}, (IndustryContext x) => Need(x.Amount2))
			}
		};
	}

	// A friendly studio suggests swapping staff for a month: everyone learns something.
	private static IndustryEventDef Swap()
	{
		return new IndustryEventDef
		{
			Id = "swap",
			From = 1985,
			Cooldown = 52,
			Setup = delegate(IndustryContext c)
			{
				List<publisherScript> friends = IndustryCore.Studios().Where((publisherScript p) => p.developer && IndustryCore.Tier(IndustryCore.Standing(p)) >= 2).ToList();
				if (friends.Count == 0 || Trainable() == 0)
				{
					return false;
				}
				c.StudioId = Pick(friends).myID;
				c.Amount = Scaled(3000.0);
				return true;
			},
			Title = (IndustryContext c) => "Swap Know-How?",
			Text = (IndustryContext c) => "Your friends at " + StudioName(c.StudioId) + " suggest a swap: for a month, some of your people work alongside theirs, and the other way round. Everyone learns something, though they'd learn your tricks too.",
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Swap for a Month", "Free: up to 3 staff improve", "Standing with them +8; their studio improves a little too", delegate(IndustryContext x)
				{
					List<string> names = Train(3, 3f, 5f);
					publisherScript p = IndustryCore.Studio(x.StudioId);
					IndustryCore.AddStars(p, 2f);
					IndustryCore.Change(p, 8, "You swapped staff for a month");
					return Learned(names) + " " + StudioName(x.StudioId) + " picked up a few of your tricks as well.";
				}),
				Choice("Pay for Lessons Only", "Costs " + Money(c.Amount), "Up to 3 staff improve; standing with them +3", delegate(IndustryContext x)
				{
					G.MS.Pay(x.Amount, 9);
					List<string> names = Train(3, 3f, 5f);
					IndustryCore.Change(IndustryCore.Studio(x.StudioId), 3, "You paid for their lessons");
					return Learned(names);
				}, (IndustryContext x) => Need(x.Amount)),
				Choice("Not Now", "Nothing changes", "Maybe another time", (IndustryContext x) => "You thank " + StudioName(x.StudioId) + " and say not now.")
			}
		};
	}

	private static string Learned(List<string> names)
	{
		if (names.Count == 0)
		{
			return "Nobody had anything left to learn.";
		}
		return string.Join(", ", names) + ((names.Count == 1) ? " comes" : " come") + " back better at the job.";
	}

	// A rival pays the shops to put its game in front of yours.
	private static IndustryEventDef ShelfSpace()
	{
		return new IndustryEventDef
		{
			Id = "shelf",
			From = 1985,
			To = 2005,
			Cooldown = 45,
			Setup = delegate(IndustryContext c)
			{
				gameScript g = (from x in IndustryCore.MyGamesOnMarket()
					where x.weeksOnMarket <= 26 && Weekly(x) > 0 && x.sellsTotal > 0
					orderby Weekly(x) descending
					select x).FirstOrDefault();
				if (!g)
				{
					return false;
				}
				List<gameScript> theirs = IndustryCore.AllGames().Where((gameScript x) => x.isOnMarket && !x.IsMyGame() && x.weeksOnMarket <= 12 && x.developerID >= 0).ToList();
				publisherScript rival = (from p in IndustryCore.Studios()
					where p.stars >= 30f && IndustryCore.Standing(p) <= 10f && theirs.Any((gameScript x) => x.developerID == p.myID)
					orderby IndustryCore.Standing(p) + R(0f, 20f)
					select p).FirstOrDefault();
				if (!rival)
				{
					return false;
				}
				c.GameId = g.myID;
				c.StudioId = rival.myID;
				c.Game2 = theirs.Where((gameScript x) => x.developerID == rival.myID).OrderByDescending(Weekly).First().myID;
				c.Amount = (long)Mathf.Clamp(WeeklyRevenue(g), (float)Scaled(3000.0), (float)Scaled(50000.0));
				return true;
			},
			Title = (IndustryContext c) => "Shelf Space",
			Text = (IndustryContext c) => StudioName(c.StudioId) + " is paying the big shops to put " + Name(c.Game2) + " at the front, and " + Name(c.GameId) + " has been moved to the back shelves.",
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Outbid Them", "Costs " + Money(c.Amount), Name(c.GameId) + " sells 8% more for 8 weeks", delegate(IndustryContext x)
				{
					G.MS.Pay(x.Amount, 12);
					IndustryCore.AddMod(Game(x.GameId), 0.08f, 8);
					IndustryCore.Change(IndustryCore.Studio(x.StudioId), -3, "You outbid them for shelf space");
					return Name(x.GameId) + " is back at the front of the shops.";
				}, (IndustryContext x) => Need(x.Amount)),
				Choice("Complain to the Trade Press", "Free, but a gamble", "Standing with them -10; the shops may back down", delegate(IndustryContext x)
				{
					IndustryCore.Change(IndustryCore.Studio(x.StudioId), -10, "You complained about them to the trade press");
					if (Random.value < 0.5f)
					{
						return "The story embarrasses the shops, and " + Name(x.GameId) + " goes back on the front shelves.";
					}
					IndustryCore.AddMod(Game(x.GameId), -0.08f, 8);
					return "Nobody listens: " + Name(x.GameId) + " sells 8% less for 8 weeks.";
				}),
				Choice("Let It Go", "Nothing to pay", Name(c.GameId) + " sells 8% less for 8 weeks", delegate(IndustryContext x)
				{
					IndustryCore.AddMod(Game(x.GameId), -0.08f, 8);
					return "You leave " + StudioName(x.StudioId) + " to it.";
				})
			}
		};
	}

	// ---- Headlines ----

	// A new console or handheld sells out at launch.
	private static IndustryEventDef Launch()
	{
		return new IndustryEventDef
		{
			Id = "launch",
			Headline = true,
			From = 1985,
			Cooldown = 26,
			Weight = 0.8f,
			Setup = delegate(IndustryContext c)
			{
				platformScript[] all = (G.MS ? G.MS.arrayPlatformsScripts : null);
				List<platformScript> pool = ((all == null) ? new List<platformScript>() : all.Where((platformScript p) => (bool)p && p.isUnlocked && !p.vomMarktGenommen && p.ownerID != G.MS.myID && (p.typ == 1 || p.typ == 2) && Now - LaunchWeek(p) >= 0 && Now - LaunchWeek(p) <= 8 && !IndustryCore.State.Cooldowns.ContainsKey("launch:" + p.myID)).ToList());
				if (pool.Count == 0)
				{
					return false;
				}
				c.PlatformId = Pick(pool).myID;
				return true;
			},
			Apply = delegate(IndustryContext c)
			{
				platformScript p2 = GameUpdates.Platform(c.PlatformId);
				if (!p2)
				{
					return null;
				}
				IndustryCore.State.Cooldowns["launch:" + p2.myID] = Now;
				p2.AddHype(8f);
				return "<b>" + p2.GetName() + "</b> sells out at launch: shops can't get enough.";
			}
		};
	}

	// The two leading consoles fight it out in adverts.
	private static IndustryEventDef ConsoleWar()
	{
		return new IndustryEventDef
		{
			Id = "consolewar",
			Headline = true,
			From = 1987,
			To = 2005,
			Cooldown = 52,
			Weight = 0.6f,
			Setup = delegate(IndustryContext c)
			{
				platformScript[] all = (G.MS ? G.MS.arrayPlatformsScripts : null);
				if (all == null)
				{
					return false;
				}
				List<platformScript> top = (from p in all
					where (bool)p && p.isUnlocked && !p.vomMarktGenommen && p.ownerID != G.MS.myID && p.typ == 1 && p.marktanteil > 0f && Now - LaunchWeek(p) <= 312
					orderby p.marktanteil descending
					select p).ToList();
				if (top.Count < 2)
				{
					return false;
				}
				platformScript second = top.Skip(1).FirstOrDefault((platformScript p) => p.GetManufacturer() != top[0].GetManufacturer());
				if (!second)
				{
					return false;
				}
				c.PlatformId = top[0].myID;
				c.Number2 = second.myID;
				return true;
			},
			Apply = delegate(IndustryContext c)
			{
				platformScript a = GameUpdates.Platform(c.PlatformId);
				platformScript b = GameUpdates.Platform(c.Number2);
				if (!a || !b)
				{
					return null;
				}
				a.AddHype(5f);
				b.AddHype(5f);
				return "<b>" + a.GetName() + "</b> and <b>" + b.GetName() + "</b> are at war: their adverts are everywhere, and players are picking sides.";
			}
		};
	}

	// A studio shows off its next game, and players can't wait.
	private static IndustryEventDef Reveal()
	{
		return new IndustryEventDef
		{
			Id = "reveal",
			Headline = true,
			From = 1985,
			Cooldown = 14,
			Setup = delegate(IndustryContext c)
			{
				List<publisherScript> pool = IndustryCore.Studios().Where((publisherScript p) => p.developer && p.stars >= 40f && (bool)p.FindAngekuendigtesGame() && p.FindAngekuendigtesGame().hype < 70f).ToList();
				if (pool.Count == 0)
				{
					return false;
				}
				publisherScript pick = Pick(pool);
				c.StudioId = pick.myID;
				c.GameId = pick.FindAngekuendigtesGame().myID;
				return true;
			},
			Apply = delegate(IndustryContext c)
			{
				gameScript g = Game(c.GameId);
				if (!g)
				{
					return null;
				}
				g.AddHype(10f);
				genres gen = IndustryCore.Genres;
				if ((bool)gen && g.maingenre >= 0 && g.maingenre < gen.genres_BELIEBTHEIT.Length)
				{
					gen.genres_BELIEBTHEIT[g.maingenre] = Mathf.Min(100f, gen.genres_BELIEBTHEIT[g.maingenre] + 2f);
				}
				return "<b>" + StudioName(c.StudioId) + "</b> shows off " + g.GetNameSimple() + ", and players can't wait for it.";
			}
		};
	}

	// Budget re-releases: your older games get a second wind.
	private static IndustryEventDef Budget()
	{
		return new IndustryEventDef
		{
			Id = "budget",
			Headline = true,
			From = 1985,
			To = 1995,
			Cooldown = 60,
			Weight = 0.6f,
			Setup = (IndustryContext c) => IndustryCore.MyGamesOnMarket().Any((gameScript x) => x.weeksOnMarket >= 52 && Weekly(x) > 0),
			Apply = delegate(IndustryContext c)
			{
				List<gameScript> old = (from x in IndustryCore.MyGamesOnMarket()
					where x.weeksOnMarket >= 52 && Weekly(x) > 0
					orderby Weekly(x) descending
					select x).Take(3).ToList();
				if (old.Count == 0)
				{
					return null;
				}
				foreach (gameScript g in old)
				{
					IndustryCore.AddMod(g, 0.12f, 8);
				}
				return "Budget re-releases are the latest thing: older games like your " + old[0].GetNameSimple() + " sell 12% more for 8 weeks.";
			}
		};
	}

	// A new chain of games shops opens: games are easier to find.
	private static IndustryEventDef NewShops()
	{
		return new IndustryEventDef
		{
			Id = "shops",
			Headline = true,
			From = 1986,
			To = 1996,
			Cooldown = 80,
			Weight = 0.5f,
			Setup = delegate(IndustryContext c)
			{
				List<int> unused = Enumerable.Range(0, Shops.Length).Where((int i) => !IndustryCore.State.Cooldowns.ContainsKey("shops:" + i)).ToList();
				if (unused.Count == 0)
				{
					return false;
				}
				c.Number = Pick(unused);
				return true;
			},
			Apply = delegate(IndustryContext c)
			{
				IndustryCore.State.Cooldowns["shops:" + c.Number] = Now;
				List<gameScript> mine = IndustryCore.MyGamesOnMarket().Where((gameScript x) => Weekly(x) > 0).ToList();
				foreach (gameScript g in mine)
				{
					IndustryCore.AddMod(g, 0.05f, 6);
				}
				return "A new chain of games shops, <i>" + Shops[c.Number] + "</i>, opens in towns everywhere" + ((mine.Count > 0) ? ": your games sell 5% more for 6 weeks." : ".");
			}
		};
	}
}
