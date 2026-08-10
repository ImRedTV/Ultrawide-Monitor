using System;
using Microsoft.Win32;

namespace UltrawideToys;

internal static class StartupRegistration
{
	private const string RunKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";

	private const string ValueName = "UltrawideMonitor";

	private const string LegacyValueName = "UltrawideToys";

	public static void SetEnabled(bool enabled)
	{
		try
		{
			using RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", writable: true);
			if (key != null)
			{
				if (enabled)
				{
					string path = Environment.ProcessPath ?? string.Empty;
					key.SetValue("UltrawideMonitor", "\"" + path + "\" --startup");
					key.DeleteValue("UltrawideToys", throwOnMissingValue: false);
				}
				else
				{
					key.DeleteValue("UltrawideMonitor", throwOnMissingValue: false);
					key.DeleteValue("UltrawideToys", throwOnMissingValue: false);
				}
			}
		}
		catch
		{
		}
	}
}

