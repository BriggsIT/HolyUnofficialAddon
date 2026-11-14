using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HolyAddon.Core.Industry;

// Rivals, merged from The Holy Expansion's Rival Studio (Holy's own is switched off, HolyCompat).
// - Rivalry is your standing with a studio below zero: Rival tier is -40 or lower, and Holy's "rivalry 60%" is
//   standing -60.
// - Your arch-rival is the Rival-tier studio you get on worst with. It makes Holy's direct moves against you on its
//   own clock (Core/Industry/IndustryEventsRival): every 9-18 weeks, sooner the worse things are, quiet for 26 weeks
//   after a truce.
// - If no rivalry has formed once you're established (2 games out, studio level 2), a studio your size declares you
//   its rival, Holy's way.
// - Your moves (Poach a Developer, Spread Rumours, Make Peace, Drive Them Out) work on any Rival-tier studio, at Holy's
//   costs and odds, from the studio's page in the Industry window.
internal static class IndustryRivals
{
	private static IndustryState S => IndustryCore.State;

	private static int Now => IndustryCore.Now;

	public static bool On
	{
		get
		{
			if (Plugin.MergedRival.Value)
			{
				return IndustryCore.On;
			}
			return false;
		}
	}

	private static bool Open(publisherScript p)
	{
		if ((bool)p && !p.isPlayer && !p.Geschlossen())
		{
			return !p.IsTochterfirma();
		}
		return false;
	}

	public static bool IsRival(publisherScript p)
	{
		if (Open(p))
		{
			return IndustryCore.Tier(IndustryCore.Standing(p)) == 0;
		}
		return false;
	}

	// Holy's rivalry %: how far below zero your standing is.
	public static int Rivalry(publisherScript p)
	{
		return Mathf.Clamp(Mathf.RoundToInt(0f - IndustryCore.Standing(p)), 0, 100);
	}

	public static publisherScript Arch
	{
		get
		{
			if (!On || S.ArchRival < 0)
			{
				return null;
			}
			publisherScript p = IndustryCore.Studio(S.ArchRival);
			if (!IsRival(p))
			{
				return null;
			}
			return p;
		}
	}

	public static bool IsArch(publisherScript p)
	{
		if ((bool)p)
		{
			return Arch == p;
		}
		return false;
	}

	// Weeks until the arch-rival's next move (0: any time now).
	public static int NextMoveIn
	{
		get
		{
			return Mathf.Max(0, Mathf.Max(S.RivalNextMove, S.RivalQuietUntil) - Now);
		}
	}

	// ---- Each week, from IndustryCore ----

	public static void Weekly()
	{
		if (!On)
		{
			return;
		}
		try
		{
			CarryHoly();
			Comeback();
			KeepArch();
			Clock();
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Rivals: " + ex);
		}
	}

	private static void KeepArch()
	{
		if (S.ArchRival >= 0)
		{
			publisherScript a = IndustryCore.Studio(S.ArchRival);
			if (!IsRival(a))
			{
				if ((bool)a)
				{
					string name = "<b>" + a.GetName() + "</b>";
					News(a.Geschlossen() ? (name + " has closed its doors. The feud is over.") : (a.IsTochterfirma() ? (name + " is now part of a bigger company. The feud is over.") : (name + " no longer sees you as its rival.")));
				}
				S.ArchRival = -1;
				S.RivalDeclareAfter = Mathf.Max(S.RivalDeclareAfter, Now + 26);
			}
		}
		if (S.ArchRival >= 0)
		{
			return;
		}
		publisherScript worst = IndustryCore.Studios().Where(IsRival).OrderBy(IndustryCore.Standing).FirstOrDefault();
		if ((bool)worst)
		{
			Become(worst);
			News("<b>" + worst.GetName() + "</b> is now your arch-rival.");
		}
		else if (Now >= S.RivalDeclareAfter && Established())
		{
			Declare();
		}
	}

