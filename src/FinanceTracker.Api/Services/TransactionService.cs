using FinanceTracker.Api.Data;
using FinanceTracker.Api.Dtos;
using FinanceTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Api.Services;

public sealed class TransactionService(AppDbContext db)
{
    public async Task<PagedResponse<TransactionResponse>> GetTransactionsAsync(
        Guid userId,
        DateOnly? from,
        DateOnly? to,
        Guid? accountId,
        Guid? categoryId,
        string? keyword,
        decimal? minAmount,
        decimal? maxAmount,
        int page,
        int pageSize,
        string sortByValue,
        string sortDirectionValue,
        CancellationToken cancellationToken)
    {
        var query = ApplyFilters(userId, from, to, accountId, categoryId, keyword, minAmount, maxAmount);
        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        var orderedQuery = ApplySorting(query, sortByValue, sortDirectionValue);

        var transactions = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(transaction => new TransactionResponse(
                transaction.Id,
                transaction.AccountId,
                transaction.CategoryId,
                transaction.Type,
                transaction.Amount,
                transaction.Note,
                transaction.OccurredOn))
            .ToListAsync(cancellationToken);

        return new PagedResponse<TransactionResponse>(transactions, page, pageSize, totalCount, totalPages);
    }

    private IQueryable<Transaction> ApplyFilters(
        Guid userId,
        DateOnly? from,
        DateOnly? to,
        Guid? accountId,
        Guid? categoryId,
        string? keyword,
        decimal? minAmount,
        decimal? maxAmount)
    {
        var query = db.Transactions.Where(transaction => transaction.UserId == userId);
        if (from is not null) query = query.Where(transaction => transaction.OccurredOn >= from.Value);
        if (to is not null) query = query.Where(transaction => transaction.OccurredOn <= to.Value);
        if (accountId is not null) query = query.Where(transaction => transaction.AccountId == accountId.Value);
        if (categoryId is not null) query = query.Where(transaction => transaction.CategoryId == categoryId.Value);
        if (minAmount is not null) query = query.Where(transaction => transaction.Amount >= minAmount.Value);
        if (maxAmount is not null) query = query.Where(transaction => transaction.Amount <= maxAmount.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var keywordValue = keyword.Trim();
            query = query.Where(transaction => transaction.Note != null && transaction.Note.Contains(keywordValue));
        }

        return query;
    }

    private static IOrderedQueryable<Transaction> ApplySorting(IQueryable<Transaction> query, string sortByValue, string sortDirectionValue) =>
        (sortByValue, sortDirectionValue) switch
        {
            ("amount", "asc") => query.OrderBy(transaction => transaction.Amount).ThenByDescending(transaction => transaction.Id),
            ("amount", "desc") => query.OrderByDescending(transaction => transaction.Amount).ThenByDescending(transaction => transaction.Id),
            ("occurredon", "asc") => query.OrderBy(transaction => transaction.OccurredOn).ThenByDescending(transaction => transaction.Id),
            _ => query.OrderByDescending(transaction => transaction.OccurredOn).ThenByDescending(transaction => transaction.Id)
        };
}
