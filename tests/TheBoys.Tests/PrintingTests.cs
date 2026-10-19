using System.Text.Json;
using TheBoys.Application.Printing;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;
using TheBoys.Infrastructure.Printing;

namespace TheBoys.Tests;

public class PrintingTests
{
    [Theory]
    [InlineData(100, 1, 390000)]
    [InlineData(120, 2, 780000)]
    public void PackagePriceUsesQuantityNotBoundingBoxVolume(int height, int quantity, int expected)
    {
        var task = new GenerationTask { Status = GenerationStatus.Succeeded, RequestedHeightMm = height,
            BoundingBoxX_Mm = 60, BoundingBoxY_Mm = 40, BoundingBoxZ_Mm = height };
        var quote = new PrintPricingService().Quote(task, new PricingPackage { PriceVnd = 390000, Material = "Resin" }, quantity, new());
        Assert.Equal(expected, quote.TotalVnd);
        Assert.False(quote.ApprovedForPrinting);
        Assert.Null(quote.ModelVolumeCm3);
    }

    [Fact]
    public void QuoteRejectsWrongSizeAndIncompleteGeometry()
    {
        var task = new GenerationTask { Status = GenerationStatus.Succeeded, RequestedHeightMm = 130,
            BoundingBoxX_Mm = 60, BoundingBoxY_Mm = 40, BoundingBoxZ_Mm = 130 };
        var pricing = new PrintPricingService();
        Assert.Throws<ArgumentException>(() => pricing.Quote(task, new PricingPackage { PriceVnd = 390000 }, 1, new()));
        task.BoundingBoxX_Mm = null;
        Assert.Throws<InvalidOperationException>(() => pricing.Quote(task, new PricingPackage { PriceVnd = 390000 }, 1, new()));
    }

    [Fact]
    public void NoCreditCannotBeUsedAndPaidCreditRefundKeepsSource()
    {
        var user = new User { FreeTrialRemaining = 0, PurchasedRemaining = 1 };
        var purchased = user.DeductCredit();
        Assert.True(purchased);
        Assert.Throws<InvalidOperationException>(() => user.DeductCredit());
        Assert.Equal(1, user.TotalUsed);
        user.RefundCredit(purchased);
        Assert.Equal(0, user.FreeTrialRemaining);
        Assert.Equal(1, user.PurchasedRemaining);
    }

    [Fact]
    public void AnalysisUsesCubicMeterConversionAndNeverAutoApproves()
    {
        var task = new GenerationTask { RequestedHeightMm = 100 };
        GenerationProcessor.ApplyAnalysis(task, Analysis());
        Assert.Equal(120m, task.EstimatedVolumeCm3);
        Assert.Equal(60m, task.BoundingBoxX_Mm);
        Assert.Equal(40m, task.BoundingBoxY_Mm);
        Assert.Equal(100m, task.BoundingBoxZ_Mm);
        Assert.Null(task.MinWallThicknessMm); // API's 0.8 mm is a threshold, not a measurement.
        Assert.False(task.IsApprovedForPrinting);
    }

    [Fact]
    public void StlIsNormalizedToMillimetersAndRejectsWrongAspectRatio()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, Stl());
            StlGeometry.NormalizeToMillimeters(path, 60, 40, 100);
            var bytes = File.ReadAllBytes(path);
            Assert.Equal(100f, BitConverter.ToSingle(bytes, 84 + 50 + 12 + 8), 3);
            StlGeometry.NormalizeToMillimeters(path, 60, 40, 100); // Safe on a resumed stage.
            Assert.Throws<InvalidDataException>(() => StlGeometry.NormalizeToMillimeters(path, 10, 10, 100));
        }
        finally { File.Delete(path); }
    }

    public static JsonElement Analysis(int height = 100) => JsonSerializer.SerializeToElement(new
    {
        status = "healthy", metrics = new { is_watertight = true, volume = 0.00012m, non_manifold_edges = 0, holes = 0 },
        intended_print = new { dimensions_mm = new decimal[] { height * 0.6m, height * 0.4m, height }, wall_thickness_mm = 0.8m },
        thin_walls = new { status = "healthy" }
    });

    public static byte[] Stl()
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write(new byte[80]); writer.Write((uint)2);
        float[][][] faces = [ [[0,0,0], [0.06f,0,0], [0,0.04f,0]], [[0,0,0.1f], [0.06f,0,0], [0,0.04f,0]] ];
        foreach (var face in faces)
        {
            writer.Write(0f); writer.Write(0f); writer.Write(1f);
            foreach (var point in face) foreach (var coordinate in point) writer.Write(coordinate);
            writer.Write((ushort)0);
        }
        return stream.ToArray();
    }
}
