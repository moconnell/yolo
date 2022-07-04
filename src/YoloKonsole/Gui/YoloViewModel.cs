using System;
using System.Data;
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
public class YoloViewModel : ReactiveObject, IDisposable, IActivatableViewModel
{
    private readonly IYoloRuntime _yoloRuntime;
    private readonly Subject<bool> _canSubmitSubject;
    private readonly Subject<decimal?> _completedSubject;
    private readonly IDisposable _tradeUpdatesSubscription;
    private bool _disposed;

    public YoloViewModel(
        IYoloRuntime yoloRuntime,
        CancellationTokenSource cancellationTokenSource)
    {
        _yoloRuntime = yoloRuntime;
        _canSubmitSubject = new Subject<bool>();
        _completedSubject = new Subject<decimal?>();

        var tradeUpdates = _yoloRuntime
            .TradeUpdates
            // .Throttle(TimeSpan.FromMilliseconds(250))
            .ToObservableChangeSet(x => x.Trade.Id)
            .AsObservableCache();

        var tradeUpdatesObservable = tradeUpdates.Connect();

        TradesTable = tradeUpdatesObservable.AsDataTable(cancellationTokenSource.Token);

        _tradeUpdatesSubscription = tradeUpdatesObservable
            .Subscribe(
                _ =>
                {
                    var tradeUpdatesItems = tradeUpdates.Items.ToArray();
                    _canSubmitSubject.OnNext(tradeUpdatesItems.Any(y => y.Order is null));

                    var orders = tradeUpdatesItems
                        .Select(tr => tr.Order)
                        .Where(o => o is { })
                        .Cast<Order>()
                        .ToArray();
                    var completed = orders.Any()
                        ? 1 -
                          orders.Sum(o => o.AmountRemaining * o.LimitPrice) /
                          orders.Sum(o => o.Amount * o.LimitPrice)
                        : null;
                    _completedSubject.OnNext(completed);
                });

        Submit = ReactiveCommand.CreateFromTask(
            () =>
            {
                _canSubmitSubject.OnNext(false);
                return _yoloRuntime.PlaceTradesAsync(
                    tradeUpdates.Items.Select(x => x.Trade),
                    cancellationTokenSource.Token);
            },
            _canSubmitSubject);
        
        Cancel = ReactiveCommand.Create(cancellationTokenSource.Cancel);

        Observable.StartAsync(() => _yoloRuntime.RebalanceAsync(cancellationTokenSource.Token))
            .ObserveOn(RxApp.MainThreadScheduler);
    }

    public ViewModelActivator Activator { get; } = new();

    [IgnoreDataMember] public ReactiveCommand<Unit, Unit> Submit { get; }
    [IgnoreDataMember] public ReactiveCommand<Unit, Unit> Cancel { get; }

    public DataTable TradesTable { get; }
    public IObservable<decimal?> Completed => _completedSubject;

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
        Submit.Dispose();
        Cancel.Dispose();
        _tradeUpdatesSubscription.Dispose();
        _canSubmitSubject.Dispose();
        _completedSubject.Dispose();

        _disposed = true;
    }
}