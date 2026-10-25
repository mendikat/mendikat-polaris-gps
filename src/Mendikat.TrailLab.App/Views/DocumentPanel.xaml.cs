using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Mendikat.TrailLab.App.ViewModels;

namespace Mendikat.TrailLab.App.Views;

/// <summary>
/// Layers, points and point detail of one document. Code-behind only keeps the multi-selection of the
/// points list in sync with <see cref="DocumentViewModel.SelectedRows"/> (WPF cannot bind SelectedItems).
/// </summary>
public partial class DocumentPanel : UserControl
{
    private DocumentViewModel? _viewModel;
    private bool _syncing;

    public DocumentPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as DocumentViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            // The tab content is reused for every document: restore this document's selection once the list is rebound.
            Dispatcher.BeginInvoke(SyncGridFromViewModel);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DocumentViewModel.SelectedRows) or nameof(DocumentViewModel.Rows))
        {
            Dispatcher.BeginInvoke(SyncGridFromViewModel);
        }
    }

    /// <summary>Only selections made by the user in the list reach the view model; changes caused by rebinding are ignored.</summary>
    private void OnGridSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _viewModel is null || !PointsGrid.IsKeyboardFocusWithin)
        {
            return;
        }

        _viewModel.SetSelectedRows(PointsGrid.SelectedItems.Cast<PointRowViewModel>());
    }

    private void SyncGridFromViewModel()
    {
        if (_viewModel is null || !ReferenceEquals(PointsGrid.ItemsSource, _viewModel.Rows))
        {
            return;
        }

        var wanted = _viewModel.SelectedRows;
        if (wanted.Count == PointsGrid.SelectedItems.Count && wanted.All(PointsGrid.SelectedItems.Contains))
        {
            return;
        }

        _syncing = true;
        try
        {
            PointsGrid.SelectedItems.Clear();
            foreach (var row in wanted)
            {
                PointsGrid.SelectedItems.Add(row);
            }

            if (wanted.Count > 0)
            {
                PointsGrid.ScrollIntoView(wanted[0]);
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>The context menu is outside the visual tree: give it the window's view model for its commands.</summary>
    private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (PointsGrid.ContextMenu is { } menu)
        {
            menu.DataContext = Window.GetWindow(this)?.DataContext;
        }
    }
}
