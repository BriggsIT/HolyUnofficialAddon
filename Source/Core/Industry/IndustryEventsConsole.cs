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
		SetProfession(c, profession);
		SetMarketSkill(c, profession, Mathf.Max(MarketSkill(c, profession), R(min, max)));
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

	// Cartridge chips run short: the production room makes copies dearer, or none at all for a while (the warehouse runs
	// down and orders wait, the game's own backorders), or you share your chips with a studio that has none.
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
					where x.weeksOnMarket <= 52 && Weekly(x) > 0 && x.sellsTotal > 0 && OnConsole(x) && SelfPublishedRetail(x)
					orderby Weekly(x) descending
					select x).FirstOrDefault();
				if (!g)
				{
					return false;
				}
				publisherScript friend = (from p in IndustryCore.Studios()
					where p.developer && IndustryCore.Tier(IndustryCore.Standing(p)) > 0 && (bool)TheirGame(p)
					orderby R(0f, 1f)
					select p).FirstOrDefault();
				if (!friend)
				{
					return false;
				}
				c.GameId = g.myID;
				c.StudioId = friend.myID;
				c.Game2 = TheirGame(friend).myID;
				return true;
			},
			Title = (IndustryContext c) => "A Cartridge Shortage",
			Text = (IndustryContext c) => "The chip makers can't keep up, and cartridges are in short supply. The factory will keep making " + Name(c.GameId) + " if you pay over the odds; otherwise your production room gets no chips for a while. Meanwhile " + StudioName(c.StudioId) + " has no chips at all for " + Name(c.Game2) + ".",
			Default = 1,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Pay the Premium", "Copies cost 50% more to make", "For 8 weeks; production carries on", delegate(IndustryContext x)
				{
					IndustryReal.LimitProduction(Game(x.GameId), 8, block: false, 0.5f);
					return "You pay over the odds: every copy of " + Name(x.GameId) + " your production room makes for 8 weeks costs half as much again.";
				}),
				Choice("Ship What You Can", "No new copies for 8 weeks", "Your stock runs down; orders wait", delegate(IndustryContext x)
				{
					IndustryReal.LimitProduction(Game(x.GameId), 8, block: true, 0f);
					return "Your production room gets no chips for " + Name(x.GameId) + " for 8 weeks. Once the warehouse is empty, orders wait for new copies (" + IndustryReal.Stock(Game(x.GameId)) + " in stock now).";
				}),
				Choice("Share Your Chips", "No new copies for 12 weeks", "They launch sooner; standing +15", delegate(IndustryContext x)
				{
					IndustryReal.LimitProduction(Game(x.GameId), 12, block: true, 0f);
					publisherScript p = IndustryCore.Studio(x.StudioId);
					if ((bool)p)
					{
						p.newGameInWeeks = Mathf.Max(1, p.newGameInWeeks - 3);
					}
					IndustryCore.Change(p, 15, "You shared your cartridge chips");
					return StudioName(x.StudioId) + " gets your chips and can launch " + Name(x.Game2) + " sooner. Your production room makes no copies of " + Name(x.GameId) + " for 12 weeks.";
				})
			}
		};
	}

	// A film studio offers its film's licence for your game in development: one of the game's own film licences (its
	// licence list: films that are out by now, suiting the game's genre or at least not clashing with it), at the
	// licence market's price for one game. The game applies the licence itself (better reviews, and more sales for as
	// long as the game sells, most when the film suits the genre) and shows it on the game. A weak film is the risk.
	// Sharing the revenue instead costs a share tied to the film's quality, for a year: the licence's boost never ends.
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
				licences lic = Licences();
				gameScript g = (from x in IndustryCore.MyGamesInDevelopment()
					where x.gameTyp == 0 && x.gameLicence < 0 && DevProgress(x) <= 0.5f && !IndustryCore.State.Cooldowns.ContainsKey("film:" + x.myID)
					orderby R(0f, 1f)
					select x).FirstOrDefault();
				if (!g || !lic || lic.licence_TYP == null)
				{
					return false;
				}
				List<int> films = new List<int>();
				for (int i = 0; i < lic.licence_TYP.Length; i++)
				{
					if (lic.licence_TYP[i] == 0 && lic.licence_YEAR[i] <= Year && lic.licence_QUALITY[i] > 0f && lic.licence_GENREBAD[i] != g.maingenre && !string.IsNullOrEmpty(lic.licence_EN[i]))
					{
						films.Add(i);
					}
				}
				if (films.Count == 0)
				{
					return false;
				}
				List<int> suits = films.Where((int i) => lic.licence_GENREGOOD[i] == g.maingenre).ToList();
				int pick = ((suits.Count > 0 && Random.value < 0.7f) ? Pick(suits) : Pick(films));
				float q = lic.licence_QUALITY[pick];
				c.GameId = g.myID;
				c.PlatformId = pick;
				c.Number2 = Random.Range(0, FilmStudios.Length);
				c.Amount = LicencePrice(lic, pick);
				c.Share = Mathf.Round((0.05f + q / 500f) * 100f) / 100f;
				IndustryCore.State.Cooldowns["film:" + g.myID] = Now;
				return true;
			},
			Title = (IndustryContext c) => "The Game of the Film",
			Text = delegate(IndustryContext c)
			{
				licences lic = Licences();
				gameScript g = Game(c.GameId);
				if (!lic || c.PlatformId < 0 || c.PlatformId >= lic.licence_EN.Length || !g)
				{
					return "A film studio offered you a licence, but the offer has lapsed.";
				}
				return FilmStudios[c.Number2] + "'s film " + lic.GetName(c.PlatformId) + " is out, and it offers you the licence for " + Name(c.GameId) + ": the film's name and heroes on the box. It's " + FilmQuality(lic.licence_QUALITY[c.PlatformId]) + " (quality " + Mathf.RoundToInt(lic.licence_QUALITY[c.PlatformId]) + "), and " + (FilmSuits(lic, c.PlatformId, g) ? ("it suits " + IndustryCore.GenreGames(g.maingenre) + ".") : ("it isn't an obvious fit for " + IndustryCore.GenreGames(g.maingenre) + ", so it helps less."));
			},
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Buy the Licence", "Costs " + Money(c.Amount), FilmEffect(c) + " for as long as it sells; better reviews", delegate(IndustryContext x)
				{
					string why = AttachFilm(x);
					if (why != null)
					{
						return why;
					}
					G.MS.Pay(x.Amount, 7);
					return Name(x.GameId) + " is now " + Licences().GetName(x.PlatformId) + ": the game. The licence shows on the game, and it does the rest.";
				}, (IndustryContext x) => FilmProblem(x) ?? Need(x.Amount)),
				Choice("Share the Revenue", "No fee; " + Mathf.RoundToInt(c.Share * 100f) + "% of its revenue", "For a year after release; the same licence (" + FilmEffect(c) + ")", delegate(IndustryContext x)
				{
					string why = AttachFilm(x);
					if (why != null)
					{
						return why;
					}
					IndustryCore.State.Streams.Add(new IncomeStream
					{
						Label = FilmStudios[x.Number2] + "'s share of " + Name(x.GameId),
						GameId = x.GameId,
						Share = x.Share,
						WeeksLeft = 52,
						Out = true
					});
					return Name(x.GameId) + " is now " + Licences().GetName(x.PlatformId) + ": the game. " + FilmStudios[x.Number2] + " takes " + Mathf.RoundToInt(x.Share * 100f) + "% of its revenue for a year after release.";
				}, FilmProblem),
				Choice("No Thanks", "Nothing changes", "The film finds another studio", (IndustryContext x) => "You turn the licence down.")
			}
		};
	}

	private static licences Licences()
	{
		if (!G.MainObj)
		{
			return null;
		}
		return G.MainObj.GetComponent<licences>();
	}

	// The licence market's price for one game's worth of this licence (licences.GetPrice, for one).
	private static long LicencePrice(licences lic, int i)
	{
		int years = Mathf.Clamp(Year - 1976, 1, 50);
		float q = lic.licence_QUALITY[i];
		float per = ((q < 33f) ? (1500f + (float)years * 50f) : ((q < 66f) ? (4000f + (float)years * 100f) : ((q < 80f) ? (7000f + (float)years * 200f) : (11000f + (float)years * 300f))));
		return (long)Mathf.RoundToInt(q * per) / 500 * 500;
	}

	private static bool FilmSuits(licences lic, int i, gameScript g)
	{
		return lic.licence_GENREGOOD[i] == g.maingenre;
	}

	private static string FilmQuality(float q)
	{
		if (q >= 80f)
		{
			return "a blockbuster";
		}
		if (q >= 60f)
		{
			return "a solid hit";
		}
		if (q >= 35f)
		{
			return "a modest success";
		}
		return "a flop";
	}

	// The game's own licence sales effect for the game's main genre (gameScript: x0.8 of quality if it suits, x0.3 if
	// it's neutral).
	private static string FilmEffect(IndustryContext c)
	{
		licences lic = Licences();
		gameScript g = Game(c.GameId);
		if (!lic || !g || c.PlatformId < 0 || c.PlatformId >= lic.licence_QUALITY.Length)
		{
			return "the licence's sales boost";
		}
		float share = (FilmSuits(lic, c.PlatformId, g) ? 0.8f : 0.3f);
		return "sales about +" + Mathf.RoundToInt(lic.licence_QUALITY[c.PlatformId] * share) + "%";
	}

	private static string FilmProblem(IndustryContext c)
	{
		licences lic = Licences();
		gameScript g = Game(c.GameId);
		if (!lic || c.PlatformId < 0 || c.PlatformId >= lic.licence_TYP.Length)
		{
			return "The offer has lapsed.";
		}
		if (!g || !g.inDevelopment)
		{
			return "The game is already finished.";
		}
		if (g.gameLicence >= 0)
		{
			return "The game already has a licence.";
		}
		return null;
	}

	// The game carries the licence from now on: the game's own licence effects apply when it's reviewed and sold.
	private static string AttachFilm(IndustryContext c)
	{
		string problem = FilmProblem(c);
		if (problem != null)
		{
			return problem;
		}
		Game(c.GameId).gameLicence = c.PlatformId;
		return null;
	}

	// A TV studio wants a Saturday-morning cartoon of your hit: an animated series of the IP that premieres about a year
	// later and is judged on the IP's fame and its games' reviews. A hit brings lapsed players back to every game of the
	// series, IP points and fans; a flop costs IP points. With The Holy Expansion's Movie & TV deals on, it's Holy's own
	// deal (so the two never double up); otherwise the addon runs it (IndustryReal). Co-producing also brings the
	// series' merchandise: the IP's fan shop demand starts fresh at the premiere.
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
					where x.gameTyp == 0 && x.reviewTotal >= 70 && x.weeksOnMarket <= 156 && x.mainIP >= 0 && !IndustryCore.State.Cooldowns.ContainsKey("cartoon:" + x.mainIP)
					let ip = Game(x.mainIP)
					where (bool)ip && ip.ownerID == G.MS.myID && !IndustryReal.CartoonRunning(ip)
					orderby Landmarks.Landmarks.IsLandmarkSeries(x) descending, x.reviewTotal descending
					select x).FirstOrDefault();
				if (!g)
				{
					return false;
				}
				gameScript ip2 = Game(g.mainIP);
				c.GameId = g.myID;
				c.Game2 = ip2.myID;
				c.Number = Random.Range(0, TvStudios.Length);
				// Holy's price for an animated series, by the IP's fame.
				c.Amount = Prices.Round((double)Prices.Scaled(150000.0) * (double)Mathf.Clamp(ip2.ipPunkte / 1000f, 0.25f, 1f) * (double)R(0.8f, 1.3f));
				IndustryCore.State.Cooldowns["cartoon:" + ip2.myID] = Now;
				return true;
			},
			Title = (IndustryContext c) => "A Cartoon of " + Name(c.GameId),
			Text = (IndustryContext c) => TvStudios[c.Number] + " wants to make a Saturday-morning cartoon of " + (Game(c.Game2) ? Game(c.Game2).GetIpName() : Name(c.GameId)) + ". It would air in about a year: a hit brings players back to every game of the series, a flop does it no favours. Sell them the rights, or put your own money in and share in the toys and lunchboxes.",
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Sell the Rights", Money(c.Amount) + " now", "IP points +15; airs in about a year", delegate(IndustryContext x)
				{
					gameScript ip = Game(x.Game2);
					if (!ip)
					{
						return "The series is gone.";
					}
					G.MS.Earn(x.Amount, 1);
					ip.AddIpPoints(15f);
					IndustryReal.StartCartoon(ip, TvStudios[x.Number], x.Amount, coProduced: false);
					return TvStudios[x.Number] + " pays " + Money(x.Amount) + ". The " + ip.GetIpName() + " cartoon airs in about " + Mathf.Max(1, IndustryReal.CartoonWeeks(ip) / 4) + " months.";
				}),
				Choice("Co-Produce It", "Costs " + Money(c.Amount), "The same series, and its merchandise", delegate(IndustryContext x)
				{
					gameScript ip = Game(x.Game2);
					if (!ip)
					{
						return "The series is gone.";
					}
					G.MS.Pay(x.Amount, 13);
					ip.AddIpPoints(15f);
					IndustryReal.StartCartoon(ip, TvStudios[x.Number], x.Amount, coProduced: true);
					return "You co-produce the " + ip.GetIpName() + " cartoon with " + TvStudios[x.Number] + ". It airs in about " + Mathf.Max(1, IndustryReal.CartoonWeeks(ip) / 4) + " months, and the toys and lunchboxes come with it: if it doesn't flop, your fan shop's demand for the series starts fresh.";
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
				c.Amount = OfRevenue(g, 0.01f, Scaled(6000.0));
				IndustryCore.State.Cooldowns["lowscore:" + g.myID] = Now;
				return true;
			},
			Title = (IndustryContext c) => "A Harsh Review",
			Text = (IndustryContext c) => ConsoleMags[c.Number] + " gives " + Name(c.GameId) + " just " + c.Number2 + "%, far below every other review, and calls it \"a waste of pocket money\". Players are writing in to argue about it.",
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Write to the Editor", "Free, but a gamble", "A re-review lifts its score, or readers side with them", delegate(IndustryContext x)
				{
					gameScript g = Game(x.GameId);
					if (!g)
					{
						return "The game is gone.";
					}
					if (Random.value < 0.5f)
					{
						int up = Random.Range(2, 4);
						g.reviewTotal = Mathf.Min(100, g.reviewTotal + up);
						int fans = AddFans(150f * R(0.8f, 1.3f));
						return ConsoleMags[x.Number] + " prints your letter and re-reviews " + g.GetNameSimple() + ": its review score goes up " + up + " points to " + g.reviewTotal + "%, and " + fans + " new fans.";
					}
					HolyCompat.ShiftUserReviews(g, -0.05f);
					return "Readers side with the magazine, and the row puts players off: " + g.GetNameSimple() + "'s user score drops.";
				}),
				Choice("Buy a Double-Page Advert", "Costs " + Money(c.Amount), "Hype +8: let the game speak", delegate(IndustryContext x)
				{
					G.MS.Pay(x.Amount, 12);
					Game(x.GameId)?.AddHype(8f);
					return "Your advert runs two issues later, with quotes from every other review: hype +8.";
				}, (IndustryContext x) => Need(x.Amount)),
				Choice("Let It Go", "Nothing changes", "The other reviews speak for it", (IndustryContext x) => "You let the other reviews speak for themselves.")
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
				// A shelf of copies for its shops: a couple of weeks' sales, at a rental copy's higher price.
				c.Number2 = Mathf.Max(50, Mathf.RoundToInt((float)Weekly(g) * R(1.5f, 3f)));
				c.Share = IndustryReal.PerCopy(g) * 1.5f;
				return true;
			},
			Title = (IndustryContext c) => "Rent It Out?",
			Text = (IndustryContext c) => RentalChains[c.Number] + " wants " + Name(c.GameId) + " on its shelves next to the films, for players to rent for the weekend. It buys rental copies at a premium, but every player who rents it is one who won't buy it.",
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Sign the Deal", "They buy " + c.Number2 + " copies (" + Money((long)((float)c.Number2 * c.Share)) + ")", "Renters become fans, but fewer will buy it later", (IndustryContext x) => Rent(x, x.Number2)),
				Choice("Ask for More", "A gamble", "A bigger order, or they walk away", delegate(IndustryContext x)
				{
					if (Random.value < 0.5f)
					{
						return "They agree. " + Rent(x, x.Number2 * 3 / 2);
					}
					return RentalChains[x.Number] + " walks away and signs another studio's game instead.";
				}),
				Choice("Say No", "Nothing changes", "Players will have to buy it", (IndustryContext x) => "You keep " + Name(x.GameId) + " out of the rental shops.")
			}
		};
	}

	// Real copies sold to the chain (from your warehouse when it's boxed and yours), renters who become fans, and a
	// shorter shelf life: the players who rent it won't buy it.
	private static string Rent(IndustryContext c, int copies)
	{
		gameScript g = Game(c.GameId);
		if (!g)
		{
			return "The game is gone.";
		}
		long sold = IndustryReal.SellCopies(g, copies, c.Share);
		if (sold <= 0)
		{
			return "You have no copies of " + g.GetNameSimple() + " in the warehouse to send them.";
		}
		IndustryReal.Saturate(g, 0.08f);
		int fans = AddFans((float)sold * 0.5f * R(0.8f, 1.3f));
		return RentalChains[c.Number] + " buys " + sold + " copies of " + g.GetNameSimple() + " (" + Money((long)((float)sold * c.Share)) + "). Renters become " + fans + " new fans, but fewer players will buy it from now on.";
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
				c.Amount = OfRevenue(g, 0.015f, (long)((float)Scaled(4000.0) * (1f + (float)g.gameSize * 0.4f) * (1f + (float)g.reviewTotal / 100f)));
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
				Choice("Run a Hint Line", "About " + Money(c.Amount * 13 / 120) + " a week for 12 weeks", "A bit more than the fee; parents may complain (user score)", delegate(IndustryContext x)
				{
					IndustryCore.State.Streams.Add(new IncomeStream
					{
						Label = Name(x.GameId) + " hint line",
						Fixed = x.Amount * 13 / 120,
						WeeksLeft = 12,
						Category = 2
					});
					if (Random.value < 0.3f)
					{
						HolyCompat.ShiftUserReviews(Game(x.GameId), -0.03f);
						return "The phones ring all day, but parents complain about their bills, and players hold it against the game: its user score drops.";
					}
					return "The phones ring all day: about " + Money(x.Amount * 13 / 120) + " a week for 12 weeks.";
				}),
				Choice("Print Free Tips", "Free: about 300 new fans", "Happier players, who keep playing", delegate(IndustryContext x)
				{
					gameScript g = Game(x.GameId);
					int fans = AddFans(300f * R(0.8f, 1.3f));
					HolyCompat.ShiftUserReviews(g, 0.02f);
					IndustryReal.Saturate(g, -0.05f);
					return "Your tips run in every magazine: " + fans + " new fans, a better user score, and players who'd given up pick it up again.";
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
		G.MS.Earn(bonus, 2);
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
				c.Amount = OfRevenue(g, 0.003f, Scaled(2000.0));
				c.Amount2 = OfRevenue(g, 0.015f, Scaled(6000.0));
				// The milder version's rating: at most two steps down (an 18+ game can't become a 6+ without being
				// remade), and never below what its topic allows.
				c.Number2 = Mathf.Max(GameUpdates.MinUsk(g), g.usk - 2);
				IndustryCore.State.Cooldowns["parents:" + g.myID] = Now;
				return true;
			},
			Title = (IndustryContext c) => "Concerned Parents",
			Text = (IndustryContext c) => "A campaign group, " + ParentGroups[c.Number] + ", says " + Name(c.GameId) + " is far too violent for children. It's on the evening news, and some shops are thinking of taking it off their shelves.",
			Default = 1,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Defend It on TV", "About 600 new fans; hype +8", "Unless the row gets it re-rated higher", delegate(IndustryContext x)
				{
					gameScript g = Game(x.GameId);
					if (!g)
					{
						return "The game is gone.";
					}
					int fans = AddFans(600f * R(0.8f, 1.3f));
					g.AddHype(8f);
					if (Random.value < 0.75f)
					{
						return "The row makes " + g.GetNameSimple() + " the game every teenager wants: " + fans + " new fans, and hype +8.";
					}
					if (g.usk < 5)
					{
						int old = g.usk;
						g.usk++;
						return fans + " new fans, but the rating board takes another look: " + g.GetNameSimple() + " is re-rated " + GameUpdates.UskName(g.usk) + " (was " + GameUpdates.UskName(old) + "), and fewer shops and players can buy it.";
					}
					IndustryReal.Saturate(g, 0.15f);
					return fans + " new fans, but two big chains take " + g.GetNameSimple() + " off their shelves for good: it won't sell for as long.";
				}),
				Choice("Add a Warning Sticker", "Costs " + Money(c.Amount), "The fuss dies down", delegate(IndustryContext x)
				{
					G.MS.Pay(x.Amount, 13);
					return "A bright warning sticker goes on every box, and the news moves on.";
				}, (IndustryContext x) => Need(x.Amount)),
				Choice("Make a Milder Version", "Costs " + Money(c.Amount2), "Rated " + GameUpdates.UskName(c.Number2) + " instead: more shops and players; a little gameplay cut", delegate(IndustryContext x)
				{
					gameScript g = Game(x.GameId);
					if (!g)
					{
						return "The game is gone.";
					}
					G.MS.Pay(x.Amount2, 4);
					int old = g.usk;
					int steps = old - x.Number2;
					g.usk = x.Number2;
					g.points_gameplay = Mathf.Max(0f, g.points_gameplay * (1f - 0.03f * (float)steps));
					g.userNegativ += Mathf.RoundToInt((float)(g.userPositiv + g.userNegativ) * 0.03f * (float)steps);
					return "A milder version replaces the original in the shops: " + g.GetNameSimple() + " is now rated " + GameUpdates.UskName(g.usk) + " (was " + GameUpdates.UskName(old) + "). Some fans miss the cut content.";
				}, delegate(IndustryContext x)
				{
					gameScript g = Game(x.GameId);
					if ((bool)g && x.Number2 >= g.usk)
					{
						return "Its topic doesn't allow a lower age rating.";
					}
					return Need(x.Amount2);
				})
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
				c.Amount = IncomeCap(Scaled(3000.0), 0.001f);
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
				Choice("Pay for Lessons Only", "Costs " + Money(c.Amount), "Up to 5 staff improve (your tricks stay yours); standing +3", delegate(IndustryContext x)
				{
					G.MS.Pay(x.Amount, 13);
					List<string> names = Train(5, 3f, 5f);
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
					orderby IndustryCore.Standing(p) + R(0f, 20f) - (IndustryCompetitors.IsCompetitor(p) ? 30f : 0f)
					select p).FirstOrDefault();
				if (!rival)
				{
					return false;
				}
				c.GameId = g.myID;
				c.StudioId = rival.myID;
				c.Game2 = theirs.Where((gameScript x) => x.developerID == rival.myID).OrderByDescending(Weekly).First().myID;
				c.Amount = (long)Mathf.Clamp(WeeklyRevenue(g), (float)Scaled(3000.0), (float)IncomeCap(Scaled(50000.0)));
				return true;
			},
			Title = (IndustryContext c) => "Shelf Space",
			Text = (IndustryContext c) => StudioName(c.StudioId) + (IndustryCompetitors.IsCompetitor(IndustryCore.Studio(c.StudioId)) ? ", one of your competitors," : "") + " is paying the big shops to put " + Name(c.Game2) + " at the front, and " + Name(c.GameId) + " has been moved to the back shelves.",
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Outbid Them", "Costs " + Money(c.Amount), "Front-of-shop displays: hype +6", delegate(IndustryContext x)
				{
					G.MS.Pay(x.Amount, 12);
					Game(x.GameId)?.AddHype(6f);
					IndustryCore.Change(IndustryCore.Studio(x.StudioId), -3, "You outbid them for shelf space");
					return Name(x.GameId) + " is back at the front of the shops, with a display of its own: hype +6.";
				}, (IndustryContext x) => Need(x.Amount)),
				Choice("Complain to the Trade Press", "Free, but a gamble", "Standing with them -10; the shops may back down", delegate(IndustryContext x)
				{
					IndustryCore.Change(IndustryCore.Studio(x.StudioId), -10, "You complained about them to the trade press");
					if (Random.value < 0.5f)
					{
						return "The story embarrasses the shops, and " + Name(x.GameId) + " goes back on the front shelves.";
					}
					IndustryReal.Saturate(Game(x.GameId), 0.1f);
					return "Nobody listens: " + Name(x.GameId) + " stays on the back shelves, and won't sell for as long.";
				}),
				Choice("Let It Go", "Nothing to pay", "A shorter shelf life for " + Name(c.GameId), delegate(IndustryContext x)
				{
					IndustryReal.Saturate(Game(x.GameId), 0.1f);
					return "You leave " + StudioName(x.StudioId) + " to it. On the back shelves, " + Name(x.GameId) + " won't sell for as long.";
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
				List<platformScript> pool = ((all == null) ? new List<platformScript>() : all.Where((platformScript p) => (bool)p && p.isUnlocked && !p.vomMarktGenommen && p.ownerID != G.MS.myID && (p.typ == 1 || p.typ == 2) && Now - LaunchWeek(p) >= 0 && Now - LaunchWeek(p) <= 8 && !IndustryCore.State.Cooldowns.ContainsKey("launch:" + p.myID) && !IndustryHooks.WeakLaunch(p)).ToList());
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

	// Budget re-releases: budget buyers find your older games, and players who'd passed them by buy them after all
	// (their saturation eases).
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
			Setup = (IndustryContext c) => IndustryCore.MyGamesOnMarket().Any((gameScript x) => x.gameTyp == 0 && x.weeksOnMarket >= 52 && Weekly(x) > 0),
			Apply = delegate(IndustryContext c)
			{
				List<gameScript> old = (from x in IndustryCore.MyGamesOnMarket()
					where x.gameTyp == 0 && x.weeksOnMarket >= 52 && Weekly(x) > 0
					orderby Weekly(x) descending
					select x).ToList();
				if (old.Count == 0)
				{
					return null;
				}
				foreach (gameScript g in old)
				{
					IndustryReal.Saturate(g, -0.15f);
				}
				return "Budget re-releases are the latest thing: bargain hunters find older games like your " + old[0].GetNameSimple() + ", and buyers come back to them.";
			}
		};
	}

	// A new chain of games shops opens: it stocks up on your boxed games (an opening order, which the game fills from
	// your warehouse as it would any order).
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
				List<gameScript> mine = IndustryCore.MyGamesOnMarket().Where((gameScript x) => IndustryReal.Boxed(x) && x.releaseDate <= 0 && Weekly(x) > 0).ToList();
				int ordered = 0;
				foreach (gameScript g in mine)
				{
					int n = Mathf.Max(20, Mathf.RoundToInt((float)Weekly(g) * R(0.5f, 1f)));
					g.vorbestellungen += n;
					ordered += n;
				}
				return "A new chain of games shops, <i>" + Shops[c.Number] + "</i>, opens in towns everywhere" + ((ordered > 0) ? (": it orders " + ordered + " copies of your boxed games to stock its shelves.") : ".");
			}
		};
	}
}
