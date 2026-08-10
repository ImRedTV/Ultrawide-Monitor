using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using UltrawideToys.Core;

namespace UltrawideToys;

public partial class MainWindow : Window, IComponentConnector
{
	private readonly App _app;

	private string _page = "zones";

	private readonly Dictionary<string, Button> _navButtons = new Dictionary<string, Button>();

	public MainWindow(App app)
	{
		InitializeComponent();
		_app = app;
		FooterVersionText.Text = "Version " + Branding.Version;
		ApplyTheme();
		base.Loaded += delegate
		{
			WindowBackdrop.Apply(this);
		};
		BuildNavigation();
		ShowZonesPage();
	}

	public void ShowZonesPage()
	{
		_page = "zones";
		BuildNavigation();
		BuildZonesPage();
	}

	private string T(string key)
	{
		return Localization.Get(key, _app.Settings.Language);
	}

	private void BuildNavigation()
	{
		NavigationPanel.Children.Clear();
		_navButtons.Clear();
		AddNav("zones", T("Zones"), "▦");
		AddNav("behavior", T("Behavior"), "⌘");
		AddNav("startup", T("Startup"), "◷");
		AddNav("appearance", T("Appearance"), "◐");
		AddNav("compatibility", T("Compatibility"), "♡");
		AddNav("about", T("About"), "ⓘ");
		foreach (KeyValuePair<string, Button> pair in _navButtons)
		{
			pair.Value.Background = ((pair.Key == _page) ? new SolidColorBrush(Color.FromArgb(35, 0, 120, 212)) : Brushes.Transparent);
		}
	}

	private void AddNav(string page, string label, string glyph)
	{
		Button button = new Button
		{
			HorizontalContentAlignment = HorizontalAlignment.Left,
			Background = Brushes.Transparent,
			BorderThickness = new Thickness(0.0),
			Padding = new Thickness(10.0, 10.0, 8.0, 10.0),
			Margin = new Thickness(0.0, 2.0, 0.0, 2.0),
			Tag = page
		};
		StackPanel panel = new StackPanel
		{
			Orientation = Orientation.Horizontal
		};
		panel.Children.Add(new TextBlock
		{
			Text = glyph,
			FontSize = 18.0,
			Width = 30.0,
			VerticalAlignment = VerticalAlignment.Center,
			Opacity = 0.8
		});
		panel.Children.Add(new TextBlock
		{
			Text = label,
			FontSize = 14.0,
			VerticalAlignment = VerticalAlignment.Center
		});
		button.Content = panel;
		button.Click += delegate
		{
			_page = page;
			BuildNavigation();
			BuildPage();
		};
		NavigationPanel.Children.Add(button);
		_navButtons[page] = button;
	}

	private void BuildPage()
	{
		switch (_page)
		{
		case "behavior":
			BuildBehaviorPage();
			break;
		case "startup":
			BuildStartupPage();
			break;
		case "appearance":
			BuildAppearancePage();
			break;
		case "compatibility":
			BuildCompatibilityPage();
			break;
		case "about":
			BuildAboutPage();
			break;
		default:
			BuildZonesPage();
			break;
		}
	}

