using GLMS.Shared.Dtos;
using GLMS.Shared.Results;

namespace GLMS.Core.Services;

public interface IFileValidator
{
    Task<ServiceResult> ValidateAsync(FileUpload? file, CancellationToken ct = default);
}

/// <summary>
/// Accepts PDFs only. Checks the extension, the declared content type and the file's first bytes
/// ("%PDF-"), so a renamed .exe is rejected even if it has a .pdf extension.
/// </summary>
public class PdfFileValidator : IFileValidator
{
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;
    public const string FieldName = "SignedAgreement";

    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();
    private static readonly string[] AllowedContentTypes = { "application/pdf", "application/x-pdf" };

    public async Task<ServiceResult> ValidateAsync(FileUpload? file, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return Invalid("Select a PDF file to upload.");

        if (file.Length > MaxFileSizeBytes)
            return Invalid($"The file is too large. The maximum size is {MaxFileSizeBytes / (1024 * 1024)} MB.");

        if (!string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
            return Invalid("Only PDF files are allowed.");

        if (!AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            return Invalid("Only PDF files are allowed (invalid content type).");

        if (!await HasPdfSignatureAsync(file.Content, ct))
            return Invalid("The file is not a valid PDF document.");

        return ServiceResult.Success();
    }

    private static async Task<bool> HasPdfSignatureAsync(Stream content, CancellationToken ct)
    {
        if (!content.CanSeek)
            throw new ArgumentException("File content must be seekable for validation.", nameof(content));

        var header = new byte[PdfSignature.Length];
        content.Position = 0;
        var read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        content.Position = 0;
        return read == header.Length && header.AsSpan().SequenceEqual(PdfSignature);
    }

    private static ServiceResult Invalid(string message) =>
        ServiceResult.Fail(ServiceErrorType.Validation, message, FieldName);
}
