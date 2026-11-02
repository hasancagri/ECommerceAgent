namespace Common.Results;

public class ResultDomain: BaseResultModel
{
    public static ResultDomain Ok()
    {
        return new ResultDomain
        {
            IsSuccess = true,
        };
    }
    public static ResultDomain Error()
    {

        return new ResultDomain
        {
            IsSuccess = false
        };
    }
    public static ResultDomain Error(List<MessageItem> messageItems)
    {

        return new ResultDomain
        {
            IsSuccess = false,
            Messages = messageItems
        };
    }
    public static ResultDomain Error(MessageItem messageItem)
    {
        return Error(new List<MessageItem> { messageItem });
    }
}
public class ResultDomain<T> : BaseResultModel
{
   
    public static ResultDomain<T> Ok(T data)
    {
        return new ResultDomain<T>
        {
            IsSuccess = true,
            Data = data
        };
    }
    public T? Data { get; init; }
    public static ResultDomain<T> Error(List<MessageItem> messageItems)
    {

        return new ResultDomain<T>
        {
            IsSuccess = false,
            Messages = messageItems
        };
    }
    public static ResultDomain<T> Error(MessageItem messageItem)
    {
        return Error(new List<MessageItem> { messageItem });
    }
}