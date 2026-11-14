using System;

namespace HolyAddon.Core;

// The Holy Expansion's price curve (Money.Scaled): prices given in 1990 money rise 6% a year, and early years never
// go below 35%. The addon uses it for its own costs and income so they keep pace with Holy's.
internal static class Prices
{
	public static double Factor => FactorAt(G.MS ? G.MS.year : 1990);

	public static double FactorAt(int year)
	{
		return Math.Max(0.35, Math.Pow(1.06, year - 1990));
	}

	public static long Scaled(double base1990)
	{
		return (long)(base1990 * Factor);
	}

	// The curve against another year: 1 in that year, Holy's 6% a year either side. For prices the addon set for a
	// particular year (the store's for 2003, anti-bot systems' for 1997...), so they're unchanged then and keep pace
	// with Holy's afterwards.
	public static double Since(int anchorYear)
	{
		return Factor / FactorAt(anchorYear);
	}

	public static long Anchored(double value, int anchorYear)
	{
		return Round(value * Since(anchorYear));
	}

	// Round sums: to the thousand from $100,000, the hundred from $10,000, otherwise the ten.
	public static long Round(double v)
	{
		if (v >= 100000.0)
		{
			return (long)Math.Round(v / 1000.0) * 1000;
		}
		if (v >= 10000.0)
		{
			return (long)Math.Round(v / 100.0) * 100;
		}
		return (long)Math.Round(v / 10.0) * 10;
	}
}
