using System;

namespace UltrawideToys.Core;

public sealed class LayoutDefinition
{
	public string Id { get; set; } = Guid.NewGuid().ToString("N");

	public string Name { get; set; } = "Disposition personnalisée";

	public LayoutNode Root { get; set; } = LayoutNode.Leaf();

	public LayoutDefinition Clone()
	{
		return new LayoutDefinition
		{
			Id = Id,
			Name = Name,
			Root = Root.Clone()
		};
	}
}

