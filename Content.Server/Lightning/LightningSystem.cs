// SPDX-FileCopyrightText: 2022 keronshb
// SPDX-FileCopyrightText: 2023 AJCM-git
// SPDX-FileCopyrightText: 2023 Leon Friedrich
// SPDX-FileCopyrightText: 2023 Visne
// SPDX-FileCopyrightText: 2024 Ed
// SPDX-FileCopyrightText: 2024 Emisse
// SPDX-FileCopyrightText: 2024 Kara
// SPDX-FileCopyrightText: 2024 Mervill
// SPDX-FileCopyrightText: 2024 TemporalOroboros
// SPDX-FileCopyrightText: 2024 TinManTim
// SPDX-FileCopyrightText: 2024 lzk
// SPDX-FileCopyrightText: 2024 metalgearsloth
// SPDX-FileCopyrightText: 2025 Ilya246
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2022 keronshb <54602815+keronshb@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 AJCM-git <60196617+AJCM-git@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Visne <39844191+Visne@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Ed <96445749+TheShuEd@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Emisse <99158783+Emisse@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Kara <lunarautomaton6@gmail.com>
// SPDX-FileCopyrightText: 2024 Mervill <mervills.email@gmail.com>
// SPDX-FileCopyrightText: 2024 TemporalOroboros <TemporalOroboros@gmail.com>
// SPDX-FileCopyrightText: 2024 TinManTim <73014819+Tin-Man-Tim@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 lzk <124214523+lzk228@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Dark <darkwindleaf@hotmail.co.uk>
// SPDX-FileCopyrightText: 2025 Lachryphage (GitHub)
// SPDX-FileCopyrightText: 2025 V <97265903+formlessnameless@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 hivehum <ketchupfaced@gmail.com>
//
// SPDX-License-Identifier: MIT

using System.Linq;
using System.Numerics;
using Content.Server.Beam;
using Content.Server.Beam.Components;
using Content.Server.Lightning.Components;
using Content.Shared.Lightning;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes; // Mono
using Robust.Shared.Map; // Imp, Supermatter port
using Robust.Shared.Random;

namespace Content.Server.Lightning;

// TheShuEd:
//I've redesigned the lightning system to be more optimized.
//Previously, each lightning element, when it touched something, would try to branch into nearby entities.
//So if a lightning bolt was 20 entities long, each one would check its surroundings and have a chance to create additional lightning...
//which could lead to recursive creation of more and more lightning bolts and checks.

