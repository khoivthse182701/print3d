namespace TheBoys.Domain.Entities;

public sealed class ModelProject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Name { get; set; } = default!;
    public DateTime? ArchivedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    // Navigation
    public User User { get; set; } = default!;
    public ICollection<GenerationJob> Jobs { get; set; } = new List<GenerationJob>();
    public ICollection<ModelVersion> Versions { get; set; } = new List<ModelVersion>();
}

