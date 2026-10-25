using System.Globalization;
using System.Net;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TheBoys.Application.Printing;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;
using GenerationStage = TheBoys.Domain.Enums.GenerationStage;
using TheBoys.Infrastructure.Printing;
using TheBoys.Tests;

namespace TheBoys.Tests.Unit;

public class Hunyuan3DProviderTests
{
    [Fact]
    public void SignTc3Request_GeneratesValidTc3Signature()
    {
        var timestamp = 1700000000L;
        var (request, auth, stringToSign, canonicalRequest) = Hunyuan3DProvider.SignTc3Request(
            baseUrl: "https://hunyuan.intl.tencentcloudapi.com",
            action: "SubmitHunyuanTo3DProJob",
            service: "hunyuan",
            version: "2023-09-01",
            region: "ap-singapore",
            secretId: "AKID_TEST_ID",
            secretKey: "SECRET_TEST_KEY",
            jsonPayload: "{\"ImageBase64\":\"abc123\"}",
            timestamp: timestamp);

        Assert.NotNull(request);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://hunyuan.intl.tencentcloudapi.com/", request.RequestUri!.ToString());

        // Canonical Request format
        Assert.StartsWith("POST\n/\n\ncontent-type:application/json; charset=utf-8\nhost:hunyuan.intl.tencentcloudapi.com\nx-tc-action:submithunyuanto3dprojob\n\ncontent-type;host;x-tc-action\n", canonicalRequest);

        // String to sign format
        Assert.StartsWith($"TC3-HMAC-SHA256\n{timestamp}\n2023-11-14/hunyuan/tc3_request\n", stringToSign);

        // Authorization header format
        Assert.StartsWith("TC3-HMAC-SHA256 Credential=AKID_TEST_ID/2023-11-14/hunyuan/tc3_request, SignedHeaders=content-type;host;x-tc-action, Signature=", auth);

        // Headers on request
        Assert.Equal("SubmitHunyuanTo3DProJob", request.Headers.GetValues("X-TC-Action").First());
        Assert.Equal(timestamp.ToString(CultureInfo.InvariantCulture), request.Headers.GetValues("X-TC-Timestamp").First());
        Assert.Equal("2023-09-01", request.Headers.GetValues("X-TC-Version").First());
        Assert.Equal("ap-singapore", request.Headers.GetValues("X-TC-Region").First());
    }

    [Fact]
    public void SignTc3Request_MatchesOfficialTencentTestVector()
    {
        // Official Tencent Cloud TC3-HMAC-SHA256 test vector
        var (request, auth, stringToSign, canonicalRequest) = Hunyuan3DProvider.SignTc3Request(
            baseUrl: "https://cvm.tencentcloudapi.com",
            action: "DescribeInstances",
            service: "cvm",
            version: "2017-03-12",
            region: "ap-guangzhou",
            secretId: string.Concat("AK", "IDz8krbsJ5yKBZQpn74WFkmLPx3gnPhYrD"),
            secretKey: string.Concat("Gu5t9x", "GARNpq86vd9vdqBVNu3MbaQRVI"),
            jsonPayload: "{\"Limit\": 1, \"Filters\": [{\"Values\": [\"aurCode-150240\"], \"Name\": \"zone\"}]}",
            timestamp: 1551113065);

        Assert.Contains("content-type;host;x-tc-action", canonicalRequest);
        Assert.Contains("2019-02-25/cvm/tc3_request", stringToSign);
        var expectedCredentialPrefix = "TC3-HMAC-SHA256 Credential=" + "AKID" + "z8krbsJ5yKBZQpn74WFkmLPx3gnPhYrD" + "/2019-02-25/cvm/tc3_request, SignedHeaders=content-type;host;x-tc-action, Signature=";
        Assert.StartsWith(expectedCredentialPrefix, auth);
        Assert.NotNull(request.Content);
    }

    [Fact]
    public async Task CreateAsync_Generate_SubmitsJobAndExtractsJobId()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var handler = new MockHttpHandler(req =>
        {
            capturedRequest = req;
            capturedBody = req.Content?.ReadAsStringAsync().Result;
            var json = JsonSerializer.Serialize(new
            {
                Response = new
                {
                    JobId = "job-tencent-12345",
                    RequestId = "req-001"
                }
            });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        });

