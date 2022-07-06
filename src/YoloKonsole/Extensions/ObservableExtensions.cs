using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace YoloKonsole.Extensions;

public static class ObservableExtensions
{
    public static IObservable<KeyValuePair<TKey, TValue>> Throttle<T, TKey, TValue>(
        this IObservable<T> observable,
        TimeSpan dueTime,
        Func<T, TKey> keyFunc,
        Func<T, TValue> valueFunc,
        IComparer<TValue> comparer,
        CancellationToken cancellationToken) where TKey : notnull
    {
        var subject = new Subject<KeyValuePair<TKey, TValue>>();
        var updates = new ConcurrentDictionary<TKey, (DateTime timeStamp, TValue update)>();

        void OnNext(T update)
        {
            var key = keyFunc(update);
            var value = valueFunc(update);
            if (!updates.TryGetValue(key, out var existing) || comparer.Compare(existing.update, value) != 0)
                updates[key] = (DateTime.UtcNow, value);
        }

        async Task SendUpdates()
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(dueTime, cancellationToken);
                if (updates.IsEmpty)
                {
                    continue;
                }

                var now = DateTime.UtcNow;
                var updatesCopy = updates
                    .Where(kvp => now - kvp.Value.timeStamp <= dueTime);

                foreach (var update in updatesCopy)
                {
                    subject.OnNext(new KeyValuePair<TKey, TValue>(update.Key, update.Value.update));
                }
            }
        }

        observable.Subscribe(OnNext, cancellationToken);

        Task.Factory.StartNew(SendUpdates, cancellationToken);

        return subject;
    }
}