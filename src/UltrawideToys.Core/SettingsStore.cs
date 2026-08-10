using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace UltrawideToys.Core;

public sealed class SettingsStore
{
	private const string CurrentDataFolder = "UltrawideMonitor";

	private const string LegacyDataFolder = "UltrawideToys";

	private readonly string _path;

	private readonly JsonSerializerOptions _json = new JsonSerializerOptions(JsonSerializerDefaults.General)
	{
		WriteIndented = true,
		PropertyNameCaseInsensitive = true,
		Converters = { (JsonConverter)new JsonStringEnumConverter() }
	};

	public string Path => _path;

	public SettingsStore(string? path = null)
	{
		if (!string.IsNullOrWhiteSpace(path))
		{
			_path = path;
			return;
		}
		string configuredPath = Environment.GetEnvironmentVariable("ULTRAWIDE_TOYS_CONFIG");
		if (!string.IsNullOrWhiteSpace(configuredPath))
		{
			_path = configuredPath;
			return;
		}
		string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		_path = System.IO.Path.Combine(localAppData, "UltrawideMonitor", "settings.json");
		MigrateLegacySettings(localAppData, _path);
	}

	private static void MigrateLegacySettings(string localAppData, string currentPath)
	{
		try
		{
			string legacyPath = System.IO.Path.Combine(localAppData, "UltrawideToys", "settings.json");
			if (File.Exists(currentPath) || !File.Exists(legacyPath))
			{
				return;
			}
			string directory = System.IO.Path.GetDirectoryName(currentPath);
			if (!string.IsNullOrWhiteSpace(directory))
			{
				Directory.CreateDirectory(directory);
				File.Copy(legacyPath, currentPath, overwrite: false);
				string legacyBackup = legacyPath + ".bak";
				string currentBackup = currentPath + ".bak";
				if (File.Exists(legacyBackup) && !File.Exists(currentBackup))
				{
					File.Copy(legacyBackup, currentBackup, overwrite: false);
				}
			}
		}
		catch (Exception exception)
		{
			LocalLog.Error("Impossible de migrer les réglages vers le dossier Ultrawide Monitor", exception);
		}
	}

	public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		try
		{
			if (!File.Exists(_path))
			{
				return new AppSettings();
			}
			AppSettings result;
			await using (FileStream stream = File.OpenRead(_path))
			{
				AppSettings settings = await JsonSerializer.DeserializeAsync<AppSettings>((Stream)stream, _json, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				result = ((settings == null) ? new AppSettings() : Normalize(settings));
			}
			return result;
		}
		catch (Exception ex) when (((ex is IOException || ex is JsonException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
			LocalLog.Error("Impossible de charger la configuration; tentative avec la sauvegarde", ex);
			string backup = _path + ".bak";
			try
			{
				if (File.Exists(backup))
				{
					await using FileStream stream2 = File.OpenRead(backup);
					AppSettings settings2 = await JsonSerializer.DeserializeAsync<AppSettings>((Stream)stream2, _json, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					if (settings2 != null)
					{
						return Normalize(settings2);
					}
				}
			}
			catch (Exception ex2)
			{
				Exception backupException = ex2;
				LocalLog.Error("Impossible de charger la sauvegarde de configuration", backupException);
			}
			return new AppSettings();
		}
	}

	public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default(CancellationToken))
	{
		string directory = System.IO.Path.GetDirectoryName(_path) ?? throw new InvalidOperationException("Chemin de configuration invalide.");
		cancellationToken.ThrowIfCancellationRequested();
		Directory.CreateDirectory(directory);
		string temp = _path + ".tmp";
		using (FileStream stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 16384, FileOptions.SequentialScan))
		{
			JsonSerializer.Serialize(stream, Normalize(settings), _json);
			stream.Flush(flushToDisk: true);
		}
		if (File.Exists(_path))
		{
			string backup = _path + ".bak";
			try
			{
				File.Copy(_path, backup, overwrite: true);
			}
			catch
			{
			}
			File.Replace(temp, _path, null);
		}
		else
		{
			File.Move(temp, _path);
		}
		return Task.CompletedTask;
	}

	private static AppSettings Normalize(AppSettings settings)
	{
		settings.SchemaVersion = 1;
		settings.SnapDistance = Math.Clamp(settings.SnapDistance, 0, 64);
		AppSettings appSettings = settings;
		if (appSettings.Monitors == null)
		{
			List<MonitorProfile> list = (appSettings.Monitors = new List<MonitorProfile>());
		}
		appSettings = settings;
		if (appSettings.UserPresets == null)
		{
			List<LayoutDefinition> list3 = (appSettings.UserPresets = new List<LayoutDefinition>());
		}
		appSettings = settings;
		if (appSettings.ExcludedProcesses == null)
		{
			List<string> list5 = (appSettings.ExcludedProcesses = new List<string>());
		}
		foreach (MonitorProfile monitor in settings.Monitors)
		{
			MonitorProfile monitorProfile = monitor;
			if (monitorProfile.Layouts == null)
			{
				List<LayoutDefinition> list3 = (monitorProfile.Layouts = new List<LayoutDefinition>());
			}
			if (monitor.Layouts.Count == 0)
			{
				monitor.Layouts.Add(new LayoutDefinition
				{
					Name = "Zone unique"
				});
			}
			if (string.IsNullOrWhiteSpace(monitor.ActiveLayoutId) || monitor.Layouts.All((LayoutDefinition x) => x.Id != monitor.ActiveLayoutId))
			{
				monitor.ActiveLayoutId = monitor.Layouts[0].Id;
			}
		}
		return settings;
	}
}