	private void BeginPage(string title, string description)
	{
		ContentPanel.Children.Clear();
		ContentPanel.Children.Add(new TextBlock
		{
			Text = title,
			FontSize = 32.0,
			FontWeight = FontWeights.SemiBold,
			Margin = new Thickness(0.0, 0.0, 0.0, 6.0)
		});
		ContentPanel.Children.Add(new TextBlock
		{
			Text = description,
			FontSize = 15.0,
			Opacity = 0.72,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0.0, 0.0, 0.0, 24.0)
		});
	}

	private void BuildZonesPage()
	{
		_page = "zones";
		BeginPage(T("Zones"), T("ZonesDescription"));
		CheckBox active = new CheckBox
		{
			Content = T("Enabled"),
			IsChecked = _app.Settings.Enabled,
			FontSize = 16.0,
			Margin = new Thickness(0.0, 0.0, 0.0, 18.0)
		};
		active.Checked += async delegate
		{
			_app.Settings.Enabled = true;
			await _app.SaveSettingsAsync();
		};
		active.Unchecked += async delegate
		{
			_app.Settings.Enabled = false;
			await _app.SaveSettingsAsync();
		};
		ContentPanel.Children.Add(active);
		if (_app.Monitors.Count == 0)
		{
			ContentPanel.Children.Add(new TextBlock
			{
				Text = T("NoMonitors"),
				FontSize = 16.0
			});
			return;
		}
		WrapPanel wrap = new WrapPanel
		{
			Orientation = Orientation.Horizontal
		};
		foreach (MonitorProfile monitor in _app.Monitors)
		{
			wrap.Children.Add(BuildMonitorCard(monitor));
		}
		ContentPanel.Children.Add(wrap);
		TextBlock hint = new TextBlock
		{
			Text = T("DoubleClick"),
			Opacity = 0.65,
			Margin = new Thickness(0.0, 28.0, 0.0, 0.0),
			TextWrapping = TextWrapping.Wrap
		};
		ContentPanel.Children.Add(hint);
	}

	private Border BuildMonitorCard(MonitorProfile monitor)
	{
		Border border = new Border
		{
			Width = 320.0,
			Margin = new Thickness(0.0, 0.0, 16.0, 16.0),
			Padding = new Thickness(18.0),
			CornerRadius = new CornerRadius(12.0),
			BorderThickness = new Thickness(1.0),
			BorderBrush = new SolidColorBrush(Color.FromArgb(35, 128, 128, 128)),
			Background = new SolidColorBrush(Color.FromArgb(18, 0, 120, 212))
		};
		StackPanel panel = new StackPanel();
		DockPanel title = new DockPanel();
		TextBlock titleText = new TextBlock
		{
			Text = monitor.FriendlyName,
			FontSize = 17.0,
			FontWeight = FontWeights.SemiBold
		};
		title.Children.Add(titleText);
		if (monitor.IsPrimary)
		{
			title.Children.Add(new TextBlock
			{
				Text = T("Primary"),
				FontSize = 11.0,
				Opacity = 0.7,
				HorizontalAlignment = HorizontalAlignment.Right,
				Margin = new Thickness(8.0, 4.0, 0.0, 0.0)
			});
		}
		panel.Children.Add(title);
		panel.Children.Add(new TextBlock
		{
			Text = $"{monitor.Resolution}  ·  {monitor.Dpi} DPI",
			Opacity = 0.65,
			Margin = new Thickness(0.0, 2.0, 0.0, 12.0)
		});
		panel.Children.Add(new ZonePreviewControl(monitor)
		{
			Height = 132.0,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			Margin = new Thickness(0.0, 0.0, 0.0, 12.0)
		});
		panel.Children.Add(new TextBlock
		{
			Text = monitor.ActiveLayout.Name,
			FontSize = 13.0,
			Opacity = 0.75,
			Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
		});
		StackPanel actions = new StackPanel
		{
			Orientation = Orientation.Horizontal
		};
		Button edit = new Button
		{
			Content = T("Edit"),
			Tag = monitor,
			MinWidth = 100.0
		};
		edit.Click += delegate
		{
			_app.OpenEditor(monitor.Id);
		};
		actions.Children.Add(edit);
		ComboBox presets = new ComboBox
		{
			MinWidth = 150.0,
			Margin = new Thickness(8.0, 4.0, 0.0, 4.0),
			ToolTip = T("Preset")
		};
		List<LayoutDefinition> all = (from x in LayoutEngine.BuiltInPresets()
			select x.Clone()).Concat(_app.Settings.UserPresets.Select((LayoutDefinition x) => x.Clone())).ToList();
		foreach (LayoutDefinition preset in all)
		{
			presets.Items.Add(preset);
		}
		presets.DisplayMemberPath = "Name";
		presets.SelectedItem = all.FirstOrDefault((LayoutDefinition x) => x.Name == monitor.ActiveLayout.Name) ?? all.FirstOrDefault();
		presets.SelectionChanged += async delegate
		{
			object selectedItem = presets.SelectedItem;
			if (selectedItem is LayoutDefinition selected)
			{
				LayoutDefinition copy = selected.Clone();
				copy.Id = monitor.ActiveLayoutId;
				copy.Name = selected.Name;
				await _app.ApplyLayoutAsync(monitor, copy);
				BuildZonesPage();
			}
		};
		actions.Children.Add(presets);
		panel.Children.Add(actions);
		border.Child = panel;
		return border;
	}

	private void BuildBehaviorPage()
	{
		BeginPage(T("Behavior"), T("BehaviorDescription"));
		AddSettingCheck(T("Maximize"), _app.Settings.MaximizeToZones, async delegate(bool value)
		{
			_app.Settings.MaximizeToZones = value;
			await _app.SaveSettingsAsync();
		});
		AddSettingCheck(T("ShiftBypass"), _app.Settings.ShiftBypass, async delegate(bool value)
		{
			_app.Settings.ShiftBypass = value;
			await _app.SaveSettingsAsync();
		});
		AddSettingCheck(T("Snap"), _app.Settings.SnapEnabled, async delegate(bool value)
		{
			_app.Settings.SnapEnabled = value;
			await _app.SaveSettingsAsync();
		});
		StackPanel distance = new StackPanel
		{
			Margin = new Thickness(0.0, 12.0, 0.0, 0.0)
		};
		distance.Children.Add(new TextBlock
		{
			Text = T("SnapDistance"),
			FontSize = 16.0,
			FontWeight = FontWeights.SemiBold
		});
		Slider slider = new Slider
		{
			Minimum = 0.0,
			Maximum = 32.0,
			Value = _app.Settings.SnapDistance,
			TickFrequency = 1.0,
			IsSnapToTickEnabled = true,
			Width = 300.0
		};
		TextBlock valueText = new TextBlock
		{
			Text = $"{_app.Settings.SnapDistance} px",
			Opacity = 0.7
		};
		slider.ValueChanged += async delegate
		{
			_app.Settings.SnapDistance = (int)slider.Value;
			valueText.Text = $"{_app.Settings.SnapDistance} px";
			await _app.SaveSettingsAsync();
		};
		distance.Children.Add(slider);
		distance.Children.Add(valueText);
		ContentPanel.Children.Add(distance);
	}

	private void AddSettingCheck(string text, bool value, Func<bool, Task> changed)
	{
		CheckBox check = new CheckBox
		{
			Content = text,
			IsChecked = value,
			FontSize = 16.0,
			Margin = new Thickness(0.0, 8.0, 0.0, 8.0)
		};
		check.Checked += async delegate
		{
			await changed(arg: true);
		};
		check.Unchecked += async delegate
		{
			await changed(arg: false);
		};
		ContentPanel.Children.Add(check);
	}

	private void BuildStartupPage()
	{
		BeginPage(T("Startup"), T("StartupDescription"));
		AddSettingCheck(T("StartWithWindows"), _app.Settings.StartupEnabled, async delegate(bool value)
		{
			_app.Settings.StartupEnabled = value;
			StartupRegistration.SetEnabled(value);
			await _app.SaveSettingsAsync();
		});
		AddSettingCheck(T("StartMinimized"), _app.Settings.StartMinimized, async delegate(bool value)
		{
			_app.Settings.StartMinimized = value;
			await _app.SaveSettingsAsync();
		});
		AddSettingCheck(T("AdminAgent"), _app.Settings.ElevatedAgentEnabled, async delegate(bool value)
		{
			_app.Settings.ElevatedAgentEnabled = value;
			await _app.SaveSettingsAsync();
		});
		ContentPanel.Children.Add(new TextBlock
		{
			Text = T("StartupAgentText"),
			Opacity = 0.68,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0.0, 18.0, 0.0, 0.0)
		});
	}

	private void BuildAppearancePage()
	{
		BeginPage(T("Appearance"), T("AppearanceDescription"));
		ComboBox language = CreateAppearanceSelector(
			_app.Settings.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
			"Français",
			"English");
		ComboBox theme = CreateAppearanceSelector(
			_app.Settings.Theme switch
			{
				AppTheme.Light => 1,
				AppTheme.Dark => 2,
				_ => 0
			},
			T("System"),
			T("Light"),
			T("Dark"));

		Grid settings = new Grid
		{
			MaxWidth = 820.0,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			Margin = new Thickness(0.0, 2.0, 0.0, 0.0)
		};
		settings.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		settings.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		settings.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		Grid languageRow = BuildAppearanceRow(T("Language"), T("AppearanceLanguageDescription"), language);
		Grid.SetRow(languageRow, 0);
		settings.Children.Add(languageRow);
		Border separator = new Border
		{
			Height = 1.0,
			Background = ThemeBrush("DividerStrokeColorDefaultBrush", Color.FromArgb(35, 128, 128, 128)),
			Margin = new Thickness(0.0, 2.0, 0.0, 2.0)
		};
		Grid.SetRow(separator, 1);
		settings.Children.Add(separator);
		Grid themeRow = BuildAppearanceRow(T("Theme"), T("AppearanceThemeDescription"), theme);
		Grid.SetRow(themeRow, 2);
		settings.Children.Add(themeRow);
		ContentPanel.Children.Add(settings);
		ContentPanel.Children.Add(new TextBlock
		{
			Text = T("AppearanceAutoSave"),
			MaxWidth = 820.0,
			FontSize = 13.0,
			Opacity = 0.62,
			Margin = new Thickness(0.0, 14.0, 0.0, 0.0)
		});

		language.SelectionChanged += async delegate
		{
			_app.Settings.Language = ((language.SelectedIndex == 1) ? "en-US" : "fr-FR");
			await _app.SaveSettingsAsync();
			BuildNavigation();
			BuildPage();
		};
		theme.SelectionChanged += async delegate
		{
			_app.Settings.Theme = theme.SelectedIndex switch
			{
				1 => AppTheme.Light,
				2 => AppTheme.Dark,
				_ => AppTheme.System
			};
			ApplyTheme();
			await _app.SaveSettingsAsync();
		};
	}

	private ComboBox CreateAppearanceSelector(int selectedIndex, params string[] options)
	{
		ComboBox selector = new ComboBox
		{
			Width = 260.0,
			Height = 36.0,
			HorizontalAlignment = HorizontalAlignment.Right,
			HorizontalContentAlignment = HorizontalAlignment.Left,
			VerticalContentAlignment = VerticalAlignment.Center,
			Padding = new Thickness(10.0, 0.0, 10.0, 0.0),
			FontSize = 14.0
		};
		foreach (string option in options)
		{
			selector.Items.Add(option);
		}
		selector.SelectedIndex = selectedIndex;
		return selector;
	}

	private Grid BuildAppearanceRow(string title, string description, ComboBox selector)
	{
		Grid row = new Grid
		{
			MinHeight = 92.0
		};
		row.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		row.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		StackPanel content = new StackPanel();
		content.VerticalAlignment = VerticalAlignment.Center;
		content.Children.Add(new TextBlock
		{
			Text = title,
			FontSize = 16.0,
			FontWeight = FontWeights.SemiBold
		});
		content.Children.Add(new TextBlock
		{
			Text = description,
			FontSize = 13.0,
			Opacity = 0.68,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0.0, 4.0, 20.0, 0.0)
		});
		Grid.SetColumn(content, 0);
		row.Children.Add(content);
		selector.Margin = new Thickness(16.0, 0.0, 0.0, 0.0);
		Grid.SetColumn(selector, 1);
		row.Children.Add(selector);
		return row;
	}

	private void ApplyTheme()
	{
		AppTheme theme = _app.Settings.Theme;
		if (1 == 0)
		{
		}
		ThemeMode themeMode = theme switch
		{
			AppTheme.Light => ThemeMode.Light, 
			AppTheme.Dark => ThemeMode.Dark, 
			_ => ThemeMode.System, 
		};
		if (1 == 0)
		{
		}
		base.ThemeMode = themeMode;
	}

	private void BuildCompatibilityPage()
	{
		BeginPage(T("Compatibility"), T("CompatibilityDescription"));
		Border card = new Border
		{
			MaxWidth = 720.0,
			Padding = new Thickness(24.0),
			CornerRadius = new CornerRadius(12.0),
			BorderThickness = new Thickness(1.0),
			BorderBrush = ThemeBrush("CardStrokeColorDefaultBrush", Color.FromArgb(42, 128, 128, 128)),
			Background = ThemeBrush("CardBackgroundFillColorDefaultBrush", Color.FromArgb(18, 128, 128, 128))
		};
		StackPanel panel = new StackPanel();
		panel.Children.Add(new TextBlock
		{
			Text = T("CompatibilityAppsTitle"),
			FontSize = 18.0,
			FontWeight = FontWeights.SemiBold
		});
		panel.Children.Add(new TextBlock
		{
			Text = T("CompatibilityAppsDescription"),
			FontSize = 13.0,
			Opacity = 0.7,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0.0, 5.0, 0.0, 18.0)
		});
		Grid inputRow = new Grid
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 12.0)
		};
		inputRow.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		inputRow.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		TextBox input = new TextBox
		{
			Height = 36.0,
			MinWidth = 360.0,
			Padding = new Thickness(11.0, 0.0, 11.0, 0.0),
			VerticalContentAlignment = VerticalAlignment.Center,
			ToolTip = T("ProcessTooltip")
		};
		Button addButton = new Button
		{
			Content = T("Add"),
			Height = 36.0,
			MinWidth = 96.0,
			Margin = new Thickness(10.0, 0.0, 0.0, 0.0),
			Padding = new Thickness(14.0, 5.0, 14.0, 5.0),
			Background = ThemeBrush("AccentFillColorDefaultBrush", Color.FromRgb(0, 120, 212)),
			Foreground = Brushes.White,
			BorderThickness = new Thickness(0.0)
		};
		Grid.SetColumn(input, 0);
		Grid.SetColumn(addButton, 1);
		inputRow.Children.Add(input);
		inputRow.Children.Add(addButton);
		panel.Children.Add(inputRow);
		panel.Children.Add(new TextBlock
		{
			Text = T("CompatibilityHint"),
			FontSize = 12.0,
			Opacity = 0.62,
			Margin = new Thickness(0.0, 0.0, 0.0, 14.0)
		});
		StackPanel rows = new StackPanel();
		Border rowsBorder = new Border
		{
			CornerRadius = new CornerRadius(8.0),
			BorderThickness = new Thickness(1.0),
			BorderBrush = ThemeBrush("ControlStrokeColorDefaultBrush", Color.FromArgb(38, 128, 128, 128)),
			Background = ThemeBrush("ControlFillColorDefaultBrush", Color.FromArgb(12, 128, 128, 128)),
			Child = rows
		};
		panel.Children.Add(rowsBorder);
		card.Child = panel;
		ContentPanel.Children.Add(card);
		addButton.Click += async delegate
		{
			await AddProcessAsync();
		};
		input.KeyDown += async delegate(object _, KeyEventArgs e)
		{
			if ((int)e.Key == 6)
			{
				e.Handled = true;
				await AddProcessAsync();
			}
		};
		RefreshRows();
		async Task AddProcessAsync()
		{
			string text = input.Text.Trim();
			if (text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
			{
				string text2 = text;
				text = text2.Substring(0, text2.Length - 4);
			}
			if (text.Length != 0 && !_app.Settings.ExcludedProcesses.Contains<string>(text, StringComparer.OrdinalIgnoreCase))
			{
				_app.Settings.ExcludedProcesses.Add(text);
				input.Clear();
				RefreshRows();
				await _app.SaveSettingsAsync();
				input.Focus();
			}
		}
		void RefreshRows()
		{
			rows.Children.Clear();
			if (_app.Settings.ExcludedProcesses.Count == 0)
			{
				rows.Children.Add(new TextBlock
				{
					Text = T("CompatibilityNoApps"),
					Opacity = 0.62,
					Padding = new Thickness(14.0, 13.0, 14.0, 13.0)
				});
				return;
			}
			foreach (string process in _app.Settings.ExcludedProcesses.ToList())
			{
				Grid row = new Grid
				{
					MinHeight = 48.0,
					Margin = new Thickness(14.0, 0.0, 10.0, 0.0)
				};
				row.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = new GridLength(1.0, GridUnitType.Star)
				});
				row.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = GridLength.Auto
				});
				row.Children.Add(new TextBlock
				{
					Text = process,
					FontSize = 14.0,
					VerticalAlignment = VerticalAlignment.Center
				});
				Button remove = new Button
				{
					Content = T("Remove"),
					MinWidth = 86.0,
					Margin = new Thickness(10.0, 7.0, 0.0, 7.0),
					Padding = new Thickness(10.0, 5.0, 10.0, 5.0),
					ToolTip = T("Remove")
				};
				remove.Click += async delegate
				{
					_app.Settings.ExcludedProcesses.Remove(process);
					RefreshRows();
					await _app.SaveSettingsAsync();
				};
				Grid.SetColumn(remove, 1);
				row.Children.Add(remove);
				rows.Children.Add(row);
				if (!string.Equals(process, _app.Settings.ExcludedProcesses.LastOrDefault(), StringComparison.OrdinalIgnoreCase))
				{
					rows.Children.Add(new Border
					{
						Height = 1.0,
						Background = ThemeBrush("DividerStrokeColorDefaultBrush", Color.FromArgb(25, 128, 128, 128)),
						Margin = new Thickness(14.0, 0.0, 14.0, 0.0)
					});
				}
			}
		}
	}

	private Brush ThemeBrush(string key, Color fallback)
	{
		return (TryFindResource(key) as Brush) ?? new SolidColorBrush(fallback);
	}

	private void BuildAboutPage()
	{
		BeginPage(T("About"), "Ultrawide Monitor");
		ContentPanel.Children.Add(new TextBlock
		{
			Text = "Version " + Branding.Version,
			FontSize = 18.0,
			Margin = new Thickness(0.0, 0.0, 0.0, 12.0)
		});
		ContentPanel.Children.Add(new TextBlock
		{
			Text = T("AboutText"),
			FontSize = 15.0,
			Opacity = 0.72,
			TextWrapping = TextWrapping.Wrap
		});
		ContentPanel.Children.Add(new TextBlock
		{
			Text = T("GithubCredit"),
			Margin = new Thickness(0.0, 18.0, 0.0, 4.0),
			Opacity = 0.72
		});
		Button github = new Button
		{
			Content = "github.com/ImRedTV",
			Width = 190.0,
			HorizontalAlignment = HorizontalAlignment.Left,
			ToolTip = "https://github.com/ImRedTV"
		};
		github.Click += delegate
		{
			try
			{
				Process.Start(new ProcessStartInfo("https://github.com/ImRedTV")
				{
					UseShellExecute = true
				});
			}
			catch (Exception exception)
			{
				LocalLog.Error("Impossible d'ouvrir GitHub", exception);
			}
		};
		ContentPanel.Children.Add(github);
		ContentPanel.Children.Add(new TextBlock
		{
			Text = T("StorageText"),
			Margin = new Thickness(0.0, 18.0, 0.0, 0.0),
			Opacity = 0.6
		});
	}
}
