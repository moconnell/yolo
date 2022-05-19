using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.Serialization;
using System.Threading;
using DynamicData;
using ReactiveUI;
using YoloAbstractions;
using YoloRuntime;

namespace YoloKonsole;

[DataContract]
public class YoloViewModel : ReactiveObject, IDisposable
{
    private readonly IYoloRuntime _yoloRuntime;
    private readonly Subject<bool> _canSubmitSubject;
    private readonly IDisposable _tradeUpdatesSubscription;
    private bool _disposed;

    public YoloViewModel(
        IYoloRuntime yoloRuntime,
        CancellationTokenSource cancellationTokenSource)
    {
        _yoloRuntime = yoloRuntime;
        _canSubmitSubject = new Subject<bool>();

        TradeUpdates = _yoloRuntime.TradeUpdates
            .Throttle(TimeSpan.FromMilliseconds(250))
            .ToObservableChangeSet(x => x.Trade.Id)
            .AsObservableCache();

        _tradeUpdatesSubscription = TradeUpdates
            .Connect()
            .Subscribe(
                x =>
                {
                    var canSubmit = TradeUpdates.Items.Any(y => y.Order is null);
                    _canSubmitSubject.OnNext(canSubmit);
                });

        Submit = ReactiveCommand.CreateFromTask(
            () =>
            {
                _canSubmitSubject.OnNext(false);
                return _yoloRuntime.PlaceTradesAsync(Trades, cancellationTokenSource.Token);
            },
            _canSubmitSubject);
        Cancel = ReactiveCommand.Create(cancellationTokenSource.Cancel);
    }

    [IgnoreDataMember] public ReactiveCommand<Unit, Unit> Submit { get; }
    [IgnoreDataMember] public ReactiveCommand<Unit, Unit> Cancel { get; }

    public IObservableCache<TradeResult, Guid> TradeUpdates { get; }

    private IEnumerable<Trade> Trades => TradeUpdates.Items.Select(x => x.Trade);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing || _disposed)
        {
            return;
        }

        _yoloRuntime.Dispose();
        TradeUpdates.Dispose();
        Submit.Dispose();
        Cancel.Dispose();
        _tradeUpdatesSubscription.Dispose();
        _canSubmitSubject.Dispose();

        _disposed = true;
    }
}