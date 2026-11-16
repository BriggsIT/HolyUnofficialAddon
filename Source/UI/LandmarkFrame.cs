using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HolyAddon.Core.Landmarks;
using HolyAddon.Core.Updates;
using HolyAddon.UI.Pages;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI;

// A landmark game's picture wears a gold frame, with the landmark badge in its bottom-left corner (the user's design,
// 16 Nov 2026). It's for landmarks and milestone games (Landmarks.IsLandmark), yours or a studio's. It shows wherever a
// game's picture does:
// - the game's lists (Sequel, Spin-off, Remaster, Port, add-ons, bundles, budget and GOTY editions, publishing offers,
//   your IPs...);
// - the Game Complete window, the development report and the review;
// - the addon's pages (charts, awards, MMO pages, the store).
internal static class LandmarkFrame
{
	private const string Name = "Addon_LandmarkFrame";

	// Gold edges round the picture and the badge in its corner; off takes them away (the game reuses some windows).
	public static void Apply(Transform picture, bool on)
	{
		if (!picture)
		{
			return;
		}
		Transform old = picture.Find(Name);
		if (!on)
		{
			if ((bool)old)
			{
				UnityEngine.Object.Destroy(old.gameObject);
			}
			return;
		}
		if ((bool)old)
		{
			old.SetAsLastSibling();
			return;
		}
		float h = ((picture is RectTransform prt) ? prt.rect.height : 0f);
		if (h <= 1f)
		{
			h = 60f;
		}
		float edge = Mathf.Clamp(h * 0.035f, 2f, 4f);
		GameObject root = new GameObject(Name, typeof(RectTransform));
		root.transform.SetParent(picture, worldPositionStays: false);
		RectTransform rt = (RectTransform)root.transform;
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		rt.offsetMin = (rt.offsetMax = Vector2.zero);
		Color gold = LandmarkUi.Gold;
		Bar(root.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, edge), gold);
		Bar(root.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, edge), gold);
		Bar(root.transform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(edge, 0f), gold);
		Bar(root.transform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(edge, 0f), gold);
		Sprite icon = UpdateIcons.LandmarkBadge();
		if ((bool)icon)
		{
			float size = Mathf.Clamp(h * 0.4f, 16f, 44f);
			Image badge = NewImage("Badge", root.transform, Color.white);
			badge.sprite = icon;
			badge.preserveAspect = true;
			RectTransform b = badge.rectTransform;
			b.anchorMin = (b.anchorMax = Vector2.zero);
			b.pivot = Vector2.zero;
			b.sizeDelta = new Vector2(size, size);
			b.anchoredPosition = new Vector2(edge + 1f, edge + 1f);
		}
		root.transform.SetAsLastSibling();
	}

	private static void Bar(Transform parent, Vector2 min, Vector2 max, Vector2 pivot, Vector2 size, Color c)
	{
		RectTransform r = NewImage("Edge", parent, c).rectTransform;
		r.anchorMin = min;
		r.anchorMax = max;
		r.pivot = pivot;
		r.sizeDelta = size;
		r.anchoredPosition = Vector2.zero;
	}

	private static Image NewImage(string name, Transform parent, Color c)
	{
		Image img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
		img.transform.SetParent(parent, worldPositionStays: false);
		img.color = c;
		img.raycastTarget = false;
		return img;
	}

	// ---- The game's own lists and windows ----

	// Every method of these that sets a game's picture (gameScript.GetScreenshot), found by reading its code.
	[HarmonyPatch]
	private static class GameLists
	{
		private static readonly string[] Types = new string[26]
		{
			"Item_AddonBundleSelect", "Item_BudgetSelect", "Item_BundleSelect", "Item_DevGame_Addon", "Item_DevGame_f2PUpdate", "Item_DevGame_GameUpdate", "Item_DevGame_Nachfolger", "Item_DevGame_Port", "Item_DevGame_MMOAddon", "Item_DevGame_Spinoff",
			"Item_DevGame_ShowConcept", "Item_DevGame_Remaster", "Item_FreewareSelect", "Item_KonsoleGame", "Item_GotySelect", "Item_MyGames_ShowIP", "Item_ProduceSelect", "Item_Restbestand", "Item_PackungSelect", "Item_RemoveGame",
			"Item_WerkstattSelect", "Item_QA_Bugfixing", "Item_PubOfferSelect", "Menu_DevGame_Complete", "Menu_Dev_GameEntwicklungsbericht", "Menu_Review"
		};

		private static IEnumerable<MethodBase> TargetMethods()
		{
			MethodInfo shot = AccessTools.Method(typeof(gameScript), "GetScreenshot");
			int found = 0;
			foreach (string name in Types)
			{
				Type t = AccessTools.TypeByName(name);
				if (t == null)
				{
					continue;
				}
				foreach (MethodInfo m in AccessTools.GetDeclaredMethods(t))
				{
					bool calls = false;
					try
					{
						calls = !m.IsAbstract && Core.Performance.StutterFinder.SafeReturn(m) && PatchProcessor.ReadMethodBody(m).Any((KeyValuePair<System.Reflection.Emit.OpCode, object> op) => op.Value as MethodInfo == shot);
					}
					catch
					{
					}
					if (calls)
					{
						found++;
						yield return m;
					}
				}
			}
			Plugin.Log.LogInfo("Landmark frames: " + found + " of the game's game-picture methods");
		}

		private static void Postfix(object __instance)
		{
			try
			{
				FrameIn(__instance);
			}
			catch (Exception ex)
			{
				Plugin.Log.LogWarning("Landmark frame: " + ex.Message);
			}
		}
	}

	private static readonly Dictionary<Type, FieldInfo[]> fields = new Dictionary<Type, FieldInfo[]>();

	// The game's item or window: its game (game_ or gS_) and its pictures (the uiObjects showing that game's screenshot).
	private static void FrameIn(object item)
	{
		Type t = item.GetType();
		if (!fields.TryGetValue(t, out var f))
		{
			f = new FieldInfo[2]
			{
				AccessTools.Field(t, "game_") ?? AccessTools.Field(t, "gS_"),
				AccessTools.Field(t, "uiObjects")
			};
			fields[t] = f;
		}
		gameScript g = f[0]?.GetValue(item) as gameScript;
		GameObject[] ui = f[1]?.GetValue(item) as GameObject[];
		if (!g || ui == null)
		{
			return;
		}
		Sprite s = g.GetScreenshot();
		bool on = Landmarks.IsLandmark(g);
		foreach (GameObject go in ui)
		{
			Image img = (go ? go.GetComponent<Image>() : null);
			if ((bool)img && (bool)s && img.sprite == s)
			{
				Apply(img.transform, on);
			}
		}
	}
}
