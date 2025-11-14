using HackathonModels.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HackathonIRepository.Helper
{
    public interface IJwtHandler
    {
        string CreateAccessToken(User user, out DateTime expiresAt);
        string CreateRefreshToken();
    }
}
