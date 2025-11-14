using HackathonModels.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HackathonIService
{
    public interface IAuthenticationService
    {
        Task<TokenResponse?> LoginAsync(LoginRequest request, string remoteIp = "unknown");
        Task<TokenResponse?> RefreshTokenAsync(string refreshToken, string remoteIp = "unknown");
        Task<bool> RevokeRefreshTokenAsync(string refreshToken);
    }
}