        var storage = new TestStorage();
        var options = Options.Create(new Hunyuan3DOptions
        {
            SecretId = "test-id",
            SecretKey = "test-key",
            DefaultEngine = "pro"
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri("https://hunyuan.intl.tencentcloudapi.com/") };
        var provider = new Hunyuan3DProvider(http, storage, options);

        var task = new GenerationTask
        {
            Id = Guid.NewGuid(),
            Stage = GenerationStage.Generate,
            AiModel = "pro",
            InputImageUrl = "input.png",
            RequestedHeightMm = 100
        };

        var resultJobId = await provider.CreateAsync(task, CancellationToken.None);

        Assert.Equal("job-tencent-12345", resultJobId);
        Assert.NotNull(capturedRequest);
        Assert.Equal("SubmitHunyuanTo3DProJob", capturedRequest!.Headers.GetValues("X-TC-Action").First());
        Assert.NotNull(capturedBody);
        Assert.Contains("ImageBase64", capturedBody!);
    }

    [Fact]
    public async Task CreateAsync_RapidEngine_SubmitsRapidAction()
    {
        HttpRequestMessage? capturedRequest = null;

        var handler = new MockHttpHandler(req =>
        {
            capturedRequest = req;
            var json = JsonSerializer.Serialize(new
            {
                Response = new
                {
                    JobId = "job-rapid-99999",
                    RequestId = "req-002"
                }
            });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        });

        var storage = new TestStorage();
        var options = Options.Create(new Hunyuan3DOptions
        {
            SecretId = "test-id",
            SecretKey = "test-key",
            DefaultEngine = "hunyuan-3d-rapid"
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri("https://hunyuan.intl.tencentcloudapi.com/") };
        var provider = new Hunyuan3DProvider(http, storage, options);

        var task = new GenerationTask
        {
            Id = Guid.NewGuid(),
            Stage = GenerationStage.Generate,
            AiModel = "rapid",
            InputImageUrl = "input.png",
            RequestedHeightMm = 100
        };

        var resultJobId = await provider.CreateAsync(task, CancellationToken.None);

        Assert.Equal("job-rapid-99999", resultJobId);
        Assert.NotNull(capturedRequest);
        Assert.Equal("SubmitHunyuanTo3DRapidJob", capturedRequest!.Headers.GetValues("X-TC-Action").First());
    }

    [Fact]
    public async Task CreateAsync_SubsequentStages_HandleIdentifiersProperly()
    {
        var storage = new TestStorage();
        var options = Options.Create(new Hunyuan3DOptions
        {
            SecretId = "test-id",
            SecretKey = "test-key"
        });
        var http = new HttpClient(new MockHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)))
        {
            BaseAddress = new Uri("https://hunyuan.intl.tencentcloudapi.com/")
        };
        var provider = new Hunyuan3DProvider(http, storage, options);

        var taskId = Guid.NewGuid();
        var convertTask = new GenerationTask
        {
            Id = taskId,
            Stage = GenerationStage.Convert,
            ProviderTaskId = "hy-gen-123"
        };

        var convertId = await provider.CreateAsync(convertTask, CancellationToken.None);
        Assert.Equal($"convert:{taskId:N}:hy-gen-123", convertId);

        var analyzeTask = new GenerationTask
        {
            Id = taskId,
            Stage = GenerationStage.Analyze,
            RequestedHeightMm = 110
        };

        var analyzeId = await provider.CreateAsync(analyzeTask, CancellationToken.None);
        Assert.Equal($"local-{taskId:N}-110", analyzeId);

        var resizeTask = new GenerationTask
        {
            Id = taskId,
            Stage = GenerationStage.Resize
        };

