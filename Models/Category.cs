using monexa.Models.Enums;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace monexa.Models;

[Table("categories")]
public class Category : BaseModel, IProjectEntity
{
    [PrimaryKey("id", true)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("project_id")]
    public Guid ProjectId { get; set; }

    [Column("name")]
    public string Name { get; set; } = "";

    [Column("description")]
    public string? Description { get; set; }

    [Column("type")]
    public CategoryType Type { get; set; } = CategoryType.Ausgabe;

    /// <summary>Schlüssel aus <see cref="monexa.Services.UiHelpers.CategoryIcons"/>.</summary>
    [Column("icon")]
    public string Icon { get; set; } = "Sonstiges";

    /// <summary>Name eines MudBlazor-<c>Color</c>-Werts, z. B. "Primary".</summary>
    [Column("color")]
    public string Color { get; set; } = "Primary";

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Category Clone() => (Category)MemberwiseClone();
}
