using System;
using System.Collections.Generic;
using System.IO;
using HolyAddon.UI;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HolyAddon.Core.Reviews;

internal class Outlet
{
	public string Id;

	public string Name;

	public string ParodyOf;

	public int From;

	public int To;

	public string Scale;

	public string Voice;

	public float Bias;

	public float Spread;

	public bool Retro;

	public HashSet<string> Genres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	public Sprite Logo => UIKit.LoadSprite(Path.Combine(ReviewContent.Folder, "logos", Id + ".png"));

	public bool ActiveIn(int year)
	{
		if (year >= From)
		{
			return year <= To;
		}
		return false;
	}
}

// reviews/reviews.json next to Editor.dll: the outlets and every pool of review sentences.
// It is re-read whenever the file changes, so text can be edited while the game runs.
internal static class ReviewContent
{
	private static JObject root;

	private static DateTime stamp;

	public static readonly List<Outlet> Outlets = new List<Outlet>();

	public static string Folder => Path.Combine(Plugin.PluginDir, "reviews");

	private static string FilePath => Path.Combine(Folder, "reviews.json");

	// Changes whenever the content is reloaded, so cached reviews know to rebuild.
	public static int Version { get; private set; }

	public static bool Ready()
	{
		try
		{
			if (!File.Exists(FilePath))
			{
				return false;
			}
			DateTime time = File.GetLastWriteTimeUtc(FilePath);
			if (root != null && time == stamp)
			{
				return true;
			}
			stamp = time;
			root = JObject.Parse(File.ReadAllText(FilePath));
			Outlets.Clear();
			foreach (JToken o in (JArray)root["outlets"] ?? new JArray())
			{
				Outlet outlet = new Outlet
				{
					Id = (string)o["id"],
					Name = (string)o["name"],
					ParodyOf = (string)o["parodyOf"],
					From = (int?)o["from"] ?? 0,
					To = (int?)o["to"] ?? 9999,
					Scale = (string)o["scale"] ?? "percent",
					Voice = (string)o["voice"] ?? "serious",
					Bias = (float?)o["bias"] ?? 0f,
					Spread = (float?)o["spread"] ?? 5f,
					Retro = (bool?)o["retro"] ?? false
				};
				foreach (JToken g in (JArray)o["genres"] ?? new JArray())
				{
					outlet.Genres.Add((string)g);
				}
				if (!string.IsNullOrEmpty(outlet.Id) && !string.IsNullOrEmpty(outlet.Name))
				{
					Outlets.Add(outlet);
				}
			}
			Version++;
			Plugin.Log.LogInfo("Review content loaded: " + Outlets.Count + " outlets");
			return true;
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("reviews.json could not be read: " + ex.Message);
			root = null;
			return false;
		}
	}

	// The list of sentences at a path such as ("aspects", "sound", "good"); empty when missing.
	public static IList<string> Pool(params string[] path)
	{
		JToken token = root;
		foreach (string key in path)
		{
			if (!(token is JObject obj) || !obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out token))
			{
				return Array.Empty<string>();
			}
		}
		if (!(token is JArray array))
		{
			return Array.Empty<string>();
		}
		List<string> list = new List<string>();
		foreach (JToken item in array)
		{
			string text = (string)item;
			if (!string.IsNullOrWhiteSpace(text))
			{
				list.Add(text);
			}
		}
		return list;
	}
}
