using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FinanceTracker.Api.Models;
using FinanceTracker.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace FinanceTracker.Tests;

public sealed class JwtTokenServiceTests
{
    [Fact]
    public void CreateToken_includes_user_identity_claims_and_token_metadata()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "jwt-user@example.com",
            PasswordHash = "hash"
        };
        var service = new JwtTokenService(CreateConfiguration());

        var token = service.CreateToken(user);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Issuer.Should().Be("FinanceTracker.Tests");
        jwt.Audiences.Should().ContainSingle("FinanceTracker.Tests");
        jwt.Claims.Single(claim => claim.Type == ClaimTypes.NameIdentifier).Value.Should().Be(user.Id.ToString());
        jwt.Claims.Single(claim => claim.Type == ClaimTypes.Email).Value.Should().Be(user.Email);
        jwt.ValidTo.Should().BeAfter(DateTime.UtcNow.AddHours(7));
    }

    [Fact]
    public void CreateToken_generates_token_that_passes_signature_validation()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "jwt-validate@example.com",
            PasswordHash = "hash"
        };
        var configuration = CreateConfiguration();
        var service = new JwtTokenService(configuration);
        var validationParameters = CreateValidationParameters(configuration);

        var token = service.CreateToken(user);

        var principal = new JwtSecurityTokenHandler().ValidateToken(token, validationParameters, out var validatedToken);
        validatedToken.Should().BeOfType<JwtSecurityToken>();
        principal.FindFirstValue(ClaimTypes.NameIdentifier).Should().Be(user.Id.ToString());
        principal.FindFirstValue(ClaimTypes.Email).Should().Be(user.Email);
    }

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "unit-test-secret-key-change-me-32chars",
                ["Jwt:Issuer"] = "FinanceTracker.Tests"
            })
            .Build();

    private static TokenValidationParameters CreateValidationParameters(IConfiguration configuration) =>
        new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = configuration["Jwt:Issuer"],
            ValidAudience = configuration["Jwt:Issuer"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!))
        };
}
