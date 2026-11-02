namespace Common.Results.BaseClasses;

public abstract class BaseResultObjectPagedListModel<TData> : BaseResultModel, IResultObjectPagedListModel<TData>
    where TData : class, new()
{
    protected BaseResultObjectPagedListModel()
    {
        IsSuccess = false;
        Data = new List<TData>();
    }

    protected BaseResultObjectPagedListModel(IPagedList metaData, List<TData> data)
    {
        Data = data;
        TotalItemCount = metaData.TotalItemCount;
        PageCount = metaData.PageCount;
        HasNextPage = metaData.HasNextPage;
        HasPreviousPage = metaData.HasPreviousPage;
        PageNumber = metaData.PageNumber;
        IsFirstPage = metaData.IsFirstPage;
        IsLastPage = metaData.IsLastPage;
    }

    protected BaseResultObjectPagedListModel(List<TData> data, int pageNumber, int pageCount, int totalItemCount)
    {
        Data = data;
        PageNumber = pageNumber;
        PageCount = pageCount;
        TotalItemCount = totalItemCount;
        HasNextPage = pageNumber < pageCount;
        HasPreviousPage = pageNumber > 1;
        IsFirstPage = pageNumber == 1;
        IsLastPage = pageNumber == pageCount;
    }

    public List<TData> Data { get; set; }
    public int TotalItemCount { get; set; }
    public int PageNumber { get; set; }
    public int PageCount { get; set; }
    public bool HasPreviousPage { get; set; }
    public bool IsFirstPage { get; set; }
    public bool IsLastPage { get; set; }
    public bool HasNextPage { get; set; }
}