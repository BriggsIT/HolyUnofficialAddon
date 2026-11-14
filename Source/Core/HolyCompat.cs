using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace HolyAddon.Core;

// Links to The Holy Expansion's own systems, made at load time (its DLL stays as it is), with the author's blessing:
// its seasonal sales start year, its piracy (our updates break pirated copies like the game's own updates do), and
// its sales bonus total (ours count toward its cap). Everything here does nothing when Holy isn't installed.
internal static class HolyCompat
{
	// Holy's Seasonal Sales start in 1998 ("Digital storefront sales start around 1998"). Digital stores arrive
	// in 2003 in this addon (as the game's own digital versions do), so its sales start then too.
	public const int HolySeasonalYear = 1998;

	public const int StoreYear = 2003;

	public static int SeasonalYear()
	{
		if (!Plugin.HolySeasonalFrom2003.Value)
		{
			return HolySeasonalYear;
		}
		return StoreYear;
	}

	// ---- Cached lookups ----
	// Finding a type by name (AccessTools.TypeByName) searches every loaded assembly, and reading a field through
	// Traverse looks the field up again each time: both are far too slow for anything that runs every frame or for
	// every room. These look each one up once.

	private static readonly Dictionary<string, Type> types = new Dictionary<string, Type>();

	private static readonly Dictionary<KeyValuePair<Type, string>, FieldInfo> fields = new Dictionary<KeyValuePair<Type, string>, FieldInfo>();

	public static Type HolyType(string fullName)
	{
		if (!types.TryGetValue(fullName, out var t))
		{
			t = (types[fullName] = AccessTools.TypeByName(fullName));
		}
		return t;
	}

	// A field of Holy's save state (HolyState.Current.<name>), or null without Holy.
	public static object HolyState(string name)
	{
		Type state = HolyType("HolyExpansion.Core.HolyState");
		if (state == null)
		{
			return null;
		}
		object current = Field(state, "Current")?.GetValue(null);
		if (current == null)
		{
			return null;
		}
		return Field(state, name)?.GetValue(current);
	}

	// A field's value on any object (Holy's state classes), with the field looked up once per type.
	public static T Get<T>(object o, string name)
	{
		if (o == null)
		{
			return default(T);
		}
		object v = Field(o.GetType(), name)?.GetValue(o);
		if (v is T t)
		{
			return t;
		}
		return default(T);
	}

	public static void Set(object o, string name, object value)
	{
		if (o != null)
		{
			Field(o.GetType(), name)?.SetValue(o, value);
		}
	}

	private static FieldInfo Field(Type t, string name)
	{
		KeyValuePair<Type, string> key = new KeyValuePair<Type, string>(t, name);
		if (!fields.TryGetValue(key, out var f))
		{
			f = (fields[key] = AccessTools.Field(t, name));
		}
		return f;
	}

	// ---- Holy's piracy (HolyExpansion.Systems.GamePiracy; its numbers live in HolyState.Current.GamePiracy) ----

	private static bool piracyLooked;

	private static FieldInfo holyCurrent;

	private static FieldInfo piracyField;

	private static PropertyInfo piracyOn;

