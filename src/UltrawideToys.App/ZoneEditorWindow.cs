using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using UltrawideToys.Core;

namespace UltrawideToys;

public partial class ZoneEditorWindow : Window, IComponentConnector
{
	private readonly App _app;

	private readonly MonitorProfile _monitor;

	private LayoutDefinition _draft;

	private readonly Stack<LayoutDefinition> _undo = new Stack<LayoutDefinition>();

	private readonly Stack<LayoutDefinition> _redo = new Stack<LayoutDefinition>();

	private readonly Dictionary<string, double> _dragRatios = new Dictionary<string, double>();

	private readonly Dictionary<string, Border> _zoneVisuals = new Dictionary<string, Border>();

	private readonly Dictionary<string, Thumb> _splitLineVisuals = new Dictionary<string, Thumb>();

	private readonly Dictionary<string, Button> _splitMergeVisuals = new Dictionary<string, Button>();

	private LayoutDefinition? _dragBase;

	private LayoutNode? _dragPreviewRoot;

	private bool _ready;

	private bool _busy;

	private bool _closing;

	private RectModel Area => RectModel.From(0, 0, Math.Max(1, (int)EditorCanvas.ActualWidth), Math.Max(1, (int)EditorCanvas.ActualHeight));

	private string T(string key)
	{
		return Localization.Get(key, _app.Settings.Language);
	}

	public ZoneEditorWindow(App app, MonitorProfile monitor)
	{
		InitializeComponent();
		_app = app;
		_monitor = monitor;
		base.Title = (app.Settings.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "Edit zones" : "Modifier les zones");
		ResetButton.Content = T("Reset");
		CancelButton.Content = T("Cancel");
		ApplyButton.Content = T("EditorApply");
		PresetName.ToolTip = T("EditorPresetName");
		SavePresetButton.Content = T("EditorSave");
		DeletePresetButton.Content = T("EditorDelete");
		_draft = monitor.ActiveLayout.Clone();
		MonitorTitle.Text = monitor.FriendlyName + "  ·  " + monitor.Resolution;
		base.Left = monitor.WorkArea.X;
		base.Top = monitor.WorkArea.Y;
		base.Width = Math.Max(500, monitor.WorkArea.Width);
		base.Height = Math.Max(400, monitor.WorkArea.Height);
		PresetName.Text = _draft.Name;
		List<LayoutDefinition> presets = (from x in LayoutEngine.BuiltInPresets()
			select x.Clone()).Concat(app.Settings.UserPresets.Select((LayoutDefinition x) => x.Clone())).ToList();
		foreach (LayoutDefinition preset in presets)
		{
			PresetCombo.Items.Add(preset);
		}
		PresetCombo.SelectedItem = presets.FirstOrDefault((LayoutDefinition x) => x.Id == _draft.Id) ?? presets.FirstOrDefault((LayoutDefinition x) => x.Name.Equals(_draft.Name, StringComparison.OrdinalIgnoreCase));
		PresetCombo.SelectionChanged += PresetCombo_SelectionChanged;
		base.Loaded += delegate
		{
			_ready = true;
			UpdatePresetActions();
			RenderEditor();
		};
		base.Closed += delegate
		{
			_closing = true;
		};
		base.PreviewKeyDown += Window_PreviewKeyDown;
	}

