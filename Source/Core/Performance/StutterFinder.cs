using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Profiling;

namespace HolyAddon.Core.Performance;

// Finds what makes the game stutter. While it's on (Game settings > Stutter finder, or the config file) it times:
//   - every script's per-frame work (Update / LateUpdate / FixedUpdate) in the game, the addon and the other
//     installed plugins, added up per frame;
//   - the game's big recurring jobs (daily, weekly and monthly updates, autosave, loading and saving, scene
//     scans) and every big method of its manager classes;
//   - what menus do when they open or you click something.
// Every frame slower than the threshold (50 ms by default) is noted with what ran in it, and whether the garbage
// collector ran. Memory use (including how close the 32-bit game gets to its 4 GB limit) is sampled every 15
// seconds. Everything goes to BepInEx\StutterReport.txt, rewritten every minute and when it's switched off.
//
// The timing hooks are added when it's first switched on (that pauses the game for a few seconds). Switching it off
// stops the timing at once, but the hooks stay in place, idle, until the game restarts: taking thousands of hooks
// out of a running 32-bit game crashed it to the desktop (0.14.1). With the setting off at start, nothing is hooked.
internal class StutterFinder : MonoBehaviour
{
	private const string HarmonyId = Plugin.Guid + ".stutterfinder";

	private class Stat
	{
		public string Name;

		public bool PerFrame;

		public long Ticks;

		public long Calls;

		public long Max;

		public long FrameTicks;

		public int FrameCalls;
	}

	private class Slow
	{
		public float Ms;

		public string When;

		public string GameDate;

		public bool Gc;

		public string Context;

		public List<string> Top;
	}

	private static readonly Dictionary<MethodBase, Stat> stats = new Dictionary<MethodBase, Stat>();

	private static readonly List<Stat> touched = new List<Stat>();

	private static readonly List<Slow> worst = new List<Slow>();

	private static readonly List<Slow> recent = new List<Slow>();

	private static Harmony harmony;

	private static bool applied;

	// Whether the hooks time anything: false once switched off (the hooks themselves stay until the game restarts).
	private static volatile bool timing;

	private static int mainThread;

	private static string instrumentedNote = "";

	private long lastFrame;

	private int lastGc;

	private DateTime started;

	private long frames;

	private double frameMsTotal;

	private int slowCount;

	private int gcFrames;

	private int gcCountStart;

	private readonly int[] buckets = new int[4];

	private float nextMemory;

	private float nextReport;

	private string memoryNow = "";

	private long peakMono;

	private long peakPrivate;

	private long peakVirtual;

	private static readonly MethodInfo BeforeMethod = AccessTools.Method(typeof(StutterFinder), "Before");

	private static readonly MethodInfo AfterMethod = AccessTools.Method(typeof(StutterFinder), "After");

	public static string ReportPath => Path.Combine(Paths.BepInExRootPath, "StutterReport.txt");

	private static float Threshold => Mathf.Max(16f, Plugin.StutterThresholdMs.Value);

	// ---- Timing hooks ----

	private static void Before(out long __state)
	{
		__state = (timing ? Stopwatch.GetTimestamp() : 0);
	}

	private static void After(long __state, MethodBase __originalMethod)
	{
		if (__state == 0L || !timing)
		{
			return;
		}
		long t = Stopwatch.GetTimestamp() - __state;
		if (Thread.CurrentThread.ManagedThreadId != mainThread || !stats.TryGetValue(__originalMethod, out var s))
		{
			return;
		}
		s.Ticks += t;
		s.Calls++;
		if (t > s.Max)
		{
			s.Max = t;
		}
		if (s.FrameCalls == 0)
		{
			touched.Add(s);
		}
		s.FrameTicks += t;
		s.FrameCalls++;
	}

	private static float Ms(long ticks)
	{
		return (float)((double)ticks * 1000.0 / (double)Stopwatch.Frequency);
	}

	// ---- Switching on and off ----

