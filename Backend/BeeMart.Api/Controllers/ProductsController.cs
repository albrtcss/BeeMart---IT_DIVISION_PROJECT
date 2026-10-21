using BeeMart.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeeMart.Api.Controllers;

[ApiController]
[Route("api/v1/products")]
public class ProductsController : ControllerBase
{
    private readonly BeeMartDbContext _db;

    public ProductsController(BeeMartDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetActiveProducts([FromQuery] string? search = null)
    {
        var query = _db.Products.Where(p => p.Stsrc).OrderBy(p => p.Name).AsQueryable();

        // kalau ada kata kunci: nama mengandung kata itu, maksimal 10 data
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.Contains(search.Trim())).Take(10);

        var products = await query
            .Select(p => new
            {
                p.IdProduct,
                p.Name,
                p.Weight,
                p.Stock,
                p.Price,
                p.Discount
            })
            .ToListAsync();

        return Ok(products);
    }
}
