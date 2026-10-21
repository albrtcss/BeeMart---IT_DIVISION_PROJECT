
using System.Data;
using System.Security.Claims;
using BeeMart.Api.Data;
using BeeMart.Api.DTOs;
using BeeMart.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeeMart.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
public class TransactionsController : ControllerBase
{
    private readonly BeeMartDbContext _db;

    public TransactionsController(BeeMartDbContext db)
    {
        _db = db;
    }

    [HttpGet("payment-methods")]
    public async Task<IActionResult> GetPaymentMethods()
    {
        var methods = await _db.PaymentMethods
            .AsNoTracking()
            .Where(p => p.Stsrc)
            .OrderBy(p => p.IdPaymentMethod)
            .Select(p => new
            {
                p.IdPaymentMethod,
                p.MethodName
            })
            .ToListAsync();

        return Ok(methods);
    }

    [HttpGet("transactions/next-invoice")]
    public async Task<IActionResult> GetNextInvoice()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!IsValidUserId(userId))
            return Unauthorized(new { message = "User ID tidak valid." });

        var prefix = $"{userId}{DateTime.Now:ddMMyy}";

        // cuma preview, nomor aslinya dibuat ulang saat transaksi disimpan
        var lastInvoice = await _db.Transactions
            .AsNoTracking()
            .Where(t => t.InvoiceNo.StartsWith(prefix))
            .OrderByDescending(t => t.InvoiceNo)
            .Select(t => t.InvoiceNo)
            .FirstOrDefaultAsync();

        var nextNumber = GetNextRunningNumber(lastInvoice, prefix);

        if (nextNumber > 999)
            return Conflict(new { message = "Nomor invoice hari ini sudah mencapai batas." });

        return Ok(new
        {
            invoiceNo = $"{prefix}{nextNumber:000}"
        });
    }

    [HttpPost("transactions")]
    public async Task<IActionResult> CreateTransaction(
        [FromBody] CreateTransactionRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!IsValidUserId(userId))
            return Unauthorized(new { message = "User ID tidak valid." });

        var authenticatedUserId = userId!;

        if (request.Items is null || request.Items.Count == 0)
            return BadRequest(new { message = "Transaksi harus memiliki minimal satu produk." });

        if (request.Items.Any(i => string.IsNullOrWhiteSpace(i.IdProduct) || i.Qty <= 0))
            return BadRequest(new { message = "ID produk harus valid dan kuantitas harus lebih dari nol." });

        // produk yang sama digabung supaya stok dicek dari total qty
        var requestedItems = request.Items
            .GroupBy(i => i.IdProduct)
            .Select(g => new
            {
                IdProduct = g.Key,
                Qty = g.Sum(i => i.Qty)
            })
            .ToList();

        // batas qty biar tidak overflow
        if (requestedItems.Any(i => i.Qty <= 0 || i.Qty > 1_000_000))
            return BadRequest(new { message = "Kuantitas produk tidak valid." });

        await using var dbTransaction =
            await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        try
        {
            var paymentMethod = await _db.PaymentMethods
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.IdPaymentMethod == request.IdPaymentMethod &&
                    p.Stsrc);

            if (paymentMethod is null)
            {
                await dbTransaction.RollbackAsync();
                return BadRequest(new { message = "Metode pembayaran tidak valid atau tidak aktif." });
            }

            var productIds = requestedItems.Select(i => i.IdProduct).ToList();

            var products = await _db.Products
                .AsNoTracking()
                .Where(p => productIds.Contains(p.IdProduct) && p.Stsrc)
                .ToDictionaryAsync(p => p.IdProduct);

            if (products.Count != requestedItems.Count)
            {
                await dbTransaction.RollbackAsync();
                return BadRequest(new { message = "Satu atau lebih produk tidak ditemukan atau tidak aktif." });
            }

            foreach (var item in requestedItems)
            {
                if (products[item.IdProduct].Stock < item.Qty)
                {
                    await dbTransaction.RollbackAsync();
                    return Conflict(new
                    {
                        message = $"Stok {products[item.IdProduct].Name} tidak mencukupi."
                    });
                }
            }

            var now = DateTime.Now;
            var prefix = $"{authenticatedUserId}{now:ddMMyy}";

            // tabel dikunci supaya nomor invoice tidak kembar kalau ada request bersamaan
            var lastInvoice = await _db.Database
                .SqlQuery<string>($"""
                    SELECT TOP (1) [InvoiceNo] AS [Value]
                    FROM dbo.TrTransaction WITH (TABLOCKX, HOLDLOCK)
                    WHERE [InvoiceNo] LIKE {prefix + "%"}
                    ORDER BY [InvoiceNo] DESC
                    """)
                .FirstOrDefaultAsync();

            var nextNumber = GetNextRunningNumber(lastInvoice, prefix);

            if (nextNumber > 999)
            {
                await dbTransaction.RollbackAsync();
                return Conflict(new { message = "Nomor invoice hari ini sudah mencapai batas." });
            }

            var invoiceNo = $"{prefix}{nextNumber:000}";
            var transactionId = Guid.NewGuid().ToString();

            var details = new List<TransactionDetail>();
            decimal totalPrice = 0m;

            foreach (var item in requestedItems)
            {
                var product = products[item.IdProduct];

                // diskon dalam persen (10 = 10%)
                if (product.Discount < 0m || product.Discount > 100m)
                {
                    await dbTransaction.RollbackAsync();
                    return BadRequest(new
                    {
                        message = $"Diskon produk {product.Name} tidak valid."
                    });
                }

                var subtotal = decimal.Round(
                    product.Price * item.Qty * (1m - product.Discount / 100m),
                    2,
                    MidpointRounding.AwayFromZero);

                totalPrice += subtotal;

                details.Add(new TransactionDetail
                {
                    IdTransactionDetail = Guid.NewGuid().ToString(),
                    IdTransaction = transactionId,
                    IdProduct = product.IdProduct,
                    Qty = item.Qty,
                    Price = product.Price,
                    Discount = product.Discount,
                    SubTotal = subtotal,
                    Stsrc = true,
                    Datein = now,
                    Userin = authenticatedUserId
                });
            }

            // stok dikurangi hanya kalau cukup, kalau gagal semua di-rollback
            foreach (var item in requestedItems)
            {

                var affected = await _db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE dbo.MsProduct
                    SET Stock = Stock - {item.Qty},
                        Dateup = {now},
                        Userup = {authenticatedUserId}
                    WHERE IdProduct = {item.IdProduct}
                    AND Stsrc = 1
                    AND Stock >= {item.Qty}
                    """);


                if (affected != 1)
                {
                    await dbTransaction.RollbackAsync();
                    return Conflict(new
                    {
                        message = $"Stok produk {products[item.IdProduct].Name} berubah atau tidak mencukupi. Silakan coba kembali."
                    });
                }
            }

            var transaction = new Transaction
            {
                IdTransaction = transactionId,
                InvoiceNo = invoiceNo,
                TransactionDate = now,
                IdUser = authenticatedUserId,
                IdPaymentMethod = request.IdPaymentMethod,
                TotalPrice = totalPrice,
                Stsrc = true,
                Datein = now,
                Userin = userId!,
                Details = details
            };

            _db.Transactions.Add(transaction);
            await _db.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            return Created(
                $"/api/v1/transactions/{transactionId}",
                new
                {
                    transaction.IdTransaction,
                    transaction.InvoiceNo,
                    transaction.TransactionDate,
                    transaction.IdUser,
                    transaction.IdPaymentMethod,
                    transaction.TotalPrice,
                    details = transaction.Details.Select(d => new
                    {
                        d.IdProduct,
                        productName = products[d.IdProduct].Name,
                        d.Qty,
                        d.Price,
                        d.Discount,
                        d.SubTotal
                    })
                });
        }
        catch (DbUpdateException)
        {
            await dbTransaction.RollbackAsync();

            return Conflict(new
            {
                message = "Transaksi gagal disimpan. Silakan periksa data dan coba kembali."
            });
        }
        catch
        {
            await dbTransaction.RollbackAsync();
            throw;
        }
    }

    [HttpGet("cashiers")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetCashiers()
    {
        var cashiers = await _db.Users
            .AsNoTracking()
            .Where(u => u.Stsrc && u.Role == "Cashier")
            .OrderBy(u => u.FullName)
            .Select(u => new { u.IdUser, u.FullName })
            .ToListAsync();

        return Ok(cashiers);
    }

    [HttpGet("transactions/history")]
    public async Task<IActionResult> GetTransactionHistory(
        [FromQuery] DateTime? date,
        [FromQuery] int page = 1,
        [FromQuery] string? cashierId = null)
    {
        if (page < 1)
        {
            return BadRequest(new
            {
                message = "Nomor halaman harus minimal 1."
            });
        }

        if (!date.HasValue)
        {
            return BadRequest(new
            {
                message = "Tanggal transaksi wajib dipilih."
            });
        }

        if (date.Value.TimeOfDay != TimeSpan.Zero)
        {
            return BadRequest(new
            {
                message = "Format tanggal transaksi tidak valid."
            });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var role = User.FindFirstValue(ClaimTypes.Role);

        if (!IsValidUserId(userId))
        {
            return Unauthorized(new
            {
                message = "User ID tidak valid."
            });
        }

        if (role is not ("Admin" or "Cashier"))
        {
            return Forbid();
        }

        var selectedDate = date.Value.Date;
        var nextDate = selectedDate.AddDays(1);

        var query = _db.Transactions
            .AsNoTracking()
            .Where(t =>
                t.Stsrc &&
                t.TransactionDate >= selectedDate &&
                t.TransactionDate < nextDate);

        // kasir hanya lihat transaksinya sendiri, admin lihat semua
        if (role == "Cashier")
        {
            query = query.Where(t => t.IdUser == userId);
        }

        // filter kasir khusus admin, kosong = semua kasir
        if (role == "Admin" && !string.IsNullOrWhiteSpace(cashierId))
        {
            query = query.Where(t => t.IdUser == cashierId);
        }

        var totalData = await query.CountAsync();

        const int pageSize = 20;

        var totalPages = (int)Math.Ceiling(
            totalData / (double)pageSize);

        var transactions = await query
            .OrderBy(t => t.TransactionDate)
            .ThenBy(t => t.InvoiceNo)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                t.IdTransaction,
                t.InvoiceNo,
                t.TransactionDate,
                t.IdUser,

                CashierName = _db.Users
                    .Where(u => u.IdUser == t.IdUser)
                    .Select(u => u.FullName)
                    .FirstOrDefault(),

                PaymentMethod = _db.PaymentMethods
                    .Where(p => p.IdPaymentMethod == t.IdPaymentMethod)
                    .Select(p => p.MethodName)
                    .FirstOrDefault(),

                TotalItems = _db.TransactionDetails
                    .Where(d =>
                        d.IdTransaction == t.IdTransaction &&
                        d.Stsrc)
                    .Sum(d => (int?)d.Qty) ?? 0,

                t.TotalPrice
            })
            .ToListAsync();

        return Ok(new
        {
            page,
            pageSize,
            totalData,
            totalPages,
            transactions
        });
    }

    private static bool IsValidUserId(string? userId) =>
        userId is { Length: 5 } &&
        userId.All(char.IsAsciiDigit);

    private static int GetNextRunningNumber(
        string? lastInvoice,
        string prefix)
    {
        if (string.IsNullOrEmpty(lastInvoice) ||
            !lastInvoice.StartsWith(prefix, StringComparison.Ordinal))
        {
            return 1;
        }

        var suffix = lastInvoice[prefix.Length..];

        if (suffix.Length != 3 ||
            !int.TryParse(suffix, out var lastNumber))
        {
            throw new InvalidOperationException(
                "Format nomor invoice terakhir tidak valid.");
        }

        return lastNumber + 1;
    }
}
