using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HouseholdBudgetMate.Domain.Infrastructure;

namespace HouseholdBudgetMate.Domain.Entities;

public class LogEntry : IEntityId
{
    public int Id { get; set; }
    public string Message { get; set; } = null!;
    public string MessageTemplate { get; set; } = null!;
    public string Level { get; set; } = null!;
    public DateTime Timestamp { get; set; }
    public string? Exception { get; set; }
    public string? Properties { get; set; }
}