using System;
using System.Linq;
using HolyAddon.Core;
using HolyAddon.Core.Store;
using HolyAddon.Core.Updates;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI;

// "Your digital store" in the game's Management menu, below "Your copy protection" (or "Buy copy protection").
internal static class StoreMenu
{
	private const string EntryName = "Editor_YourStore";

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
		if (!dropdown.activeSelf)
		{
			return;
		}
		bool show = StoreOps.On;
		if (button.activeSelf != show)
		{
			button.SetActive(show);
		}
		if (show)
		{
			Transform after = dropdown.transform.Find("Editor_YourCopyProtect");
			if ((bool)after && after.gameObject.activeSelf && button.transform.GetSiblingIndex() != after.GetSiblingIndex() + 1)
			{
				button.transform.SetSiblingIndex(after.GetSiblingIndex() + 1);
			}
			Transform lockIcon = button.transform.Find("Lock");
			if ((bool)lockIcon)
			{
				lockIcon.gameObject.SetActive(!StoreOps.Open);
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
		Transform label = go.transform.Find("Text");
		if ((bool)label)
		{
			label.GetComponent<Text>().text = "Your digital store";
		}
		// Swap the copy protection picture for the store's.
		Sprite sprite = UpdateIcons.Store();
		Image picture = go.GetComponentsInChildren<Image>(includeInactive: true).FirstOrDefault((Image i) => i.transform != go.transform && i.name != "Lock" && i.transform.parent.name != "Lock");
		if ((bool)picture && (bool)sprite)
		{
			picture.sprite = sprite;
		}
		UIKit.SetTooltip(go, "Run your own digital PC store: sell your games, take a cut of other studios' sales, and compete with the rival stores. From " + StoreOps.OpenYear + ".");
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
				EditorWindow.Store.Open();
			}
			catch (Exception ex)
			{
				Plugin.Log.LogError("Your digital store failed: " + ex);
			}
		});
		go.transform.SetSiblingIndex(buy.GetSiblingIndex() + 1);
		Plugin.Log.LogInfo("Your digital store added to the Management menu");
		return go;
	}
}
