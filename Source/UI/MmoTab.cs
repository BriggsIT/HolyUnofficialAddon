using System;
using System.Linq;
using HarmonyLib;
using HolyAddon.Core;
using HolyAddon.Core.Updates;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI;

// An MMO tab in the game list on the right, next to the game's own tabs (All, Games, Arcade, Mobile, Your Consoles,
// Shelved, Game Pass): only your MMOs and their expansion packs, in development or out. It's a copy of the game's
// Game Pass tab with an MMO icon, in the same group of tabs, and shows only while you have an MMO card.
[HarmonyPatch]
internal static class MmoTab
{
	private static Toggle tab;

	private static GamesGroupContent owner;

	private static bool failed;

	private static bool IsMmo(gameScript g)
	{
		if ((bool)g && !g.schublade)
		{
			if (g.gameTyp != 1)
			{
				return g.typ_mmoaddon;
			}
			return true;
		}
		return false;
	}

	private static bool Showing
	{
		get
		{
			if ((bool)tab && tab.isOn)
			{
				return tab.gameObject.activeSelf;
			}
			return false;
		}
	}

	// Right after the game's own refresh of the list (once a second): make sure the tab is there, and show it while
	// you have an MMO card.
	[HarmonyPostfix]
	[HarmonyPatch(typeof(GamesGroupContent), "Update")]
	private static void AfterUpdate(GamesGroupContent __instance)
	{
		if (failed || __instance.timer != 0f || !G.Singleplayer)
		{
			return;
		}
		try
		{
			if (!tab || owner != __instance)
			{
				Create(__instance);
				if (!tab)
				{
					return;
				}
			}
			bool any = false;
			Transform list = __instance.transform;
			for (int i = 0; i < list.childCount; i++)
			{
				gameTab t = list.GetChild(i).GetComponent<gameTab>();
				if ((bool)t && IsMmo(t.gS_))
				{
					any = true;
					break;
				}
			}
			if (tab.gameObject.activeSelf != any)
			{
				tab.gameObject.SetActive(any);
			}
			if (!any && tab.isOn)
			{
				tab.isOn = false;
				__instance.uiTabs[0].GetComponent<Toggle>().isOn = true;
			}
		}
		catch (Exception ex)
		{
			failed = true;
			Plugin.Log.LogWarning("MMO tab: " + ex.Message);
		}
	}

