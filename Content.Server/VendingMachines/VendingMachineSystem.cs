// SPDX-FileCopyrightText: 2022 Alex Evgrashin
// SPDX-FileCopyrightText: 2022 Andreas Kämper
// SPDX-FileCopyrightText: 2022 EmoGarbage404
// SPDX-FileCopyrightText: 2022 Fishfish458
// SPDX-FileCopyrightText: 2022 Flipp Syder
// SPDX-FileCopyrightText: 2022 Rane
// SPDX-FileCopyrightText: 2022 Visne
// SPDX-FileCopyrightText: 2022 fishfish458 <fishfish458>
// SPDX-FileCopyrightText: 2023 Cheackraze
// SPDX-FileCopyrightText: 2023 DrSmugleaf
// SPDX-FileCopyrightText: 2023 Leon Friedrich
// SPDX-FileCopyrightText: 2023 Nemanja
// SPDX-FileCopyrightText: 2023 Slava0135
// SPDX-FileCopyrightText: 2023 Vordenburg
// SPDX-FileCopyrightText: 2023 deltanedas
// SPDX-FileCopyrightText: 2023 keronshb
// SPDX-FileCopyrightText: 2024 Checkraze
// SPDX-FileCopyrightText: 2024 Dvir
// SPDX-FileCopyrightText: 2024 Ed
// SPDX-FileCopyrightText: 2024 GreaseMonk
// SPDX-FileCopyrightText: 2024 LordCarve
// SPDX-FileCopyrightText: 2024 Niels Huylebroeck
// SPDX-FileCopyrightText: 2024 Pieter-Jan Briers
// SPDX-FileCopyrightText: 2024 Repo
// SPDX-FileCopyrightText: 2024 Robert V
// SPDX-FileCopyrightText: 2024 Tayrtahn
// SPDX-FileCopyrightText: 2024 checkraze
// SPDX-FileCopyrightText: 2024 goet
// SPDX-FileCopyrightText: 2024 lzk
// SPDX-FileCopyrightText: 2024 metalgearsloth
// SPDX-FileCopyrightText: 2025 Ark
// SPDX-FileCopyrightText: 2025 ScarKy0
// SPDX-FileCopyrightText: 2025 Whatstone
// SPDX-FileCopyrightText: 2025 ark1368
//
// SPDX-License-Identifier: MPL-2.0

using System.Linq;
using System.Numerics;
using Content.Server.Cargo.Systems;
using Content.Server.Power.Components;
using Content.Server.Vocalization.Systems;
using Content.Shared.Cargo;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible;
using Content.Shared.Emp;
using Content.Shared.Power;
using Content.Shared.Throwing;
using Content.Shared.UserInterface;
using Content.Shared.VendingMachines;
using Content.Shared.Wall;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Server._NF.Bank; // Frontier
using Content.Shared._NF.Bank.Components; // Frontier
using Robust.Shared.Timing; // Frontier?
using Robust.Shared.Audio.Systems; // Frontier?
using Content.Server.Administration.Logs; // Frontier
using Content.Shared.Database; // Frontier
using Content.Shared._NF.Bank.BUI; // Frontier
using Content.Server._NF.Contraband.Systems; // Frontier
using Content.Shared.Stacks; // Frontier
using Content.Server.Stack; // Frontier?
using Content.Server._Mono.VendingMachine; // Mono
using Content.Server.Popups; // Frontier
using Content.Shared._Mono.Traits.Physical; // Mono
using Robust.Shared.Containers; // Frontier

namespace Content.Server.VendingMachines
{
    public sealed class VendingMachineSystem : SharedVendingMachineSystem
    {
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly PricingSystem _pricing = default!;
        [Dependency] private readonly ThrowingSystem _throwingSystem = default!;

