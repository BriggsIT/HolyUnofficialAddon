using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HolyAddon.Core.Industry;

// The 1976-1985 pack: the garage and arcade years. Most of these need no released game, so a 1976 start has things
// happening before your first game is out. All names (clubs, magazines, toy makers, distributors) are made up.
internal static partial class IndustryEvents
{
	private static readonly string[] Clubs = new string[5] { "the Riverside Computer Club", "the Homebrew Hobbyists", "the Northgate Micro Society", "the Silicon Street Club", "the Basic Builders" };

	private static readonly string[] ToyMakers = new string[4] { "Tikko Toys", "Brightwave Electronics", "Pocketline", "Kiddo-Tronic" };

	private static readonly string[] Distributors = new string[4] { "Allways Distribution", "Midland Software Supply", "Coastline Wholesale", "Keystone Media" };

	private static readonly string[] Guilds = new string[3] { "the Software Makers' Guild", "the Independent Programmers' Association", "the Home Computing Trade Council" };

	private static readonly string[] NewMagazines = new string[6] { "Fire Button", "Pixel Post", "Hi-Score Herald", "Game Grid", "Screen Play Weekly", "Loading Screen" };

	private static readonly string[] Roles = new string[3] { "lead programmer", "star designer", "best artist" };

	private static IEnumerable<IndustryEventDef> Early()
	{
		yield return ComputerClub();
		yield return LendAHand();
		yield return TalkedDown();
		yield return FirstLook();
		yield return Guild();
		yield return Distributor();
		yield return Handheld();
		yield return HighScore();
		yield return ChristmasCraze();
		yield return Flop();
		yield return StarHire();
		yield return NewMagazine();
	}

