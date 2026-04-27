// SPDX-FileCopyrightText: 2023 Cheackraze
// SPDX-FileCopyrightText: 2023 Visne
// SPDX-FileCopyrightText: 2023 deltanedas
// SPDX-FileCopyrightText: 2023 deltanedas <@deltanedas:kde.org>
// SPDX-FileCopyrightText: 2024 Checkraze
// SPDX-FileCopyrightText: 2024 ElectroJr
// SPDX-FileCopyrightText: 2024 Kara
// SPDX-FileCopyrightText: 2024 Leon Friedrich
// SPDX-FileCopyrightText: 2024 MilenVolf
// SPDX-FileCopyrightText: 2024 Nemanja
// SPDX-FileCopyrightText: 2024 SlamBamActionman
// SPDX-FileCopyrightText: 2024 Vasilis
// SPDX-FileCopyrightText: 2024 Whatstone
// SPDX-FileCopyrightText: 2024 checkraze
// SPDX-FileCopyrightText: 2024 metalgearsloth
// SPDX-FileCopyrightText: 2025 Dvir
// SPDX-FileCopyrightText: 2025 GreaseMonk
// SPDX-FileCopyrightText: 2025 Redrover1760
// SPDX-FileCopyrightText: 2025 starch
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Atmos;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Robust.Shared.CPUJob.JobQueues;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Parallax;
using Content.Server.Procedural;
using Content.Server.Salvage.Expeditions;
using Content.Shared.Atmos;
using Content.Shared.Construction.EntitySystems;
using Content.Shared.Dataset;
using Content.Shared.Gravity;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Physics;
using Content.Shared.Procedural;
using Content.Shared.Procedural.Loot;
using Content.Shared.Random;
using Content.Shared.Salvage;
using Content.Shared.Salvage.Expeditions;
using Content.Shared.Salvage.Expeditions.Modifiers;
using Content.Shared.Shuttles.Components;
using Content.Shared.Storage;
using Robust.Shared.Collections;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Content.Server.Shuttles.Components;
using Content.Server._NF.Salvage; // Frontier: job complete event
using Content.Server.Weather; // Frontier
using Content.Shared.Weather; // Frontier

namespace Content.Server.Salvage;

public sealed class SpawnSalvageMissionJob : Job<bool>
{
    private readonly IEntityManager _entManager;
    private readonly IGameTiming _timing;
    private readonly IPrototypeManager _prototypeManager;
    private readonly AnchorableSystem _anchorable;
    private readonly BiomeSystem _biome;
    private readonly DungeonSystem _dungeon;
    private readonly MetaDataSystem _metaData;
    private readonly SharedMapSystem _map;

    public readonly EntityUid Station;
    public readonly EntityUid? CoordinatesDisk;
    private readonly SalvageMissionParams _missionParams;

    private readonly ISawmill _sawmill;

    // Frontier START: Used for saving state between async job
#pragma warning disable IDE1006 // suppressing _ prefix complaints to reduce merge conflict area
    private EntityUid mapUid = EntityUid.Invalid;
#pragma warning restore IDE1006
    private readonly WeatherSystem _weather;

    // Frontier END

    // Mono START
    private const float MassConstant = 50f; // Arbitrary, at this value massMultiplier = 0.65
    private const float MassMultiplierMin = 0.5f;
    private const float MassMultiplierMax = 5f;
    private const float StartupTime = 5.5f;
    private const float HyperSpaceTime = 50f;
    // Mono END


    public SpawnSalvageMissionJob(
        double maxTime,
        IEntityManager entManager,
        IGameTiming timing,
        ILogManager logManager,
        IPrototypeManager protoManager,
        AnchorableSystem anchorable,
        BiomeSystem biome,
        DungeonSystem dungeon,
        MetaDataSystem metaData,
        SharedMapSystem map,
        EntityUid station,
        EntityUid? coordinatesDisk,
        SalvageMissionParams missionParams,
        CancellationToken cancellation = default) : base(maxTime, cancellation)
    {
        _entManager = entManager;
        _timing = timing;
        _prototypeManager = protoManager;
        _anchorable = anchorable;
        _biome = biome;
        _dungeon = dungeon;
        _metaData = metaData;
        _map = map;
        Station = station;
        CoordinatesDisk = coordinatesDisk;
        _missionParams = missionParams;
        _sawmill = logManager.GetSawmill("salvage_job");
#if !DEBUG
        _sawmill.Level = LogLevel.Info;
#endif
    }

