using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FinanceTracker.Api.Data;
using FinanceTracker.Api.Dtos;
using FinanceTracker.Api.Models;
using FinanceTracker.Api.Services;
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
    public async Task Error_response_has_consistent_shape()
    {
        await using var factory = new FinanceTrackerFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("bad", "short"));
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error.Should().NotBeNull();
        error!.Code.Should().Be("invalid_auth_request");
        error.Message.Should().Be("Email must be valid and password must be at least 8 characters.");
    }

    [Fact]
    public async Task Development_data_seeder_creates_demo_data_once()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AppDbContext(options);
        var seeder = new DevelopmentDataSeeder(db, new PasswordHasher());

        await seeder.SeedAsync();
        await seeder.SeedAsync();

        db.Users.Should().ContainSingle(user => user.Email == DevelopmentDataSeeder.DemoEmail);
        db.Accounts.Should().HaveCount(2);
        db.Categories.Should().HaveCount(3);
        db.Transactions.Should().HaveCount(3);
        db.Budgets.Should().HaveCount(2);
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
    public async Task User_can_get_update_and_delete_account_without_transactions()
    {
        await using var factory = new FinanceTrackerFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "account-crud@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var account = await PostAsync<AccountResponse>(client, "/api/accounts", new AccountRequest("Cash", "Wallet", 100));

        var fetched = await client.GetFromJsonAsync<AccountResponse>($"/api/accounts/{account.Id}", JsonOptions);
        fetched.Should().NotBeNull();
        fetched!.Name.Should().Be("Cash");

        var updateResponse = await client.PutAsJsonAsync($"/api/accounts/{account.Id}", new AccountRequest("Everyday Card", "Debit", 250));
        var updateContent = await updateResponse.Content.ReadAsStringAsync();
        updateResponse.IsSuccessStatusCode.Should().BeTrue(updateContent);
        var updated = JsonSerializer.Deserialize<AccountResponse>(updateContent, JsonOptions);
        updated!.Name.Should().Be("Everyday Card");
        updated.Type.Should().Be("Debit");
        updated.OpeningBalance.Should().Be(250);

        var deleteResponse = await client.DeleteAsync($"/api/accounts/{account.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var missingResponse = await client.GetAsync($"/api/accounts/{account.Id}");
        missingResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task User_cannot_delete_account_that_has_transactions()
    {
        await using var factory = new FinanceTrackerFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "account-delete@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var account = await PostAsync<AccountResponse>(client, "/api/accounts", new AccountRequest("Bank", "Debit", 0));
        var category = await PostAsync<CategoryResponse>(client, "/api/categories", new CategoryRequest("Salary", TransactionType.Income));
        await PostAsync<TransactionResponse>(client, "/api/transactions", new TransactionRequest(account.Id, category.Id, TransactionType.Income, 1000, "Pay", new DateOnly(2026, 7, 3)));

        var deleteResponse = await client.DeleteAsync($"/api/accounts/{account.Id}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task User_can_get_update_and_delete_category_without_usage()
    {
        await using var factory = new FinanceTrackerFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "category-crud@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var category = await PostAsync<CategoryResponse>(client, "/api/categories", new CategoryRequest("Snacks", TransactionType.Expense));

        var fetched = await client.GetFromJsonAsync<CategoryResponse>($"/api/categories/{category.Id}", JsonOptions);
        fetched.Should().NotBeNull();
        fetched!.Name.Should().Be("Snacks");

        var updateResponse = await client.PutAsJsonAsync($"/api/categories/{category.Id}", new CategoryRequest("Groceries", TransactionType.Expense));
        var updateContent = await updateResponse.Content.ReadAsStringAsync();
        updateResponse.IsSuccessStatusCode.Should().BeTrue(updateContent);
        var updated = JsonSerializer.Deserialize<CategoryResponse>(updateContent, JsonOptions);
        updated!.Name.Should().Be("Groceries");
        updated.Type.Should().Be(TransactionType.Expense);

        var deleteResponse = await client.DeleteAsync($"/api/categories/{category.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var missingResponse = await client.GetAsync($"/api/categories/{category.Id}");
        missingResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task User_can_soft_delete_category_that_has_transactions()
    {
        await using var factory = new FinanceTrackerFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "category-delete@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var account = await PostAsync<AccountResponse>(client, "/api/accounts", new AccountRequest("Cash", "Wallet", 0));
        var category = await PostAsync<CategoryResponse>(client, "/api/categories", new CategoryRequest("Food", TransactionType.Expense));
        await PostAsync<TransactionResponse>(client, "/api/transactions", new TransactionRequest(account.Id, category.Id, TransactionType.Expense, 20, "Dinner", new DateOnly(2026, 7, 3)));

        var deleteResponse = await client.DeleteAsync($"/api/categories/{category.Id}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var missingResponse = await client.GetAsync($"/api/categories/{category.Id}");
        missingResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var transactions = await client.GetFromJsonAsync<List<TransactionResponse>>($"/api/transactions?categoryId={category.Id}", JsonOptions);
        transactions.Should().NotBeNull();
        transactions.Should().HaveCount(1);

        var newTransactionResponse = await client.PostAsJsonAsync(
            "/api/transactions",
            new TransactionRequest(account.Id, category.Id, TransactionType.Expense, 8, "Snack", new DateOnly(2026, 7, 4)));
        newTransactionResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task User_can_get_update_and_delete_budget()
    {
        await using var factory = new FinanceTrackerFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "budget-crud@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var account = await PostAsync<AccountResponse>(client, "/api/accounts", new AccountRequest("Cash", "Wallet", 0));
        var category = await PostAsync<CategoryResponse>(client, "/api/categories", new CategoryRequest("Food", TransactionType.Expense));
        await PostAsync<TransactionResponse>(client, "/api/transactions", new TransactionRequest(account.Id, category.Id, TransactionType.Expense, 30, "Dinner", new DateOnly(2026, 7, 5)));
        var budget = await PostAsync<BudgetResponse>(client, "/api/budgets", new BudgetRequest(category.Id, 2026, 7, 50));

        var fetched = await client.GetFromJsonAsync<BudgetResponse>($"/api/budgets/{budget.Id}", JsonOptions);
        fetched.Should().NotBeNull();
        fetched!.SpentAmount.Should().Be(30);
        fetched.IsOverBudget.Should().BeFalse();

        var updateResponse = await client.PutAsJsonAsync($"/api/budgets/{budget.Id}", new BudgetRequest(category.Id, 2026, 7, 20));
        var updateContent = await updateResponse.Content.ReadAsStringAsync();
        updateResponse.IsSuccessStatusCode.Should().BeTrue(updateContent);
        var updated = JsonSerializer.Deserialize<BudgetResponse>(updateContent, JsonOptions);
        updated!.LimitAmount.Should().Be(20);
        updated.SpentAmount.Should().Be(30);
        updated.IsOverBudget.Should().BeTrue();

        var deleteResponse = await client.DeleteAsync($"/api/budgets/{budget.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var missingResponse = await client.GetAsync($"/api/budgets/{budget.Id}");
        missingResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task User_cannot_read_another_users_account_category_or_budget()
    {
        await using var factory = new FinanceTrackerFactory();
        var client = factory.CreateClient();

        var firstToken = await RegisterAndGetTokenAsync(client, "owner@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", firstToken);
        var account = await PostAsync<AccountResponse>(client, "/api/accounts", new AccountRequest("Owner Cash", "Wallet", 0));
        var category = await PostAsync<CategoryResponse>(client, "/api/categories", new CategoryRequest("Owner Food", TransactionType.Expense));
        var budget = await PostAsync<BudgetResponse>(client, "/api/budgets", new BudgetRequest(category.Id, 2026, 7, 100));

        var secondToken = await RegisterAndGetTokenAsync(client, "other@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secondToken);

        var accountResponse = await client.GetAsync($"/api/accounts/{account.Id}");
        var categoryResponse = await client.GetAsync($"/api/categories/{category.Id}");
        var budgetResponse = await client.GetAsync($"/api/budgets/{budget.Id}");

        accountResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        categoryResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        budgetResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task User_cannot_create_transaction_with_another_users_account_or_category()
    {
        await using var factory = new FinanceTrackerFactory();
        var client = factory.CreateClient();

        var firstToken = await RegisterAndGetTokenAsync(client, "transaction-owner@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", firstToken);
        var account = await PostAsync<AccountResponse>(client, "/api/accounts", new AccountRequest("Owner Bank", "Debit", 0));
        var category = await PostAsync<CategoryResponse>(client, "/api/categories", new CategoryRequest("Owner Salary", TransactionType.Income));

        var secondToken = await RegisterAndGetTokenAsync(client, "transaction-other@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secondToken);

        var response = await client.PostAsJsonAsync(
            "/api/transactions",
            new TransactionRequest(account.Id, category.Id, TransactionType.Income, 1000, "Pay", new DateOnly(2026, 7, 6)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
