// SPDX-FileCopyrightText: 2025 Avalon
//
// SPDX-License-Identifier: MPL-2.0

using Content.Shared.Damage;
using Content.Shared.Explosion.EntitySystems;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Robust.Shared.Network;

namespace Content.Shared.Weapons.Hitscan.Systems;

public sealed class HitscanSpawnEntitySystem : EntitySystem
{
    [Dependency] private readonly SharedExplosionSystem _explosion = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HitscanSpawnEntityComponent, HitscanRaycastFiredEvent>(OnHitscanHit, after: [typeof(HitscanReflectSystem)]);
    }

    private void OnHitscanHit(Entity<HitscanSpawnEntityComponent> ent, ref HitscanRaycastFiredEvent args)
    {
        if (_net.IsClient || args.Data.HitEntity == null)
            return;

        Spawn(ent.Comp.SpawnedEntity, Transform(args.Data.HitEntity.Value).Coordinates);

        // TODO: Mono - maybe split up the effects component or something - this won't play sounds and stuff (maybe that's ok?)
    }
}
