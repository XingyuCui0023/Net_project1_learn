using FinanceTracker.Api.Data;
using FinanceTracker.Api.Dtos;
using FinanceTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Api.Services;

public sealed class ReportService(AppDbContext db)
{
    public async Task<MonthlyReportResponse> GetMonthlyReportAsync(Guid userId, int year, int month, CancellationToken cancellationToken)
    {
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1);

        var transactions = await db.Transactions
            .Where(transaction => transaction.UserId == userId && transaction.OccurredOn >= start && transaction.OccurredOn < end)
            .ToListAsync(cancellationToken);

        var categories = await db.Categories
            .Where(category => category.UserId == userId)
            .ToDictionaryAsync(category => category.Id, cancellationToken);

        var income = transactions.Where(transaction => transaction.Type == TransactionType.Income).Sum(transaction => transaction.Amount);
        var expense = transactions.Where(transaction => transaction.Type == TransactionType.Expense).Sum(transaction => transaction.Amount);

        var spendingByCategory = transactions
            .Where(transaction => transaction.Type == TransactionType.Expense)
            .GroupBy(transaction => transaction.CategoryId)
            .Select(group => new CategorySpendResponse(
                group.Key,
                categories.TryGetValue(group.Key, out var category) ? category.Name : "Unknown",
                group.Sum(transaction => transaction.Amount)))
            .OrderByDescending(item => item.Amount)
            .ToList();

        var budgets = await db.Budgets
            .Where(budget => budget.UserId == userId && budget.Year == year && budget.Month == month)
            .ToListAsync(cancellationToken);

        var budgetResponses = budgets.Select(budget =>
        {
            var spent = transactions
                .Where(transaction => transaction.Type == TransactionType.Expense && transaction.CategoryId == budget.CategoryId)
                .Sum(transaction => transaction.Amount);

            return new BudgetResponse(budget.Id, budget.CategoryId, budget.Year, budget.Month, budget.LimitAmount, spent, spent > budget.LimitAmount);
        }).ToList();

        return new MonthlyReportResponse(year, month, income, expense, income - expense, spendingByCategory, budgetResponses);
    }
}
