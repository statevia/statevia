using System.ComponentModel.DataAnnotations;
using Statevia.Service.Api.Contracts;

namespace Statevia.Service.Api.Tests.Contracts;

/// <summary>一覧のクエリ（実行・件数・定義）の並び順・期間の検証と、ページクエリへの変換を確かめる。</summary>
public sealed class ListQueryValidationTests
{
    private static List<ValidationResult> Validate(object query)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(query, new ValidationContext(query), results, validateAllProperties: true);
        return results;
    }

    /// <summary>実行の一覧は許可する sortBy / sortOrder を受ける。</summary>
    [Theory]
    [InlineData("updatedAt", "asc")]
    [InlineData("displayId", "desc")]
    [InlineData("startedAt", null)]
    [InlineData(null, null)]
    [InlineData("  ", "")]
    public void ExecutionListQuery_WhenSortIsAllowed_HasNoErrors(string? sortBy, string? sortOrder)
    {
        // Arrange
        var query = new ExecutionListQuery { Limit = 1, SortBy = sortBy, SortOrder = sortOrder };

        // Act
        var results = Validate(query);

        // Assert
        Assert.Empty(results);
    }

    /// <summary>実行の一覧で許可していない sortBy は 422 の対象（sortBy のエラー）。大文字小文字を区別する。</summary>
    [Theory]
    [InlineData("createdAt")]
    [InlineData("StartedAt")]
    public void ExecutionListQuery_WhenSortByIsNotAllowed_ReturnsSortByError(string sortBy)
    {
        // Arrange
        var query = new ExecutionListQuery { Limit = 1, SortBy = sortBy };

        // Act
        var error = Assert.Single(Validate(query));

        // Assert
        Assert.Contains(nameof(ExecutionListQuery.SortBy), error.MemberNames);
        Assert.Equal("sortBy must be one of updatedAt, displayId, startedAt", error.ErrorMessage);
    }

    /// <summary>許可していない sortOrder は sortOrder のエラーになる。</summary>
    [Fact]
    public void ExecutionListQuery_WhenSortOrderIsNotAllowed_ReturnsSortOrderError()
    {
        // Arrange
        var query = new ExecutionListQuery { Limit = 1, SortOrder = "DESC" };

        // Act
        var error = Assert.Single(Validate(query));

        // Assert
        Assert.Contains(nameof(ExecutionListQuery.SortOrder), error.MemberNames);
        Assert.Equal("sortOrder must be one of asc, desc", error.ErrorMessage);
    }

    /// <summary>タイムゾーンの無い開始日時はエラーになる。</summary>
    [Theory]
    [InlineData("2026-11-14T00:00:00")]
    [InlineData("2026-11-14")]
    [InlineData("yesterday")]
    public void ExecutionListQuery_WhenStartedFromHasNoZone_ReturnsStartedFromError(string startedFrom)
    {
        // Arrange
        var query = new ExecutionListQuery { Limit = 1, StartedFrom = startedFrom };

        // Act
        var error = Assert.Single(Validate(query));

        // Assert
        Assert.Contains(nameof(ExecutionListQuery.StartedFrom), error.MemberNames);
    }

    /// <summary>startedFrom が startedTo 以上ならエラーになる。</summary>
    [Fact]
    public void ExecutionListQuery_WhenStartedFromIsNotBeforeStartedTo_ReturnsRangeError()
    {
        // Arrange
        var query = new ExecutionListQuery
        {
            Limit = 1,
            StartedFrom = "2026-11-14T09:00:00+09:00",
            StartedTo = "2026-11-14T00:00:00Z"
        };

        // Act
        var error = Assert.Single(Validate(query));

        // Assert
        Assert.Equal("startedFrom must be earlier than startedTo", error.ErrorMessage);
    }

    /// <summary>検証済みのクエリは、期間を UTC に直し、空の並び順を省略にしてページクエリへ渡す。</summary>
    [Fact]
    public void ExecutionListQuery_ToPageQuery_ConvertsRangeToUtcAndForwardsWaitingFilters()
    {
        // Arrange
        var query = new ExecutionListQuery
        {
            Limit = 10,
            SortBy = " ",
            SortOrder = "asc",
            StartedFrom = "2026-11-14T09:00:00+09:00",
            Waiting = true,
            NeedsAttention = false
        };

        // Act
        var page = query.ToPageQuery(definitionIdFilter: null);

        // Assert
        Assert.Equal(new DateTime(2026, 11, 14, 0, 0, 0, DateTimeKind.Utc), page.StartedFrom);
        Assert.Equal(DateTimeKind.Utc, page.StartedFrom!.Value.Kind);
        Assert.Null(page.StartedTo);
        Assert.Equal(new SortQuery(null, "asc"), page.Sort);
        Assert.True(page.Waiting);
        Assert.False(page.NeedsAttention);
    }

    /// <summary>件数のクエリも期間を検証する。</summary>
    [Fact]
    public void ExecutionCountQuery_WhenStartedToHasNoZone_ReturnsStartedToError()
    {
        // Arrange
        var query = new ExecutionCountQuery { StartedTo = "2026-11-14T00:00" };

        // Act
        var error = Assert.Single(Validate(query));

        // Assert
        Assert.Contains(nameof(ExecutionCountQuery.StartedTo), error.MemberNames);
    }

    /// <summary>定義の一覧は updatedAt を受ける。</summary>
    [Fact]
    public void DefinitionListQuery_WhenSortByIsUpdatedAt_HasNoErrors()
    {
        // Arrange
        var query = new DefinitionListQuery { Limit = 1, SortBy = "updatedAt", SortOrder = "desc" };

        // Act
        var results = Validate(query);

        // Assert
        Assert.Empty(results);
    }

    /// <summary>定義の一覧で許可していない sortBy は sortBy のエラーになる。</summary>
    [Fact]
    public void DefinitionListQuery_WhenSortByIsNotAllowed_ReturnsSortByError()
    {
        // Arrange
        var query = new DefinitionListQuery { Limit = 1, SortBy = "startedAt" };

        // Act
        var error = Assert.Single(Validate(query));

        // Assert
        Assert.Contains(nameof(DefinitionListQuery.SortBy), error.MemberNames);
        Assert.Equal("sortBy must be one of createdAt, name, updatedAt", error.ErrorMessage);
    }
}
