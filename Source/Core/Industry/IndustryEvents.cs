using System;
using System.Collections.Generic;
using System.Linq;
using HolyAddon.Core.Updates;
using UnityEngine;

namespace HolyAddon.Core.Industry;

// One answer to a decision: two lines saying what it does, and what happens when you pick it.
internal class IndustryChoice
{
	public string Title = "";

	public string Line1 = "";

	public string Line2 = "";

	public string Tooltip;

	public Func<IndustryContext, string> Problem;

	public Func<IndustryContext, string> Apply;
}

// A headline (news only) or a decision (a pop-up with choices). Setup fills the context and says whether it can
// happen right now; From/To keep it to its era; Cooldown (weeks) keeps it from repeating too soon.
internal class IndustryEventDef
{
	public string Id = "";

	public bool Headline;

	public int From = 1976;

	public int To = 9999;

	public int Cooldown = 52;

	public float Weight = 1f;

	public Func<IndustryContext, bool> Setup;

	public Func<IndustryContext, string> Title;

	public Func<IndustryContext, string> Text;

	public Func<IndustryContext, List<IndustryChoice>> Choices;

	public int Default;

	public Func<IndustryContext, string> Apply;
}

// The events. All names (magazines, arcade chains) are made up. The 1976-1985 pack is in IndustryEventsEarly, the
// 1985-1994 pack in IndustryEventsConsole.
internal static partial class IndustryEvents
{
	private static List<IndustryEventDef> defs;

	private static readonly string[] Magazines = new string[6] { "Home Micro Weekly", "Byte Club Monthly", "Program Page", "Keyboard Kids", "Cassette Corner", "The Listing" };

	private static readonly string[] Operators = new string[5] { "Neon Alley Amusements", "Coin Kingdom", "Pixel Palace Arcades", "StarPlay Halls", "Joystick Junction" };

	public static List<IndustryEventDef> Defs => defs ?? (defs = Build());

	public static IndustryEventDef Find(string id)
	{
		return Defs.FirstOrDefault((IndustryEventDef d) => d.Id == id);
	}

	private static int Now => IndustryCore.Now;

	private static int Year => IndustryCore.Year;

	private static long Scaled(double v)
	{
		return Prices.Scaled(v);
	}

	private static string Money(long v)
	{
		return G.Money(v);
	}

	private static float R(float a, float b)
	{
		return UnityEngine.Random.Range(a, b);
	}

	private static T Pick<T>(IList<T> list)
	{
		return list[UnityEngine.Random.Range(0, list.Count)];
	}

	private static bool Ready(IndustryEventDef d)
	{
		if (Year < d.From || Year > d.To)
		{
			return false;
		}
		if (IndustryCore.State.LastFired.TryGetValue(d.Id, out var last))
		{
			return Now - last >= d.Cooldown;
		}
		return true;
	}

	// A decision that can happen now, picked by weight (null when none fits).
	public static IndustryPending PickDecision()
	{
		List<IndustryEventDef> pool = Defs.Where((IndustryEventDef d) => !d.Headline && Ready(d)).ToList();
		while (pool.Count > 0)
		{
			IndustryEventDef d2 = Weighted(pool);
			pool.Remove(d2);
			IndustryContext c = new IndustryContext();
			bool ok;
			try
			{
				ok = d2.Setup(c);
			}
			catch (Exception ex)
			{
				Plugin.Log.LogWarning("Industry event " + d2.Id + ": " + ex.Message);
				ok = false;
			}
			if (ok)
			{
				return new IndustryPending
				{
					Id = d2.Id,
					C = c,
					Week = Now
				};
			}
		}
		return null;
	}

	// A headline: the industry moves on its own, in the news.
	public static void Headline()
	{
		List<IndustryEventDef> pool = Defs.Where((IndustryEventDef d) => d.Headline && Ready(d)).ToList();
		while (pool.Count > 0)
		{
			IndustryEventDef d2 = Weighted(pool);
			pool.Remove(d2);
			IndustryContext c = new IndustryContext();
			try
			{
				if (!d2.Setup(c))
				{
					continue;
				}
				string text = d2.Apply(c);
				IndustryCore.State.LastFired[d2.Id] = Now;
				if (!string.IsNullOrEmpty(text))
				{
					IndustryCore.News(text);
					IndustryCore.Feed(text);
				}
				return;
			}
			catch (Exception ex)
			{
				Plugin.Log.LogWarning("Industry headline " + d2.Id + ": " + ex.Message);
			}
		}
	}

