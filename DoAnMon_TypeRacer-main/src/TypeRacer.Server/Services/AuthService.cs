using TypeRacer.Server.Data;
using TypeRacer.Shared.Crypto;
using TypeRacer.Shared.Models;
using TypeRacer.Shared.Payloads.Auth;

namespace TypeRacer.Server.Services;

public class AuthService
{
    private readonly IUserRepository _userRepo;

    public AuthService(IUserRepository userRepo)
    {
        _userRepo = userRepo;
    }

    public async Task<LoginResponse> LoginAsync(string username, string password)
    {
        username = username?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            return new LoginResponse { Success = false, ErrorMessage = "Sai tên đăng nhập hoặc mật khẩu." };

        var user = await _userRepo.GetByUsernameAsync(username);
        if (user == null)
            return new LoginResponse { Success = false, ErrorMessage = "Sai tên đăng nhập hoặc mật khẩu." };

        var isValidPassword = HashHelper.VerifyStoredPassword(password, user.PasswordHash, out var needsUpgrade);
        if (!isValidPassword)
            return new LoginResponse { Success = false, ErrorMessage = "Sai tên đăng nhập hoặc mật khẩu." };

        if (needsUpgrade)
        {
            try
            {
                var upgradedHash = HashHelper.HashPasswordV2(password);
                await _userRepo.UpdatePasswordHashAsync(user.Id, upgradedHash);
            }
            catch
            {
                // Không chặn login nếu nâng cấp hash lỗi tạm thời.
            }
        }

        var sessionToken = HashHelper.GenerateSessionToken();

        return new LoginResponse
        {
            Success = true,
            SessionToken = sessionToken,
            User = new UserDto { Id = user.Id, Username = user.Username },
        };
    }

    public Task LogoutAsync(int userId)
    {
        // Không cần cập nhật DB — trạng thái online chỉ quản lý trong bộ nhớ
        return Task.CompletedTask;
    }

    public async Task<RegisterResponse> RegisterAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Length < 3 || username.Length > 50)
            return new RegisterResponse { Success = false, ErrorMessage = "Tên đăng nhập phải từ 3-50 ký tự." };

        // Chỉ cho phép chữ cái, số, gạch dưới, gạch ngang
        if (!System.Text.RegularExpressions.Regex.IsMatch(username, @"^[a-zA-Z0-9_\-]+$"))
            return new RegisterResponse { Success = false, ErrorMessage = "Tên đăng nhập chỉ chứa chữ cái, số, _ và -." };

        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            return new RegisterResponse { Success = false, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự." };

        if (await _userRepo.UsernameExistsAsync(username))
            return new RegisterResponse { Success = false, ErrorMessage = "Tên đăng nhập đã được sử dụng." };

        var hash = HashHelper.HashPasswordV3(password);
        try
        {
            await _userRepo.CreateAsync(username, hash);
        }
        catch (DuplicateUsernameException)
        {
            return new RegisterResponse { Success = false, ErrorMessage = "Tên đăng nhập đã được sử dụng." };
        }

        return new RegisterResponse { Success = true };
    }
}
