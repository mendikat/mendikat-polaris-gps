using Mendikat.TrailLab.App.Settings;

namespace Mendikat.TrailLab.Tests.Fakes;

internal sealed class FakeSettingsStore : ISettingsStore
{
    public UserSettings Current { get; set; } = new();

    public bool FailSaving { get; set; }

    public int SaveCount { get; private set; }

    public UserSettings Load() => Current;

    public bool Save(UserSettings settings)
    {
        if (FailSaving)
        {
            return false;
        }

        SaveCount++;
        Current = settings;
        return true;
    }
}
