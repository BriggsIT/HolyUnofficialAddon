using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HolyAddon.Core.Logos;
using HolyAddon.Core.Updates;
using UnityEngine;
using Random = UnityEngine.Random;

namespace HolyAddon.Core.Industry;

// The 3D and online years, part 2: events that reach into the game's studios, engines, IPs, platforms and copy
// protection. See Docs/Events-1995-2009-Plan.md.
internal static partial class IndustryEvents
{
	private static readonly AccessTools.FieldRef<mainScript, publisher> PublisherMaker = AccessTools.FieldRefAccess<mainScript, publisher>("publisher_");

	private static IEnumerable<IndustryEventDef> Studios3D()
	{
		yield return EngineRequest();
		yield return ClosureAuction();
		yield return StarStudio();
		yield return FundPort();
		yield return CdBurners();
		yield return HardwareQuit();
	}

	// ---- Engine Request ----

	// A studio wants one of your engines that you don't sell: license it to them only (their next game is built on it,
	// so the game's own engine statement pays your share of its profit), or put it on sale for everyone.
	private static IndustryEventDef EngineRequest()
	{
		return new IndustryEventDef
		{
			Id = "enginereq",
			From = 1993,
			Cooldown = 52,
			Setup = delegate(IndustryContext c)
			{
				engineScript e = (from x in G.MS.arrayEnginesScripts ?? new engineScript[0]
					where (bool)x && x.ownerID == G.MS.myID && !x.sellEngine && !x.updating && x.Complete()
					orderby x.GetTechLevel() descending
					select x).FirstOrDefault();
				if (!e)
				{
					return false;
				}
				List<publisherScript> pool = IndustryCore.Studios().Where((publisherScript p) => p.developer && p.stars >= 15f && IndustryCore.Tier(IndustryCore.Standing(p)) >= 1 && !IndustryCore.State.EngineDeals.Any((EngineDeal d) => d.StudioId == p.myID) && !Bought(e, p.myID)).ToList();
				if (pool.Count == 0)
				{
					return false;
				}
				publisherScript who = Pick(pool);
				EngineMarket(out long top, out int topShare);
				c.StudioId = who.myID;
				c.Number = e.myID;
				// Priced from the game's own engine market: a one-studio deal pays above the dearest engine on sale.
				c.Number2 = Mathf.Clamp(Random.Range(topShare - 2, topShare + 4), 3, 15);
				c.Amount = Thousands(Math.Max((double)Scaled(20000.0) * (1.0 + (double)e.GetTechLevel() * 0.25), (double)(top + 20000) * (double)Random.Range(1.2f, 2f)));
				// On sale to everyone: the going rate, which is what the game lets studios pay for a player's engine.
				c.Amount2 = Thousands(Math.Max(Scaled(10000.0), top * 4 / 5));
				c.Share2 = Mathf.Min(c.Number2, topShare);
				return true;
			},
			Title = (IndustryContext c) => "Engine Request",
			Text = (IndustryContext c) => StudioName(c.StudioId) + " wants to build its next game on " + EngineName(c.Number) + ", your engine. You don't sell it, but they offer " + Money(c.Amount) + " up front and " + c.Number2 + "% of the game's profit.",
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("License It to Them", Money(c.Amount) + " now; " + c.Number2 + "% of its profit", "Only them; standing +6", delegate(IndustryContext x)
				{
					engineScript e = Engine(x.Number);
					publisherScript p = IndustryCore.Studio(x.StudioId);
					if (!e || !p)
					{
						return "The offer has lapsed.";
					}
					MarkBought(e, p.myID);
					G.MS.Earn(x.Amount, 4);
					e.umsatz += (int)Math.Min(int.MaxValue, x.Amount);
					IndustryCore.State.EngineDeals.Add(new EngineDeal
					{
						StudioId = p.myID,
						EngineId = e.myID,
						Share = x.Number2
					});
					IndustryCore.Change(p, 6, "Licensed " + e.GetName() + ", your engine");
					return p.GetName() + " pays " + Money(x.Amount) + ". Its next game will be built on " + e.GetName() + ", and you get " + x.Number2 + "% of that game's profit when it leaves the shops.";
				}),
				Choice("Put It on Sale", "Any studio can license it", "At the going rate: " + Money(c.Amount2) + " and " + Mathf.RoundToInt(c.Share2) + "% of profit", delegate(IndustryContext x)
				{
					engineScript e = Engine(x.Number);
					if (!e)
					{
						return "The engine is gone.";
					}
					e.sellEngine = true;
					e.preis = (int)Math.Min(int.MaxValue, x.Amount2);
					e.gewinnbeteiligung = Mathf.RoundToInt(x.Share2);
					IndustryCore.Change(IndustryCore.Studio(x.StudioId), 2, "You put your engine on sale");
					return e.GetName() + " is now for sale to every studio, at " + Money(x.Amount2) + " and " + Mathf.RoundToInt(x.Share2) + "% of profit, the going rate (change it in the engine's settings).";
				}),
				Choice("Keep It In-House", "Nothing changes", "Standing with them -2", delegate(IndustryContext x)
				{
					IndustryCore.Change(IndustryCore.Studio(x.StudioId), -2, "Wouldn't license your engine");
					return "You keep " + EngineName(x.Number) + " to yourself.";
				})
			}
		};
	}

	private static engineScript Engine(int id)
	{
		return G.MS.arrayEnginesScripts?.FirstOrDefault((engineScript x) => (bool)x && x.myID == id);
	}

	private static string EngineName(int id)
	{
		engineScript e = Engine(id);
		if (!e)
		{
			return "your engine";
		}
		return e.GetName();
	}

	// The game's own engine market: the dearest NPC engine and the highest profit share among them. A studio only takes
	// a player's engine priced under the first (plus $20,000) with a share under the second (plus 1).
	private static void EngineMarket(out long top, out int topShare)
	{
		top = 0L;
		topShare = 0;
		foreach (engineScript e in G.MS.arrayEnginesScripts ?? new engineScript[0])
		{
			if ((bool)e && e.isUnlocked && e.OwnerIsNPC())
			{
				top = Math.Max(top, e.GetPreis());
				topShare = Math.Max(topShare, e.GetGewinnbeteiligung());
			}
		}
		if (topShare <= 0)
		{
			topShare = 10;
		}
	}

	private static long Thousands(double v)
	{
		return Math.Max(1000L, (long)Math.Round(v / 1000.0) * 1000);
	}

	private static bool Bought(engineScript e, int studio)
	{
		if (e.publisherBuyed != null && studio >= 0 && studio < e.publisherBuyed.Length)
		{
			return e.publisherBuyed[studio];
		}
		return false;
	}

	// The game's "this studio has paid for the engine" flag, so it isn't charged again when it starts the game.
	private static void MarkBought(engineScript e, int studio)
	{
		if (e.publisherBuyed == null || e.publisherBuyed.Length <= studio)
		{
			bool[] grown = new bool[Mathf.Max(studio + 1, (G.MS.arrayPublisher != null) ? G.MS.arrayPublisher.Length : 0)];
			if (e.publisherBuyed != null)
			{
				Array.Copy(e.publisherBuyed, grown, e.publisherBuyed.Length);
			}
			e.publisherBuyed = grown;
		}
		e.publisherBuyed[studio] = true;
	}

	// ---- Closure Auction ----

	// A studio has closed: its best IP goes to auction, through the game's own IP ownership (what buying it in the IP
	// market does). Another studio bids too, and gets it if you don't.
	// It isn't drawn at random: it follows the closure (IndustryHooks.Weekly), once the series is free to change hands
	// under the game's own IP-market rule (none of its games in the shops or in development). A studio's last games
	// usually stay in the shops for months after it closes, so a closure stays on the list for two years. Each closed
	// studio is auctioned once.
	public const int AuctionGap = 13;

	private const int ClosureYears = 2;

	private static IndustryEventDef ClosureAuction()
	{
		return new IndustryEventDef
		{
			Id = "auction",
			From = 1985,
			Cooldown = AuctionGap,
			Scripted = true,
			Setup = delegate(IndustryContext c)
			{
				gameScript ip = AuctionLot(out publisherScript closed, out int week);
				if (!ip)
				{
					return false;
				}
				List<publisherScript> bidders = IndustryCore.Studios().Where((publisherScript p) => p.developer && p.stars >= 30f && p.myID != closed.myID).ToList();
				if (bidders.Count == 0)
				{
					return false;
				}
				c.StudioId = closed.myID;
				c.Studio2 = Pick(bidders).myID;
				c.GameId = ip.myID;
				c.Number = week;
				c.Amount = Math.Max(Scaled(20000.0), Landmarks.Landmarks.IpValueOf(ip));
				c.Amount2 = c.Amount * 7 / 5;
				IndustryCore.State.Cooldowns["auction:" + closed.myID] = Now;
				return true;
			},
			Title = (IndustryContext c) => "Closure Auction",
			Text = (IndustryContext c) => StudioName(c.StudioId) + " " + ClosedWhen(c.Number) + ", and its games are being auctioned. The prize: the " + Name(c.GameId) + " series (IP points " + Mathf.RoundToInt(Game(c.GameId) ? Game(c.GameId).ipPunkte : 0f) + "). " + StudioName(c.Studio2) + " is bidding too.",
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Bid the Asking Price", "Costs " + Money(c.Amount) + " if you win", "About a 55% chance", (IndustryContext x) => Auction(x, x.Amount, 0.55f), (IndustryContext x) => Need(x.Amount)),
				Choice("Bid High", "Costs " + Money(c.Amount2) + " if you win", "Almost certain to win", (IndustryContext x) => Auction(x, x.Amount2, 0.95f), (IndustryContext x) => Need(x.Amount2)),
				Choice("Pass", "Nothing to pay", StudioName(c.Studio2) + " gets it", delegate(IndustryContext x)
				{
					GiveIp(Game(x.GameId), IndustryCore.Studio(x.Studio2));
					return StudioName(x.Studio2) + " buys the " + Name(x.GameId) + " series.";
				})
			}
		};
	}

	private static string Auction(IndustryContext c, long price, float chance)
	{
		gameScript ip = Game(c.GameId);
		if (!ip || !G.MS.games_.IsIpFree(ip, messageBox_: false))
		{
			return "The auction was called off.";
		}
		if (Random.value >= chance)
		{
			GiveIp(ip, IndustryCore.Studio(c.Studio2));
			return StudioName(c.Studio2) + " outbids you for the " + ip.GetNameSimple() + " series.";
		}
		G.MS.Pay(price, 31);
		foreach (gameScript g in IndustryCore.AllGames().Where((gameScript x) => x.mainIP == ip.mainIP))
		{
			g.ipToSell = false;
			g.ownerS_ = null;
			g.ownerID = G.MS.myID;
		}
		return "You win: the " + ip.GetNameSimple() + " series is yours, for " + Money(price) + ". Its sequels, remasters and spin-offs are yours to make.";
	}

	private static void GiveIp(gameScript ip, publisherScript to)
	{
		if (!ip || !to || !G.MS.games_.IsIpFree(ip, messageBox_: false))
		{
			return;
		}
		foreach (gameScript g in IndustryCore.AllGames().Where((gameScript x) => x.mainIP == ip.mainIP))
		{
			g.ipToSell = false;
			g.ownerS_ = null;
			g.ownerID = to.myID;
		}
	}

	// When an NPC studio closed: the week the addon saw it close, or else its closing date in the game's data (closures
	// from before the addon watched, or before this version). -1: unknown.
	private static int ClosedWeek(publisherScript p)
	{
		if (IndustryCore.State.RecentClosures.TryGetValue(p.myID, out var week))
		{
			return week;
		}
		if (p.date_year_end > 0)
		{
			return (p.date_year_end * 12 + Mathf.Clamp(p.date_month_end, 1, 12) - 1) * 4;
		}
		return -1;
	}

	private static string ClosedWhen(int week)
	{
		int months = Mathf.Max(0, Now - week) / 4;
		if (months < 1)
		{
			return "has just closed its doors";
		}
		if (months == 1)
		{
			return "closed its doors a month ago";
		}
		return "closed its doors " + months + " months ago";
	}

	// The best series of a studio that closed in the last two years and hasn't been auctioned, once it's free to change
	// hands. One pass over the games, with the same rule as the game's games.IsIpFree.
	private static gameScript AuctionLot(out publisherScript studio, out int closedWeek)
	{
		studio = null;
		closedWeek = -1;
		Dictionary<int, int> closures = IndustryCore.State.RecentClosures;
		foreach (int old in closures.Where((KeyValuePair<int, int> kv) => Now - kv.Value > ClosureYears * 48).Select((KeyValuePair<int, int> kv) => kv.Key).ToList())
		{
			closures.Remove(old);
		}
		Dictionary<int, publisherScript> closed = new Dictionary<int, publisherScript>();
		Dictionary<int, int> when = new Dictionary<int, int>();
		foreach (publisherScript p in G.MS.arrayPublisherScripts ?? new publisherScript[0])
		{
			if (!p || p.isPlayer || p.IsTochterfirma() || !p.Geschlossen() || IndustryCore.State.Cooldowns.ContainsKey("auction:" + p.myID))
			{
				continue;
			}
			int week = ClosedWeek(p);
			if (week >= 0 && Now - week <= ClosureYears * 48)
			{
				closed[p.myID] = p;
				when[p.myID] = week;
			}
		}
		if (closed.Count == 0)
		{
			return null;
		}
		List<gameScript> all = IndustryCore.AllGames();
		HashSet<int> busy = new HashSet<int>();
		foreach (gameScript g in all)
		{
			if (g.inDevelopment || g.schublade || g.pubAngebot || g.auftragsspiel || g.isOnMarket)
			{
				busy.Add(g.mainIP);
			}
		}
		gameScript best = null;
		foreach (gameScript g2 in all)
		{
			if (g2.myID == g2.mainIP && closed.ContainsKey(g2.ownerID) && g2.ipPunkte > 1f && !g2.sonderIP && !busy.Contains(g2.mainIP) && (!best || g2.ipPunkte > best.ipPunkte))
			{
				best = g2;
			}
		}
		if (!best || !G.MS.games_.IsIpFree(best, messageBox_: false))
		{
			return null;
		}
		studio = closed[best.ownerID];
		closedWeek = when[best.ownerID];
		return best;
	}

	// A closed studio never finishes the game it was making (the game stops its development), and that game would
	// keep its series off the market for good. Cancel it, the way the game drops an NPC game it can't make.
	public static void CancelOrphans()
	{
		HashSet<int> closed = new HashSet<int>();
		foreach (publisherScript p in G.MS.arrayPublisherScripts ?? new publisherScript[0])
		{
			if ((bool)p && !p.isPlayer && !p.IsTochterfirma() && p.Geschlossen())
			{
				closed.Add(p.myID);
			}
		}
		if (closed.Count == 0)
		{
			return;
		}
		List<gameScript> orphans = IndustryCore.AllGames().Where((gameScript g) => g.inDevelopment && closed.Contains(g.developerID) && !g.IsMyGame() && !g.pubAngebot && !g.auftragsspiel && !g.typ_contractGame && g.publisherID != G.MS.myID).ToList();
		if (orphans.Count == 0)
		{
			return;
		}
		foreach (gameScript g2 in orphans)
		{
			string text = StudioName(g2.developerID) + " has closed, so " + g2.GetNameSimple() + " will never be finished.";
			Plugin.Log.LogInfo("Cancelled " + g2.GetNameSimple() + " (game " + g2.myID + ") of closed studio " + g2.developerID);
			g2.gameObject.tag = "GameRemoved";
			UnityEngine.Object.Destroy(g2.gameObject);
			if (g2.angekuendigt)
			{
				IndustryCore.Feed(text);
			}
		}
		G.MS.games_.FindGames();
	}

	// ---- A Star Leaves to Found a Studio ----

	// One of your best people leaves to start a real new studio, with one of the addon's own logos nobody uses (and the
	// name that goes with it). How you part sets how it treats you.
	private static IndustryEventDef StarStudio()
	{
		return new IndustryEventDef
		{
			Id = "starstudio",
			From = 1990,
			Cooldown = 104,
			Weight = 0.7f,
			Setup = delegate(IndustryContext c)
			{
				characterScript star = (from e in Employees.All()
					where e.myID != 1 && (e.perks == null || e.perks.Length == 0 || !e.perks[0]) && Employees.BestSkill(e) >= 70f
					orderby Employees.BestSkill(e) descending
					select e).FirstOrDefault();
				int logo = FreeLogo();
				if (!star || logo < 0)
				{
					return false;
				}
				c.Number = star.myID;
				c.Number2 = logo;
				c.Text = CompanyLogos.NameOf(logo) ?? (star.myName.Split(' ').Last() + " Studios");
				c.Amount = IncomeCap(Scaled(60000.0), 0.003f);
				return true;
			},
			Title = (IndustryContext c) => "A Star Leaves",
			Text = delegate(IndustryContext c)
			{
				characterScript p = Person(c.Number);
				return (p ? (p.myName + ", your " + Employees.Profession(p).ToLowerInvariant() + ",") : "One of your best people") + " wants to start a studio of their own: " + c.Text + ". Their mind is made up, but how you part will shape how " + c.Text + " treats you.";
			},
			Default = 0,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Wish Them Well", "They leave as friends", c.Text + " starts out friendly (standing +40)", (IndustryContext x) => Leave(x, 40, 0f)),
				Choice("Back Them", "Costs " + Money(c.Amount), c.Text + " starts stronger, as an ally (standing +65)", delegate(IndustryContext x)
				{
					G.MS.Pay(x.Amount, 33);
					return Leave(x, 65, 10f, x.Amount);
				}, (IndustryContext x) => Need(x.Amount)),
				Choice("Make Them Stay", "A 40% raise", "About half stay; the rest leave bitter (standing -35)", delegate(IndustryContext x)
				{
					characterScript p = Person(x.Number);
					if (!p)
					{
						return "They've already gone.";
					}
					if (Random.value < 0.5f)
					{
						Raise(p, 1.4f);
						p.AddMotivation(15f);
						return p.myName + " stays, with a big raise. " + x.Text + " will have to wait.";
					}
					return Leave(x, -35, 0f);
				})
			}
		};
	}

	private static string Leave(IndustryContext c, int standing, float extraStars, long invested = 0L)
	{
		characterScript p = Person(c.Number);
		if (!p)
		{
			return "They've already gone.";
		}
		string who = p.myName;
		float stars = Mathf.Clamp(Employees.BestSkill(p) * 0.6f + extraStars, 20f, 70f);
		p.Entlassen(eventMitarbeiterMotivation: false);
		publisherScript studio = FoundStudio(c.Text, c.Number2, stars);
		if (!studio)
		{
			return who + " leaves, but the new studio never gets off the ground.";
		}
		// Backing money goes into the studio: its company value (what buying it would cost) includes it.
		studio.firmenwert += invested;
		IndustryCore.Change(studio, standing, who + ((invested > 0) ? " left with your backing" : ((standing >= 0) ? " left on good terms" : " left on bad terms")), quiet: true);
		return who + " leaves and founds " + studio.GetName() + ". " + ((standing >= 0) ? "They'll remember how you treated them." : "They won't forget how it ended.");
	}

	// An addon logo no studio (you included) uses, with its picture loaded.
	private static int FreeLogo()
	{
		if (CompanyLogos.Count == 0 || !G.Gui || G.Gui.logoSprites == null)
		{
			return -1;
		}
		HashSet<int> used = new HashSet<int>((G.MS.arrayPublisherScripts ?? new publisherScript[0]).Where((publisherScript p) => (bool)p).Select((publisherScript p) => p.logoID));
		List<int> free = new List<int>();
		for (int i = CompanyLogos.First; i < CompanyLogos.First + CompanyLogos.Count; i++)
		{
			if (!used.Contains(i) && i < G.Gui.logoSprites.Length && (bool)G.Gui.logoSprites[i])
			{
				free.Add(i);
			}
		}
		if (free.Count == 0)
		{
			return -1;
		}
		return Pick(free);
	}

	// A real new NPC developer, the way the game loads one from its data, opened today. The game saves and loads it
	// like any other studio. The per-studio "bought" lists on engines and platforms are grown to fit its number, or the
	// next save would fail.
	private static publisherScript FoundStudio(string name, int logo, float stars)
	{
		try
		{
			publisher maker = PublisherMaker(G.MS);
			if (!maker)
			{
				return null;
			}
			List<publisherScript> all = (G.MS.arrayPublisherScripts ?? new publisherScript[0]).Where((publisherScript x) => (bool)x).ToList();
			int id = all.Where((publisherScript x) => x.myID < 100000).Select((publisherScript x) => x.myID).DefaultIfEmpty(0).Max() + 1;
			List<publisherScript> devs = all.Where((publisherScript x) => !x.isPlayer && x.developer).ToList();
			publisherScript p = maker.CreatePublisher();
			p.myID = id;
			p.name_EN = name;
			p.logoID = logo;
			p.stars = stars;
			p.share = Random.Range(8, 13);
			p.developer = true;
			p.publisher = false;
			p.onlyMobile = false;
			p.ownPlatform = false;
			p.exklusive = false;
			p.notForSale = false;
			p.fanGenre = IndustryCore.MyGamesOnMarket().GroupBy((gameScript g) => g.maingenre).OrderByDescending((IGrouping<int, gameScript> x) => x.Count()).Select((IGrouping<int, gameScript> x) => x.Key).DefaultIfEmpty(0).First();
			p.developmentSpeed = ((devs.Count > 0) ? Mathf.RoundToInt((float)devs.Average((publisherScript x) => x.developmentSpeed)) : 50);
			p.firmenwert = (long)((float)Prices.Scaled(400000.0) * stars / 40f);
			p.country = (G.MS.myPubS_ ? G.MS.myPubS_.country : 0);
			p.date_year = G.MS.year;
			p.date_month = G.MS.month;
			p.date_year_end = 0;
			p.date_month_end = 0;
			p.ownerID = -1;
			p.isPlayer = false;
			p.tf_geschlossen = false;
			p.relation = 0f;
			p.newGameInWeeks = 1;
			p.newGameInWeeksORG = 1;
			p.tf_engine = -1;
			p.tf_gameTopic = -1;
			p.tf_ownPublisherPriority = -1;
			if (p.tf_ipFocus != null)
			{
				for (int i = 0; i < p.tf_ipFocus.Length; i++)
				{
					p.tf_ipFocus[i] = -1;
				}
			}
			if (p.tf_platformFocus != null)
			{
				for (int j = 0; j < p.tf_platformFocus.Length; j++)
				{
					p.tf_platformFocus[j] = -1;
				}
			}
			p.Init();
			GrowBoughtLists(id);
			p.Unlock();
			IndustryCore.State.KnownOpen.Add(id);
			G.Gui.CreateTopNewsNewPublisher(p.GetName(), p.GetLogo());
			Plugin.Log.LogInfo("Founded studio " + name + " (id " + id + ", logo " + logo + ", stars " + Mathf.RoundToInt(stars) + ")");
			return p;
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Founding a studio failed: " + ex);
			return null;
		}
	}

	private static void GrowBoughtLists(int id)
	{
		int size = Mathf.Max(id + 1, (G.MS.arrayPublisher != null) ? G.MS.arrayPublisher.Length : 0);
		foreach (engineScript e in G.MS.arrayEnginesScripts ?? new engineScript[0])
		{
			if ((bool)e && e.publisherBuyed != null && e.publisherBuyed.Length < size)
			{
				bool[] grown = new bool[size];
				Array.Copy(e.publisherBuyed, grown, e.publisherBuyed.Length);
				e.publisherBuyed = grown;
			}
		}
		foreach (platformScript pl in G.MS.arrayPlatformsScripts ?? new platformScript[0])
		{
			if ((bool)pl && pl.publisherBuyed != null && pl.publisherBuyed.Length < size)
			{
				bool[] grown2 = new bool[size];
				Array.Copy(pl.publisherBuyed, grown2, pl.publisherBuyed.Length);
				pl.publisherBuyed = grown2;
			}
		}
	}

	// ---- Fund a Port ----

	// A platform maker pays toward porting your game to its machine: the port is a real Updates & Addons project, and
	// the discount comes off its price there (GameUpdates.PlanPort) for a year.
	private static IndustryEventDef FundPort()
	{
		return new IndustryEventDef
		{
			Id = "fundport",
			From = 1995,
			Cooldown = 40,
			Setup = delegate(IndustryContext c)
			{
				// The port screen only lists platforms you have a dev kit for, of the same kind as the game's.
				foreach (gameScript g in from x in IndustryCore.MyGamesOnMarket()
					where Standalone(x) && !x.arcade && !x.handy && x.releaseDate <= 0 && x.weeksOnMarket <= 104 && x.reviewTotal >= 60 && GameUpdates.Problem(UpdateKind.Port, x) == null && !IndustryCore.State.PortDeals.Any((PortDeal d) => d.GameId == x.myID)
					orderby Weekly(x) descending
					select x)
				{
					g.FindMyPlatforms();
					HashSet<int> kinds = new HashSet<int>((g.gamePlatformScript ?? new platformScript[0]).Where((platformScript s) => (bool)s).Select((platformScript s) => s.typ));
					List<platformScript> machines = (G.MS.arrayPlatformsScripts ?? new platformScript[0]).Where((platformScript p) => (bool)p && p.isUnlocked && p.inBesitz && !p.vomMarktGenommen && kinds.Contains(p.typ) && p.ownerID != G.MS.myID && !g.gamePlatform.Contains(p.myID)).OrderBy((platformScript p) => p.GetMarktanteil()).ToList();
					if (machines.Count == 0)
					{
						continue;
					}
					platformScript pick = Pick(machines.Take(Mathf.Max(1, (machines.Count + 1) / 2)).ToList());
					c.GameId = g.myID;
					c.PlatformId = pick.myID;
					c.StudioId = pick.ownerID;
					c.Share = 0.5f;
					return true;
				}
				return false;
			},
			Title = (IndustryContext c) => "Fund a Port",
			Text = (IndustryContext c) => "The maker of the " + PlatformName(c.PlatformId) + " wants " + Name(c.GameId) + " on its machine, and offers to pay half of porting it. Start the port in Updates & Addons within a year and the discount is there.",
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Take the Deal", "They pay 50% of the port", "Start it within a year (Updates & Addons)", (IndustryContext x) => PortDeal(x, 0.5f)),
				Choice("Ask for More", "A gamble", "Three quarters, or they walk away", delegate(IndustryContext x)
				{
					if (Random.value < 0.5f)
					{
						return "They agree. " + PortDeal(x, 0.75f);
					}
					return "They walk away and court another studio.";
				}),
				Choice("No Thanks", "Nothing changes", "It stays off their machine", (IndustryContext x) => "You turn the offer down.")
			}
		};
	}

	private static string PlatformName(int id)
	{
		platformScript p = GameUpdates.Platform(id);
		if (!p)
		{
			return "new machine";
		}
		return p.GetName();
	}

	private static string PortDeal(IndustryContext c, float share)
	{
		IndustryCore.State.PortDeals.RemoveAll((PortDeal d) => d.GameId == c.GameId && d.PlatformId == c.PlatformId);
		IndustryCore.State.PortDeals.Add(new PortDeal
		{
			GameId = c.GameId,
			PlatformId = c.PlatformId,
			Share = share,
			Expires = Now + 52
		});
		return "Deal: port " + Name(c.GameId) + " to the " + PlatformName(c.PlatformId) + " within a year (Updates & Addons), and its maker pays " + Mathf.RoundToInt(share * 100f) + "% of it.";
	}

	// ---- CD Burners ----

	// Copying spreads: every copy protection on the market gets weaker (the game's own strength, which feeds sales and
	// Holy's piracy). Update your own, patch your games, or take the losses.
	private static IndustryEventDef CdBurners()
	{
		return new IndustryEventDef
		{
			Id = "cdburn",
			From = 1998,
			To = 2005,
			Cooldown = 156,
			Setup = delegate(IndustryContext c)
			{
				List<gameScript> pc = PcGames();
				if (pc.Count == 0)
				{
					return false;
				}
				c.Amount = OfRevenue(pc.OrderByDescending(Weekly).First(), 0.01f, Scaled(10000.0));
				return true;
			},
			Title = (IndustryContext c) => "CD Burners",
			Text = (IndustryContext c) => "CD burners are in every computer shop now, and copying a game takes minutes. Every copy protection on the market just got easier to beat, yours included.",
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Update Your Copy Protection", "Your own copy protection's update", "Starts now in a free Development room", delegate(IndustryContext x)
				{
					Burn();
					CopyProtection.OwnCopyProtect p = CopyProtection.CopyProtectDev.S.Products.LastOrDefault();
					string problem = ((p == null) ? "You don't make your own copy protection." : CopyProtection.CopyProtectDev.StartUpdate(p, CopyProtection.CopyProtectDev.FreeRooms().FirstOrDefault()));
					if (problem != null)
					{
						return "Copy protections weaken across the market. " + problem;
					}
					return "Copy protections weaken across the market, but your team is already updating " + p.Name + ".";
				}, (IndustryContext x) => (CopyProtection.CopyProtectDev.S.Products.Count == 0) ? "You don't make your own copy protection." : null),
				Choice("Patch Your Games", "Costs " + Money(c.Amount), "A quarter of their pirated copies stop working", delegate(IndustryContext x)
				{
					Burn();
					G.MS.Pay(x.Amount, 15);
					long gone = 0L;
					foreach (gameScript g in PcGames())
					{
						gone += HolyCompat.BreakPirates(g, 0.25f, 0f);
					}
					return "Your PC games get a patch that the copies can't take." + ((gone > 0) ? (" About " + gone + " pirated copies stop working.") : "");
				}, (IndustryContext x) => Need(x.Amount)),
				Choice("Take the Losses", "Nothing to pay", "Your copy protection stays weaker", delegate
				{
					Burn();
					return "Copies spread. Your copy protection stays weaker until you update it, and your games lose the sales it used to protect.";
				})
			}
		};
	}

	private static List<gameScript> PcGames()
	{
		return IndustryCore.MyGamesOnMarket().Where((gameScript g) => g.releaseDate <= 0 && g.gameTyp == 0 && Store.StoreOps.PcFraction(g) > 0f).ToList();
	}

	// Every copy protection on the market loses 15 points of strength.
	private static void Burn()
	{
		foreach (GameObject go in GameObject.FindGameObjectsWithTag("CopyProtect"))
		{
			copyProtectScript cp = (go ? go.GetComponent<copyProtectScript>() : null);
			if ((bool)cp)
			{
				cp.effekt = Mathf.Max(0f, cp.effekt - 15f);
			}
		}
	}

	// ---- A console maker quits hardware ----

	// Alternate history, so it's rare: the game's data already retires makers on schedule (Sega's Dreamcast is its last
	// console). Rolled once a year from 2000 to 2009 (IndustryHooks.Weekly), and only for a maker that's really
	// struggling: its newest console out for 3 years or more, its consoles' combined market share under 10% and the
	// lowest, with at least two other makers left. A newcomer (the first Xbox in its launch years) can't be picked.
	// What happens is real: its consoles on sale are discontinued, the ones it had coming are cancelled (launch dates
	// out of reach), and its own studio goes third-party (the game's own-platform flag off).
	public const float HardwareQuitChance = 0.04f;

	private const int StrugglingYears = 3;

	private const float StrugglingShare = 10f;

	private class ConsoleMaker
	{
		public string Name;

		public publisherScript Studio;

		public List<platformScript> OnSale = new List<platformScript>();

		public List<platformScript> Coming = new List<platformScript>();

		public float Share;

		public int Newest;
	}

	private static List<ConsoleMaker> ConsoleMakers()
	{
		Dictionary<string, ConsoleMaker> makers = new Dictionary<string, ConsoleMaker>();
		foreach (platformScript p in G.MS.arrayPlatformsScripts ?? new platformScript[0])
		{
			if (!p || p.typ != 1 || !p.OwnerIsNPC())
			{
				continue;
			}
			string name = Maker(p);
			if (name.Length == 0)
			{
				continue;
			}
			if (!makers.TryGetValue(name, out var m))
			{
				m = (makers[name] = new ConsoleMaker
				{
					Name = name,
					Studio = MakerStudio(p)
				});
			}
			if (p.isUnlocked && !p.vomMarktGenommen)
			{
				m.OnSale.Add(p);
				m.Share += p.GetMarktanteil();
				m.Newest = Mathf.Max(m.Newest, LaunchWeek(p));
			}
			else if (!p.isUnlocked && p.date_year >= Year && p.date_year < 9999)
			{
				m.Coming.Add(p);
			}
		}
		return makers.Values.Where((ConsoleMaker m) => m.OnSale.Count > 0).ToList();
	}

	private static IndustryEventDef HardwareQuit()
	{
		return new IndustryEventDef
		{
			Id = "hwquit",
			From = 2000,
			To = 2009,
			Cooldown = 999,
			Scripted = true,
			Setup = delegate(IndustryContext c)
			{
				List<ConsoleMaker> makers = ConsoleMakers();
				if (makers.Count < 3)
				{
					return false;
				}
				ConsoleMaker quit = makers.OrderBy((ConsoleMaker m) => m.Share).First();
				if (quit.Coming.Count == 0 || quit.Share >= StrugglingShare || Now - quit.Newest < StrugglingYears * 48)
				{
					return false;
				}
				// Your best game on its consoles, and a console of another maker you could port it to.
				c.Number = 1;
				gameScript mine = (from g in IndustryCore.MyGamesOnMarket()
					where g.releaseDate <= 0 && Standalone(g) && g.gamePlatform != null && quit.OnSale.Any((platformScript p) => g.gamePlatform.Contains(p.myID)) && GameUpdates.Problem(UpdateKind.Port, g) == null
					orderby Weekly(g) descending
					select g).FirstOrDefault();
				if ((bool)mine)
				{
					c.Number = 2;
					platformScript target = (from p in makers.Where((ConsoleMaker m) => m != quit).SelectMany((ConsoleMaker m) => m.OnSale)
						where p.inBesitz && !mine.gamePlatform.Contains(p.myID)
						orderby p.GetMarktanteil() descending
						select p).FirstOrDefault();
					if ((bool)target)
					{
						c.Number = 0;
						c.GameId = mine.myID;
						c.PlatformId = target.myID;
					}
				}
				c.StudioId = (quit.Studio ? quit.Studio.myID : (-1));
				c.Text = quit.Name;
				c.Text2 = QuitConsoles(quit);
				IndustryCore.Feed(c.Text2.Replace("<b>", "").Replace("</b>", ""));
				Plugin.Log.LogInfo("Console maker quits: " + quit.Name + " (market share " + quit.Share.ToString("0.#") + "%)");
				return true;
			},
			Title = (IndustryContext c) => "A Console Maker Quits",
			Text = (IndustryContext c) => c.Text2,
			Default = 2,
			Choices = (IndustryContext c) => new List<IndustryChoice>
			{
				Choice("Hire Their Engineers", "Two Technicians join the job market", "Strong at hardware and engineering", delegate(IndustryContext x)
				{
					Applicant(6, 62f, 76f);
					Applicant(6, 62f, 76f);
					return "Two of " + x.Text + "'s hardware engineers are in the job market now (Staff > hire).";
				}),
				Choice("Port Your Games", PortMaker(c) + " pays half", Game(c.GameId) ? ("Port " + Name(c.GameId) + " to the " + PlatformName(c.PlatformId) + " within a year") : "Move a game to another console", (IndustryContext x) => PortDeal(x, 0.5f), (IndustryContext x) => (x.Number == 1) ? ("You have no game on sale on " + x.Text + "'s consoles that can be ported.") : ((x.Number == 2) ? "You have no dev kit for another maker's console to port it to." : null)),
				Choice("Carry On", "Nothing changes for you", "The market moves on without them", (IndustryContext x) => "The market moves on without " + x.Text + ".")
			}
		};
	}

	// The studio behind a console. The game's console data names only a manufacturer (ownerID -1), so it's the open
	// studio of that name, preferring one that makes games for its own consoles ("Microsoft", "Sony", "Nintendo").
	private static publisherScript MakerStudio(platformScript p)
	{
		if (p.ownerID >= 0)
		{
			return IndustryCore.Studio(p.ownerID);
		}
		string maker = p.manufacturer_EN;
		if (string.IsNullOrEmpty(maker))
		{
			return null;
		}
		return (from s in G.MS.arrayPublisherScripts ?? new publisherScript[0]
			where (bool)s && !s.isPlayer && !s.Geschlossen() && string.Equals(s.name_EN, maker, StringComparison.OrdinalIgnoreCase)
			orderby s.ownPlatform descending
			select s).FirstOrDefault();
	}

	private static string PortMaker(IndustryContext c)
	{
		platformScript p = GameUpdates.Platform(c.PlatformId);
		if (!p || Maker(p).Length == 0)
		{
			return "Another maker";
		}
		return Maker(p);
	}

	// The exit itself, done when the decision comes up: what it ended, for the pop-up.
	private static string QuitConsoles(ConsoleMaker m)
	{
		List<string> ended = new List<string>();
		List<string> cancelled = new List<string>();
		foreach (platformScript p in m.OnSale)
		{
			ended.Add(p.GetName());
			p.RemoveFromMarket();
		}
		foreach (platformScript p2 in (G.MS.arrayPlatformsScripts ?? new platformScript[0]).Where((platformScript x) => (bool)x && x.typ == 1 && x.OwnerIsNPC() && !x.isUnlocked && x.date_year < 9999 && Maker(x) == m.Name))
		{
			cancelled.Add(p2.GetName());
			p2.angekuendigt = false;
			p2.date_year = 9999;
		}
		if ((bool)m.Studio)
		{
			m.Studio.ownPlatform = false;
			m.Studio.exklusive = false;
		}
		string text = "<b>" + m.Name + "</b> is leaving the console business.";
		if (ended.Count > 0)
		{
			text = text + " The " + string.Join(" and the ", ended) + ((ended.Count == 1) ? " is" : " are") + " discontinued";
			text += ((cancelled.Count > 0) ? (", and the " + string.Join(" and the ", cancelled) + " it was working on will never come out.") : ".");
		}
		else if (cancelled.Count > 0)
		{
			text = text + " The " + string.Join(" and the ", cancelled) + " it was working on will never come out.";
		}
		if ((bool)m.Studio)
		{
			text = text + " Its own studio will make games for every console from now on.";
		}
		return text;
	}

	// Saves where 0.29.0's first console-exit headline ("hwexit") cancelled consoles: they get their launch dates back
	// from the game's data. The game only launches a console in the exact month of its date, so one whose date has
	// passed is announced now and comes out next month. Runs once per save.
	public static void RestoreOldExits()
	{
		if (!IndustryCore.State.LastFired.ContainsKey("hwexit"))
		{
			return;
		}
		IndustryCore.State.LastFired.Remove("hwexit");
		Dictionary<int, int[]> dates = PlatformDates();
		foreach (platformScript p in G.MS.arrayPlatformsScripts ?? new platformScript[0])
		{
			if (!p || p.typ != 1 || !p.OwnerIsNPC() || p.isUnlocked || p.date_year != 9999 || !dates.TryGetValue(p.myID, out var date))
			{
				continue;
			}
			int now = G.MS.year * 12 + G.MS.month;
			if (date[0] * 12 + date[1] > now)
			{
				p.date_year = date[0];
				p.date_month = date[1];
				p.angekuendigt = date[0] * 12 + date[1] - now <= 12;
			}
			else
			{
				int next = now + 1;
				p.date_year = (next - 1) / 12;
				p.date_month = (next - 1) % 12 + 1;
				p.angekuendigt = true;
				IndustryCore.Feed(p.GetName() + " is back on: " + Maker(p) + " brings it out next month.");
			}
			Plugin.Log.LogInfo("Restored " + p.GetName() + " (cancelled by the old console-exit headline): out " + p.date_month + "/" + p.date_year);
		}
	}

	// Launch dates from the platform data the game loaded (The Holy Expansion's merged copy when it's there).
	private static Dictionary<int, int[]> PlatformDates()
	{
		Dictionary<int, int[]> dates = new Dictionary<int, int[]>();
		string holy = System.IO.Path.Combine(BepInEx.Paths.PluginPath, "HolyExpansion", "cache", "DATA_Platforms.txt");
		string game = System.IO.Path.Combine(Application.dataPath, "Extern", "Text", "DATA", "Platforms.txt");
		string file = (System.IO.File.Exists(holy) ? holy : game);
		if (!System.IO.File.Exists(file))
		{
			return dates;
		}
		string[] months = new string[12] { "JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC" };
		int id = -1;
		bool wantId = false;
		foreach (string raw in System.IO.File.ReadAllLines(file, System.Text.Encoding.Unicode))
		{
			string line = raw.Trim();
			if (wantId)
			{
				wantId = false;
				int.TryParse(line, out id);
			}
			if (line.StartsWith("[ID]"))
			{
				string rest = line.Substring(4).Trim();
				if (rest.Length > 0)
				{
					int.TryParse(rest, out id);
				}
				else
				{
					wantId = true;
				}
			}
			else if (line.StartsWith("[DATE]") && id >= 0)
			{
				string[] parts = line.Substring(6).Trim().Split(' ');
				int month = ((parts.Length == 2) ? (Array.IndexOf(months, parts[0].ToUpperInvariant()) + 1) : 0);
				if (month > 0 && int.TryParse(parts[1], out var year))
				{
					dates[id] = new int[2] { year, month };
				}
			}
		}
		return dates;
	}

	private static string Maker(platformScript p)
	{
		if (p.ownerID >= 0)
		{
			publisherScript s = IndustryCore.Studio(p.ownerID);
			if ((bool)s)
			{
				return s.GetName();
			}
		}
		return p.manufacturer_EN ?? "";
	}
}