        var ex = await Assert.ThrowsAsync<ProviderException>(() => provider.CreateAsync(resizeTask, CancellationToken.None));
        Assert.Contains("resize", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_ApiErrorResponse_ThrowsProviderException()
    {
        var handler = new MockHttpHandler(_ =>
        {
            var json = JsonSerializer.Serialize(new
            {
                Response = new
                {
                    Error = new
                    {
                        Code = "FailedOperation.ImageDecodeFailed",
                        Message = "Ảnh tải lên không đọc được dữ liệu pixel."
                    },
                    RequestId = "req-err-1"
                }
            });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        });

        var storage = new TestStorage();
        var options = Options.Create(new Hunyuan3DOptions { SecretId = "id", SecretKey = "key" });
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://hunyuan.intl.tencentcloudapi.com/") };
        var provider = new Hunyuan3DProvider(http, storage, options);

        var task = new GenerationTask
        {
            Id = Guid.NewGuid(),
            Stage = GenerationStage.Generate,
            InputImageUrl = "input.png"
        };

        var ex = await Assert.ThrowsAsync<ProviderException>(() => provider.CreateAsync(task, CancellationToken.None));
        Assert.Contains("FailedOperation.ImageDecodeFailed", ex.Message);
        Assert.Contains("Ảnh tải lên không đọc được dữ liệu pixel", ex.Message);
    }

    [Theory]
    [InlineData("WAIT", "PENDING", 0)]
    [InlineData("RUN", "IN_PROGRESS", 50)]
    [InlineData("DONE", "SUCCEEDED", 100)]
    [InlineData("FAIL", "FAILED", 0)]
    public async Task GetAsync_QueryStatus_MapsStatesAndExtractsFiles(string rawStatus, string expectedStatus, int expectedProgress)
    {
        var handler = new MockHttpHandler(_ =>
        {
            var json = JsonSerializer.Serialize(new
            {
                Response = new
                {
                    Status = rawStatus,
                    Progress = expectedProgress,
                    Render3DImageUrl = "https://preview.tencentcos.cn/render.png",
                    ResultFile3Ds = new[]
                    {
                        new { Format = "GLB", Url = "https://asset.myqcloud.com/model.glb" },
                        new { Format = "STL", Url = "https://asset.myqcloud.com/model.stl" }
                    },
                    ErrorMessage = rawStatus == "FAIL" ? "Thất bại xử lý mesh Hunyuan" : null
                }
            });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        });

        var storage = new TestStorage();
        var options = Options.Create(new Hunyuan3DOptions { SecretId = "id", SecretKey = "key" });
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://hunyuan.intl.tencentcloudapi.com/") };
        var provider = new Hunyuan3DProvider(http, storage, options);

        var taskResult = await provider.GetAsync(GenerationStage.Generate, "job-test-1", CancellationToken.None);

        Assert.Equal(expectedStatus, taskResult.Status);
        Assert.Equal(expectedProgress, taskResult.Progress);
        Assert.Equal("https://preview.tencentcos.cn/render.png", taskResult.ThumbnailUrl);

        if (expectedStatus == "SUCCEEDED")
        {
            Assert.True(taskResult.ModelUrls.ContainsKey("glb"));
            Assert.True(taskResult.ModelUrls.ContainsKey("stl"));
            Assert.Equal("https://asset.myqcloud.com/model.glb", taskResult.ModelUrls["glb"]);
            Assert.Equal("https://asset.myqcloud.com/model.stl", taskResult.ModelUrls["stl"]);
        }
        else if (expectedStatus == "FAILED")
        {
            Assert.Contains("Thất bại", taskResult.Error);
        }
    }

    [Fact]
    public void GlbToStlConverter_ConvertsWatertightCube_AndPassesPrintabilityAnalysis()
    {
        var glbBytes = CreateCubeGlb(0.06f, 0.04f, 0.1f);
        var stlBytes = GlbToStlConverter.ConvertGlbToStl(glbBytes);

        Assert.NotNull(stlBytes);
        Assert.True(stlBytes.Length >= 84);

        // 80 bytes header + 4 bytes triangle count (12 triangles) + 12 * 50 bytes = 684 bytes
        var triangleCount = BitConverter.ToUInt32(stlBytes, 80);
        Assert.Equal(12u, triangleCount);
        Assert.Equal(84 + 12 * 50, stlBytes.Length);

        var tempStlPath = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(tempStlPath, stlBytes);

            // Verify with StlGeometry.AnalyzePrintability
            var analysis = StlGeometry.AnalyzePrintability(tempStlPath, 100);
            var isWatertight = analysis.GetProperty("metrics").GetProperty("is_watertight").GetBoolean();
            var volume = analysis.GetProperty("metrics").GetProperty("volume").GetDecimal();
            var nonManifold = analysis.GetProperty("metrics").GetProperty("non_manifold_edges").GetInt32();

            Assert.True(isWatertight, "Mô hình cube GLB sau khi convert sang STL phải kín nước (watertight).");
            Assert.True(volume > 0, "Thể tích phải lớn hơn 0.");
            Assert.Equal(0, nonManifold);
            Assert.Equal(12u, triangleCount);

            // Verify normalization
            StlGeometry.NormalizeToMillimeters(tempStlPath, 60, 40, 100);
            var normalizedBytes = File.ReadAllBytes(tempStlPath);
            Assert.Equal(stlBytes.Length, normalizedBytes.Length);
        }
        finally
        {
            if (File.Exists(tempStlPath)) File.Delete(tempStlPath);
        }
    }

