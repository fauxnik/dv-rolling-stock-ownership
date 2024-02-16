namespace RollingStockOwnership.Extensions;

internal static class int_Extensions
{
	// By redditor BinaryCow: https://www.reddit.com/r/csharp/comments/17y6df7/comment/k9rruxm/
	public static int Modulo(this int left, int right)
	{
		bool isExactlyOneNegative = left < 0 ^ right < 0;
		if (isExactlyOneNegative)
		{
			left = left + right;
		}
		int result = left % right;
		return result;
	}
}
