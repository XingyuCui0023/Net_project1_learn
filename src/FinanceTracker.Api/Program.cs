using System.Security.Claims;
using System.Text;
using FinanceTracker.Api.Data;
using FinanceTracker.Api.Dtos;
using FinanceTracker.Api.Models;
using FinanceTracker.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Finance Tracker API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = []
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=finance-tracker.db"));

builder.Services.AddSingleton<PasswordHasher>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<DevelopmentDataSeeder>();

var jwtKey = builder.Configuration["Jwt:Key"] ?? "development-only-secret-key-change-me-32chars";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "FinanceTracker";
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtIssuer,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (app.Environment.IsEnvironment("Testing"))
    {
        db.Database.EnsureCreated();
    }
    else
    {
        db.Database.Migrate();
    }

    if (app.Environment.IsDevelopment())
    {
        var seeder = scope.ServiceProvider.GetRequiredService<DevelopmentDataSeeder>();
        await seeder.SeedAsync();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", service = "FinanceTracker.Api" }))
    .AllowAnonymous()
    .WithName("HealthCheck");

var auth = app.MapGroup("/api/auth").AllowAnonymous();
auth.MapPost("/register", async (RegisterRequest request, AppDbContext db, PasswordHasher passwordHasher, JwtTokenService jwtTokenService, CancellationToken cancellationToken) =>
{
    var email = request.Email.Trim().ToLowerInvariant();
    if (email.Length < 5 || !email.Contains('@') || request.Password.Length < 8)
    {
        return BadRequest("invalid_auth_request", "Email must be valid and password must be at least 8 characters.");
    }

    if (await db.Users.AnyAsync(user => user.Email == email, cancellationToken))
    {
        return Conflict("duplicate_email", "Email is already registered.");
    }

    var user = new User { Email = email, PasswordHash = passwordHasher.Hash(request.Password) };
    db.Users.Add(user);
    await db.SaveChangesAsync(cancellationToken);
    return Results.Created($"/api/users/{user.Id}", new AuthResponse(user.Id, user.Email, jwtTokenService.CreateToken(user)));
});

auth.MapPost("/login", async (LoginRequest request, AppDbContext db, PasswordHasher passwordHasher, JwtTokenService jwtTokenService, CancellationToken cancellationToken) =>
{
    var email = request.Email.Trim().ToLowerInvariant();
    var user = await db.Users.SingleOrDefaultAsync(item => item.Email == email, cancellationToken);
    if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
    {
        return Results.Unauthorized();
    }

    return Results.Ok(new AuthResponse(user.Id, user.Email, jwtTokenService.CreateToken(user)));
});

var api = app.MapGroup("/api").RequireAuthorization();

api.MapGet("/accounts", async (ClaimsPrincipal principal, AppDbContext db, CancellationToken cancellationToken) =>
{
    var userId = principal.GetUserId();
    var accounts = await db.Accounts
        .Where(account => account.UserId == userId)
        .OrderBy(account => account.Name)
        .Select(account => new AccountResponse(account.Id, account.Name, account.Type, account.OpeningBalance))
        .ToListAsync(cancellationToken);
    return Results.Ok(accounts);
});

api.MapGet("/accounts/{id:guid}", async (Guid id, ClaimsPrincipal principal, AppDbContext db, CancellationToken cancellationToken) =>
{
    var userId = principal.GetUserId();
    var account = await db.Accounts
        .Where(item => item.UserId == userId && item.Id == id)
        .Select(item => new AccountResponse(item.Id, item.Name, item.Type, item.OpeningBalance))
        .SingleOrDefaultAsync(cancellationToken);

    return account is null ? Results.NotFound() : Results.Ok(account);
});

api.MapGet("/accounts/{id:guid}/balance", async (Guid id, ClaimsPrincipal principal, AppDbContext db, CancellationToken cancellationToken) =>
{
    var userId = principal.GetUserId();
    var account = await db.Accounts.SingleOrDefaultAsync(item => item.UserId == userId && item.Id == id, cancellationToken);
    if (account is null)
    {
        return Results.NotFound();
    }

    var transactions = await db.Transactions
        .Where(transaction => transaction.UserId == userId && transaction.AccountId == id)
        .ToListAsync(cancellationToken);

    var income = transactions
        .Where(transaction => transaction.Type == TransactionType.Income)
        .Sum(transaction => transaction.Amount);
    var expense = transactions
        .Where(transaction => transaction.Type == TransactionType.Expense)
        .Sum(transaction => transaction.Amount);
    var currentBalance = account.OpeningBalance + income - expense;

    return Results.Ok(new AccountBalanceResponse(account.Id, account.OpeningBalance, income, expense, currentBalance));
});

api.MapPost("/accounts", async (AccountRequest request, ClaimsPrincipal principal, AppDbContext db, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Type))
    {
        return BadRequest("invalid_account", "Account name and type are required.");
    }

    var account = new Account
    {
        UserId = principal.GetUserId(),
        Name = request.Name.Trim(),
        Type = request.Type.Trim(),
        OpeningBalance = request.OpeningBalance
    };
    db.Accounts.Add(account);
    await db.SaveChangesAsync(cancellationToken);
    return Results.Created($"/api/accounts/{account.Id}", new AccountResponse(account.Id, account.Name, account.Type, account.OpeningBalance));
});

