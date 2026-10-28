using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace monexa.Models;

[Table("savings_goals")]
public class SavingsGoal : BaseModel, IProjectEntity
{
    [PrimaryKey("id", true)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("project_id")]
    public Guid ProjectId { get; set; }

    [Column("name")]
    public string Name { get; set; } = "";

    [Column("target_amount")]
    public decimal TargetAmount { get; set; }

    [Column("current_amount")]
    public decimal CurrentAmount { get; set; }

    [Column("target_date")]
    public DateTime? TargetDate { get; set; }

    [Column("icon")]
    public string Icon { get; set; } = "Sparen";

    [Column("color")]
    public string Color { get; set; } = "Success";

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public SavingsGoal Clone() => (SavingsGoal)MemberwiseClone();
}
