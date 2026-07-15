using FinanceTracker.Api.Data;
using FinanceTracker.Api.Models;
using FinanceTracker.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests;

public sealed class TransactionServiceTests
{
    [Fact]
    public async Task GetTransactionsAsync_applies_user_filter_search_amount_range_sorting_and_pagination()
    {
        await using var db = CreateDbContext();
        var service = new TransactionService(db);
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        db.Transactions.AddRange(
            CreateTransaction(userId, accountId, categoryId, 5, "Lunch small", new DateOnly(2026, 7, 1)),
            CreateTransaction(userId, accountId, categoryId, 15, "Lunch medium", new DateOnly(2026, 7, 2)),
            CreateTransaction(userId, accountId, categoryId, 25, "Lunch large", new DateOnly(2026, 7, 3)),
            CreateTransaction(userId, accountId, categoryId, 35, "Dinner large", new DateOnly(2026, 7, 4)),
            CreateTransaction(otherUserId, accountId, categoryId, 20, "Lunch other user", new DateOnly(2026, 7, 5)));
        await db.SaveChangesAsync();

        var result = await service.GetTransactionsAsync(
            userId,
            from: null,
            to: null,
            accountId,
            categoryId,
            keyword: "Lunch",
            minAmount: 10,
            maxAmount: 30,
            page: 1,
            pageSize: 2,
            sortByValue: "amount",
            sortDirectionValue: "desc",
            CancellationToken.None);

        result.TotalCount.Should().Be(2);
        result.TotalPages.Should().Be(1);
        result.Items.Select(transaction => transaction.Amount).Should().Equal(25, 15);
        result.Items.Should().OnlyContain(transaction => transaction.Note!.Contains("Lunch"));
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static Transaction CreateTransaction(
        Guid userId,
        Guid accountId,
        Guid categoryId,
        decimal amount,
        string note,
        DateOnly occurredOn) =>
        new()
        {
            UserId = userId,
            AccountId = accountId,
            CategoryId = categoryId,
            Type = TransactionType.Expense,
            Amount = amount,
            Note = note,
            OccurredOn = occurredOn
        };
}
