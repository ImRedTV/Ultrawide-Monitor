using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UltrawideToys.Core;

namespace UltrawideToys.Agent;

internal static class Program
{
	private static readonly ManualResetEventSlim StopEvent = new ManualResetEventSlim(initialState: false);

	public static int Main(string[] args)
	{
		SettingsStore store = new SettingsStore();
		AppSettings settings = store.LoadAsync().GetAwaiter().GetResult();
		LocalLog.Info("Démarrage de l’agent administrateur");
		List<MonitorProfile> monitors = LoadMonitors(settings);
		using (WindowManager manager = new WindowManager(CurrentSettings, CurrentMonitors, WindowScope.ElevatedOnly))
		{
			if (settings.Enabled && settings.ElevatedAgentEnabled)
			{
				manager.Start();
			}
			PipeLoopAsync(store, () => settings, delegate(AppSettings value)
			{
				settings = value;
			}, delegate
			{
				monitors = LoadMonitors(settings);
			});
			Console.CancelKeyPress += delegate(object? _, ConsoleCancelEventArgs e)
			{
				e.Cancel = true;
				StopEvent.Set();
			};
			StopEvent.Wait();
			LocalLog.Info("Arrêt de l’agent administrateur");
			return 0;
		}
		IReadOnlyList<MonitorProfile> CurrentMonitors()
		{
			return monitors;
		}
		AppSettings CurrentSettings()
		{
			return settings;
		}
	}

	private static List<MonitorProfile> LoadMonitors(AppSettings settings)
	{
		IReadOnlyList<MonitorProfile> current = DisplayService.Enumerate();
		Dictionary<string, MonitorProfile> old = settings.Monitors.ToDictionary<MonitorProfile, string>((MonitorProfile x) => x.Id, StringComparer.OrdinalIgnoreCase);
		foreach (MonitorProfile monitor in current)
		{
			MonitorProfile byId;
			MonitorProfile saved = (old.TryGetValue(monitor.Id, out byId) ? byId : settings.Monitors.FirstOrDefault((MonitorProfile x) => string.Equals(x.DeviceName, monitor.DeviceName, StringComparison.OrdinalIgnoreCase) || (string.Equals(x.FriendlyName, monitor.FriendlyName, StringComparison.OrdinalIgnoreCase) && x.Bounds.Width == monitor.Bounds.Width && x.Bounds.Height == monitor.Bounds.Height)));
			if (saved != null)
			{
				monitor.Layouts = saved.Layouts;
				monitor.ActiveLayoutId = saved.ActiveLayoutId;
				continue;
			}
			LayoutDefinition layout = LayoutEngine.BuiltInPresets()[0].Clone();
			monitor.Layouts = new List<LayoutDefinition> { layout };
			monitor.ActiveLayoutId = layout.Id;
		}
		return current.ToList();
	}

	private static async Task PipeLoopAsync(SettingsStore store, Func<AppSettings> read, Action<AppSettings> write, Action refreshDisplays)
	{
		while (!StopEvent.IsSet)
		{
			try
			{
				await using NamedPipeServerStream pipe = new NamedPipeServerStream("UltrawideToys.Elevated", PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
				await pipe.WaitForConnectionAsync().ConfigureAwait(continueOnCapturedContext: false);
				using StreamReader reader = new StreamReader(pipe);
				switch ((await reader.ReadLineAsync().ConfigureAwait(continueOnCapturedContext: false))?.Trim().ToLowerInvariant())
				{
				case "sync":
					write(await store.LoadAsync().ConfigureAwait(continueOnCapturedContext: false));
					refreshDisplays();
					break;
				case "shutdown":
					StopEvent.Set();
					break;
				case "pause":
					break;
				}
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch (IOException)
			{
			}
			catch (Exception exception)
			{
				LocalLog.Error("Erreur du canal de l’agent administrateur", exception);
				await Task.Delay(500).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
	}
}