//I redesigned so that lightning branches can only be created from the point where the lightning struck, no more collide checks
//and the number of these branches is explicitly controlled in the new function.
public sealed class LightningSystem : SharedLightningSystem
{
    [Dependency] private readonly BeamSystem _beam = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!; // Mono
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LightningComponent, ComponentRemove>(OnRemove);
    }

    private void OnRemove(EntityUid uid, LightningComponent component, ComponentRemove args)
    {
        if (!TryComp<BeamComponent>(uid, out var lightningBeam) || !TryComp<BeamComponent>(lightningBeam.VirtualBeamController, out var beamController))
        {
            return;
        }

        beamController.CreatedBeams.Remove(uid);
    }

    /// <summary>
    /// Fires lightning from user to target
    /// </summary>
    /// <param name="user">Where the lightning fires from</param>
    /// <param name="target">Where the lightning fires to</param>
    /// <param name="lightningPrototype">The prototype for the lightning to be created</param>
    /// <param name="triggerLightningEvents">if the lightnings being fired should trigger lightning events.</param>
    public void ShootLightning(EntityUid user, EntityUid target, string lightningPrototype = "Lightning", bool triggerLightningEvents = true)
    {
        // Mono
        EntProtoId? spawnOnHit = null;
        var proto = _proto.Index(lightningPrototype);
        if (proto.TryGetComponent<LightningComponent>(out var lightningComp, EntityManager.ComponentFactory))
            spawnOnHit = lightningComp.SpawnOnHit;

        ShootLightning(user, target, lightningPrototype, triggerLightningEvents);
    }

    // Mono - for optimisation purposes
    private void ShootLightning(EntityUid user, EntityUid target, EntProtoId? spawnOnHit, string lightningPrototype = "Lightning", bool triggerLightningEvents = true)
    /// <param name="canExplode">Whether this lightning can trigger explosions.</param>
    public void ShootLightning(EntityUid user, EntityUid target, string lightningPrototype = "Lightning", bool triggerLightningEvents = true, bool canExplode = true)
    {
        var spriteState = LightningRandomizer();
        _beam.TryCreateBeam(user, target, lightningPrototype, spriteState);

        if (triggerLightningEvents) // we don't want certain prototypes to trigger lightning level events
        {
            var ev = new HitByLightningEvent(user, target, canExplode);
            RaiseLocalEvent(target, ref ev);
        }
    }

    /// <summary>
    /// Fires lightning from user to coordinates
    /// </summary>
    /// <param name="user">Where the lightning fires from</param>
    /// <param name="targetCoordinates">Where the lightning fires to</param>
    /// <param name="lightningPrototype">The prototype for the lightning to be created</param>
    public void ShootLightning(EntityUid user, MapCoordinates targetCoordinates, string lightningPrototype = "Lightning")
    {
        var spriteState = LightningRandomizer();
        _beam.TryCreateBeam(user, targetCoordinates, lightningPrototype, spriteState);
    }

    /// <summary>
    /// Fires lightning from coordinates to target
    /// </summary>
    /// <param name="coordinates">Where the lightning fires from</param>
    /// <param name="target">Where the lightning fires to</param>
    /// <param name="lightningPrototype">The prototype for the lightning to be created</param>
    /// <param name="triggerLightningEvents">if the lightnings being fired should trigger lightning events.</param>
    /// <param name="canExplode">Whether this lightning can trigger explosions.</param>
    public void ShootLightning(MapCoordinates coordinates, EntityUid target, string lightningPrototype = "Lightning", bool triggerLightningEvents = true, bool canExplode = true)
    {
        var spriteState = LightningRandomizer();
        _beam.TryCreateBeam(coordinates, target, lightningPrototype, spriteState);

        if (triggerLightningEvents) // we don't want certain prototypes to trigger lightning level events
        {
            var ev = new HitByLightningEvent(null, target, canExplode);
            RaiseLocalEvent(target, ref ev);
        }

        if (spawnOnHit != null)
            Spawn(spawnOnHit.Value, _transform.GetMapCoordinates(target));
    }

    /// <summary>
    /// Fires lightning from coordinates to other coordinates
    /// </summary>
    /// <param name="coordinates">Where the lightning fires from</param>
    /// <param name="targetCoordinates">Where the lightning fires to</param>
    /// <param name="lightningPrototype">The prototype for the lightning to be created</param>
    public void ShootLightning(MapCoordinates coordinates, MapCoordinates targetCoordinates, string lightningPrototype = "Lightning")
    {
        var spriteState = LightningRandomizer();
        _beam.TryCreateBeam(coordinates, targetCoordinates, lightningPrototype, spriteState);
    }


    /// <summary>
    /// Looks for objects with a LightningTarget component in the radius, prioritizes them, and hits the highest priority targets with lightning.
    /// </summary>
    /// <param name="coordinates">Where the lightning fires from</param>
    /// <param name="range">Targets selection radius</param>
    /// <param name="boltCount">Number of lightning bolts</param>
    /// <param name="lightningPrototype">The prototype for the lightning to be created</param>
    /// <param name="arcDepth">how many times to recursively fire lightning bolts from the target points of the first shot.</param>
    /// <param name="triggerLightningEvents">if the lightnings being fired should trigger lightning events.</param>
    public void ShootRandomLightnings(EntityUid user, float range, int boltCount, string lightningPrototype = "Lightning", int arcDepth = 0, bool triggerLightningEvents = true)
    {
        // Mono
        EntProtoId? spawnOnHit = null;
        var proto = _proto.Index(lightningPrototype);
        if (proto.TryGetComponent<LightningComponent>(out var lightningComp, EntityManager.ComponentFactory))
            spawnOnHit = lightningComp.SpawnOnHit;

        ShootRandomLightnings(user, range, boltCount, spawnOnHit, lightningPrototype, arcDepth, triggerLightningEvents);
    }

    // Mono - for optimisation purposes
    private void ShootRandomLightnings(EntityUid user, float range, int boltCount, EntProtoId? spawnOnHit, string lightningPrototype = "Lightning", int arcDepth = 0, bool triggerLightningEvents = true)
    /// <param name="hitCoordsChance">Chance for lightning to strike random coordinates instead of an entity.</param>
    /// <param name="canExplode">Whether this lightning can trigger explosions.</param>
    /// <param name="user">The entity that is shooting lightning.</param>
    public void ShootRandomLightnings(
        MapCoordinates coordinates,
        float range,
        int boltCount,
        string lightningPrototype = "Lightning",
        int arcDepth = 0,
        bool triggerLightningEvents = true,
        float hitCoordsChance = 0f,
        bool canExplode = true,
        EntityUid? user = null)
    {
        //TODO: add support to different priority target tablem for different lightning types
        //TODO: Remove Hardcode LightningTargetComponent (this should be a parameter of the SharedLightningComponent)
        //TODO: This is still pretty bad for perf but better than before and at least it doesn't re-allocate
        // several hashsets every time

        var targets = _lookup.GetEntitiesInRange<LightningTargetComponent>(coordinates, range).ToList();
        _random.Shuffle(targets);
        targets.Sort((x, y) => y.Comp.Priority.CompareTo(x.Comp.Priority));

        int shootedCount = 0;
        int count = -1;
        int mobLightningResistance = 2;
        while (shootedCount < boltCount)
        {
            count++;
            var outOfRange = count >= targets.Count ? true : false;
            var targetLightningResistance = outOfRange ? 0 : targets[count].Comp.LightningResistance;

            if (_random.Prob(hitCoordsChance) && targetLightningResistance <= mobLightningResistance)
            {
                var targetCoordinate = coordinates.Offset(_random.NextVector2(range, range));

                if (user != null)
                    ShootLightning(user.Value, targetCoordinate, lightningPrototype);
                else
                    ShootLightning(coordinates, targetCoordinate, lightningPrototype);

                if (arcDepth > 0)
                {
                    ShootRandomLightnings(targetCoordinate, range, 1, lightningPrototype, arcDepth - 1, triggerLightningEvents, hitCoordsChance, canExplode, user);
                }

                shootedCount++;
                continue;
            }

            if (outOfRange) { break; }

            var curTarget = targets[count];
            if (!_random.Prob(curTarget.Comp.HitProbability)) //Chance to ignore target
                continue;

            ShootLightning(user, targets[count].Owner, spawnOnHit, lightningPrototype, triggerLightningEvents);
            if (arcDepth - targets[count].Comp.LightningResistance > 0)
            if (user != null)
                ShootLightning(user.Value, targets[count].Owner, lightningPrototype, triggerLightningEvents, canExplode);
            else
                ShootLightning(coordinates, targets[count].Owner, lightningPrototype, triggerLightningEvents, canExplode);

            if (arcDepth - targetLightningResistance > 0)
            {
                ShootRandomLightnings(targets[count].Owner, range, 1, spawnOnHit, lightningPrototype, arcDepth - targets[count].Comp.LightningResistance, triggerLightningEvents);
                ShootRandomLightnings(targets[count].Owner, range, 1, lightningPrototype, arcDepth - targetLightningResistance, triggerLightningEvents, hitCoordsChance);
            }
            shootedCount++;
        }
    }

    /// <summary>
    /// Looks for objects with a LightningTarget component in the radius, prioritizes them, and hits the highest priority targets with lightning.
    /// </summary>
    /// <param name="user">Where the lightning fires from</param>
    /// <param name="range">Targets selection radius</param>
    /// <param name="boltCount">Number of lightning bolts</param>
    /// <param name="lightningPrototype">The prototype for the lightning to be created</param>
    /// <param name="arcDepth">how many times to recursively fire lightning bolts from the target points of the first shot.</param>
    /// <param name="triggerLightningEvents">if the lightnings being fired should trigger lightning events.</param>
    /// <param name="hitCoordsChance">Chance for lightning to strike random coordinates instead of an entity.</param>
    /// <param name="canExplode">Whether this lightning can trigger explosions.</param>
    public void ShootRandomLightnings(EntityUid user, float range, int boltCount, string lightningPrototype = "Lightning", int arcDepth = 0, bool triggerLightningEvents = true, float hitCoordsChance = 0f, bool canExplode = true)
    {
        ShootRandomLightnings(_transform.GetMapCoordinates(user), range, boltCount, lightningPrototype, arcDepth, triggerLightningEvents, hitCoordsChance, canExplode, user);
    }
}

/// <summary>
/// Raised directed on the target when an entity becomes the target of a lightning strike (not when touched)
/// </summary>
/// <param name="Source">The entity that created the lightning</param>
/// <param name="Target">The entity that was struck by lightning.</param>
/// <param name="CanExplode">Whether this event can trigger explosions.</param>
[ByRefEvent]
public readonly record struct HitByLightningEvent(EntityUid? Source, EntityUid Target, bool CanExplode); // imp - added CanExplode
