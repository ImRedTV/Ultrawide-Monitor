using System;
using System.Collections.Generic;
using System.Linq;

namespace UltrawideToys.Core;

public static class LayoutEngine
{
	public const int MaxZones = 16;

	public const int MinimumWidth = 160;

	public const int MinimumHeight = 120;

	public static IReadOnlyList<ZoneRect> Calculate(LayoutNode root, RectModel area)
	{
		List<ZoneRect> result = new List<ZoneRect>();
		Visit(root, area, null, result);
		return result;
	}

	public static IReadOnlyList<SplitRect> CalculateSplits(LayoutNode root, RectModel area)
	{
		List<SplitRect> result = new List<SplitRect>();
		VisitSplits(root, area, result);
		return result;
	}

	private static void Visit(LayoutNode node, RectModel rect, string? parentId, ICollection<ZoneRect> result)
	{
		if (node.IsLeaf || node.First == null || node.Second == null)
		{
			result.Add(new ZoneRect
			{
				ZoneId = (node.ZoneId ?? node.Id),
				Rect = rect.Clone(),
				ParentSplitId = parentId
			});
			return;
		}
		double ratio = Math.Clamp(node.Ratio, 0.05, 0.95);
		if (node.Orientation == SplitOrientation.Vertical)
		{
			int firstWidth = Math.Clamp((int)Math.Round((double)rect.Width * ratio), 1, Math.Max(1, rect.Width - 1));
			Visit(node.First, RectModel.From(rect.X, rect.Y, firstWidth, rect.Height), node.Id, result);
			Visit(node.Second, RectModel.From(rect.X + firstWidth, rect.Y, rect.Width - firstWidth, rect.Height), node.Id, result);
		}
		else
		{
			int firstHeight = Math.Clamp((int)Math.Round((double)rect.Height * ratio), 1, Math.Max(1, rect.Height - 1));
			Visit(node.First, RectModel.From(rect.X, rect.Y, rect.Width, firstHeight), node.Id, result);
			Visit(node.Second, RectModel.From(rect.X, rect.Y + firstHeight, rect.Width, rect.Height - firstHeight), node.Id, result);
		}
	}

	private static void VisitSplits(LayoutNode node, RectModel rect, ICollection<SplitRect> result)
	{
		if (!node.IsLeaf && node.First != null && node.Second != null)
		{
			double ratio = Math.Clamp(node.Ratio, 0.05, 0.95);
			if (node.Orientation == SplitOrientation.Vertical)
			{
				int firstWidth = Math.Clamp((int)Math.Round((double)rect.Width * ratio), 1, Math.Max(1, rect.Width - 1));
				result.Add(new SplitRect
				{
					NodeId = node.Id,
					Orientation = node.Orientation,
					Ratio = ratio,
					Bounds = rect.Clone(),
					Position = rect.X + firstWidth
				});
				VisitSplits(node.First, RectModel.From(rect.X, rect.Y, firstWidth, rect.Height), result);
				VisitSplits(node.Second, RectModel.From(rect.X + firstWidth, rect.Y, rect.Width - firstWidth, rect.Height), result);
			}
			else
			{
				int firstHeight = Math.Clamp((int)Math.Round((double)rect.Height * ratio), 1, Math.Max(1, rect.Height - 1));
				result.Add(new SplitRect
				{
					NodeId = node.Id,
					Orientation = node.Orientation,
					Ratio = ratio,
					Bounds = rect.Clone(),
					Position = rect.Y + firstHeight
				});
				VisitSplits(node.First, RectModel.From(rect.X, rect.Y, rect.Width, firstHeight), result);
				VisitSplits(node.Second, RectModel.From(rect.X, rect.Y + firstHeight, rect.Width, rect.Height - firstHeight), result);
			}
		}
	}

	public static LayoutNode SplitLeaf(LayoutNode root, string zoneId, SplitOrientation orientation)
	{
		if (CountLeaves(root) >= 16)
		{
			return root.Clone();
		}
		LayoutNode copy = root.Clone();
		if (TrySplit(copy, zoneId, orientation))
		{
			return copy;
		}
		return root.Clone();
	}