	private void Update()
	{
		long now = Stopwatch.GetTimestamp();
		bool want = Plugin.StutterFinderOn.Value;
		if (want != applied)
		{
			if (want)
			{
				Hook();
			}
			else
			{
				Unhook();
			}
			lastFrame = Stopwatch.GetTimestamp();
			return;
		}
		if (!applied)
		{
			return;
		}
		try
		{
			Frame(now);
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Stutter finder: " + ex);
		}
		lastFrame = now;
	}

	private void Hook()
	{
		mainThread = Thread.CurrentThread.ManagedThreadId;
		Stopwatch watch = Stopwatch.StartNew();
		harmony = harmony ?? new Harmony(HarmonyId);
		HarmonyMethod before = new HarmonyMethod(BeforeMethod);
		HarmonyMethod after = new HarmonyMethod(AfterMethod);
		int ok = 0;
		int failedCount = 0;
		int already = stats.Count;
		// Switched on again: the hooks from last time are still there, only their numbers start over.
		foreach (Stat s in stats.Values)
		{
			s.Ticks = 0L;
			s.Calls = 0L;
			s.Max = 0L;
			s.FrameTicks = 0L;
			s.FrameCalls = 0;
		}
		touched.Clear();
		foreach (KeyValuePair<MethodBase, bool> target in Targets())
		{
			MethodBase m = target.Key;
			if (stats.ContainsKey(m))
			{
				continue;
			}
			stats[m] = new Stat
			{
				Name = NameOf(m),
				PerFrame = target.Value
			};
			try
			{
				harmony.Patch(m, before, after);
				ok++;
			}
			catch (Exception)
			{
				stats.Remove(m);
				failedCount++;
			}
		}
		applied = true;
		timing = true;
		started = DateTime.Now;
		frames = 0L;
		frameMsTotal = 0.0;
		slowCount = 0;
		gcFrames = 0;
		Array.Clear(buckets, 0, buckets.Length);
		worst.Clear();
		recent.Clear();
		lastGc = (gcCountStart = GC.CollectionCount(0));
		peakMono = (peakPrivate = (peakVirtual = 0L));
		nextMemory = 0f;
		nextReport = Time.unscaledTime + 60f;
		instrumentedNote = "Timing " + stats.Count + " methods" + ((already > 0) ? (" (" + already + " still hooked from earlier this session)") : "") + ((failedCount > 0) ? (" (" + failedCount + " couldn't be hooked)") : "") + ", hooked in " + watch.ElapsedMilliseconds + " ms.";
		Plugin.Log.LogInfo("Stutter finder on. " + instrumentedNote + " Report: " + ReportPath);
	}

	// Stops timing. The hooks stay, idle (two quick calls per hooked method), until the game restarts: removing
	// thousands of them from the running game isn't safe on this 32-bit build.
	private void Unhook()
	{
		if (applied)
		{
			WriteReport();
		}
		timing = false;
		applied = false;
		touched.Clear();
		Plugin.Log.LogInfo("Stutter finder off. Its hooks stay idle until the game restarts. Report kept in " + ReportPath);
	}

	private void OnApplicationQuit()
	{
		if (applied)
		{
			WriteReport();
		}
	}

	// ---- Each frame ----

	private void Frame(long now)
	{
		if (lastFrame != 0L)
		{
			float ms = Ms(now - lastFrame);
			int gc = GC.CollectionCount(0);
			bool gcRan = gc != lastGc;
			lastGc = gc;
			// Frames while the game window isn't in front are slowed down on purpose by Windows and Unity.
			if (Application.isFocused)
			{
				frames++;
				frameMsTotal += ms;
				if (gcRan)
				{
					gcFrames++;
				}
				if (ms >= Threshold)
				{
					NoteSlow(ms, gcRan);
				}
			}
		}
		foreach (Stat s in touched)
		{
			s.FrameTicks = 0L;
			s.FrameCalls = 0;
		}
		touched.Clear();
		if (Time.unscaledTime >= nextMemory)
		{
			nextMemory = Time.unscaledTime + 15f;
			SampleMemory();
		}
		if (Time.unscaledTime >= nextReport)
		{
			nextReport = Time.unscaledTime + 60f;
			WriteReport();
		}
	}