    // Frontier START: gracefully handle expedition failures
    protected override async Task<bool> Process()
    {
        bool success = true;
        string? errorStackTrace = null;
        try
        {
            await InternalProcess().ContinueWith((t) => { success = false; errorStackTrace = t.Exception?.InnerException?.StackTrace; }, TaskContinuationOptions.OnlyOnFaulted);
        }
        finally
        {
            ExpeditionSpawnCompleteEvent ev = new(Station, success, _missionParams.Index);
            _entManager.EventBus.RaiseLocalEvent(Station, ev);
            if (errorStackTrace != null)
                Logger.ErrorS("salvage", $"Expedition generation failed with exception: {errorStackTrace}!");
            if (!success)
            {
                // Invalidate station, expedition cancellation will be handled by task handler
                if (_entManager.TryGetComponent<SalvageExpeditionComponent>(mapUid, out var salvage))
                    salvage.Station = EntityUid.Invalid;

                _entManager.QueueDeleteEntity(mapUid);
            }
        }
        return success;
    }
    // Frontier END: gracefully handle expedition failures

    //protected override async Task<bool> Process()
    private async Task<bool> InternalProcess() // Frontier: make process an internal function (for a try block indenting an entire), add "out EntityUid mapUid" param
    {
        _sawmill.Debug("salvage", $"Spawning salvage mission with seed {_missionParams.Seed}");
        //var mapUid = _map.CreateMap(out var mapId, runMapInit: false);
        mapUid = _map.CreateMap(out var mapId, runMapInit: false); // Frontier: remove "var"
        MetaDataComponent? metadata = null;
        var grid = _entManager.EnsureComponent<MapGridComponent>(mapUid);
        var random = new Random(_missionParams.Seed);
        var destComp = _entManager.AddComponent<FTLDestinationComponent>(mapUid);
        destComp.BeaconsOnly = true;
        destComp.RequireCoordinateDisk = true;
        destComp.Enabled = true;
        _metaData.SetEntityName(
            mapUid,
            _entManager.System<SharedSalvageSystem>().GetFTLName(_prototypeManager.Index(SalvageSystem.PlanetNames), _missionParams.Seed));
        _entManager.AddComponent<FTLBeaconComponent>(mapUid);

        // Saving the mission mapUid to a CD is made optional, in case one is somehow made in a process without a CD entity
        if (CoordinatesDisk.HasValue)
        {
            var cd = _entManager.EnsureComponent<ShuttleDestinationCoordinatesComponent>(CoordinatesDisk.Value);
            cd.Destination = mapUid;
            _entManager.Dirty(CoordinatesDisk.Value, cd);
        }

        // Setup mission configs
        // As we go through the config the rating will deplete so we'll go for most important to least important.
        var difficultyId = "Moderate";
        var difficultyProto = _prototypeManager.Index<SalvageDifficultyPrototype>(difficultyId);

        var mission = _entManager.System<SharedSalvageSystem>()
            //.GetMission(difficultyProto, _missionParams.Seed);
            .GetMission(_missionParams.MissionType, _missionParams.Difficulty, _missionParams.Seed); // Frontier

        var missionWeather = _prototypeManager.Index<SalvageWeatherMod>(mission.Weather); // Frontier?
        var missionBiome = _prototypeManager.Index<SalvageBiomeModPrototype>(mission.Biome);
        BiomeComponent? biome = null; // Frontier?

        if (missionBiome.BiomePrototype != null)
        {
            //var biome = _entManager.AddComponent<BiomeComponent>(mapUid);
            biome = _entManager.AddComponent<BiomeComponent>(mapUid); // Frontier?
            var biomeSystem = _entManager.System<BiomeSystem>();
            biomeSystem.SetTemplate(mapUid, biome, _prototypeManager.Index<BiomeTemplatePrototype>(missionBiome.BiomePrototype));
            biomeSystem.SetSeed(mapUid, biome, mission.Seed);
            _entManager.Dirty(mapUid, biome);

            // Gravity
            var gravity = _entManager.EnsureComponent<GravityComponent>(mapUid);
            gravity.Enabled = true;
            _entManager.Dirty(mapUid, gravity, metadata);

            // Atmos
            var air = _prototypeManager.Index<SalvageAirMod>(mission.Air);
            // copy into a new array since the yml deserialization discards the fixed length
            var moles = new float[Atmospherics.AdjustedNumberOfGases];
            air.Gases.CopyTo(moles, 0);
            var atmos = _entManager.EnsureComponent<MapAtmosphereComponent>(mapUid);
            _entManager.System<AtmosphereSystem>().SetMapSpace(mapUid, air.Space, atmos);
            _entManager.System<AtmosphereSystem>().SetMapGasMixture(mapUid, new GasMixture(moles, mission.Temperature), atmos);

            // Frontier? START
            if (!air.Space)
            {
                var weather = _entManager.EnsureComponent<WeatherComponent>(mapUid);
                _entManager.System<WeatherSystem>().SetWeather(mapId, _prototypeManager.Index<WeatherPrototype>(missionWeather.WeatherPrototype), null);
                _entManager.Dirty(mapUid, weather);
            }
            // Frontier? END

            if (mission.Color != null)
            {
                var lighting = _entManager.EnsureComponent<MapLightComponent>(mapUid);
                lighting.AmbientLightColor = mission.Color.Value;
                _entManager.Dirty(mapUid, lighting);
            }
        }

        _map.InitializeMap(mapId);
        _map.SetPaused(mapUid, true);

        // Setup expedition
        var expedition = _entManager.AddComponent<SalvageExpeditionComponent>(mapUid);
        expedition.Station = Station;
        expedition.EndTime = _timing.CurTime + mission.Duration;
        expedition.MissionParams = _missionParams;
        expedition.Difficulty = _missionParams.Difficulty; // Frontier
        expedition.Rewards = mission.Rewards; // Frontier

        //var landingPadRadius = 24;
        var landingPadRadius = 4; // Frontier: 24<4 - using this as a margin (4-16), not a radius
        var minDungeonOffset = landingPadRadius + 4;

        // We'll use the dungeon rotation as the spawn angle
        var dungeonRotation = _dungeon.GetDungeonRotation(_missionParams.Seed);
        Dungeon dungeon = default!; // Frontier: explicitly type as Dungeon

        Vector2 dungeonOffset = new Vector2(); // Frontier: needed for dungeon offset
        if (config != SalvageMissionType.Mining) // Frontier: why?
        {
            var maxDungeonOffset = minDungeonOffset + 12;
            var dungeonOffsetDistance = minDungeonOffset + (maxDungeonOffset - minDungeonOffset) * random.NextFloat();
            var dungeonOffset = new Vector2(0f, dungeonOffsetDistance);
            dungeonOffset = dungeonRotation.RotateVec(dungeonOffset);
            var dungeonMod = _prototypeManager.Index<SalvageDungeonModPrototype>(mission.Dungeon);
            var dungeonConfig = _prototypeManager.Index(dungeonMod.Proto);
            var dungeons = await WaitAsyncTask(_dungeon.GenerateDungeonAsync(dungeonConfig, mapUid, grid, (Vector2i)dungeonOffset,
                _missionParams.Seed,
                dungeonConfig.ID)); // Frontier: add dungeonConfig.ID ... but why? dungeonConfig is already passed in

            var dungeon = dungeons.First();

            // Aborty
            if (dungeon.Rooms.Count == 0)
            {
                return false;
            }

            expedition.DungeonLocation = dungeonOffset;
        }

        // FRONTIER START
        // Frontier: get map bounding box
        Box2 dungeonBox = new Box2(dungeonOffset, dungeonOffset);
        foreach (var tile in dungeon.AllTiles)
        {
            dungeonBox = dungeonBox.ExtendToContain(tile);
        }

        var stationData = _entManager.GetComponent<StationDataComponent>(Station);

        // Frontier: get ship bounding box relative to largest grid coords
        var shuttleUid = _stationSystem.GetLargestGrid(stationData);
        Box2 shuttleBox = new Box2();

        if (shuttleUid is { Valid: true } vesselUid &&
            _entManager.TryGetComponent<MapGridComponent>(vesselUid, out var gridComp))
        {
            shuttleBox = gridComp.LocalAABB;
        }

        // Frontier: offset ship spawn point from bounding boxes
        Vector2 dungeonProjection = new Vector2(dungeonBox.Width * (float) -Math.Sin(dungeonRotation) / 2, dungeonBox.Height * (float) Math.Cos(dungeonRotation) / 2); // Project boxes to get relevant offset for dungeon rotation.
        Vector2 shuttleProjection = new Vector2(shuttleBox.Width * (float) -Math.Sin(dungeonRotation) / 2, shuttleBox.Height * (float) Math.Cos(dungeonRotation) / 2); // Note: sine is negative because of CCW rotation (starting north, then west)
        Vector2 coords = dungeonBox.Center - dungeonProjection - dungeonOffset - shuttleProjection - shuttleBox.Center; // Coordinates to spawn the ship at to center it with the dungeon's bounding boxes
        coords = coords.Rounded(); // Ensure grid is aligned to map coords

        // Frontier: delay ship FTL
        if (shuttleUid is { Valid: true })
        {
            var shuttle = _entManager.GetComponent<ShuttleComponent>(shuttleUid.Value);
            MassAdjustFTLExpedStartup(shuttleUid, out var massStartupTime);
            _shuttle.FTLToCoordinates(shuttleUid.Value, shuttle, new EntityCoordinates(mapUid, coords), 0f, massStartupTime, HyperSpaceTime);
        }
        // FRONTIER END

        List<Vector2i> reservedTiles = new();

        /* Frontier START: no need for intersecting tiles, we offset the map
        foreach (var tile in _map.GetTilesIntersecting(mapUid, grid, new Circle(Vector2.Zero, landingPadRadius), false))
        {
            if (!_biome.TryGetBiomeTile(mapUid, grid, tile.GridIndices, out _))
                continue;

            reservedTiles.Add(tile.GridIndices);
        }
        */// Frontier END

        var budgetEntries = new List<IBudgetEntry>();

        // Frontier START: Mission setup
        switch (config)
        {
            case SalvageMissionType.Mining:
                await SetupMining(mission, mapUid);
                break;
            case SalvageMissionType.Destruction:
                await SetupStructure(mission, dungeon, mapUid, grid, random);
                break;
            case SalvageMissionType.Elimination:
                await SetupElimination(mission, dungeon, mapUid, grid, random);
                break;
            default:
                throw new NotImplementedException();
        }
        // Frontier END: Mission setup

        /*
         * GUARANTEED LOOT
         */

        // We'll always add this loot if possible
        // mainly used for ore layers.
        foreach (var lootProto in _prototypeManager.EnumeratePrototypes<SalvageLootPrototype>())
        {
            if (!lootProto.Guaranteed)
                continue;

            try
            {
                await SpawnDungeonLoot(lootProto, mapUid);
            }
            catch (Exception e)
            {
                _sawmill.Error($"Failed to spawn guaranteed loot {lootProto.ID}: {e}");
            }
        }

        // Handle boss loot (when relevant).

        // Handle mob loot.

        // Handle remaining loot

        /*
         * MOB SPAWNS
         */

        /* FRONTIER START: disable upstream spawning method in favor of mission type setup
        var mobBudget = difficultyProto.MobBudget;
        var faction = _prototypeManager.Index<SalvageFactionPrototype>(mission.Faction);
        var randomSystem = _entManager.System<RandomSystem>();

        foreach (var entry in faction.MobGroups)
        {
            budgetEntries.Add(entry);
        }

        var probSum = budgetEntries.Sum(x => x.Prob);

        while (mobBudget > 0f)
        {
            var entry = randomSystem.GetBudgetEntry(ref mobBudget, ref probSum, budgetEntries, random);
            if (entry == null)
                break;

            try
            {
                await SpawnRandomEntry((mapUid, grid), entry, dungeon, random);
            }
            catch (Exception e)
            {
                _sawmill.Error($"Failed to spawn mobs for {entry.Proto}: {e}");
            }
        }

        var allLoot = _prototypeManager.Index(SharedSalvageSystem.ExpeditionsLootProto);
        var lootBudget = difficultyProto.LootBudget;

        foreach (var rule in allLoot.LootRules)
        {
            switch (rule)
            {
                case RandomSpawnsLoot randomLoot:
                    budgetEntries.Clear();

                    foreach (var entry in randomLoot.Entries)
                    {
                        budgetEntries.Add(entry);
                    }

                    probSum = budgetEntries.Sum(x => x.Prob);

                    while (lootBudget > 0f)
                    {
                        var entry = randomSystem.GetBudgetEntry(ref lootBudget, ref probSum, budgetEntries, random);
                        if (entry == null)
                            break;

                        _sawmill.Debug($"Spawning dungeon loot {entry.Proto}");
                        await SpawnRandomEntry((mapUid, grid), entry, dungeon, random);
                    }
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
        *///FRONTIER END: disable upstream spawning method in favor of mission type setup

        return true;
    }
    
