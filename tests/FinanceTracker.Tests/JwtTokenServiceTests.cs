using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FinanceTracker.Api.Models;
using FinanceTracker.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

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

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "unit-test-secret-key-change-me-32chars",
                ["Jwt:Issuer"] = "FinanceTracker.Tests"
            })
            .Build();
}
