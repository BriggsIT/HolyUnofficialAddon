using System;
using System.Collections.Generic;
using System.Linq;
using HolyAddon.Core.Landmarks;
using HolyAddon.Core.Updates;
using UnityEngine;
using Random = UnityEngine.Random;

namespace HolyAddon.Core.Industry;

// Events about your landmark series (Core/Landmarks, Docs/Landmarks-Release2-Plan.md part C):
// - Anniversary: 10, 20 and 25 years on, plan a remaster (an anniversary launch when it comes out), run a campaign
//   for the series' games in the shops, or mark it quietly.
// - Buy-back: the studio that made it in history wants the series back, through the game's own IP ownership.
// - Speedrun craze: an old landmark is raced again, and players go back to the series' games in the shops.
// - Film or TV: an adaptation of the series, through the screen deals (Holy's Movie & TV deals when they're on).
// A landmark series is one whose first game is a landmark; yours when you own its IP now (claimed or bought).
internal static partial class IndustryEvents
{
	private static IEnumerable<IndustryEventDef> LandmarkEvents()
	{
		yield return Anniversary();
		yield return BuyBack();
		yield return Speedrun();
		yield return Adaptation();
	}

	// The series' games in the shops (yours or anyone's).
	private static List<gameScript> SeriesInShops(gameScript ip)
	{
		if (!ip)
		{
			return new List<gameScript>();
		}
		return IndustryCore.AllGames().Where((gameScript x) => x.isOnMarket && (x.mainIP == ip.myID || x.myID == ip.myID)).ToList();
	}

	private static string LandmarkTitle(IndustryContext c)
	{
		LandmarkRecord r = (string.IsNullOrEmpty(c.Text) ? null : Landmarks.Landmarks.Record(c.Text));
		if (r == null)
		{
			return Name(c.GameId);
		}
		return Landmarks.Landmarks.TitleOf(r);
	}

	// ---- Anniversary ----

	private static readonly int[] Anniversaries = new int[3] { 10, 20, 25 };