    //Frontier START
    private void MassAdjustFTLExpedStartup(EntityUid? shuttleUid, out float massStartupTime)
    {
        var massMultiplier = 1f;
        if (_entManager.TryGetComponent(shuttleUid, out PhysicsComponent? shuttlePhysics))
        {
            var mass = shuttlePhysics.Mass;
            massMultiplier = float.Log(float.Sqrt(mass / MassConstant + float.E));
            massMultiplier = float.Clamp(massMultiplier, MassMultiplierMin, MassMultiplierMax);
        }
        massStartupTime = StartupTime * massMultiplier;
    }
    // Frontier END

    private async Task SpawnRandomEntry(Entity<MapGridComponent> grid, IBudgetEntry entry, Dungeon dungeon, Random random)
    {
        await SuspendIfOutOfTime();

        var availableRooms = new ValueList<DungeonRoom>(dungeon.Rooms);
        var availableTiles = new List<Vector2i>();

        while (availableRooms.Count > 0)
        {
            availableTiles.Clear();
            var roomIndex = random.Next(availableRooms.Count);
            var room = availableRooms.RemoveSwap(roomIndex);
            availableTiles.AddRange(room.Tiles);

            while (availableTiles.Count > 0)
            {
                var tile = availableTiles.RemoveSwap(random.Next(availableTiles.Count));

                if (!_anchorable.TileFree(grid, tile, (int)CollisionGroup.MachineLayer,
                        (int)CollisionGroup.MachineLayer))
                {
                    continue;
                }

                var uid = _entManager.SpawnAtPosition(entry.Proto, _map.GridTileToLocal(grid, grid, tile));
                _entManager.RemoveComponent<GhostRoleComponent>(uid);
                _entManager.RemoveComponent<GhostTakeoverAvailableComponent>(uid);
                return;
            }
        }

        // oh noooooooooooo
    }

