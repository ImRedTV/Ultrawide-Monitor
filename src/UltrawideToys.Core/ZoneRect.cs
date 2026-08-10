namespace UltrawideToys.Core;

public sealed class ZoneRect
{
	public string ZoneId { get; init; } = string.Empty;

	public RectModel Rect { get; init; } = new RectModel();

	public string? ParentSplitId { get; init; }
}

