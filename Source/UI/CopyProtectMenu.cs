using System;
using System.Linq;
using HolyAddon.Core;
using HolyAddon.Core.CopyProtection;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI;

// "Your copy protection" in the game's Management menu, right under "Buy copy protection".
internal static class CopyProtectMenu
{
	private const string EntryName = "Editor_YourCopyProtect";

	private const int Management = 9;

	private static GameObject button;

	private static float nextTry;

	public static void Tick()
	{
		GUI_MainButtons buttons = (G.Gui ? G.Gui.guiMainButtons_ : null);
		if (!buttons || buttons.uiObjects == null || buttons.uiObjects.Length <= Management || !buttons.uiObjects[Management])
		{
			return;
		}
		GameObject dropdown = buttons.uiObjects[Management];
		if (!button)
		{
			if (Time.unscaledTime < nextTry)
			{
				return;
			}
			nextTry = Time.unscaledTime + 2f;
			button = Inject(dropdown);
			if (!button)
			{
				return;
			}
		}
		if (dropdown.activeSelf)
		{
			bool show = CopyProtectDev.On && CopyProtectDev.MarketOpen;
			if (button.activeSelf != show)
			{
				button.SetActive(show);
			}
		}
	}

	private static Transform BuyOption(Transform dropdown)
	{
		return dropdown.Cast<Transform>().FirstOrDefault((Transform c) =>
		{
			Button b = c.GetComponent<Button>();
			return (bool)b && b.onClick.GetPersistentEventCount() > 0 && b.onClick.GetPersistentMethodName(0) == "DROPDOWN_BuyCopyProtect";
		});
	}

	// A copy of "Buy copy protection" (so it has the copy protection picture), placed right below it.
	private static GameObject Inject(GameObject dropdown)
	{
		Transform buy = BuyOption(dropdown.transform);
		if (!buy)
		{
			return null;
		}
		GameObject go = UnityEngine.Object.Instantiate(buy.gameObject, dropdown.transform, worldPositionStays: false);
		go.name = EntryName;
		foreach (setText s in go.GetComponentsInChildren<setText>(includeInactive: true))
		{
			UnityEngine.Object.DestroyImmediate(s);
		}
		foreach (lockDestroy l in go.GetComponentsInChildren<lockDestroy>(includeInactive: true))
		{
			UnityEngine.Object.DestroyImmediate(l);
		}
		Transform lockIcon = go.transform.Find("Lock");
		if ((bool)lockIcon)
		{
			lockIcon.gameObject.SetActive(value: false);
		}
		Transform label = go.transform.Find("Text");
		if ((bool)label)
		{
			label.GetComponent<Text>().text = "Your copy protection";
		}
		UIKit.SetTooltip(go, "Develop your own copy protection, keep it up to date and license it to other studios.");
		Button component = go.GetComponent<Button>();
		component.onClick = new Button.ButtonClickedEvent();
		component.onClick.AddListener(() =>
		{
			try
			{
				if ((bool)G.Gui)
				{
					G.Gui.guiMainButtons_.CloseAllDropdowns();
				}
				EditorWindow.CopyProtect.Open();
			}
			catch (Exception ex)
			{
				Plugin.Log.LogError("Your copy protection failed: " + ex);
			}
		});
		go.transform.SetSiblingIndex(buy.GetSiblingIndex() + 1);
		Plugin.Log.LogInfo("Your copy protection added to the Management menu");
		return go;
	}
}
