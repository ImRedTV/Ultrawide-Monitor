using System.Text.Json.Serialization;

namespace UltrawideToys.Core;

public sealed class RectModel
{
	public int X { get; set; }

	public int Y { get; set; }

	public int Width { get; set; }

	public int Height { get; set; }

	[JsonIgnore]
	public int Right => X + Width;

	[JsonIgnore]
	public int Bottom => Y + Height;

	public RectModel Clone()
	{
		return new RectModel
		{
			X = X,
			Y = Y,
			Width = Width,
			Height = Height
		};
	}

	public bool Contains(int x, int y)
	{
		return x >= X && x < Right && y >= Y && y < Bottom;
	}

	public static RectModel From(int x, int y, int width, int height)
	{
		return new RectModel
		{
			X = x,
			Y = y,
			Width = width,
			Height = height
		};
	}
}

