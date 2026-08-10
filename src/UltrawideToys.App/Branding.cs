namespace UltrawideToys;

internal static class Branding
{
	public const string ProductName = "Ultrawide Monitor";

	public const string ShortName = "Ultrawide Monitor";

	public const string Publisher = "Gil Breysse (RED)";

	public static string Version => typeof(Branding).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";
}
