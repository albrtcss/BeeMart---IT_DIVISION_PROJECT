using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BeeMart.WebClient.Pages;

public class LoginModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public LoginModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [BindProperty]
    public LoginInput Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public class LoginInput
    {
        [Required(ErrorMessage = "Username wajib diisi.")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password wajib diisi.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public string Role { get; set; } = "Cashier";
    }
    private sealed class LoginResponse
    {
        public string? Message { get; set; }
        public string? Token { get; set; }
        public LoginUser? User { get; set; }
    }

    private sealed class LoginUser
    {
        public string IdUser { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    public void OnGet()
    {
    }


    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        try
        {
            var client = _httpClientFactory.CreateClient("BackendApi");

            var response = await client.PostAsJsonAsync(
                "/api/v1/auth/login",
                new
                {
                    username = Input.Username.Trim(),
                    password = Input.Password,
                    role =Input.Role
                });

            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = "Invalid login";
                return Page();
            }

            var result =
                await response.Content.ReadFromJsonAsync<LoginResponse>();

            if (result?.User is null ||
                string.IsNullOrWhiteSpace(result.User.IdUser) ||
                string.IsNullOrWhiteSpace(result.User.Role) ||
                string.IsNullOrWhiteSpace(result.Token))
            {
                ErrorMessage = "Respons login dari server tidak valid.";
                return Page();
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, result.User.IdUser),
                new(ClaimTypes.Name, result.User.Username),
                new("FullName", result.User.FullName),
                new(ClaimTypes.Role, result.User.Role)
            };

            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            var principal = new ClaimsPrincipal(identity);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = false,
                AllowRefresh = true
            };

            authProperties.StoreTokens(new[]
            {
                new AuthenticationToken
                {
                    Name = "access_token",
                    Value = result.Token
                }
            });

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                authProperties);

            return RedirectToPage("/NewTransaction");
        }
        catch (HttpRequestException)
        {
            ErrorMessage =
                "Backend tidak dapat dihubungi. Silakan coba lagi.";

            return Page();
        }
        catch (System.Text.Json.JsonException)
        {
            ErrorMessage = "Respons login dari server tidak dapat dibaca.";
            return Page();
        }
    }

}