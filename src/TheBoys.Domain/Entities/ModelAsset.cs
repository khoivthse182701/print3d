namespace TheBoys.Domain.Entities;

public sealed class ModelAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VersionId { get; set; }
    public string Format { get; set; } = "GLB"; // GLB, STL, PNG, JPEG, WEBP, 3MF
    public string Purpose { get; set; } = "Preview"; // Preview, Print, Thumbnail
    public string StorageKey { get; set; } = default!;
    public byte[] Sha256 { get; set; } = [];
    public long ByteLength { get; set; }
    public string CoordinateUnit { get; set; } = "mm"; // mm, m, pixel
    public string? UpAxis { get; set; } // X, Y, Z
    public DateTime? PurgedAtUtc { get; set; }
    public DateTime? RetainUntilUtc { get; set; }
    public bool LegalHold { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public ModelVersion Version { get; set; } = default!;
}

