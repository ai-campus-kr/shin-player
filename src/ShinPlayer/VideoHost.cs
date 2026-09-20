using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace ShinPlayer;

public sealed class VideoHost : HwndHost
{
    public IntPtr WindowHandle { get; private set; }
    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        WindowHandle = CreateWindowEx(0, "static", "ShinPlayer video", 0x50000000 | 0x02000000 | 0x04000000, 0, 0, 1, 1, hwndParent.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        if (WindowHandle == IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
        return new HandleRef(this, WindowHandle);
    }
    protected override void DestroyWindowCore(HandleRef hwnd) { DestroyWindow(hwnd.Handle); WindowHandle = IntPtr.Zero; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateWindowEx(int exStyle, string cls, string title, int style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr handle);
}
