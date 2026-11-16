using System;
using HolyAddon.UI;
using UnityEngine;

namespace HolyAddon.Core;

internal class EditorRunner : MonoBehaviour
{
	// For coroutines (a window's backdrop is captured at the end of a frame).
	public static EditorRunner Instance { get; private set; }

	private string lastError;

	private void Awake()
	{
		Instance = this;
	}

	private void Update()
	{
		if (!G.InGame)
		{
			return;
		}
		try
		{
			IpLinks.Tick();
			BottomButton.Tick();
			SpeedButtons.Tick();
			EditorWindow.Main.Tick();
			EditorWindow.Contracts.Tick();
			EditorWindow.CopyProtect.Tick();
			EditorWindow.Updates.Tick();
			EditorWindow.Mmo.Tick();
			EditorWindow.Store.Tick();
			EditorWindow.Awards.Tick();
			EditorWindow.FanFest.Tick();
			EditorWindow.Charts.Tick();
			ChartsMenu.Tick();
			EditorWindow.StudioRequest.Tick();
			UI.Pages.StudioRequests.Tick();
			Mmo.MmoFanFest.Tick();
			EditorWindow.Sales.Tick();
			EditorWindow.SalePopup.Tick();
			Store.SalesEvents.Tick();
			EditorWindow.Convention.Tick();
			EditorWindow.Rooms.Tick();
			EditorWindow.Industry.Tick();
			EditorWindow.IndustryEvent.Tick();
			Industry.IndustryCore.Tick();
			EditorWindow.Landmark.Tick();
			EditorWindow.LandmarkMade.Tick();
			Landmarks.Landmarks.Tick();
			Rooms.RoomScan.Tick();
			Events.StudioCon.Tick();
			StoreMenu.Tick();
			TimedExclusiveMenu.Tick();
			ExtraTopicsMenu.Tick();
			EditorWindow.ExtraTopics.Tick();
			DesignScreenPanels.Tick();
			LanguagesRow.Tick();
			DesignAssist.Tick();
			EditorWindow.DesignAssist.Tick();
			CopyProtectMenu.Tick();
			UpdateMenus.Tick();
			ContractTracker.Tick();
			if (Plugin.Hotkey.Value.IsDown())
			{
				BottomButton.Toggle();
			}
		}
		catch (Exception ex)
		{
			// Update runs every frame: only log an error the first time it shows up.
			string text = ex.ToString();
			if (text != lastError)
			{
				lastError = text;
				Plugin.Log.LogError("Editor update failed: " + text);
			}
		}
	}
}
