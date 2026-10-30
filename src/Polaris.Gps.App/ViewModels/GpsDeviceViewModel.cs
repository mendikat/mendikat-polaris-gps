using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using Polaris.Gps.App.Resources;
using Polaris.Gps.App.Services;

namespace Polaris.Gps.App.ViewModels;

/// <summary>A file of the GPS in the receive list; checked files are opened.</summary>
public sealed class GpsFileItem(string path, string name, string details) : ObservableObject
{
    private bool _isChecked;

    public string Path { get; } = path;

    public string Name { get; } = name;

    /// <summary>"Current · 12/10/2026 18:04 · 245 KB".</summary>
    public string Details { get; } = details;

    public bool IsChecked { get => _isChecked; set => SetProperty(ref _isChecked, value); }
}

/// <summary>
/// Communications › Send to GPS / Receive from GPS: the GPS connected by USB, and either the name of the GPX file to
/// copy to it or its files to open. Nothing is written or opened until the user accepts.
/// </summary>
public sealed class GpsDeviceViewModel : ObservableObject
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");
    private readonly IGpsDeviceFinder _finder;
    private IReadOnlyList<GpsDevice> _devices = [];
    private GpsDevice? _device;
    private string _fileName;
    private string? _error;

    /// <param name="sending">True to send <paramref name="fileName"/>; false to receive files.</param>
    public GpsDeviceViewModel(IGpsDeviceFinder finder, bool sending, string fileName = "")
    {
        _finder = finder;
        IsSending = sending;
        _fileName = fileName;
        RefreshCommand = new RelayCommand(Refresh);
        Refresh();
    }

    public bool IsSending { get; }

    public bool IsReceiving => !IsSending;

    public string Title => IsSending ? Strings.GpsSendTitle : Strings.GpsReceiveTitle;

    public string AcceptText => IsSending ? Strings.GpsSend : Strings.GpsOpen;

    public RelayCommand RefreshCommand { get; }

    public IReadOnlyList<GpsDevice> Devices
    {
        get => _devices;
        private set
        {
            SetProperty(ref _devices, value);
            OnPropertyChanged(nameof(HasNoDevice));
        }
    }

    public bool HasNoDevice => Devices.Count == 0;

    public GpsDevice? Device
    {
        get => _device;
        set
        {
            if (SetProperty(ref _device, value))
            {
                LoadFiles();
                OnPropertyChanged(nameof(Destination));
                OnPropertyChanged(nameof(CanAccept));
            }
        }
    }

    /// <summary>Receive: the files of the device, newest first.</summary>
    public ObservableCollection<GpsFileItem> Files { get; } = [];

    /// <summary>Send: name of the GPX file on the device.</summary>
    public string FileName
    {
        get => _fileName;
        set
        {
            if (SetProperty(ref _fileName, value ?? string.Empty))
            {
                Error = null;
                OnPropertyChanged(nameof(Destination));
                OnPropertyChanged(nameof(CanAccept));
            }
        }
    }

    /// <summary>Send: full path of the file on the device, or null while the name is not valid.</summary>
    public string? Destination
    {
        get
        {
            var name = FileName.Trim();
            if (Device is null || name.Length == 0 || name.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
            {
                return null;
            }

            if (!name.EndsWith(".gpx", StringComparison.OrdinalIgnoreCase))
            {
                name += ".gpx";
            }

            return System.IO.Path.Combine(Device.SendFolder, name);
        }
    }

    public string? Error
    {
        get => _error;
        set => SetProperty(ref _error, value);
    }

    /// <summary>Receive: the checked files.</summary>
    public IReadOnlyList<string> CheckedFiles => Files.Where(f => f.IsChecked).Select(f => f.Path).ToList();

    public bool CanAccept => IsSending ? Destination is not null : Device is not null;

    /// <summary>Looks for connected GPS again (after plugging one in), keeping the chosen one if it is still there.</summary>
    public void Refresh()
    {
        var previous = Device?.Root;
        Devices = _finder.Find();
        Device = Devices.FirstOrDefault(d => d.Root == previous) ?? Devices.FirstOrDefault();
        LoadFiles();
    }

    private void LoadFiles()
    {
        Files.Clear();
        if (IsSending || Device is null)
        {
            return;
        }

        try
        {
            foreach (var file in DriveGpsDeviceFinder.Files(Device))
            {
                var folder = System.IO.Path.GetFileName(file.DirectoryName) ?? string.Empty;
                var details = $"{folder} · {file.LastWriteTime.ToString("dd/MM/yyyy HH:mm", Spanish)} · {Math.Max(1, file.Length / 1024).ToString("N0", Spanish)} KB";
                Files.Add(new GpsFileItem(file.FullName, file.Name, details));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Error = string.Format(Strings.GpsReadFailed, ex.Message);
        }
    }
}
