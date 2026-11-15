using System.Collections.Generic;
using System.Linq;
using HolyAddon.Core.Updates;
using UnityEngine;

namespace HolyAddon.Core.Industry;

// The 3D and online years (1995-2009). Every event changes the game's real systems: hype, the game's own review score,
// user score and age rating, real copies sold, saturation, online interest, installed bases, studios closing. See Docs/Events-1995-2009-Plan.md.
internal static partial class IndustryEvents
{
	private static readonly string[] PcMags = new string[5] { "PC Power Monthly", "Gamer's Disc", "Polygon Weekly", "CD Zone", "The Demo Drawer" };

	private static readonly string[] CardMakers = new string[4] { "Polygon Forge", "Hexel Graphics", "TriStar Visual", "Vertex Labs" };

	private static readonly string[] ModWordsA = new string[8] { "Ultimate", "Unofficial", "Extended", "Hardcore", "Total", "Enhanced", "Classic", "Remixed" };

	private static readonly string[] ModWordsB = new string[8] { "Overhaul", "Expansion", "Campaign", "Conversion", "Map Pack", "Story Mode", "Rebalance", "Remix" };

	private static IEnumerable<IndustryEventDef> ThreeD()
	{
		yield return ModTeam();
		yield return CoverDisc();
		yield return CardBundle();
		yield return HiddenContent();
		yield return DotComBoom();
		yield return DotComBust();
		yield return InternetCafes();
		yield return UsedGames();
		yield return HandheldRevival();
	}

	// Games out now (any studio's), past any pre-order weeks.
	private static IEnumerable<gameScript> OnSale()
	{
		return IndustryCore.AllGames().Where((gameScript g) => g.isOnMarket && g.releaseDate <= 0 && !g.typ_bundle && !g.typ_bundleAddon);
	}

	private static bool Online(gameScript g)
	{
		if (g.gameTyp != 1)
		{
			return g.gameTyp == 2;
		}
		return true;
	}

	private static bool OnHandheld(gameScript g)
	{
		if (g.gamePlatform == null)
		{
			return false;
		}
		foreach (int id in g.gamePlatform)
		{
			platformScript p = ((id >= 0) ? GameUpdates.Platform(id) : null);
			if ((bool)p && p.typ == 2)
			{
				return true;
			}
		}
		return false;
	}

