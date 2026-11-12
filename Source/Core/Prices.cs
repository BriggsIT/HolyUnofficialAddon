using System;

namespace HolyAddon.Core;

// The Holy Expansion's price curve (Money.Scaled): prices given in 1990 money rise 6% a year, and early years never
// go below 35%. The addon uses it for its own costs and income so they keep pace with Holy's.
internal static class Prices
{
	public static double Factor
	{
		get
		{
			int year = (G.MS ? G.MS.year : 1990);
			return Math.Max(0.35, Math.Pow(1.06, year - 1990));
		}
	}

	public static long Scaled(double base1990)
	{
		return (long)(base1990 * Factor);
	}
}