	private static void Create(GamesGroupContent c)
	{
		owner = c;
		tab = null;
		if (c.uiTabs == null || c.uiTabs.Length < 7 || !c.uiTabs[6] || !c.uiTabs[5])
		{
			failed = true;
			Plugin.Log.LogWarning("MMO tab: the game list's tabs weren't found");
			return;
		}
		GameObject src = c.uiTabs[6];
		Toggle srcToggle = src.GetComponent<Toggle>();
		GameObject go = UnityEngine.Object.Instantiate(src, src.transform.parent, worldPositionStays: false);
		go.name = "AddonMmoTab";
		go.transform.SetSiblingIndex(src.transform.GetSiblingIndex() + 1);
		Toggle t = go.GetComponent<Toggle>();
		if (!t)
		{
			UnityEngine.Object.Destroy(go);
			failed = true;
			return;
		}
		// Its own listener only (the copy's would act as the Game Pass tab).
		t.onValueChanged = new Toggle.ToggleEvent();
		t.group = (srcToggle ? srcToggle.group : null);
		t.isOn = false;
		t.onValueChanged.AddListener(delegate(bool on)
		{
			if (on)
			{
				UIKit.Sound();
				if (!t.group)
				{
					foreach (GameObject other in c.uiTabs)
					{
						Toggle o = (other ? other.GetComponent<Toggle>() : null);
						if ((bool)o && o.isOn)
						{
							o.isOn = false;
						}
					}
				}
			}
			c.timer = 10f;
		});
		if (!t.group)
		{
			// Without a group, picking one of the game's tabs turns this one off.
			foreach (GameObject other in c.uiTabs)
			{
				Toggle o = (other ? other.GetComponent<Toggle>() : null);
				o?.onValueChanged.AddListener(delegate(bool on)
				{
					if (on && (bool)tab && tab.isOn)
					{
						tab.isOn = false;
					}
				});
			}
		}
		// Placed after the Game Pass tab: by the layout if there is one, otherwise one step on from it.
		RectTransform rt = go.GetComponent<RectTransform>();
		RectTransform a = c.uiTabs[5].GetComponent<RectTransform>();
		RectTransform b = src.GetComponent<RectTransform>();
		bool layout = (bool)src.transform.parent.GetComponent<LayoutGroup>();
		if (!layout && (bool)rt && (bool)a && (bool)b)
		{
			Vector2 step = b.anchoredPosition - a.anchoredPosition;
			if (step.magnitude < 1f)
			{
				step = new Vector2(0f, 0f - (b.rect.height + 4f));
			}
			rt.anchoredPosition = b.anchoredPosition + step;
		}
		// The MMO icon in place of the Game Pass one.
		Sprite icon = UpdateIcons.Mmo();
		int swapped = 0;
		foreach (Image img in go.GetComponentsInChildren<Image>(includeInactive: true))
		{
			if ((bool)img.sprite && img.sprite.name.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				img.sprite = icon;
				img.preserveAspect = true;
				swapped++;
			}
		}
		if (swapped == 0 && (bool)rt)
		{
			GameObject pic = new GameObject("MmoIcon", typeof(RectTransform), typeof(Image));
			pic.transform.SetParent(go.transform, worldPositionStays: false);
			RectTransform p = pic.GetComponent<RectTransform>();
			p.anchorMin = (p.anchorMax = new Vector2(0.5f, 0.5f));
			float size = Mathf.Min(rt.rect.width, rt.rect.height) * 0.7f;
			p.sizeDelta = new Vector2(size, size);
			Image image = pic.GetComponent<Image>();
			image.sprite = icon;
			image.preserveAspect = true;
			image.raycastTarget = false;
		}
		tooltip tip = go.GetComponent<tooltip>() ?? go.GetComponentInChildren<tooltip>(includeInactive: true);
		if ((bool)tip)
		{
			tip.textID = -1;
			tip.textArray = "";
			tip.c = "MMOs and their expansion packs";
		}
		go.SetActive(value: false);
		tab = t;
		Plugin.Log.LogInfo("MMO tab added to the game list (layout: " + (layout ? src.transform.parent.GetComponent<LayoutGroup>().GetType().Name : ("none, at " + (rt ? rt.anchoredPosition.ToString() : "?"))) + ", icons swapped: " + swapped + ", in a toggle group: " + ((bool)t.group).ToString() + ")");
	}

	// While the MMO tab is picked: only MMO cards (the game's own "only my games" filter still applies).
	[HarmonyPrefix]
	[HarmonyPatch(typeof(GamesGroupContent), "SetTab")]
	private static bool SetTabPrefix(GameObject go, gameTab script_)
	{
		if (!Showing)
		{
			return true;
		}
		gameScript g = (script_ ? script_.gS_ : null);
		bool show = IsMmo(g) && (!G.MS || G.MS.gameTabFilter == null || G.MS.gameTabFilter.Length <= 11 || !G.MS.gameTabFilter[11] || g.developerID == G.MS.myID);
		if (go.activeSelf != show)
		{
			go.SetActive(show);
		}
		return false;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(GamesGroupContent), "SetTabKonsole")]
	private static bool SetTabKonsolePrefix(GameObject go)
	{
		return Hide(go);
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(GamesGroupContent), "SetTabGamePass")]
	private static bool SetTabGamePassPrefix(GameObject go)
	{
		return Hide(go);
	}

	private static bool Hide(GameObject go)
	{
		if (!Showing)
		{
			return true;
		}
		if (go.activeSelf)
		{
			go.SetActive(value: false);
		}
		return false;
	}
}