    private async Task SpawnDungeonLoot(SalvageLootPrototype loot, EntityUid gridUid)
    {
        for (var i = 0; i < loot.LootRules.Count; i++)
        {
            var rule = loot.LootRules[i];

            switch (rule)
            {
                case BiomeMarkerLoot biomeLoot:
                    {
                        if (_entManager.TryGetComponent<BiomeComponent>(gridUid, out var biome))
                        {
                            _biome.AddMarkerLayer(gridUid, biome, biomeLoot.Prototype);
                        }
                    }
                    break;
                case BiomeTemplateLoot biomeLoot:
                    {
                        if (_entManager.TryGetComponent<BiomeComponent>(gridUid, out var biome))
                        {
                            _biome.AddTemplate(gridUid, biome, "Loot", _prototypeManager.Index<BiomeTemplatePrototype>(biomeLoot.Prototype), i);
                        }
                    }
                    break;
            }
        }
    }

    #region Mission Specific
    // Frontier START
    private async Task SetupMining(
        SalvageMission mission,
        EntityUid gridUid)
    {
        var faction = _prototypeManager.Index<SalvageFactionPrototype>(mission.Faction);

        if (_entManager.TryGetComponent<BiomeComponent>(gridUid, out var biome))
        {
            // TODO: Better
            for (var i = 0; i < _salvage.GetDifficulty(mission.Difficulty); i++)
            {
                _biome.AddMarkerLayer(gridUid, biome, faction.Configs["Mining"]);
            }
        }
    }
    // Frontier END

