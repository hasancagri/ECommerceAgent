namespace ScoutAgent.Api.Constants;

// 007: ScoutAgent kendi hata kodlarına sahip (serbest metin değil resource sabiti — İlke IV).
public static class ScoutResourceConstants
{
    public const string VALUE_EMPTY = "scout.value.empty";
    public const string CONVERSATION_USER_REQUIRED = "scout.conversation.user_required";
    public const string CONVERSATION_MESSAGE_EMPTY = "scout.conversation.message_empty";
}

// 007: Marten şema adı (tek yer). tr-TR ı tuzağından kaçınmak için DocumentAlias elle verilir.
public static class ScoutSchemaConstants
{
    public const string SchemaName = "scout";
    public const string ConversationAlias = "conversation";
}
