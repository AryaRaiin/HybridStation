using Robust.Shared.GameStates;

namespace Content.Shared.Shuttles.Components;

/// <summary>
///     Cleanup component that deletes the entity if it has a cross-grid collision.
///     Useful for small, unimportant items like bullets to avoid generating many contacts.
/// </summary>
[RegisterComponent, NetworkedComponent]
//public sealed partial class SpaceGarbageComponent : Component;
// Mono START - add definition
public sealed partial class SpaceGarbageComponent : Component
{
    /// <summary>
    /// Signifies an entity to be ignored by SpaceGarbageCleanupSystem. - Mono
    /// </summary>
    [DataField("cleanupExempt")]
    public bool CleanupExempt = false;
};
// Mono END
