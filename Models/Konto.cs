using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using monexa.Models.Enums;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace monexa.Models;

[Table("accounts")]
public class Konto : BaseModel, IProjectEntity
{
    [PrimaryKey("id", true)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("project_id")]
    public Guid ProjectId { get; set; }

    [Required(ErrorMessage = "Kontoname ist erforderlich")]
    [Column("name")]
    public string Name { get; set; } = "";

    [Column("type")]
    public AccountType Type { get; set; }

    [Column("iban")]
    public string IBAN { get; set; } = "";

    /// <summary>Kontostand vor der ersten erfassten Buchung.</summary>
    [Column("opening_balance")]
    public decimal OpeningBalance { get; set; }

    [Required]
    [Column("currency")]
    public string Currency { get; set; } = "EUR";

    [Required(ErrorMessage = "Bank ist erforderlich")]
    [Column("bank_name")]
    public string BankName { get; set; } = "";

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("description")]
    public string Description { get; set; } = "";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Aktueller Kontostand – wird vom FinanceState aus den Buchungen berechnet.</summary>
    [JsonIgnore]
    public decimal Balance { get; set; }

    public Konto Clone() => (Konto)MemberwiseClone();
}
