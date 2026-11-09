using UnityEngine;

namespace HolyAddon.Core;

// Cached lookups of the game's main scripts. Unity's bool operator makes these re-resolve after a scene reload.
internal static class G
{
	private static GameObject main;

	private static mainScript ms;

	private static GUI_Main gui;

	private static textScript ts;

	private static sfxScript sfx;

	public static GameObject MainObj
	{
		get
		{
			if (!main)
			{
				main = GameObject.Find("Main");
			}
			return main;
		}
	}

	public static mainScript MS
	{
		get
		{
			if (!ms && (bool)MainObj)
			{
				ms = MainObj.GetComponent<mainScript>();
			}
			return ms;
		}
	}

	public static GUI_Main Gui
	{
		get
		{
			if (!gui)
			{
				GameObject gameObject = GameObject.Find("CanvasInGameMenu");
				if ((bool)gameObject)
				{
					gui = gameObject.GetComponent<GUI_Main>();
				}
			}
			return gui;
		}
	}

	public static textScript TS
	{
		get
		{
			if (!ts && (bool)MainObj)
			{
				ts = MainObj.GetComponent<textScript>();
			}
			return ts;
		}
	}

	public static sfxScript Sfx
	{
		get
		{
			if (!sfx)
			{
				GameObject gameObject = GameObject.Find("SFX");
				if ((bool)gameObject)
				{
					sfx = gameObject.GetComponent<sfxScript>();
				}
			}
			return sfx;
		}
	}

	public static bool InGame
	{
		get
		{
			if ((bool)MS && MS.officeLoaded && (bool)Gui)
			{
				return !Gui.IsStartMenuActive();
			}
			return false;
		}
	}

	public static bool Singleplayer
	{
		get
		{
			if ((bool)MS)
			{
				return !MS.multiplayer;
			}
			return false;
		}
	}

	// The game's own translated text (Extern/Text/<lang>/Text_<lang>.txt), so labels follow the game language.
	public static string Text(int id, string fallback)
	{
		string text = (TS ? TS.GetText(id) : null);
		if (string.IsNullOrEmpty(text) || text.StartsWith("<Missing"))
		{
			return fallback;
		}
		return text;
	}

	public static string Money(long amount)
	{
		if (!MS)
		{
			return "$" + amount;
		}
		return MS.GetMoney(amount, showDollar: true);
	}
}
