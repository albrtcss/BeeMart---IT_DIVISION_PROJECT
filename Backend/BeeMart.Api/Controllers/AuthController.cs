using BeeMart.Api.Data;
using BeeMart.Api.DTOs;
using BeeMart.Api.Models;
using BeeMart.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeeMart.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly BeeMartDbContext _db;
    private readonly JwtTokenService _jwtTokenService;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public AuthController(BeeMartDbContext db, JwtTokenService jwtTokenService)
    {
        _db = db;
        _jwtTokenService = jwtTokenService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var username = request.Username.Trim();
        var fullName = request.FullName.Trim();

        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(fullName) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new
            {
                message = "Username, nama lengkap, dan password wajib diisi."
            });
        }

        if (username.Length > 50 || fullName.Length > 300)
        {
            return BadRequest(new
            {
                message = "Username atau nama terlalu panjang."
            });
        }

        if (await _db.Users.AnyAsync(u => u.Username == username))
        {
            return Conflict(new { message = "username has already exists" });
        }

        if (request.Role != "Admin" && request.Role != "Cashier")
        {
            return BadRequest(new { message = "Role tidak valid." });
        }

        var userId = await GenerateUserIdAsync();

        if (userId is null)
        {
            return StatusCode(503, new
            {
                message = "Tidak dapat membuat ID pengguna. Coba lagi."
            });
        }

        var user = new User
        {
            IdUser = userId,
            Username = username,
            FullName = fullName,
            Role = request.Role,
            Stsrc = true,
            Datein = DateTime.UtcNow,
            Userin = userId
        };

        user.PasswordHash = _passwordHasher.HashPassword(
            user, request.Password);

        _db.Users.Add(user);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict(new
            {
                message = "Registrasi gagal. Periksa username atau coba kembali."
            });
        }

        return Created("/api/v1/auth/register", new
        {
            user.IdUser,
            user.Username,
            user.FullName,
            user.Role
        });
    }

    
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.Role))
        {
            return Unauthorized(new { message = "Invalid login" });
        }

        var username = request.Username.Trim();

        var user = await _db.Users
            .FirstOrDefaultAsync(u =>
                u.Username == username && u.Stsrc);

        if (user is null ||
            !string.Equals(
                user.Role,
                request.Role,
                StringComparison.Ordinal))
        {
            return Unauthorized(new { message = "Invalid login" });
        }

        var verification = _passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.Password);

        if (verification == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { message = "Invalid login" });
        }

        var token = _jwtTokenService.GenerateToken(user);

        return Ok(new
        {
            message = "Login berhasil.",
            token,
            user = new
            {
                user.IdUser,
                user.Username,
                user.FullName,
                user.Role
            }
        });
    }


    private async Task<string?> GenerateUserIdAsync()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var id = Random.Shared.Next(0, 100000).ToString("D5");

            if (!await _db.Users.AnyAsync(u => u.IdUser == id))
            {
                return id;
            }
        }

        return null;
    }
}