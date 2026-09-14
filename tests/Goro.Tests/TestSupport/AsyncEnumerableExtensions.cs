namespace Goro.Tests.TestSupport;

internal static class AsyncEnumerableExtensions
{
    // .NET 10's System.Linq.AsyncEnumerable already provides ToListAsync/ToAsyncEnumerable
    // for IAsyncEnumerable<T>; only the AsAsyncEnumerable name differs from its ToAsyncEnumerable.
    public static async IAsyncEnumerable<T> AsAsyncEnumerable<T>(this IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            await Task.Yield();
            yield return item;
        }
    }
}
