namespace Common;

public class MessageItem 
{
    public MessageItem()
    {
        Params = new List<string>();
    }

    public string? Property { get; set; }
    public string? Table { get; set; }
    public string? Code { get; set; }
    public List<string> Params { get; set; }
}