api.MapPut("/accounts/{id:guid}", async (Guid id, AccountRequest request, ClaimsPrincipal principal, AppDbContext db, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Type))
    {
        return BadRequest("invalid_account", "Account name and type are required.");
    }

    var userId = principal.GetUserId();
    var account = await db.Accounts.SingleOrDefaultAsync(item => item.UserId == userId && item.Id == id, cancellationToken);
    if (account is null)
    {
        return Results.NotFound();
    }

    account.Name = request.Name.Trim();
    account.Type = request.Type.Trim();
    account.OpeningBalance = request.OpeningBalance;
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(new AccountResponse(account.Id, account.Name, account.Type, account.OpeningBalance));
});

api.MapDelete("/accounts/{id:guid}", async (Guid id, ClaimsPrincipal principal, AppDbContext db, CancellationToken cancellationToken) =>
{
    var userId = principal.GetUserId();
    var account = await db.Accounts.SingleOrDefaultAsync(item => item.UserId == userId && item.Id == id, cancellationToken);
    if (account is null)
    {
        return Results.NotFound();
    }

    var hasTransactions = await db.Transactions.AnyAsync(transaction => transaction.UserId == userId && transaction.AccountId == id, cancellationToken);
    if (hasTransactions)
    {
        return Conflict("account_in_use", "Account has transactions and cannot be deleted.");
    }

    db.Accounts.Remove(account);
    await db.SaveChangesAsync(cancellationToken);
    return Results.NoContent();
});

api.MapGet("/categories", async (ClaimsPrincipal principal, AppDbContext db, CancellationToken cancellationToken) =>
{
    var userId = principal.GetUserId();
    var categories = await db.Categories
        .Where(category => category.UserId == userId && !category.IsDeleted)
        .OrderBy(category => category.Type)
        .ThenBy(category => category.Name)
        .Select(category => new CategoryResponse(category.Id, category.Name, category.Type))
        .ToListAsync(cancellationToken);
    return Results.Ok(categories);
});

api.MapGet("/categories/{id:guid}", async (Guid id, ClaimsPrincipal principal, AppDbContext db, CancellationToken cancellationToken) =>
{
    var userId = principal.GetUserId();
    var category = await db.Categories
        .Where(item => item.UserId == userId && item.Id == id && !item.IsDeleted)
        .Select(item => new CategoryResponse(item.Id, item.Name, item.Type))
        .SingleOrDefaultAsync(cancellationToken);

    return category is null ? Results.NotFound() : Results.Ok(category);
});

api.MapPost("/categories", async (CategoryRequest request, ClaimsPrincipal principal, AppDbContext db, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) || !Enum.IsDefined(request.Type))
    {
        return BadRequest("invalid_category", "Category name and type are required.");
    }

    var category = new Category { UserId = principal.GetUserId(), Name = request.Name.Trim(), Type = request.Type };
    db.Categories.Add(category);
    await db.SaveChangesAsync(cancellationToken);
    return Results.Created($"/api/categories/{category.Id}", new CategoryResponse(category.Id, category.Name, category.Type));
});

