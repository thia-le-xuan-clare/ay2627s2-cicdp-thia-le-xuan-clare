using System.Windows.Media;
using Waddle.Wpf;

namespace Waddle.Tests;

public class PieChartModelTests
{
  private static readonly Brush Red = Brushes.Red;
  private static readonly Brush Blue = Brushes.Blue;

  [Fact]
  public void LegendText_ShowsLabelCountAndRoundedPercent()
  {
    var pie = new PieChartModel("Banner", new[] { ("Sat up", 1, Red), ("Ignored", 2, Blue) });

    Assert.Equal("Sat up: 1 (33%)", pie.Slices[0].LegendText);
    Assert.Equal("Ignored: 2 (67%)", pie.Slices[1].LegendText);
  }

  [Fact]
  public void SingleSlice_IsAFullCircle_At100Percent()
  {
    var pie = new PieChartModel("Overlay", new[] { ("Dismissed", 4, Red) });

    var slice = Assert.Single(pie.Slices);
    Assert.IsType<EllipseGeometry>(slice.Shape);
    Assert.Equal(100, slice.Percent, precision: 5);
    Assert.Equal("Dismissed: 4 (100%)", slice.LegendText);
  }

  [Fact]
  public void TwoSlices_AreArcWedges_WithTheRightPercentages()
  {
    // 3 of 4 is a sweep over 180 degrees (large arc), 1 of 4 is under 180 degrees (small arc)
    var pie = new PieChartModel("Banner", new[] { ("Big", 3, Red), ("Small", 1, Blue) });

    Assert.All(pie.Slices, s => Assert.IsType<PathGeometry>(s.Shape));
    Assert.Equal(75, pie.Slices[0].Percent, precision: 5);
    Assert.Equal(25, pie.Slices[1].Percent, precision: 5);
  }

  [Fact]
  public void ZeroCountParts_AreLeftOut()
  {
    var pie = new PieChartModel("Banner", new[] { ("A", 0, Red), ("B", 2, Blue), ("C", 0, Red) });

    Assert.Equal("B", Assert.Single(pie.Slices).Label);
    Assert.Equal(2, pie.Total);
  }

  [Fact]
  public void NoParts_IsEmpty()
  {
    var pie = new PieChartModel("Banner", Array.Empty<(string, int, Brush)>());

    Assert.True(pie.IsEmpty);
    Assert.False(pie.HasData);
    Assert.Empty(pie.Slices);
  }
}
