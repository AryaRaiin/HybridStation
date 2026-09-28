using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;

namespace Content.Shared._Goobstation.Factory.Slots;

/// <summary>
/// Abstraction over a specific hand of the machine.
/// </summary>
public sealed partial class AutomatedHand : AutomationSlot
{
    /// <summary>
    /// The name of the hand to use
    /// </summary>
    [DataField(required: true)]
    public string HandName = string.Empty;

    private SharedHandsSystem _hands;

    private Hand? _hand;

    [ViewVariables]
    public Hand? Hand
    {
        get
        {
            if (_hand != null)
                return _hand;

            _hands.TryGetHand(Owner, HandName, out _hand);
            return _hand;
        }
    }

    public override void Initialize()
    {
        base.Initialize();

        _hands = EntMan.System<SharedHandsSystem>();
    }

    public override bool Insert(EntityUid item)
    {
        return Hand is not null
            && base.Insert(item)
            && _hands.TryPickup(Owner, item, HandName);
    }

    public override bool CanInsert(EntityUid item)
    {
        return Hand is not null
            && base.CanInsert(item)
            && _hands.CanPickupToHand(Owner, item, HandName);
    }

    public override EntityUid? GetItem(EntityUid? filter)
    {
        var item = _hands.GetHeldItem(Owner, HandName);
        if (item is not { } held
            || _filter.IsBlocked(filter, held))
            return null;

        return held;
    }
}