	private static IndustryEventDef Anniversary()
	{
		return new IndustryEventDef
		{
			Id = "lm_anniversary",
			From = 1986,
			Cooldown = 20,
			Weight = 1.4f,
			Setup = delegate(IndustryContext c)
			{
				LandmarkRecord r = (from x in Landmarks.Landmarks.YourSeries()
					where Anniversaries.Contains(Year - x.Year) && !IndustryCore.State.Cooldowns.ContainsKey("lm_anniv:" + x.Id + ":" + (Year - x.Year))
					orderby Landmarks.Landmarks.SeriesOf(x).ipPunkte descending
					select x).FirstOrDefault();
				if (r == null)
				{
					return false;
				}
				gameScript ip = Landmarks.Landmarks.SeriesOf(r);
				c.Text = r.Id;
				c.GameId = r.GameId;
				c.Game2 = ip.myID;
				c.Number = Year - r.Year;
				c.Amount = IncomeCap(Scaled(25000.0), 0.01f);
				IndustryCore.State.Cooldowns["lm_anniv:" + r.Id + ":" + c.Number] = Now;
				return true;
			},
			Title = (IndustryContext c) => LandmarkTitle(c) + " Turns " + c.Number,
			Text = (IndustryContext c) => "It's " + c.Number + " years since " + LandmarkTitle(c) + " came out. Magazines are running retrospectives, and fans who grew up with it are sharing their memories. How do you mark it?",
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Plan a Remaster", "Out by the end of " + (Year + 2) + ": an anniversary launch", "Hype +30 and returning fans; IP points +20 now", delegate(IndustryContext x)
				{
					gameScript ip = Game(x.Game2);
					if (!ip)
					{
						return "The series is gone.";
					}
					Landmarks.Landmarks.PlanRemaster(ip, Year + 2);
					ip.AddIpPoints(20f);
					return "You announce a remaster of " + LandmarkTitle(x) + " for its anniversary. Release one (a remaster of the series) by the end of " + (Year + 2) + " and it gets an anniversary launch: hype +30, and fans of the original come back.";
				}),
				Choice("Anniversary Campaign", "Costs " + Money(c.Amount), "The series' games in the shops: hype +20, more buyers; IP points +40", delegate(IndustryContext x)
				{
					gameScript ip = Game(x.Game2);
					if (!ip)
					{
						return "The series is gone.";
					}
					G.MS.Pay(x.Amount, 13);
					ip.AddIpPoints(40f);
					List<gameScript> shops = SeriesInShops(ip);
					foreach (gameScript g in shops)
					{
						g.AddHype(20f);
						IndustryReal.Saturate(g, -0.08f);
					}
					int fans = AddFans(Mathf.Clamp((float)(IndustryCore.Genres ? IndustryCore.Genres.GetAmountFans() : 0) * 0.01f, 500f, 100000f));
					return "The " + LandmarkTitle(x) + " anniversary is everywhere: fans +" + G.MS.GetMoney(fans, showDollar: false) + ", IP points +40" + ((shops.Count > 0) ? (", and " + shops.Count + ((shops.Count == 1) ? " game" : " games") + " of the series in the shops get a boost.") : ". None of its games are in the shops to cash in on it.");
				}, (IndustryContext x) => Need(x.Amount)),
				Choice("Mark It Quietly", "Nothing to pay", "IP points +10", delegate(IndustryContext x)
				{
					Game(x.Game2)?.AddIpPoints(10f);
					return "A post on your website and a cake in the office. The fans notice.";
				})
			}
		};
	}

	// ---- Buy-back ----

	private static IndustryEventDef BuyBack()
	{
		return new IndustryEventDef
		{
			Id = "lm_buyback",
			From = 1985,
			Cooldown = 52,
			Weight = 0.8f,
			Setup = delegate(IndustryContext c)
			{
				List<publisherScript> open = IndustryCore.Studios().ToList();
				foreach (LandmarkRecord r in Landmarks.Landmarks.YourSeries().OrderBy((LandmarkRecord x) => Random.value))
				{
					gameScript ip = Landmarks.Landmarks.SeriesOf(r);
					int studio = Landmarks.Landmarks.HistoricStudio(r);
					publisherScript p = open.FirstOrDefault((publisherScript x) => x.myID == studio);
					if (!p || !p.developer || IndustryCore.State.Cooldowns.TryGetValue("lm_buyback:" + ip.myID, out var last) && Now - last < 240 || !G.MS.games_.IsIpFree(ip, messageBox_: false))
					{
						continue;
					}
					c.Text = r.Id;
					c.GameId = r.GameId;
					c.Game2 = ip.myID;
					c.StudioId = p.myID;
					c.Amount = Prices.Round(Math.Max((double)Scaled(50000.0), (double)Landmarks.Landmarks.IpValueOf(ip) * (double)R(1.6f, 2.2f)));
					c.Amount2 = Prices.Round((double)c.Amount * 1.35);
					IndustryCore.State.Cooldowns["lm_buyback:" + ip.myID] = Now;
					return true;
				}
				return false;
			},
			Title = (IndustryContext c) => StudioName(c.StudioId) + " Wants " + LandmarkTitle(c) + " Back",
			Text = (IndustryContext c) => StudioName(c.StudioId) + " always felt " + LandmarkTitle(c) + " should have been theirs. They're offering " + Money(c.Amount) + " for the whole series: its games, and the rights to its sequels, remasters and spin-offs.",
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Sell the Series", Money(c.Amount) + " now", "They own it from now on; standing +8", (IndustryContext x) => SellSeries(x, x.Amount)),
				Choice("Hold Out for More", "Ask for " + Money(c.Amount2), "About a 50% chance they pay; else they walk away", delegate(IndustryContext x)
				{
					if (Random.value < 0.5f)
					{
						return SellSeries(x, x.Amount2);
					}
					IndustryCore.Change(IndustryCore.Studio(x.StudioId), -4, "You held out on " + LandmarkTitle(x));
					return StudioName(x.StudioId) + " won't go that high and walks away. " + LandmarkTitle(x) + " stays yours.";
				}),
				Choice("Not for Sale", "It stays yours", "Standing -3; IP points +10", delegate(IndustryContext x)
				{
					IndustryCore.Change(IndustryCore.Studio(x.StudioId), -3, "Wouldn't sell " + LandmarkTitle(x));
					Game(x.Game2)?.AddIpPoints(10f);
					return "You turn " + StudioName(x.StudioId) + " down. The fans like that you're keeping it.";
				})
			}
		};
	}

	private static string SellSeries(IndustryContext c, long price)
	{
		gameScript ip = Game(c.Game2);
		publisherScript p = IndustryCore.Studio(c.StudioId);
		if (!ip || !p || ip.ownerID != G.MS.myID || !G.MS.games_.IsIpFree(ip, messageBox_: false))
		{
			return "The deal fell through: the series can't change hands right now.";
		}
		GiveIp(ip, p);
		G.MS.Earn(price, 1);
		IndustryCore.Change(p, 8, "Sold them " + LandmarkTitle(c));
		return p.GetName() + " pays " + Money(price) + " for the " + LandmarkTitle(c) + " series. Its next games are theirs to make.";
	}

	// ---- Speedrun craze ----

	private static IndustryEventDef Speedrun()
	{
		return new IndustryEventDef
		{
			Id = "lm_speedrun",
			From = 1998,
			Cooldown = 52,
			Weight = 0.9f,
			Setup = delegate(IndustryContext c)
			{
				LandmarkRecord r = (from x in Landmarks.Landmarks.YourSeries()
					where Year - x.Year >= 5 && !IndustryCore.State.Cooldowns.ContainsKey("lm_speedrun:" + x.Id) && SeriesInShops(Landmarks.Landmarks.SeriesOf(x)).Count > 0
					orderby Random.value
					select x).FirstOrDefault();
				if (r == null)
				{
					return false;
				}
				c.Text = r.Id;
				c.GameId = r.GameId;
				c.Game2 = Landmarks.Landmarks.SeriesOf(r).myID;
				c.Amount = IncomeCap(Scaled(8000.0), 0.004f);
				IndustryCore.State.Cooldowns["lm_speedrun:" + r.Id] = Now;
				return true;
			},
			Title = (IndustryContext c) => "Speedrun Craze: " + LandmarkTitle(c),
			Text = (IndustryContext c) => "Runners are racing through " + LandmarkTitle(c) + " faster than ever, and the record attempts are drawing crowds. Players are going back to the series.",
			Default = 1,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				// (Streamed marathons only from 2010, IndustryCommunity.MarathonsFrom: before that you back the runners.)
				Choice((Year >= IndustryCommunity.MarathonsFrom) ? "Sponsor a Marathon" : "Sponsor the Runners", "Costs " + Money(c.Amount), "The series' games in the shops: hype +20, more buyers; IP points +40", delegate(IndustryContext x)
				{
					gameScript ip = Game(x.Game2);
					if (!ip)
					{
						return "The series is gone.";
					}
					G.MS.Pay(x.Amount, 13);
					ip.AddIpPoints(40f);
					foreach (gameScript g in SeriesInShops(ip))
					{
						g.AddHype(20f);
						IndustryReal.Saturate(g, -0.12f);
					}
					int fans = AddFans(Mathf.Clamp((float)(IndustryCore.Genres ? IndustryCore.Genres.GetAmountFans() : 0) * 0.005f, 300f, 50000f));
					return ((Year >= IndustryCommunity.MarathonsFrom) ? "Your marathon runs all weekend and the record falls live" : "The runners you back break the record, and their runs do the rounds") + ": fans +" + G.MS.GetMoney(fans, showDollar: false) + ", IP points +40, and the series sells again.";
				}, (IndustryContext x) => Need(x.Amount)),
				Choice("Let It Run", "Nothing to pay", "The series' games in the shops: hype +8; IP points +15", delegate(IndustryContext x)
				{
					gameScript ip = Game(x.Game2);
					if (!ip)
					{
						return "The series is gone.";
					}
					ip.AddIpPoints(15f);
					foreach (gameScript g in SeriesInShops(ip))
					{
						g.AddHype(8f);
					}
					return "The runners carry on without you, and the series gets a little attention.";
				})
			}
		};
	}

	// ---- Film or TV ----

	private static IndustryEventDef Adaptation()
	{
		return new IndustryEventDef
		{
			Id = "lm_screen",
			From = 1985,
			Cooldown = 60,
			Setup = delegate(IndustryContext c)
			{
				gameScript ip = (from x in Landmarks.Landmarks.YourSeries()
					select Landmarks.Landmarks.SeriesOf(x) into x
					where x.ipPunkte >= 100f && !IndustryReal.CartoonRunning(x) && (!IndustryCore.State.Cooldowns.TryGetValue("lm_screen:" + x.myID, out var last) || Now - last >= 156)
					orderby x.ipPunkte descending
					select x).FirstOrDefault();
				if (!ip)
				{
					return false;
				}
				// Films of games from 1993 (as in history); TV and animated series before.
				int kind = ((Year >= 1993) ? Random.Range(0, 3) : Random.Range(1, 3));
				c.GameId = ip.myID;
				c.Game2 = ip.myID;
				c.Number = kind;
				c.Text2 = ((kind == 0) ? Pick(FilmStudios) : Pick(TvStudios));
				// Holy's price for the kind, by the IP's fame, with a landmark's premium.
				double basePrice = ((kind == 0) ? 400000.0 : ((kind == 1) ? 250000.0 : 150000.0));
				c.Amount = Prices.Round((double)Prices.Scaled(basePrice) * (double)Mathf.Clamp(ip.ipPunkte / 1000f, 0.35f, 1f) * (double)R(1f, 1.4f));
				IndustryCore.State.Cooldowns["lm_screen:" + ip.myID] = Now;
				return true;
			},
			Title = (IndustryContext c) => ((c.Number == 0) ? "A Film of " : ((c.Number == 1) ? "A TV Series of " : "A Cartoon of ")) + IpName(c.Game2),
			Text = (IndustryContext c) => c.Text2 + " wants to make a " + IndustryReal.ScreenKinds[Mathf.Clamp(c.Number, 0, 2)] + " of " + IpName(c.Game2) + ", the landmark series. It would come out in " + ((c.Number == 0) ? "about a year and a half" : "about a year") + ": a hit brings players back to every game of the series, a flop does it no favours. Sell them the rights, or put your own money in and share in the merchandise.",
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Sell the Rights", Money(c.Amount) + " now", "IP points +15", delegate(IndustryContext x)
				{
					gameScript ip = Game(x.Game2);
					if (!ip)
					{
						return "The series is gone.";
					}
					G.MS.Earn(x.Amount, 1);
					ip.AddIpPoints(15f);
					IndustryReal.StartCartoon(ip, x.Text2, x.Amount, coProduced: false, x.Number);
					return x.Text2 + " pays " + Money(x.Amount) + ". The " + ip.GetIpName() + " " + IndustryReal.ScreenKinds[Mathf.Clamp(x.Number, 0, 2)] + " comes out in about " + Mathf.Max(1, IndustryReal.CartoonWeeks(ip) / 4) + " months.";
				}),
				Choice("Co-Produce It", "Costs " + Money(c.Amount), "The same, and its merchandise", delegate(IndustryContext x)
				{
					gameScript ip = Game(x.Game2);
					if (!ip)
					{
						return "The series is gone.";
					}
					G.MS.Pay(x.Amount, 13);
					ip.AddIpPoints(15f);
					IndustryReal.StartCartoon(ip, x.Text2, x.Amount, coProduced: true, x.Number);
					return "You co-produce the " + ip.GetIpName() + " " + IndustryReal.ScreenKinds[Mathf.Clamp(x.Number, 0, 2)] + " with " + x.Text2 + ". It comes out in about " + Mathf.Max(1, IndustryReal.CartoonWeeks(ip) / 4) + " months, and unless it flops, your fan shop's demand for the series starts fresh.";
				}, (IndustryContext x) => Need(x.Amount)),
				Choice("Decline", "Nothing changes", "It stays a game", (IndustryContext x) => "You turn " + x.Text2 + " down.")
			}
		};
	}

	private static string IpName(int id)
	{
		gameScript g = Game(id);
		if (!g)
		{
			return "the series";
		}
		return g.GetIpName();
	}
}
