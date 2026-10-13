using GLMS.Core.Services;
using GLMS.Shared.Results;

namespace GLMS.UnitTests;

public class PdfFileValidatorTests
{
    private readonly PdfFileValidator _validator = new();

    [Fact]
    public async Task ValidPdf_IsAccepted()
    {
        var result = await _validator.ValidateAsync(TestData.File("agreement.pdf", "application/pdf", TestData.PdfBytes));
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task UppercaseExtension_IsAccepted()
    {
        var result = await _validator.ValidateAsync(TestData.File("AGREEMENT.PDF", "application/pdf", TestData.PdfBytes));
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task ExeFile_IsRejected()
    {
        var result = await _validator.ValidateAsync(TestData.File("setup.exe", "application/x-msdownload", TestData.ExeBytes));

        Assert.False(result.Succeeded);
        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        Assert.Contains("Only PDF files are allowed.", result.AllMessages);
    }

    [Fact]
    public async Task ExeRenamedToPdf_IsRejectedByContentCheck()
    {
        var result = await _validator.ValidateAsync(TestData.File("invoice.pdf", "application/pdf", TestData.ExeBytes));

        Assert.False(result.Succeeded);
        Assert.Contains("The file is not a valid PDF document.", result.AllMessages);
    }

    [Fact]
    public async Task PdfExtensionWithWrongContentType_IsRejected()
    {
        var result = await _validator.ValidateAsync(TestData.File("agreement.pdf", "application/octet-stream", TestData.PdfBytes));
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task NullFile_IsRejected()
    {
        var result = await _validator.ValidateAsync(null);

        Assert.False(result.Succeeded);
        Assert.Contains("Select a PDF file to upload.", result.AllMessages);
    }

    [Fact]
    public async Task EmptyFile_IsRejected()
    {
        var result = await _validator.ValidateAsync(TestData.File("empty.pdf", "application/pdf", Array.Empty<byte>()));
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task FileOverSizeLimit_IsRejected()
    {
        var upload = TestData.File("big.pdf", "application/pdf", TestData.PdfBytes) with { Length = PdfFileValidator.MaxFileSizeBytes + 1 };

        var result = await _validator.ValidateAsync(upload);

        Assert.False(result.Succeeded);
        Assert.Contains(result.AllMessages, m => m.Contains("too large"));
    }

    [Fact]
    public async Task Validation_RewindsStream_SoItCanBeSaved()
    {
        var upload = TestData.File("agreement.pdf", "application/pdf", TestData.PdfBytes);

        await _validator.ValidateAsync(upload);

        Assert.Equal(0, upload.Content.Position);
    }
}