	// Names for the test page (cheats on).
	public static readonly Dictionary<string, string> Names = new Dictionary<string, string>
	{
		{ "cassette", "A Cassette in the Post" },
		{ "typein", "Print Your Code?" },
		{ "arcade", "Cabinet Deal" },
		{ "machine", "A New Machine Wants Games" },
		{ "stand", "Share a Stand?" },
		{ "clone", "A Copy of Your Game" },
		{ "bidding", "Publishers Bid" },
		{ "rescue", "A Studio Needs Help" },
		{ "club", "Computer Club Night" },
		{ "hand", "A Studio Asks for a Hand" },
		{ "talkeddown", "Talked Down in Print" },
		{ "firstlook", "First Look" },
		{ "guild", "A Guild for Game Makers" },
		{ "distributor", "A Distributor in Trouble" },
		{ "handheld", "A Pocket Version" },
		{ "highscore", "A High-Score Contest" },
		{ "craze", "Genre craze" },
		{ "fatigue", "Genre fatigue" },
		{ "hit", "A rival's hit" },
		{ "pricecut", "A machine's price cut" },
		{ "delay", "A studio delays its game" },
		{ "xmas", "The Christmas must-have" },
		{ "flop", "A rival's flop" },
		{ "starhire", "A star changes studios" },
		{ "magazine", "A new magazine" },
		{ "cartridge", "A Cartridge Shortage" },
		{ "film", "The Game of the Film" },
		{ "cartoon", "A Cartoon of Your Hit" },
		{ "lowscore", "A Harsh Review" },
		{ "poach", "A Star Wants to Join You" },
		{ "rental", "Rent It Out?" },
		{ "hintline", "Stuck on Your Game" },
		{ "exclusive", "An Exclusive Offer" },
		{ "parents", "Concerned Parents" },
		{ "swap", "Swap Know-How?" },
		{ "shelf", "Shelf Space" },
		{ "launch", "A sell-out launch" },
		{ "consolewar", "A console war" },
		{ "reveal", "A big reveal" },
		{ "budget", "Budget re-releases" },
		{ "shops", "A new chain of shops" }
	};

	// Testing (cheats on): runs one event now if its conditions are met, whatever its years and cooldown.
	// Returns what happened, for the test page.
	public static string Trigger(string id)
	{
		IndustryEventDef d = Find(id);
		if (d == null)
		{
			return "No such event.";
		}
		if (!d.Headline && IndustryCore.State.Pending != null)
		{
			return "A decision is already waiting: answer it first.";
		}
		IndustryContext c = new IndustryContext();
		bool ok;
		try
		{
			ok = d.Setup(c);
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Industry event " + id + ": " + ex.Message);
			ok = false;
		}
		if (!ok)
		{
			return "It can't happen right now: its conditions aren't met (for example, it needs a game on sale or in development).";
		}
		if (d.Headline)
		{
			string text = d.Apply(c);
			IndustryCore.State.LastFired[d.Id] = Now;
			if (string.IsNullOrEmpty(text))
			{
				return "It fired, but had nothing to say.";
			}
			IndustryCore.News(text);
			IndustryCore.Feed(text);
			return "In the news: " + text;
		}
		IndustryCore.Force(new IndustryPending
		{
			Id = d.Id,
			C = c,
			Week = Now
		});
		return "It will pop up when you close this window.";
	}

	private static IndustryEventDef Weighted(List<IndustryEventDef> pool)
	{
		float total = pool.Sum((IndustryEventDef d) => d.Weight);
		float roll = R(0f, total);
		foreach (IndustryEventDef d in pool)
		{
			roll -= d.Weight;
			if (roll <= 0f)
			{
				return d;
			}
		}
		return pool[pool.Count - 1];
	}

	// ---- Helpers ----

	private static string Need(long cost)
	{
		if (!G.MS || !G.MS.NotEnoughMoney(cost))
		{
			return null;
		}
		return "You need " + Money(cost) + " for this.";
	}

	private static long Weekly(gameScript g)
	{
		if (g.sellsPerWeek == null || g.sellsPerWeek.Length == 0)
		{
			return 0L;
		}
		return Math.Max(0, g.sellsPerWeek[0]);
	}

	private static void HypeInDevelopment(float hype)
	{
		foreach (gameScript g in IndustryCore.MyGamesInDevelopment())
		{
			g.AddHype(hype);
		}
	}

	private static int AddFans(float n)
	{
		int fans = Mathf.Max(0, Mathf.RoundToInt(n));
		if (fans > 0 && (bool)G.MS)
		{
			G.MS.AddFans(fans, -1);
		}
		return fans;
	}

	private static gameScript Game(int id)
	{
		return GameUpdates.FindGame(id);
	}

	private static string Name(int gameId)
	{
		gameScript g = Game(gameId);
		if (!g)
		{
			return "the game";
		}
		return g.GetNameSimple();
	}

	private static string StudioName(int id)
	{
		publisherScript p = IndustryCore.Studio(id);
		if (!p)
		{
			return "the studio";
		}
		return p.GetName();
	}

	private static IndustryChoice Choice(string title, string line1, string line2, Func<IndustryContext, string> apply, Func<IndustryContext, string> problem = null)
	{
		return new IndustryChoice
		{
			Title = title,
			Line1 = line1,
			Line2 = line2,
			Apply = apply,
			Problem = problem
		};
	}

	private static float DevProgress(gameScript g)
	{
		return Mathf.Max(0f, UI.Pages.CurrentChartsPage.DevProgress(g, out int _));
	}

	// ---- The events ----

	private static List<IndustryEventDef> Build()
	{
		return new List<IndustryEventDef>
		{
			Cassette(),
			TypeIn(),
			Arcade(),
			HomeComputer(),
			SharedStand(),
			Clone(),
			BiddingWar(),
			Rescue(),
			Craze(),
			Fatigue(),
			Hit(),
			PriceCut(),
			Delay()
		}.Concat(Early()).Concat(Consoles()).ToList();
	}

