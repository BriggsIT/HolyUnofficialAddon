using System;
using System.Linq;
using HolyAddon.Core;
using HolyAddon.Core.Updates;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI;

// "Current Charts" in the game's Statistics menu, right under its own "Charts".
internal static class ChartsMenu
{
	private const string EntryName = "Editor_CurrentCharts";

	private const int Statistics = 11;

	private static GameObject button;

	private static float nextTry;

	public static void Tick()
	{
		GUI_MainButtons buttons = (G.Gui ? G.Gui.guiMainButtons_ : null);
		if (!buttons || buttons.uiObjects == null || buttons.uiObjects.Length <= Statistics || !buttons.uiObjects[Statistics])
		{
			return;
		}
		GameObject dropdown = buttons.uiObjects[Statistics];
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
		Transform charts = ChartsOption(dropdown.transform);
		if ((bool)charts && button.transform.GetSiblingIndex() != charts.GetSiblingIndex() + 1)
		{
			button.transform.SetSiblingIndex(charts.GetSiblingIndex() + 1);
		}
	}

	private static Transform ChartsOption(Transform dropdown)
	{
		return dropdown.Cast<Transform>().FirstOrDefault((Transform c) =>
		{
			Button b = c.GetComponent<Button>();
			return (bool)b && b.onClick.GetPersistentEventCount() > 0 && b.onClick.GetPersistentMethodName(0) == "DROPDOWN_Statstics_Charts";
		});
	}

	private static GameObject Inject(GameObject dropdown)
	{
		Transform charts = ChartsOption(dropdown.transform);
		if (!charts)
		{
			return null;
		}
		GameObject go = UnityEngine.Object.Instantiate(charts.gameObject, dropdown.transform, worldPositionStays: false);
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
			label.GetComponent<Text>().text = "Current Charts";
		}
		Image picture = go.GetComponentsInChildren<Image>(includeInactive: true).FirstOrDefault((Image i) => i.transform != go.transform);
		Sprite sprite = UpdateIcons.Game("iconWochencharts");
		if ((bool)picture && (bool)sprite)
		{
			picture.sprite = sprite;
		}
		UIKit.SetTooltip(go, "What sells right now: top sellers this week, by genre, consoles, studios, new releases and the most anticipated games. (Holy Unofficial Addon)");
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
				EditorWindow.Charts.Open();
			}
			catch (Exception ex)
			{
				Plugin.Log.LogError("Current charts failed: " + ex);
			}
		});
		go.transform.SetSiblingIndex(charts.GetSiblingIndex() + 1);
		Plugin.Log.LogInfo("Current Charts added to the Statistics menu");
		return go;
	}
}
