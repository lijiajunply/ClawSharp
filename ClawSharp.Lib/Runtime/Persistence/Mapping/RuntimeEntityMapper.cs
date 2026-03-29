namespace ClawSharp.Lib.Runtime;

internal static class RuntimeEntityMapper
{
    public static ThreadSpaceEntity ToEntity(ThreadSpaceRecord record) => new()
    {
        ThreadSpaceId = record.ThreadSpaceId.Value,
        Name = record.Name,
        BoundFolderPath = record.BoundFolderPath,
        IsInit = record.IsInit,
        CreatedAt = record.CreatedAt,
        ArchivedAt = record.ArchivedAt
    };

    public static ThreadSpaceRecord ToRecord(ThreadSpaceEntity entity) =>
        new(new ThreadSpaceId(entity.ThreadSpaceId), entity.Name, entity.BoundFolderPath, entity.IsInit, entity.CreatedAt, entity.ArchivedAt);

    public static SessionEntity ToEntity(SessionRecord record) => new()
    {
        SessionId = record.SessionId.Value,
        ThreadSpaceId = record.ThreadSpaceId.Value,
        AgentId = record.AgentId,
        WorkspaceRoot = record.WorkspaceRoot,
        Status = record.Status,
        StartedAt = record.StartedAt,
        EndedAt = record.EndedAt
    };

    public static SessionRecord ToRecord(SessionEntity entity) =>
        new(new SessionId(entity.SessionId), new ThreadSpaceId(entity.ThreadSpaceId), entity.AgentId, entity.WorkspaceRoot, entity.Status, entity.StartedAt, entity.EndedAt);

    public static MessageEntity ToEntity(PromptMessage message) => new()
    {
        MessageId = message.MessageId.Value,
        SessionId = message.SessionId.Value,
        TurnId = message.TurnId.Value,
        Role = message.Role,
        Content = message.Content,
        Name = message.Name,
        ToolCallId = message.ToolCallId,
        BlocksJson = JsonSessionSerializerHelper.SerializeBlocks(message.Blocks),
        SequenceNo = message.SequenceNo,
        CreatedAt = message.CreatedAt
    };

    public static PromptMessage ToRecord(MessageEntity entity) =>
        new(
            new MessageId(entity.MessageId),
            new SessionId(entity.SessionId),
            new TurnId(entity.TurnId),
            entity.Role,
            JsonSessionSerializerHelper.ParseBlocks(entity.BlocksJson, entity.Role, entity.Content, entity.Name, entity.ToolCallId),
            entity.SequenceNo,
            entity.CreatedAt);

    public static SessionEventEntity ToEntity(SessionEvent sessionEvent) => new()
    {
        EventId = sessionEvent.EventId.Value,
        SessionId = sessionEvent.SessionId.Value,
        TurnId = sessionEvent.TurnId.Value,
        EventType = sessionEvent.EventType,
        PayloadJson = sessionEvent.Payload.GetRawText(),
        SequenceNo = sessionEvent.SequenceNo,
        CreatedAt = sessionEvent.CreatedAt
    };

    public static SessionEvent ToRecord(SessionEventEntity entity) =>
        new(
            new EventId(entity.EventId),
            new SessionId(entity.SessionId),
            new TurnId(entity.TurnId),
            entity.EventType,
            JsonSessionSerializerHelper.ParseElement(entity.PayloadJson),
            entity.SequenceNo,
            entity.CreatedAt);
}