	private void RenderEditor()
	{
		if (!_ready || EditorCanvas.ActualWidth <= 0.0 || EditorCanvas.ActualHeight <= 0.0)
		{
			return;
		}
		EditorCanvas.Children.Clear();
		_zoneVisuals.Clear();
		_splitLineVisuals.Clear();
		_splitMergeVisuals.Clear();
		_dragBase = null;
		_dragPreviewRoot = null;
		RectModel area = Area;
		IReadOnlyList<ZoneRect> zones = LayoutEngine.Calculate(_draft.Root, area);
		Color[] fills = new Color[4]
		{
			Color.FromArgb(72, 0, 120, 212),
			Color.FromArgb(62, 70, 145, 210),
			Color.FromArgb(55, 0, 155, 185),
			Color.FromArgb(58, 100, 70, 180)
		};
		for (int i = 0; i < zones.Count; i++)
		{
			ZoneRect zone = zones[i];
			RectModel rect = zone.Rect;
			Border border = new Border
			{
				BorderBrush = new SolidColorBrush(Color.FromArgb(175, 203, 231, 250)),
				BorderThickness = new Thickness(1.0),
				Background = new SolidColorBrush(fills[i % fills.Length]),
				CornerRadius = new CornerRadius(0.0),
				Tag = zone.ZoneId
			};
			StackPanel panel = new StackPanel
			{
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			};
			panel.Children.Add(new TextBlock
			{
				Text = $"{rect.Width} × {rect.Height}",
				Foreground = Brushes.White,
				FontSize = 16.0,
				FontWeight = FontWeights.SemiBold,
				HorizontalAlignment = HorizontalAlignment.Center
			});
			StackPanel buttons = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				HorizontalAlignment = HorizontalAlignment.Center
			};
			Button vertical = CreateEditorActionButton(CreateSplitIcon(SplitOrientation.Vertical), T("EditorSplitVertical"));
			vertical.Click += delegate
			{
				Split(zone.ZoneId, SplitOrientation.Vertical);
			};
			Button horizontal = CreateEditorActionButton(CreateSplitIcon(SplitOrientation.Horizontal), T("EditorSplitHorizontal"));
			horizontal.Click += delegate
			{
				Split(zone.ZoneId, SplitOrientation.Horizontal);
			};
			buttons.Children.Add(vertical);
			buttons.Children.Add(horizontal);
			panel.Children.Add(buttons);
			border.Child = panel;
			Canvas.SetLeft(border, rect.X);
			Canvas.SetTop(border, rect.Y);
			border.Width = Math.Max(1, rect.Width);
			border.Height = Math.Max(1, rect.Height);
			EditorCanvas.Children.Add(border);
			_zoneVisuals[zone.ZoneId] = border;
		}
		foreach (SplitRect split in LayoutEngine.CalculateSplits(_draft.Root, area))
		{
			AddSplitHandle(split);
		}
	}

	private void AddSplitHandle(SplitRect split)
	{
		if (split.Orientation == SplitOrientation.Vertical)
		{
			Thumb line = new Thumb
			{
				Width = 12.0,
				Height = Math.Max(32, split.Bounds.Height - 12),
				Background = Brushes.Transparent,
				Cursor = Cursors.SizeWE,
				Tag = split
			};
			double dragPosition = split.Position;
			Canvas.SetLeft(line, split.Position - 6);
			Canvas.SetTop(line, split.Bounds.Y + 6);
			line.DragStarted += delegate
			{
				dragPosition = split.Position;
				_dragBase = _draft.Clone();
				_dragPreviewRoot = null;
			};
			line.DragDelta += delegate(object _, DragDeltaEventArgs args)
			{
				dragPosition = Math.Clamp(dragPosition + args.HorizontalChange, split.Bounds.X + 160, split.Bounds.Right - 160);
				double num = (dragPosition - (double)split.Bounds.X) / Math.Max(1.0, split.Bounds.Width);
				_dragRatios[split.NodeId] = num;
				LayoutNode root = _dragBase?.Root ?? _draft.Root;
				_dragPreviewRoot = LayoutEngine.SetRatio(root, split.NodeId, num, Area);
				UpdateLiveVisuals(_dragPreviewRoot);
			};
			line.DragCompleted += delegate
			{
				LayoutNode dragPreviewRoot = _dragPreviewRoot;
				_dragRatios.Remove(split.NodeId);
				_dragBase = null;
				_dragPreviewRoot = null;
				if (dragPreviewRoot != null)
				{
					Change(dragPreviewRoot);
				}
			};
			EditorCanvas.Children.Add(line);
			_splitLineVisuals[split.NodeId] = line;
			AddMergeButton(split, split.Position - 18, split.Bounds.Y + split.Bounds.Height / 2 - 18);
			return;
		}
		Thumb line2 = new Thumb
		{
			Width = Math.Max(32, split.Bounds.Width - 12),
			Height = 12.0,
			Background = Brushes.Transparent,
			Cursor = Cursors.SizeNS,
			Tag = split
		};
		double dragPosition2 = split.Position;
		Canvas.SetLeft(line2, split.Bounds.X + 6);
		Canvas.SetTop(line2, split.Position - 6);
		line2.DragStarted += delegate
		{
			dragPosition2 = split.Position;
			_dragBase = _draft.Clone();
			_dragPreviewRoot = null;
		};
		line2.DragDelta += delegate(object _, DragDeltaEventArgs args)
		{
			dragPosition2 = Math.Clamp(dragPosition2 + args.VerticalChange, split.Bounds.Y + 120, split.Bounds.Bottom - 120);
			double num = (dragPosition2 - (double)split.Bounds.Y) / Math.Max(1.0, split.Bounds.Height);
			_dragRatios[split.NodeId] = num;
			LayoutNode root = _dragBase?.Root ?? _draft.Root;
			_dragPreviewRoot = LayoutEngine.SetRatio(root, split.NodeId, num, Area);
			UpdateLiveVisuals(_dragPreviewRoot);
		};
		line2.DragCompleted += delegate
		{
			LayoutNode dragPreviewRoot = _dragPreviewRoot;
			_dragRatios.Remove(split.NodeId);
			_dragBase = null;
			_dragPreviewRoot = null;
			if (dragPreviewRoot != null)
			{
				Change(dragPreviewRoot);
			}
		};
		EditorCanvas.Children.Add(line2);
		_splitLineVisuals[split.NodeId] = line2;
		AddMergeButton(split, split.Bounds.X + split.Bounds.Width / 2 - 18, split.Position - 18);
	}

	private void AddMergeButton(SplitRect split, double left, double top)
	{
		Button merge = new Button
		{
			Content = CreateMergeIcon(),
			Style = (Style)FindResource("EditorActionButton"),
			Width = 36.0,
			Height = 36.0,
			Padding = new Thickness(0.0),
			Margin = new Thickness(2.0),
			ToolTip = T("EditorMerge"),
			Background = new SolidColorBrush(Color.FromRgb(0, 120, 212)),
			Foreground = Brushes.White,
			BorderBrush = Brushes.White,
			BorderThickness = new Thickness(1.0),
			FontSize = 16.0,
			FontWeight = FontWeights.SemiBold,
			HorizontalContentAlignment = HorizontalAlignment.Center,
			VerticalContentAlignment = VerticalAlignment.Center,
			Tag = split.NodeId
		};
		merge.Click += delegate
		{
			Change(LayoutEngine.Merge(_draft.Root, split.NodeId));
		};
		Canvas.SetLeft(merge, left);
		Canvas.SetTop(merge, top);
		EditorCanvas.Children.Add(merge);
		_splitMergeVisuals[split.NodeId] = merge;
	}

	private void UpdateLiveVisuals(LayoutNode root)
	{
		if (EditorCanvas.ActualWidth <= 0.0 || EditorCanvas.ActualHeight <= 0.0)
		{
			return;
		}
		RectModel area = Area;
		foreach (ZoneRect zone in LayoutEngine.Calculate(root, area))
		{
			if (_zoneVisuals.TryGetValue(zone.ZoneId, out Border border))
			{
				Canvas.SetLeft(border, zone.Rect.X);
				Canvas.SetTop(border, zone.Rect.Y);
				border.Width = Math.Max(1, zone.Rect.Width);
				border.Height = Math.Max(1, zone.Rect.Height);
			}
		}
		foreach (SplitRect split in LayoutEngine.CalculateSplits(root, area))
		{
			if (_splitLineVisuals.TryGetValue(split.NodeId, out Thumb line))
			{
				if (split.Orientation == SplitOrientation.Vertical)
				{
					line.Width = 12.0;
					line.Height = Math.Max(32, split.Bounds.Height - 12);
					Canvas.SetLeft(line, split.Position - 6);
					Canvas.SetTop(line, split.Bounds.Y + 6);
				}
				else
				{
					line.Width = Math.Max(32, split.Bounds.Width - 12);
					line.Height = 12.0;
					Canvas.SetLeft(line, split.Bounds.X + 6);
					Canvas.SetTop(line, split.Position - 6);
				}
			}
			if (_splitMergeVisuals.TryGetValue(split.NodeId, out Button merge))
			{
				int left = ((split.Orientation == SplitOrientation.Vertical) ? (split.Position - 18) : (split.Bounds.X + split.Bounds.Width / 2 - 18));
				int top = ((split.Orientation == SplitOrientation.Vertical) ? (split.Bounds.Y + split.Bounds.Height / 2 - 18) : (split.Position - 18));
				Canvas.SetLeft(merge, left);
				Canvas.SetTop(merge, top);
			}
		}
		EditorCanvas.UpdateLayout();
	}

	private Button CreateEditorActionButton(UIElement content, string toolTip)
	{
		return new Button
		{
			Content = content,
			Style = (Style)FindResource("EditorActionButton"),
			ToolTip = toolTip,
			Width = 36.0,
			Height = 34.0,
			Padding = new Thickness(0.0),
			Margin = new Thickness(2.0),
			Background = new SolidColorBrush(Color.FromRgb(0, 120, 212)),
			Foreground = Brushes.White,
			BorderBrush = Brushes.White,
			BorderThickness = new Thickness(1.0),
			FontSize = 17.0,
			FontWeight = FontWeights.SemiBold,
			HorizontalContentAlignment = HorizontalAlignment.Center,
			VerticalContentAlignment = VerticalAlignment.Center
		};
	}

	private static Path CreateSplitIcon(SplitOrientation orientation)
	{
		string data = ((orientation == SplitOrientation.Vertical) ? "M1,1 H17 V17 H1 Z M9,1 V17" : "M1,1 H17 V17 H1 Z M1,9 H17");
		return new Path
		{
			Data = Geometry.Parse(data),
			Stroke = Brushes.White,
			StrokeThickness = 2.0,
			StrokeLineJoin = PenLineJoin.Round,
			Width = 18.0,
			Height = 18.0,
			Stretch = Stretch.Fill,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
	}

	private static Path CreateMergeIcon()
	{
		return new Path
		{
			Data = Geometry.Parse("M1,3 H5 V17 H1 Z M19,3 H15 V17 H19 Z M4,6 L8,10 L4,14 M16,6 L12,10 L16,14"),
			Stroke = Brushes.White,
			StrokeThickness = 1.7,
			StrokeLineJoin = PenLineJoin.Round,
			Width = 20.0,
			Height = 20.0,
			Stretch = Stretch.Fill,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
	}

	private void Split(string zoneId, SplitOrientation orientation)
	{
		if (LayoutEngine.CountLeaves(_draft.Root) < 16)
		{
			Change(LayoutEngine.SplitLeaf(_draft.Root, zoneId, orientation));
		}
	}

	private void Change(LayoutNode root)
	{
		_undo.Push(_draft.Clone());
		_redo.Clear();
		_draft.Root = root;
		RenderEditor();
	}

	private void PresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_ready && PresetCombo.SelectedItem is LayoutDefinition preset)
		{
			UpdatePresetActions();
			Change(preset.Root.Clone());
			PresetName.Text = preset.Name;
		}
	}

	private void UpdatePresetActions()
	{
		object selectedItem = PresetCombo.SelectedItem;
		LayoutDefinition preset = selectedItem as LayoutDefinition;
		bool isUserPreset = preset != null && _app.Settings.UserPresets.Any((LayoutDefinition x) => x.Id == preset.Id);
		DeletePresetButton.Visibility = ((!isUserPreset) ? Visibility.Collapsed : Visibility.Visible);
		DeletePresetButton.IsEnabled = isUserPreset && !_busy;
	}

	private async void DeletePreset_Click(object sender, RoutedEventArgs e)
	{
		if (_busy)
		{
			return;
		}
		object selectedItem = PresetCombo.SelectedItem;
		LayoutDefinition preset = selectedItem as LayoutDefinition;
		if (preset == null)
		{
			return;
		}
		LayoutDefinition saved = _app.Settings.UserPresets.FirstOrDefault((LayoutDefinition x) => x.Id == preset.Id);
		if (saved == null)
		{
			return;
		}
		SetBusy(busy: true);
		_app.Settings.UserPresets.Remove(saved);
		try
		{
			await _app.SaveSettingsAsync();
			PresetCombo.Items.Remove(preset);
			PresetCombo.SelectedItem = null;
			UpdatePresetActions();
			EditorStatus.Text = T("EditorDeleted");
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			_app.Settings.UserPresets.Add(saved);
			LocalLog.Error("Impossible de supprimer le preset", ex2);
			EditorStatus.Text = T("EditorSaveFailed");
		}
		finally
		{
			if (base.IsVisible)
			{
				SetBusy(busy: false);
			}
		}
	}

	private async void SavePreset_Click(object sender, RoutedEventArgs e)
	{
		if (_busy)
		{
			return;
		}
		string name = (string.IsNullOrWhiteSpace(PresetName.Text) ? $"{T("CustomPresetName")} {DateTime.Now:HH-mm}" : PresetName.Text.Trim());
		LayoutDefinition saved = _draft.Clone();
		saved.Id = Guid.NewGuid().ToString("N");
		saved.Name = name;
		LayoutDefinition existing = _app.Settings.UserPresets.FirstOrDefault((LayoutDefinition x) => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
		if (existing == null)
		{
			_app.Settings.UserPresets.Add(saved);
		}
		else
		{
			existing.Root = saved.Root;
			existing.Name = saved.Name;
		}
		SetBusy(busy: true);
		try
		{
			await _app.SaveSettingsAsync();
			if (!_closing)
			{
				EditorStatus.Text = T("PresetSaved");
			}
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			if (existing == null)
			{
				_app.Settings.UserPresets.Remove(saved);
			}
			LocalLog.Error("Impossible d'enregistrer le preset", ex2);
			if (!_closing)
			{
				EditorStatus.Text = T("EditorSaveFailed");
			}
		}
		finally
		{
			if (base.IsVisible)
			{
				SetBusy(busy: false);
			}
		}
	}

	private void Reset_Click(object sender, RoutedEventArgs e)
	{
		Change(LayoutEngine.BuiltInPresets()[0].Root.Clone());
	}

	private void Cancel_Click(object sender, RoutedEventArgs e)
	{
		_closing = true;
		Close();
	}

	private async void Apply_Click(object sender, RoutedEventArgs e)
	{
		if (_busy)
		{
			return;
		}
		if (!LayoutEngine.IsValid(_draft.Root, _monitor.WorkArea, out string error))
		{
			MessageBox.Show(this, error ?? T("InvalidLayout"), "Ultrawide Monitor", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		_draft.Name = (string.IsNullOrWhiteSpace(PresetName.Text) ? _draft.Name : PresetName.Text.Trim());
		SetBusy(busy: true);
		try
		{
			await _app.ApplyLayoutAsync(_monitor, _draft);
			_closing = true;
			Close();
		}
		catch (Exception exception)
		{
			LocalLog.Error("Impossible d'appliquer la disposition", exception);
			EditorStatus.Text = T("EditorSaveFailed");
			if (base.IsVisible)
			{
				SetBusy(busy: false);
			}
		}
	}

	private void EditorCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
	{
		RenderEditor();
	}

	private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Invalid comparison between Unknown and I4
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Invalid comparison between Unknown and I4
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Invalid comparison between Unknown and I4
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Invalid comparison between Unknown and I4
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Invalid comparison between Unknown and I4
		if ((int)e.Key == 13)
		{
			_closing = true;
			Close();
			e.Handled = true;
		}
		else if ((int)Keyboard.Modifiers == 2 && (int)e.Key == 69 && _undo.Count > 0)
		{
			_redo.Push(_draft.Clone());
			_draft = _undo.Pop();
			RenderEditor();
			e.Handled = true;
		}
		else if ((int)Keyboard.Modifiers == 2 && (int)e.Key == 68 && _redo.Count > 0)
		{
			_undo.Push(_draft.Clone());
			_draft = _redo.Pop();
			RenderEditor();
			e.Handled = true;
		}
	}

	private void SetBusy(bool busy)
	{
		_busy = busy;
		ResetButton.IsEnabled = !busy;
		ApplyButton.IsEnabled = !busy;
		SavePresetButton.IsEnabled = !busy;
		PresetCombo.IsEnabled = !busy;
		PresetName.IsEnabled = !busy;
		EditorCanvas.IsHitTestVisible = !busy;
		UpdatePresetActions();
	}
}
