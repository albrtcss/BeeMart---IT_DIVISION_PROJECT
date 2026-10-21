
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BeeMart.WebClient.Pages;

[Authorize]
public class NewTransactionModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public NewTransactionModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public List<PaymentMethodItem> PaymentMethods { get; set; } = new();

    public string InvoiceNo { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }

    public sealed class TransactionInput
    {
        public int IdPaymentMethod { get; set; }

        public List<TransactionItemInput> Items { get; set; } = new();
    }

    public sealed class TransactionItemInput
    {
        public string IdProduct { get; set; } = string.Empty;
        public int Qty { get; set; }
    }

    public sealed class ProductItem
    {
        public string IdProduct { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Weight { get; set; }
        public decimal Price { get; set; }
        public decimal Discount { get; set; }
        public int Stock { get; set; }
    }

    public sealed class PaymentMethodItem
    {
        public int IdPaymentMethod { get; set; }
        public string MethodName { get; set; } = string.Empty;
    }

    private sealed class InvoiceResponse
    {
        public string? InvoiceNo { get; set; }
    }

    private sealed class ApiError
    {
        public string? Message { get; set; }
    }

    private async Task<HttpClient?> CreateAuthenticatedClientAsync()
    {
        var token = await HttpContext.GetTokenAsync("access_token");

        if (string.IsNullOrWhiteSpace(token))
            return null;

        var client = _httpClientFactory.CreateClient("BackendApi");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var client = await CreateAuthenticatedClientAsync();

        if (client is null)
            return RedirectToPage("/Login");

        await LoadPageDataAsync();
        return Page();
    }

    // cari barang lewat API (maksimal 10 data)
    public async Task<IActionResult> OnGetSearchAsync(string? q)
    {
        var client = await CreateAuthenticatedClientAsync();

        if (client is null || string.IsNullOrWhiteSpace(q))
            return new JsonResult(Array.Empty<ProductItem>());

        var products = await client.GetFromJsonAsync<List<ProductItem>>(
            $"/api/v1/products?search={Uri.EscapeDataString(q)}") ?? new();

        return new JsonResult(products);
    }

    public async Task<IActionResult> OnPostSubmitAsync([FromBody] TransactionInput input)
    {
        if (input.Items.Count == 0 ||
            input.Items.Any(item =>
                string.IsNullOrWhiteSpace(item.IdProduct) ||
                item.Qty <= 0))
            return BadRequest(new { message = "Tambahkan produk dengan kuantitas yang valid." });

        if (input.IdPaymentMethod <= 0)
            return BadRequest(new { message = "Pilih metode pembayaran." });

        var client = await CreateAuthenticatedClientAsync();

        if (client is null)
            return Unauthorized();

        try
        {
            var response = await client.PostAsJsonAsync(
                "/api/v1/transactions",
                new
                {
                    idPaymentMethod = input.IdPaymentMethod,
                    items = input.Items
                        .GroupBy(item => item.IdProduct)
                        .Select(group => new
                        {
                            idProduct = group.Key,
                            qty = group.Sum(item => item.Qty)
                        })
                        .ToList()
                });

            if (!response.IsSuccessStatusCode)
            {
                var apiError = await response.Content
                    .ReadFromJsonAsync<ApiError>();

                return BadRequest(new
                {
                    message = apiError?.Message
                        ?? $"Transaksi gagal disimpan (HTTP {(int)response.StatusCode})."
                });
            }

            // ambil nomor invoice baru
            await LoadPageDataAsync();
            return new JsonResult(new { invoiceNo = InvoiceNo });
        }
        catch (HttpRequestException)
        {
            return BadRequest(new { message = "Backend tidak dapat dihubungi." });
        }
    }

    private async Task LoadPageDataAsync()
    {
        var client = await CreateAuthenticatedClientAsync();

        if (client is null)
            return;

        try
        {
            PaymentMethods = await client.GetFromJsonAsync<List<PaymentMethodItem>>(
                "/api/v1/payment-methods") ?? new();

            var invoiceResponse = await client.GetAsync(
                "/api/v1/transactions/next-invoice");

            if (invoiceResponse.IsSuccessStatusCode)
            {
                var invoice = await invoiceResponse.Content
                    .ReadFromJsonAsync<InvoiceResponse>();

                InvoiceNo = invoice?.InvoiceNo ?? string.Empty;
            }
        }
        catch (HttpRequestException)
        {
            ErrorMessage ??= "Gagal memuat ulang data dari backend.";
        }
        catch (System.Text.Json.JsonException)
        {
            ErrorMessage ??= "Format data dari backend tidak valid.";
        }
    }
}
