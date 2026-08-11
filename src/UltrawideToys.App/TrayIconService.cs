using System;
using System.Drawing;
using System.Windows.Forms;

namespace UltrawideToys;

internal sealed class TrayIconService : IDisposable
{
	private readonly App _app;

	private readonly NotifyIcon _icon;

	private readonly ToolStripMenuItem _enabled;

	public TrayIconService(App app)
	{
		TrayIconService trayIconService = this;
		_app = app;
		_enabled = new ToolStripMenuItem(T("TrayEnabled"))
		{
			Checked = app.Settings.Enabled,
			CheckOnClick = true
		};
		_enabled.Click += async delegate
		{
			app.Settings.Enabled = trayIconService._enabled.Checked;
			await app.SaveSettingsAsync();
		};
		ContextMenuStrip menu = new ContextMenuStrip
		{
			Items = 
			{
				(ToolStripItem)_enabled,
				(ToolStripItem)new ToolStripSeparator(),
				{
					T("TrayEdit"),
					(Image?)null,
					(EventHandler?)delegate
					{
						app.OpenEditor();
					}
				},
				{
					T("TraySettings"),
					(Image?)null,
					(EventHandler?)delegate
					{
						app.ShowSettings();
					}
				},
				{
					T("TrayStartup"),
					(Image?)null,
					(EventHandler?)delegate
					{
						app.Settings.StartupEnabled = !app.Settings.StartupEnabled;
						StartupRegistration.SetEnabled(app.Settings.StartupEnabled);
					}
				},
				(ToolStripItem)new ToolStripSeparator(),
				{
					T("TrayAbout"),
					(Image?)null,
					(EventHandler?)delegate
					{
						app.ShowSettings();
					}
				},
				{
					T("TrayQuit"),
					(Image?)null,
					(EventHandler?)delegate
					{
						app.ExitApplication();
					}
				}
			}
		};
		_icon = new NotifyIcon
		{
			Icon = IconFactory.Create(),
			Text = "Ultrawide Monitor",
			Visible = true,
			ContextMenuStrip = menu
		};
		_icon.DoubleClick += delegate
		{
			app.OpenEditor();
		};
		string T(string key)
		{
			return Localization.Get(key, app.Settings.Language);
		}
	}

	public void Dispose()
	{
		_icon.Visible = false;
		_icon.Dispose();
	}
}