    // Frontier START
    private async Task SetupStructure(
        SalvageMission mission,
        Dungeon dungeon,
        EntityUid gridUid,
        MapGridComponent grid,
        Random random)
    {
        var structureComp = _entManager.EnsureComponent<SalvageStructureExpeditionComponent>(gridUid);
        var availableRooms = dungeon.Rooms.ToList();
        var faction = _prototypeManager.Index<SalvageFactionPrototype>(mission.Faction);
        await SpawnMobsRandomRooms(mission, dungeon, faction, grid, random);

        var structureCount = _salvage.GetStructureCount(mission.Difficulty);
        var shaggy = faction.Configs["DefenseStructure"];
        var validSpawns = new List<Vector2i>();

        // Spawn the objectives
        for (var i = 0; i < structureCount; i++)
        {
            var structureRoom = availableRooms[random.Next(availableRooms.Count)];
            validSpawns.Clear();
            validSpawns.AddRange(structureRoom.Tiles);
            random.Shuffle(validSpawns);

            while (validSpawns.Count > 0)
            {
                var spawnTile = validSpawns[^1];
                validSpawns.RemoveAt(validSpawns.Count - 1);

                if (!_anchorable.TileFree(grid, spawnTile, (int) CollisionGroup.MachineLayer,
                        (int) CollisionGroup.MachineMask)) // Frontier: MachineLayer<MachineMask
                {
                    continue;
                }

                var spawnPosition = _map.GridTileToLocal(mapUid, grid, spawnTile);
                var uid = _entManager.SpawnEntity(shaggy, spawnPosition);
                _entManager.AddComponent<SalvageStructureComponent>(uid);
                structureComp.Structures.Add(uid);
                break;
            }
        }
    }
    // Frontier END

