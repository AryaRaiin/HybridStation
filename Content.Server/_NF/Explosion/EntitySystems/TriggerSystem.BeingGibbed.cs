using Content.Server.Body.Components;
using Content.Server.Explosion.Components;
using Content.Shared.Implants;
using Content.Shared.Trigger.Systems;

namespace Content.Server._NF.Explosion.EntitySystems;

public sealed class TriggerOnBeingGibbedSystem : EntitySystem
{
    [Dependency] private readonly TriggerSystem _trigger = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TriggerOnBeingGibbedComponent, BeforeGibbedEvent>(OnBeingGibbed);
        SubscribeLocalEvent<TriggerOnBeingGibbedComponent, ImplantRelayEvent<BeforeGibbedEvent>>(OnBeingGibbedRelay);
    }

    private void OnBeingGibbed(EntityUid uid, TriggerOnBeingGibbedComponent component, ref BeforeGibbedEvent args)
    {
        _trigger.Trigger(uid);
    }

    private void OnBeingGibbedRelay(EntityUid uid, TriggerOnBeingGibbedComponent component, ImplantRelayEvent<BeforeGibbedEvent> args)
    {
        _trigger.Trigger(uid);
    }
}
