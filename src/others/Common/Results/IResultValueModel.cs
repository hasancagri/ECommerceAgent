namespace Common.Results;

public interface IResultValueModel<TValue> : IResultModel
{
    TValue Value { get; set; }
}