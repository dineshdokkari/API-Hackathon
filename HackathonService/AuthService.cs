using HackathonHelper.Models;
using HackathonIRepository;
using HackathonIRepository.Helper;
using HackathonIService;
using HackathonModels.Authentication;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace HackathonService
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepo;
        private readonly IDistributedCache _cache;
        private readonly IJwtHandler _jwt;
        private readonly JwtSettings _jwtSettings;

        public AuthService(IUserRepository userRepo, IDistributedCache cache, IJwtHandler jwt, IOptions<JwtSettings> jwtOptions)
        {
            _userRepo = userRepo;
            _cache = cache;
            _jwt = jwt;
            _jwtSettings = jwtOptions.Value;
        }

        public async Task<TokenResponse?> LoginAsync(LoginRequest request, string remoteIp = "unknown")
        {
            var user = await _userRepo.GetUserByUsernameAsync(request.Username);
            if (user == null) return null;

            // Verify password using BCrypt
            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash)) return null;

            var accessToken = _jwt.CreateAccessToken(user, out DateTime accessExpiresAt);
            var refreshToken = _jwt.CreateRefreshToken();
            var refreshExpires = DateTime.UtcNow.AddMinutes(_jwtSettings.RefreshTokenMinutes);

            // Store refreshToken -> userId mapping in Redis (or other IDistributedCache) with expiry
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = refreshExpires
            };

            // We store a small JSON payload in Redis if you want to store extra meta
            var payload = JsonSerializer.Serialize(new { UserId = user.Id, RemoteIp = remoteIp });
            await _cache.SetStringAsync(GetCacheKey(refreshToken), payload, options);

            return new TokenResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                AccessTokenExpiresAt = accessExpiresAt,
                RefreshTokenExpiresAt = refreshExpires
            };
        }

        public async Task<TokenResponse?> RefreshTokenAsync(string refreshToken, string remoteIp = "unknown")
        {
            // Validate the refresh token exists in cache
            var cacheVal = await _cache.GetStringAsync(GetCacheKey(refreshToken));
            if (string.IsNullOrEmpty(cacheVal))
                return null; // expired / invalid

            // parse payload (contains userId)
            var doc = JsonDocument.Parse(cacheVal);
            var userId = doc.RootElement.GetProperty("UserId").GetInt32();

            // fetch user from db (to get current role / username)
            var user = await _userRepo.GetUserByIdAsync(userId);
            if (user == null)
            {
                // cleanup just in case
                await _cache.RemoveAsync(GetCacheKey(refreshToken));
                return null;
            }

            // Rotate: delete current refresh token and create a new one
            await _cache.RemoveAsync(GetCacheKey(refreshToken));

            var newRefreshToken = _jwt.CreateRefreshToken();
            var newRefreshExpires = DateTime.UtcNow.AddMinutes(_jwtSettings.RefreshTokenMinutes);

            var options = new DistributedCacheEntryOptions { AbsoluteExpiration = newRefreshExpires };
            var newPayload = JsonSerializer.Serialize(new { UserId = user.Id, RemoteIp = remoteIp });
            await _cache.SetStringAsync(GetCacheKey(newRefreshToken), newPayload, options);

            var accessToken = _jwt.CreateAccessToken(user, out DateTime accessExpiresAt);

            return new TokenResponse
            {
                AccessToken = accessToken,
                RefreshToken = newRefreshToken,
                AccessTokenExpiresAt = accessExpiresAt,
                RefreshTokenExpiresAt = newRefreshExpires
            };
        }

        public async Task<bool> RevokeRefreshTokenAsync(string refreshToken)
        {
            await _cache.RemoveAsync(GetCacheKey(refreshToken));
            return true;
        }

        private static string GetCacheKey(string refreshToken) => $"refresh:{refreshToken}";
    }
}