	private void NoteSlow(float ms, bool gcRan)
	{
		slowCount++;
		buckets[(ms < 100f) ? 0 : ((ms < 250f) ? 1 : ((ms < 500f) ? 2 : 3))]++;
		Slow slow = new Slow
		{
			Ms = ms,
			When = DateTime.Now.ToString("HH:mm:ss"),
			GameDate = GameDate(),
			Gc = gcRan,
			Context = Context(),
			Top = (from s in touched
				where s.FrameTicks > 0
				orderby s.FrameTicks descending
				select s).Take(8).Select((Stat s) => s.Name + ((s.FrameCalls > 1) ? (" x" + s.FrameCalls) : "") + ": " + Ms(s.FrameTicks).ToString("0.0") + " ms").ToList()
		};
		recent.Add(slow);
		if (recent.Count > 15)
		{
			recent.RemoveAt(0);
		}
		worst.Add(slow);
		if (worst.Count > 30)
		{
			worst.Sort((Slow a, Slow b) => b.Ms.CompareTo(a.Ms));
			worst.RemoveRange(30, worst.Count - 30);
		}
	}

	private static string GameDate()
	{
		if (!G.MS)
		{
			return "main menu";
		}
		return "Y" + G.MS.year + " M" + G.MS.month + " W" + G.MS.week;
	}

	private static string Context()
	{
		List<string> parts = new List<string>();
		try
		{
			if ((bool)G.MS)
			{
				float speed = G.MS.GetGameSpeed();
				parts.Add((speed <= 0f) ? "paused" : ("speed " + speed.ToString("0.#")));
			}
			if ((bool)G.Gui && G.Gui.menuOpen)
			{
				parts.Add("menu open");
			}
		}
		catch (Exception)
		{
		}
		return string.Join(", ", parts);
	}

	// Windows' own memory counters: .NET's Process memory properties read 0 inside Unity's Mono.
	[StructLayout(LayoutKind.Sequential)]
	private struct ProcessMemoryCounters
	{
		public uint cb;

		public uint PageFaultCount;

		public UIntPtr PeakWorkingSetSize;

		public UIntPtr WorkingSetSize;

		public UIntPtr QuotaPeakPagedPoolUsage;

		public UIntPtr QuotaPagedPoolUsage;

		public UIntPtr QuotaPeakNonPagedPoolUsage;

		public UIntPtr QuotaNonPagedPoolUsage;

		public UIntPtr PagefileUsage;

		public UIntPtr PeakPagefileUsage;

		public UIntPtr PrivateUsage;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct MemoryStatus
	{
		public uint dwLength;

		public uint dwMemoryLoad;

		public ulong ullTotalPhys;

		public ulong ullAvailPhys;

		public ulong ullTotalPageFile;

		public ulong ullAvailPageFile;

		public ulong ullTotalVirtual;

		public ulong ullAvailVirtual;

		public ulong ullAvailExtendedVirtual;
	}

	[DllImport("kernel32.dll")]
	private static extern IntPtr GetCurrentProcess();

	[DllImport("kernel32.dll", EntryPoint = "K32GetProcessMemoryInfo")]
	private static extern bool GetProcessMemoryInfo(IntPtr process, ref ProcessMemoryCounters counters, uint size);

	[DllImport("kernel32.dll")]
	private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

	// Private bytes, and how much of the process's address space is in use (and its size: 4 GB for this game).
	private static void Win32Memory(out long priv, out long used, out long total)
	{
		priv = (used = (total = 0L));
		try
		{
			ProcessMemoryCounters c = new ProcessMemoryCounters
			{
				cb = (uint)Marshal.SizeOf(typeof(ProcessMemoryCounters))
			};
			if (GetProcessMemoryInfo(GetCurrentProcess(), ref c, c.cb))
			{
				priv = (long)c.PrivateUsage.ToUInt64();
			}
			MemoryStatus m = new MemoryStatus
			{
				dwLength = (uint)Marshal.SizeOf(typeof(MemoryStatus))
			};
			if (GlobalMemoryStatusEx(ref m))
			{
				total = (long)m.ullTotalVirtual;
				used = (long)(m.ullTotalVirtual - m.ullAvailVirtual);
			}
		}
		catch (Exception)
		{
		}
	}

