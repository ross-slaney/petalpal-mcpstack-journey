using SqlOS.Fga.Interfaces;

namespace PetalPal.Sample.Api.Models;

public sealed class Plant : IHasResourceId
{
    public Guid Id { get; set; }
    public Guid GardenId { get; set; }
    public Garden? Garden { get; set; }
    public string ResourceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Mood { get; set; } = "cozy";
    public string Note { get; set; } = string.Empty;
    public int WaterCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastWateredAt { get; set; }
}
