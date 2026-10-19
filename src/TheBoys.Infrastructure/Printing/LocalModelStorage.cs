using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TheBoys.Application.Printing;

namespace TheBoys.Infrastructure.Printing;

public sealed class LocalModelStorage(IHostEnvironment env, IConfiguration configuration,
    IHttpClientFactory clients, IOptions<MeshyOptions> options) : IModelStorage
{
    private readonly string root = Path.GetFullPath(configuration["Storage:Root"] ?? Path.Combine(env.ContentRootPath, "App_Data", "models"));
    private const long MaxAssetBytes = 200L * 1024 * 1024;

    public string ResolvePath(string key)
    {
        var path = Path.GetFullPath(Path.Combine(root, key));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Đường dẫn file không hợp lệ.");
        return path;
    }

    public async Task<string> SaveImageAsync(Guid id, byte[] data, string contentType, CancellationToken ct)
    {
        var extension = contentType switch { "image/png" => "png", "image/jpeg" => "jpg", "image/webp" => "webp", _ => throw new ArgumentException("Ảnh không hợp lệ.") };
        var key = $"{id:N}/input.{extension}";
        var path = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, data, ct);
        return key;
    }

    public async Task<string> ReadImageDataUriAsync(string key, CancellationToken ct)
    {
        var mime = Path.GetExtension(key) switch { ".png" => "image/png", ".jpg" => "image/jpeg", ".webp" => "image/webp", _ => throw new ArgumentException("Ảnh không hợp lệ.") };
        return $"data:{mime};base64,{Convert.ToBase64String(await File.ReadAllBytesAsync(ResolvePath(key), ct))}";
    }

    public async Task<string> DownloadAsync(Guid id, string kind, string url, CancellationToken ct)
    {
        if (kind is not ("glb" or "stl")) throw new ArgumentException("Định dạng không hợp lệ.");
        var uri = new Uri(url);
        // Dedicated client: no Meshy Authorization header is ever sent to asset hosts.
        var client = clients.CreateClient("MeshyAssets");
        for (var redirects = 0; redirects <= 3; redirects++)
        {
            if (uri.Scheme != "https" || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo)
                || !options.Value.AssetHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
                throw new InvalidDataException("Host tải model không thuộc danh sách cho phép.");
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is { } location)
            { uri = location.IsAbsoluteUri ? location : new Uri(uri, location); continue; }
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaxAssetBytes) throw new InvalidDataException("Model vượt giới hạn 200 MB.");
            var key = $"{id:N}/model.{kind}";
            var path = ResolvePath(key);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temp = path + ".tmp";
            try
            {
                await using (var input = await response.Content.ReadAsStreamAsync(ct))
                await using (var output = File.Create(temp))
                {
                    var buffer = new byte[81920];
                    long total = 0;
                    int read;
                    while ((read = await input.ReadAsync(buffer, ct)) > 0)
                    {
                        total += read;
                        if (total > MaxAssetBytes) throw new InvalidDataException("Model vượt giới hạn 200 MB.");
                        await output.WriteAsync(buffer.AsMemory(0, read), ct);
                    }
                    if (total < 12) throw new InvalidDataException("File model rỗng hoặc hỏng.");
                }
                if (kind == "glb")
                {
                    using var reader = new BinaryReader(File.OpenRead(temp));
                    if (reader.ReadUInt32() != 0x46546c67 || reader.ReadUInt32() != 2 || reader.ReadUInt32() != reader.BaseStream.Length)
                        throw new InvalidDataException("Meshy không trả GLB hợp lệ.");
                }
                File.Move(temp, path, overwrite: true);
                return key;
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        throw new InvalidDataException("Quá nhiều chuyển hướng tải model.");
    }
}