api.MapPut("/categories/{id:guid}", async (Guid id, CategoryRequest request, ClaimsPrincipal principal, AppDbContext db, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) || !Enum.IsDefined(request.Type))
    {
        return BadRequest("invalid_category", "Category name and type are required.");
    }

    var userId = principal.GetUserId();
    var category = await db.Categories.SingleOrDefaultAsync(item => item.UserId == userId && item.Id == id && !item.IsDeleted, cancellationToken);
    if (category is null)
    {
        return Results.NotFound();
    }

    category.Name = request.Name.Trim();
    category.Type = request.Type;
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(new CategoryResponse(category.Id, category.Name, category.Type));
});

api.MapDelete("/categories/{id:guid}", async (Guid id, ClaimsPrincipal principal, AppDbContext db, CancellationToken cancellationToken) =>
{
    var userId = principal.GetUserId();
    var category = await db.Categories.SingleOrDefaultAsync(item => item.UserId == userId && item.Id == id && !item.IsDeleted, cancellationToken);
    if (category is null)
    {
        return Results.NotFound();
    }

    category.IsDeleted = true;
    await db.SaveChangesAsync(cancellationToken);
    return Results.NoContent();
});

api.MapGet("/transactions", async (ClaimsPrincipal principal, AppDbContext db, DateOnly? from, DateOnly? to, Guid? accountId, Guid? categoryId, CancellationToken cancellationToken) =>
{
    var userId = principal.GetUserId();
    var query = db.Transactions.Where(transaction => transaction.UserId == userId);
    if (from is not null) query = query.Where(transaction => transaction.OccurredOn >= from.Value);
    if (to is not null) query = query.Where(transaction => transaction.OccurredOn <= to.Value);
    if (accountId is not null) query = query.Where(transaction => transaction.AccountId == accountId.Value);
    if (categoryId is not null) query = query.Where(transaction => transaction.CategoryId == categoryId.Value);

    var transactions = await query
        .OrderByDescending(transaction => transaction.OccurredOn)
        .Select(transaction => new TransactionResponse(transaction.Id, transaction.AccountId, transaction.CategoryId, transaction.Type, transaction.Amount, transaction.Note, transaction.OccurredOn))
        .ToListAsync(cancellationToken);
    return Results.Ok(transactions);
});

api.MapGet("/transactions/{id:guid}", async (Guid id, ClaimsPrincipal principal, AppDbContext db, CancellationToken cancellationToken) =>
{
    var userId = principal.GetUserId();
    var transaction = await db.Transactions
        .Where(item => item.UserId == userId && item.Id == id)
        .Select(item => new TransactionResponse(item.Id, item.AccountId, item.CategoryId, item.Type, item.Amount, item.Note, item.OccurredOn))
        .SingleOrDefaultAsync(cancellationToken);

    return transaction is null ? Results.NotFound() : Results.Ok(transaction);
});

api.MapPost("/transactions", async (TransactionRequest request, ClaimsPrincipal principal, AppDbContext db, CancellationToken cancellationToken) =>
{
    var userId = principal.GetUserId();
    var validation = await ValidateTransactionRequestAsync(request, userId, db, cancellationToken);
    if (validation is not null) return validation;

    var transaction = new Transaction
    {
        UserId = userId,
        AccountId = request.AccountId,
        CategoryId = request.CategoryId,
        Type = request.Type,
        Amount = request.Amount,
        Note = request.Note?.Trim(),
        OccurredOn = request.OccurredOn
    };
    db.Transactions.Add(transaction);
    await db.SaveChangesAsync(cancellationToken);
    return Results.Created($"/api/transactions/{transaction.Id}", new TransactionResponse(transaction.Id, transaction.AccountId, transaction.CategoryId, transaction.Type, transaction.Amount, transaction.Note, transaction.OccurredOn));
});

api.MapPut("/transactions/{id:guid}", async (Guid id, TransactionRequest request, ClaimsPrincipal principal, AppDbContext db, CancellationToken cancellationToken) =>
{
    var userId = principal.GetUserId();
    var transaction = await db.Transactions.SingleOrDefaultAsync(item => item.UserId == userId && item.Id == id, cancellationToken);
    if (transaction is null) return Results.NotFound();

    var validation = await ValidateTransactionRequestAsync(request, userId, db, cancellationToken);
    if (validation is not null) return validation;

    transaction.AccountId = request.AccountId;
    transaction.CategoryId = request.CategoryId;
    transaction.Type = request.Type;
    transaction.Amount = request.Amount;
    transaction.Note = request.Note?.Trim();
    transaction.OccurredOn = request.OccurredOn;
    await db.SaveChangesAsync(cancellationToken);
    return Results.Ok(new TransactionResponse(transaction.Id, transaction.AccountId, transaction.CategoryId, transaction.Type, transaction.Amount, transaction.Note, transaction.OccurredOn));
});

