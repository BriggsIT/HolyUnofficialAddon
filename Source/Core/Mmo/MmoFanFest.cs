using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HolyAddon.Core.Awards;
using HolyAddon.Core.CopyProtection;
using HolyAddon.Core.Updates;
using HolyAddon.UI;
using UnityEngine;

namespace HolyAddon.Core.Mmo;

// What you pick for this year's fan festival.
internal class FanFestChoice
{
	public int GameId = -1;

	public int Venue = 1;

	public int Announce;

	public bool Gifts;

	public bool Merch = true;

	public bool Panels = true;
}

// What a fan festival would bring.
internal class FanFestOutcome
{
	public long Cost;

	public float Hype;

	public float Interest;

	public long Returning;

	public long Merch;

	public int Fans;

	public float Reviews;

	public int Seats;
}

// A yearly fan festival for your MMOs (in the style of the big publishers' own conventions), as a pop-up in the
// second week of October: pick the MMO to feature, the venue, an announcement and extras, or skip it this year.
[HarmonyPatch]
internal static class MmoFanFest
{
	public const int Month = 10;

	public const int Week = 2;

	public static readonly string[] VenueNames = new string[4] { "Livestream only", "Hotel ballroom", "Convention centre", "Arena" };

	private static readonly long[] VenueCost = new long[4] { 15000L, 120000L, 500000L, 1800000L };

	private static readonly int[] VenueSeats = new int[4] { 0, 2500, 15000, 45000 };

	private static readonly float[] VenueReach = new float[4] { 0.35f, 0.6f, 1f, 1.4f };

	private static readonly float[] VenueHype = new float[4] { 3f, 6f, 10f, 14f };

	private static readonly float[] VenueInterest = new float[4] { 2f, 4f, 6f, 8f };

	public static readonly string[] AnnounceNames = new string[4] { "No big announcement", "Next content patch", "A new expansion pack", "Classic servers" };

	public static FanFestChoice Choice = new FanFestChoice();

	private static bool pending;

	private static bool shown;

	private static MmoState S => MmoOps.State;

	public static bool On
	{
		get
		{
			// The yearly convention (Core/Events/StudioCon) takes its place when on: MMOs go on its stage.
			if (MmoOps.On && !Events.StudioCon.On)
			{
				return Plugin.MmoFanFest.Value;
			}
			return false;
		}
	}

	public static int Year
	{
		get
		{
			if (!G.MS)
			{
				return 0;
			}
			return G.MS.year;
		}
	}

	public static float Inflation()
	{
		return 1f + (float)Mathf.Max(0, Year - 1995) * 0.04f;
	}

	public static string FestName()
	{
		string studio = (G.MS ? G.MS.GetCompanyName() : "");
		return (string.IsNullOrEmpty(studio) ? "FanFest" : (studio + " FanFest")) + " " + Year;
	}

	public static bool ClassicAnnounceable(gameScript g)
	{
		if (!g)
		{
			return false;
		}
		MmoGame m = MmoOps.Get(g);
		if (!m.ClassicOn)
		{
			return MmoLive.PlanClassic(g, 0).Problem == null;
		}
		return false;
	}

	public static FanFestOutcome Estimate(FanFestChoice c, gameScript g)
	{
		FanFestOutcome o = new FanFestOutcome();
		if (!g)
		{
			return o;
		}
		int v = Mathf.Clamp(c.Venue, 0, VenueNames.Length - 1);
		float infl = Inflation();
		float reach = VenueReach[v];
		long subs = g.abonnements;
		o.Seats = VenueSeats[v];
		o.Cost = (long)((float)VenueCost[v] * infl);
		o.Hype = VenueHype[v];
		o.Interest = VenueInterest[v];
		switch (c.Announce)
		{
		case 1:
			o.Interest += 3f;
			o.Hype += 2f;
			break;
		case 2:
			o.Hype += 10f;
			o.Interest += 2f;
			break;
		case 3:
			o.Hype += 6f;
			o.Interest += 4f;
			break;
		}
		if (c.Gifts)
		{
			o.Cost += (long)((float)subs * 0.05f);
			o.Returning = (long)((float)MmoLive.Lapsed(g) * 0.02f * reach);
		}
		if (c.Merch)
		{
			o.Cost += (long)(20000f * infl);
			o.Merch = (long)(((float)o.Seats * 30f + (float)subs * 0.03f * reach) * infl);
		}
		if (c.Panels)
		{
			o.Cost += (long)(10000f * infl);
			o.Reviews = 0.01f * reach;
		}
		o.Fans = Mathf.RoundToInt(Mathf.Min(2000000f, (float)subs * 0.01f * reach + (float)o.Seats * 0.5f));
		o.Cost = (long)Mathf.Round((float)o.Cost / 1000f) * 1000;
		return o;
	}

