using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BeeMart.WebClient.Pages;

public class RegisterModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public RegisterModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [BindProperty]
    public RegisterInput Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public string? SuccessMessage { get; set; }

    public class RegisterInput
    {
        [Required(ErrorMessage = "Username wajib diisi.")]
        [StringLength(50, ErrorMessage = "Username maksimal 50 karakter.")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nama lengkap wajib diisi.")]
        [StringLength(300, ErrorMessage = "Nama maksimal 300 karakter.")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password wajib diisi.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public string Role { get; set; } = "Cashier";

        [Required(ErrorMessage = "Konfirmasi password wajib diisi.")]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Konfirmasi password tidak cocok.")]
        public string ConfirmPassword { get; set; } = string.Empty;
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
                "/api/v1/auth/register",
                new
                {
                    username = Input.Username.Trim(),
                    fullName = Input.FullName.Trim(),
                    password = Input.Password,
                    role = Input.Role
                });

            if (response.IsSuccessStatusCode)
            {
                return RedirectToPage("/Login");
            }

            if ((int)response.StatusCode == 409)
            {
                ErrorMessage = "username has already exists";
            }
            else
            {
                var apiError = await response.Content.ReadFromJsonAsync<ApiError>();
                ErrorMessage = apiError?.Message
                    ?? "Registrasi gagal. Periksa data dan coba lagi.";
            }

            return Page();
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Backend tidak dapat dihubungi. Silakan coba lagi.";
            return Page();
        }
    }

    private sealed class ApiError
    {
        public string? Message { get; set; }
    }
}