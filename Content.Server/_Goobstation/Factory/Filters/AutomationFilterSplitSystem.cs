// SPDX-License-Identifier: AGPL-3.0-or-later

// Hybridstation update to Goob factory code, split off event due to upstream changes from shared to server

using Content.Server.Stack;
using Content.Shared._Goobstation.Factory.Filters;
using Content.Shared.Stacks;

namespace Content.Server._Goobstation.Factory.Filters;

public sealed class AutomationFilterSplitSystem : EntitySystem
{
    [Dependency]
    private readonly StackSystem _stack = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AutomationFilterSplitRequestEvent>(OnSplitRequest);
    }

    private void OnSplitRequest(AutomationFilterSplitRequestEvent args)
    {
        if (!TryComp<StackComponent>(args.Item, out var stack))
        {
            args.Result = null;
            return;
        }

        args.Result = _stack.Split(
            (args.Item, stack),
            args.Amount,
            args.Coordinates);
    }
}
