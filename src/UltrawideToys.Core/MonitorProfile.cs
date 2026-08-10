using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace UltrawideToys.Core;

public sealed class MonitorProfile
{
	public string Id { get; set; } = string.Empty;

	public string DeviceName { get; set; } = string.Empty;

	public string FriendlyName { get; set; } = "Écran";

	public bool IsPrimary { get; set; }

	public RectModel Bounds { get; set; } = new RectModel();

	public RectModel WorkArea { get; set; } = new RectModel();

	public int Dpi { get; set; } = 96;

	public string Resolution => $"{Bounds.Width} × {Bounds.Height}";

	public List<LayoutDefinition> Layouts { get; set; } = new List<LayoutDefinition>();

	public string ActiveLayoutId { get; set; } = string.Empty;

	[JsonIgnore]
	public LayoutDefinition ActiveLayout => Layouts.FirstOrDefault((LayoutDefinition x) => x.Id == ActiveLayoutId) ?? Layouts.First();
}

