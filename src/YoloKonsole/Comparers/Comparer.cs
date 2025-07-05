using System;
using System.Collections.Generic;

namespace YoloKonsole.Comparers;

public class Comparer<T> : IComparer<T>
{
    private readonly Func<T?, T?, int> _compareImplementation;

    public Comparer(Func<T?, T?, int> compareImplementation) => _compareImplementation = compareImplementation;

    public int Compare(T? x, T? y) => _compareImplementation(x, y);
}