api.MapDelete("/transactions/{id:guid}", async (Guid id, ClaimsPrincipal principal, AppDbContext db, CancellationToken cancellationToken) =>
{
    var userId = principal.GetUserId();
    var transaction = await db.Transactions.SingleOrDefaultAsync(item => item.UserId == userId && item.Id == id, cancellationToken);
    if (transaction is null) return Results.NotFound();

    db.Transactions.Remove(transaction);
    await db.SaveChangesAsync(cancellationToken);
    return Results.NoContent();
});

api.MapGet("/budgets", async (ClaimsPrincipal principal, AppDbContext db, int year, int month, CancellationToken cancellationToken) =>
{
    if (!IsValidMonth(year, month)) return BadRequest("invalid_month", "Year and month are invalid.");

    var userId = principal.GetUserId();
    var start = new DateOnly(year, month, 1);
    var end = start.AddMonths(1);
    var budgets = await db.Budgets.Where(budget => budget.UserId == userId && budget.Year == year && budget.Month == month).ToListAsync(cancellationToken);
    var transactions = await db.Transactions
        .Where(transaction => transaction.UserId == userId && transaction.Type == TransactionType.Expense && transaction.OccurredOn >= start && transaction.OccurredOn < end)
        .ToListAsync(cancellationToken);

    return Results.Ok(budgets.Select(budget =>
    {
        var spent = transactions.Where(transaction => transaction.CategoryId == budget.CategoryId).Sum(transaction => transaction.Amount);
        return new BudgetResponse(budget.Id, budget.CategoryId, budget.Year, budget.Month, budget.LimitAmount, spent, spent > budget.LimitAmount);
    }));
});

api.MapPost("/budgets", async (BudgetRequest request, ClaimsPrincipal principal, AppDbContext db, CancellationToken cancellationToken) =>
{
    var userId = principal.GetUserId();
    var validation = await ValidateBudgetRequestAsync(request, userId, db, cancellationToken);
    if (validation is not null) return validation;

    var budget = await db.Budgets.SingleOrDefaultAsync(item =>
        item.UserId == userId && item.CategoryId == request.CategoryId && item.Year == request.Year && item.Month == request.Month, cancellationToken);

    if (budget is null)
    {
        budget = new Budget { UserId = userId, CategoryId = request.CategoryId, Year = request.Year, Month = request.Month, LimitAmount = request.LimitAmount };
        db.Budgets.Add(budget);
    }
    else
    {
        budget.LimitAmount = request.LimitAmount;
    }

    await db.SaveChangesAsync(cancellationToken);
    return Results.Ok(await BuildBudgetResponseAsync(budget, userId, db, cancellationToken));
});

api.MapGet("/budgets/{id:guid}", async (Guid id, ClaimsPrincipal principal, AppDbContext db, CancellationToken cancellationToken) =>
{
    var userId = principal.GetUserId();
    var budget = await db.Budgets.SingleOrDefaultAsync(item => item.UserId == userId && item.Id == id, cancellationToken);
    if (budget is null)
    {
        return Results.NotFound();
    }

    return Results.Ok(await BuildBudgetResponseAsync(budget, userId, db, cancellationToken));
});

api.MapPut("/budgets/{id:guid}", async (Guid id, BudgetRequest request, ClaimsPrincipal principal, AppDbContext db, CancellationToken cancellationToken) =>
{
    var userId = principal.GetUserId();
    var validation = await ValidateBudgetRequestAsync(request, userId, db, cancellationToken);
    if (validation is not null) return validation;

    var budget = await db.Budgets.SingleOrDefaultAsync(item => item.UserId == userId && item.Id == id, cancellationToken);
    if (budget is null)
    {
        return Results.NotFound();
    }

    var duplicateExists = await db.Budgets.AnyAsync(item =>
        item.UserId == userId &&
        item.Id != id &&
        item.CategoryId == request.CategoryId &&
        item.Year == request.Year &&
        item.Month == request.Month,
        cancellationToken);
    if (duplicateExists)
    {
        return Conflict("duplicate_budget", "Budget already exists for this category and month.");
    }

    budget.CategoryId = request.CategoryId;
    budget.Year = request.Year;
    budget.Month = request.Month;
    budget.LimitAmount = request.LimitAmount;
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(await BuildBudgetResponseAsync(budget, userId, db, cancellationToken));
});

