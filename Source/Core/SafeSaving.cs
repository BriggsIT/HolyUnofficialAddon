using System;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace HolyAddon.Core;

// A safety net for saving. The game deletes a slot's save before it writes the new one (savegameScript.Save,
// and Menu_OverwriteSavegame.BUTTON_Yes before that), so if anything goes wrong while writing (a bug in the game
// or in any mod) that slot's save is simply gone, and the half-written file stays locked until the game closes.
// Here the slot's save is copied aside first; if writing fails, the copy goes back, the half-written file is
// closed and removed, and you're told the save didn't happen. A save that works removes the copy again.
[HarmonyPatch]
internal static class SafeSaving
{
	private static readonly AccessTools.FieldRef<savegameScript, ES3Writer> writer = AccessTools.FieldRefAccess<savegameScript, ES3Writer>("writer");

	// The slot backed up by the overwrite question, just before the game deletes it.
	private static string overwriteBackup;

	private static string PathFor(int slot)
	{
		if (!G.MS)
		{
			return null;
		}
		return Path.Combine(Application.persistentDataPath, G.MS.GetSavegameTitle() + slot + ".sav");
	}

	private static string Backup(string path)
	{
		return path + ".bak";
	}

	// "Overwrite this save?" > Yes deletes the save straight away: copy it aside first.
	[HarmonyPrefix]
	[HarmonyPatch(typeof(Menu_OverwriteSavegame), "BUTTON_Yes")]
	private static void BeforeOverwrite(int ___slot)
	{
		overwriteBackup = null;
		if (!G.Singleplayer)
		{
			return;
		}
		try
		{
			string path = PathFor(___slot);
			if (path != null && File.Exists(path))
			{
				File.Copy(path, Backup(path), overwrite: true);
				overwriteBackup = path;
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Save backup before overwriting failed: " + ex.Message);
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(savegameScript), "Save")]
	private static void BeforeSave(int i, out string __state)
	{
		__state = null;
		if (!G.Singleplayer)
		{
			return;
		}
		try
		{
			string path = PathFor(i);
			if (path == null)
			{
				return;
			}
			if (File.Exists(path))
			{
				File.Copy(path, Backup(path), overwrite: true);
				__state = path;
			}
			else if (overwriteBackup == path && File.Exists(Backup(path)))
			{
				__state = path;
			}
			else if (File.Exists(Backup(path)))
			{
				// Left over from before: nothing of this slot to protect now.
				File.Delete(Backup(path));
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Save backup failed: " + ex.Message);
		}
		finally
		{
			overwriteBackup = null;
		}
	}

	[HarmonyFinalizer]
	[HarmonyPatch(typeof(savegameScript), "Save")]
	private static Exception AfterSave(savegameScript __instance, int i, Exception __exception, string __state)
	{
		if (__state == null)
		{
			return __exception;
		}
		string backup = Backup(__state);
		try
		{
			if (__exception == null)
			{
				if (File.Exists(backup))
				{
					File.Delete(backup);
				}
				return null;
			}
			// Writing failed: close the half-written file so it isn't locked, then put the old save back.
			try
			{
				writer(__instance)?.Dispose();
			}
			catch (Exception)
			{
			}
			writer(__instance) = null;
			foreach (string partial in new string[2] { __state, __state + ".tmp" })
			{
				try
				{
					if (File.Exists(partial))
					{
						File.Delete(partial);
					}
				}
				catch (Exception)
				{
				}
			}
			if (File.Exists(backup))
			{
				File.Copy(backup, __state, overwrite: true);
				File.Delete(backup);
			}
			Plugin.Log.LogError("Saving to slot " + i + " failed, so the save that was in that slot was put back. The error: " + __exception);
			CopyProtection.CopyProtectDev.Report("<b>The game couldn't be saved.</b>\n\nSomething went wrong while writing slot " + i + ". The save that was in that slot before has been put back, so nothing is lost.\n\nThe details are in BepInEx\\LogOutput.log.");
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Putting the previous save back failed (the copy is still in " + backup + "): " + ex);
		}
		return __exception;
	}
}
