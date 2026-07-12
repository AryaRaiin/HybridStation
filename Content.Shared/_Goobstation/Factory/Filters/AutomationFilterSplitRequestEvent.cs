// SPDX-License-Identifier: AGPL-3.0-or-later

// Hybridstation update to Goob factory code, split off event due to upstream changes from shared to server

using Robust.Shared.Map;

namespace Content.Shared._Goobstation.Factory.Filters;

public sealed class AutomationFilterSplitRequestEvent : EntityEventArgs
{
    public EntityUid Item;
    public int Amount;
    public EntityCoordinates Coordinates;

    public EntityUid? Result;
}
