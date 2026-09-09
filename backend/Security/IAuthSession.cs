using IsoDocument.Api.Data.Entities;

namespace IsoDocument.Api.Security;

public interface IAuthSession
{
    Task SignInAsync(User user);

    Task SignOutAsync();

    void IssueAntiforgeryToken();
}