        [Dependency] private readonly IPrototypeManager _prototypeManager = default!; // Frontier
        [Dependency] private readonly SharedAudioSystem _audioSystem = default!; // Frontier
        [Dependency] private readonly BankSystem _bankSystem = default!; // Frontier
        [Dependency] private readonly PopupSystem _popupSystem = default!; // Frontier
        [Dependency] private readonly IAdminLogManager _adminLogger = default!; // Frontier
        [Dependency] private readonly ContrabandTurnInSystem _contraband = default!; // Frontier
        [Dependency] private readonly StackSystem _stack = default!; // Frontier
        [Dependency] private readonly VendingMachinePurchaseSystem _vendingPurchase = default!; // Mono

        private const float WallVendEjectDistanceFromWall = 1f;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<VendingMachineComponent, PowerChangedEvent>(OnPowerChanged);
            SubscribeLocalEvent<VendingMachineComponent, BreakageEventArgs>(OnBreak);
            SubscribeLocalEvent<VendingMachineComponent, DamageChangedEvent>(OnDamageChanged);
            SubscribeLocalEvent<VendingMachineComponent, PriceCalculationEvent>(OnVendingPrice);
            SubscribeLocalEvent<VendingMachineComponent, TryVocalizeEvent>(OnTryVocalize);

            SubscribeLocalEvent<VendingMachineComponent, ActivatableUIOpenAttemptEvent>(OnActivatableUIOpenAttempt);
            SubscribeLocalEvent<VendingMachineComponent, VendingMachineSelfDispenseEvent>(OnSelfDispense);

            SubscribeLocalEvent<VendingMachineRestockComponent, PriceCalculationEvent>(OnPriceCalculation);

            SubscribeLocalEvent<VendingMachineComponent, EntInsertedIntoContainerMessage>(OnEntityInserted); // Frontier
            SubscribeLocalEvent<VendingMachineComponent, EntRemovedFromContainerMessage>(OnEntityRemoved); // Frontier
        }

        private void OnVendingPrice(EntityUid uid, VendingMachineComponent component, ref PriceCalculationEvent args)
        {
            var price = 0.0;

            foreach (var entry in component.Inventory.Values)
            {
                if (!PrototypeManager.TryIndex<EntityPrototype>(entry.ID, out var proto))
                {
                    Log.Error($"Unable to find entity prototype {entry.ID} on {ToPrettyString(uid)} vending.");
                    continue;
                }

                //price += entry.Amount * _pricing.GetEstimatedPrice(proto);
                price += entry.Amount; // Frontier - This is used to price the worth of a vending machine with the inventory it has.
            }

            //args.Price += price; // Frontier Disabled Upstream - This is used to price the worth of a vending machine with the inventory it has.
        }

        protected override void OnMapInit(EntityUid uid, VendingMachineComponent component, MapInitEvent args)
        {
            base.OnMapInit(uid, component, args);

            if (HasComp<ApcPowerReceiverComponent>(uid))
            {
                TryUpdateVisualState((uid, component));
            }
        }

        private void OnActivatableUIOpenAttempt(EntityUid uid, VendingMachineComponent component, ActivatableUIOpenAttemptEvent args)
        {
            if (component.Broken)
                args.Cancel();
        }

        private void OnPowerChanged(EntityUid uid, VendingMachineComponent component, ref PowerChangedEvent args)
        {
            TryUpdateVisualState((uid, component));
        }

        private void OnBreak(EntityUid uid, VendingMachineComponent vendComponent, BreakageEventArgs eventArgs)
        {
            vendComponent.Broken = true;
            Dirty(uid, vendComponent);
            TryUpdateVisualState((uid, vendComponent));
        }

        private void OnDamageChanged(EntityUid uid, VendingMachineComponent component, DamageChangedEvent args)
        {
            if (!args.DamageIncreased && component.Broken)
            {
                component.Broken = false;
                Dirty(uid, component);
                TryUpdateVisualState((uid, component));
                return;
            }

            if (component.Broken || component.DispenseOnHitCoolingDown ||
                component.DispenseOnHitChance == null || args.DamageDelta == null)
                return;

            if (args.DamageIncreased && args.DamageDelta.GetTotal() >= component.DispenseOnHitThreshold &&
                _random.Prob(component.DispenseOnHitChance.Value))
            {
                if (component.DispenseOnHitCooldown != null)
                {
                    component.DispenseOnHitEnd = Timing.CurTime + component.DispenseOnHitCooldown.Value;
                }

                EjectRandom(uid, throwItem: true, forceEject: true, component);
            }
        }

