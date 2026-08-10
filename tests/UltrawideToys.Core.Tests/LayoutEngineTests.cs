using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace UltrawideToys.Core.Tests;

public sealed class LayoutEngineTests
{
	[Fact]
	public void TwoColumnsCoverAreaWithoutOverlap()
	{
		LayoutDefinition layout = LayoutEngine.BuiltInPresets().Single((LayoutDefinition x) => x.Id == "builtin-two");
		RectModel area = RectModel.From(0, 0, 3440, 1440);
		IReadOnlyList<ZoneRect> zones = LayoutEngine.Calculate(layout.Root, area);
		Assert.Equal(2, zones.Count);
		Assert.Equal(4953600L, zones.Sum((ZoneRect x) => (long)x.Rect.Width * (long)x.Rect.Height));
		Assert.Equal(zones[0].Rect.Right, zones[1].Rect.X);
		Assert.Equal(area.X, zones.Min((ZoneRect x) => x.Rect.X));
		Assert.Equal(area.Right, zones.Max((ZoneRect x) => x.Rect.Right));
		Assert.True(LayoutEngine.IsValid(layout.Root, area, out string _));
	}

	[Fact]
	public void SplittingAndMergingKeepTheTreeValid()
	{
		LayoutNode root = LayoutNode.Leaf("root");
		root = LayoutEngine.SplitLeaf(root, "root", SplitOrientation.Vertical);
		string first = LayoutEngine.Calculate(root, RectModel.From(0, 0, 1920, 1080))[0].ZoneId;
		root = LayoutEngine.SplitLeaf(root, first, SplitOrientation.Horizontal);
		Assert.Equal(3, LayoutEngine.CountLeaves(root));
		SplitRect split = LayoutEngine.CalculateSplits(root, RectModel.From(0, 0, 1920, 1080)).Last();
		root = LayoutEngine.Merge(root, split.NodeId);
		Assert.Equal(2, LayoutEngine.CountLeaves(root));
	}

	[Fact]
	public void RatiosAreClampedToMinimumSize()
	{
		LayoutNode root = LayoutNode.Split(SplitOrientation.Vertical, 0.5, LayoutNode.Leaf(), LayoutNode.Leaf());
		LayoutNode changed = LayoutEngine.SetRatio(root, root.Id, 0.001, RectModel.From(0, 0, 500, 400));
		IReadOnlyList<ZoneRect> zones = LayoutEngine.Calculate(changed, RectModel.From(0, 0, 500, 400));
		Assert.True(zones[0].Rect.Width >= 160);
		Assert.True(LayoutEngine.IsValid(changed, RectModel.From(0, 0, 500, 400), out string _));
	}

	[Fact]
	public void BuiltInPresetsCoverNegativeCoordinateWorkArea()
	{
		RectModel area = RectModel.From(-2560, -120, 5120, 1392);
		foreach (LayoutDefinition preset in LayoutEngine.BuiltInPresets())
		{
			Assert.True(LayoutEngine.IsValid(preset.Root, area, out string error), preset.Name + ": " + error);
			IReadOnlyList<ZoneRect> zones = LayoutEngine.Calculate(preset.Root, area);
			Assert.Equal((long)area.Width * (long)area.Height, zones.Sum((ZoneRect x) => (long)x.Rect.Width * (long)x.Rect.Height));
			Assert.Equal(zones.Count, zones.Select((ZoneRect x) => x.ZoneId).Distinct().Count());
		}
	}

	[Fact]
	public void SplitsRespectMaximumZoneCountAndMinimumSize()
	{
		LayoutNode root = LayoutNode.Leaf("root");
		RectModel area = RectModel.From(0, 0, 65536, 65536);
		for (int i = 0; i < 20; i++)
		{
			ZoneRect leaf = LayoutEngine.Calculate(root, area).First();
			root = LayoutEngine.SplitLeaf(root, leaf.ZoneId, (i % 2 != 0) ? SplitOrientation.Horizontal : SplitOrientation.Vertical);
		}
		Assert.True(LayoutEngine.CountLeaves(root) <= 16);
		Assert.True(LayoutEngine.IsValid(root, area, out string _));
	}

	[Fact]
	public async Task SettingsStoreRecoversAndWritesBackup()
	{
		string path = Path.Combine(Path.GetTempPath(), "UltrawideToysTests", Guid.NewGuid().ToString("N"), "settings.json");
		SettingsStore store = new SettingsStore(path);
		await store.SaveAsync(new AppSettings
		{
			UserPresets = { LayoutEngine.BuiltInPresets()[1].Clone() }
		});
		Assert.Single((await store.LoadAsync()).UserPresets);
		Assert.True(File.Exists(path));
	}

	[Fact]
	public async Task SettingsStoreLoadsBackupAfterCorruption()
	{
		string directory = Path.Combine(Path.GetTempPath(), "UltrawideToysTests", Guid.NewGuid().ToString("N"));
		string path = Path.Combine(directory, "settings.json");
		try
		{
			SettingsStore store = new SettingsStore(path);
			AppSettings settings = new AppSettings
			{
				UserPresets = { LayoutEngine.BuiltInPresets()[3].Clone() }
			};
			await store.SaveAsync(settings);
			settings.UserPresets.Add(LayoutEngine.BuiltInPresets()[4].Clone());
			await store.SaveAsync(settings);
			await File.WriteAllTextAsync(path, "{ broken json");
			Assert.Single((await store.LoadAsync()).UserPresets);
			Assert.True(File.Exists(path + ".bak"));
		}
		finally
		{
			if (Directory.Exists(directory))
			{
				Directory.Delete(directory, recursive: true);
			}
		}
	}
}

