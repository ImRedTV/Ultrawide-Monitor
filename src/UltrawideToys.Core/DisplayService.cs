using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace UltrawideToys.Core;

public static class DisplayService
{
	private static class Native
	{
		public struct RECT
		{
			public int Left;

			public int Top;

			public int Right;

			public int Bottom;
		}

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		public struct MONITORINFOEX
		{
			public int cbSize;

			public RECT rcMonitor;

			public RECT rcWork;

			public uint dwFlags;

			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
			public string szDevice;
		}

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		public struct DISPLAY_DEVICE
		{
			public int cb;

			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
			public string DeviceName;

			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
			public string DeviceString;

			public int StateFlags;

			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
			public string DeviceID;

			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
			public string DeviceKey;
		}

		public delegate bool MonitorEnumProc(nint hMonitor, nint hdc, nint rect, nint data);

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		public static extern bool EnumDisplayMonitors(nint hdc, nint clip, MonitorEnumProc callback, nint data);

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		public static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFOEX info);

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		public static extern bool EnumDisplayDevices(string? device, uint index, ref DISPLAY_DEVICE display, uint flags);

		[DllImport("Shcore.dll")]
		public static extern int GetDpiForMonitor(nint hmonitor, int dpiType, out uint dpiX, out uint dpiY);
	}

	private const int MDT_EFFECTIVE_DPI = 0;

	private const int MonitorDefaultToNull = 0;

	public static IReadOnlyList<MonitorProfile> Enumerate()
	{
		List<MonitorProfile> result = new List<MonitorProfile>();
		Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, delegate(nint handle, nint _, nint _, nint _)
		{
			Native.MONITORINFOEX info = new Native.MONITORINFOEX
			{
				cbSize = Marshal.SizeOf<Native.MONITORINFOEX>()
			};
			if (!Native.GetMonitorInfo(handle, ref info))
			{
				return true;
			}
			string text = info.szDevice ?? string.Empty;
			string text2 = text;
			int value = result.Count + 1;
			try
			{
				Native.DISPLAY_DEVICE display = new Native.DISPLAY_DEVICE
				{
					cb = Marshal.SizeOf<Native.DISPLAY_DEVICE>()
				};
				if (Native.EnumDisplayDevices(text, 0u, ref display, 0u) && !string.IsNullOrWhiteSpace(display.DeviceString))
				{
					text2 = display.DeviceString.Trim();
				}
				if (string.IsNullOrWhiteSpace(text2))
				{
					text2 = $"Écran {value}";
				}
			}
			catch
			{
			}
			int dpi = 96;
			try
			{
				if (Native.GetDpiForMonitor(handle, 0, out var dpiX, out var _) == 0)
				{
					dpi = (int)dpiX;
				}
			}
			catch
			{
			}
			RectModel bounds = RectModel.From(info.rcMonitor.Left, info.rcMonitor.Top, info.rcMonitor.Right - info.rcMonitor.Left, info.rcMonitor.Bottom - info.rcMonitor.Top);
			RectModel workArea = RectModel.From(info.rcWork.Left, info.rcWork.Top, info.rcWork.Right - info.rcWork.Left, info.rcWork.Bottom - info.rcWork.Top);
			DisplayIdentity displayIdentity = DisplayTopologyInspector.TryGetTargetIdentity(bounds);
			if (!string.IsNullOrWhiteSpace(displayIdentity?.FriendlyName))
			{
				text2 = displayIdentity.FriendlyName;
			}
			string id = StableId(text, text2, bounds, displayIdentity);
			result.Add(new MonitorProfile
			{
				Id = id,
				DeviceName = text,
				FriendlyName = text2,
				IsPrimary = ((info.dwFlags & 1) != 0),
				Bounds = bounds,
				WorkArea = workArea,
				Dpi = dpi
			});
			return true;
		}, IntPtr.Zero);
		return result;
	}

	public static MonitorProfile? FindForPoint(IEnumerable<MonitorProfile> monitors, int x, int y)
	{
		return monitors.FirstOrDefault((MonitorProfile m) => m.Bounds.Contains(x, y)) ?? monitors.FirstOrDefault((MonitorProfile m) => m.IsPrimary) ?? monitors.FirstOrDefault();
	}

	public static MonitorProfile? FindForWindow(IEnumerable<MonitorProfile> monitors, RectModel rect)
	{
		int centerX = rect.X + rect.Width / 2;
		int centerY = rect.Y + rect.Height / 2;
		MonitorProfile center = FindForPoint(monitors, centerX, centerY);
		if (center != null)
		{
			return center;
		}
		return monitors.OrderByDescending((MonitorProfile m) => IntersectionArea(m.WorkArea, rect)).FirstOrDefault();
	}

	private static long IntersectionArea(RectModel a, RectModel b)
	{
		int width = Math.Max(0, Math.Min(a.Right, b.Right) - Math.Max(a.X, b.X));
		int height = Math.Max(0, Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Y, b.Y));
		return (long)width * (long)height;
	}

	private static string StableId(string device, string friendly, RectModel bounds, DisplayIdentity? topology)
	{
		string identity = (((object)topology == null) ? $"{device}|{friendly}|{bounds.Width}x{bounds.Height}" : $"{topology.DevicePath}|{topology.EdidManufacturerId:X4}|{topology.EdidProductCodeId:X4}|{topology.ConnectorInstance}|{device}");
		byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
		return "display-" + Convert.ToHexString(bytes).Substring(0, 16).ToLowerInvariant();
	}
}

