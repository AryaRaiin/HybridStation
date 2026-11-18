using Content.Shared.Chat;

namespace Content.Server._Mono.Chat.Events;

//HS: Moved out of ChatSystem.cs due to upstream changes, but needed for Mono cortical borers.
public sealed class CheckTargetedSpeechEvent : EntityEventArgs
{
    public List<EntityUid> Targets { get; } = new();
    public HashSet<InGameICChatType> ChatTypeIgnore { get; } = new();
}
