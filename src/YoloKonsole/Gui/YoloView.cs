using System.Reactive.Disposables;
using System.Reactive.Linq;
using NStack;
using ReactiveMarbles.ObservableEvents;
using ReactiveUI;
using Terminal.Gui;

namespace YoloKonsole;

public class YoloView : Window, IViewFor<YoloViewModel>
{
    private readonly CompositeDisposable _disposable = new();

    public YoloView(YoloViewModel viewModel) : base("YOLO!")
    {
        ViewModel = viewModel;

        View titleLabel = TitleLabel();
        var tableView = TradesTable(titleLabel);
        var submitButton = SubmitButton(tableView);
        var cancelButton = CancelButton(submitButton);
        RebalanceProgressLabel(cancelButton);
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
        base.Dispose(disposing);
    }

    private Label TitleLabel()
    {
        var label = new Label("Rebalance");
        Add(label);
        return label;
    }

    private TableView TradesTable(View previous)
    {
        var tableView = new TableView(ViewModel.Trades)
        {
            X = Pos.Left(previous),
            Y = Pos.Top(previous) + 1,
            AutoSize = true,
            Width = 127,
            Height = 14
        };
        
        Add(tableView);

        return tableView;
    }

    // private (View, decimal) TradeResultRow((View, decimal) tuple, TradeResult tradeResult)
    // {
    //     var (previous, previousCompleted) = tuple;
    //     var t = tradeResult.Trade;
    //     var o = tradeResult.Order;
    //     var e = tradeResult.Error;
    //     var text = ustring.Make($"{t.AssetName}\t{t.Side}\t{t.Amount}\t{o?.OrderStatus}\t{o?.Completed:0%}\t{e}");
    //     var tradeLabel = new Label(text)
    //     {
    //         X = Pos.Left(previous),
    //         Y = Pos.Top(previous) + 1,
    //         Width = 127
    //     };
    //
    //     tradeResult
    //         .WhenAnyValue(x => x.Error)
    //         .Select(error => error is { } ? Colors.Error : Colors.Base)
    //         .BindTo(tradeLabel, x => x.ColorScheme)
    //         .DisposeWith(_disposable);
    //
    //     Add(tradeLabel);
    //
    //     return (tradeLabel, previousCompleted + o?.Completed ?? 0);
    // }

    private Label RebalanceProgressLabel(View previous)
    {
        var idle = ustring.Make("Press 'Submit' to execute trades");
        var rebalanceProgressLabel = new Label(idle)
        {
            X = Pos.Left(previous),
            Y = Pos.Top(previous) + 1,
            Width = 40
        };

        ViewModel
            .WhenAnyObservable(x => x.Completed)
            .Select(completed => completed.HasValue ? ustring.Make($"Rebalancing... {ViewModel.Completed:0%}") : idle)
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
            Y = Pos.Bottom(previous) + 1,
            Width = 12
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
            X = Pos.Right(previous) + 1,
            Y = Pos.Top(previous),
            Width = 12
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