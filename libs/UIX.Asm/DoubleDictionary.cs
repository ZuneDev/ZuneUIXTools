using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Iris.Asm;

/// <summary>
/// Represents a dictionary where entries can be quickly retrieved by their key or value.
/// Optimal for one-to-one mappings.
/// </summary>
public class DoubleDictionary<TLeft, TRight>(int capacity = 0) : ICollection<(TLeft, TRight)>
{
    private readonly Dictionary<TLeft, TRight> _leftDict = new(capacity);
    private readonly Dictionary<TRight, TLeft> _rightDict = new(capacity);

    public int Count => _leftDict.Count;
    public bool IsReadOnly => false;

    public IReadOnlyDictionary<TLeft, TRight> GetLeftDictionary() => _leftDict;
    public IReadOnlyDictionary<TRight, TLeft> GetRightDictionary() => _rightDict;

    public void Add(TLeft l, TRight r)
    {
        _leftDict[l] = r;
        _rightDict[r] = l;
    }

    public TRight this[TLeft l] => _leftDict[l];
    public TLeft this[TRight r] => _rightDict[r];

    public bool Contains(TLeft l) => _leftDict.ContainsKey(l);
    public bool Contains(TRight r) => _rightDict.ContainsKey(r);

    public bool TryGetRight(TLeft l, out TRight r) => _leftDict.TryGetValue(l, out r);
    public bool TryGetLeft(TRight r, out TLeft l) => _rightDict.TryGetValue(r, out l);

    public bool Remove(TLeft l)
    {
        if (!TryGetRight(l, out var r))
            return false;
        _leftDict.Remove(l);

        return _rightDict.Remove(r);
    }

    public bool Remove(TRight r)
    {
        if (!TryGetLeft(r, out var l))
            return false;
        _rightDict.Remove(r);

        return _leftDict.Remove(l);
    }

    public IEnumerator<(TLeft, TRight)> GetEnumerator() => _leftDict
        .Select(kv => (kv.Key, kv.Value))
        .GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Add((TLeft, TRight) item) => Add(item.Item1, item.Item2);

    public void Clear()
    {
        _leftDict.Clear();
        _rightDict.Clear();
    }

    public bool Contains((TLeft, TRight) item)
        => _leftDict.TryGetValue(item.Item1, out var r) && r!.Equals(item.Item2);

    public void CopyTo((TLeft, TRight)[] array, int arrayIndex)
    {
        var i = arrayIndex;
        foreach (var pair in this)
            array[i++] = pair;
    }

    public bool Remove((TLeft, TRight) item) => Remove(item.Item1) && Remove(item.Item2);
}