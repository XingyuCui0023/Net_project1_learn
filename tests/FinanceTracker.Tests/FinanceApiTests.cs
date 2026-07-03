using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FinanceTracker.Api.Data;
using FinanceTracker.Api.Dtos;
using FinanceTracker.Api.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Tests;

public sealed class FinanceApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task Protected_endpoint_requires_login()
    {
        await using var factory = new FinanceTrackerFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/transactions");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task User_can_register_login_create_transaction_and_read_monthly_report()
    {
        await using var factory = new FinanceTrackerFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "learner@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var account = await PostAsync<AccountResponse>(client, "/api/accounts", new AccountRequest("Cash", "Wallet", 100));
        var category = await PostAsync<CategoryResponse>(client, "/api/categories", new CategoryRequest("Food", TransactionType.Expense));
        await PostAsync<TransactionResponse>(client, "/api/transactions", new TransactionRequest(account.Id, category.Id, TransactionType.Expense, 45.50m, "Lunch", new DateOnly(2026, 7, 2)));
        await PostAsync<BudgetResponse>(client, "/api/budgets", new BudgetRequest(category.Id, 2026, 7, 40));

        var report = await client.GetFromJsonAsync<MonthlyReportResponse>("/api/reports/monthly?year=2026&month=7", JsonOptions);

        report.Should().NotBeNull();
        report!.Expense.Should().Be(45.50m);
        report.Net.Should().Be(-45.50m);
        report.SpendingByCategory.Single().Amount.Should().Be(45.50m);
        report.Budgets.Single().IsOverBudget.Should().BeTrue();
    }

    [Fact]
    public async Task User_can_read_account_balance()
    {
        await using var factory = new FinanceTrackerFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "balance@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var account = await PostAsync<AccountResponse>(client, "/api/accounts", new AccountRequest("Cash", "Wallet", 100));
        var incomeCategory = await PostAsync<CategoryResponse>(client, "/api/categories", new CategoryRequest("Salary", TransactionType.Income));
        var expenseCategory = await PostAsync<CategoryResponse>(client, "/api/categories", new CategoryRequest("Food", TransactionType.Expense));
        await PostAsync<TransactionResponse>(client, "/api/transactions", new TransactionRequest(account.Id, incomeCategory.Id, TransactionType.Income, 1000, "Pay", new DateOnly(2026, 7, 3)));
        await PostAsync<TransactionResponse>(client, "/api/transactions", new TransactionRequest(account.Id, expenseCategory.Id, TransactionType.Expense, 45.50m, "Lunch", new DateOnly(2026, 7, 3)));

        var balance = await client.GetFromJsonAsync<AccountBalanceResponse>($"/api/accounts/{account.Id}/balance", JsonOptions);

        balance.Should().NotBeNull();
        balance!.OpeningBalance.Should().Be(100);
        balance.Income.Should().Be(1000);
        balance.Expense.Should().Be(45.50m);
        balance.CurrentBalance.Should().Be(1054.50m);
    }

    [Fact]
    public async Task User_cannot_read_another_users_transaction()
    {
        await using var factory = new FinanceTrackerFactory();
        var client = factory.CreateClient();

        var firstToken = await RegisterAndGetTokenAsync(client, "first@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", firstToken);
        var account = await PostAsync<AccountResponse>(client, "/api/accounts", new AccountRequest("Bank", "Debit", 0));
        var category = await PostAsync<CategoryResponse>(client, "/api/categories", new CategoryRequest("Salary", TransactionType.Income));
        var transaction = await PostAsync<TransactionResponse>(client, "/api/transactions", new TransactionRequest(account.Id, category.Id, TransactionType.Income, 1000, "Pay", new DateOnly(2026, 7, 2)));

        var secondToken = await RegisterAndGetTokenAsync(client, "second@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secondToken);
        var response = await client.GetAsync($"/api/transactions/{transaction.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task<string> RegisterAndGetTokenAsync(HttpClient client, string email)
    {
        var auth = await PostAsync<AuthResponse>(client, "/api/auth/register", new RegisterRequest(email, "Password123!"));
        return auth.Token;
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        var content = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue(content);
        return JsonSerializer.Deserialize<T>(content, JsonOptions)!;
    }
}

public sealed class FinanceTrackerFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_databaseName));
        });
    }
}