	private static bool TrySplit(LayoutNode node, string zoneId, SplitOrientation orientation)
	{
		if (node.IsLeaf)
		{
			if (!string.Equals(node.ZoneId, zoneId, StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
			LayoutNode old = LayoutNode.Leaf(node.ZoneId);
			LayoutNode replacement = LayoutNode.Split(orientation, 0.5, old, LayoutNode.Leaf());
			node.Kind = replacement.Kind;
			node.ZoneId = replacement.ZoneId;
			node.Orientation = replacement.Orientation;
			node.Ratio = replacement.Ratio;
			node.First = replacement.First;
			node.Second = replacement.Second;
			return true;
		}
		return (node.First != null && TrySplit(node.First, zoneId, orientation)) || (node.Second != null && TrySplit(node.Second, zoneId, orientation));
	}

	public static LayoutNode Merge(LayoutNode root, string splitId)
	{
		LayoutNode copy = root.Clone();
		if (copy.Id == splitId)
		{
			return LayoutNode.Leaf();
		}
		TryMerge(copy, splitId);
		return copy;
	}

	private static bool TryMerge(LayoutNode node, string splitId)
	{
		if (node.IsLeaf)
		{
			return false;
		}
		if (node.Id == splitId)
		{
			node.Kind = "leaf";
			node.ZoneId = Guid.NewGuid().ToString("N");
			node.First = null;
			node.Second = null;
			return true;
		}
		return (node.First != null && TryMerge(node.First, splitId)) || (node.Second != null && TryMerge(node.Second, splitId));
	}

	public static LayoutNode SetRatio(LayoutNode root, string splitId, double ratio, RectModel area)
	{
		LayoutNode copy = root.Clone();
		SetRatioInternal(copy, splitId, ratio, area);
		return copy;
	}

	private static bool SetRatioInternal(LayoutNode node, string splitId, double ratio, RectModel area)
	{
		if (node.IsLeaf || node.First == null || node.Second == null)
		{
			return false;
		}
		if (node.Id == splitId)
		{
			int extent = ((node.Orientation == SplitOrientation.Vertical) ? area.Width : area.Height);
			double minRatio = (double)((node.Orientation == SplitOrientation.Vertical) ? 160 : 120) / (double)Math.Max(extent, 1);
			node.Ratio = Math.Clamp(ratio, Math.Max(0.05, minRatio), Math.Min(0.95, 1.0 - minRatio));
			return true;
		}
		RectModel first = ((node.Orientation == SplitOrientation.Vertical) ? RectModel.From(area.X, area.Y, (int)Math.Round((double)area.Width * node.Ratio), area.Height) : RectModel.From(area.X, area.Y, area.Width, (int)Math.Round((double)area.Height * node.Ratio)));
		RectModel second = ((node.Orientation == SplitOrientation.Vertical) ? RectModel.From(first.Right, area.Y, area.Right - first.Right, area.Height) : RectModel.From(area.X, first.Bottom, area.Width, area.Bottom - first.Bottom));
		return SetRatioInternal(node.First, splitId, ratio, first) || SetRatioInternal(node.Second, splitId, ratio, second);
	}

	public static int CountLeaves(LayoutNode root)
	{
		return Calculate(root, RectModel.From(0, 0, 10000, 10000)).Count;
	}

	public static bool IsValid(LayoutNode root, RectModel area, out string? error)
	{
		IReadOnlyList<ZoneRect> zones = Calculate(root, area);
		if (zones.Count == 0 || zones.Count > 16)
		{
			error = "Le nombre de zones est invalide.";
			return false;
		}
		if (zones.Any((ZoneRect z) => z.Rect.Width < 160 || z.Rect.Height < 120))
		{
			error = "Une zone est trop petite.";
			return false;
		}
		long total = zones.Sum((ZoneRect z) => (long)z.Rect.Width * (long)z.Rect.Height);
		if (total != (long)area.Width * (long)area.Height)
		{
			error = "Les zones ne couvrent pas toute la surface.";
			return false;
		}
		error = null;
		return true;
	}

	public static IReadOnlyList<LayoutDefinition> BuiltInPresets()
	{
		return new LayoutDefinition[7]
		{
			new LayoutDefinition
			{
				Id = "builtin-full",
				Name = "Zone unique",
				Root = LayoutNode.Leaf("full")
			},
			new LayoutDefinition
			{
				Id = "builtin-two",
				Name = "Deux colonnes",
				Root = LayoutNode.Split(SplitOrientation.Vertical, 0.5, LayoutNode.Leaf(), LayoutNode.Leaf())
			},
			new LayoutDefinition
			{
				Id = "builtin-three",
				Name = "Trois colonnes",
				Root = LayoutNode.Split(SplitOrientation.Vertical, 0.3333, LayoutNode.Leaf(), LayoutNode.Split(SplitOrientation.Vertical, 0.5, LayoutNode.Leaf(), LayoutNode.Leaf()))
			},
			new LayoutDefinition
			{
				Id = "builtin-ultra",
				Name = "Ultrawide 25 / 50 / 25",
				Root = LayoutNode.Split(SplitOrientation.Vertical, 0.25, LayoutNode.Leaf(), LayoutNode.Split(SplitOrientation.Vertical, 2.0 / 3.0, LayoutNode.Leaf(), LayoutNode.Leaf()))
			},
			new LayoutDefinition
			{
				Id = "builtin-focus",
				Name = "Ultrawide 30 / 40 / 30",
				Root = LayoutNode.Split(SplitOrientation.Vertical, 0.3, LayoutNode.Leaf(), LayoutNode.Split(SplitOrientation.Vertical, 0.5714, LayoutNode.Leaf(), LayoutNode.Leaf()))
			},
			new LayoutDefinition
			{
				Id = "builtin-rows",
				Name = "Deux lignes",
				Root = LayoutNode.Split(SplitOrientation.Horizontal, 0.5, LayoutNode.Leaf(), LayoutNode.Leaf())
			},
			new LayoutDefinition
			{
				Id = "builtin-grid",
				Name = "Grille 2 × 2",
				Root = LayoutNode.Split(SplitOrientation.Horizontal, 0.5, LayoutNode.Split(SplitOrientation.Vertical, 0.5, LayoutNode.Leaf(), LayoutNode.Leaf()), LayoutNode.Split(SplitOrientation.Vertical, 0.5, LayoutNode.Leaf(), LayoutNode.Leaf()))
			}
		};
	}
}

