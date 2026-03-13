using System.Drawing;
using PoeCurrencySpammer.Models;
using PoeCurrencySpammer.Services;

namespace PoeCurrencySpammer.Automation.Strategies;

public class AlchScourStrategy : ICurrencyStrategy
{
    private readonly InputSimulatorService _input;
    private readonly ClipboardService _clipboard;
    private readonly ItemParserService _parser;
    private readonly AppConfig _config;

    public string ModeName => "Alch/Scour";
    public string[] CoordinateLabels => ["ALCHEMY ORB", "SCOURING ORB", "ITEM"];

    public AlchScourStrategy(InputSimulatorService input, ClipboardService clipboard,
        ItemParserService parser, AppConfig config)
    {
        _input = input;
        _clipboard = clipboard;
        _parser = parser;
        _config = config;
    }

    public MatchResult ExecuteIteration(Point[] coords, CancellationToken ct)
    {
        var alchemy = coords[0];
        var scour = coords[1];
        var item = coords[2];

        _input.ReleaseModifiers();

        // Step 1: Scour
        _input.FastClick(scour, "right", waitAfter: 0.06);
        _input.FastClick(item, "left", waitAfter: 0.07);

        if (ct.IsCancellationRequested)
            return new MatchResult(false, string.Empty);

        // Step 2: Alchemy
        _input.FastClick(alchemy, "right", waitAfter: 0.06);
        _input.FastClick(item, "left", waitAfter: 0.08);

        if (ct.IsCancellationRequested)
            return new MatchResult(false, string.Empty);

        // Step 3: Check — must reliably read before allowing next cycle
        var itemText = _clipboard.CopyItemTextReliable(ct);
        if (!string.IsNullOrEmpty(itemText))
            return _parser.CheckModQuality(itemText, _config.SearchRegex);

        return new MatchResult(false, string.Empty);
    }
}
