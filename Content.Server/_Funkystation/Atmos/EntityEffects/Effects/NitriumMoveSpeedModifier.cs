// SPDX-FileCopyrightText: 2024 SlamBamActionman
// SPDX-FileCopyrightText: 2025 marc-pelletier
// SPDX-FileCopyrightText: 2025 tonotom
// SPDX-FileCopyrightText: 2025 tonotom1
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Chemistry.Components;
using Content.Shared.EntityEffects;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server.EntityEffects.Effects;

/// <summary>
/// Default metabolism for stimulants and tranqs. Attempts to find a MovementSpeedModifier on the target,
/// adding one if not there and to change the movespeed
/// </summary>
public sealed partial class NitriumMovespeedModifier : EntityEffect
{
    /// <summary>
    /// How much the entities' walk speed is multiplied by.
    /// </summary>
    [DataField]
    public float WalkSpeedModifier { get; set; } = 1;

    /// <summary>
    /// How much the entities' run speed is multiplied by.
    /// </summary>
    [DataField]
    public float SprintSpeedModifier { get; set; } = 1;

    /// <summary>
    /// How long the modifier applies (in seconds).
    /// Is scaled by reagent amount if used with an EntityEffectReagentArgs.
    /// </summary>
    [DataField]
    public float StatusLifetime = 6f;

    public override void RaiseEvent(EntityUid target, IEntityEffectRaiser raiser, float scale, EntityUid? user)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        var status = entMan.EnsureComponent<MovespeedModifierMetabolismComponent>(target);

        // Only refresh movement if we need to.
        var modified = !status.WalkSpeedModifier.Equals(WalkSpeedModifier) ||
                       !status.SprintSpeedModifier.Equals(SprintSpeedModifier);

        status.WalkSpeedModifier = WalkSpeedModifier;
        status.SprintSpeedModifier = SprintSpeedModifier;

        IncreaseTimer(status, StatusLifetime);

        if (modified)
            entMan.System<MovementSpeedModifierSystem>().RefreshMovementSpeedModifiers(target);
    }

    public void IncreaseTimer(MovespeedModifierMetabolismComponent status, float time)
    {
        var gameTiming = IoCManager.Resolve<IGameTiming>();

        var offsetTime = Math.Max(status.ModifierTimer.TotalSeconds, gameTiming.CurTime.TotalSeconds);

        status.ModifierTimer = TimeSpan.FromSeconds(offsetTime + time);
        status.Dirty();
    }
}
