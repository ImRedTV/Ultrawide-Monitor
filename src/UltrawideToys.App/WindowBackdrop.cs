using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace UltrawideToys;

internal static class WindowBackdrop
{
	public static void Apply(Window window)
	{
		try
		{
			nint handle = new WindowInteropHelper(window).Handle;
			if (handle != IntPtr.Zero)
			{
				int backdrop = 2;
				DwmSetWindowAttribute(handle, 38, ref backdrop, 4);
				int corner = 2;
				DwmSetWindowAttribute(handle, 33, ref corner, 4);
			}
		}
		catch
		{
		}
	}

	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
}

