using FinanceTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Budget> Budgets => Set<Budget>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasIndex(user => user.Email).IsUnique();
        modelBuilder.Entity<Account>().Property(account => account.OpeningBalance).HasPrecision(18, 2);
        modelBuilder.Entity<Transaction>().Property(transaction => transaction.Amount).HasPrecision(18, 2);
        modelBuilder.Entity<Budget>().Property(budget => budget.LimitAmount).HasPrecision(18, 2);
        modelBuilder.Entity<Budget>().HasIndex(budget => new { budget.UserId, budget.CategoryId, budget.Year, budget.Month }).IsUnique();
    }
}
