using System.Diagnostics;
using System.Runtime.InteropServices;
using static PoeCurrencySpammer.Services.NativeMethods;

namespace PoeCurrencySpammer.Services;

public class GlobalHotkeyService : IDisposable
{
    private IntPtr _hookId = IntPtr.Zero;
    private readonly LowLevelKeyboardProc _proc;
    private readonly Dictionary<int, List<Action>> _keyDownHandlers = new();
    private bool _disposed;

    public GlobalHotkeyService()
    {
        _proc = HookCallback;
    }

    public void Install()
    {
        if (_hookId != IntPtr.Zero) return;
        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule!;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(module.ModuleName), 0);
    }

    public void RegisterKey(int vk, Action handler)
    {
        if (!_keyDownHandlers.TryGetValue(vk, out var list))
        {
            list = [];
            _keyDownHandlers[vk] = list;
        }
        list.Add(handler);
    }

    public void UnregisterAll() => _keyDownHandlers.Clear();

    /// <summary>
    /// Async wait for a specific key press. Useful for coordinate setup flow.
    /// </summary>
    public async Task WaitForKeyAsync(int vk, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var reg = ct.Register(() => tcs.TrySetCanceled());

        void Handler()
        {
            tcs.TrySetResult();
        }

        RegisterKey(vk, Handler);
        try
        {
            await tcs.Task;
        }
        finally
        {
            if (_keyDownHandlers.TryGetValue(vk, out var list))
                list.Remove(Handler);
        }
    }

    public static bool IsKeyPressed(int vk)
    {
        return (GetAsyncKeyState(vk) & 0x8000) != 0;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (int)wParam == WM_KEYDOWN)
        {
            var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            int vk = (int)hookStruct.vkCode;

            if (_keyDownHandlers.TryGetValue(vk, out var handlers))
            {
                // Copy to avoid modification during iteration
                foreach (var handler in handlers.ToArray())
                {
                    try { handler(); }
                    catch { /* swallow to avoid crashing the hook */ }
                }
            }
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
        GC.SuppressFinalize(this);
    }
}
