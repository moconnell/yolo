using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using NStack;
using ReactiveMarbles.ObservableEvents;
using ReactiveUI;
using Terminal.Gui;
using YoloAbstractions;

namespace YoloKonsole;

public class YoloView : Window, IViewFor<YoloViewModel>
{
    private readonly CompositeDisposable _disposable = new();
    private readonly SortedDictionary<Guid, TradeResult> _tradeResults;
    private readonly object _tradeResultsLock = new();
    private readonly IDisposable _tradeUpdateSubscription;

    public YoloView(YoloViewModel viewModel) : base("YOLO!")
    {
        ViewModel = viewModel;
        _tradeResults = new SortedDictionary<Guid, TradeResult>();
        _tradeUpdateSubscription = ViewModel.TradeUpdates
            .Connect()
            .Subscribe(OnNext);
    }

    private IReadOnlyCollection<TradeResult> TradeResults
    {
        get
        {
            lock (_tradeResultsLock)
            {
                return _tradeResults.Values.ToArray();
            }
        }
    }

    public YoloViewModel ViewModel { get; set; }

    object IViewFor.ViewModel
    {
        get => ViewModel;
        set => ViewModel = (YoloViewModel) value;
    }

    protected override void Dispose(bool disposing)
    {
        _disposable.Dispose();
        _tradeUpdateSubscription.Dispose();
        base.Dispose(disposing);
    }

    private void OnNext(IChangeSet<TradeResult, Guid> changes)
    {
        UpdateCache(changes);

        View titleLabel = TitleLabel();
        var tradeResults = TradeResults;
        var (lastRow, completed) = tradeResults.Aggregate((titleLabel, 0m), TradeResultRow);
        var submitButton = SubmitButton(lastRow);
        var cancelButton = CancelButton(submitButton);
        var averageCompleted = completed / tradeResults.Count;
        RebalanceProgressLabel(averageCompleted, cancelButton);
    }

    private void UpdateCache(IChangeSet<TradeResult, Guid> changes)
    {
        foreach (var change in changes)
        {
            switch (change.Reason)
            {
                case ChangeReason.Add:
                case ChangeReason.Update:
                case ChangeReason.Refresh:
                    lock (_tradeResultsLock)
                    {
                        _tradeResults[change.Key] = change.Current;
                    }

                    break;
                case ChangeReason.Remove:
                    lock (_tradeResultsLock)
                    {
                        _tradeResults.Remove(change.Key, out _);
                    }

                    break;
                case ChangeReason.Moved:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(change.Reason));
            }
        }
    }

    private Label TitleLabel()
    {
        var label = new Label("Rebalance");
        Add(label);
        return label;
    }

    private (View, decimal) TradeResultRow((View, decimal) tuple, TradeResult tradeResult)
    {
        var (previous, previousCompleted) = tuple;
        var t = tradeResult.Trade;
        var o = tradeResult.Order;
        var e = tradeResult.Error;
        var text = ustring.Make($"{t.AssetName}\t{t.Side}\t{t.Amount}\t{o?.OrderStatus}\t{o?.Completed:0%}\t{e}");
        var tradeLabel = new Label(text)
        {
            X = Pos.Left(previous),
            Y = Pos.Top(previous) + 1,
            Width = 127
        };

        tradeResult
            .WhenAnyValue(x => x.Error)
            .Select(error => error is { } ? Colors.Error : Colors.Base)
            .BindTo(tradeLabel, x => x.ColorScheme)
            .DisposeWith(_disposable);

        Add(tradeLabel);

        return (tradeLabel, previousCompleted + o?.Completed ?? 0);
    }

    private Label RebalanceProgressLabel(decimal completed, View previous)
    {
        var progress = ustring.Make($"Rebalancing... {completed:0%}");
        var idle = ustring.Make("Press 'Submit' to execute trades");
        var rebalanceProgressLabel = new Label(idle)
        {
            X = Pos.Left(previous),
            Y = Pos.Top(previous) + 1,
            Width = 40
        };

        ViewModel
            .WhenAnyObservable(x => x.Submit.IsExecuting)
            .Select(executing => executing ? progress : idle)
            .ObserveOn(RxApp.MainThreadScheduler)
            .BindTo(rebalanceProgressLabel, x => x.Text)
            .DisposeWith(_disposable);

        Add(rebalanceProgressLabel);

        return rebalanceProgressLabel;
    }

    private Button SubmitButton(View previous)
    {
        var loginButton = new Button("Submit")
        {
            X = Pos.Left(previous),
            Y = Pos.Top(previous) + 1,
            Width = 40
        };
        loginButton
            .Events()
            .Clicked
            .InvokeCommand(ViewModel, x => x.Submit)
            .DisposeWith(_disposable);
        Add(loginButton);
        return loginButton;
    }

    private Button CancelButton(View previous)
    {
        var cancelButton = new Button("Cancel")
        {
            X = Pos.Left(previous),
            Y = Pos.Top(previous) + 1,
            Width = 40
        };
        cancelButton
            .Events()
            .Clicked
            .InvokeCommand(ViewModel, x => x.Cancel)
            .DisposeWith(_disposable);
        Add(cancelButton);
        return cancelButton;
    }
}