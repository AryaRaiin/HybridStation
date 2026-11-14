// SPDX-FileCopyrightText: 2024 Aviu00
// SPDX-FileCopyrightText: 2025 Redrover1760
// SPDX-FileCopyrightText: 2025 starch
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.PowerCell;
using Content.Shared._Mono.Blocking; // Mono
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Power;
using Content.Shared.PowerCell.Components;
using System.Diagnostics.CodeAnalysis;
using Content.Shared.Power.Components;
using Content.Shared.PowerCell; // HS

namespace Content.Server._White.Blocking;

public sealed class RechargeableBlockingSystem : SharedBlockingSystem // Mono
{
    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly ItemToggleSystem _itemToggle = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RechargeableBlockingComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<RechargeableBlockingComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<RechargeableBlockingComponent, ItemToggleActivateAttemptEvent>(AttemptToggle);
        SubscribeLocalEvent<RechargeableBlockingComponent, ChargeChangedEvent>(OnChargeChanged);
        SubscribeLocalEvent<RechargeableBlockingComponent, PowerCellChangedEvent>(OnPowerCellChanged);
    }

    private void OnExamined(EntityUid uid, RechargeableBlockingComponent component, ExaminedEvent args)
    {
        if (!component.Discharged)
        {
            // HS START: Changed from using _powerCell.OnBatteryExamined, as it would've shown duplicate details shown by powerCell, not object attached battery
            if (!HasComp<PowerCellComponent>(uid) && !HasComp<PowerCellSlotComponent>(uid))
            {
                if (TryGetBattery(uid, out var battery, out _))
                {
                    var charge = battery.CurrentCharge / battery.MaxCharge * 100;
                    args.PushMarkup(Loc.GetString("power-cell-component-examine-details",
                        ("currentCharge", $"{charge:F0}")));
                }
                else
                {
                    args.PushMarkup(Loc.GetString("power-cell-component-examine-details-no-battery"));
                }
            }
            // HS END

            return;
        }

        args.PushMarkup(Loc.GetString("rechargeable-blocking-discharged"));
        args.PushMarkup(Loc.GetString("rechargeable-blocking-remaining-time", ("remainingTime", GetRemainingTime(uid))));
    }

    private int GetRemainingTime(EntityUid uid)
    {
        if (!TryGetBattery(uid, out var batteryComponent, out var batteryUid)
            || !TryComp<BatterySelfRechargerComponent>(batteryUid, out var recharger)
            || recharger is not { AutoRechargeRate: > 0, AutoRecharge: true })
            return 0;

        return (int) MathF.Round((batteryComponent.MaxCharge - batteryComponent.CurrentCharge) /
                                 recharger.AutoRechargeRate);
    }

    private void OnDamageChanged(EntityUid uid, RechargeableBlockingComponent component, DamageChangedEvent args)
    {
        if (!TryGetBattery(uid, out var batteryComponent, out var batteryUid)
            || !_itemToggle.IsActivated(uid)
            || args.DamageDelta == null)
            return;

        var batteryUse = Math.Min(args.DamageDelta.GetTotal().Float(), batteryComponent.CurrentCharge);
        _battery.TryUseCharge(batteryUid, batteryUse, batteryComponent);
    }

    private void AttemptToggle(EntityUid uid, RechargeableBlockingComponent component, ref ItemToggleActivateAttemptEvent args)
    {
        if (!component.Discharged)
            return;

        _popup.PopupEntity(Loc.GetString("rechargeable-blocking-remaining-time-popup",
                ("remainingTime", GetRemainingTime(uid))),
            args.User ?? uid);
        args.Cancelled = true;
    }
    private void OnChargeChanged(EntityUid uid, RechargeableBlockingComponent component, ChargeChangedEvent args)
    {
        CheckCharge(uid, component);
    }

    private void OnPowerCellChanged(EntityUid uid, RechargeableBlockingComponent component, PowerCellChangedEvent args)
    {
        CheckCharge(uid, component);
    }

    private void CheckCharge(EntityUid uid, RechargeableBlockingComponent component)
    {
        if (!TryGetBattery(uid, out var battery, out var batteryUid))
            return;

        BatterySelfRechargerComponent? recharger;
        if (battery.CurrentCharge < 1)
        {
            if (TryComp(batteryUid, out recharger))
                recharger.AutoRechargeRate = component.DischargedRechargeRate;

            component.Discharged = true;
            _itemToggle.TryDeactivate(uid, predicted: false);
            return;
        }

        if (battery.CurrentCharge < battery.MaxCharge)
            return;

        component.Discharged = false;
        if (TryComp(batteryUid, out recharger))
            recharger.AutoRechargeRate = component.ChargedRechargeRate;
    }
    // HS START: added TryGetBattery back, used for checking if an object has its own battery before checking the battery slot
    private bool TryGetBattery(EntityUid uid, [NotNullWhen(true)] out BatteryComponent? battery, out EntityUid batteryUid)
    {
        if (TryComp(uid, out battery))
        {
            batteryUid = uid;
            return true;
        }

        if (_powerCell.TryGetBatteryFromSlot(uid, out var slottedUid, out battery))
        {
            batteryUid = slottedUid.Value;
            return true;
        }

        batteryUid = default;
        return false;
    }
    // HS END
}

