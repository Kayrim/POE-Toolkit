using CommunityToolkit.Mvvm.ComponentModel;

namespace PoeCurrencySpammer.Models;

public partial class SelectedMod : ObservableObject
{
    public StatEntry Entry { get; }
    public bool HasRoll { get; }

    [ObservableProperty] private string _min = "";
    [ObservableProperty] private string _max = "";

    public int MinValue => int.TryParse(Min, out int v) ? v : 0;
    public int MaxValue => int.TryParse(Max, out int v) ? v : 0;

    public SelectedMod(StatEntry entry)
    {
        Entry = entry;
        HasRoll = entry.Text.Contains('#');
    }
}
