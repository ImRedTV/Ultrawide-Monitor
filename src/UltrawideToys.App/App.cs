using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using UltrawideToys.Core;

namespace UltrawideToys;

public partial class App : Application
{
	private Mutex? _instanceMutex;

	private MainWindow? _mainWindow;

	private TrayIconService? _tray;

	private WindowManager? _windowManager;

	private SettingsStore? _settingsStore;

	private AppSettings _settings = new AppSettings();

	private CancellationTokenSource _lifetime = new CancellationTokenSource();

	private int _displayRefreshQueued;

	private bool _ownsMutex;

	public AppSettings Settings => _settings;

	public IReadOnlyList<MonitorProfile> Monitors => _settings.Monitors;

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		if (e.Args.Any((string x) => string.Equals(x, "--version", StringComparison.OrdinalIgnoreCase)))
		{
			MessageBox.Show("Ultrawide Monitor " + (typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0.1.0"), "Ultrawide Monitor", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			Shutdown();
			return;
		}
		if (!AcquireInstance(e.Args))
		{
			Shutdown();
			return;
		}
		_settingsStore = new SettingsStore();
		_settings = _settingsStore.LoadAsync().GetAwaiter().GetResult();
		LocalLog.Info("Démarrage de l’application; mode=" + (e.Args.Any((string x) => string.Equals(x, "--startup", StringComparison.OrdinalIgnoreCase)) ? "startup" : "manuel"));
		StartupRegistration.SetEnabled(_settings.StartupEnabled);
		RefreshDisplays();
		SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
		_mainWindow = new MainWindow(this);
		_tray = new TrayIconService(this);
		_windowManager = new WindowManager(() => _settings, () => _settings.Monitors);
		if (_settings.Enabled)
		{
			_windowManager.Start();
		}
		RunControlPipeAsync(_lifetime.Token);
		bool startup = e.Args.Any((string x) => string.Equals(x, "--startup", StringComparison.OrdinalIgnoreCase));
		string editor = e.Args.FirstOrDefault((string x) => x.StartsWith("--editor", StringComparison.OrdinalIgnoreCase));
		if (!startup || editor != null)
		{
			_mainWindow.Show();
			if (editor != null)
			{
				string id = e.Args.SkipWhile((string x) => !x.StartsWith("--editor", StringComparison.OrdinalIgnoreCase)).Skip(1).FirstOrDefault();
				((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
				{
					OpenEditor(string.IsNullOrWhiteSpace(id) ? null : id);
				}, Array.Empty<object>());
			}
		}
		UpdateService.CheckAndPromptAsync(_mainWindow, _settings.Language, !startup, _lifetime.Token);
	}

	protected override void OnExit(ExitEventArgs e)
	{
		_lifetime.Cancel();
		SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
		_windowManager?.Dispose();
		_tray?.Dispose();
		if (_ownsMutex)
		{
			try
			{
				ElevatedAgentClient.SendAsync("shutdown").GetAwaiter().GetResult();
			}
			catch
			{
			}
		}
		LocalLog.Info("Arrêt de l’application");
		if (_ownsMutex)
		{
			_instanceMutex?.ReleaseMutex();
		}
		_instanceMutex?.Dispose();
		base.OnExit(e);
	}

	private bool AcquireInstance(string[] args)
	{
		_instanceMutex = new Mutex(initiallyOwned: true, "Local\\UltrawideToys.SingleInstance", out var created);
		_ownsMutex = created;
		if (created)
		{
			return true;
		}
		string command = string.Join(" ", args.Select(Quote));
		SendControlCommandAsync(command).GetAwaiter().GetResult();
		return false;
	}

	private static string Quote(string value)
	{
		return value.Contains(' ') ? ("\"" + value.Replace("\"", "\\\"") + "\"") : value;
	}

	private async Task SendControlCommandAsync(string command)
	{
		try
		{
			await using NamedPipeClientStream pipe = new NamedPipeClientStream(".", "UltrawideToys.Control", PipeDirection.Out, PipeOptions.Asynchronous);
			await pipe.ConnectAsync(600).ConfigureAwait(continueOnCapturedContext: false);
			await using StreamWriter writer = new StreamWriter(pipe)
			{
				AutoFlush = true
			};
			await writer.WriteLineAsync(command).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch
		{
		}
	}

	private async Task RunControlPipeAsync(CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				await using (NamedPipeServerStream pipe = new NamedPipeServerStream("UltrawideToys.Control", PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly))
				{
					await pipe.WaitForConnectionAsync(cancellationToken);
					using (StreamReader reader = new StreamReader(pipe))
					{
						string command = await reader.ReadLineAsync(cancellationToken);
						if (command == null)
						{
							goto end_IL_0114;
						}
						((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
						{
							HandleControlCommand(command);
						}, Array.Empty<object>());
						goto end_IL_00f2;
						end_IL_0114:;
					}
					goto end_IL_003c;
					end_IL_00f2:;
				}
				end_IL_003c:;
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch
			{
				await Task.Delay(250, cancellationToken);
			}
		}
	}

	private void HandleControlCommand(string command)
	{
		string[] parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (parts.Any((string x) => string.Equals(x, "--editor", StringComparison.OrdinalIgnoreCase)))
		{
			int index = Array.FindIndex(parts, (string x) => string.Equals(x, "--editor", StringComparison.OrdinalIgnoreCase));
			OpenEditor((index >= 0 && index + 1 < parts.Length) ? parts[index + 1].Trim('"') : null);
		}
		else
		{
			ShowSettings();
		}
	}

	public void RefreshDisplays()
	{
		IReadOnlyList<MonitorProfile> current = DisplayService.Enumerate();
		Dictionary<string, MonitorProfile> old = _settings.Monitors.ToDictionary<MonitorProfile, string>((MonitorProfile x) => x.Id, StringComparer.OrdinalIgnoreCase);
		foreach (MonitorProfile monitor in current)
		{
			MonitorProfile byId;
			MonitorProfile existing = (old.TryGetValue(monitor.Id, out byId) ? byId : _settings.Monitors.FirstOrDefault((MonitorProfile x) => string.Equals(x.DeviceName, monitor.DeviceName, StringComparison.OrdinalIgnoreCase) || (string.Equals(x.FriendlyName, monitor.FriendlyName, StringComparison.OrdinalIgnoreCase) && x.Bounds.Width == monitor.Bounds.Width && x.Bounds.Height == monitor.Bounds.Height)));
			if (existing != null)
			{
				monitor.Layouts = ((existing.Layouts.Count == 0) ? new List<LayoutDefinition>() : existing.Layouts);
				monitor.ActiveLayoutId = existing.ActiveLayoutId;
				continue;
			}
			LayoutDefinition initial = LayoutEngine.BuiltInPresets()[0].Clone();
			monitor.Layouts = new List<LayoutDefinition> { initial };
			monitor.ActiveLayoutId = initial.Id;
		}
		_settings.Monitors = current.ToList();
		_settingsStore?.SaveAsync(_settings).GetAwaiter().GetResult();
	}

	private void OnDisplaySettingsChanged(object? sender, EventArgs e)
	{
		if (Interlocked.Exchange(ref _displayRefreshQueued, 1) != 0)
		{
			return;
		}
		try
		{
			((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
			{
				try
				{
					RefreshDisplays();
					SyncElevatedAsync();
				}
				finally
				{
					Volatile.Write(ref _displayRefreshQueued, 0);
				}
			}, Array.Empty<object>());
		}
		catch
		{
			Volatile.Write(ref _displayRefreshQueued, 0);
		}
	}

	private static async Task SyncElevatedAsync()
	{
		try
		{
			await ElevatedAgentClient.SendAsync("sync");
		}
		catch
		{
		}
	}

	public void ShowSettings()
	{
		if (_mainWindow != null)
		{
			if (!_mainWindow.IsVisible)
			{
				_mainWindow.Show();
			}
			if (_mainWindow.WindowState == WindowState.Minimized)
			{
				_mainWindow.WindowState = WindowState.Normal;
			}
			_mainWindow.Activate();
			_mainWindow.ShowZonesPage();
		}
	}

	public void OpenEditor(string? monitorId = null)
	{
		MonitorProfile monitor = (string.IsNullOrWhiteSpace(monitorId) ? _settings.Monitors.FirstOrDefault() : _settings.Monitors.FirstOrDefault((MonitorProfile x) => x.Id.Equals(monitorId, StringComparison.OrdinalIgnoreCase)));
		if (monitor == null)
		{
			ShowSettings();
			return;
		}
		ZoneEditorWindow editor = new ZoneEditorWindow(this, monitor);
		MainWindow mainWindow = _mainWindow;
		if (mainWindow != null && mainWindow.IsLoaded && mainWindow.IsVisible)
		{
			editor.Owner = _mainWindow;
		}
		editor.ShowDialog();
	}

	public async Task ApplyLayoutAsync(MonitorProfile monitor, LayoutDefinition layout)
	{
		LayoutDefinition existing = monitor.Layouts.FirstOrDefault((LayoutDefinition x) => x.Id == layout.Id);
		if (existing == null)
		{
			monitor.Layouts.Add(layout.Clone());
		}
		else
		{
			existing.Name = layout.Name;
			existing.Root = layout.Root.Clone();
		}
		monitor.ActiveLayoutId = layout.Id;
		if (_settingsStore != null)
		{
			await _settingsStore.SaveAsync(_settings);
		}
		try
		{
			await ElevatedAgentClient.SendAsync("sync");
		}
		catch
		{
		}
	}

	public async Task SaveSettingsAsync()
	{
		if (_settingsStore != null)
		{
			await _settingsStore.SaveAsync(_settings);
		}
		if (_settings.Enabled && _windowManager == null)
		{
			_windowManager = new WindowManager(() => _settings, () => _settings.Monitors);
			_windowManager.Start();
		}
		if (!_settings.Enabled && _windowManager != null)
		{
			_windowManager.Dispose();
			_windowManager = null;
		}
		try
		{
			await ElevatedAgentClient.SendAsync("sync");
		}
		catch
		{
		}
	}

	public void ToggleWindow()
	{
		MainWindow? mainWindow = _mainWindow;
		if (mainWindow != null && mainWindow.IsVisible)
		{
			_mainWindow.Hide();
		}
		else
		{
			ShowSettings();
		}
	}
}
