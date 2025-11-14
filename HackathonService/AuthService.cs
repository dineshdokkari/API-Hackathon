using HackathonHelper.Models;
using HackathonIRepository;
using HackathonIRepository.Helper;
using HackathonIService;
using HackathonModels.Authentication;
using HackathonModels.Registeruser;
using HackathonRepository;
using HackathonRepository.Helper;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
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

            byte[] passwordBytes = Convert.FromBase64String(request?.Password ?? string.Empty);
            string decodedPassword = Encoding.UTF8.GetString(passwordBytes);
            bool isValid = ValidateUserCredentials(request.Username, decodedPassword, user);
            if (!isValid) return null;
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
            //await _cache.SetStringAsync(GetCacheKey(refreshToken), payload, options);

            return new TokenResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                AccessTokenExpiresAt = accessExpiresAt,
                RefreshTokenExpiresAt = refreshExpires
            };
        }


        private bool ValidateUserCredentials(string email, string password, User userdetails)
        {

            try
            {
                // Attempt to decrypt the password
                string passwordString = userdetails.PasswordHash ?? string.Empty;
                var decryptedText = AesEncryptionHelper.Decrypt(passwordString);
                bool isValidUser = (password == decryptedText);
                return isValidUser;
            }
            catch (CryptographicException ex)
            {
                return false;
            }
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


        public async Task<RegisterUserResponse> RegisterUserAsync(RegisterUserRequest req)
        {
            // Basic validation
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
                return new RegisterUserResponse { Success = false, Message = "Username and password are required." };

            // Check exists
            var existing = await _userRepo.GetUserByUsernameAsync(req.Username);
            if (existing != null)
                return new RegisterUserResponse { Success = false, Message = "Username already exists." };

            // Get role id for "User"
            var roleId = await _userRepo.GetRoleIdByNameAsync("User");
            if (roleId == null)
                return new RegisterUserResponse { Success = false, Message = "Default role 'User' not configured in DB." };

            byte[] passwordBytes = Convert.FromBase64String(req?.Password ?? string.Empty);
            string decodedPassword = Encoding.UTF8.GetString(passwordBytes);
            var cipherText = AesEncryptionHelper.Encrypt(decodedPassword);

            var newUser = new AppUser
            {
                FullName = req.FullName ?? string.Empty,
                Username = req.Username,
                PasswordHash = cipherText,
                RoleId = roleId.Value
            };

            // Create user
            await _userRepo.CreateUserWithRoleAsync(newUser);

            return new RegisterUserResponse { Success = true, Message = "User created successfully." };
        }
    }
}
