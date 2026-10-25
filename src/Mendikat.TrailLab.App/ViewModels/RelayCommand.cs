using System.Windows.Input;

namespace Mendikat.TrailLab.App.ViewModels;

/// <summary>
/// Command delegating to a function that may be asynchronous. CanExecute changes are raised explicitly
/// (no CommandManager), which keeps view models testable without a dispatcher.
/// While an asynchronous execution is running the command is disabled to avoid re-entrancy.
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Func<object?, bool>? _canExecute;
    private bool _isRunning;

    public RelayCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => { execute(); return Task.CompletedTask; }, canExecute is null ? null : _ => canExecute())
    {
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !_isRunning && (_canExecute?.Invoke(parameter) ?? true);

    /// <remarks>
    /// Exceptions are not swallowed: expected errors are handled inside the view model; anything else
    /// reaches the global dispatcher handler, which logs it and keeps the application running (NFR-03).
    /// </remarks>
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        _isRunning = true;
        RaiseCanExecuteChanged();
        try
        {
            await _execute(parameter);
        }
        finally
        {
            _isRunning = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
