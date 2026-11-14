using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.Core.Industry;

// Publishers Bid: the publisher you went with has paid an advance to publish your game. When the game is done, the
// game's own publisher list shows them at the top as "Your Deal", even if your review score is below what they'd
// normally take. Publishing with them keeps them happy; publishing with anyone else (or yourself) means paying the
// advance back, and they remember it.
[HarmonyPatch]
internal static class PublishDeals
{
	// The game the publisher list is open for (its items fill themselves in a frame later).
	private static int listingFor = -1;

	private static readonly AccessTools.FieldRef<Menu_Dev_SelectPublisher, textScript> TextOf = AccessTools.FieldRefAccess<Menu_Dev_SelectPublisher, textScript>("tS_");

	private static readonly AccessTools.FieldRef<Menu_Dev_SelectPublisher, sfxScript> SfxOf = AccessTools.FieldRefAccess<Menu_Dev_SelectPublisher, sfxScript>("sfx_");

	private static readonly AccessTools.FieldRef<Menu_Dev_SelectPublisher, genres> GenresOf = AccessTools.FieldRefAccess<Menu_Dev_SelectPublisher, genres>("genres_");

	public static void Add(gameScript g, publisherScript p, long advance)
	{
		if (!g || !p)
		{
			return;
		}
		IndustryCore.State.Deals.RemoveAll((PublishDeal d) => d.GameId == g.myID);
		IndustryCore.State.Deals.Add(new PublishDeal
		{
			GameId = g.myID,
			StudioId = p.myID,
			Advance = advance
		});
	}

	public static PublishDeal For(int gameId)
	{
		return IndustryCore.State.Deals?.FirstOrDefault((PublishDeal d) => d.GameId == gameId);
	}

	// Still around to publish it (closed studios and closed subsidiaries can't).
	private static bool Valid(publisherScript p)
	{
		if ((bool)p && !p.Geschlossen())
		{
			return !p.TochterfirmaGeschlossen();
		}
		return false;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_Dev_SelectPublisher), "Init")]
	private static void Listed(Menu_Dev_SelectPublisher __instance, gameScript game_)
	{
		try
		{
			listingFor = (game_ ? game_.myID : (-1));
			PublishDeal deal = (game_ ? For(game_.myID) : null);
			if (deal == null)
			{
				return;
			}
			publisherScript p = IndustryCore.Studio(deal.StudioId);
			if (!Valid(p))
			{
				return;
			}
			Transform list = __instance.uiObjects[0].transform;
			Item_SelectPublisher item = list.GetComponentsInChildren<Item_SelectPublisher>(includeInactive: true).FirstOrDefault((Item_SelectPublisher i) => i.pS_ == p);
			if (!item)
			{
				// Below their usual review bar: the game left them out, but they've already paid.
				item = UnityEngine.Object.Instantiate(__instance.uiPrefabs[0], Vector3.zero, Quaternion.identity, list).GetComponent<Item_SelectPublisher>();
				item.pS_ = p;
				item.mS_ = G.MS;
				item.tS_ = TextOf(__instance);
				item.sfx_ = SfxOf(__instance);
				item.guiMain_ = G.Gui;
				item.genres_ = GenresOf(__instance);
				G.Gui.KeinEintrag(__instance.uiObjects[0], __instance.uiObjects[4]);
			}
			item.transform.SetAsFirstSibling();
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Publishing deal in the publisher list failed: " + ex);
		}
	}

	// Your deal stays at the top whatever the list is sorted by.
	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_Dev_SelectPublisher), "DROPDOWN_Sort")]
	private static void KeepOnTop(Menu_Dev_SelectPublisher __instance)
	{
		try
		{
			PublishDeal deal = For(listingFor);
			if (deal == null || __instance.uiObjects == null || !__instance.uiObjects[0])
			{
				return;
			}
			Item_SelectPublisher item = __instance.uiObjects[0].GetComponentsInChildren<Item_SelectPublisher>(includeInactive: true).FirstOrDefault((Item_SelectPublisher i) => (bool)i.pS_ && i.pS_.myID == deal.StudioId);
			if ((bool)item)
			{
				item.transform.SetAsFirstSibling();
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Publishing deal sort failed: " + ex);
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Item_SelectPublisher), "SetData")]
	private static void Label(Item_SelectPublisher __instance)
	{
		try
		{
			PublishDeal deal = For(listingFor);
			if (deal == null || !__instance.pS_ || __instance.pS_.myID != deal.StudioId)
			{
				return;
			}
			string name = __instance.pS_.GetName();
			Text text = __instance.uiObjects[0].GetComponent<Text>();
			text.supportRichText = true;
			text.text = "<color=#1d8f2f><b>Your Deal:</b></color> " + name;
			if (!text.resizeTextForBestFit)
			{
				text.resizeTextMaxSize = text.fontSize;
				text.resizeTextMinSize = Mathf.Min(10, text.fontSize);
				text.resizeTextForBestFit = true;
			}
			if ((bool)__instance.tooltip_)
			{
				__instance.tooltip_.c = "<b>Your Deal</b>\n" + name + " paid you " + G.Money(deal.Advance) + " up front when they won the bid for this game. Publish with them to keep your word: going with anyone else, or publishing it yourself, means paying the advance back, and they won't forget it.\n\n" + __instance.tooltip_.c;
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Publishing deal label failed: " + ex);
		}
	}

	// The game is out: did it go to the publisher that paid for it?
	[HarmonyPostfix]
	[HarmonyPatch(typeof(gameScript), "SetOnMarket")]
	private static void Released(gameScript __instance)
	{
		try
		{
			List<PublishDeal> deals = IndustryCore.State.Deals;
			if (deals == null || deals.Count == 0 || !__instance)
			{
				return;
			}
			int index = deals.FindIndex((PublishDeal d) => d.GameId == __instance.myID);
			if (index < 0)
			{
				return;
			}
			PublishDeal deal = deals[index];
			deals.RemoveAt(index);
			publisherScript p = IndustryCore.Studio(deal.StudioId);
			string game = __instance.GetNameSimple();
			if (!Valid(p))
			{
				IndustryCore.Feed("The publishing deal for " + game + " lapsed: the publisher is gone. You keep the advance.");
				return;
			}
			if (__instance.publisherID == p.myID)
			{
				IndustryCore.Change(p, 3, "Published " + game + " as agreed");
				IndustryCore.Feed(p.GetName() + " publishes " + game + ", as agreed.");
				return;
			}
			if (deal.Advance > 0)
			{
				G.MS.Pay(deal.Advance, 14);
			}
			IndustryCore.Change(p, -12, "Took their advance for " + game + ", then went elsewhere");
			string text = "<b>" + p.GetName() + "</b> takes back its " + G.Money(deal.Advance) + " advance: " + game + " went to another publisher.";
			IndustryCore.News(text);
			IndustryCore.Feed(p.GetName() + " took back its " + G.Money(deal.Advance) + " advance: " + game + " went to another publisher.");
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Publishing deal at release failed: " + ex);
		}
	}
}
