namespace UltrawideToys.Core;

public sealed class SplitRect
{
	public string NodeId { get; init; } = string.Empty;

	public SplitOrientation Orientation { get; init; }

	public double Ratio { get; init; }

	public RectModel Bounds { get; init; } = new RectModel();

	public int Position { get; init; }
}

