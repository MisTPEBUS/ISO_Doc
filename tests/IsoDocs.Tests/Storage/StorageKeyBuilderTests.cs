using IsoDocument.Api.Storage;
using Xunit;

namespace IsoDocs.Tests.Storage;

public sealed class StorageKeyBuilderTests
{
    private static readonly Guid FileId = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");

    [Fact]
    public void BuildMainKey_UsesRequiredFormatAndSanitizesFileName()
    {
        var builder = new StorageKeyBuilder();

        var result = builder.BuildMainKey(
            "ACME",
            "HR-I-01",
            "1.0",
            FileId,
            "中文/報告:最終版.PDF");

        Assert.Equal(
            "store/ACME/HR-I-01/main/v1.0/00112233445566778899aabbccddeeff_中文報告_最終版.pdf",
            result);
    }

    [Fact]
    public void BuildAttachmentKey_UsesAttachmentIdentityVersionAndGuidNFormat()
    {
        var builder = new StorageKeyBuilder();

        var result = builder.BuildAttachmentKey(
            "ACME",
            "HR-I-01",
            "ATT-A",
            Guid.Empty,
            "2.3",
            FileId,
            "表單及附件.XLSX");

        Assert.Equal(
            "store/ACME/HR-I-01/att/ATT-A/v2.3/00112233445566778899aabbccddeeff_表單及附件.xlsx",
            result);
    }

    [Theory]
    [InlineData("CON.txt", "_CON.txt")]
    [InlineData("NUL.report.PDF", "_NUL.report.pdf")]
    [InlineData("bad/name\\part?.PDF. ", "badnamepart_.pdf")]
    [InlineData("...", "file")]
    public void ToSafeName_HandlesPortableFileNameRules(string input, string expected)
    {
        var builder = new StorageKeyBuilder();

        var result = builder.ToSafeName(input);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ToSafeName_TruncatesBaseNameToOneHundredFiftyCharacters()
    {
        var builder = new StorageKeyBuilder();
        var originalName = new string('文', 151) + ".PDF";

        var result = builder.ToSafeName(originalName);

        Assert.Equal(new string('文', 150) + ".pdf", result);
    }

    [Theory]
    [InlineData("../ACME")]
    [InlineData("ACME/OTHER")]
    [InlineData("ACME:OTHER")]
    public void BuildMainKey_WhenIdentifierIsInvalid_Throws(string companyCode)
    {
        var builder = new StorageKeyBuilder();

        Assert.Throws<ArgumentException>(() => builder.BuildMainKey(
            companyCode,
            "HR-I-01",
            "1.0",
            FileId,
            "document.pdf"));
    }

    [Fact]
    public void BuildGcpMainKey_UsesCompanyCodeCategoryAndImmutableFileId()
    {
        var builder = new StorageKeyBuilder();

        var result = builder.BuildGcpMainKey(
            "documents/iso", "ACME", "ISO 9001", "HR-I-01", "2.1", FileId, "程序.PDF");

        Assert.Equal(
            "documents/iso/ACME/ISO 9001/HR-I-01/main/v2.1/00112233445566778899aabbccddeeff.pdf",
            result);
    }

    [Fact]
    public void BuildGcpAttachmentKey_WithoutNumberUsesAttachmentId()
    {
        var builder = new StorageKeyBuilder();
        var attachmentId = Guid.Parse("12345678-1234-1234-1234-123456789abc");

        var result = builder.BuildGcpAttachmentKey(
            "documents/iso", "ACME", null, "HR-I-01", null,
            attachmentId, "1.0", FileId, "表單.xlsx");

        Assert.Equal(
            "documents/iso/ACME/_uncategorized/HR-I-01/att/_12345678123412341234123456789abc/v1.0/00112233445566778899aabbccddeeff.xlsx",
            result);
    }

    [Fact]
    public void BuildKeys_PreserveCompanyCodeCaseAndRepeatedSpaces()
    {
        var builder = new StorageKeyBuilder();

        var mainKey = builder.BuildGcpMainKey(
            "documents/iso", "Capital  Bus", "ISO9001", "GA-I-02", "1.6", FileId, "main.pdf");
        var attachmentKey = builder.BuildGcpAttachmentKey(
            "documents/iso", "Capital  Bus", "ISO9001", "GA-I-02", "ATT-1",
            Guid.Empty, "1.0", FileId, "attachment.pdf");
        var localKey = builder.BuildMainKey(
            "Capital  Bus", "GA-I-02", "1.6", FileId, "main.pdf");

        Assert.StartsWith("documents/iso/Capital  Bus/ISO9001/GA-I-02/main/", mainKey);
        Assert.StartsWith("documents/iso/Capital  Bus/ISO9001/GA-I-02/att/", attachmentKey);
        Assert.StartsWith("store/Capital  Bus/GA-I-02/main/", localKey);
    }

    [Theory]
    [InlineData("ISO/39001", "ISO%2F39001")]
    [InlineData("..", "%2E%2E")]
    [InlineData("中文分類", "中文分類")]
    public void BuildGcpMainKey_KeepsCategoryInOneSegment(string category, string expectedSegment)
    {
        var builder = new StorageKeyBuilder();

        var result = builder.BuildGcpMainKey(
            "documents/iso", "ACME", category, "DOC-1", "1.0", FileId, "original.pdf");

        Assert.Contains($"/{expectedSegment}/DOC-1/main/", result);
    }

    [Theory]
    [InlineData("../ACME")]
    [InlineData("ACME/OTHER")]
    [InlineData("ACME:OTHER")]
    [InlineData(" Capital  Bus")]
    [InlineData("Capital  Bus ")]
    [InlineData("Capital\tBus")]
    public void BuildGcpKeys_WhenCompanyCodeIsInvalid_Throw(string companyCode)
    {
        var builder = new StorageKeyBuilder();

        Assert.Throws<ArgumentException>(() => builder.BuildGcpMainKey(
            "documents/iso", companyCode, null, "DOC-1", "1.0", FileId, "document.pdf"));
        Assert.Throws<ArgumentException>(() => builder.BuildGcpAttachmentKey(
            "documents/iso", companyCode, null, "DOC-1", "ATT-1", Guid.Empty,
            "1.0", FileId, "attachment.pdf"));
    }
}
