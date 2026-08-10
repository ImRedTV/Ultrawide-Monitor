using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace UltrawideToys;

internal static class IconFactory
{
	private sealed class SafeIconHandle : SafeHandleZeroOrMinusOneIsInvalid
	{
		public SafeIconHandle(nint handle)
			: base(ownsHandle: true)
		{
			SetHandle(handle);
		}

		protected override bool ReleaseHandle()
		{
			return DestroyIcon(handle);
		}

		[DllImport("user32.dll")]
		private static extern bool DestroyIcon(nint handle);
	}

	public static Icon Create()
	{
		try
		{
			string processPath = Environment.ProcessPath;
			if (!string.IsNullOrWhiteSpace(processPath))
			{
				using Icon executableIcon = Icon.ExtractAssociatedIcon(processPath);
				if (executableIcon != null && executableIcon.Clone() is Icon clone)
				{
					return clone;
				}
			}
		}
		catch
		{
		}
		return CreateFallback();
	}

	private static Icon CreateFallback()
	{
		using Bitmap bitmap = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
		using Graphics graphics = Graphics.FromImage(bitmap);
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.Clear(Color.Transparent);
		using SolidBrush background = new SolidBrush(Color.FromArgb(0, 120, 212));
		using Pen white = new Pen(Color.White, 2.2f);
		graphics.FillRoundedRectangle(background, 2f, 4f, 28f, 24f, 5f);
		graphics.DrawRoundedRectangle(white, 5f, 7f, 22f, 18f, 3f);
		graphics.DrawLine(white, 12, 7, 12, 25);
		graphics.DrawLine(white, 20, 7, 20, 25);
		using SafeIconHandle handle = new SafeIconHandle(bitmap.GetHicon());
		return (Icon.FromHandle(handle.DangerousGetHandle()).Clone() as Icon) ?? SystemIcons.Application;
	}
}

