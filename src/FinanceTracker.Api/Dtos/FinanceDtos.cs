using FinanceTracker.Api.Models;

namespace FinanceTracker.Api.Dtos;

public sealed record RegisterRequest(string Email, string Password);
public sealed record LoginRequest(string Email, string Password);
public sealed record AuthResponse(Guid UserId, string Email, string Token);
public sealed record ErrorResponse(string Code, string Message);
public sealed record ValidationError(string Code, string Message);
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public sealed record AccountRequest(string Name, string Type, decimal OpeningBalance);
public sealed record AccountResponse(Guid Id, string Name, string Type, decimal OpeningBalance);
public sealed record AccountBalanceResponse(Guid AccountId, decimal OpeningBalance, decimal Income, decimal Expense, decimal CurrentBalance);

public sealed record CategoryRequest(string Name, TransactionType Type);
public sealed record CategoryResponse(Guid Id, string Name, TransactionType Type);

public sealed record TransactionRequest(Guid AccountId, Guid CategoryId, TransactionType Type, decimal Amount, string? Note, DateOnly OccurredOn);
public sealed record TransactionResponse(Guid Id, Guid AccountId, Guid CategoryId, TransactionType Type, decimal Amount, string? Note, DateOnly OccurredOn);
public sealed record TransactionQueryParameters(
    DateOnly? From,
    DateOnly? To,
    Guid? AccountId,
    Guid? CategoryId,
    string? Keyword,
    decimal? MinAmount,
    decimal? MaxAmount,
    int Page = 1,
    int PageSize = 20,
    string SortBy = TransactionSortOptions.DefaultSortBy,
    string SortDirection = TransactionSortOptions.DefaultSortDirection)
{
    public string NormalizedSortBy => SortBy.Trim().ToLowerInvariant();
    public string NormalizedSortDirection => SortDirection.Trim().ToLowerInvariant();

    public ValidationError? Validate()
    {
        if (Page <= 0 || PageSize <= 0 || PageSize > 100)
        {
            return new ValidationError("invalid_pagination", "Page must be greater than zero and page size must be between 1 and 100.");
        }

        if (!TransactionSortOptions.IsValidSortBy(NormalizedSortBy) || !TransactionSortOptions.IsValidSortDirection(NormalizedSortDirection))
        {
            return new ValidationError("invalid_sort", "Sort by must be occurredOn or amount, and sort direction must be asc or desc.");
        }

        if (MinAmount is < 0 || MaxAmount is < 0 || (MinAmount is not null && MaxAmount is not null && MinAmount > MaxAmount))
        {
            return new ValidationError("invalid_amount_range", "Amount range must be valid and cannot be negative.");
        }

        return null;
    }
}

public sealed record BudgetRequest(Guid CategoryId, int Year, int Month, decimal LimitAmount);
public sealed record BudgetResponse(Guid Id, Guid CategoryId, int Year, int Month, decimal LimitAmount, decimal SpentAmount, bool IsOverBudget);

public sealed record MonthlyReportResponse(int Year, int Month, decimal Income, decimal Expense, decimal Net, IReadOnlyList<CategorySpendResponse> SpendingByCategory, IReadOnlyList<BudgetResponse> Budgets);
public sealed record CategorySpendResponse(Guid CategoryId, string CategoryName, decimal Amount);
