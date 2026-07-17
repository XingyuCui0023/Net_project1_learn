using FinanceTracker.Api.Services;
using FluentAssertions;

namespace FinanceTracker.Tests;

public sealed class PasswordHasherTests
{
    [Fact]
    public void Hash_does_not_store_plain_text_password()
    {
        var hasher = new PasswordHasher();

        var hash = hasher.Hash("Password123!");

        hash.Should().NotBe("Password123!");
        hash.Should().NotContain("Password123!");
    }

    [Fact]
    public void Hash_generates_different_values_for_same_password()
    {
        var hasher = new PasswordHasher();

        var firstHash = hasher.Hash("Password123!");
        var secondHash = hasher.Hash("Password123!");

        firstHash.Should().NotBe(secondHash);
    }

    [Fact]
    public void Verify_returns_true_for_correct_password_and_false_for_wrong_password()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.Hash("Password123!");

        hasher.Verify("Password123!", hash).Should().BeTrue();
        hasher.Verify("WrongPassword123!", hash).Should().BeFalse();
    }
}
