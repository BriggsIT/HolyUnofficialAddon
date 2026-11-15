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

	// Never picked at random: started by something else (your arch-rival's moves, IndustryRivals).
	public bool Scripted;
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
		List<IndustryEventDef> pool = Defs.Where((IndustryEventDef d) => !d.Headline && !d.Scripted && Ready(d)).ToList();
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
		{ "shops", "A new chain of shops" },
		{ "rv_poach", "Poaching Attempt (arch-rival)" },
		{ "rv_copy", "Copycat (arch-rival)" },
		{ "rv_clash", "Release Clash (arch-rival)" },
		{ "rv_taunt", "Trash Talk (arch-rival)" },
		{ "cp_clash", "Release Clash (competitor)" },
		{ "cp_talent", "Talent Race (competitor)" },
		{ "cp_price", "Price War (competitor)" },
		{ "cp_idea", "Same Idea (competitor)" },
		{ "modteam", "The Mod Team" },
		{ "coverdisc", "Cover Disc" },
		{ "cardbundle", "Graphics Card Bundle" },
		{ "hidden", "Hidden Content" },
		{ "dotcom", "The dot-com boom" },
		{ "dotbust", "The dot-com bust" },
		{ "cafes", "Internet cafés" },
		{ "usedgames", "The used-games market" },
		{ "handheld2", "The handheld revival" },
		{ "enginereq", "Engine Request" },
		{ "auction", "Closure Auction" },
		{ "starstudio", "A Star Leaves" },
		{ "fundport", "Fund a Port" },
		{ "cdburn", "CD Burners" },
		{ "hwquit", "A Console Maker Quits" }
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

	// ---- Money that grows with your studio ----
	// Holy's price curve rises about 5x from 1980 to 2008, but a studio's income can rise 2,000x, so an amount that
	// matters in 1980 means nothing by 1995. Events about one of your games are priced from what such a game earns,
	// and caps from what you earned last year; the old fixed amounts stay as the minimum (early game is unchanged).

	// What a game of this size of yours usually earns over its life: the average of your last 3 released games of the
	// same size that have had time to sell, or 3x this game's development cost if there are none yet.
	private static long ExpectedRevenue(gameScript g)
	{
		if (!g)
		{
			return 0L;
		}
		List<gameScript> done = (from x in IndustryCore.AllGames()
			where x != g && x.IsMyGame() && x.gameSize == g.gameSize && x.umsatzTotal > 0 && !x.inDevelopment && !x.typ_contractGame && Standalone(x) && (!x.isOnMarket || x.weeksOnMarket >= 20)
			orderby x.date_year * 12 + x.date_month descending
			select x).Take(3).ToList();
		if (done.Count > 0)
		{
			return (long)done.Average((gameScript x) => (double)x.umsatzTotal);
		}
		return GameUpdates.DevCost(g) * 3;
	}

	// A share of what the game should earn, never below the old fixed amount.
	private static long OfRevenue(gameScript g, float share, long floor)
	{
		return Math.Max(floor, Prices.Round((double)ExpectedRevenue(g) * (double)share));
	}

	// Everything you earned last year (all income types).
	private static long LastYearIncome()
	{
		long[] f = (G.MS ? G.MS.finanzenJahrLast : null);
		if (f == null || f.Length < 100)
		{
			return 0L;
		}
		long sum = 0L;
		for (int i = 50; i < 100; i++)
		{
			sum += Math.Max(0L, f[i]);
		}
		return sum;
	}

	// A cap that grows with your studio: a share of last year's income, never below the old fixed cap.
	private static long IncomeCap(long floor, float share = 0.02f)
	{
		return Math.Max(floor, (long)((double)LastYearIncome() * (double)share));
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

	// A studio's own game in development. The game's FindGameInDevelopment() can return a game it offers you to
	// publish, or contract work: those never come out on its own countdown, so events skip them.
	private static gameScript TheirGame(publisherScript p)
	{
		if (!p)
		{
			return null;
		}
		gameScript g = p.FindGameInDevelopment();
		if ((bool)g && !g.pubAngebot && !g.auftragsspiel)
		{
			return g;
		}
		return IndustryCore.AllGames().FirstOrDefault((gameScript x) => x.developerID == p.myID && x.inDevelopment && !x.isOnMarket && !x.pubAngebot && !x.auftragsspiel);
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
		}.Concat(Early()).Concat(Consoles()).Concat(RivalMoves()).Concat(CompetitorFights()).Concat(ThreeD()).Concat(Studios3D()).ToList();
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
			// The game they sent is a small studio's real game in development; publishing it turns it into one of the game's
			// own publishing offers (as the game does with NPC games: you negotiate and publish it there).
			Setup = (IndustryContext c) =>
			{
				publisherScript tiny = (from p in IndustryCore.Studios()
					where p.developer && !p.publisher && p.stars <= 25f && !p.IsTochterfirma() && (bool)TheirGame(p) && Offerable(TheirGame(p))
					orderby p.stars
					select p).FirstOrDefault();
				c.StudioId = (tiny ? tiny.myID : (-1));
				c.Game2 = (tiny ? TheirGame(tiny).myID : (-1));
				return true;
			},
			Title = (IndustryContext c) => "A " + Cap(IndustryCore.Media()) + " in the Post",
			Text = (IndustryContext c) => "Two teenagers" + ((c.StudioId >= 0) ? (" from " + StudioName(c.StudioId)) : "") + " have sent you their first game on a " + IndustryCore.Media() + ((c.Game2 >= 0) ? (": " + Name(c.Game2)) : "") + ". It's rough around the edges, but there's something there. They'd love you to publish it, or to come and work for you.",
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Publish It", "It joins your publishing offers", "Negotiate and publish it there", delegate(IndustryContext x)
				{
					gameScript g = Game(x.Game2);
					string problem = OfferProblem(g);
					if (problem != null)
					{
						return problem;
					}
					MakeOffer(g);
					int fans = AddFans(150f * R(0.8f, 1.3f));
					return g.GetNameSimple() + " is in your publishing offers now, with " + StudioName(x.StudioId) + "'s terms: publish it like any other game you're offered. Word gets around that you give newcomers a chance: " + fans + " new fans.";
				}, (IndustryContext x) => OfferProblem(Game(x.Game2))),
				Choice("Hire Them", "Two applicants join the job market", "A game designer and a programmer, keen to learn", delegate
				{
					int added = Applicants();
					return (added > 0) ? "They're in the job market now (Staff > hire): a game designer and a programmer." : "They couldn't come after all.";
				}),
				Choice("Pass", "Nothing changes", "They'll try another studio", (IndustryContext x) => "You send the " + IndustryCore.Media() + " back with a kind note.")
			}
		};
	}

	// A game the game itself could turn into a publishing offer (publisherScript.CreateNewGame2's conditions).
	private static bool Offerable(gameScript g)
	{
		if ((bool)g && g.inDevelopment && !g.isOnMarket && !g.pubAngebot && !g.auftragsspiel && g.gameTyp == 0 && !g.arcade && !g.handy && !g.typ_budget && !g.typ_goty && !g.typ_bundle && !g.typ_bundleAddon && !g.typ_addon && !g.typ_addonStandalone && !g.typ_mmoaddon)
		{
			if (!g.typ_standard && !g.typ_remaster && !g.typ_nachfolger)
			{
				return g.typ_spinoff;
			}
			return true;
		}
		return false;
	}

	private static string OfferProblem(gameScript g)
	{
		if (!g || !Offerable(g))
		{
			return "No small studio has a game you could publish right now.";
		}
		if (!G.Singleplayer)
		{
			return "Not in multiplayer.";
		}
		if (G.MS.pubOffersAmount >= G.MS.GetStudioLevel(G.MS.studioPoints) / 2 + 1)
		{
			return "You already have as many publishing offers as your studio's level allows.";
		}
		return null;
	}

	// The studio's game in development becomes a publishing offer, as the game makes its own: no publisher yet, out of
	// development (so the studio doesn't release it itself), with the game's terms (SetAsPublishingAngebot).
	private static void MakeOffer(gameScript g)
	{
		g.inDevelopment = false;
		g.angekuendigt = false;
		g.publisherID = -1;
		g.pS_ = null;
		G.MS.pubOffersAmount++;
		g.SetAsPublishingAngebot();
		Plugin.Log.LogInfo("Cassette in the Post: " + g.GetNameSimple() + " (game " + g.myID + ") is now a publishing offer");
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
				SetProfession(c, i);
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

	// Create(null) rolls a random profession: its skill 30-40 and the perks that suit it. An event's applicant has a set
	// profession, so the rolled one's skill goes back to a beginner's and perks only another job can have are dropped.
	// A "dev legend" (a rare roll, already in the news by profession) is left as the game made it.
	private static void SetProfession(charArbeitsmarkt c, int profession)
	{
		if (c.beruf == profession)
		{
			return;
		}
		if (c.beruf >= 0 && c.beruf <= 7)
		{
			if (MarketSkill(c, c.beruf) >= 75f)
			{
				return;
			}
			SetMarketSkill(c, c.beruf, R(10f, 20f));
		}
		c.beruf = profession;
		if (c.perks == null)
		{
			return;
		}
		int[][] onlyFor = new int[5][]
		{
			new int[2] { 14, 0 },
			new int[2] { 25, 0 },
			new int[2] { 21, 1 },
			new int[2] { 24, 1 },
			new int[2] { 23, 2 }
		};
		foreach (int[] rule in onlyFor)
		{
			if (rule[0] < c.perks.Length && profession != rule[1])
			{
				c.perks[rule[0]] = false;
			}
		}
		if (3 < c.perks.Length && profession > 1)
		{
			c.perks[3] = false;
		}
	}

	private static float MarketSkill(charArbeitsmarkt c, int skill)
	{
		return skill switch
		{
			0 => c.s_gamedesign,
			1 => c.s_programmieren,
			2 => c.s_grafik,
			3 => c.s_sound,
			4 => c.s_pr,
			5 => c.s_gametests,
			6 => c.s_technik,
			_ => c.s_forschen,
		};
	}

	private static void SetMarketSkill(charArbeitsmarkt c, int skill, float v)
	{
		switch (skill)
		{
		case 0:
			c.s_gamedesign = v;
			break;
		case 1:
			c.s_programmieren = v;
			break;
		case 2:
			c.s_grafik = v;
			break;
		case 3:
			c.s_sound = v;
			break;
		case 4:
			c.s_pr = v;
			break;
		case 5:
			c.s_gametests = v;
			break;
		case 6:
			c.s_technik = v;
			break;
		default:
			c.s_forschen = v;
			break;
		}
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
				Choice("Let Them Print It", "About " + c.Amount2 + " new fans, hype +5 for your games in development", "Readers who type it in won't buy it", delegate(IndustryContext x)
				{
					int fans = AddFans(x.Amount2);
					HypeInDevelopment(5f);
					IndustryReal.Saturate(Game(x.GameId), 0.1f);
					return "Readers everywhere are typing in " + Name(x.GameId) + ": " + fans + " new fans, though the ones who type it in won't buy it.";
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
					where x.gameTyp == 0 && !x.arcade && !x.handy && x.reviewTotal >= 60 && x.weeksOnMarket <= 104 && !x.ExistAutomatenspiel()
					orderby x.reviewTotal descending
					select x).FirstOrDefault();
				if (!g)
				{
					return false;
				}
				c.GameId = g.myID;
				c.Number = UnityEngine.Random.Range(0, Operators.Length);
				c.Amount = OfRevenue(g, 0.03f, (long)((float)Scaled(5000.0) * (1f + (float)g.reviewTotal / 50f) * (1f + (float)g.gameSize * 0.5f)));
				return true;
			},
			Title = (IndustryContext c) => "Cabinet Deal",
			Text = (IndustryContext c) => Operators[c.Number] + " wants " + Name(c.GameId) + " in its arcades: a cabinet version under its own name. It offers a fee up front, or a share of the coins. It would be the game's arcade version: an arcade hit helps the home game sell, but you couldn't port it to arcades yourself.",
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Take the Fee", Money(c.Amount) + " now", "Some new fans; it's the game's arcade version", delegate(IndustryContext x)
				{
					G.MS.Earn(x.Amount, 2);
					ArcadeVersion(Game(x.GameId));
					int fans = AddFans(300f);
					return Operators[x.Number] + " pays " + Money(x.Amount) + ". " + fans + " players discover " + Name(x.GameId) + " in the arcades.";
				}),
				Choice("Share the Coins", "A gamble on the cabinets", "About 1.4x the fee over 14 weeks if players queue up, much less if not", delegate(IndustryContext x)
				{
					bool hit = UnityEngine.Random.value < 0.7f;
					long weekly = x.Amount * (hit ? 14 : 3) / 140;
					IndustryCore.State.Streams.Add(new IncomeStream
					{
						Label = Name(x.GameId) + " cabinets",
						Fixed = weekly,
						WeeksLeft = 14,
						Category = 2
					});
					ArcadeVersion(Game(x.GameId));
					AddFans(300f);
					return hit ? ("Players queue for the cabinets: about " + Money(weekly) + " a week for 14 weeks.") : ("The cabinets gather dust: only about " + Money(weekly) + " a week for 14 weeks.");
				}),
				Choice("Decline", "Nothing changes", "It stays a home game", (IndustryContext x) => "You turn " + Operators[x.Number] + " down.")
			}
		};
	}

	// Boxes in shops that you publish yourself: a publisher deals with its own chips and distributors.
	private static bool SelfPublishedRetail(gameScript g)
	{
		if ((bool)g && (bool)G.MS && g.publisherID == G.MS.myID)
		{
			return g.retailVersion;
		}
		return false;
	}

	// The game counts it as having an arcade version (its "arcade port exists" flag): the home game gets the game's own
	// arcade-version bonus, and the port menu won't offer an arcade port.
	private static void ArcadeVersion(gameScript g)
	{
		if ((bool)g && g.portExist != null && g.portExist.Length > 2)
		{
			g.portExist[2] = true;
		}
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
		if (p.inBesitz)
		{
			// Bought while the offer waited: there's nothing to give, and no promise to make.
			return "You already have the " + p.GetName() + " dev kit, so the maker just thanks you for your support.";
		}
		p.inBesitz = true;
		if (bonus > 0)
		{
			G.MS.Earn(bonus, 2);
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
				List<publisherScript> small = IndustryCore.Studios().Where((publisherScript p) => p.developer && p.stars <= 60f && IndustryCore.Tier(IndustryCore.Standing(p)) > 0 && (bool)TheirGame(p)).ToList();
				if (small.Count == 0)
				{
					return false;
				}
				publisherScript pick = Pick(small);
				c.StudioId = pick.myID;
				c.GameId = TheirGame(pick).myID;
				c.Amount = Scaled(3000.0);
				return true;
			},
			Title = (IndustryContext c) => "Share a Stand?",
			Text = (IndustryContext c) => StudioName(c.StudioId) + " can't afford a stand of its own at July's trade fair, and asks to share yours. They'd pay half of whatever stand you book and show " + Name(c.GameId) + ".",
			Default = 1,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Share the Stand", "They pay half your stand in July", "Hype +3 for both at the fair; standing +12 (if you go)", delegate(IndustryContext x)
				{
					IndustryCore.State.Stand = new StandShare
					{
						StudioId = x.StudioId,
						GameId = x.GameId,
						Year = Year
					};
					IndustryCore.Change(IndustryCore.Studio(x.StudioId), 4, "Agreed to share your trade-fair stand");
					return "Deal: book your stand at July's trade fair as usual, and " + StudioName(x.StudioId) + " pays half and shows " + Name(x.GameId) + " next to your games.";
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
				// Not in the same weeks as the arch-rival's Copycat (both would hit one game).
				if (Fired("rv_copy", 12))
				{
					return false;
				}
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
				// Mostly a competitor's game, when one fits.
				List<KeyValuePair<gameScript, gameScript>> rivalPairs = pairs.Where((KeyValuePair<gameScript, gameScript> kv) => IndustryCompetitors.IsCompetitor(IndustryCore.Studio(kv.Value.developerID))).ToList();
				KeyValuePair<gameScript, gameScript> pair = ((rivalPairs.Count > 0 && UnityEngine.Random.value < 0.75f) ? Pick(rivalPairs) : Pick(pairs));
				publisherScript studio = IndustryCore.Studio(pair.Value.developerID);
				c.GameId = pair.Key.myID;
				c.Game2 = pair.Value.myID;
				c.StudioId = studio.myID;
				c.Amount = OfRevenue(pair.Key, 0.005f, Scaled(10000.0));
				c.Amount2 = OfRevenue(pair.Key, 0.04f, (long)((float)Scaled(25000.0) * (1f + (float)pair.Value.gameSize)));
				c.Share = Mathf.Clamp(0.55f - studio.stars * 0.0025f + ((IndustryCore.Tier(IndustryCore.Standing(studio)) == 0) ? 0.1f : 0f), 0.2f, 0.8f);
				c.Number = (int)Math.Min(int.MaxValue, OfRevenue(pair.Key, 0.02f, Scaled(6000.0) * (1 + pair.Value.gameSize)));
				c.Number2 = (int)Math.Min(int.MaxValue, OfRevenue(pair.Key, 0.015f, Scaled(8000.0)));
				return true;
			},
			Title = (IndustryContext c) => "A Copy of Your Game",
			Text = (IndustryContext c) => StudioName(c.StudioId) + "'s new " + Name(c.Game2) + " is " + IndustryCore.AGenreGame(Game(c.Game2)?.maingenre ?? (-1)) + " on the same topic as your " + Name(c.GameId) + ", and players say it looks a lot like it.",
			Default = 3,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Sue Them", "Lawyers cost " + Money(c.Amount), "About " + Mathf.RoundToInt(c.Share * 100f) + "%: theirs off the shelves, " + Money(c.Amount2), delegate(IndustryContext x)
				{
					G.MS.Pay(x.Amount, 13);
					publisherScript p = IndustryCore.Studio(x.StudioId);
					if (UnityEngine.Random.value < x.Share)
					{
						G.MS.Earn(x.Amount2, 1);
						gameScript theirs = Game(x.Game2);
						string name = Name(x.Game2);
						if ((bool)theirs && theirs.isOnMarket)
						{
							theirs.RemoveFromMarket();
						}
						if ((bool)p)
						{
							IndustryCore.AddStars(p, -4f);
						}
						IndustryCore.Change(p, -25, "You sued them over " + Name(x.GameId) + " and won");
						return "You win: the court orders " + name + " off the shelves, and " + StudioName(x.StudioId) + " pays " + Money(x.Amount2) + ".";
					}
					IndustryCore.Change(p, -20, "You sued them over " + Name(x.GameId) + " and lost");
					return "You lose: the court says " + Name(x.Game2) + " is different enough. The lawyers still want paying.";
				}, (IndustryContext x) => Need(x.Amount)),
				Choice("Outsell It", "Marketing costs " + Money(OutsellCost(c)), "Hype +12 for " + Name(c.GameId), delegate(IndustryContext x)
				{
					G.MS.Pay(OutsellCost(x), 12);
					Game(x.GameId)?.AddHype(12f);
					return "Your campaign reminds everyone which one came first: hype +12.";
				}, (IndustryContext x) => Need(OutsellCost(x))),
				Choice("License It to Them", "They pay " + Money(c.Number), "And " + LicenceShare + "% of what it earns for a year; standing +10", delegate(IndustryContext x)
				{
					G.MS.Earn(x.Number, 2);
					gameScript theirs = Game(x.Game2);
					if ((bool)theirs)
					{
						IndustryCore.State.Streams.Add(new IncomeStream
						{
							Label = "Your share of " + theirs.GetNameSimple(),
							StudioId = x.StudioId,
							GameId = theirs.myID,
							Share = (float)LicenceShare / 100f,
							WeeksLeft = 52,
							Category = 2
						});
					}
					IndustryCore.Change(IndustryCore.Studio(x.StudioId), 10, "Licensed " + Name(x.GameId) + " to them");
					return StudioName(x.StudioId) + " pays " + Money(x.Number) + " for the rights, and is grateful. " + Name(x.Game2) + " is now the official version: you get " + LicenceShare + "% of what it earns for a year.";
				}),
				Choice("Ignore It", "Nothing to pay", "Both stay in the shops; players choose", (IndustryContext x) => "You leave it to the players. The two games crowd the same genre and topic, as any two games would.")
			}
		};
	}

	// A Copy of Your Game, License It: your share of what the official version earns, for a year.
	private const int LicenceShare = 15;

	// Two publishers bid to publish your game in development: an advance now, and when it's done they publish it for
	// their usual cut (PublishDeals puts them at the top of the publisher list). Not with an exclusive publishing
	// contract: that one publishes everything.
	private static IndustryEventDef BiddingWar()
	{
		return new IndustryEventDef
		{
			Id = "bidding",
			Cooldown = 26,
			Weight = 1.1f,
			Setup = delegate(IndustryContext c)
			{
				if (G.MS.exklusivVertrag_ID != -1)
				{
					return false;
				}
				gameScript g = (from x in IndustryCore.MyGamesInDevelopment()
					where x.developerID == G.MS.myID && (x.publisherID == -1 || x.publisherID == G.MS.myID) && PublishDeals.Publishable(x) && !IndustryCore.State.DealtGames.Contains(x.myID) && DevProgress(x) >= 0.3f
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
				c.Number = UnityEngine.Random.Range(5, 11);
				return true;
			},
			Title = (IndustryContext c) => "Publishers Bid for " + Name(c.GameId),
			Text = (IndustryContext c) => "Word of " + Name(c.GameId) + " has got around. " + StudioName(c.StudioId) + " and " + StudioName(c.Studio2) + " both want to publish it: each pays an advance now, starts the hype, and publishes it for its usual cut when it's done.",
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Go with " + StudioName(c.StudioId), "Advance " + Money(c.Amount) + ", hype +" + c.Number, "They publish it when it's done", (IndustryContext x) => Bid(x, x.StudioId, x.Studio2, x.Amount)),
				Choice("Go with " + StudioName(c.Studio2), "Advance " + Money(c.Amount2) + ", hype +" + c.Number, "They publish it when it's done", (IndustryContext x) => Bid(x, x.Studio2, x.StudioId, x.Amount2)),
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

	// Outsell It (A Copy of Your Game): saved with the event; older saves fall back to the old price.
	private static long OutsellCost(IndustryContext c)
	{
		if (c.Number2 <= 0)
		{
			return Scaled(8000.0);
		}
		return c.Number2;
	}

	private static readonly int[] GuaranteePerPoint = new int[6] { 2000, 4000, 8000, 16000, 32000, 48000 };

	// The game's own publishing guarantee for a game of this size (gameScript, pubAngebot_Garantiesumme: review points x
	// 2,000-48,000 by size x (difficulty + 1)), with your recent reviews standing in for the unfinished game's, by the
	// publisher's stars. The game's sum doesn't change with the years, so it's capped at a tenth of what your games of
	// this size earn (a 1977 studio isn't handed years of income); never below the old price.
	private static long Advance(gameScript g, publisherScript p)
	{
		float stars = 0.6f + p.stars * 0.01f;
		long old = (long)((float)Scaled(12000.0) * (1f + (float)g.gameSize) * stars);
		List<int> reviews = (from x in IndustryCore.AllGames()
			where x != g && x.IsMyGame() && x.reviewTotal > 0 && !x.inDevelopment && Standalone(x)
			orderby x.date_year * 12 + x.date_month descending
			select x.reviewTotal).Take(3).ToList();
		int review = ((reviews.Count > 0) ? Mathf.RoundToInt((float)reviews.Average()) : 60);
		int size = Mathf.Clamp(g.gameSize, 0, GuaranteePerPoint.Length - 1);
		long guarantee = (long)((float)((long)review * (long)GuaranteePerPoint[size] * (long)((G.MS ? G.MS.difficulty : 1) + 1)) * stars);
		long sum = Math.Max(old, Math.Min(guarantee, ExpectedRevenue(g) / 10));
		return Prices.Round((double)((float)sum * R(0.85f, 1.2f)));
	}

	private static string Bid(IndustryContext c, int winner, int loser, long advance)
	{
		gameScript g = Game(c.GameId);
		IndustryCore.State.DealtGames.Add(c.GameId);
		if (!g)
		{
			return "The game is gone.";
		}
		if (!g.inDevelopment || g.isOnMarket)
		{
			// Answered after it came out (a decision can wait a while): there's nothing left to publish.
			return "Too late: " + g.GetNameSimple() + " is already out, so the offers lapse.";
		}
		G.MS.Earn(advance, 2);
		g.AddHype(c.Number);
		PublishDeals.Add(g, IndustryCore.Studio(winner), advance);
		IndustryCore.Change(IndustryCore.Studio(winner), 12, "Won the bid for " + g.GetNameSimple());
		IndustryCore.Change(IndustryCore.Studio(loser), -4, "Lost the bid for " + g.GetNameSimple());
		return StudioName(winner) + " pays " + Money(advance) + " and starts the hype for " + g.GetNameSimple() + ". When it's done, they publish it: the finished game's screen offers \"Publish with " + StudioName(winner) + "\" instead of finding a publisher or publishing it yourself.";
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
				// A fifth of what their games usually earn (it pays back about that, when it works); never below the old price.
				c.Amount = Math.Max((long)((float)Scaled(20000.0) * (1f + (float)g.gameSize)), Prices.Round((double)StudioRevenue(pick) * 0.2));
				IndustryCore.State.Cooldowns["rescue:" + pick.myID] = Now + 156;
				return true;
			},
			Title = (IndustryContext c) => StudioName(c.StudioId) + " Needs Help",
			Text = (IndustryContext c) => StudioName(c.StudioId) + " has run out of money finishing " + Name(c.GameId) + ". They ask you to fund the rest, for a share of its revenue for a year after it comes out.",
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Fund It", "Costs " + Money(c.Amount), "25% of its revenue for a year (if they finish it); standing +20", (IndustryContext x) => Fund(x, x.Amount, 0.25f, 20, 4), (IndustryContext x) => Need(x.Amount)),
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
		IndustryCore.Change(p, standing, "You funded " + g.GetNameSimple());
		// Money doesn't fix everything: sometimes they still can't finish it.
		if (UnityEngine.Random.value < 0.25f)
		{
			p.newGameInWeeks += 26;
			return p.GetName() + " takes the money, but still can't finish " + g.GetNameSimple() + ": it's shelved for half a year, and your money is gone. They won't forget you tried.";
		}
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
		return p.GetName() + " can finish " + g.GetNameSimple() + ". You'll get " + Mathf.RoundToInt(share * 100f) + "% of its revenue for a year after it's out.";
	}

	// What one of this studio's games usually earns: the average of its last 3 released games (0 if none).
	private static long StudioRevenue(publisherScript p)
	{
		if (!p)
		{
			return 0L;
		}
		List<gameScript> done = (from x in IndustryCore.AllGames()
			where x.developerID == p.myID && !x.IsMyGame() && x.umsatzTotal > 0 && !x.inDevelopment && Standalone(x)
			orderby x.date_year * 12 + x.date_month descending
			select x).Take(3).ToList();
		if (done.Count <= 0)
		{
			return 0L;
		}
		return (long)done.Average((gameScript x) => (double)x.umsatzTotal);
	}

	// ---- Headlines ----

	private static IndustryEventDef Craze()
	{
		return new IndustryEventDef
		{
			Id = "craze",
			Headline = true,
			// The game itself moves its trend every 50-100 weeks: not much more often than that.
			Cooldown = 40,
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
			Cooldown = 40,
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
				// The biggest new hit, or a competitor's if one of theirs is among the top three.
				List<gameScript> top = (from x in IndustryCore.AllGames()
					where x.isOnMarket && !x.IsMyGame() && x.weeksOnMarket <= 12 && x.developerID >= 0 && (bool)IndustryCore.Studio(x.developerID) && Weekly(x) > 0
					orderby Weekly(x) descending
					select x).Take(3).ToList();
				gameScript g = top.FirstOrDefault((gameScript x) => IndustryCompetitors.IsCompetitor(IndustryCore.Studio(x.developerID))) ?? top.FirstOrDefault();
				if (!g)
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
				if (IndustryCompetitors.IsCompetitor(p))
				{
					return "<b>" + StudioName(c.StudioId) + "</b>, one of your competitors, has a hit: " + Name(c.GameId) + " is flying off the shelves.";
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
