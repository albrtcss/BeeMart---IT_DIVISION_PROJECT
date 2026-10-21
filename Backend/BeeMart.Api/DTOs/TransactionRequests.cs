namespace BeeMart.Api.DTOs;

public class CreateTransactionRequest
{
    public int IdPaymentMethod { get; set; }

    public List<TransactionItemRequest> Items { get; set; } = new();
}

public class TransactionItemRequest
{
    public string IdProduct { get; set; } = string.Empty;

    public int Qty { get; set; }
}