        private void OnSelfDispense(EntityUid uid, VendingMachineComponent component, VendingMachineSelfDispenseEvent args)
        {
            if (args.Handled)
                return;

            args.Handled = true;
            EjectRandom(uid, throwItem: true, forceEject: false, component);
        }

        /// <summary>
        /// Sets the <see cref="VendingMachineComponent.CanShoot"/> property of the vending machine.
        /// </summary>
        public void SetShooting(EntityUid uid, bool canShoot, VendingMachineComponent? component = null)
        {
            if (!Resolve(uid, ref component))
                return;

            component.CanShoot = canShoot;
        }

        /// <summary>
        /// Sets the <see cref="VendingMachineComponent.Contraband"/> property of the vending machine.
        /// </summary>
        public void SetContraband(EntityUid uid, bool contraband, VendingMachineComponent? component = null)
        {
            if (!Resolve(uid, ref component))
                return;

            component.Contraband = contraband;
            Dirty(uid, component);
        }

        /// <summary>
        /// Ejects a random item from the available stock. Will do nothing if the vending machine is empty.
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="throwItem">Whether to throw the item in a random direction after dispensing it.</param>
        /// <param name="forceEject">Whether to skip the regular ejection checks and immediately dispense the item without animation.</param>
        /// <param name="vendComponent"></param>
        public void EjectRandom(EntityUid uid, bool throwItem, bool forceEject = false, VendingMachineComponent? vendComponent = null)
        {
            if (!Resolve(uid, ref vendComponent))
                return;

            var availableItems = GetAvailableInventory(uid, vendComponent);
            if (availableItems.Count <= 0)
                return;

            var item = _random.Pick(availableItems);

            if (forceEject)
            {
                vendComponent.NextItemToEject = item.ID;
                vendComponent.ThrowNextItem = throwItem;
                var entry = GetEntry(uid, item.ID, item.Type, vendComponent);
                if (entry != null)
                    entry.Amount--;
                EjectItem(uid, vendComponent, forceEject);
            }
            else
            {
                TryEjectVendorItem(uid, item.Type, item.ID, throwItem, user: null, vendComponent: vendComponent);
            }
        }

        protected override void EjectItem(EntityUid uid, VendingMachineComponent? vendComponent = null, bool forceEject = false)
        {
            if (!Resolve(uid, ref vendComponent))
                return;

            // No need to update the visual state because we never changed it during a forced eject
            if (!forceEject)
                TryUpdateVisualState((uid, vendComponent));

            if (string.IsNullOrEmpty(vendComponent.NextItemToEject))
            {
                vendComponent.ThrowNextItem = false;
                return;
            }

            // Default spawn coordinates
            var xform = Transform(uid);
            var spawnCoordinates = xform.Coordinates;

            //Make sure the wallvends spawn outside of the wall.
            if (TryComp<WallMountComponent>(uid, out var wallMountComponent))
            {
                var offset = (wallMountComponent.Direction + xform.LocalRotation - Math.PI / 2).ToVec() * WallVendEjectDistanceFromWall;
                spawnCoordinates = spawnCoordinates.Offset(offset);
            }

            var ent = Spawn(vendComponent.NextItemToEject, spawnCoordinates);

            _contraband.ClearContrabandValue(ent); // Frontier

            // MONO START: Track vending machine purchases for pricing modifications
            // Only track if this was a paid purchase (not a random eject or force eject)
            if (!forceEject && vendComponent.LastPurchasePrice.HasValue)
            {
                _vendingPurchase.MarkAsPurchased(ent, uid, vendComponent.LastPurchasePrice.Value);
                vendComponent.LastPurchasePrice = null; // Clear after use
            }
            // MONO END

            if (vendComponent.ThrowNextItem)
            {
                var range = vendComponent.NonLimitedEjectRange;
                var direction = new Vector2(_random.NextFloat(-range, range), _random.NextFloat(-range, range));
                _throwingSystem.TryThrow(ent, direction, vendComponent.NonLimitedEjectForce);
            }

            vendComponent.NextItemToEject = null;
            vendComponent.ThrowNextItem = false;
        }