api.MapDelete("/budgets/{id:guid}", async (Guid id, ClaimsPrincipal principal, AppDbContext db, CancellationToken cancellationToken) =>
{
    var userId = principal.GetUserId();
    var budget = await db.Budgets.SingleOrDefaultAsync(item => item.UserId == userId && item.Id == id, cancellationToken);
    if (budget is null)
    {
        return Results.NotFound();
    }

    db.Budgets.Remove(budget);
    await db.SaveChangesAsync(cancellationToken);
    return Results.NoContent();
});

api.MapGet("/reports/monthly", async (ClaimsPrincipal principal, ReportService reportService, int year, int month, CancellationToken cancellationToken) =>
{
    if (!IsValidMonth(year, month)) return BadRequest("invalid_month", "Year and month are invalid.");
    return Results.Ok(await reportService.GetMonthlyReportAsync(principal.GetUserId(), year, month, cancellationToken));
});

app.Run();

static async Task<IResult?> ValidateTransactionRequestAsync(TransactionRequest request, Guid userId, AppDbContext db, CancellationToken cancellationToken)
{
    if (request.Amount <= 0 || !Enum.IsDefined(request.Type))
    {
        return BadRequest("invalid_transaction", "Amount must be greater than zero and type must be valid.");
    }

    var ownsAccount = await db.Accounts.AnyAsync(account => account.UserId == userId && account.Id == request.AccountId, cancellationToken);
    var category = await db.Categories.SingleOrDefaultAsync(item =>
        item.UserId == userId &&
        item.Id == request.CategoryId &&
        !item.IsDeleted,
        cancellationToken);
    if (!ownsAccount || category is null)
    {
        return BadRequest("invalid_transaction_reference", "Account or category does not exist.");
    }

    if (category.Type != request.Type)
    {
        return BadRequest("transaction_type_mismatch", "Transaction type must match category type.");
    }

    return null;
}

static async Task<IResult?> ValidateBudgetRequestAsync(BudgetRequest request, Guid userId, AppDbContext db, CancellationToken cancellationToken)
{
    if (!IsValidMonth(request.Year, request.Month) || request.LimitAmount <= 0)
    {
        return BadRequest("invalid_budget", "Budget month must be valid and limit amount must be greater than zero.");
    }

    var ownsCategory = await db.Categories.AnyAsync(category =>
        category.UserId == userId &&
        category.Id == request.CategoryId &&
        category.Type == TransactionType.Expense &&
        !category.IsDeleted,
        cancellationToken);
    if (!ownsCategory)
    {
        return BadRequest("invalid_budget_category", "Expense category does not exist.");
    }

    return null;
}

static async Task<BudgetResponse> BuildBudgetResponseAsync(Budget budget, Guid userId, AppDbContext db, CancellationToken cancellationToken)
{
    var start = new DateOnly(budget.Year, budget.Month, 1);
    var end = start.AddMonths(1);
    var spent = await db.Transactions
        .Where(transaction =>
            transaction.UserId == userId &&
            transaction.CategoryId == budget.CategoryId &&
            transaction.Type == TransactionType.Expense &&
            transaction.OccurredOn >= start &&
            transaction.OccurredOn < end)
        .SumAsync(transaction => transaction.Amount, cancellationToken);

    return new BudgetResponse(budget.Id, budget.CategoryId, budget.Year, budget.Month, budget.LimitAmount, spent, spent > budget.LimitAmount);
}

static IResult BadRequest(string code, string message) => Results.BadRequest(new ErrorResponse(code, message));

static IResult Conflict(string code, string message) => Results.Conflict(new ErrorResponse(code, message));

static bool IsValidMonth(int year, int month) => year is >= 2000 and <= 2100 && month is >= 1 and <= 12;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : throw new InvalidOperationException("User id claim is missing.");
    }
}

public partial class Program;
