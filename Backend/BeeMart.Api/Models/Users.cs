namespace BeeMart.Api.Models;

public class User
{
    public string IdUser { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public bool Stsrc { get; set; }

    public DateTime Datein { get; set; }

    public string Userin { get; set; } = string.Empty;

    public DateTime? Dateup { get; set; }

    public string? Userup { get; set; }
}