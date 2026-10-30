using System.IO;
using Polaris.Gps.App.Resources;
using Polaris.Gps.App.Services;
using Microsoft.Extensions.Logging;

namespace Polaris.Gps.App.ViewModels;

/// <summary>
/// Communications menu: send the active document to a GPS connected by USB (Garmin, TwoNav) as a GPX file, and open
/// the tracks, routes and waypoints recorded in it.
/// </summary>
public sealed partial class MainViewModel
{
    public RelayCommand SendToGpsCommand { get; private set; } = null!;

    public RelayCommand ReceiveFromGpsCommand { get; private set; } = null!;

    /// <summary>Finds the GPS connected by USB. Replaced in tests.</summary>
    public IGpsDeviceFinder GpsDevices { get; init; } = new DriveGpsDeviceFinder();

    /// <summary>
    /// Copies the active document to the GPS as a GPX file. The document keeps its own file and stays as it was
    /// (saved or not); an existing file on the device is replaced only after confirmation.
    /// </summary>
    public async Task SendToGpsAsync()
    {
        if (ActiveDocument is not { } doc || IsBusy)
        {
            return;
        }

        var dialog = new GpsDeviceViewModel(GpsDevices, sending: true, Path.GetFileNameWithoutExtension(doc.FileName));
        if (!_dialogs.ShowToolDialog(dialog) || dialog.Destination is not { } path || dialog.Device is not { } device)
        {
            return;
        }

        if (File.Exists(path) && !_dialogs.Confirm(string.Format(Strings.GpsConfirmReplace, Path.GetFileName(path), device.Name)))
        {
            return;
        }

        try
        {
            await _store.SaveAsync(doc.Document, path, CancellationToken.None);
            StatusMessage = string.Format(Strings.GpsSent, Path.GetFileName(path), device.Name);
            _logger.LogInformation("Sent {FileName} to a {Kind} GPS", doc.FileName, device.Kind);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Sending to the GPS failed");
            _dialogs.ShowError(string.Format(Strings.GpsSendFailed, device.Name, ex.Message));
        }
    }

    /// <summary>Opens the files of the GPS the user checks, as when opening them from disk (they are converted to GPX if needed).</summary>
    public async Task ReceiveFromGpsAsync()
    {
        if (IsBusy)
        {
            return;
        }

        var dialog = new GpsDeviceViewModel(GpsDevices, sending: false);
        if (!_dialogs.ShowToolDialog(dialog) || dialog.CheckedFiles is not { Count: > 0 } files)
        {
            return;
        }

        _logger.LogInformation("Receiving {Count} files from a GPS", files.Count);
        await OpenFilesAsync(files);
    }

    private void InitializeCommunicationCommands()
    {
        SendToGpsCommand = new RelayCommand(_ => SendToGpsAsync(), _ => !IsBusy && ActiveDocument is not null);
        ReceiveFromGpsCommand = new RelayCommand(_ => ReceiveFromGpsAsync(), _ => !IsBusy);
    }

    private void RefreshCommunicationCommands()
    {
        SendToGpsCommand.RaiseCanExecuteChanged();
        ReceiveFromGpsCommand.RaiseCanExecuteChanged();
    }
}
