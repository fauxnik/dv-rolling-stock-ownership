using System;
using System.Collections.Generic;
using System.Linq;

namespace RollingStockOwnership.Extensions;

internal static class IEnumerable_Extensions
{
	public static IEnumerable<T> Replace<T>(this IEnumerable<T> enumerable, int index, T value)
	{
		return enumerable.Select((x, i) => index == i ? value : x);
	}

	public static IEnumerable<T> RandomSorting<T>(this IEnumerable<T> enumerable, System.Random rng)
	{
		int length = enumerable.Count();
		for (int leftToSort = length; leftToSort > 0; --leftToSort)
		{
			int index = rng.Next(leftToSort);
			T swap = enumerable.ElementAt(leftToSort - 1);
			enumerable = enumerable.Replace(leftToSort - 1, enumerable.ElementAt(index));
			enumerable = enumerable.Replace(index, swap);
		}
		return enumerable;
	}

	public static T ElementAtRandom<T>(this IEnumerable<T> enumerable, System.Random rng)
	{
		var index = rng.Next(enumerable.Count());
		return enumerable.ElementAt(index);
	}

	public static IEnumerable<T> SkipTakeCyclical<T>(this IEnumerable<T> enumerable, int skip, int take)
	{
		if (take < 0)
		{
			throw new ArgumentOutOfRangeException("Number of elements to take must be positive.");
		}

		int count = enumerable.Count();
		if (count < 1)
		{
			yield break;
		}

		int current = skip;
		while (current < skip + take)
		{
			yield return enumerable.ElementAt(current++.Modulo(count));
		}
	}
}
