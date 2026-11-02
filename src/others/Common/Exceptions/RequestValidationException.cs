namespace Common.Exceptions;

public class RequestValidationException : Exception
{
    public RequestValidationException(List<MessageItem> messages)
    {
        Messages = messages;
    }
    public List<MessageItem> Messages { get; private set; }
}
