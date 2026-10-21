
using System.Globalization;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BeeMart.WebClient.Pages;

[Authorize]
public class TransactionHistoryModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public TransactionHistoryModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public List<TransactionHistoryItem> Transactions { get; set; } = new();

    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalData { get; set; }
    public int TotalPages { get; set; }

    public string? SelectedDate { get; set; }
    public string? SelectedCashierId { get; set; }
    public List<CashierItem> Cashiers { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public string RoleName =>
        User.FindFirstValue(ClaimTypes.Role) ?? "";

    public string Username =>
        User.Identity?.Name ?? "";
    public string FilterLabel
    {
        get
        {
            if (!IsAdmin)
                return $"(Hanya Kasir: {Username})";

            var cashier = Cashiers.FirstOrDefault(c => c.IdUser == SelectedCashierId);

            return cashier is null
                ? "(Menampilkan Semua Kasir)"
                : $"(Hanya Kasir: {cashier.FullName})";
        }
    }

    public bool IsAdmin =>
        User.IsInRole("Admin");

        public async Task<IActionResult> OnGetAsync(
        string? date,
        string? cashierId,
        int page = 1)
    {
        PageNumber = Math.Max(page, 1);
        SelectedDate = date;
        SelectedCashierId = cashierId;

        if (IsAdmin)
        {
            await LoadCashiersAsync();
        }

        // data baru diambil setelah tanggal dipilih
        if (string.IsNullOrWhiteSpace(date))
        {
            return Page();
        }

        if (!IsValidDate(date))
        {
            ErrorMessage = "Format tanggal harus YYYY-MM-DD.";
            return Page();
        }

        var client = _httpClientFactory.CreateClient("BackendApi");
        var token = await HttpContext.GetTokenAsync("access_token");

        if (string.IsNullOrWhiteSpace(token))
        {
            return Challenge();
        }

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var url =
            "/api/v1/transactions/history?" +
            $"date={Uri.EscapeDataString(date)}&page={PageNumber}" +
            $"&cashierId={Uri.EscapeDataString(cashierId ?? "")}";

        try
        {
            using var response = await client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = response.StatusCode switch
                {
                    System.Net.HttpStatusCode.Unauthorized =>
                        "Sesi autentikasi API tidak valid. Silakan login kembali.",

                    System.Net.HttpStatusCode.Forbidden =>
                        "Akun tidak memiliki izin untuk melihat riwayat transaksi.",

                    _ =>
                        $"Gagal mengambil riwayat transaksi. HTTP {(int)response.StatusCode}."
                };

                return Page();
            }

            var json = await response.Content.ReadAsStringAsync();

            var result =
                JsonSerializer.Deserialize<TransactionHistoryResponse>(
                    json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            if (result is null)
            {
                ErrorMessage = "Respons riwayat transaksi tidak valid.";
                return Page();
            }

            Transactions = result.Transactions ?? new();
            TotalData = result.TotalData;
            TotalPages = result.TotalPages;
            PageSize = result.PageSize;
            PageNumber = result.Page;
        }
        catch (HttpRequestException)
        {
            ErrorMessage =
                "Backend tidak dapat dihubungi. Pastikan API BeeMart sedang berjalan.";
        }
        catch (JsonException)
        {
            ErrorMessage =
                "Data riwayat transaksi dari backend tidak dapat dibaca.";
        }

        return Page();
    }    
    private async Task LoadCashiersAsync()
    {
        var token = await HttpContext.GetTokenAsync("access_token");

        if (string.IsNullOrWhiteSpace(token))
            return;

        var client = _httpClientFactory.CreateClient("BackendApi");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        try
        {
            Cashiers = await client.GetFromJsonAsync<List<CashierItem>>(
                "/api/v1/cashiers") ?? new();
        }
        catch (HttpRequestException)
        {
            // dropdown dibiarkan kosong
        }
    }

    public class CashierItem
    {
        public string IdUser { get; set; } = "";
        public string FullName { get; set; } = "";
    }

    private static bool IsValidDate(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ||
            DateTime.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _);
    }

    public string BuildPageUrl(int page)
    {
    return "/TransactionHistory?" +
           $"date={Uri.EscapeDataString(SelectedDate ?? "")}" +
           $"&cashierId={Uri.EscapeDataString(SelectedCashierId ?? "")}" +
           $"&page={page}";
    }

    public class TransactionHistoryResponse
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalData { get; set; }
        public int TotalPages { get; set; }

        public List<TransactionHistoryItem>? Transactions { get; set; }
    }

    public class TransactionHistoryItem
    {
        public string IdTransaction { get; set; } = "";
        public string InvoiceNo { get; set; } = "";
        public DateTime TransactionDate { get; set; }
        public string IdUser { get; set; } = "";
        public string? CashierName { get; set; }
        public string? PaymentMethod { get; set; }
        public int TotalItems { get; set; }
        public decimal TotalPrice { get; set; }
    }
}