    // Frontier START
    private async Task SetupElimination(
        SalvageMission mission,
        Dungeon dungeon,
        EntityUid gridUid,
        MapGridComponent grid,
        Random random)
    {
        // spawn megafauna in a random place
        var roomIndex = random.Next(dungeon.Rooms.Count);
        var room = dungeon.Rooms[roomIndex];
        var tile = room.Tiles.ElementAt(random.Next(room.Tiles.Count));
        var position = _map.GridTileToLocal(mapUid, grid, tile);

        var faction = _prototypeManager.Index<SalvageFactionPrototype>(mission.Faction);
        var prototype = faction.Configs["Megafauna"];
        var uid = _entManager.SpawnEntity(prototype, position);
        // not removing ghost role since its 1 megafauna, expect that you won't be able to cheese it.
        var eliminationComp = _entManager.EnsureComponent<SalvageEliminationExpeditionComponent>(gridUid);
        eliminationComp.Megafauna.Add(uid);

        // spawn less mobs than usual since there's megafauna to deal with too
        await SpawnMobsRandomRooms(mission, dungeon, faction, grid, random, 0.5f);
    }
    // Frontier END

    // Frontier START
    private async Task SpawnMobsRandomRooms(SalvageMission mission, Dungeon dungeon, SalvageFactionPrototype faction, MapGridComponent grid, Random random, float scale = 1f)
    {
        // scale affects how many groups are spawned, not the size of the groups themselves
        var groupSpawns = _salvage.GetSpawnCount(mission.Difficulty) * scale;
        var groupSum = faction.MobGroups.Sum(o => o.Prob);
        var validSpawns = new List<Vector2i>();

        for (var i = 0; i < groupSpawns; i++)
        {
            var roll = random.NextFloat() * groupSum;
            var value = 0f;

            foreach (var group in faction.MobGroups)
            {
                value += group.Prob;

                if (value < roll)
                    continue;

                var mobGroupIndex = random.Next(faction.MobGroups.Count);
                var mobGroup = faction.MobGroups[mobGroupIndex];

                var spawnRoomIndex = random.Next(dungeon.Rooms.Count);
                var spawnRoom = dungeon.Rooms[spawnRoomIndex];
                validSpawns.Clear();
                validSpawns.AddRange(spawnRoom.Tiles);
                random.Shuffle(validSpawns);

                foreach (var entry in EntitySpawnCollection.GetSpawns(mobGroup.Entries, random))
                {
                    while (validSpawns.Count > 0)
                    {
                        var spawnTile = validSpawns[^1];
                        validSpawns.RemoveAt(validSpawns.Count - 1);

                        if (!_anchorable.TileFree(grid, spawnTile, (int)CollisionGroup.MachineLayer,
                                (int)CollisionGroup.MachineLayer))
                        {
                            continue;
                        }

                        var spawnPosition = _map.GridTileToLocal(mapUid, grid, spawnTile); // Frontier: grid<_map

                        var uid = _entManager.CreateEntityUninitialized(entry, spawnPosition);
                        _entManager.RemoveComponent<GhostTakeoverAvailableComponent>(uid);
                        _entManager.RemoveComponent<GhostRoleComponent>(uid);
                        _entManager.InitializeAndStartEntity(uid);

                        break;
                    }
                }

                await SuspendIfOutOfTime();
                break;
            }
        }
    }
    // Frontier END
    #endregion
}
