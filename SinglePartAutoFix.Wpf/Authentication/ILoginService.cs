using System.Threading.Tasks;

namespace SinglePartAutoFix.Wpf.Authentication
{
    internal interface ILoginService
    {
        Task<LoginResult> SignInAsync(LoginRequest request);
    }
}
