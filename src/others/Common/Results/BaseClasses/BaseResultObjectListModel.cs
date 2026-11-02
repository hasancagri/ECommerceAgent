namespace Common.Results.BaseClasses;

public abstract class BaseResultObjectListModel<TData> : BaseResultModel, IResultObjectListModel<TData>
    where TData : class
{
    protected BaseResultObjectListModel()
    {
        Data = new List<TData>();
    }
    public List<TData> Data { get; set; }
}