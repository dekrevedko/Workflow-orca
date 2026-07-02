using System.Collections;

namespace OrcaCore.Core.Definitions;

internal sealed class ReadOnlyList<T> : IReadOnlyList<T>
{
    private readonly T[] items;

    internal ReadOnlyList(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        this.items = items.ToArray();
    }

    public int Count => items.Length;

    public T this[int index] => items[index];

    public IEnumerator<T> GetEnumerator()
    {
        return ((IEnumerable<T>)items).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
