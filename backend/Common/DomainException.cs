namespace IsoDocument.Api.Common;

public class DomainException : Exception
{
    public DomainException(Result result)
        : base(result.Detail ?? result.Title)
    {
        if (result.IsSuccess)
        {
            throw new ArgumentException("A domain exception must carry a failed result.", nameof(result));
        }

        Result = result;
    }

    public Result Result { get; }
}
