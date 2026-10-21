namespace BeeMart.Api.Models;

public class Transaction
{
    public string IdTransaction { get; set; } = string.Empty;

    public string InvoiceNo { get; set; } = string.Empty;

    public DateTime TransactionDate { get; set; }

    public string IdUser { get; set; } = string.Empty;

    public int IdPaymentMethod { get; set; }

    public decimal TotalPrice { get; set; }

    public bool Stsrc { get; set; }

    public DateTime Datein { get; set; }

    public string Userin { get; set; } = string.Empty;

    public DateTime? Dateup { get; set; }

    public string? Userup { get; set; }

    public List<TransactionDetail> Details { get; set; } = new();
}