	private static void LookUpPiracy()
	{
		if (piracyLooked)
		{
			return;
		}
		piracyLooked = true;
		Type state = AccessTools.TypeByName("HolyExpansion.Core.HolyState");
		holyCurrent = state?.GetField("Current", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		piracyField = state?.GetField("GamePiracy", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		piracyOn = AccessTools.TypeByName("HolyExpansion.Systems.GamePiracy")?.GetProperty("On", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
	}

	public static bool PiracyOn
	{
		get
		{
			LookUpPiracy();
			if (piracyOn == null)
			{
				return false;
			}
			try
			{
				return (bool)piracyOn.GetValue(null);
			}
			catch
			{
				return false;
			}
		}
	}

	private static T PiracyField<T>(string name) where T : class
	{
		LookUpPiracy();
		object state = holyCurrent?.GetValue(null);
		object piracy = ((state != null) ? piracyField?.GetValue(state) : null);
		return piracy?.GetType().GetField(name)?.GetValue(piracy) as T;
	}

	private static copyProtectScript Protection(gameScript g)
	{
		if (g.gameCopyProtect == -1)
		{
			return null;
		}
		if (!g.gameCopyProtectScript_)
		{
			GameObject go = GameObject.Find("COPYPROTECT_" + g.gameCopyProtect);
			if ((bool)go)
			{
				g.gameCopyProtectScript_ = go.GetComponent<copyProtectScript>();
			}
		}
		return g.gameCopyProtectScript_;
	}

	// As Holy does it for the game's own update: a share of the pirated copies stop working, and the copy
	// protection's wear clock goes back (renew 0.3 = held as if 30% worn, 0 = like new). Returns the pirates removed.
	public static long BreakPirates(gameScript g, float cut, float renewWorn)
	{
		if (!(bool)g || g.gameTyp != 0 || !PiracyOn)
		{
			return 0L;
		}
		try
		{
			Dictionary<int, float> renewed = PiracyField<Dictionary<int, float>>("Renewed");
			Dictionary<int, long> copies = PiracyField<Dictionary<int, long>>("Copies");
			copyProtectScript cp = Protection(g);
			if ((bool)cp && renewed != null)
			{
				float hold = Mathf.Lerp(12f, 80f, Mathf.Clamp01(cp.effekt / 100f));
				float renew = (float)g.weeksOnMarket - hold * renewWorn;
				renewed[g.myID] = (renewed.TryGetValue(g.myID, out var old) ? Mathf.Max(old, renew) : renew);
			}
			if (copies == null || !copies.TryGetValue(g.myID, out var now) || now <= 0)
			{
				return 0L;
			}
			long keep = (long)((float)now * (1f - cut));
			copies[g.myID] = keep;
			return now - keep;
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Holy piracy: " + ex.Message);
			return 0L;
		}
	}

	// Holy's way of moving user reviews (Util.ShiftUserReviews): adds that share of all reviews as positive or negative.
	public static void ShiftUserReviews(gameScript g, float fraction)
	{
		if ((bool)g && fraction != 0f)
		{
			int n = (int)Mathf.Clamp((float)((long)g.userPositiv + (long)g.userNegativ) * Mathf.Abs(fraction), 20f, 50000000f);
			if (fraction > 0f)
			{
				g.userPositiv += n;
			}
			else
			{
				g.userNegativ += n;
			}
		}
	}

	// ---- Sales bonuses: ours count toward Holy's total (and its cap of +250%) when Holy is installed ----

	public static bool SalesBonusShared { get; private set; }

	private static void ShareSalesBonus()
	{
		try
		{
			MethodInfo register = AccessTools.TypeByName("HolyExpansion.Systems.SalesBonus")?.GetMethod("Register", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			if (register == null)
			{
				return;
			}
			register.Invoke(null, new object[1] { new Func<gameScript, float>((gameScript g) => Updates.GameUpdates.OurSalesFactor(g) - 1f) });
			SalesBonusShared = true;
			Plugin.Log.LogInfo("Sales bonuses now count toward The Holy Expansion's total");
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Could not share sales bonuses with Holy: " + ex.Message);
		}
	}

	// ---- Holy's Seasonal Sales give way to the addon's sales events (Core/Store/SalesEvents) ----

	// Our price cut goes through Holy's own price modifiers, so Holy's SellGame patch applies it once.
	private static void SharePriceCut()
	{
		try
		{
			MethodInfo register = AccessTools.TypeByName("HolyExpansion.Systems.SalesBonus")?.GetMethod("RegisterPrice", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			if (register == null)
			{
				return;
			}
			register.Invoke(null, new object[1] { new Func<gameScript, float>(Store.SalesEvents.PriceFactor) });
			Store.SalesEvents.HolyPrices = true;
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Could not share the sale price cut with Holy: " + ex.Message);
		}
	}

	// While sales events are on, Holy's Seasonal Sales count as switched off (no sale, bonus, price cut or event).
	private static bool SeasonalOff(ref bool __result)
	{
		if (Plugin.MergedSales.Value)
		{
			__result = false;
			return false;
		}
		return true;
	}

	// Holy's "Seasonal Sales" entry in the Price & Packaging menu opens the sales events instead.
	private static void TakeOverMenu(Type seasonal)
	{
		Type injector = AccessTools.TypeByName("HolyExpansion.UI.MenuInjector");
		if (!(injector?.GetField("entries", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) is System.Collections.IList entries))
		{
			return;
		}
		foreach (object entry in entries)
		{
			Traverse t = Traverse.Create(entry);
			if (t.Field("Id").GetValue<string>() != "seasonal")
			{
				continue;
			}
			Func<string> label = t.Field("Label").GetValue<Func<string>>();
			Action click = t.Field("Click").GetValue<Action>();
			Func<string> locked = t.Field("LockReason").GetValue<Func<string>>();
			Func<bool> visible = t.Field("Visible").GetValue<Func<bool>>();
			t.Field("Label").SetValue(new Func<string>(() => Plugin.MergedSales.Value ? "Sales Events" : label?.Invoke()));
			t.Field("Click").SetValue(new Action(() =>
			{
				if (Plugin.MergedSales.Value)
				{
					UI.EditorWindow.Sales.Open();
				}
				else
				{
					click?.Invoke();
				}
			}));
			t.Field("LockReason").SetValue(new Func<string>(() => Plugin.MergedSales.Value ? Store.SalesEvents.LockReason() : locked?.Invoke()));
			t.Field("Visible").SetValue(new Func<bool>(() => Plugin.MergedSales.Value || (visible?.Invoke() ?? true)));
			Plugin.Log.LogInfo("Holy's Seasonal Sales menu entry opens the sales events");
			return;
		}
	}

	// ---- Holy's Fan Convention gives way to the yearly convention (Core/Events/StudioCon) ----

	private static bool ConventionOff(ref bool __result)
	{
		if (Plugin.Convention.Value)
		{
			__result = false;
			return false;
		}
		return true;
	}

	// Holy's "Fan Convention" button in Support rooms opens the yearly convention's planner for that room.
	private static void TakeOverRoomButton()
	{
		Type menu = AccessTools.TypeByName("HolyExpansion.UI.RoomMenu");
		if (!(menu?.GetField("entries", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) is System.Collections.IList entries))
		{
			return;
		}
		foreach (object entry in entries)
		{
			Traverse t = Traverse.Create(entry);
			if (t.Field("Id").GetValue<string>() != "convention")
			{
				continue;
			}
			Func<string> label = t.Field("Label").GetValue<Func<string>>();
			Action<roomScript> click = t.Field("Click").GetValue<Action<roomScript>>();
			Func<string> locked = t.Field("LockReason").GetValue<Func<string>>();
			Func<bool> visible = t.Field("Visible").GetValue<Func<bool>>();
			t.Field("Label").SetValue(new Func<string>(() => Plugin.Convention.Value ? Events.StudioCon.Name() : label?.Invoke()));
			t.Field("Click").SetValue(new Action<roomScript>((roomScript room) =>
			{
				if (Plugin.Convention.Value)
				{
					UI.Pages.ConventionPage.FromRoom = room;
					UI.EditorWindow.Convention.Open();
				}
				else
				{
					click?.Invoke(room);
				}
			}));
			t.Field("LockReason").SetValue(new Func<string>(() => Plugin.Convention.Value ? Events.StudioCon.LockReason() : locked?.Invoke()));
			t.Field("Visible").SetValue(new Func<bool>(() => Plugin.Convention.Value || (visible?.Invoke() ?? true)));
			Plugin.Log.LogInfo("Holy's Fan Convention room button opens the yearly convention");
			return;
		}
	}

	// One of Holy's menu entries (MenuInjector: the game's top menus; RoomMenu: room menus), by its id.
	private static Traverse HolyEntry(string menuType, string id)
	{
		Type menu = AccessTools.TypeByName(menuType);
		if (!(menu?.GetField("entries", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) is System.Collections.IList entries))
		{
			return null;
		}
		foreach (object entry in entries)
		{
			Traverse t = Traverse.Create(entry);
			if (t.Field("Id").GetValue<string>() == id)
			{
				return t;
			}
		}
		return null;
	}

	// Holy's "Your Anti-Cheat" (Management menu) has no lock, so it opens before anti-cheat software exists. It takes
	// the lock of Holy's own "Develop Anti-Cheat" room button ("Anti-cheat software appears around 1990."), so it's
	// greyed out with that tooltip like the game's locked options.
	private static void LockAntiCheatMenu()
	{
		try
		{
			Traverse menu = HolyEntry("HolyExpansion.UI.MenuInjector", "anticheat");
			Traverse room = HolyEntry("HolyExpansion.UI.RoomMenu", "anticheat");
			Func<string> reason = room?.Field("LockReason").GetValue<Func<string>>();
			if (menu != null && reason != null && menu.Field("LockReason").GetValue() == null)
			{
				menu.Field("LockReason").SetValue(reason);
				Plugin.Log.LogInfo("Holy's Your Anti-Cheat menu entry is locked until anti-cheat software exists");
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Could not lock Holy's Your Anti-Cheat menu entry: " + ex.Message);
		}
	}

	// Holy's console-scene pop-ups (a cracked console, an emulator) answer "You can't afford that." whenever a
	// counter-move isn't possible, even with billions in the bank (the hackers already work for you, nobody has
	// cracked it, your lawyers are already on it). This gives Holy's own reason instead.
	private static MethodInfo securityBlock;

	private static bool HitBackPrefix(platformScript p, int action, ref string __result)
	{
		try
		{
			if (securityBlock == null || !p)
			{
				return true;
			}
			if (!(securityBlock.Invoke(null, new object[2] { p, action }) is string why))
			{
				return true;
			}
			__result = why;
			return false;
		}
		catch
		{
			return true;
		}
	}

	private static void FixHitBack(Harmony harmony)
	{
		try
		{
			Type scene = AccessTools.TypeByName("HolyExpansion.Systems.ConsoleScene");
			MethodInfo hit = ((scene != null) ? AccessTools.Method(scene, "HitBack") : null);
			securityBlock = ((scene != null) ? AccessTools.Method(scene, "SecurityBlock") : null);
			if (hit != null && securityBlock != null)
			{
				harmony.Patch(hit, new HarmonyMethod(typeof(HolyCompat), "HitBackPrefix"));
				Plugin.Log.LogInfo("Holy's console-scene pop-ups give the real reason when a move isn't possible");
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Could not fix Holy's console-scene messages: " + ex.Message);
		}
	}

	public static void Apply(Harmony harmony)
	{
		ShareSalesBonus();
		SharePriceCut();
		LockAntiCheatMenu();
		FixHitBack(harmony);
		HolyLiveService.Apply(harmony);
		RoomSupport.Apply(harmony);
		try
		{
			Type convention = AccessTools.TypeByName("HolyExpansion.Systems.Convention");
			MethodInfo on = ((convention != null) ? AccessTools.PropertyGetter(convention, "On") : null);
			if (on != null)
			{
				harmony.Patch(on, new HarmonyMethod(typeof(HolyCompat), "ConventionOff"));
				TakeOverRoomButton();
				Events.HolyRooms.TakeOver();
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Could not hand Holy's Fan Convention over to the yearly convention: " + ex.Message);
		}
		try
		{
			Type seasonal = AccessTools.TypeByName("HolyExpansion.Systems.SeasonalSales");
			MethodInfo on = ((seasonal != null) ? AccessTools.PropertyGetter(seasonal, "On") : null);
			if (on != null)
			{
				harmony.Patch(on, new HarmonyMethod(typeof(HolyCompat), "SeasonalOff"));
				TakeOverMenu(seasonal);
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Could not hand Holy's Seasonal Sales over to the sales events: " + ex.Message);
		}
		try
		{
			Type seasonal = AccessTools.TypeByName("HolyExpansion.Systems.SeasonalSales");
			if (seasonal == null)
			{
				return;
			}
			List<Type> types = new List<Type> { seasonal };
			types.AddRange(seasonal.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic));
			int patched = 0;
			foreach (MethodInfo m in types.SelectMany((Type t) => t.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)))
			{
				if (UsesYear(m, HolySeasonalYear))
				{
					harmony.Patch(m, null, null, new HarmonyMethod(typeof(HolyCompat), "SeasonalTranspiler"));
					patched++;
				}
			}
			Plugin.Log.LogInfo("Holy Seasonal Sales: start year now follows the addon (" + patched + " places)");
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Could not change Holy's Seasonal Sales start year: " + ex.Message);
		}
	}

	// Whether the method loads this number as a constant (ldc.i4 <year>).
	private static bool UsesYear(MethodInfo m, int year)
	{
		byte[] il = m.GetMethodBody()?.GetILAsByteArray();
		if (il == null)
		{
			return false;
		}
		byte[] value = BitConverter.GetBytes(year);
		for (int i = 0; i + 4 < il.Length; i++)
		{
			if (il[i] == 0x20 && il[i + 1] == value[0] && il[i + 2] == value[1] && il[i + 3] == value[2] && il[i + 4] == value[3])
			{
				return true;
			}
		}
		return false;
	}

	private static IEnumerable<CodeInstruction> SeasonalTranspiler(IEnumerable<CodeInstruction> instructions)
	{
		MethodInfo year = AccessTools.Method(typeof(HolyCompat), "SeasonalYear");
		foreach (CodeInstruction i in instructions)
		{
			if (i.opcode == OpCodes.Ldc_I4 && i.operand is int v && v == HolySeasonalYear)
			{
				yield return new CodeInstruction(OpCodes.Call, year)
				{
					labels = i.labels,
					blocks = i.blocks
				};
			}
			else
			{
				yield return i;
			}
		}
	}
}
