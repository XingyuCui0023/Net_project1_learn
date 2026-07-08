using FinanceTracker.Api.Models;

namespace FinanceTracker.Api.Dtos;

public sealed record RegisterRequest(string Email, string Password);
public sealed record LoginRequest(string Email, string Password);
public sealed record AuthResponse(Guid UserId, string Email, string Token);
public sealed record ErrorResponse(string Code, string Message);

public sealed record AccountRequest(string Name, string Type, decimal OpeningBalance);
public sealed record AccountResponse(Guid Id, string Name, string Type, decimal OpeningBalance);
public sealed record AccountBalanceResponse(Guid AccountId, decimal OpeningBalance, decimal Income, decimal Expense, decimal CurrentBalance);

public sealed record CategoryRequest(string Name, TransactionType Type);
public sealed record CategoryResponse(Guid Id, string Name, TransactionType Type);

public sealed record TransactionRequest(Guid AccountId, Guid CategoryId, TransactionType Type, decimal Amount, string? Note, DateOnly OccurredOn);
public sealed record TransactionResponse(Guid Id, Guid AccountId, Guid CategoryId, TransactionType Type, decimal Amount, string? Note, DateOnly OccurredOn);

public sealed record BudgetRequest(Guid CategoryId, int Year, int Month, decimal LimitAmount);
public sealed record BudgetResponse(Guid Id, Guid CategoryId, int Year, int Month, decimal LimitAmount, decimal SpentAmount, bool IsOverBudget);

public sealed record MonthlyReportResponse(int Year, int Month, decimal Income, decimal Expense, decimal Net, IReadOnlyList<CategorySpendResponse> SpendingByCategory, IReadOnlyList<BudgetResponse> Budgets);
public sealed record CategorySpendResponse(Guid CategoryId, string CategoryName, decimal Amount);
