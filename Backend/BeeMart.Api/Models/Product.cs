namespace BeeMart.Api.Models;

public class Product
{
    public string IdProduct { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public decimal Weight { get; set; }

    public int Stock { get; set; }

    public decimal Price { get; set; }

    public decimal Discount { get; set; }

    public bool Stsrc { get; set; }

    public DateTime Datein { get; set; }

    public string Userin { get; set; } = string.Empty;

    public DateTime? Dateup { get; set; }

    public string? Userup { get; set; }
}
