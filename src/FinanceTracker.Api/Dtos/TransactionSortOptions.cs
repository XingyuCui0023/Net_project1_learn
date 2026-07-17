namespace FinanceTracker.Api.Dtos;

public static class TransactionSortOptions
{
    public const string OccurredOn = "occurredon";
    public const string Amount = "amount";
    public const string Ascending = "asc";
    public const string Descending = "desc";
    public const string DefaultSortBy = "occurredOn";
    public const string DefaultSortDirection = Descending;

    public static bool IsValidSortBy(string value) => value is OccurredOn or Amount;

    public static bool IsValidSortDirection(string value) => value is Ascending or Descending;
}
