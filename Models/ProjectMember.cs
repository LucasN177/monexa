using System.Text.Json.Serialization;
using monexa.Models.Enums;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace monexa.Models;

/// <summary>Mitgliedschaft bzw. offene Einladung (UserId == null) in einem Projekt.</summary>
[Table("project_members")]
public class ProjectMember : BaseModel, IProjectEntity
{
    [PrimaryKey("id", true)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("project_id")]
    public Guid ProjectId { get; set; }

    [Column("user_id")]
    public Guid? UserId { get; set; }

    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [Column("role")]
    public ProjectRole Role { get; set; } = ProjectRole.Leser;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public bool IsPending => UserId == null;
}
