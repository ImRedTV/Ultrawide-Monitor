using System;
using System.Collections.Generic;

namespace UltrawideToys.Core;

public sealed class DisplaySnapshot
{
	public List<MonitorProfile> Monitors { get; init; } = new List<MonitorProfile>();

	public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.Now;
}

