using HackathonModels.Authentication;
using HackathonModels.Registeruser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HackathonIRepository
{
    public interface IUserRepository
    {
        Task<User?> GetUserByUsernameAsync(string username);
        Task<User?> GetUserByIdAsync(int id);

        Task<int?> GetRoleIdByNameAsync(string roleName);
        Task CreateUserWithRoleAsync(AppUser user);
    }
}
