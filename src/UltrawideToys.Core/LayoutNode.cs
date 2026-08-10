using System;
using System.Text.Json.Serialization;

namespace UltrawideToys.Core;

public sealed class LayoutNode
{
	public string Id { get; set; } = Guid.NewGuid().ToString("N");

	public string Kind { get; set; } = "leaf";

	public string? ZoneId { get; set; } = Guid.NewGuid().ToString("N");

	public SplitOrientation Orientation { get; set; } = SplitOrientation.Vertical;

	public double Ratio { get; set; } = 0.5;

	public LayoutNode? First { get; set; }

	public LayoutNode? Second { get; set; }

	[JsonIgnore]
	public bool IsLeaf => string.Equals(Kind, "leaf", StringComparison.OrdinalIgnoreCase) || (First == null && Second == null);

	public static LayoutNode Leaf(string? zoneId = null)
	{
		return new LayoutNode
		{
			Kind = "leaf",
			ZoneId = (zoneId ?? Guid.NewGuid().ToString("N"))
		};
	}

	public static LayoutNode Split(SplitOrientation orientation, double ratio, LayoutNode first, LayoutNode second)
	{
		return new LayoutNode
		{
			Kind = "split",
			Orientation = orientation,
			Ratio = Math.Clamp(ratio, 0.05, 0.95),
			First = first,
			Second = second,
			ZoneId = null
		};
	}

	public LayoutNode Clone()
	{
		return new LayoutNode
		{
			Id = Id,
			Kind = Kind,
			ZoneId = ZoneId,
			Orientation = Orientation,
			Ratio = Ratio,
			First = First?.Clone(),
			Second = Second?.Clone()
		};
	}
}