	// Two teenagers send you their game: publish it (a gamble), hire them, or pass.
	private static IndustryEventDef Cassette()
	{
		return new IndustryEventDef
		{
			Id = "cassette",
			To = 2002,
			Cooldown = 60,
			Weight = 1.2f,
			Setup = (IndustryContext c) =>
			{
				c.Amount = Scaled(3000.0);
				c.Amount2 = Scaled(450.0);
				return true;
			},
			Title = (IndustryContext c) => "A " + Cap(IndustryCore.Media()) + " in the Post",
			Text = (IndustryContext c) => "Two teenagers have sent you their first game on a " + IndustryCore.Media() + ". It's rough around the edges, but there's something there. They'd love you to publish it, or to come and work for you.",
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Publish It", "Costs " + Money(c.Amount), "A gamble: about " + Money(c.Amount2) + " a week for 20 weeks if it catches on", delegate(IndustryContext x)
				{
					G.MS.Pay(x.Amount, 7);
					bool hit = UnityEngine.Random.value < 0.65f;
					long week = (long)((float)x.Amount2 * (hit ? R(0.7f, 1.5f) : 0.2f));
					IndustryCore.State.Streams.Add(new IncomeStream
					{
						Label = "The teenagers' game",
						Fixed = week,
						WeeksLeft = 20,
						Category = 3
					});
					int fans = AddFans((hit ? 600f : 150f) * R(0.8f, 1.3f));
					return hit ? ("Their game finds an audience: about " + Money(week) + " a week for 20 weeks, and " + fans + " new fans.") : ("It barely sells: about " + Money(week) + " a week. Still, two kids are over the moon.");
				}, (IndustryContext x) => Need(x.Amount)),
				Choice("Hire Them", "Two applicants join the job market", "A game designer and a programmer, keen to learn", delegate
				{
					int added = Applicants();
					return (added > 0) ? "They're in the job market now (Staff > hire): a game designer and a programmer." : "They couldn't come after all.";
				}),
				Choice("Pass", "Nothing changes", "They'll try another studio", (IndustryContext x) => "You send the " + IndustryCore.Media() + " back with a kind note.")
			}
		};
	}

	private static string Cap(string s)
	{
		return string.Join(" ", from w in s.Split(' ')
			select (w.Length > 0) ? (char.ToUpper(w[0]) + w.Substring(1)) : w);
	}

	// Applicants in the job market, a little better than usual: by default a game designer and a programmer
	// (profession 0 = game designer, 1 = programmer; "first" picks the first one's).
	private static int Applicants(int count = 2, int first = 0)
	{
		arbeitsmarkt market = UnityEngine.Object.FindObjectOfType<arbeitsmarkt>();
		if (!market)
		{
			return 0;
		}
		int added = 0;
		for (int n = 0; n < count; n++)
		{
			int i = (first + n) % 2;
			charArbeitsmarkt c = market.CreateArbeitsmarktItem();
			if ((bool)c)
			{
				c.Create(null);
				c.beruf = i;
				if (i == 0)
				{
					c.s_gamedesign = Mathf.Max(c.s_gamedesign, R(35f, 45f));
				}
				else
				{
					c.s_programmieren = Mathf.Max(c.s_programmieren, R(35f, 45f));
				}
				added++;
			}
		}
		return added;
	}

	// A magazine wants to print your game's code for readers to type in.
	private static IndustryEventDef TypeIn()
	{
		return new IndustryEventDef
		{
			Id = "typein",
			To = 1988,
			Cooldown = 70,
			Setup = delegate(IndustryContext c)
			{
				gameScript g = IndustryCore.MyGamesOnMarket().OrderByDescending(Weekly).FirstOrDefault();
				if (!g)
				{
					return false;
				}
				c.GameId = g.myID;
				c.Number = UnityEngine.Random.Range(0, Magazines.Length);
				c.Amount = Scaled(4000.0);
				c.Amount2 = (long)(1500f * R(0.8f, 1.3f));
				return true;
			},
			Title = (IndustryContext c) => "Print Your Code?",
			Text = (IndustryContext c) => Magazines[c.Number] + " wants to print the code of " + Name(c.GameId) + " for readers to type in at home. Great publicity, but some players won't buy what they can type in for free.",
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Let Them Print It", "About " + c.Amount2 + " new fans, hype +5 for your games in development", Name(c.GameId) + " sells 10% less for 8 weeks", delegate(IndustryContext x)
				{
					int fans = AddFans(x.Amount2);
					HypeInDevelopment(5f);
					IndustryCore.AddMod(Game(x.GameId), -0.1f, 8);
					return "Readers everywhere are typing in " + Name(x.GameId) + ": " + fans + " new fans.";
				}),
				Choice("Buy an Advert Instead", "Costs " + Money(c.Amount), "About " + c.Amount2 / 3 + " new fans, hype +3 for your games in development", delegate(IndustryContext x)
				{
					G.MS.Pay(x.Amount, 12);
					int fans = AddFans(x.Amount2 / 3);
					HypeInDevelopment(3f);
					return "Your advert runs in " + Magazines[x.Number] + ": " + fans + " new fans.";
				}, (IndustryContext x) => Need(x.Amount)),
				Choice("No Thanks", "Nothing changes", "The code stays yours", (IndustryContext x) => "You keep your code to yourself.")
			}
		};
	}

	// An arcade chain wants a cabinet version of your hit.
	private static IndustryEventDef Arcade()
	{
		return new IndustryEventDef
		{
			Id = "arcade",
			To = 1997,
			Cooldown = 60,
			Setup = delegate(IndustryContext c)
			{
				gameScript g = (from x in IndustryCore.MyGamesOnMarket()
					where x.gameTyp == 0 && !x.arcade && !x.handy && x.reviewTotal >= 60 && x.weeksOnMarket <= 104
					orderby x.reviewTotal descending
					select x).FirstOrDefault();
				if (!g)
				{
					return false;
				}
				c.GameId = g.myID;
				c.Number = UnityEngine.Random.Range(0, Operators.Length);
				c.Amount = (long)((float)Scaled(5000.0) * (1f + (float)g.reviewTotal / 50f) * (1f + (float)g.gameSize * 0.5f));
				return true;
			},
			Title = (IndustryContext c) => "Cabinet Deal",
			Text = (IndustryContext c) => Operators[c.Number] + " wants " + Name(c.GameId) + " in its arcades: a cabinet version under its own name. It offers a fee up front, or a share of the coins.",
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Take the Fee", Money(c.Amount) + " now", "Some new fans", delegate(IndustryContext x)
				{
					G.MS.Earn(x.Amount, 2);
					int fans = AddFans(300f);
					return Operators[x.Number] + " pays " + Money(x.Amount) + ". " + fans + " players discover " + Name(x.GameId) + " in the arcades.";
				}),
				Choice("Share the Coins", "About " + Money(c.Amount / 7) + " a week for 14 weeks", "Twice the fee, spread out", delegate(IndustryContext x)
				{
					IndustryCore.State.Streams.Add(new IncomeStream
					{
						Label = Name(x.GameId) + " cabinets",
						Fixed = x.Amount / 7,
						WeeksLeft = 14,
						Category = 2
					});
					AddFans(300f);
					return "The cabinets go in: about " + Money(x.Amount / 7) + " a week for 14 weeks.";
				}),
				Choice("Decline", "Nothing changes", "It stays a home game", (IndustryContext x) => "You turn " + Operators[x.Number] + " down.")
			}
		};
	}

	// A hardware maker wants games for its new machine: a free dev kit, if you release on it within a year.
	private static IndustryEventDef HomeComputer()
	{
		return new IndustryEventDef
		{
			Id = "machine",
			From = 1977,
			To = 2006,
			Cooldown = 40,
			Setup = delegate(IndustryContext c)
			{
				platformScript[] all = (G.MS ? G.MS.arrayPlatformsScripts : null);
				if (all == null)
				{
					return false;
				}
				List<platformScript> fresh = all.Where((platformScript p) => (bool)p && p.isUnlocked && !p.vomMarktGenommen && !p.inBesitz && p.ownerID != G.MS.myID && p.typ <= 2 && Now - (p.date_year * 48 + (p.date_month - 1) * 4) >= 0 && Now - (p.date_year * 48 + (p.date_month - 1) * 4) <= 30 && !IndustryCore.State.Promises.Any((PlatformPromise x) => x.PlatformId == p.myID)).ToList();
				if (fresh.Count == 0)
				{
					return false;
				}
				platformScript pick = Pick(fresh);
				c.PlatformId = pick.myID;
				c.Amount = pick.GetPrice();
				c.Amount2 = Scaled(8000.0);
				return c.Amount > 0;
			},
			Title = (IndustryContext c) => "A New Machine Wants Games",
			Text = delegate(IndustryContext c)
			{
				platformScript p2 = GameUpdates.Platform(c.PlatformId);
				return (p2 ? p2.GetManufacturer() : "A hardware maker") + " has just launched the " + (p2 ? p2.GetName() : "new machine") + " and wants games for it. They'll send you a dev kit for free (it normally costs " + Money(c.Amount) + ") if you promise to release a game on it within a year.";
			},
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Take the Dev Kit", "A free dev kit (worth " + Money(c.Amount) + ")", "Release a game on it within a year, or pay for it", (IndustryContext x) => Kit(x, 48, x.Amount, 0L)),
				Choice("Ask for a Launch Bonus Too", "The kit and " + Money(c.Amount2) + " now", "Release within 9 months, or pay " + Money(c.Amount * 3 / 2), (IndustryContext x) => Kit(x, 36, x.Amount * 3 / 2, x.Amount2)),
				Choice("No Thanks", "Nothing changes", "You can still buy the kit later", (IndustryContext x) => "You pass on the offer for now.")
			}
		};
	}

	private static string Kit(IndustryContext c, int weeks, long penalty, long bonus)
	{
		platformScript p = GameUpdates.Platform(c.PlatformId);
		if (!p)
		{
			return "The offer has gone.";
		}
		p.inBesitz = true;
		if (bonus > 0)
		{
			G.MS.Earn(bonus, 1);
		}
		IndustryCore.State.Promises.Add(new PlatformPromise
		{
			PlatformId = p.myID,
			Start = Now,
			Deadline = Now + weeks,
			Price = penalty
		});
		return "The " + p.GetName() + " dev kit arrives" + ((bonus > 0) ? (" with " + Money(bonus)) : "") + ". Release a game on it within " + ((weeks >= 48) ? "a year" : (weeks / 4 + " months")) + ", or pay " + Money(penalty) + ".";
	}

	// A small studio asks to share your stand at the summer trade fair.
	private static IndustryEventDef SharedStand()
	{
		return new IndustryEventDef
		{
			Id = "stand",
			From = 1977,
			Cooldown = 40,
			Setup = delegate(IndustryContext c)
			{
				if (!G.MS || G.MS.month != 6)
				{
					return false;
				}
				List<publisherScript> small = IndustryCore.Studios().Where((publisherScript p) => p.developer && p.stars <= 60f && IndustryCore.Tier(IndustryCore.Standing(p)) > 0 && (bool)p.FindGameInDevelopment()).ToList();
				if (small.Count == 0)
				{
					return false;
				}
				publisherScript pick = Pick(small);
				c.StudioId = pick.myID;
				c.GameId = pick.FindGameInDevelopment().myID;
				c.Amount = Scaled(3000.0);
				return true;
			},
			Title = (IndustryContext c) => "Share a Stand?",
			Text = (IndustryContext c) => StudioName(c.StudioId) + " can't afford a stand of its own at this summer's trade fair, and asks to share yours. They'd pay half and show " + Name(c.GameId) + ".",
			Default = 1,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Share the Stand", "They pay " + Money(c.Amount) + ", hype +3 for both", "Standing with them +12", delegate(IndustryContext x)
				{
					G.MS.Earn(x.Amount, 1);
					HypeInDevelopment(3f);
					Game(x.GameId)?.AddHype(3f);
					IndustryCore.Change(IndustryCore.Studio(x.StudioId), 12, "Shared your trade-fair stand");
					return "You share the stand with " + StudioName(x.StudioId) + ". The crowds like what both of you show.";
				}),
				Choice("Keep It to Yourself", "Nothing changes for you", "Standing with them -3", delegate(IndustryContext x)
				{
					IndustryCore.Change(IndustryCore.Studio(x.StudioId), -3, "Didn't share your stand");
					return StudioName(x.StudioId) + " stays home this year.";
				})
			}
		};
	}

	// A studio's new game looks a lot like yours: sue, outsell, license, or ignore.
	private static IndustryEventDef Clone()
	{
		return new IndustryEventDef
		{
			Id = "clone",
			Cooldown = 30,
			Weight = 1.2f,
			Setup = delegate(IndustryContext c)
			{
				List<gameScript> mine = IndustryCore.MyGamesOnMarket().Where((gameScript g) => g.gameTyp == 0 && g.reviewTotal >= 50 && g.weeksOnMarket >= 4).ToList();
				if (mine.Count == 0)
				{
					return false;
				}
				List<KeyValuePair<gameScript, gameScript>> pairs = new List<KeyValuePair<gameScript, gameScript>>();
				foreach (gameScript other in IndustryCore.AllGames())
				{
					if (!other.isOnMarket || other.weeksOnMarket > 6 || other.IsMyGame() || other.developerID < 0)
					{
						continue;
					}
					gameScript like = mine.FirstOrDefault((gameScript m) => m.maingenre == other.maingenre && m.gameMainTheme == other.gameMainTheme);
					if ((bool)like && (bool)IndustryCore.Studio(other.developerID))
					{
						pairs.Add(new KeyValuePair<gameScript, gameScript>(like, other));
					}
				}
				if (pairs.Count == 0)
				{
					return false;
				}
				KeyValuePair<gameScript, gameScript> pair = Pick(pairs);
				publisherScript studio = IndustryCore.Studio(pair.Value.developerID);
				c.GameId = pair.Key.myID;
				c.Game2 = pair.Value.myID;
				c.StudioId = studio.myID;
				c.Amount = Scaled(10000.0);
				c.Amount2 = (long)((float)Scaled(25000.0) * (1f + (float)pair.Value.gameSize));
				c.Share = Mathf.Clamp(0.55f - studio.stars * 0.0025f + ((IndustryCore.Tier(IndustryCore.Standing(studio)) == 0) ? 0.1f : 0f), 0.2f, 0.8f);
				c.Number = (int)Scaled(6000.0) * (1 + pair.Value.gameSize);
				return true;
			},
			Title = (IndustryContext c) => "A Copy of Your Game",
			Text = (IndustryContext c) => StudioName(c.StudioId) + "'s new " + Name(c.Game2) + " is " + IndustryCore.AGenreGame(Game(c.Game2)?.maingenre ?? (-1)) + " on the same topic as your " + Name(c.GameId) + ", and players say it looks a lot like it.",
			Default = 3,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Sue Them", "Lawyers cost " + Money(c.Amount), "About " + Mathf.RoundToInt(c.Share * 100f) + "% to win " + Money(c.Amount2) + "; standing with them drops", delegate(IndustryContext x)
				{
					G.MS.Pay(x.Amount, 13);
					publisherScript p = IndustryCore.Studio(x.StudioId);
					if (UnityEngine.Random.value < x.Share)
					{
						G.MS.Earn(x.Amount2, 1);
						gameScript theirs = Game(x.Game2);
						if ((bool)theirs)
						{
							HolyCompat.ShiftUserReviews(theirs, -0.05f);
						}
						if ((bool)p)
						{
							IndustryCore.AddStars(p, -4f);
						}
						IndustryCore.Change(p, -25, "You sued them over " + Name(x.GameId) + " and won");
						return "You win: " + StudioName(x.StudioId) + " pays " + Money(x.Amount2) + ", and players turn on " + Name(x.Game2) + ".";
					}
					IndustryCore.Change(p, -20, "You sued them over " + Name(x.GameId) + " and lost");
					return "You lose: the court says " + Name(x.Game2) + " is different enough. The lawyers still want paying.";
				}, (IndustryContext x) => Need(x.Amount)),
				Choice("Outsell It", "Marketing costs " + Money(Scaled(8000.0)), Name(c.GameId) + " sells 15% more for 8 weeks", delegate(IndustryContext x)
				{
					G.MS.Pay(Scaled(8000.0), 12);
					IndustryCore.AddMod(Game(x.GameId), 0.15f, 8);
					return "Your campaign reminds everyone which one came first.";
				}, (IndustryContext x) => Need(Scaled(8000.0))),
				Choice("License It to Them", "They pay " + Money(c.Number), "Standing with them +10", delegate(IndustryContext x)
				{
					G.MS.Earn(x.Number, 2);
					IndustryCore.Change(IndustryCore.Studio(x.StudioId), 10, "Licensed " + Name(x.GameId) + " to them");
					return StudioName(x.StudioId) + " pays " + Money(x.Number) + " for the rights, and is grateful.";
				}),
				Choice("Ignore It", "Nothing to pay", Name(c.GameId) + " sells 5% less for 8 weeks", delegate(IndustryContext x)
				{
					IndustryCore.AddMod(Game(x.GameId), -0.05f, 8);
					return "Some buyers pick up the copy instead.";
				})
			}
		};
	}

	// Two publishers bid to market your game in development: an advance now for a share of its revenue later.
	private static IndustryEventDef BiddingWar()
	{
		return new IndustryEventDef
		{
			Id = "bidding",
			Cooldown = 26,
			Weight = 1.1f,
			Setup = delegate(IndustryContext c)
			{
				gameScript g = (from x in IndustryCore.MyGamesInDevelopment()
					where x.developerID == G.MS.myID && (x.publisherID == -1 || x.publisherID == G.MS.myID) && !IndustryCore.State.DealtGames.Contains(x.myID) && DevProgress(x) >= 0.3f
					orderby x.hype descending
					select x).FirstOrDefault();
				if (!g)
				{
					return false;
				}
				List<publisherScript> pubs = (from p in IndustryCore.Studios()
					where p.publisher && IndustryCore.Tier(IndustryCore.Standing(p)) > 0 && (!p.onlyMobile || g.handy)
					orderby p.stars / 20f + IndustryCore.Standing(p) / 50f + R(0f, 1.5f) descending
					select p).Take(2).ToList();
				if (pubs.Count < 2)
				{
					return false;
				}
				c.GameId = g.myID;
				c.StudioId = pubs[0].myID;
				c.Studio2 = pubs[1].myID;
				c.Amount = Advance(g, pubs[0]);
				c.Amount2 = Advance(g, pubs[1]);
				c.Share = Mathf.Round(R(0.08f, 0.2f) * 100f) / 100f;
				c.Share2 = Mathf.Round(R(0.08f, 0.2f) * 100f) / 100f;
				c.Number = UnityEngine.Random.Range(5, 11);
				return true;
			},
			Title = (IndustryContext c) => "Publishers Bid for " + Name(c.GameId),
			Text = (IndustryContext c) => "Word of " + Name(c.GameId) + " has got around. " + StudioName(c.StudioId) + " and " + StudioName(c.Studio2) + " both want to market it: each pays an advance now, for a share of its revenue for half a year after it comes out.",
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Go with " + StudioName(c.StudioId), "Advance " + Money(c.Amount) + ", hype +" + c.Number, Mathf.RoundToInt(c.Share * 100f) + "% of its revenue for 26 weeks after release", (IndustryContext x) => Bid(x, x.StudioId, x.Studio2, x.Amount, x.Share)),
				Choice("Go with " + StudioName(c.Studio2), "Advance " + Money(c.Amount2) + ", hype +" + c.Number, Mathf.RoundToInt(c.Share2 * 100f) + "% of its revenue for 26 weeks after release", (IndustryContext x) => Bid(x, x.Studio2, x.StudioId, x.Amount2, x.Share2)),
				Choice("Turn Both Down", "Keep all its revenue", "Standing with both -2", delegate(IndustryContext x)
				{
					IndustryCore.State.DealtGames.Add(x.GameId);
					IndustryCore.Change(IndustryCore.Studio(x.StudioId), -2, "Turned down their offer for " + Name(x.GameId));
					IndustryCore.Change(IndustryCore.Studio(x.Studio2), -2, "Turned down their offer for " + Name(x.GameId));
					return "You keep " + Name(x.GameId) + " to yourself.";
				})
			}
		};
	}

	private static long Advance(gameScript g, publisherScript p)
	{
		return (long)((float)Scaled(12000.0) * (1f + (float)g.gameSize) * (0.6f + p.stars * 0.01f) * R(0.85f, 1.2f));
	}

	private static string Bid(IndustryContext c, int winner, int loser, long advance, float share)
	{
		gameScript g = Game(c.GameId);
		IndustryCore.State.DealtGames.Add(c.GameId);
		if (!g)
		{
			return "The game is gone.";
		}
		G.MS.Earn(advance, 2);
		g.AddHype(c.Number);
		IndustryCore.State.Streams.Add(new IncomeStream
		{
			Label = StudioName(winner) + "'s share of " + g.GetNameSimple(),
			StudioId = winner,
			GameId = g.myID,
			Share = share,
			WeeksLeft = 26,
			Out = true
		});
		IndustryCore.Change(IndustryCore.Studio(winner), 12, "Won the bid for " + g.GetNameSimple());
		IndustryCore.Change(IndustryCore.Studio(loser), -4, "Lost the bid for " + g.GetNameSimple());
		return StudioName(winner) + " pays " + Money(advance) + " and starts the hype for " + g.GetNameSimple() + ". It takes " + Mathf.RoundToInt(share * 100f) + "% of its revenue for 26 weeks after release.";
	}

	// A struggling studio can't finish its announced game: fund it for a share, lend a little, or decline.
	private static IndustryEventDef Rescue()
	{
		return new IndustryEventDef
		{
			Id = "rescue",
			Cooldown = 30,
			Setup = delegate(IndustryContext c)
			{
				List<publisherScript> poor = IndustryCore.Studios().Where((publisherScript p) => p.developer && p.stars <= 50f && (!IndustryCore.State.Cooldowns.TryGetValue("rescue:" + p.myID, out var until) || until <= Now) && (bool)p.FindAngekuendigtesGame()).ToList();
				if (poor.Count == 0)
				{
					return false;
				}
				publisherScript pick = Pick(poor);
				gameScript g = pick.FindAngekuendigtesGame();
				c.StudioId = pick.myID;
				c.GameId = g.myID;
				c.Amount = (long)((float)Scaled(20000.0) * (1f + (float)g.gameSize));
				IndustryCore.State.Cooldowns["rescue:" + pick.myID] = Now + 156;
				return true;
			},
			Title = (IndustryContext c) => StudioName(c.StudioId) + " Needs Help",
			Text = (IndustryContext c) => StudioName(c.StudioId) + " has run out of money finishing " + Name(c.GameId) + ". They ask you to fund the rest, for a share of its revenue for a year after it comes out.",
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Fund It", "Costs " + Money(c.Amount), "25% of its revenue for a year; standing +20", (IndustryContext x) => Fund(x, x.Amount, 0.25f, 20, 4), (IndustryContext x) => Need(x.Amount)),
				Choice("A Smaller Loan", "Costs " + Money(c.Amount / 3), "8% of its revenue for a year; standing +8", (IndustryContext x) => Fund(x, x.Amount / 3, 0.08f, 8, 1), (IndustryContext x) => Need(x.Amount / 3)),
				Choice("Decline", "Nothing to pay", "Standing -5; they cut back and delay it", delegate(IndustryContext x)
				{
					publisherScript p = IndustryCore.Studio(x.StudioId);
					if ((bool)p)
					{
						p.newGameInWeeks += 8;
					}
					IndustryCore.Change(p, -5, "Didn't help them finish " + Name(x.GameId));
					return StudioName(x.StudioId) + " cuts back and delays " + Name(x.GameId) + ".";
				})
			}
		};
	}

	private static string Fund(IndustryContext c, long cost, float share, int standing, int weeksSooner)
	{
		publisherScript p = IndustryCore.Studio(c.StudioId);
		gameScript g = Game(c.GameId);
		if (!p || !g)
		{
			return "Too late: the studio or its game is gone.";
		}
		G.MS.Pay(cost, 33);
		IndustryCore.State.Streams.Add(new IncomeStream
		{
			Label = "Your share of " + g.GetNameSimple(),
			StudioId = p.myID,
			GameId = g.myID,
			Share = share,
			WeeksLeft = 52,
			Category = 2
		});
		g.AddHype(5f);
		p.newGameInWeeks = Mathf.Max(1, p.newGameInWeeks - weeksSooner);
		IndustryCore.Change(p, standing, "You funded " + g.GetNameSimple());
		return p.GetName() + " can finish " + g.GetNameSimple() + ". You'll get " + Mathf.RoundToInt(share * 100f) + "% of its revenue for a year after it's out.";
	}

	// ---- Headlines ----

	private static IndustryEventDef Craze()
	{
		return new IndustryEventDef
		{
			Id = "craze",
			Headline = true,
			Cooldown = 20,
			Setup = delegate(IndustryContext c)
			{
				genres gen = IndustryCore.Genres;
				if (!gen || gen.genres_BELIEBTHEIT == null)
				{
					return false;
				}
				List<int> pool = new List<int>();
				for (int i = 0; i < gen.genres_BELIEBTHEIT.Length; i++)
				{
					if (gen.genres_UNLOCK[i] && gen.genres_BELIEBTHEIT[i] < 80f && i != G.MS.trendGenre)
					{
						pool.Add(i);
					}
				}
				if (pool.Count == 0)
				{
					return false;
				}
				c.Number = Pick(pool);
				return true;
			},
			Apply = delegate(IndustryContext c)
			{
				genres gen2 = IndustryCore.Genres;
				gen2.genres_BELIEBTHEIT[c.Number] = Mathf.Min(100f, gen2.genres_BELIEBTHEIT[c.Number] + 12f);
				gen2.genres_BELIEBTHEIT_SOLL[c.Number] = true;
				int weeks = IndustryCore.MakeTrend(c.Number);
				return "<b>" + Cap(IndustryCore.GenreName(c.Number)) + " craze:</b> players can't get enough of " + IndustryCore.GenreGames(c.Number) + " right now" + ((weeks > 0) ? (". It's the trend for the next " + weeks + " weeks.") : ".");
			}
		};
	}

	private static IndustryEventDef Fatigue()
	{
		return new IndustryEventDef
		{
			Id = "fatigue",
			Headline = true,
			Cooldown = 24,
			Weight = 0.8f,
			Setup = delegate(IndustryContext c)
			{
				genres gen = IndustryCore.Genres;
				if (!gen || gen.genres_BELIEBTHEIT == null)
				{
					return false;
				}
				List<int> pool = new List<int>();
				for (int i = 0; i < gen.genres_BELIEBTHEIT.Length; i++)
				{
					if (gen.genres_UNLOCK[i] && gen.genres_BELIEBTHEIT[i] >= 60f)
					{
						pool.Add(i);
					}
				}
				if (pool.Count == 0)
				{
					return false;
				}
				c.Number = Pick(pool);
				return true;
			},
			Apply = delegate(IndustryContext c)
			{
				genres gen2 = IndustryCore.Genres;
				gen2.genres_BELIEBTHEIT[c.Number] = Mathf.Max(0f, gen2.genres_BELIEBTHEIT[c.Number] - 12f);
				gen2.genres_BELIEBTHEIT_SOLL[c.Number] = false;
				bool out_ = IndustryCore.MakeAntiTrend(c.Number);
				return "Players are growing tired of " + IndustryCore.GenreGames(c.Number) + (out_ ? ": they're out of fashion for now." : ".");
			}
		};
	}

	private static IndustryEventDef Hit()
	{
		return new IndustryEventDef
		{
			Id = "hit",
			Headline = true,
			Cooldown = 10,
			Weight = 1.2f,
			Setup = delegate(IndustryContext c)
			{
				gameScript g = (from x in IndustryCore.AllGames()
					where x.isOnMarket && !x.IsMyGame() && x.weeksOnMarket <= 12 && x.developerID >= 0 && (bool)IndustryCore.Studio(x.developerID)
					orderby Weekly(x) descending
					select x).FirstOrDefault();
				if (!g || Weekly(g) <= 0)
				{
					return false;
				}
				c.GameId = g.myID;
				c.StudioId = g.developerID;
				return true;
			},
			Apply = delegate(IndustryContext c)
			{
				gameScript g2 = Game(c.GameId);
				publisherScript p = IndustryCore.Studio(c.StudioId);
				if ((bool)p)
				{
					IndustryCore.AddStars(p, 2f);
				}
				genres gen = IndustryCore.Genres;
				if ((bool)gen && (bool)g2 && g2.maingenre >= 0 && g2.maingenre < gen.genres_BELIEBTHEIT.Length)
				{
					gen.genres_BELIEBTHEIT[g2.maingenre] = Mathf.Min(100f, gen.genres_BELIEBTHEIT[g2.maingenre] + 4f);
				}
				return "<b>" + StudioName(c.StudioId) + "</b>'s " + Name(c.GameId) + " is flying off the shelves.";
			}
		};
	}

	private static IndustryEventDef PriceCut()
	{
		return new IndustryEventDef
		{
			Id = "pricecut",
			Headline = true,
			From = 1977,
			Cooldown = 16,
			Weight = 0.7f,
			Setup = delegate(IndustryContext c)
			{
				platformScript[] all = (G.MS ? G.MS.arrayPlatformsScripts : null);
				List<platformScript> pool = ((all == null) ? new List<platformScript>() : all.Where((platformScript p) => (bool)p && p.isUnlocked && !p.vomMarktGenommen && p.ownerID != G.MS.myID && p.marktanteil > 0f && p.typ != 3).ToList());
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
				p2.AddHype(8f);
				return "<b>" + p2.GetName() + "</b> gets a price cut: more players can afford one.";
			}
		};
	}

	private static IndustryEventDef Delay()
	{
		return new IndustryEventDef
		{
			Id = "delay",
			Headline = true,
			Cooldown = 8,
			Setup = delegate(IndustryContext c)
			{
				List<publisherScript> pool = IndustryCore.Studios().Where((publisherScript p) => p.developer && (bool)p.FindAngekuendigtesGame()).ToList();
				if (pool.Count == 0)
				{
					return false;
				}
				publisherScript pick = Pick(pool);
				c.StudioId = pick.myID;
				c.GameId = pick.FindAngekuendigtesGame().myID;
				c.Number = UnityEngine.Random.Range(4, 13);
				return true;
			},
			Apply = delegate(IndustryContext c)
			{
				publisherScript p = IndustryCore.Studio(c.StudioId);
				if (!p)
				{
					return null;
				}
				p.newGameInWeeks += c.Number;
				return "<b>" + p.GetName() + "</b> delays " + Name(c.GameId) + " by about " + Mathf.Max(1, c.Number / 4) + ((c.Number / 4 == 1) ? " month." : " months.");
			}
		};
	}
}
