namespace BeeMart.Api.Models;

public class TransactionDetail
{
    public string IdTransactionDetail { get; set; } = string.Empty;

    public string IdTransaction { get; set; } = string.Empty;

    public string IdProduct { get; set; } = string.Empty;

    public int Qty { get; set; }

    public decimal Price { get; set; }

    public decimal Discount { get; set; }

    public decimal SubTotal { get; set; }

    public bool Stsrc { get; set; }

    public DateTime Datein { get; set; }

    public string Userin { get; set; } = string.Empty;

    public DateTime? Dateup { get; set; }

    public string? Userup { get; set; }
}
