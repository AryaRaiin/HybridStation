// SPDX-FileCopyrightText: 2019 Pieter-Jan Briers
// SPDX-FileCopyrightText: 2019 Silver
// SPDX-FileCopyrightText: 2019 ZelteHonor
// SPDX-FileCopyrightText: 2020 Víctor Aguilera Puerto
// SPDX-FileCopyrightText: 2021 Acruid
// SPDX-FileCopyrightText: 2021 DrSmugleaf
// SPDX-FileCopyrightText: 2021 Vera Aguilera Puerto
// SPDX-FileCopyrightText: 2023 08A
// SPDX-FileCopyrightText: 2023 metalgearsloth
// SPDX-FileCopyrightText: 2025 ArtisticRoomba
// SPDX-FileCopyrightText: 2025 ScyronX
// SPDX-FileCopyrightText: 2025 chromiumboy
// SPDX-FileCopyrightText: 2025 starch
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared.Construction.Prototypes;
using Robust.Client.Placement;
using Robust.Shared.Map;
using Robust.Client.GameObjects; // HS
using Robust.Shared.Prototypes; // HS

namespace Content.Client.Construction
{
    public sealed class ConstructionPlacementHijack : PlacementHijack
    {
        private readonly ConstructionSystem _constructionSystem;
        private readonly ConstructionPrototype? _prototype;

        public ConstructionSystem? CurrentConstructionSystem { get { return _constructionSystem; } }
        public ConstructionPrototype? CurrentPrototype { get { return _prototype; } }

        public override bool CanRotate { get; }

        public ConstructionPlacementHijack(ConstructionSystem constructionSystem, ConstructionPrototype? prototype)
        {
            _constructionSystem = constructionSystem;
            _prototype = prototype;
            CanRotate = prototype?.CanRotate ?? true;
        }

        /// <inheritdoc />
        public override bool HijackPlacementRequest(EntityCoordinates coordinates)
        {
            if (_prototype != null)
            {
                var dir = Manager.Direction;
                _constructionSystem.SpawnGhost(_prototype, coordinates, dir);
            }
            return true;
        }

        /// <inheritdoc />
        public override bool HijackDeletion(EntityUid entity)
        {
            if (IoCManager.Resolve<IEntityManager>().HasComponent<ConstructionGhostComponent>(entity))
            {
                _constructionSystem.ClearGhost(entity.GetHashCode());
            }
            return true;
        }

        /// <inheritdoc />
        public override void StartHijack(PlacementManager manager)
        {
            base.StartHijack(manager);
            // HS START: Handles construction placement preview of object under the cursor, using the SpriteSystem.GetPrototypeTextures helper
            var entities = IoCManager.Resolve<IEntityManager>();
            var prototypes = IoCManager.Resolve<IPrototypeManager>();

            if (_prototype != null &&
                _constructionSystem.TryGetRecipePrototype(_prototype.ID, out var targetId) &&
                prototypes.TryIndex(targetId, out EntityPrototype? target))
            {
                manager.CurrentTextures = entities.System<SpriteSystem>()
                    .GetPrototypeTextures(target)
                    .ToList();
            }
            else
            {
                manager.CurrentTextures = null;
            }
            // HS END
        }
    }
}