        // FRONTIER START - add this function, override shared
        /// <summary>
        /// Checks whether the user is authorized to use the vending machine, then ejects the provided item if true
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="sender">Entity that is trying to use the vending machine</param>
        /// <param name="type">The type of inventory the item is from</param>
        /// <param name="itemId">The prototype ID of the item</param>
        /// <param name="component"></param>
        public void AuthorizedVend(EntityUid uid, EntityUid sender, InventoryType type, string itemId, VendingMachineComponent component)
        {
            // FRONTIER START
            if (!PrototypeManager.TryIndex<EntityPrototype>(itemId, out var proto))
                return;

            var price = _pricing.GetEstimatedPrice(proto); // TO DO: _pricing is coming from a server file... how do we fix this? It's calling PricingSystem.cs
            // Somewhere deep in the code of pricing, a hardcoded 20 dollar value exists for anything without
            // a staticprice component for some god forsaken reason, and I cant find it or think of another way to
            // get an accurate price from a prototype with no staticprice comp.
            // this will undoubtably lead to vending machine exploits if I cant find wtf pricing system is doing.
            // also stacks, food, solutions, are handled poorly too f
            if (price == 0)
                price = 20;


            if (TryComp<MarketModifierComponent>(sender, out var modifier))
                price *= modifier.Mod;

            var totalPrice = (int) price;

            // If any price has a vendor price, explicitly use its value - higher OR lower, over others.
            var priceVend = _pricing.GetEstimatedVendPrice(proto);
            if (priceVend > 0.0) // if vending price exists, overwrite it.
                totalPrice = (int) priceVend;
            // FRONTIER END

            if (IsAuthorized(uid, sender, component))
            {
                // FRONTIER START
                int bankBalance = 0;
                if (!HasComp<IronmanComponent>(sender) && TryComp<BankAccountComponent>(sender, out var bank))
                    bankBalance = bank.Balance;
                int cashSlotBalance = 0;
                Entity<StackComponent>? cashEntity = null;
                if (component.CashSlotName != null
                    && component.CurrencyStackType != null
                    && ItemSlots.TryGetSlot(uid, component.CashSlotName, out var cashSlot)
                    && TryComp<StackComponent>(cashSlot?.ContainerSlot?.ContainedEntity, out var stackComp)
                    && stackComp!.StackTypeId == component.CurrencyStackType)
                {
                    cashSlotBalance = stackComp!.Count;
                    cashEntity = (cashSlot!.ContainerSlot!.ContainedEntity.Value, stackComp!);
                }
                if (totalPrice > bankBalance + cashSlotBalance)
                {
                    _popupSystem.PopupEntity(Loc.GetString("bank-insufficient-funds"), uid);
                    Deny( uid, sender );
                    return;
                }
                bool paidFully = false;
                // Mono: Store the purchase price for tracking
                component.LastPurchasePrice = totalPrice;
                //TryEjectVendorItem(uid, type, itemId, component.CanShoot, sender, component); // Upstream method, doesn't have rv
                bool ejectsuccess=false;
                TryEjectVendorItem(uid, type, itemId, component.CanShoot, sender, component, ref ejectsuccess);
                if (ejectsuccess)
                {
                    if (cashEntity != null)
                    {
                        var newCashSlotBalance = Math.Max(cashSlotBalance - totalPrice, 0);
                        _stack.SetCount(cashEntity.Value.Owner, newCashSlotBalance, cashEntity.Value.Comp);
                        component.CashSlotBalance = newCashSlotBalance;
                        paidFully = true; // Either we paid fully with cash, or we need to withdraw the remainder
                    }
                    if (totalPrice > cashSlotBalance && !HasComp<Content.Shared._Mono.Traits.Physical.IronmanComponent>(sender))
                        paidFully = _bankSystem.TryBankWithdraw(sender, totalPrice - cashSlotBalance);
                    // If we paid completely, pay our station taxes
                    if (paidFully)
                    {
                        foreach (var (account, taxCoeff) in component.TaxAccounts)
                        {
                            if (!float.IsFinite(taxCoeff) || taxCoeff <= 0.0f)
                                continue;
                            var tax = (int)Math.Floor(totalPrice * taxCoeff);
                            _bankSystem.TrySectorDeposit(account, tax, LedgerEntryType.VendorTax);
                        }
                    }
                    // Something was ejected, update the vending component's state
                    Dirty(uid, component);
                    _adminLogger.Add(LogType.Action, LogImpact.Low,
                        $"{ToPrettyString(sender):user} bought from [vendingMachine:{ToPrettyString(uid!)}, product:{proto.Name}, cost:{totalPrice},  with ${cashSlotBalance} in the cash slot and ${bankBalance} in the bank.");
                }
                // FRONTIER END
            }
        }
        // FRONTIER END - add this function, override shared

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var disabled = EntityQueryEnumerator<EmpDisabledComponent, VendingMachineComponent>();
            while (disabled.MoveNext(out var uid, out _, out var comp))
            {
                if (comp.NextEmpEject < Timing.CurTime)
                {
                    EjectRandom(uid, true, false, comp);
                    comp.NextEmpEject += (5 * comp.EjectDelay);
                }
            }
        }

        private void OnPriceCalculation(EntityUid uid, VendingMachineRestockComponent component, ref PriceCalculationEvent args)
        {
            // FRONTIER? START
            args.Price = 0; // This area of the code make it so the cargoblacklist gets ignored, this change was to resolve it.
            return;
            // FRONTIER? END
            List<double> priceSets = new();

            // Find the most expensive inventory and use that as the highest price.
            foreach (var vendingInventory in component.CanRestock)
            {
                double total = 0;

                if (PrototypeManager.TryIndex(vendingInventory, out VendingMachineInventoryPrototype? inventoryPrototype))
                {
                    foreach (var (item, amount) in inventoryPrototype.StartingInventory)
                    {
                        if (PrototypeManager.TryIndex(item, out EntityPrototype? entity))
                            total += _pricing.GetEstimatedPrice(entity) * amount;
                    }
                }

                priceSets.Add(total);
            }

            args.Price += priceSets.Max();
        }

        private void OnTryVocalize(Entity<VendingMachineComponent> ent, ref TryVocalizeEvent args)
        {
            args.Cancelled |= ent.Comp.Broken;
        }
        // FRONTIER START: cash slot logic
        private void OnEntityInserted(Entity<VendingMachineComponent> ent, ref EntInsertedIntoContainerMessage args)
        {
            if (ent.Comp.CashSlotName != null
            && ent.Comp.CurrencyStackType != null
            && ItemSlots.TryGetSlot(ent, ent.Comp.CashSlotName, out var slot)
            && TryComp<StackComponent>(slot?.ContainerSlot?.ContainedEntity, out var stack)
            && stack.StackTypeId == ent.Comp.CurrencyStackType)
            {
                ent.Comp.CashSlotBalance = stack.Count;
            }
            else
            {
                ent.Comp.CashSlotBalance = 0;
            }
            Dirty(ent, ent.Comp);
        }

        private void OnEntityRemoved(Entity<VendingMachineComponent> ent, ref EntRemovedFromContainerMessage args)
        {
            ent.Comp.CashSlotBalance = 0;
            Dirty(ent, ent.Comp);
        }
        // FRONTIER END: cash slot logic
    }
}
