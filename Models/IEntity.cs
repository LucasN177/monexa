namespace monexa.Models;

public interface IEntity
{
    Guid Id { get; set; }
}

/// <summary>Entität, die zu genau einem Projekt gehört (Mandantentrennung).</summary>
public interface IProjectEntity : IEntity
{
    Guid ProjectId { get; set; }
}
