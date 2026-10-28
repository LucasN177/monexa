using monexa.Models.Enums;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace monexa.Models;

/// <summary>Dauerauftrag / Abo, aus dem zum Fälligkeitstermin automatisch Buchungen entstehen.</summary>
[Table("recurring_transactions")]
public class RecurringTransaction : BaseModel, IProjectEntity
{
    [PrimaryKey("id", true)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("project_id")]
    public Guid ProjectId { get; set; }

    [Column("account_id")]
    public Guid AccountId { get; set; }

    [Column("to_account_id")]
    public Guid? ToAccountId { get; set; }

    [Column("category_id")]
    public Guid? CategoryId { get; set; }

    [Column("description")]
    public string Description { get; set; } = "";

    [Column("amount")]
    public decimal Amount { get; set; }

    [Column("type")]
    public TransactionType Type { get; set; } = TransactionType.Ausgabe;

    [Column("interval")]
    public RecurrenceInterval Interval { get; set; } = RecurrenceInterval.Monatlich;

    /// <summary>Nächster Termin, zu dem gebucht wird.</summary>
    [Column("next_due")]
    public DateTime NextDue { get; set; } = DateTime.Today;

    [Column("end_date")]
    public DateTime? EndDate { get; set; }

    /// <summary>Fällige Termine automatisch beim Laden verbuchen.</summary>
    [Column("auto_book")]
    public bool AutoBook { get; set; } = true;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public RecurringTransaction Clone() => (RecurringTransaction)MemberwiseClone();
}
