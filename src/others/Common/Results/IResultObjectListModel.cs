namespace Common.Results;

public interface IResultObjectListModel<TData> : IResultModel
      where TData : class
{
    List<TData> Data { get; set; }
}