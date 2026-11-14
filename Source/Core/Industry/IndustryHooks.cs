using System;
using HarmonyLib;
using HolyAddon.Core.Updates;

namespace HolyAddon.Core.Industry;

// Industry events that carry on into the game's own screens.
// - Share a Stand?: when you book your stand at the summer trade fair (Menu_MesseSelect), the studio you agreed with
//   pays half of it and shows its game there. If the fair goes by without you, the deal lapses.
[HarmonyPatch]
internal static class IndustryHooks
{
	private static IndustryState S => IndustryCore.State;

	[HarmonyPrefix]
	[HarmonyPatch(typeof(Menu_MesseSelect), "BUTTON_OK")]
	private static void BeforeStand(out long __state)
	{
		__state = (G.MS ? G.MS.money : 0L);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_MesseSelect), "BUTTON_OK")]
	private static void StandBooked(long __state)
	{
		try
		{
			StandShare share = S.Stand;
			if (share == null || !G.MS || share.Year != G.MS.year)
			{
				return;
			}
			long paid = __state - G.MS.money;
			if (paid <= 0)
			{
				// Nothing booked (no game picked): the game asked again.
				return;
			}
			S.Stand = null;
			publisherScript p = IndustryCore.Studio(share.StudioId);
			if (!p || p.Geschlossen())
			{
				return;
			}
			long half = paid / 2;
			G.MS.Pay(-half, 17);
			gameScript theirs = GameUpdates.FindGame(share.GameId);
			theirs?.AddHype(3f);
			foreach (gameScript g in IndustryCore.MyGamesInDevelopment())
			{
				g.AddHype(3f);
			}
			IndustryCore.Change(p, 8, "Shared your trade-fair stand");
			string text = "<b>" + p.GetName() + "</b> pays half your trade-fair stand (" + G.Money(half) + ")" + (theirs ? (" and shows " + theirs.GetNameSimple() + " next to your games") : "") + ". Hype +3 for both.";
			IndustryCore.News(text);
			IndustryCore.Feed(text.Replace("<b>", "").Replace("</b>", ""));
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Shared trade-fair stand failed: " + ex);
		}
	}

	public static void Weekly()
	{
		StandShare share = S.Stand;
		if (share == null || !G.MS)
		{
			return;
		}
		// The fair is in July's first week: by the end of July (or another year) it's gone by without you.
		if (G.MS.year != share.Year || G.MS.month >= 8)
		{
			S.Stand = null;
			publisherScript p = IndustryCore.Studio(share.StudioId);
			IndustryCore.Change(p, -6, "You skipped the trade fair after promising them half a stand");
			if ((bool)p)
			{
				IndustryCore.Feed("You didn't go to the trade fair, so " + p.GetName() + " missed it too.");
			}
		}
	}
}