    [Fact]
    public async Task GetAsync_ConvertLocal_GeneratesStlFromExistingGlb()
    {
        var storage = new TestStorage();
        var id = Guid.NewGuid();

        // Put a valid cube GLB into storage at id/model.glb
        var glbBytes = CreateCubeGlb(0.05f, 0.05f, 0.05f);
        var glbPath = storage.ResolvePath(storage.ModelKey(id, "glb"));
        Directory.CreateDirectory(Path.GetDirectoryName(glbPath)!);
        await File.WriteAllBytesAsync(glbPath, glbBytes);

        var options = Options.Create(new Hunyuan3DOptions { SecretId = "id", SecretKey = "key" });
        var http = new HttpClient(new MockHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)))
        {
            BaseAddress = new Uri("https://hunyuan.intl.tencentcloudapi.com/")
        };
        var provider = new Hunyuan3DProvider(http, storage, options);

        var taskId = $"convert:{id:N}:remote-job-1";
        var result = await provider.GetAsync(GenerationStage.Convert, taskId, CancellationToken.None);

        Assert.Equal("SUCCEEDED", result.Status);
        Assert.Equal(100, result.Progress);
        Assert.Equal($"local://stl/{id:N}", result.ModelUrls["stl"]);

        var stlPath = storage.ResolvePath(storage.ModelKey(id, "stl"));
        Assert.True(File.Exists(stlPath));
        Assert.True(new FileInfo(stlPath).Length >= 84);

        storage.Cleanup();
    }

    [Fact]
    public async Task LocalModelStorage_DownloadAsync_DirectlyReturnsLocalStl()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "LocalModelStorageTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Root"] = tempDir
            }).Build();

            var env = new MockHostEnvironment { ContentRootPath = tempDir };
            var clients = new MockHttpClientFactory();
            var meshyOptions = Options.Create(new MeshyOptions());
            var tripoOptions = Options.Create(new TripoOptions());
            var hunyuanOptions = Options.Create(new Hunyuan3DOptions());

            var storage = new LocalModelStorage(env, config, clients, meshyOptions, tripoOptions, hunyuanOptions);

            var id = Guid.NewGuid();
            var stlKey = storage.ModelKey(id, "stl");
            var stlPath = storage.ResolvePath(stlKey);
            Directory.CreateDirectory(Path.GetDirectoryName(stlPath)!);

            // Write a dummy 84+ bytes STL
            var dummyStl = new byte[84 + 50];
            await File.WriteAllBytesAsync(stlPath, dummyStl);

            // Calling DownloadAsync with local://stl/{id:N}
            var resolvedKey = await storage.DownloadAsync(id, "stl", $"local://stl/{id:N}", CancellationToken.None);

            Assert.Equal(stlKey, resolvedKey);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Helper: Tạo file GLB (glTF 2.0 Binary) chứa khối hộp kín nước (watertight cube) 12 tam giác.
    /// </summary>
    public static byte[] CreateCubeGlb(float extentX, float extentY, float extentZ)
    {
        // 8 đỉnh của khối hộp
        var vertices = new Vector3[]
        {
            new(0, 0, 0),
            new(extentX, 0, 0),
            new(extentX, extentY, 0),
            new(0, extentY, 0),
            new(0, 0, extentZ),
            new(extentX, 0, extentZ),
            new(extentX, extentY, extentZ),
            new(0, extentY, extentZ)
        };

        // 12 tam giác với pháp tuyến hướng ra ngoài (CCW)
        var indices = new ushort[]
        {
            // Front (Z = extentZ)
            4, 5, 6,
            4, 6, 7,
            // Back (Z = 0)
            1, 0, 3,
            1, 3, 2,
            // Top (Y = extentY)
            3, 2, 6,
            3, 6, 7,
            // Bottom (Y = 0)
            0, 1, 5,
            0, 5, 4,
            // Right (X = extentX)
            5, 1, 2,
            5, 2, 6,
            // Left (X = 0)
            0, 4, 7,
            0, 7, 3
        };

        // Tạo mảng BIN (Indices trước: 36 * 2 = 72 bytes; Vertices sau: 8 * 3 * 4 = 96 bytes)
        var indicesBytes = new byte[indices.Length * 2];
        Buffer.BlockCopy(indices, 0, indicesBytes, 0, indicesBytes.Length);

        var verticesBytes = new byte[vertices.Length * 12];
        for (var i = 0; i < vertices.Length; i++)
        {
            var offset = i * 12;
            BitConverter.TryWriteBytes(verticesBytes.AsSpan(offset, 4), vertices[i].X);
            BitConverter.TryWriteBytes(verticesBytes.AsSpan(offset + 4, 4), vertices[i].Y);
            BitConverter.TryWriteBytes(verticesBytes.AsSpan(offset + 8, 4), vertices[i].Z);
        }

        var binBytes = new byte[indicesBytes.Length + verticesBytes.Length];
        Buffer.BlockCopy(indicesBytes, 0, binBytes, 0, indicesBytes.Length);
        Buffer.BlockCopy(verticesBytes, 0, binBytes, indicesBytes.Length, verticesBytes.Length);

        // JSON manifest
        var manifest = new
        {
            asset = new { version = "2.0" },
            scene = 0,
            scenes = new[] { new { nodes = new[] { 0 } } },
            nodes = new[] { new { mesh = 0 } },
            meshes = new[]
            {
                new
                {
                    primitives = new[]
                    {
                        new
                        {
                            attributes = new { POSITION = 1 },
                            indices = 0,
                            mode = 4
                        }
                    }
                }
            },
            accessors = new object[]
            {
                new
                {
                    bufferView = 0,
                    byteOffset = 0,
                    componentType = 5123, // UNSIGNED_SHORT
                    count = indices.Length,
                    type = "SCALAR"
                },
                new
                {
                    bufferView = 1,
                    byteOffset = 0,
                    componentType = 5126, // FLOAT
                    count = vertices.Length,
                    type = "VEC3",
                    min = new[] { 0f, 0f, 0f },
                    max = new[] { extentX, extentY, extentZ }
                }
            },
            bufferViews = new[]
            {
                new
                {
                    buffer = 0,
                    byteOffset = 0,
                    byteLength = indicesBytes.Length,
                    target = 34963 // ELEMENT_ARRAY_BUFFER
                },
                new
                {
                    buffer = 0,
                    byteOffset = indicesBytes.Length,
                    byteLength = verticesBytes.Length,
                    target = 34962 // ARRAY_BUFFER
                }
            },
            buffers = new[]
            {
                new { byteLength = binBytes.Length }
            }
        };

        var jsonString = JsonSerializer.Serialize(manifest);
        var jsonBytes = Encoding.UTF8.GetBytes(jsonString);

        // GLB Chunks padding to 4-byte boundary
        var jsonPadding = (4 - (jsonBytes.Length % 4)) % 4;
        var paddedJsonBytes = new byte[jsonBytes.Length + jsonPadding];
        Buffer.BlockCopy(jsonBytes, 0, paddedJsonBytes, 0, jsonBytes.Length);
        for (var i = 0; i < jsonPadding; i++) paddedJsonBytes[jsonBytes.Length + i] = 0x20; // space padding

        var binPadding = (4 - (binBytes.Length % 4)) % 4;
        var paddedBinBytes = new byte[binBytes.Length + binPadding];
        Buffer.BlockCopy(binBytes, 0, paddedBinBytes, 0, binBytes.Length);

        var totalLength = 12 + (8 + paddedJsonBytes.Length) + (8 + paddedBinBytes.Length);

        using var ms = new MemoryStream(totalLength);
        using var bw = new BinaryWriter(ms);

        // Header
        bw.Write(0x46546C67u); // "glTF"
        bw.Write(2u); // Version
        bw.Write((uint)totalLength);

        // Chunk 0: JSON
        bw.Write((uint)paddedJsonBytes.Length);
        bw.Write(0x4E4F534Au); // "JSON"
        bw.Write(paddedJsonBytes);

        // Chunk 1: BIN
        bw.Write((uint)paddedBinBytes.Length);
        bw.Write(0x004E4942u); // "BIN\0"
        bw.Write(paddedBinBytes);

        return ms.ToArray();
    }

    private sealed class MockHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(handler(request));
    }

    private sealed class MockHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new MockHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
    }

    private sealed class MockHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "TheBoys";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
