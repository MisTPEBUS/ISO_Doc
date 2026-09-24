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
    public void BuildGcpMainKey_UsesCompanyIdCategoryAndImmutableFileId()
    {
        var builder = new StorageKeyBuilder();
        var companyId = Guid.Parse("aabbccdd-1122-3344-5566-77889900aabb");

        var result = builder.BuildGcpMainKey(
            "documents/iso", companyId, "ISO 9001", "HR-I-01", "2.1", FileId, "程序.PDF");

        Assert.Equal(
            "documents/iso/aabbccdd-1122-3344-5566-77889900aabb/ISO 9001/HR-I-01/main/v2.1/00112233445566778899aabbccddeeff.pdf",
            result);
    }

    [Fact]
    public void BuildGcpAttachmentKey_WithoutNumberUsesAttachmentId()
    {
        var builder = new StorageKeyBuilder();
        var companyId = Guid.Parse("aabbccdd-1122-3344-5566-77889900aabb");
        var attachmentId = Guid.Parse("12345678-1234-1234-1234-123456789abc");

        var result = builder.BuildGcpAttachmentKey(
            "documents/iso", companyId, null, "HR-I-01", null,
            attachmentId, "1.0", FileId, "表單.xlsx");

        Assert.Equal(
            "documents/iso/aabbccdd-1122-3344-5566-77889900aabb/_uncategorized/HR-I-01/att/_12345678123412341234123456789abc/v1.0/00112233445566778899aabbccddeeff.xlsx",
            result);
    }

    [Theory]
    [InlineData("ISO/39001", "ISO%2F39001")]
    [InlineData("..", "%2E%2E")]
    [InlineData("中文分類", "中文分類")]
    public void BuildGcpMainKey_KeepsCategoryInOneSegment(string category, string expectedSegment)
    {
        var builder = new StorageKeyBuilder();

        var result = builder.BuildGcpMainKey(
            "documents/iso", Guid.Empty, category, "DOC-1", "1.0", FileId, "original.pdf");

        Assert.Contains($"/{expectedSegment}/DOC-1/main/", result);
    }
}