	public static bool Established()
	{
		if (!G.MS || IndustryCore.MyGamesOnMarket().Count < 2)
		{
			return false;
		}
		return G.MS.GetStudioLevel(G.MS.studioPoints) >= 2;
	}

	// Holy's pick: a developer about your size declares you its rival.
	private static void Declare()
	{
		float mine = (G.MS.myPubS_ ? G.MS.myPubS_.stars : ((float)(G.MS.GetStudioLevel(G.MS.studioPoints) * 10)));
		publisherScript p = (from x in IndustryCore.Studios()
			where x.developer && IndustryCore.Tier(IndustryCore.Standing(x)) < 3
			orderby Mathf.Abs(x.stars - mine) + UnityEngine.Random.Range(0f, 8f)
			select x).FirstOrDefault();
		if ((bool)p)
		{
			SetStanding(p, -40f, "Declared your studio its rival");
			Become(p);
			string text = "<b>" + p.GetName() + "</b> has declared your studio its rival. Expect trouble.";
			News(text);
		}
	}

	private static void Become(publisherScript p)
	{
		S.ArchRival = p.myID;
		S.RivalNextMove = Now + UnityEngine.Random.Range(10, 18);
		Note(p, "Became your arch-rival");
	}

	// The arch-rival's moves: one every 9-18 weeks, sooner the worse things are (Holy's pace).
	private static void Clock()
	{
		publisherScript a = Arch;
		if (!a)
		{
			return;
		}
		if (S.RivalNextMove < 0)
		{
			S.RivalNextMove = Now + UnityEngine.Random.Range(10, 18);
		}
		if (Now < S.RivalNextMove || Now < S.RivalQuietUntil || S.Pending != null || !IndustryCore.Quiet())
		{
			return;
		}
		S.RivalNextMove = Now + Mathf.RoundToInt(Mathf.Lerp(18f, 9f, (float)Rivalry(a) / 100f) + UnityEngine.Random.Range(0f, 6f));
		List<string> moves = new List<string> { "rv_taunt", "rv_poach", "rv_copy", "rv_clash", "rv_clash" };
		foreach (string id in moves.OrderBy((string _) => UnityEngine.Random.value).Distinct())
		{
			IndustryEventDef d = IndustryEvents.Find(id);
			IndustryContext c = new IndustryContext();
			if (d != null && d.Setup(c))
			{
				IndustryCore.Force(new IndustryPending
				{
					Id = id,
					C = c,
					Week = Now
				});
				return;
			}
		}
	}

	// A studio Holy's way drove out may come back, angrier.
	private static void Comeback()
	{
		if (S.ComebackId < 0 || Now < S.ComebackWeek)
		{
			return;
		}
		publisherScript p = IndustryCore.Studio(S.ComebackId);
		S.ComebackId = -1;
		if (!p || !p.Geschlossen() || p.IsTochterfirma())
		{
			return;
		}
		string old = p.GetName();
		string reborn = (old.EndsWith(" Reborn") ? old : (old + " Reborn"));
		p.name_EN = (p.name_GE = (p.name_TU = (p.name_CH = (p.name_FR = (p.name_JA = (p.name_UA = (p.name_TH = reborn)))))));
		p.tf_geschlossen = false;
		p.date_year_end = 0;
		p.date_month_end = 0;
		p.stars = 35f;
		S.KnownClosed.Remove(p.myID);
		SetStanding(p, -70f, "Came back for revenge");
		Become(p);
		S.RivalNextMove = Now + UnityEngine.Random.Range(4, 9);
		if ((bool)G.Gui)
		{
			G.Gui.CreateTopNewsNewPublisher(reborn, p.GetLogo());
		}
		News("The founders of <b>" + old + "</b> are back with a new studio, <b>" + reborn + "</b>, and they want revenge.");
	}

