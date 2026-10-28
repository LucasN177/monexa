using monexa.Models.Enums;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace monexa.Models;

[Table("budgets")]
public class Budget : BaseModel, IProjectEntity
{
    [PrimaryKey("id", true)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("project_id")]
    public Guid ProjectId { get; set; }

    /// <summary>Kategorie, deren Ausgaben gegen das Limit laufen. Null = alle Ausgaben.</summary>
    [Column("category_id")]
    public Guid? CategoryId { get; set; }

    [Column("name")]
    public string Name { get; set; } = "";

    [Column("limit_amount")]
    public decimal Limit { get; set; }

    [Column("period")]
    public BudgetPeriod Period { get; set; } = BudgetPeriod.Monatlich;

    [Column("icon")]
    public string Icon { get; set; } = "Shopping";

    [Column("color")]
    public string Color { get; set; } = "Primary";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Budget Clone() => (Budget)MemberwiseClone();
}