	private static long addressTotal;

	private void SampleMemory()
	{
		try
		{
			long mono = Profiler.GetMonoUsedSizeLong();
			long monoHeap = Profiler.GetMonoHeapSizeLong();
			long unity = Profiler.GetTotalAllocatedMemoryLong();
			Win32Memory(out var priv, out var virt, out var total);
			if (total > 0)
			{
				addressTotal = total;
			}
			peakMono = Math.Max(peakMono, mono);
			peakPrivate = Math.Max(peakPrivate, priv);
			peakVirtual = Math.Max(peakVirtual, virt);
			memoryNow = "Script memory (Mono) " + Mb(mono) + " used of a " + Mb(monoHeap) + " heap, engine (Unity) " + Mb(unity) + ", whole game " + Mb(priv) + " private, address space " + Mb(virt) + " of " + Mb(addressTotal);
		}
		catch (Exception ex)
		{
			memoryNow = "Memory couldn't be read: " + ex.Message;
		}
	}

	private static string Mb(long bytes)
	{
		return (bytes / 1048576).ToString("N0") + " MB";
	}

	// ---- What to time ----

	// Manager classes whose big methods are timed one call at a time.
	private static readonly string[] Managers = new string[9] { "mainScript", "games", "platforms", "savegameScript", "arbeitsmarkt", "GUI_Main", "publisherScript", "gameScript", "genres" };

	private static readonly string[] MenuPrefixes = new string[5] { "Init", "BUTTON_", "DROPDOWN_", "TOGGLE_", "Open" };

	private static readonly string[] PluginEvents = new string[13] { "Weekly", "Monthly", "Yearly", "Daily", "Open", "Build", "Rebuild", "Refresh", "Tick", "TickAll", "Spawn", "Inject", "Check" };

	// A plugin's systems: static methods without arguments that return nothing or yes/no (Holy's HolyRunner runs
	// its systems as Tick() methods like these), timed one by one so a runner's slow frames can be pinned on one.
	private static bool PluginSystem(MethodInfo m)
	{
		if (m.IsStatic && m.GetParameters().Length == 0 && (m.ReturnType == typeof(void) || m.ReturnType == typeof(bool)))
		{
			return m.Name.IndexOf('<') < 0;
		}
		return false;
	}

