using FinanceTracker.Api.Models;
using FinanceTracker.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Api.Data;

public sealed class DevelopmentDataSeeder(AppDbContext db, PasswordHasher passwordHasher)
{
    public const string DemoEmail = "demo@example.com";
    public const string DemoPassword = "Password123!";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(item => item.Email == DemoEmail, cancellationToken);
        if (user is not null)
        {
            return;
        }

        user = new User
        {
            Email = DemoEmail,
            PasswordHash = passwordHasher.Hash(DemoPassword)
        };

        var cash = new Account
        {
            UserId = user.Id,
            Name = "Cash",
            Type = "Wallet",
            OpeningBalance = 100
        };
        var bank = new Account
        {
            UserId = user.Id,
            Name = "Everyday Card",
            Type = "Debit",
            OpeningBalance = 500
        };

        var salary = new Category
        {
            UserId = user.Id,
            Name = "Salary",
            Type = TransactionType.Income
        };
        var food = new Category
        {
            UserId = user.Id,
            Name = "Food",
            Type = TransactionType.Expense
        };
        var transport = new Category
        {
            UserId = user.Id,
            Name = "Transport",
            Type = TransactionType.Expense
        };

        db.Users.Add(user);
        db.Accounts.AddRange(cash, bank);
        db.Categories.AddRange(salary, food, transport);
        db.Transactions.AddRange(
            new Transaction
            {
                UserId = user.Id,
                AccountId = bank.Id,
                CategoryId = salary.Id,
                Type = TransactionType.Income,
                Amount = 3000,
                Note = "Monthly salary",
                OccurredOn = new DateOnly(2026, 7, 1)
            },
            new Transaction
            {
                UserId = user.Id,
                AccountId = cash.Id,
                CategoryId = food.Id,
                Type = TransactionType.Expense,
                Amount = 45.50m,
                Note = "Lunch",
                OccurredOn = new DateOnly(2026, 7, 2)
            },
            new Transaction
            {
                UserId = user.Id,
                AccountId = bank.Id,
                CategoryId = transport.Id,
                Type = TransactionType.Expense,
                Amount = 28,
                Note = "Train card top-up",
                OccurredOn = new DateOnly(2026, 7, 3)
            });
        db.Budgets.AddRange(
            new Budget
            {
                UserId = user.Id,
                CategoryId = food.Id,
                Year = 2026,
                Month = 7,
                LimitAmount = 300
            },
            new Budget
            {
                UserId = user.Id,
                CategoryId = transport.Id,
                Year = 2026,
                Month = 7,
                LimitAmount = 120
            });

        await db.SaveChangesAsync(cancellationToken);
    }
}
