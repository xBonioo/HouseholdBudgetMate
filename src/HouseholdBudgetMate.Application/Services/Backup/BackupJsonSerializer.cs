using System.Text.Json;
using System.Text.Json.Serialization;
using HouseholdBudgetMate.Abstractions.Contracts.Backup.Dto;
using HouseholdBudgetMate.Application.Kernel.Exceptions;

namespace HouseholdBudgetMate.Application.Services.Backup;

internal static class BackupJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    public static byte[] SerializeToUtf8Bytes(BackupEnvelopeDto envelope)
    {
        return JsonSerializer.SerializeToUtf8Bytes(envelope, Options);
    }

    public static BackupEnvelopeDto Deserialize(ReadOnlySpan<byte> json)
    {
        var envelope = JsonSerializer.Deserialize<BackupEnvelopeDto>(json, Options)
            ?? throw new BadRequestException("Backup JSON is empty or invalid.");

        if (envelope.SchemaVersion != BackupEnvelopeDto.CurrentSchemaVersion)
        {
            throw new BadRequestException("Backup schema version is not supported.");
        }

        return envelope;
    }
}
