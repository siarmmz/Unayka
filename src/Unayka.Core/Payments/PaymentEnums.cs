namespace Unayka.Core.Payments;

public enum PaymentMethod
{
    MPesa,
    EMola,
    Cash,
    Card,
}

public enum PaymentStatus
{
    Pending,
    Paid,
    Failed,
    Cancelled,
}