	// As soon as the save is up (the Industry window or Holy's Rival Studio entry asks), not only at the next week:
	// Holy's rival carries over, and the rival you get on worst with becomes your arch-rival.
	public static void Ensure()
	{
		if (!On || !G.MS)
		{
			return;
		}
		try
		{
			CarryHoly();
			if (S.ArchRival < 0)
			{
				publisherScript worst = IndustryCore.Studios().Where(IsRival).OrderBy(IndustryCore.Standing).FirstOrDefault();
				if ((bool)worst)
				{
					Become(worst);
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Rivals: " + ex.Message);
		}
	}

	// Holy's rival, record and log carry over once (Holy's Rival is switched off from now on).
	private static void CarryHoly()
	{
		if (S.HolyRivalCarried)
		{
			return;
		}
		object r = HolyCompat.HolyState("Rival");
		if (r == null)
		{
			// Without Holy there's nothing to carry; with Holy, its save state isn't up yet: try again later.
			if (HolyCompat.HolyType("HolyExpansion.Core.HolyState") == null)
			{
				S.HolyRivalCarried = true;
			}
			return;
		}
		S.HolyRivalCarried = true;
		S.RivalWins += HolyCompat.Get<int>(r, "Wins");
		S.RivalLosses += HolyCompat.Get<int>(r, "Losses");
		S.PoachedFromUs += HolyCompat.Get<int>(r, "PoachedFromUs");
		S.PoachedFromThem += HolyCompat.Get<int>(r, "PoachedFromThem");
		S.RivalsDrivenOut += HolyCompat.Get<int>(r, "Defeated");
		int comeback = HolyCompat.Get<int>(r, "ComebackId");
		if (comeback >= 0 && HolyCompat.Get<int>(r, "ComebackWeek") > 0 && S.ComebackId < 0)
		{
			S.ComebackId = comeback;
			S.ComebackWeek = HolyCompat.Get<int>(r, "ComebackWeek");
		}
		publisherScript p = IndustryCore.Studio(HolyCompat.Get<int>(r, "RivalId"));
		if (!Open(p))
		{
			return;
		}
		float rivalry = HolyCompat.Get<float>(r, "Rivalry");
		List<string> log = HolyCompat.Get<List<string>>(r, "Log");
		if (log != null)
		{
			foreach (string line in log.Take(6).Reverse())
			{
				Note(p, line);
			}
		}
		SetStanding(p, 0f - Mathf.Max(40f, rivalry), "Your rival from before (rivalry " + Mathf.RoundToInt(rivalry) + "%)");
		Become(p);
		S.RivalNextMove = Mathf.Clamp(HolyCompat.Get<int>(r, "NextMoveWeek"), Now + 4, Now + 18);
		Plugin.Log.LogInfo("Rivals: Holy's rival " + p.GetName() + " (rivalry " + Mathf.RoundToInt(rivalry) + "%) is now your arch-rival");
	}

	// ---- Your moves (on any Rival-tier studio's page) ----

	public static long PoachCost => Prices.Scaled(70000.0);

	public static long RumourCost => Prices.Scaled(15000.0);

	public static long PeaceCost => Prices.Scaled(120000.0);

	// Where the studio stands by company value among the open studios (0 smallest, 1 biggest), as Holy reckons it.
	public static float Size(publisherScript p)
	{
		List<publisherScript> all = IndustryCore.Studios();
		if (all.Count < 2 || !p)
		{
			return 0.5f;
		}
		long value = p.GetFirmenwert();
		return (float)all.Count((publisherScript x) => x != p && x.GetFirmenwert() < value) / (float)(all.Count - 1);
	}

	public static long DriveOutCost(publisherScript p)
	{
		return (long)Math.Round((double)Mathf.Lerp(20000000f, 150000000f, Size(p)) / 1000000.0) * 1000000;
	}

	public static float DriveOutChance(publisherScript p)
	{
		return Mathf.Lerp(0.75f, 0.15f, Size(p));
	}

	public static long Cost(string move, publisherScript p)
	{
		return move switch
		{
			"poach" => PoachCost,
			"rumours" => RumourCost,
			"peace" => PeaceCost,
			_ => DriveOutCost(p),
		};
	}

	// Why a move can't be made now (null: it can).
	public static string Problem(string move, publisherScript p)
	{
		if (!On)
		{
			return "Rivals are switched off in Game settings.";
		}
		if (!Open(p))
		{
			return "This studio is gone.";
		}
		if (!IsRival(p))
		{
			return "Only against a studio at Rival tier (standing -40 or lower).";
		}
		if (S.Cooldowns.TryGetValue("rv:" + move, out var ready) && Now < ready)
		{
			int weeks = ready - Now;
			return "You can try again in " + ((weeks == 1) ? "1 week." : (weeks + " weeks."));
		}
		if (move == "driveout")
		{
			if (Rivalry(p) < 100)
			{
				return "Only at 100% rivalry (now " + Rivalry(p) + "%).";
			}
			if (!p.CloseFree())
			{
				return "You have a contract with them or they publish one of your games, so you can't shut them down now.";
			}
		}
		long cost = Cost(move, p);
		if ((bool)G.MS && G.MS.NotEnoughMoney(cost))
		{
			return "You need " + G.Money(cost) + ".";
		}
		return null;
	}

	private static void Wait(string move, int weeks)
	{
		S.Cooldowns["rv:" + move] = Now + weeks;
	}

	public static string Poach(publisherScript p)
	{
		string why = Problem("poach", p);
		if (why != null)
		{
			return why;
		}
		G.MS.Pay(PoachCost, 9);
		Wait("poach", 12);
		string who = StrongApplicant();
		IndustryCore.AddStars(p, -2f);
		S.PoachedFromThem++;
		IndustryCore.Change(p, -8, "You lured one of their senior developers away");
		return "You lured a senior developer away from " + p.GetName() + ". " + (who ?? "They") + " is waiting on the job market (Staff > hire).";
	}

	public static string Rumours(publisherScript p)
	{
		string why = Problem("rumours", p);
		if (why != null)
		{
			return why;
		}
		G.MS.Pay(RumourCost, 12);
		Wait("rumours", 8);
		if (UnityEngine.Random.value < 0.6f)
		{
			IndustryCore.AddStars(p, 0f - UnityEngine.Random.Range(2f, 5f));
			S.RivalWins++;
			IndustryCore.Change(p, -6, "You spread rumours about them");
			return "Rumours about " + p.GetName() + " spread fast. Their reputation took a hit.";
		}
		G.MS.AddStudioPoints(-20);
		S.RivalLosses++;
		IndustryCore.Change(p, -6, "Your rumours about them were traced back to you");
		return "A journalist traced the rumours back to you. Your reputation suffered (studio points -20).";
	}

	public static string Peace(publisherScript p)
	{
		string why = Problem("peace", p);
		if (why != null)
		{
			return why;
		}
		G.MS.Pay(PeaceCost, 13);
		Wait("peace", 26);
		bool arch = IsArch(p);
		IndustryCore.Change(p, 35, "You agreed a truce over dinner");
		if (arch)
		{
			S.RivalQuietUntil = Now + 26;
		}
		Morale(2f);
		return "You and " + p.GetName() + " agreed a truce over dinner. It should be quiet for a while.\n\nStanding +35" + (arch ? ", no moves against you for 26 weeks" : "") + ", staff morale +2.";
	}

	public static string DriveOut(publisherScript p)
	{
		string why = Problem("driveout", p);
		if (why != null)
		{
			return why;
		}
		Wait("driveout", 48);
		string name = p.GetName();
		if (UnityEngine.Random.value >= DriveOutChance(p))
		{
			foreach (gameScript g in IndustryCore.MyGamesOnMarket())
			{
				IndustryCore.AddMod(g, -0.15f, 12);
			}
			foreach (publisherScript x in IndustryCore.Studios())
			{
				x.relation = Mathf.Max(0f, x.relation - 3f);
			}
			S.RivalLosses++;
			News("Your attempt to take over <b>" + name + "</b> fell through, and the press found out.");
			return "Your attempt to take over " + name + " fell through, and the press found out. Players and publishers are not impressed.\n\nYour games' sales -15% for 12 weeks, publisher relations -3, next try in a year.";
		}
		long cost = DriveOutCost(p);
		G.MS.Pay(cost, 13);
		if ((bool)G.Gui)
		{
			G.Gui.CreateTopNewsRemovePublisher(name, p.GetLogo());
		}
		if (!S.KnownClosed.Contains(p.myID))
		{
			S.KnownClosed.Add(p.myID);
		}
		bool arch = IsArch(p);
		p.CloseNpcStudio();
		S.RivalsDrivenOut++;
		G.MS.AddStudioPoints(15);
		int fans = Fans(0.02f);
		Morale(5f);
		if (arch)
		{
			S.ArchRival = -1;
		}
		S.RivalDeclareAfter = Now + 48;
		if (UnityEngine.Random.value < 0.6f)
		{
			S.ComebackId = p.myID;
			S.ComebackWeek = Now + UnityEngine.Random.Range(48, 120);
		}
		News("You bought up <b>" + name + "</b>'s debts and shut the studio down.");
		return "You bought up " + name + "'s debts and shut the studio down for " + G.Money(cost) + ".\n\nStudio points +15, " + fans + " new fans, staff morale +5. No studio will declare you its rival for at least a year.";
	}

	// ---- Helpers ----

	// A strong developer in the job market (Holy: a fresh applicant's skills x1.6).
	private static string StrongApplicant()
	{
		arbeitsmarkt market = UnityEngine.Object.FindObjectOfType<arbeitsmarkt>();
		charArbeitsmarkt c = (market ? market.CreateArbeitsmarktItem() : null);
		if (!c)
		{
			return null;
		}
		c.Create(null);
		c.s_gamedesign *= 1.6f;
		c.s_programmieren *= 1.6f;
		c.s_grafik *= 1.6f;
		c.s_sound *= 1.6f;
		c.s_pr *= 1.6f;
		c.s_gametests *= 1.6f;
		c.s_technik *= 1.6f;
		c.s_forschen *= 1.6f;
		return c.myName;
	}

	public static void Morale(float points)
	{
		foreach (characterScript c in Employees.All())
		{
			c.AddMotivation(points);
		}
	}

	// A share of your fans (at least 300), for wins.
	public static int Fans(float share)
	{
		genres gen = IndustryCore.Genres;
		long total = 0L;
		if ((bool)gen && gen.genres_FANS != null)
		{
			for (int i = 0; i < gen.genres_FANS.Length; i++)
			{
				total += gen.genres_FANS[i];
			}
		}
		int fans = Mathf.Clamp(Mathf.RoundToInt((float)total * share), 300, 500000);
		if ((bool)G.MS)
		{
			G.MS.AddFans(fans, -1);
		}
		return fans;
	}

	// Moves your standing with a studio to a value, without the usual "now sees you as" news.
	private static void SetStanding(publisherScript p, float target, string why)
	{
		int delta = Mathf.RoundToInt(target - IndustryCore.Standing(p));
		if (delta < 0)
		{
			IndustryCore.Change(p, delta, why, quiet: true);
		}
	}

	public static void Note(publisherScript p, string text)
	{
		if (!p)
		{
			return;
		}
		if (!S.Notes.TryGetValue(p.myID, out var notes))
		{
			notes = (S.Notes[p.myID] = new List<RelationNote>());
		}
		notes.Insert(0, new RelationNote
		{
			Week = Now,
			Delta = 0,
			Text = text
		});
		if (notes.Count > 12)
		{
			notes.RemoveAt(notes.Count - 1);
		}
	}

	private static void News(string text)
	{
		IndustryCore.News(text);
		IndustryCore.Feed(text);
	}
}
