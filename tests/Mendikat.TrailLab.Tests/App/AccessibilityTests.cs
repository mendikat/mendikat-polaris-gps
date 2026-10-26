using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Mendikat.TrailLab.App;
using Mendikat.TrailLab.App.Map;
using Mendikat.TrailLab.App.ViewModels;
using Mendikat.TrailLab.App.Views;
using Mendikat.TrailLab.Domain.Documents;
using Mendikat.TrailLab.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mendikat.TrailLab.Tests.App;

/// <summary>
/// Basic accessibility (NFR-08): every input has a name a screen reader can announce, and the access keys
/// (Alt + underlined letter) of each menu are unique, so every menu command can be reached with the keyboard.
/// </summary>
public sealed class AccessibilityTests
{
    [Fact]
    [Trait("Req", "NFR-08")]
    public void Every_input_control_has_an_accessible_name()
    {
        WindowSmokeTests.OnStaThread(() =>
        {
            var missing = new List<string>();
            var checkedControls = 0;
            foreach (var window in AllWindows())
            {
                foreach (var control in LogicalDescendants(window).OfType<Control>().Where(NeedsName))
                {
                    checkedControls++;
                    if (string.IsNullOrWhiteSpace(AutomationProperties.GetName(control)) && AutomationProperties.GetLabeledBy(control) is null)
                    {
                        missing.Add($"{window.GetType().Name}: {control.GetType().Name} {control.Name}");
                    }
                }

                window.Close();
            }

            Assert.Empty(missing);
            Assert.True(checkedControls >= 15, $"only {checkedControls} controls were checked");
        });
    }

    [Fact]
    [Trait("Req", "NFR-08")]
    public void Every_menu_item_has_an_access_key_unique_within_its_menu()
    {
        WindowSmokeTests.OnStaThread(() =>
        {
            var window = WindowSmokeTests.CreateMainWindow();
            var menu = LogicalDescendants(window).OfType<Menu>().Single();
            var problems = new List<string>();
            CheckAccessKeys(menu.Items.OfType<MenuItem>().ToList(), "Menú", problems);
            foreach (var item in LogicalDescendants(menu).OfType<MenuItem>().Where(i => i.HasItems))
            {
                CheckAccessKeys(item.Items.OfType<MenuItem>().ToList(), (string)item.Header, problems);
            }

            window.Close();
            Assert.Empty(problems);
        });
    }

    private static void CheckAccessKeys(IReadOnlyList<MenuItem> items, string menu, List<string> problems)
    {
        var keys = new Dictionary<char, string>();
        foreach (var header in items.Select(i => (string)i.Header))
        {
            var index = header.IndexOf('_');
            if (index < 0 || index == header.Length - 1)
            {
                problems.Add($"{menu}: «{header}» has no access key");
                continue;
            }

            var key = char.ToUpperInvariant(header[index + 1]);
            if (!keys.TryAdd(key, header))
            {
                problems.Add($"{menu}: «{header}» and «{keys[key]}» share the access key {key}");
            }
        }
    }

    /// <summary>
    /// Controls whose meaning is not given by their own text: inputs, lists, and buttons showing a symbol
    /// ("+", "⤢"…) or no text at all.
    /// </summary>
    private static bool NeedsName(Control control) =>
        control is TextBox or ComboBox or DatePicker or RangeBase or ListBox or DataGrid
        || (control is ButtonBase and not CheckBox and not RadioButton && control is not ContentControl { Content: string { Length: > 2 } })
        || (control is CheckBox { Content: null });

    private static IEnumerable<Window> AllWindows()
    {
        var layer = WindowSmokeTests.TrackLayer();
        var document = new DocumentViewModel(new TrailDocument(@"C:\data\p.gpx", DocumentFormat.Gpx));
        yield return WindowSmokeTests.CreateMainWindow();
        yield return new Window { Content = new DocumentPanel { DataContext = new DocumentViewModel(new TrailDocument(@"C:\data\d.gpx", DocumentFormat.Gpx)) } };
        yield return new SimplifyWindow(new SimplifyViewModel(layer));
        yield return new TimesWindow(new TimesViewModel(layer));
        yield return new DemWindow(new DemViewModel(layer, _ => new WindowSmokeTests.NoDataSource()));
        yield return new ProfileWindow(new ProfileViewModel(document, layer));
        yield return new AboutWindow(new AboutViewModel(), _ => { });
    }

    private static IEnumerable<DependencyObject> LogicalDescendants(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            yield return child;
            foreach (var descendant in LogicalDescendants(child))
            {
                yield return descendant;
            }
        }
    }
}
