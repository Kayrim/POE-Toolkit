using System.Windows;
using static PoeCurrencySpammer.Services.NativeMethods;

namespace PoeCurrencySpammer.Services;

public class ClipboardService
{
    private readonly InputSimulatorService _input;

    public ClipboardService(InputSimulatorService input)
    {
        _input = input;
    }

    /// <summary>
    /// Sends Ctrl+Alt+C (PoE advanced copy) and reads clipboard.
    /// Must be called from a background thread — dispatches to STA for clipboard access.
    /// </summary>
    public string CopyItemText(CancellationToken ct)
    {
        // Clear clipboard
        try { RunOnSta(() => Clipboard.SetText(" ")); }
        catch { /* clipboard locked — proceed anyway */ }

        // Send Ctrl+Alt+C (with 5ms pauses like pyautogui.PAUSE)
        // Wrapped in try/finally to guarantee modifier keys are ALWAYS released
        try
        {
            _input.KeyDown(VK_CONTROL);
            Thread.Sleep(5);
            _input.KeyDown(VK_MENU);
            Thread.Sleep(5);
            _input.KeyPress(VK_C);
        }
        finally
        {
            Thread.Sleep(5);
            _input.KeyUp(VK_MENU);
            Thread.Sleep(5);
            _input.KeyUp(VK_CONTROL);
        }

        // Poll for clipboard content
        for (int i = 0; i < 6; i++)
        {
            if (ct.IsCancellationRequested)
                return string.Empty;

            Thread.Sleep(15);

            try
            {
                string text = RunOnSta(() => Clipboard.GetText()) ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(text) && text.Trim() != string.Empty)
                    return text;
            }
            catch
            {
                // Clipboard locked — try next poll
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Reliable version: retries the full copy sequence (re-sends Ctrl+Alt+C)
    /// until it gets item text or cancellation is requested.
    /// Use this after applying currency to guarantee we check the result
    /// before allowing the next currency application.
    /// </summary>
    public string CopyItemTextReliable(CancellationToken ct, int maxAttempts = 10)
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (ct.IsCancellationRequested)
                return string.Empty;

            string result = CopyItemText(ct);
            if (!string.IsNullOrEmpty(result))
                return result;

            Thread.Sleep(20);
        }

        return string.Empty;
    }

    private static void RunOnSta(Action action)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            action();
            return;
        }

        Exception? caught = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { caught = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (caught is not null) throw caught;
    }

    private static T RunOnSta<T>(Func<T> func)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            return func();

        T result = default!;
        Exception? caught = null;
        var thread = new Thread(() =>
        {
            try { result = func(); }
            catch (Exception ex) { caught = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (caught is not null) throw caught;
        return result;
    }
}
