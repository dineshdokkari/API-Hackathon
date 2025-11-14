using HackathonModels.Authentication;
using HackathonModels.Registeruser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HackathonIService
{
    public interface IAuthService
    {
        Task<TokenResponse?> LoginAsync(LoginRequest request, string remoteIp = "unknown");
        Task<TokenResponse?> RefreshTokenAsync(string refreshToken, string remoteIp = "unknown");
        Task<bool> RevokeRefreshTokenAsync(string refreshToken);

        Task<RegisterUserResponse> RegisterUserAsync(RegisterUserRequest req);
    }
}
