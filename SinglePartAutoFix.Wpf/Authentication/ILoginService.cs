using System.Threading.Tasks;

namespace SinglePartAutoFix.Wpf.Authentication
{
    internal interface ILoginService
    {
        bool IsAuthenticated { get; }
        Task<LoginResult> SignInAsync(LoginRequest request);
    }
}
