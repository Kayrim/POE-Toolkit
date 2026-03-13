using System.Drawing;
using PoeCurrencySpammer.Models;
using PoeCurrencySpammer.Services;

namespace PoeCurrencySpammer.Automation.Strategies;

public class AlterationStrategy : ICurrencyStrategy
{
    private readonly InputSimulatorService _input;
    private readonly ClipboardService _clipboard;
    private readonly ItemParserService _parser;
    private readonly AppConfig _config;

    public string ModeName => "Alteration";

    public string[] CoordinateLabels => _config.AltAugMode is "Prefix" or "Suffix"
        ? ["ALTERATION ORB", "AUGMENTATION ORB", "ITEM"]
        : ["ALTERATION ORB", "ITEM"];

    public AlterationStrategy(InputSimulatorService input, ClipboardService clipboard,
        ItemParserService parser, AppConfig config)
    {
        _input = input;
        _clipboard = clipboard;
        _parser = parser;
        _config = config;
    }

    public MatchResult ExecuteIteration(Point[] coords, CancellationToken ct)
    {
        bool useAug = _config.AltAugMode is "Prefix" or "Suffix";
        var alteration = coords[0];
        var augmentation = useAug ? coords[1] : default;
        var item = useAug ? coords[2] : coords[1];

        int delay = (int)((_config.DelayAfterClick > 0 ? _config.DelayAfterClick : 0.05) * 1000);

        // Ensure clean modifier state before clicking
        _input.ReleaseModifiers();

        // Step 1: Apply Alteration
        _input.FastClick(alteration, "right");
        _input.FastClick(item, "left");
        Thread.Sleep(delay);

        if (ct.IsCancellationRequested)
            return new MatchResult(false, string.Empty);

        // Step 2: Read item
        var itemText = _clipboard.CopyItemTextReliable(ct);
        if (string.IsNullOrEmpty(itemText))
            return new MatchResult(false, string.Empty);

        // Step 3: Check match
        var result = _parser.CheckItem(itemText, _config.SearchRegex, _config.SelectedMods);
        if (result.IsMatch)
            return result;

        // Step 4: If aug mode active and item qualifies, apply Augmentation
        if (useAug && _parser.ShouldAugment(itemText, _config.AltAugMode))
        {
            _input.ReleaseModifiers();
            _input.FastClick(augmentation, "right");
            _input.FastClick(item, "left");
            Thread.Sleep(delay);

            if (ct.IsCancellationRequested)
                return new MatchResult(false, string.Empty);

            // Read augmented item
            var augText = _clipboard.CopyItemTextReliable(ct);
            if (!string.IsNullOrEmpty(augText))
                return _parser.CheckItem(augText, _config.SearchRegex, _config.SelectedMods);
        }

        return new MatchResult(false, string.Empty);
    }
}
