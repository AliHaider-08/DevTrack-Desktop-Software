namespace Devzone.Infrastructure.Persistence.Entities;

public sealed class CategoryEntity
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public ICollection<ProductEntity> Products { get; set; } = new List<ProductEntity>();
}
