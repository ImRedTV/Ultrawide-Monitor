using System.Collections.Generic;

namespace UltrawideToys.Core;

public sealed class AppSettings
{
	public int SchemaVersion { get; set; } = 1;

	public bool Enabled { get; set; } = true;

	public bool StartupEnabled { get; set; } = true;

	public string Language { get; set; } = "fr-FR";

	public AppTheme Theme { get; set; } = AppTheme.System;

	public bool MaximizeToZones { get; set; } = true;

	public bool ShiftBypass { get; set; } = true;

	public bool SnapEnabled { get; set; } = true;

	public int SnapDistance { get; set; } = 10;

	public bool ElevatedAgentEnabled { get; set; } = true;

	public bool StartMinimized { get; set; } = true;

	public List<string> ExcludedProcesses { get; set; } = new List<string> { "ShellExperienceHost", "ApplicationFrameHost" };

	public List<MonitorProfile> Monitors { get; set; } = new List<MonitorProfile>();

	public List<LayoutDefinition> UserPresets { get; set; } = new List<LayoutDefinition>();
}

