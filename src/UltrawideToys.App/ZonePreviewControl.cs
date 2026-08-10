using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using UltrawideToys.Core;

namespace UltrawideToys;

public sealed class ZonePreviewControl : FrameworkElement
{
	private readonly MonitorProfile _monitor;

	public ZonePreviewControl(MonitorProfile monitor)
	{
		_monitor = monitor;
	}

	protected override void OnRender(DrawingContext dc)
	{
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0265: Unknown result type (might be due to invalid IL or missing references)
		base.OnRender(dc);
		SolidColorBrush background = new SolidColorBrush(Color.FromArgb(28, 0, 120, 212));
		dc.DrawRoundedRectangle(background, new Pen(new SolidColorBrush(Color.FromArgb(90, 0, 120, 212)), 1.0), new Rect(0.0, 0.0, base.ActualWidth, base.ActualHeight), 8.0, 8.0);
		RectModel source = ((_monitor.WorkArea.Width > 0 && _monitor.WorkArea.Height > 0) ? _monitor.WorkArea : _monitor.Bounds);
		IReadOnlyList<ZoneRect> zones = LayoutEngine.Calculate(_monitor.ActiveLayout.Root, RectModel.From(0, 0, Math.Max(1, source.Width), Math.Max(1, source.Height)));
		double scale = Math.Min(base.ActualWidth / (double)Math.Max(1, source.Width), base.ActualHeight / (double)Math.Max(1, source.Height));
		double left = (base.ActualWidth - (double)source.Width * scale) / 2.0;
		double top = (base.ActualHeight - (double)source.Height * scale) / 2.0;
		Color[] colors = new Color[4]
		{
			Color.FromArgb(70, 0, 120, 212),
			Color.FromArgb(45, 0, 120, 212),
			Color.FromArgb(55, 80, 160, 220),
			Color.FromArgb(40, 20, 80, 180)
		};
		Rect rect = default(Rect);
		for (int i = 0; i < zones.Count; i++)
		{
			RectModel z = zones[i].Rect;
			rect = new Rect(left + (double)z.X * scale, top + (double)z.Y * scale, Math.Max(1.0, (double)z.Width * scale), Math.Max(1.0, (double)z.Height * scale));
			dc.DrawRectangle(new SolidColorBrush(colors[i % colors.Length]), new Pen(new SolidColorBrush(Color.FromArgb(175, 203, 231, 250)), 1.0), rect);
		}
	}
}
