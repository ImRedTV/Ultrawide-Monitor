using System.Drawing;
using System.Drawing.Drawing2D;

namespace UltrawideToys;

internal static class GraphicsExtensions
{
	public static void FillRoundedRectangle(this Graphics graphics, Brush brush, float x, float y, float width, float height, float radius)
	{
		using GraphicsPath path = RoundedPath(x, y, width, height, radius);
		graphics.FillPath(brush, path);
	}

	public static void DrawRoundedRectangle(this Graphics graphics, Pen pen, float x, float y, float width, float height, float radius)
	{
		using GraphicsPath path = RoundedPath(x, y, width, height, radius);
		graphics.DrawPath(pen, path);
	}

	private static GraphicsPath RoundedPath(float x, float y, float width, float height, float radius)
	{
		GraphicsPath path = new GraphicsPath();
		float d = radius * 2f;
		path.AddArc(x, y, d, d, 180f, 90f);
		path.AddArc(x + width - d, y, d, d, 270f, 90f);
		path.AddArc(x + width - d, y + height - d, d, d, 0f, 90f);
		path.AddArc(x, y + height - d, d, d, 90f, 90f);
		path.CloseFigure();
		return path;
	}
}

