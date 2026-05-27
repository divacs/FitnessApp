using System.Linq.Expressions;
using FitnessApp.Application.Common.Responses;

namespace FitnessApp.Application.Common.Pagination;

public static class PaginationExtensions
{
    public static int NormalizePage(this int page)
    {
        return Math.Max(page, PaginationRequest.DefaultPage);
    }

    public static int NormalizePageSize(this int pageSize)
    {
        if (pageSize <= 0)
        {
            return PaginationRequest.DefaultPageSize;
        }

        return Math.Min(pageSize, PaginationRequest.MaxPageSize);
    }

    public static IQueryable<T> ApplyPagination<T>(
        this IQueryable<T> query,
        int page,
        int pageSize)
    {
        var normalizedPage = page.NormalizePage();
        var normalizedPageSize = pageSize.NormalizePageSize();

        return query
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize);
    }

    public static IQueryable<T> WhereIf<T>(
        this IQueryable<T> query,
        bool condition,
        Expression<Func<T, bool>> predicate)
    {
        return condition ? query.Where(predicate) : query;
    }

    public static PaginatedResponse<T> ToPaginatedResponse<T>(
        this IReadOnlyCollection<T> items,
        int page,
        int pageSize,
        int totalCount)
    {
        return new PaginatedResponse<T>(
            items,
            page.NormalizePage(),
            pageSize.NormalizePageSize(),
            totalCount);
    }
}
