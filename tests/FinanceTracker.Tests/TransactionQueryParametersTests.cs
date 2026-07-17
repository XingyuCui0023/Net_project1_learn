using FinanceTracker.Api.Dtos;
using FluentAssertions;

namespace FinanceTracker.Tests;

public sealed class TransactionQueryParametersTests
{
    [Fact]
    public void Validate_returns_error_when_amount_range_is_invalid()
    {
        var query = new TransactionQueryParameters(
            From: null,
            To: null,
            AccountId: null,
            CategoryId: null,
            Keyword: null,
            MinAmount: 100,
            MaxAmount: 10);

        var validation = query.Validate();

        validation.Should().NotBeNull();
        validation!.Code.Should().Be("invalid_amount_range");
    }

    [Fact]
    public void Normalized_sort_values_ignore_spaces_and_case()
    {
        var query = new TransactionQueryParameters(
            From: null,
            To: null,
            AccountId: null,
            CategoryId: null,
            Keyword: null,
            MinAmount: null,
            MaxAmount: null,
            SortBy: " Amount ",
            SortDirection: " DESC ");

        query.Validate().Should().BeNull();
        query.NormalizedSortBy.Should().Be("amount");
        query.NormalizedSortDirection.Should().Be("desc");
    }
}
