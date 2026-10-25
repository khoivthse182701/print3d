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

    [Fact]
    public void AsymmetricZUpStlScalesPhysicalHeightZToTargetNotLongestAxis()
    {
        // Model is wider and deeper than it is tall: X = 0.2, Y = 0.15, Z = 0.05
        // X is the longest axis (0.2). Under the old bug, scale was 100 / 0.2 = 500, making Z only 25 mm!
        // Under the correct Z-up fix: scale is 100 / 0.05 = 2000, making Z exactly 100 mm.
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, AsymmetricStl(0.2f, 0.15f, 0.05f));
            var analysis = StlGeometry.AnalyzePrintability(path, 100, "Z");
            var dimensions = analysis.GetProperty("intended_print").GetProperty("dimensions_mm");

            var width = dimensions[0].GetDecimal();
            var depth = dimensions[1].GetDecimal();
            var height = dimensions[2].GetDecimal();

            Assert.Equal(400m, width);
            Assert.Equal(300m, depth);
            Assert.Equal(100m, height); // Physical Z height is 100 mm, NOT 25 mm!

            // Now normalize the STL file to millimeters
            StlGeometry.NormalizeToMillimeters(path, 400m, 300m, 100m, "Z");
            var bytes = File.ReadAllBytes(path);

            // Read vertices and verify max Z coordinate is 100 mm, max X is 400 mm, max Y is 300 mm
            var (maxX, maxY, maxZ) = ReadMaxCoordinates(bytes);
            Assert.Equal(400f, maxX, 1);
            Assert.Equal(300f, maxY, 1);
            Assert.Equal(100f, maxZ, 1);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void AsymmetricYUpStlScalesPhysicalHeightYToTarget()
    {
        // Y-up model: X = 0.06, Y = 0.05, Z = 0.04. Declared up-axis is Y, target height is 100 mm.
        // Scale should be 100 / 0.05 = 2000.
        // Physical height along Y is 100 mm, width along X is 120 mm, depth along Z is 80 mm.
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, AsymmetricStl(0.06f, 0.05f, 0.04f));
            var analysis = StlGeometry.AnalyzePrintability(path, 100, "Y");
            var dimensions = analysis.GetProperty("intended_print").GetProperty("dimensions_mm");

            var width = dimensions[0].GetDecimal();
            var depth = dimensions[1].GetDecimal();
            var height = dimensions[2].GetDecimal();

            Assert.Equal(120m, width);
            Assert.Equal(80m, depth);
            Assert.Equal(100m, height);

            StlGeometry.NormalizeToMillimeters(path, 120m, 80m, 100m, "Y");
            var bytes = File.ReadAllBytes(path);

            var (maxX, maxY, maxZ) = ReadMaxCoordinates(bytes);
            Assert.Equal(120f, maxX, 1);
            Assert.Equal(100f, maxY, 1); // Declared Y up-axis is 100 mm!
            Assert.Equal(80f, maxZ, 1);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("W")]
    [InlineData("unknown")]
    [InlineData("invalid")]
    [InlineData("42")]
    public void UnknownOrInvalidUpAxisExplicitlyRejectedAndNeverFallsBackToLongestAxis(string invalidAxis)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, AsymmetricStl(0.2f, 0.15f, 0.05f));
            Assert.Throws<InvalidDataException>(() => StlGeometry.AnalyzePrintability(path, 100, invalidAxis));
            Assert.Throws<InvalidDataException>(() => StlGeometry.NormalizeToMillimeters(path, 400, 300, 100, invalidAxis));
        }
        finally { File.Delete(path); }
    }

    public static JsonElement Analysis(int height = 100) => JsonSerializer.SerializeToElement(new
    {
        status = "healthy", metrics = new { is_watertight = true, volume = 0.00012m, non_manifold_edges = 0, holes = 0 },
        intended_print = new { dimensions_mm = new decimal[] { height * 0.6m, height * 0.4m, height }, wall_thickness_mm = 0.8m },
        thin_walls = new { status = "healthy" }
    });

    public static byte[] Stl() => AsymmetricStl(0.06f, 0.04f, 0.1f);

    public static byte[] AsymmetricStl(float extentX, float extentY, float extentZ)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write(new byte[80]); writer.Write((uint)2);
        float[][][] faces = [
            [[0, 0, 0], [extentX, 0, 0], [0, extentY, 0]],
            [[0, 0, extentZ], [extentX, 0, extentZ], [0, extentY, extentZ]]
        ];
        foreach (var face in faces)
        {
            writer.Write(0f); writer.Write(0f); writer.Write(1f);
            foreach (var point in face) foreach (var coordinate in point) writer.Write(coordinate);
            writer.Write((ushort)0);
        }
        return stream.ToArray();
    }

    private static (float maxX, float maxY, float maxZ) ReadMaxCoordinates(byte[] data)
    {
        var count = BitConverter.ToUInt32(data, 80);
        float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity, maxZ = float.NegativeInfinity;
        for (var i = 0; i < count; i++)
        {
            for (var v = 0; v < 3; v++)
            {
                var offset = 84 + i * 50 + 12 + v * 12;
                var x = BitConverter.ToSingle(data, offset);
                var y = BitConverter.ToSingle(data, offset + 4);
                var z = BitConverter.ToSingle(data, offset + 8);
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
                if (z > maxZ) maxZ = z;
            }
        }
        return (maxX, maxY, maxZ);
    }
}