	// Fans made a hit mod of your game: with Holy's Communities, it's Holy's "Mod of the month" (started the week Holy
	// names it, IndustryHooks); without them, one of your well-loved games now and then.
	private static IndustryEventDef ModTeam()
	{
		return new IndustryEventDef
		{
			Id = "modteam",
			From = 1995,
			Cooldown = 26,
			Weight = 0.8f,
			Setup = delegate(IndustryContext c)
			{
				IndustryState st = IndustryCore.State;
				st.ModHits.RemoveAll((ModHit h) => Now - h.Week > 12);
				gameScript g = null;
				string name = null;
				ModHit hit = st.ModHits.LastOrDefault((ModHit h) => (bool)Game(h.GameId) && Game(h.GameId).IsMyGame() && Game(h.GameId).isOnMarket && !st.Cooldowns.ContainsKey("modteam:" + h.GameId));
				if (hit != null)
				{
					g = Game(hit.GameId);
					name = hit.Name;
				}
				else if (!IndustryCommunity.On)
				{
					g = (from x in IndustryCore.MyGamesOnMarket()
						where x.gameTyp == 0 && Standalone(x) && x.weeksOnMarket >= 20 && x.reviewTotal >= 70 && !st.Cooldowns.ContainsKey("modteam:" + x.myID)
						orderby x.reviewTotal descending
						select x).FirstOrDefault();
					name = Pick(ModWordsA) + " " + Pick(ModWordsB);
				}
				if (!g)
				{
					return false;
				}
				st.Cooldowns["modteam:" + g.myID] = Now;
				c.GameId = g.myID;
				c.Text = name;
				// Backing them costs what Holy charges for official modding tools; buying the mod costs what the game's
				// add-on screen would charge to make it, and it sells with that add-on quality.
				c.Amount = IndustryCommunity.ToolsCost();
				IndustryCommunity.AddonOffer(g, out long price, out float quality);
				c.Amount2 = price;
				c.Share = quality;
				return true;
			},
			Title = (IndustryContext c) => "The Mod Team",
			Text = (IndustryContext c) => "Fans have made a hit mod for " + Name(c.GameId) + ": \"" + c.Text + "\". Its small team of modders is the talk of every forum, and players who'd put the game away are picking it up again.",
			Default = 3,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Hire Them", "Two modders join the job market", "A strong designer and programmer; the scene loses a few modders", delegate(IndustryContext x)
				{
					Applicant(0, 50f, 62f);
					Applicant(1, 50f, 62f);
					IndustryCommunity.ScaleModders(Game(x.GameId), 0.85f);
					return "The two leads of \"" + x.Text + "\" are in the job market now (Staff > hire), keen to work on the real thing.";
				}),
				Choice("Support Them", "Costs " + Money(c.Amount), (IndustryCommunity.On && !IndustryCommunity.HasTools(Game(c.GameId))) ? "Official modding tools, more modders; IP points +15" : (IndustryCommunity.On ? "More modders and a buzz; IP points +15" : "Lapsed players come back; IP points +15"), delegate(IndustryContext x)
				{
					gameScript g = Game(x.GameId);
					if (!g)
					{
						return "The game is gone.";
					}
					G.MS.Pay(x.Amount, 4);
					g.AddIpPoints(15f);
					if (IndustryCommunity.On)
					{
						if (!IndustryCommunity.HasTools(g))
						{
							IndustryCommunity.GiveTools(g);
						}
						else
						{
							IndustryCommunity.ScaleModders(g, 1.3f);
							IndustryCommunity.Buzz(g, 8);
						}
					}
					else
					{
						IndustryReal.Saturate(g, -0.1f);
					}
					return "You back \"" + x.Text + "\" officially. The modding scene around " + g.GetNameSimple() + " grows.";
				}, (IndustryContext x) => Need(x.Amount)),
				Choice("Make It Official", "Costs " + Money(c.Amount2) + ", what making it would", "A paid add-on, out now; the modders get 20% for a year", delegate(IndustryContext x)
				{
					gameScript g = Game(x.GameId);
					string problem = IndustryCommunity.AddonProblem(g);
					if (problem != null)
					{
						return problem;
					}
					G.MS.Pay(x.Amount2, 4);
					gameScript addon = IndustryCommunity.MakeAddon(g, x.Text, x.Amount2, x.Share);
					if (!addon)
					{
						G.MS.Pay(-x.Amount2, 4);
						return "The deal fell through (see the log); you keep your money.";
					}
					IndustryCore.State.Streams.Add(new IncomeStream
					{
						Label = "The modders' share of " + addon.GetNameSimple(),
						GameId = addon.myID,
						Share = 0.2f,
						WeeksLeft = 52,
						Out = true
					});
					return addon.GetNameSimple() + " is out now as a paid add-on, rated " + addon.reviewTotal + "%. The modders get 20% of what it makes for a year.";
				}, (IndustryContext x) => IndustryCommunity.AddonProblem(Game(x.GameId)) ?? Need(x.Amount2)),
				Choice("Leave Them to It", "Nothing changes", "It stays a fan project", (IndustryContext x) => "You let the fans enjoy \"" + x.Text + "\".")
			}
		};
	}

	// A magazine wants a demo of your game in development for its cover disc.
	private static IndustryEventDef CoverDisc()
	{
		return new IndustryEventDef
		{
			Id = "coverdisc",
			From = 1994,
			To = 2008,
			Cooldown = 40,
			Setup = delegate(IndustryContext c)
			{
				gameScript g = (from x in IndustryCore.MyGamesInDevelopment()
					where Standalone(x) && DevProgress(x) >= 0.4f && DevProgress(x) <= 0.85f && !IndustryCore.State.Cooldowns.ContainsKey("disc:" + x.myID)
					orderby x.hype descending
					select x).FirstOrDefault();
				if (!g)
				{
					return false;
				}
				c.GameId = g.myID;
				c.Number = Random.Range(0, PcMags.Length);
				c.Number2 = Random.Range(6, 13);
				c.Amount = OfRevenue(g, 0.005f, Scaled(4000.0));
				IndustryCore.State.Cooldowns["disc:" + g.myID] = Now;
				return true;
			},
			Title = (IndustryContext c) => "Cover Disc",
			Text = (IndustryContext c) => PcMags[c.Number] + " wants a playable demo of " + Name(c.GameId) + " for next month's cover disc. Every reader will try it, but the game isn't finished, and a rough demo is hard to forget.",
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Put a Demo on It", "Hype +" + c.Number2 + " now", "About a 1 in 3 chance it reviews a few points lower", delegate(IndustryContext x)
				{
					gameScript g = Game(x.GameId);
					if (!g || !g.inDevelopment)
					{
						return "Too late: it's already finished.";
					}
					g.AddHype(x.Number2);
					if (!IndustryCore.State.CoverDiscs.Contains(g.myID))
					{
						IndustryCore.State.CoverDiscs.Add(g.myID);
					}
					return "The demo goes out on " + PcMags[x.Number] + "'s disc. Everyone is talking about " + g.GetNameSimple() + ": hype +" + x.Number2 + ".";
				}),
				Choice("Send a Trailer", "Costs " + Money(c.Amount), "Hype +" + c.Number2 / 2 + ", nothing to judge it by", delegate(IndustryContext x)
				{
					G.MS.Pay(x.Amount, 12);
					Game(x.GameId)?.AddHype(x.Number2 / 2);
					return "A slick trailer goes on the disc instead.";
				}, (IndustryContext x) => Need(x.Amount)),
				Choice("No Thanks", "Nothing changes", "The disc features another game", (IndustryContext x) => "You keep " + Name(x.GameId) + " under wraps for now.")
			}
		};
	}

	// A 3D-card maker wants to bundle your PC game with its cards.
	private static IndustryEventDef CardBundle()
	{
		return new IndustryEventDef
		{
			Id = "cardbundle",
			From = 1996,
			To = 2005,
			Cooldown = 52,
			Setup = delegate(IndustryContext c)
			{
				gameScript g = (from x in IndustryCore.MyGamesOnMarket()
					where x.releaseDate <= 0 && x.weeksOnMarket <= 26 && Standalone(x) && !x.arcade && !x.handy && Store.StoreOps.PcFraction(x) > 0f && Weekly(x) > 0
					orderby Weekly(x) descending
					select x).FirstOrDefault();
				if (!g)
				{
					return false;
				}
				c.GameId = g.myID;
				c.Number = Random.Range(0, CardMakers.Length);
				c.Number2 = g.reviewGrafik;
				// A copy for every card it expects to sell: a few weeks of the game's PC sales, at a bundle price.
				c.Amount = Mathf.Max(100, Mathf.RoundToInt((float)Weekly(g) * Store.StoreOps.PcFraction(g) * R(3f, 6f)));
				c.Share = IndustryReal.PerCopy(g) * 0.3f;
				return true;
			},
			Title = (IndustryContext c) => "Graphics Card Bundle",
			Text = (IndustryContext c) => CardMakers[c.Number] + " wants to put " + Name(c.GameId) + " in the box with its new 3D card, to show what the card can do. It buys a copy for every card, at a bundle price, and every buyer will judge its graphics against the card's promise (its graphics review: " + c.Number2 + "%).",
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Bundle It", "They buy " + c.Amount + " copies (" + Money((long)((float)c.Amount * c.Share)) + ")", (c.Number2 >= 75) ? "Its graphics hold up: a showcase" : ((c.Number2 >= 60) ? "Some risk: its graphics may look plain" : "Risky: weak graphics will be shown up"), (IndustryContext x) => Bundle(x, x.Amount)),
				Choice("Ask for More", "A gamble", "A bigger order, or they bundle someone else's game", delegate(IndustryContext x)
				{
					if (Random.value < 0.5f)
					{
						return "They agree. " + Bundle(x, x.Amount * 3 / 2);
					}
					gameScript other = (from g in IndustryCore.AllGames()
						where g.isOnMarket && !g.IsMyGame() && g.weeksOnMarket <= 26 && Store.StoreOps.PcFraction(g) > 0f
						orderby Weekly(g) descending
						select g).FirstOrDefault();
					if ((bool)other)
					{
						return CardMakers[x.Number] + " bundles " + other.GetNameSimple() + " instead.";
					}
					return CardMakers[x.Number] + " walks away.";
				}),
				Choice("No Thanks", "Nothing changes", "The card ships with a tech demo", (IndustryContext x) => "You turn " + CardMakers[x.Number] + " down.")
			}
		};
	}

	// Real copies at a bundle price (the game's own totals and revenue), and new players who judge its graphics: they
	// become fans, and the user score follows what they think.
	private static string Bundle(IndustryContext c, long copies)
	{
		gameScript g = Game(c.GameId);
		if (!g)
		{
			return "The game is gone.";
		}
		// The card maker presses its own discs: these copies don't come out of your warehouse.
		long sold = copies;
		long money = (long)((float)sold * c.Share);
		g.sellsTotal += sold;
		g.sellsTotalStandard += sold;
		g.umsatzTotal += money;
		G.MS.Earn(money, 3);
		int fans = AddFans((float)sold * 0.05f * R(0.8f, 1.3f));
		bool shown = g.reviewGrafik < 60 || (g.reviewGrafik < 75 && Random.value < 0.3f);
		if (shown)
		{
			HolyCompat.ShiftUserReviews(g, -0.05f);
			return CardMakers[c.Number] + " takes " + sold + " copies (" + Money(money) + "), but next to the card's promise " + g.GetNameSimple() + " looks dated: " + fans + " new fans, and its user score drops.";
		}
		HolyCompat.ShiftUserReviews(g, 0.03f);
		if (g.reviewGrafik >= 75)
		{
			g.AddHype(3f);
		}
		return CardMakers[c.Number] + " takes " + sold + " copies (" + Money(money) + "), and every new card comes with " + g.GetNameSimple() + ": " + fans + " new fans, and players love how it looks.";
	}

	// Fans find cut content in your game's files that its age rating never covered.
	private static IndustryEventDef HiddenContent()
	{
		return new IndustryEventDef
		{
			Id = "hidden",
			From = 1998,
			Cooldown = 60,
			Setup = delegate(IndustryContext c)
			{
				gameScript g = (from x in IndustryCore.MyGamesOnMarket()
					where x.releaseDate <= 0 && x.weeksOnMarket >= 2 && x.weeksOnMarket <= 52 && x.usk <= 3 && Standalone(x) && Weekly(x) > 0
					orderby Weekly(x) descending
					select x).FirstOrDefault();
				if (!g)
				{
					return false;
				}
				c.GameId = g.myID;
				c.Number = Mathf.Min(4, g.usk + 2);
				c.Amount = OfRevenue(g, 0.02f, Scaled(10000.0));
				return true;
			},
			Title = (IndustryContext c) => "Hidden Content",
			Text = (IndustryContext c) => "Fans digging through " + Name(c.GameId) + "'s files have found a cut scene that was never meant to be playable, and far stronger than its " + GameUpdates.UskName(Game(c.GameId) ? Game(c.GameId).usk : 0) + " rating allows. It's all over the internet, and the rating board is asking questions.",
			Default = 0,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Accept a Higher Rating", "Re-rated " + GameUpdates.UskName(c.Number), "Fewer shops and players can buy it", delegate(IndustryContext x)
				{
					gameScript g = Game(x.GameId);
					if (!g)
					{
						return "The game is gone.";
					}
					int old = g.usk;
					g.usk = Mathf.Max(g.usk, x.Number);
					g.AddHype(4f);
					return g.GetNameSimple() + " is re-rated " + GameUpdates.UskName(g.usk) + " (was " + GameUpdates.UskName(old) + "). The fuss makes some players curious.";
				}),
				Choice("Recall and Patch", "Costs " + Money(c.Amount), "Its rating stays; your warehouse stock of it is scrapped", delegate(IndustryContext x)
				{
					gameScript g = Game(x.GameId);
					G.MS.Pay(x.Amount, 4);
					long scrapped = IndustryReal.ClearStock(g);
					HolyCompat.ShiftUserReviews(g, 0.02f);
					return "Every copy comes back for a patched version without the hidden scene" + ((scrapped > 0) ? (": " + scrapped + " copies in your warehouse are scrapped, and your production room has to make new ones") : "") + ". The rating stays, and players respect you for owning up.";
				}, (IndustryContext x) => Need(x.Amount)),
				Choice("Deny It", "A gamble", "It may blow over, or the board re-rates it anyway and players turn on you", delegate(IndustryContext x)
				{
					gameScript g = Game(x.GameId);
					if (!g)
					{
						return "The game is gone.";
					}
					if (Random.value < 0.5f)
					{
						g.AddHype(3f);
						return "Nobody can prove it was ever meant to be played. The story fades.";
					}
					int old = g.usk;
					g.usk = Mathf.Max(g.usk, x.Number);
					HolyCompat.ShiftUserReviews(g, -0.08f);
					return "The board finds the scene and re-rates " + g.GetNameSimple() + " " + GameUpdates.UskName(g.usk) + " (was " + GameUpdates.UskName(old) + "). Players feel lied to.";
				})
			}
		};
	}

	// ---- Headlines ----

	private static IndustryEventDef DotComBoom()
	{
		return new IndustryEventDef
		{
			Id = "dotcom",
			Headline = true,
			From = 1999,
			To = 2000,
			Cooldown = 999,
			Setup = (IndustryContext c) => OnSale().Any(Online) || IndustryReal.Public,
			Apply = delegate
			{
				IndustryReal.OnlineInterest(15f);
				bool shares = IndustryReal.MoveSharePrice(1.15f);
				return "Investors pour money into anything online: interest in online games soars" + (shares ? ", and your share price rises 15% with the market." : ".");
			}
		};
	}

	private static IndustryEventDef DotComBust()
	{
		return new IndustryEventDef
		{
			Id = "dotbust",
			Headline = true,
			From = 2001,
			To = 2002,
			Cooldown = 999,
			Setup = (IndustryContext c) => true,
			Apply = delegate
			{
				IndustryReal.OnlineInterest(-15f);
				bool shares = IndustryReal.MoveSharePrice(0.75f);
				List<string> gone = Bust();
				return "The dot-com bubble bursts. Players get wary of online games" + ((gone.Count > 0) ? (", and the money dries up for young studios: " + string.Join(" and ", gone) + ((gone.Count == 1) ? " closes its doors" : " close their doors")) : "") + (shares ? ". Your share price falls 25%." : ".");
			}
		};
	}

	// The bust: one or two of the weakest young studios (opened in the last six years) close for real. Their series
	// can come up at the Closure Auction.
	private static List<string> Bust()
	{
		List<publisherScript> young = (from p in IndustryCore.Studios()
			where p.developer && !p.publisher && !p.IsTochterfirma() && p.date_year >= Year - 6 && p.stars < 40f && p.CloseFree() && !IndustryRivals.IsRival(p)
			orderby p.stars
			select p).ToList();
		List<string> names = new List<string>();
		foreach (publisherScript p2 in young.Take(Random.Range(1, 3)))
		{
			names.Add(p2.GetName());
			p2.CloseNpcStudio();
		}
		return names;
	}

	private static IndustryEventDef InternetCafes()
	{
		return new IndustryEventDef
		{
			Id = "cafes",
			Headline = true,
			From = 1999,
			To = 2004,
			Cooldown = 104,
			Setup = (IndustryContext c) => OnSale().Any(Online),
			Apply = delegate
			{
				IndustryReal.OnlineInterest(10f);
				int leagues = IndustryReal.EsportsPopularity(0.15f);
				return "Internet cafés are opening everywhere, full of players gathering to play together: interest in online games grows" + ((leagues > 0) ? ", and your esports leagues draw bigger crowds." : ".");
			}
		};
	}

	private static IndustryEventDef UsedGames()
	{
		return new IndustryEventDef
		{
			Id = "usedgames",
			Headline = true,
			From = 2003,
			To = 2009,
			Cooldown = 104,
			Setup = (IndustryContext c) => OnSale().Any((gameScript g) => g.retailVersion && g.gameTyp == 0 && g.weeksOnMarket >= 26),
			Apply = delegate
			{
				foreach (gameScript g in OnSale().Where((gameScript x) => x.retailVersion && x.gameTyp == 0 && x.weeksOnMarket >= 26).ToList())
				{
					IndustryReal.Saturate(g, 0.1f);
				}
				return "Shops are filling up with second-hand games: players who'd have bought an older boxed game new pick up a used copy instead.";
			}
		};
	}

	private static IndustryEventDef HandheldRevival()
	{
		return new IndustryEventDef
		{
			Id = "handheld2",
			Headline = true,
			From = 2004,
			To = 2009,
			Cooldown = 156,
			Setup = (IndustryContext c) => Handhelds().Count > 0,
			Apply = delegate
			{
				List<platformScript> machines = Handhelds();
				foreach (platformScript p in machines)
				{
					p.units_max = (int)Mathf.Min(400000000f, (float)p.units_max * 1.15f);
					p.units = (int)Mathf.Min((float)p.units_max, (float)p.units * 1.15f);
				}
				return "Handhelds are back: everyone wants a game in their pocket, and the " + string.Join(" and the ", machines.Take(3).Select((platformScript p) => p.GetName())) + " sell far more machines than expected: more players for every handheld game.";
			}
		};
	}

	// Other makers' handhelds on sale (the game grows their installed base itself; the player's own run on its rules).
	private static List<platformScript> Handhelds()
	{
		return (G.MS.arrayPlatformsScripts ?? new platformScript[0]).Where((platformScript p) => (bool)p && p.typ == 2 && p.isUnlocked && !p.vomMarktGenommen && p.OwnerIsNPC()).ToList();
	}
}
