namespace SPMH.Services.Models
{
    public sealed class PagedResult<T>
    {
        public IReadOnlyList<T> Items { get; }
        public int TotalItems { get; }
        public int Page { get; }
        public int PageSize { get; }
        public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling((double)TotalItems / PageSize);

        public PagedResult(IReadOnlyList<T> items, int totalItems, int page, int pageSize)
        {
            Items = items;
            TotalItems = totalItems;
            Page = page < 1 ? 1 : page;
            PageSize = pageSize < 1 ? 5 : pageSize;
        }
    }
}