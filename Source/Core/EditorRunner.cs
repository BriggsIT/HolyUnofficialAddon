using System;
using HolyAddon.UI;
using UnityEngine;

namespace HolyAddon.Core;

internal class EditorRunner : MonoBehaviour
{
	private string lastError;

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
			StoreMenu.Tick();
			TimedExclusiveMenu.Tick();
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
