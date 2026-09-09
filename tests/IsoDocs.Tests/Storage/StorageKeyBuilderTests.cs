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
            "store/ACME/HR-I-01/v1.0/main/00112233445566778899aabbccddeeff_中文報告_最終版.pdf",
            result);
    }

    [Fact]
    public void BuildAttachmentKey_UsesTwoDigitSequenceAndGuidNFormat()
    {
        var builder = new StorageKeyBuilder();

        var result = builder.BuildAttachmentKey(
            "ACME",
            "HR-I-01",
            "2.3",
            3,
            FileId,
            "附件.XLSX");

        Assert.Equal(
            "store/ACME/HR-I-01/v2.3/att/03_00112233445566778899aabbccddeeff_附件.xlsx",
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
}
