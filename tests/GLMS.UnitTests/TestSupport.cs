using GLMS.Core.Entities;
using GLMS.Shared.Dtos;
using GLMS.Shared.Enums;

namespace GLMS.UnitTests;

/// <summary>Deterministic clock so date-based rules (expiry, activation) can be tested.</summary>
public sealed class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _now;

    public FixedTimeProvider(DateTime utcNow) => _now = new DateTimeOffset(utcNow, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

internal static class TestData
{
    public static readonly DateTime Today = new(2026, 10, 13);

    public static Contract Contract(ContractStatus status, int id = 1, DateTime? endDate = null) => new()
    {
        Id = id,
        Title = $"Contract {id}",
        ClientId = 1,
        Client = new Client { Id = 1, Name = "Cape Freight" },
        StartDate = Today.AddMonths(-6),
        EndDate = endDate ?? Today.AddMonths(6),
        Status = status,
        ServiceLevel = ServiceLevel.Standard,
        SlaResponseHours = 72
    };

    public static ExchangeRateDto Rate(decimal rate, bool stale = false) =>
        new("USD", "ZAR", rate, Today, stale);

    public static FileUpload File(string name, string contentType, byte[] content) =>
        new(name, contentType, content.Length, new MemoryStream(content));

    public static byte[] PdfBytes => "%PDF-1.7\n1 0 obj\n<<>>\nendobj\n%%EOF"u8.ToArray();

    // "MZ" is the header of every Windows executable.
    public static byte[] ExeBytes => new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 };
}
