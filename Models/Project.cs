using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace monexa.Models;

[Table("projects")]
public class Project : BaseModel, IEntity
{
    [PrimaryKey("id", true)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Schlüssel aus <see cref="monexa.Services.UiHelpers.ProjectIcons"/>.</summary>
    [Column("icon")]
    public string Icon { get; set; } = "Privat";

    [Column("owner_id")]
    public Guid? OwnerId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
