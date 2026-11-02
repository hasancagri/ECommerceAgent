namespace Common.Exceptions;

public class ValidationException : Exception
{
    public ValidationException(MessageItem messageItem)
    {
        MessageItem = messageItem;
    }

    public MessageItem MessageItem { get; private set; }
}