	private static IEnumerable<KeyValuePair<MethodBase, bool>> Targets()
	{
		Assembly game = typeof(mainScript).Assembly;
		List<Assembly> assemblies = new List<Assembly> { game };
		foreach (PluginInfo info in Chainloader.PluginInfos.Values)
		{
			Assembly a = (((object)info.Instance != null) ? info.Instance.GetType().Assembly : null);
			if (a != null && !assemblies.Contains(a))
			{
				assemblies.Add(a);
			}
		}
		foreach (Assembly a in assemblies)
		{
			bool isGame = a == game;
			foreach (Type t in TypesOf(a))
			{
				if (t == typeof(StutterFinder) || t.IsGenericTypeDefinition || t.IsInterface)
				{
					continue;
				}
				if (typeof(MonoBehaviour).IsAssignableFrom(t))
				{
					foreach (string name in new string[3] { "Update", "LateUpdate", "FixedUpdate" })
					{
						MethodInfo m = t.GetMethod(name, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
						if (Patchable(m))
						{
							yield return new KeyValuePair<MethodBase, bool>(m, value: true);
						}
					}
				}
				MethodInfo[] methods;
				try
				{
					methods = t.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				}
				catch (Exception)
				{
					continue;
				}
				bool manager = isGame && Managers.Contains(t.Name);
				bool menu = isGame && t.Name.StartsWith("Menu_", StringComparison.Ordinal);
				foreach (MethodInfo m2 in methods)
				{
					if (m2.IsSpecialName || m2.Name == "Update" || m2.Name == "LateUpdate" || m2.Name == "FixedUpdate" || !Patchable(m2))
					{
						continue;
					}
					bool take = (manager ? (ILSize(m2) >= 150) : ((!menu) ? (!isGame && (PluginEvents.Contains(m2.Name) || PluginSystem(m2))) : MenuPrefixes.Any((string p) => m2.Name.StartsWith(p, StringComparison.Ordinal))));
					if (take)
					{
						yield return new KeyValuePair<MethodBase, bool>(m2, value: false);
					}
				}
			}
		}
	}

	private static IEnumerable<Type> TypesOf(Assembly a)
	{
		try
		{
			return a.GetTypes();
		}
		catch (ReflectionTypeLoadException ex)
		{
			return ex.Types.Where((Type t) => t != null);
		}
		catch (Exception)
		{
			return new Type[0];
		}
	}

	private static bool Patchable(MethodInfo m)
	{
		if (m == null || m.IsAbstract || m.ContainsGenericParameters || m.DeclaringType == null || m.DeclaringType.ContainsGenericParameters || !SafeReturn(m))
		{
			return false;
		}
		try
		{
			return m.GetMethodBody() != null;
		}
		catch (Exception)
		{
			return false;
		}
	}

	// The game is a 32-bit build. There, Harmony's hooking library (MonoMod) wraps a hooked method that returns
	// an 8-byte value (a long such as a money total, a double, or a struct like Vector2) as if the caller passed a
	// hidden pointer for the result. Callers don't, so the result comes back as garbage and 8 bytes get written
	// somewhere random. Seen in 0.12.1: Runescape's profit (gameScript.GetGesamtGewinn) showed -2.46 billion and
	// tooltips threw errors. Only hook methods that return nothing, a reference, or a value of 4 bytes or less.
	public static bool SafeReturn(MethodInfo m)
	{
		Type r = m.ReturnType;
		if (r == typeof(void) || !r.IsValueType)
		{
			return true;
		}
		if (r.IsEnum)
		{
			r = Enum.GetUnderlyingType(r);
		}
		if (r == typeof(int) || r == typeof(uint) || r == typeof(short) || r == typeof(ushort) || r == typeof(byte) || r == typeof(sbyte) || r == typeof(bool) || r == typeof(char) || r == typeof(float))
		{
			return true;
		}
		if (IntPtr.Size == 8)
		{
			return r == typeof(long) || r == typeof(ulong) || r == typeof(double) || r == typeof(IntPtr) || r == typeof(UIntPtr);
		}
		return r == typeof(IntPtr) || r == typeof(UIntPtr);
	}

	private static int ILSize(MethodInfo m)
	{
		try
		{
			return m.GetMethodBody()?.GetILAsByteArray()?.Length ?? 0;
		}
		catch (Exception)
		{
			return 0;
		}
	}

	private static string NameOf(MethodBase m)
	{
		string assembly = m.DeclaringType.Assembly.GetName().Name;
		string tag = (assembly == "Assembly-CSharp") ? "" : (assembly switch
		{
			"HolyExpansion" => "[Holy] ",
			"HolyUnofficialAddon" => "[Addon] ",
			"GameDevAssistant" => "[GDA] ",
			_ => "[" + assembly + "] ",
		});
		return tag + m.DeclaringType.Name + "." + m.Name;
	}

	// ---- The report ----

	private void WriteReport()
	{
		try
		{
			SampleMemory();
			StringBuilder sb = new StringBuilder();
			TimeSpan running = DateTime.Now - started;
			double avg = ((frames > 0) ? (frameMsTotal / (double)frames) : 0.0);
			sb.AppendLine("Holy Unofficial Addon " + Plugin.Version + " - stutter report");
			sb.AppendLine("Written " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ", game date " + GameDate() + ", Unity " + Application.unityVersion + ", " + (IntPtr.Size * 8) + "-bit");
			sb.AppendLine("Plugins: " + string.Join(", ", Chainloader.PluginInfos.Values.Select((PluginInfo p) => p.Metadata.Name + " " + p.Metadata.Version)));
			sb.AppendLine(instrumentedNote);
			sb.AppendLine();
			sb.AppendLine("== Summary ==");
			sb.AppendLine("Recording for " + (int)running.TotalMinutes + " min " + running.Seconds + " s, " + frames.ToString("N0") + " frames in focus, average " + avg.ToString("0.0") + " ms a frame (" + ((avg > 0.0) ? (1000.0 / avg).ToString("0") : "-") + " fps).");
			sb.AppendLine("Slow frames (" + Threshold.ToString("0") + " ms or more): " + slowCount + "  [under 100 ms: " + buckets[0] + ", 100-250: " + buckets[1] + ", 250-500: " + buckets[2] + ", 500+: " + buckets[3] + "]");
			sb.AppendLine("Garbage collections: " + (GC.CollectionCount(0) - gcCountStart) + " (in " + gcFrames + " frames)");
			sb.AppendLine(memoryNow);
			sb.AppendLine("Peaks: script memory " + Mb(peakMono) + ", whole game " + Mb(peakPrivate) + " private, address space " + Mb(peakVirtual) + " of " + Mb(addressTotal));
			sb.AppendLine();
			sb.AppendLine("== Slowest frames, and what ran in them (times include what each called) ==");
			foreach (Slow s in worst.OrderByDescending((Slow x) => x.Ms))
			{
				AppendSlow(sb, s);
			}
			sb.AppendLine();
			sb.AppendLine("== Most recent slow frames ==");
			foreach (Slow s2 in recent)
			{
				AppendSlow(sb, s2);
			}
			List<Stat> all = stats.Values.Where((Stat s) => s.Calls > 0).ToList();
			sb.AppendLine();
			sb.AppendLine("== Steady cost every frame (scripts' per-frame work, average per frame) ==");
			foreach (Stat s3 in all.Where((Stat s) => s.PerFrame).OrderByDescending((Stat s) => s.Ticks).Take(30))
			{
				double perFrame = ((frames > 0) ? ((double)Ms(s3.Ticks) / (double)frames) : 0.0);
				double calls = ((frames > 0) ? ((double)s3.Calls / (double)frames) : 0.0);
				sb.AppendLine("  " + perFrame.ToString("0.00").PadLeft(7) + " ms  " + s3.Name + "  (" + calls.ToString("0.#") + " calls a frame, slowest " + Ms(s3.Max).ToString("0.0") + " ms)");
			}
			sb.AppendLine();
			sb.AppendLine("== Biggest single calls (longest one call took) ==");
			foreach (Stat s4 in all.Where((Stat s) => !s.PerFrame).OrderByDescending((Stat s) => s.Max).Take(40))
			{
				sb.AppendLine("  " + Ms(s4.Max).ToString("0.0").PadLeft(8) + " ms  " + s4.Name + "  (" + s4.Calls.ToString("N0") + " calls, " + (Ms(s4.Ticks) / (float)s4.Calls).ToString("0.00") + " ms on average, " + Ms(s4.Ticks).ToString("N0") + " ms in all)");
			}
			sb.AppendLine();
			sb.AppendLine("== Most time in all (jobs and menus) ==");
			foreach (Stat s5 in all.Where((Stat s) => !s.PerFrame).OrderByDescending((Stat s) => s.Ticks).Take(30))
			{
				sb.AppendLine("  " + Ms(s5.Ticks).ToString("N0").PadLeft(8) + " ms  " + s5.Name + "  (" + s5.Calls.ToString("N0") + " calls)");
			}
			sb.AppendLine();
			sb.AppendLine("Not measured: drawing the picture (rendering), physics and Unity's own work. A slow frame whose listed");
			sb.AppendLine("times add up to much less than its length was mostly that, or the garbage collector.");
			File.WriteAllText(ReportPath, sb.ToString());
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Stutter finder: writing the report failed: " + ex.Message);
		}
	}

	private static void AppendSlow(StringBuilder sb, Slow s)
	{
		sb.AppendLine("  " + s.Ms.ToString("0").PadLeft(5) + " ms at " + s.When + ", " + s.GameDate + ((s.Context.Length > 0) ? (", " + s.Context) : "") + (s.Gc ? ", garbage collection ran" : ""));
		if (s.Top.Count == 0)
		{
			sb.AppendLine("           nothing timed ran long in it");
		}
		foreach (string line in s.Top)
		{
			sb.AppendLine("           " + line);
		}
	}
}
