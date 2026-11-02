namespace Common.Results.BaseClasses;

public abstract class BaseResultObjectModel<TData> : BaseResultModel, IResultObjectModel<TData>
    where TData : class, new()
{
    protected BaseResultObjectModel()
    {
        Data = new TData();
    }

    public TData Data { get; set; }
}