	// A local computer club asks you to come and show what you're working on.
	private static IndustryEventDef ComputerClub()
	{
		return new IndustryEventDef
		{
			Id = "club",
			To = 1985,
			Cooldown = 50,
			Weight = 1.1f,
			Setup = delegate(IndustryContext c)
			{
				c.Number = Random.Range(0, Clubs.Length);
				c.Amount = Scaled(1500.0);
				return true;
			},
			Title = (IndustryContext c) => "Computer Club Night",
			Text = (IndustryContext c) => Cap(Clubs[c.Number]) + " meets every Thursday in a school hall: hobbyists swapping programs and tips. They'd love you to come and show what you're working on.",
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Give a Demo", "Free: about 150 new fans", "A keen member may ask for a job", delegate
				{
					int fans = AddFans(150f * R(0.7f, 1.4f));
					bool hired = Random.value < 0.4f && Applicants(1, 1) > 0;
					return "The hall is packed: " + fans + " new fans" + (hired ? ", and a programmer from the club applies for a job (Staff > hire)." : ".");
				}),
				Choice("Sponsor the Club", "Costs " + Money(c.Amount) + ": about 400 new fans", "Two members join the job market", delegate(IndustryContext x)
				{
					G.MS.Pay(x.Amount, 12);
					int fans = AddFans(400f * R(0.8f, 1.3f));
					int added = Applicants();
					return "Your name goes up on the club's banner: " + fans + " new fans" + ((added > 0) ? ", and two members apply for jobs (Staff > hire)." : ".");
				}, (IndustryContext x) => Need(x.Amount)),
				Choice("Not This Time", "Nothing changes", "Maybe next year", (IndustryContext x) => "You send your apologies to " + Clubs[x.Number] + ".")
			}
		};
	}

	// A small studio is stuck on a tricky bit of code and asks for help: a cheap way to make friends.
	private static IndustryEventDef LendAHand()
	{
		return new IndustryEventDef
		{
			Id = "hand",
			To = 1985,
			Cooldown = 40,
			Setup = delegate(IndustryContext c)
			{
				List<publisherScript> pool = IndustryCore.Studios().Where((publisherScript p) => p.developer && p.stars <= 70f && IndustryCore.Tier(IndustryCore.Standing(p)) > 0 && (bool)p.FindGameInDevelopment()).ToList();
				if (pool.Count == 0)
				{
					return false;
				}
				publisherScript pick = Pick(pool);
				c.StudioId = pick.myID;
				c.GameId = pick.FindGameInDevelopment().myID;
				c.Number = Random.Range(0, 3);
				c.Amount = Scaled(800.0);
				c.Amount2 = Scaled(2000.0);
				return true;
			},
			Title = (IndustryContext c) => StudioName(c.StudioId) + " Asks for a Hand",
			Text = (IndustryContext c) => StudioName(c.StudioId) + " is stuck on " + Name(c.GameId) + ": " + (new string[3] { "its scrolling is far too slow", "it won't fit in memory", "it crashes on half the machines it's tested on" })[c.Number] + ", and nobody there can fix it. They've heard your programmers are good, and ask for help.",
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Lend a Programmer", "Costs a week's wages (" + Money(c.Amount) + ")", "Their game comes sooner; standing with them +10", delegate(IndustryContext x)
				{
					G.MS.Pay(x.Amount, 13);
					publisherScript p = IndustryCore.Studio(x.StudioId);
					if ((bool)p)
					{
						p.newGameInWeeks = Mathf.Max(1, p.newGameInWeeks - 2);
					}
					IndustryCore.Change(p, 10, "You lent them a programmer");
					return "Your programmer sorts it out in a week. " + StudioName(x.StudioId) + " won't forget it.";
				}, (IndustryContext x) => Need(x.Amount)),
				Choice("Sell Them Your Routines", "They pay " + Money(c.Amount2), "Standing with them +3", delegate(IndustryContext x)
				{
					G.MS.Earn(x.Amount2, 2);
					IndustryCore.Change(IndustryCore.Studio(x.StudioId), 3, "Sold them your code routines");
					return StudioName(x.StudioId) + " pays " + Money(x.Amount2) + " for your routines.";
				}),
				Choice("Too Busy", "Nothing changes", "Standing with them -2", delegate(IndustryContext x)
				{
					IndustryCore.Change(IndustryCore.Studio(x.StudioId), -2, "Wouldn't help with their code");
					return "You're too busy to help " + StudioName(x.StudioId) + ".";
				})
			}
		};
	}

	// A bigger studio talks your games down in a magazine: hit back, rise above it, or answer with an advert.
	private static IndustryEventDef TalkedDown()
	{
		return new IndustryEventDef
		{
			Id = "talkeddown",
			To = 1990,
			Cooldown = 45,
			Setup = delegate(IndustryContext c)
			{
				// Most likely a studio that already likes you least.
				List<publisherScript> pool = (from p in IndustryCore.Studios()
					where p.developer && p.stars >= 40f && (bool)p.FindGameInDevelopment()
					orderby IndustryCore.Standing(p) + R(0f, 25f)
					select p).Take(3).ToList();
				if (pool.Count == 0)
				{
					return false;
				}
				publisherScript pick = Pick(pool);
				c.StudioId = pick.myID;
				c.GameId = pick.FindGameInDevelopment().myID;
				c.Number = Random.Range(0, Magazines.Length);
				c.Amount = Scaled(2500.0);
				return true;
			},
			Title = (IndustryContext c) => "Talked Down in Print",
			Text = (IndustryContext c) => "In an interview with " + Magazines[c.Number] + ", the head of " + StudioName(c.StudioId) + " calls your games \"toys for beginners\" and says " + Name(c.GameId) + " will show everyone how it's done.",
			Default = 1,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Hit Back in Print", "About 250 new fans", "Their game's hype -5; standing with them -12", delegate(IndustryContext x)
				{
					int fans = AddFans(250f * R(0.8f, 1.3f));
					Game(x.GameId)?.AddHype(-5f);
					IndustryCore.Change(IndustryCore.Studio(x.StudioId), -12, "You hit back at them in " + Magazines[x.Number]);
					return "Your reply on " + Magazines[x.Number] + "'s letters page is the talk of the computer clubs: " + fans + " new fans.";
				}),
				Choice("Take the High Road", "About 80 new fans", "Standing with them +4", delegate(IndustryContext x)
				{
					int fans = AddFans(80f * R(0.8f, 1.3f));
					IndustryCore.Change(IndustryCore.Studio(x.StudioId), 4, "You didn't rise to their jibe");
					return "You say nothing, and readers notice: " + fans + " new fans. " + StudioName(x.StudioId) + " looks a little silly.";
				}),
				Choice("Buy an Advert", "Costs " + Money(c.Amount), "Hype +4 for your games in development", delegate(IndustryContext x)
				{
					G.MS.Pay(x.Amount, 12);
					HypeInDevelopment(4f);
					return "Your advert runs on the page after the interview.";
				}, (IndustryContext x) => Need(x.Amount))
			}
		};
	}

	// Two magazines both want the first look at your game in development (once per game).
	private static IndustryEventDef FirstLook()
	{
		return new IndustryEventDef
		{
			Id = "firstlook",
			To = 1990,
			Cooldown = 36,
			Setup = delegate(IndustryContext c)
			{
				gameScript g = (from x in IndustryCore.MyGamesInDevelopment()
					where DevProgress(x) >= 0.2f && DevProgress(x) <= 0.85f && !IndustryCore.State.Cooldowns.ContainsKey("firstlook:" + x.myID)
					orderby x.hype descending
					select x).FirstOrDefault();
				if (!g)
				{
					return false;
				}
				c.GameId = g.myID;
				c.Number = Random.Range(0, Magazines.Length);
				c.Number2 = (c.Number + Random.Range(1, Magazines.Length)) % Magazines.Length;
				IndustryCore.State.Cooldowns["firstlook:" + g.myID] = Now;
				return true;
			},
			Title = (IndustryContext c) => "First Look at " + Name(c.GameId),
			Text = (IndustryContext c) => "Word of " + Name(c.GameId) + " is out. " + Magazines[c.Number] + " and " + Magazines[c.Number2] + " both want the first look, and neither will cover it if the other gets it first.",
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Give It to " + Magazines[c.Number], "The most readers", "Hype +8 for " + Name(c.GameId), delegate(IndustryContext x)
				{
					Game(x.GameId)?.AddHype(8f);
					return Magazines[x.Number] + " runs four pages on " + Name(x.GameId) + ".";
				}),
				Choice("Give It to " + Magazines[c.Number2], "It goes on the cover", "Hype +5 and about 300 new fans", delegate(IndustryContext x)
				{
					Game(x.GameId)?.AddHype(5f);
					int fans = AddFans(300f * R(0.8f, 1.3f));
					return Name(x.GameId) + " is on the cover of " + Magazines[x.Number2] + ": " + fans + " new fans.";
				}),
				Choice("Keep It Under Wraps", "Nothing to show yet", "Hype +2: everyone wonders what it is", delegate(IndustryContext x)
				{
					Game(x.GameId)?.AddHype(2f);
					return "You keep " + Name(x.GameId) + " secret. The rumours do the rest.";
				})
			}
		};
	}

	// Studios band together in a guild and ask you to join (once a game).
	private static IndustryEventDef Guild()
	{
		return new IndustryEventDef
		{
			Id = "guild",
			From = 1978,
			To = 1988,
			Cooldown = 480,
			Setup = delegate(IndustryContext c)
			{
				int count = IndustryCore.Studios().Count((publisherScript p) => IndustryCore.Tier(IndustryCore.Standing(p)) > 0);
				if (count < 4)
				{
					return false;
				}
				c.Number = Mathf.Min(10, count);
				c.Number2 = Random.Range(0, Guilds.Length);
				c.Amount = Scaled(2500.0);
				c.Amount2 = Scaled(6000.0);
				return true;
			},
			Title = (IndustryContext c) => "A Guild for Game Makers",
			Text = (IndustryContext c) => c.Number + " studios have banded together as " + Guilds[c.Number2] + ", to swap know-how, share stands at shows and speak up for the industry. They ask you to join.",
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Join", "A fee of " + Money(c.Amount), "Standing +4 with the " + c.Number + " member studios", (IndustryContext x) => JoinGuild(x, x.Amount, 4, chair: false), (IndustryContext x) => Need(x.Amount)),
				Choice("Join and Chair It", "A fee of " + Money(c.Amount2), "Standing +7 with members, about 300 new fans", (IndustryContext x) => JoinGuild(x, x.Amount2, 7, chair: true), (IndustryContext x) => Need(x.Amount2)),
				Choice("Stay Independent", "Nothing changes", "The offer won't come again", (IndustryContext x) => "You stay independent.")
			}
		};
	}

	private static string JoinGuild(IndustryContext c, long fee, int standing, bool chair)
	{
		G.MS.Pay(fee, 13);
		List<publisherScript> members = (from p in IndustryCore.Studios()
			where IndustryCore.Tier(IndustryCore.Standing(p)) > 0
			orderby R(0f, 1f)
			select p).Take(c.Number).ToList();
		foreach (publisherScript p in members)
		{
			IndustryCore.Change(p, standing, chair ? "You chair the guild they belong to" : "Fellow members of the guild");
		}
		int fans = (chair ? AddFans(300f * R(0.8f, 1.3f)) : 0);
		return "You join " + Guilds[c.Number2] + (chair ? (" as its chair: " + fans + " new fans") : "") + ". Your standing with " + members.Count + " studios improves.";
	}

	// The distributor that gets your games into the shops is short of cash.
	private static IndustryEventDef Distributor()
	{
		return new IndustryEventDef
		{
			Id = "distributor",
			To = 1990,
			Cooldown = 60,
			Setup = delegate(IndustryContext c)
			{
				gameScript g = (from x in IndustryCore.MyGamesOnMarket()
					where Weekly(x) > 0 && x.weeksOnMarket >= 3 && x.sellsTotal > 0
					orderby Weekly(x) descending
					select x).FirstOrDefault();
				if (!g)
				{
					return false;
				}
				float perCopy = (float)g.umsatzTotal / (float)g.sellsTotal;
				c.GameId = g.myID;
				c.Number = Random.Range(0, Distributors.Length);
				c.Amount = (long)Mathf.Clamp((float)Weekly(g) * 4f * perCopy, (float)Scaled(1500.0), (float)Scaled(80000.0));
				return true;
			},
			Title = (IndustryContext c) => "A Distributor in Trouble",
			Text = (IndustryContext c) => Distributors[c.Number] + ", which gets your games into the shops, is short of cash. It owes you " + Money(c.Amount) + " for copies of " + Name(c.GameId) + ", and offers half now to settle.",
			Default = 0,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Take Half Now", Money(c.Amount / 2) + " now", "The rest is written off", delegate(IndustryContext x)
				{
					G.MS.Earn(x.Amount / 2, 3);
					return Distributors[x.Number] + " pays " + Money(x.Amount / 2) + " and the debt is settled.";
				}),
				Choice("Wait for It All", "Likely paid over 8 weeks", "But they may go under (about 40%)", delegate(IndustryContext x)
				{
					if (Random.value < 0.6f)
					{
						IndustryCore.State.Streams.Add(new IncomeStream
						{
							Label = Distributors[x.Number] + " pays its debt",
							Fixed = x.Amount / 8,
							WeeksLeft = 8,
							Category = 3
						});
						return Distributors[x.Number] + " pulls through and pays you back over 8 weeks.";
					}
					return Distributors[x.Number] + " goes under. The money is gone.";
				}),
				Choice("Take Your Stock Back", "No money", "Your copies go to other shops: sales +10% for 12 weeks", delegate(IndustryContext x)
				{
					IndustryCore.AddMod(Game(x.GameId), 0.1f, 12);
					return "You take your copies of " + Name(x.GameId) + " back and sell them through other shops.";
				})
			}
		};
	}

	// A toy maker wants an LCD pocket game of your hit.
	private static IndustryEventDef Handheld()
	{
		return new IndustryEventDef
		{
			Id = "handheld",
			From = 1979,
			To = 1988,
			Cooldown = 60,
			Setup = delegate(IndustryContext c)
			{
				gameScript g = (from x in IndustryCore.MyGamesOnMarket()
					where x.gameTyp == 0 && x.reviewTotal >= 55 && x.weeksOnMarket <= 156
					orderby x.reviewTotal descending
					select x).FirstOrDefault();
				if (!g)
				{
					return false;
				}
				c.GameId = g.myID;
				c.Number = Random.Range(0, ToyMakers.Length);
				c.Amount = (long)((float)Scaled(6000.0) * (1f + (float)g.reviewTotal / 100f) * (1f + (float)g.gameSize * 0.3f));
				return true;
			},
			Title = (IndustryContext c) => "A Pocket Version of " + Name(c.GameId),
			Text = (IndustryContext c) => ToyMakers[c.Number] + " makes the little LCD pocket games that kids take to school. It wants to make one of " + Name(c.GameId) + ": much simpler, but with your game's name on the box.",
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Take the Fee", Money(c.Amount) + " now", "IP points +20, some new fans", delegate(IndustryContext x)
				{
					G.MS.Earn(x.Amount, 11);
					Game(x.GameId)?.AddIpPoints(20f);
					int fans = AddFans(250f * R(0.8f, 1.3f));
					return ToyMakers[x.Number] + " pays " + Money(x.Amount) + ". The pocket game is in every playground: " + fans + " new fans.";
				}),
				Choice("Take Royalties", "About " + Money(c.Amount / 6) + " a week for 12 weeks", "Twice the fee, spread out; IP points +20", delegate(IndustryContext x)
				{
					IndustryCore.State.Streams.Add(new IncomeStream
					{
						Label = "Pocket " + Name(x.GameId),
						Fixed = x.Amount / 6,
						WeeksLeft = 12,
						Category = 11
					});
					Game(x.GameId)?.AddIpPoints(20f);
					AddFans(250f * R(0.8f, 1.3f));
					return "The pocket game goes on sale: about " + Money(x.Amount / 6) + " a week for 12 weeks.";
				}),
				Choice("Decline", "Nothing changes", "It stays a screen game", (IndustryContext x) => "You turn " + ToyMakers[x.Number] + " down.")
			}
		};
	}

	// Players trade high scores for your game: turn it into a contest.
	private static IndustryEventDef HighScore()
	{
		return new IndustryEventDef
		{
			Id = "highscore",
			From = 1979,
			To = 1988,
			Cooldown = 50,
			Setup = delegate(IndustryContext c)
			{
				gameScript g = (from x in IndustryCore.MyGamesOnMarket()
					where x.gameTyp == 0 && x.weeksOnMarket <= 52 && Weekly(x) > 0
					orderby Weekly(x) descending
					select x).FirstOrDefault();
				if (!g)
				{
					return false;
				}
				c.GameId = g.myID;
				c.Amount = Scaled(2000.0);
				c.Amount2 = Scaled(7000.0);
				return true;
			},
			Title = (IndustryContext c) => "A High-Score Contest",
			Text = (IndustryContext c) => "Players are trading high scores for " + Name(c.GameId) + " in playgrounds and on magazine letters pages. A contest could turn that into sales.",
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Run a Local Contest", "Costs " + Money(c.Amount), "Sales +10% for 6 weeks, about 200 new fans", delegate(IndustryContext x)
				{
					G.MS.Pay(x.Amount, 12);
					IndustryCore.AddMod(Game(x.GameId), 0.1f, 6);
					int fans = AddFans(200f * R(0.8f, 1.3f));
					return "The contest fills a shopping centre on a Saturday: " + fans + " new fans.";
				}, (IndustryContext x) => Need(x.Amount)),
				Choice("Hold a National Final", "Costs " + Money(c.Amount2), "Sales +20% for 10 weeks, about 600 new fans", delegate(IndustryContext x)
				{
					G.MS.Pay(x.Amount2, 12);
					IndustryCore.AddMod(Game(x.GameId), 0.2f, 10);
					int fans = AddFans(600f * R(0.8f, 1.3f));
					return "The national final makes the evening news: " + fans + " new fans.";
				}, (IndustryContext x) => Need(x.Amount2)),
				Choice("No Contest", "Nothing changes", "Players keep their scores to themselves", (IndustryContext x) => "No contest this time.")
			}
		};
	}

	// ---- Headlines ----

	// The newest machine is the gift everyone wants (November and December).
	private static IndustryEventDef ChristmasCraze()
	{
		return new IndustryEventDef
		{
			Id = "xmas",
			Headline = true,
			From = 1977,
			To = 1986,
			Cooldown = 30,
			Setup = delegate(IndustryContext c)
			{
				if (!G.MS || (G.MS.month != 11 && G.MS.month != 12))
				{
					return false;
				}
				platformScript[] all = G.MS.arrayPlatformsScripts;
				platformScript pick = all?.Where((platformScript p) => (bool)p && p.isUnlocked && !p.vomMarktGenommen && p.ownerID != G.MS.myID && p.typ <= 2 && Now - (p.date_year * 48 + (p.date_month - 1) * 4) >= 0 && Now - (p.date_year * 48 + (p.date_month - 1) * 4) <= 156).OrderByDescending((platformScript p) => p.date_year * 48 + p.date_month * 4).FirstOrDefault();
				if (!pick)
				{
					return false;
				}
				c.PlatformId = pick.myID;
				return true;
			},
			Apply = delegate(IndustryContext c)
			{
				platformScript p2 = Updates.GameUpdates.Platform(c.PlatformId);
				if (!p2)
				{
					return null;
				}
				p2.AddHype(10f);
				return "<b>" + p2.GetName() + "</b> is the gift everyone wants this Christmas.";
			}
		};
	}

	// An NPC game flops: its studio loses a little shine, and its genre cools.
	private static IndustryEventDef Flop()
	{
		return new IndustryEventDef
		{
			Id = "flop",
			Headline = true,
			Cooldown = 12,
			Weight = 0.9f,
			Setup = delegate(IndustryContext c)
			{
				gameScript g = (from x in IndustryCore.AllGames()
					where x.isOnMarket && !x.IsMyGame() && x.weeksOnMarket >= 2 && x.weeksOnMarket <= 12 && x.reviewTotal > 0 && x.reviewTotal < 45 && x.developerID >= 0 && (bool)IndustryCore.Studio(x.developerID) && !IndustryCore.State.Cooldowns.ContainsKey("flop:" + x.myID)
					orderby x.reviewTotal
					select x).FirstOrDefault();
				if (!g)
				{
					return false;
				}
				c.GameId = g.myID;
				c.StudioId = g.developerID;
				IndustryCore.State.Cooldowns["flop:" + g.myID] = Now;
				return true;
			},
			Apply = delegate(IndustryContext c)
			{
				gameScript g2 = Game(c.GameId);
				publisherScript p = IndustryCore.Studio(c.StudioId);
				if ((bool)p)
				{
					IndustryCore.AddStars(p, -2f);
				}
				genres gen = IndustryCore.Genres;
				if ((bool)gen && (bool)g2 && g2.maingenre >= 0 && g2.maingenre < gen.genres_BELIEBTHEIT.Length)
				{
					gen.genres_BELIEBTHEIT[g2.maingenre] = Mathf.Max(0f, gen.genres_BELIEBTHEIT[g2.maingenre] - 3f);
				}
				return "<b>" + StudioName(c.StudioId) + "</b>'s " + Name(c.GameId) + " flops: shops are sending copies back.";
			}
		};
	}

	// One studio hires a star away from another.
	private static IndustryEventDef StarHire()
	{
		return new IndustryEventDef
		{
			Id = "starhire",
			Headline = true,
			Cooldown = 20,
			Weight = 0.8f,
			Setup = delegate(IndustryContext c)
			{
				List<publisherScript> devs = IndustryCore.Studios().Where((publisherScript p) => p.developer).ToList();
				List<publisherScript> hirers = devs.Where((publisherScript p) => p.stars >= 40f).ToList();
				if (hirers.Count == 0)
				{
					return false;
				}
				publisherScript hirer = Pick(hirers);
				List<publisherScript> from = devs.Where((publisherScript p) => p != hirer && p.stars >= 20f && p.stars < hirer.stars).ToList();
				if (from.Count == 0)
				{
					return false;
				}
				c.StudioId = hirer.myID;
				c.Studio2 = Pick(from).myID;
				c.Number = Random.Range(0, Roles.Length);
				return true;
			},
			Apply = delegate(IndustryContext c)
			{
				publisherScript hirer = IndustryCore.Studio(c.StudioId);
				publisherScript lost = IndustryCore.Studio(c.Studio2);
				if (!hirer || !lost)
				{
					return null;
				}
				IndustryCore.AddStars(hirer, 2f);
				IndustryCore.AddStars(lost, -2f);
				return "<b>" + hirer.GetName() + "</b> hires the " + Roles[c.Number] + " of <b>" + lost.GetName() + "</b>.";
			}
		};
	}

	// A new games magazine: more players read about the games to come.
	private static IndustryEventDef NewMagazine()
	{
		return new IndustryEventDef
		{
			Id = "magazine",
			Headline = true,
			To = 1990,
			Cooldown = 60,
			Weight = 0.6f,
			Setup = delegate(IndustryContext c)
			{
				List<int> unused = Enumerable.Range(0, NewMagazines.Length).Where((int i) => !IndustryCore.State.Cooldowns.ContainsKey("magazine:" + i)).ToList();
				if (unused.Count == 0)
				{
					return false;
				}
				c.Number = Pick(unused);
				return true;
			},
			Apply = delegate(IndustryContext c)
			{
				IndustryCore.State.Cooldowns["magazine:" + c.Number] = Now;
				HypeInDevelopment(2f);
				return "A new games magazine, <i>" + NewMagazines[c.Number] + "</i>, hits the newsstands: more players read about the games to come.";
			}
		};
	}
}