	public static gameScript Featured()
	{
		List<gameScript> mmos = MmoOps.MyMmos();
		return mmos.FirstOrDefault((gameScript g) => g.myID == Choice.GameId) ?? mmos.FirstOrDefault();
	}

	// Holds the festival; returns why it couldn't, or null with the report of how it went.
	public static string Hold(out string report)
	{
		report = null;
		gameScript g = Featured();
		if (!g)
		{
			return "You have no MMO on the market to feature.";
		}
		FanFestOutcome o = Estimate(Choice, g);
		if (G.MS.NotEnoughMoney(o.Cost))
		{
			return "You need " + G.Money(o.Cost) + " for this.";
		}
		G.MS.Pay(o.Cost, 16);
		g.AddHype(o.Hype);
		g.AddMMOInteresse(o.Interest);
		long back = 0L;
		if (o.Returning > 0)
		{
			back = MmoOps.PlaceOnServers(o.Returning);
			g.abonnements += back;
			g.abonnementsWoche += back;
			if (g.bestAbonnements < g.abonnements)
			{
				g.bestAbonnements = g.abonnements;
			}
		}
		if (o.Merch > 0)
		{
			G.MS.Earn(o.Merch, 11);
		}
		if (o.Fans > 0)
		{
			G.MS.AddFans(o.Fans, g.maingenre);
		}
		if (o.Reviews > 0f)
		{
			HolyCompat.ShiftUserReviews(g, o.Reviews);
		}
		if (Choice.Venue >= 2)
		{
			G.MS.AddStudioPoints(Choice.Venue - 1);
		}
		S.FanFestYear = Year;
		S.FanFests.Add(new FanFestRecord
		{
			Year = Year,
			GameId = g.myID,
			Game = g.GetNameSimple(),
			Venue = Choice.Venue,
			Cost = o.Cost,
			Income = o.Merch
		});
		GameUpdates.Touch();
		List<string> lines = new List<string>
		{
			"Hype +" + Mathf.RoundToInt(o.Hype) + ", interest +" + Mathf.RoundToInt(o.Interest) + ", " + MmoUiText.Num(o.Fans) + " new fans."
		};
		if (back > 0)
		{
			lines.Add(MmoUiText.Num(back) + " former players came back for the gifts.");
		}
		if (o.Merch > 0)
		{
			lines.Add("The merch store took " + G.Money(o.Merch) + ".");
		}
		if (o.Reviews > 0f)
		{
			lines.Add("Players loved the developer panels: user reviews +" + Mathf.Max(1, Mathf.RoundToInt(o.Reviews * 100f)) + "%.");
		}
		string venue = ((Choice.Venue == 0) ? "streamed online" : ("held at a " + VenueNames[Choice.Venue].ToLower() + " with " + MmoUiText.Num(o.Seats) + " fans"));
		string announce = ((Choice.Announce > 0) ? (" The big news: " + AnnounceNames[Choice.Announce].ToLower() + ".") : "");
		if ((bool)G.Gui)
		{
			G.Gui.AddHistory(FestName() + ": " + g.GetNameSimple());
		}
		report = "<b>" + FestName() + "</b> was " + venue + ", all about <b>" + g.GetNameSimple() + "</b>." + announce + "\n\n" + string.Join(" ", lines);
		return null;
	}

	public static void Skip()
	{
		S.FanFestYear = Year;
		GameUpdates.Touch();
	}

	// ---- When ----

	[HarmonyPostfix]
	[HarmonyPatch(typeof(mainScript), "WochenUpdates")]
	private static void Weekly(mainScript __instance)
	{
		try
		{
			if (On && __instance.month == Month && __instance.week == Week && S.FanFestYear != __instance.year && MmoOps.MyMmos().Count > 0)
			{
				pending = true;
				shown = false;
				gameScript top = MmoOps.MyMmos().FirstOrDefault();
				Choice = new FanFestChoice
				{
					GameId = (top ? top.myID : (-1))
				};
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Fan festival: " + ex);
		}
	}

	// Opens the pop-up once nothing else is on screen; closing it without a choice skips this year's festival.
	public static void Tick()
	{
		if (pending && (bool)G.Gui && !G.Gui.menuOpen)
		{
			pending = false;
			shown = true;
			EditorWindow.FanFest.Open();
			AwardsShow.Sound(50);
		}
		else if (shown && !EditorWindow.FanFest.IsOpen)
		{
			shown = false;
			if (S.FanFestYear != Year)
			{
				Skip();
			}
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(savegameScript), "Load")]
	private static void BeforeLoad()
	{
		pending = false;
		shown = false;
	}
}
