namespace Common;

public class FeatureResultModel : BaseResultModel
{
    public static FeatureResultModel Ok()
    {
        return new FeatureResultModel
        {
            IsSuccess = true
        };
    }

    public static FeatureResultModel Error(List<MessageItem>? messageItems)
    {
        return new FeatureResultModel
        {
            IsSuccess = false,
            Messages = messageItems
        };
    }
    public static FeatureResultModel NotFound()
    {
        var resultModel = new FeatureResultModel
        {
            IsSuccess = false,
            Messages = new List<MessageItem>
            {
                new MessageItem()
                {
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                }
            }
        };
        return resultModel;
    }
    public static FeatureResultModel Error(MessageItem messageItem)
    {
        return Error(new List<MessageItem> { messageItem });
    }
}

public class FeatureObjectResultModel<T> : BaseResultObjectModel<T>
    where T : class, new()
{
    public static FeatureObjectResultModel<T> Ok(T? data)
    {
        if (data is null)
        {
            return NotFound();
        }

        return new FeatureObjectResultModel<T>
        {
            Data = data,
            IsSuccess = true
        };
    }

    public static FeatureObjectResultModel<T> Error(List<MessageItem>? messageItems)
    {
        return new FeatureObjectResultModel<T>
        {
            IsSuccess = false,
            Messages = messageItems
        };
    }

    public static FeatureObjectResultModel<T> Error(MessageItem messageItem)
    {
        return Error(new List<MessageItem> { messageItem });
    }
    public static FeatureObjectResultModel<T> NotFound()
    {
        var resultModel = new FeatureObjectResultModel<T>
        {
            Data = null,
            IsSuccess = false,
            Messages = new List<MessageItem>
            {
                new MessageItem()
                {
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                }
            }
        };
        return resultModel;
    }
}

public class FeatureListResultModel<T> : BaseResultObjectListModel<T>
    where T : class
{
    public static FeatureListResultModel<T> Ok(List<T> data)
    {
        if (data.Count == 0)
        {
            return NotFound();
        }

        var resultModel = new FeatureListResultModel<T>
        {
            Data = data,
            IsSuccess = true
        };
        return resultModel;
    }

    public static FeatureListResultModel<T> NotFound()
    {
        var resultModel = new FeatureListResultModel<T>
        {
            IsSuccess = false,
            Messages = new List<MessageItem>
            {
                new MessageItem()
                {
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                }
            }
        };
        return resultModel;
    }

    public static FeatureListResultModel<T> Error(List<MessageItem> messageItems)
    {
        return new FeatureListResultModel<T>
        {
            IsSuccess = false,
            Messages = messageItems
        };
    }

    public static FeatureListResultModel<T> Error(MessageItem messageItem)
    {
        return Error(new List<MessageItem> { messageItem });
    }
}

public class FeaturePagedResultModel<T> : BaseResultObjectPagedListModel<T>
    where T : class, new()
{
    public FeaturePagedResultModel()
    {
    }

    public FeaturePagedResultModel(IPagedList metaData, List<T> data) : base(metaData, data)
    {
    }

    public static FeaturePagedResultModel<T> Ok(IPagedList metaData, List<T> data)
    {
        if (data.Count == 0)
        {
            return NotFound();
        }

        var resultModel = new FeaturePagedResultModel<T>(metaData, data)
        {
            IsSuccess = true
        };
        return resultModel;
    }

    public static FeaturePagedResultModel<T> NotFound()
    {
        var resultModel = new FeaturePagedResultModel<T>
        {
            IsSuccess = false,
            Messages = new List<MessageItem>
            {
                new MessageItem()
                {
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                }
            }
        };
        return resultModel;
    }
}