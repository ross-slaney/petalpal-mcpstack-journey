using SqlOS.Fga.Interfaces;

namespace PetalPal.Sample.Api.Models;

public sealed class Garden : IHasResourceId
{
    public Guid Id { get; set; }
    public string ResourceId { get; set; } = string.Empty;
    public string OwnerSubjectId { get; set; } = string.Empty;
    public string Name { get; set; } = "Pocket Garden";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Plant> Plants { get; set; } = new List<Plant>();
}
