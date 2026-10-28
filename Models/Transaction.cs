using monexa.Models.Enums;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace monexa.Models;

[Table("transactions")]
public class Transaction : BaseModel, IProjectEntity
{
    [PrimaryKey("id", true)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("project_id")]
    public Guid ProjectId { get; set; }

    [Column("account_id")]
    public Guid AccountId { get; set; }

    /// <summary>Zielkonto bei Umbuchungen.</summary>
    [Column("to_account_id")]
    public Guid? ToAccountId { get; set; }

    [Column("category_id")]
    public Guid? CategoryId { get; set; }

    [Column("description")]
    public string Description { get; set; } = "";

    /// <summary>Immer positiv; das Vorzeichen ergibt sich aus <see cref="Type"/>.</summary>
    [Column("amount")]
    public decimal Amount { get; set; }

    [Column("date")]
    public DateTime Date { get; set; } = DateTime.Today;

    [Column("type")]
    public TransactionType Type { get; set; } = TransactionType.Ausgabe;

    [Column("notes")]
    public string? Notes { get; set; }

    /// <summary>Gesetzt, wenn die Buchung automatisch aus einem Dauerauftrag erzeugt wurde.</summary>
    [Column("recurring_id")]
    public Guid? RecurringId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Transaction Clone() => (Transaction)MemberwiseClone();
}
