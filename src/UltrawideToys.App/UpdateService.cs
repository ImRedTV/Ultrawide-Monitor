using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using UltrawideToys.Core;

namespace UltrawideToys;

internal static class UpdateService
{
	private sealed class GitHubRelease
	{
		[JsonPropertyName("tag_name")]
		public string? TagName { get; set; }

		[JsonPropertyName("html_url")]
		public string? HtmlUrl { get; set; }

		[JsonPropertyName("draft")]
		public bool Draft { get; set; }

		[JsonPropertyName("prerelease")]
		public bool Prerelease { get; set; }
	}

	private const string Repository = "ImRedTV/Ultrawide-Monitor";

	private const string LatestReleaseFallback = "https://github.com/ImRedTV/Ultrawide-Monitor/releases/latest";

	private static readonly HttpClient Client = CreateClient();

	private static HttpClient CreateClient()
	{
		HttpClient client = new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(4L)
		};
		client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("UltrawideMonitor", Branding.Version));
		client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
		return client;
	}

	public static async Task CheckAndPromptAsync(Window? owner, string language, bool showPrompt, CancellationToken cancellationToken)
	{
		try
		{
			using HttpResponseMessage response = await Client.GetAsync("https://api.github.com/repos/ImRedTV/Ultrawide-Monitor/releases/latest", cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (!response.IsSuccessStatusCode)
			{
				return;
			}
			await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			GitHubRelease release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, (JsonSerializerOptions?)null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (release == null || release.Draft || release.Prerelease || !TryParseVersion(release.TagName, out Version latest) || !TryParseVersion(Branding.Version, out Version current) || latest <= current || !showPrompt)
			{
				return;
			}
			await ((DispatcherObject)Application.Current).Dispatcher.InvokeAsync((Action)delegate
			{
				string messageBoxText = Localization.Get("UpdateMessage", language).Replace("{0}", latest.ToString(3), StringComparison.Ordinal);
				Window owner2 = ((owner != null && owner.IsLoaded && owner.IsVisible) ? owner : null);
				MessageBoxResult messageBoxResult = MessageBox.Show(owner2, messageBoxText, Localization.Get("UpdateTitle", language), MessageBoxButton.YesNo, MessageBoxImage.Asterisk);
				if (messageBoxResult == MessageBoxResult.Yes)
				{
					try
					{
						Process.Start(new ProcessStartInfo(release.HtmlUrl ?? "https://github.com/ImRedTV/Ultrawide-Monitor/releases/latest")
						{
							UseShellExecute = true
						});
					}
					catch (Exception exception)
					{
						LocalLog.Error("Impossible d'ouvrir la release GitHub", exception);
					}
				}
			});
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex2)
		{
			Exception ex3 = ex2;
			LocalLog.Error("Vérification de mise à jour impossible", ex3);
		}
	}

	internal static bool TryParseVersion(string? value, out Version version)
	{
		version = new Version(0, 0, 0);
		string text = value?.Trim();
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}
		while (text.StartsWith("v", StringComparison.OrdinalIgnoreCase))
		{
			text = text.Substring(1);
		}
		if (!Version.TryParse(text, out Version parsed) || parsed.Major < 0 || parsed.Minor < 0)
		{
			return false;
		}
		version = new Version(parsed.Major, parsed.Minor, Math.Max(0, parsed.Build));
		return true;
	}
}

