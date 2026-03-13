using System.Drawing;
using System.Runtime.InteropServices;
using static PoeCurrencySpammer.Services.NativeMethods;

namespace PoeCurrencySpammer.Services;

public class InputSimulatorService
{
    private static readonly int InputSize = Marshal.SizeOf<INPUT>();

    public Point GetCursorPosition()
    {
        GetCursorPos(out var pt);
        return new Point(pt.X, pt.Y);
    }

    public void MoveTo(int x, int y, double durationSeconds = 0.01)
    {
        if (durationSeconds <= 0.015)
        {
            SetCursorPos(x, y);
            return;
        }

        GetCursorPos(out var start);
        int steps = Math.Max(2, (int)(durationSeconds / 0.005));
        for (int i = 1; i <= steps; i++)
        {
            double t = (double)i / steps;
            int cx = start.X + (int)((x - start.X) * t);
            int cy = start.Y + (int)((y - start.Y) * t);
            SetCursorPos(cx, cy);
            Thread.Sleep(1);
        }
    }

    public void Click(string button = "left", double clickDuration = 0.035)
    {
        uint downFlag = button == "right" ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_LEFTDOWN;
        uint upFlag = button == "right" ? MOUSEEVENTF_RIGHTUP : MOUSEEVENTF_LEFTUP;

        SendMouseEvent(downFlag);
        Thread.Sleep((int)(clickDuration * 1000));
        SendMouseEvent(upFlag);
    }

    public void FastClick(Point coords, string button = "left",
        double moveDuration = 0.01, double waitAfter = 0.02)
    {
        MoveTo(coords.X, coords.Y, moveDuration);
        Click(button, 0.035);
        if (waitAfter > 0)
            Thread.Sleep((int)(waitAfter * 1000));
    }

    public void KeyDown(int vk)
    {
        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new INPUTUNION
            {
                ki = new KEYBDINPUT { wVk = (ushort)vk }
            }
        };
        SendInput(1, [input], InputSize);
    }

    public void KeyUp(int vk)
    {
        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new INPUTUNION
            {
                ki = new KEYBDINPUT
                {
                    wVk = (ushort)vk,
                    dwFlags = KEYEVENTF_KEYUP
                }
            }
        };
        SendInput(1, [input], InputSize);
    }

    public void KeyPress(int vk)
    {
        KeyDown(vk);
        Thread.Sleep(5);
        KeyUp(vk);
    }

    public void ReleaseShift()
    {
        KeyUp(VK_SHIFT);
    }

    /// <summary>
    /// Release all modifier keys (Ctrl, Alt, Shift) to ensure clean state before clicking.
    /// </summary>
    public void ReleaseModifiers()
    {
        KeyUp(VK_CONTROL);
        KeyUp(VK_MENU);
        KeyUp(VK_SHIFT);
    }

    private static void SendMouseEvent(uint flags)
    {
        var input = new INPUT
        {
            type = INPUT_MOUSE,
            u = new INPUTUNION
            {
                mi = new MOUSEINPUT { dwFlags = flags }
            }
        };
        SendInput(1, [input], InputSize);
    }
}
