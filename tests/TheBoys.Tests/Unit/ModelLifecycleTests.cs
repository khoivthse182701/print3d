using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;
using GenerationStage = TheBoys.Domain.Enums.GenerationStage;
using TheBoys.Infrastructure.Persistence;

namespace TheBoys.Tests.Unit;

public class ModelLifecycleTests
{
    [Fact]
    public void ModelProjectAndJobAggregateInvariantsEnforced()
    {
        var userId = Guid.NewGuid();
        var project = new ModelProject
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Robot Model",
            CreatedAtUtc = DateTime.UtcNow
        };

        var job = new GenerationJob
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            UserId = userId,
            Operation = "ImageTo3D",
            Provider = "Meshy",
            ProviderModel = "meshy-6-lite",
            ApiVersion = "v1",
            PolicyVersion = "v1.0",
            PricingVersion = "2026.01",
            AccountScope = "default",
            RequestedHeightMm = 110,
            Status = "Queued",
            ClientRequestId = Guid.NewGuid(),
            RequestFingerprint = SHA256.HashData(new byte[16]),
            InputHash = SHA256.HashData(new byte[32]),
            CreatedAtUtc = DateTime.UtcNow
        };

        var stage = new TheBoys.Domain.Entities.GenerationStage
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            Stage = "Generate",
            AttemptNo = 1,
            Provider = "Meshy",
            ProviderAccountScope = "default",
            Status = "Queued",
            Progress = 0,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        project.Jobs.Add(job);
        job.Stages.Add(stage);

        Assert.Single(project.Jobs);
        Assert.Single(job.Stages);
        Assert.Equal(userId, project.UserId);
        Assert.Equal(project.Id, job.ProjectId);
        Assert.Equal(job.Id, stage.JobId);
        Assert.True(stage.IsActive);
    }

    [Fact]
    public void SucceededModelVersionProducesImmutableAssets()
    {
        var projectId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var stlBytes = System.Text.Encoding.UTF8.GetBytes("solid test\nfacet normal 0 0 0\nouter loop\nvertex 0 0 0\nvertex 1 0 0\nvertex 0 1 0\nendloop\nendfacet\nendsolid");
        var glbBytes = new byte[100];

        var stlHash = SHA256.HashData(stlBytes);
        var glbHash = SHA256.HashData(glbBytes);

        var version = new ModelVersion
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            SourceJobId = jobId,
            VersionNo = 1,
            GeometryHash = stlHash,
            WidthMm = 55.5m,
            DepthMm = 44.2m,
            HeightMm = 100.0m,
            VolumeCm3 = 12.34m,
            IsWatertight = true,
            PrintabilityStatus = "healthy",
            AnalysisCoverage = "GeometryAndThickness",
            IsPublished = true,
            PublishedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        };

        var glbAsset = new ModelAsset
        {
            Id = Guid.NewGuid(),
            VersionId = version.Id,
            Format = "GLB",
            Purpose = "Preview",
            StorageKey = $"{jobId:N}/model.glb",
            Sha256 = glbHash,
            ByteLength = glbBytes.Length,
            CoordinateUnit = "m",
            UpAxis = "Y",
            CreatedAtUtc = DateTime.UtcNow
        };

        var stlAsset = new ModelAsset
        {
            Id = Guid.NewGuid(),
            VersionId = version.Id,
            Format = "STL",
            Purpose = "Print",
            StorageKey = $"{jobId:N}/model.stl",
            Sha256 = stlHash,
            ByteLength = stlBytes.Length,
            CoordinateUnit = "mm",
            UpAxis = "Z",
            CreatedAtUtc = DateTime.UtcNow
        };

        version.Assets.Add(glbAsset);
        version.Assets.Add(stlAsset);

        Assert.Equal(2, version.Assets.Count);
        Assert.True(version.IsWatertight);
        Assert.Equal(100.0m, version.HeightMm);
        Assert.Equal(stlHash, version.GeometryHash);

        var preview = version.Assets.Single(a => a.Purpose == "Preview");
        Assert.Equal("GLB", preview.Format);
        Assert.Equal("m", preview.CoordinateUnit);
        Assert.Equal(glbHash, preview.Sha256);

        var print = version.Assets.Single(a => a.Purpose == "Print");
        Assert.Equal("STL", print.Format);
        Assert.Equal("mm", print.CoordinateUnit);
        Assert.Equal(stlHash, print.Sha256);
    }
}

