using System;
using System.Collections.Generic;
using System.Linq;
using FarmHub.Core;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;

namespace FarmHub.Adapters;

/// <summary>
/// The game's side of every press. Pots are read the way the game's own plant HUD finds them; a press calls the same
/// handler the plant panel, the fertilizer picker, the chore buttons or the robot panel send their events to. Each call
/// fails soft: an exception is logged once per kind of call and answered as a refusal.
/// </summary>
internal sealed class GameFarm : IFarmGame
{
    private const int PlantAction = 1610;
    private const int FertilizeAction = 1611;
    private const int SeedCategory = 10;
    private const int FertilizerCategory = 11;
    private const int RobotTiers = 3;
    private const float SecondsPerHour = 3600f;
    private const float SuitabilitySeconds = 30f;
    private const Problems KnownProblems = Problems.Pests | Problems.Weeds | Problems.Dry | Problems.TooDark | Problems.TooBright | Problems.TooCold | Problems.NeedsFeed;

    private readonly Action<string> _log;
    private readonly HashSet<string> _reportedFailures = new();

    private IconIndex? _icons;
    private (IntPtr Config, bool IsChinese) _catalogKey;
    private FarmCatalog? _catalog;

    /// <summary>A pot's light and heat change only when the player builds or moves something, so its check is kept a while.</summary>
    private readonly Dictionary<(long PotId, int KindId), (float At, HashSet<int> Crops)> _unsuitable = new();
    private bool _hasRecheckedThisRead;

    public GameFarm(Action<string> log)
    {
        _log = log;
    }

    public FarmRead Read()
    {
        try
        {
            return ReadWorld();
        }
        catch (Exception ex)
        {
            ReportOnce("read", ex);

            return new FarmRead.NotReady(Texts.ReadFailed.Pick(GameLanguage.IsChinese()));
        }
    }

    /// <summary>The plant panel's start button: the player walks over with the seeds and plants them.</summary>
    public bool Plant(long potId, int plantId, int seedCount, int seedItemId, int fertId) => Press("plant", () =>
    {
        var game = LoadedGame();

        if (game == null || FindPot(game.Value.Agents, potId) == null) return false;

        game.Value.Role.OnPlantPanelStartPlant(potId, plantId, seedCount, fertId, seedItemId);
        _log($"lined up planting pot {potId} with {seedCount} of seed {seedItemId}, fertilizer {fertId}{Queued(game.Value, PlantAction, potId)}");

        return true;
    });

    /// <summary>The fertilizer picker's confirm button on a growing pot.</summary>
    public bool Fertilize(long potId, int fertId) => Press("fertilize", () =>
    {
        var game = LoadedGame();

        if (game == null || FindPot(game.Value.Agents, potId) == null) return false;

        game.Value.Role.OnAddFertilizerConfirm(potId, fertId);
        _log($"lined up fertilizing pot {potId} with item {fertId}{Queued(game.Value, FertilizeAction, potId)}");

        return true;
    });

    /// <summary>One chore, sent the way the HUD's tend-everything patrol sends each of its chores.</summary>
    public bool Tend(long potId, TendAction action) => Press("tend", () =>
    {
        var game = LoadedGame();

        if (game == null || FindPot(game.Value.Agents, potId) == null) return false;

        var actionId = ChoreAction(action);

        PlantChoreBatchManager.Dispatch(potId, 0, actionId);
        _log($"lined up chore {actionId} at pot {potId}{Queued(game.Value, actionId, potId)}");

        return true;
    });

    public bool TendAll() => Press("tend all", () =>
    {
        var batch = LoadedGame()?.World._PlantChoreBatchManager;

        if (batch == null || !batch.IsUnlocked()) return false;

        TendHooks.IsOwnBatch = true;

        try
        {
            batch.OnBatchRequest();
        }
        finally
        {
            TendHooks.IsOwnBatch = false;
        }

        _log("sent the tend-everything patrol");

        return true;
    });

    /// <summary>Sets a robot sow tier. The robot is read back, so only a setting it now holds counts.</summary>
    public bool SetRobotTier(long robotId, int tier, int seedId, int fertId) => Press("robot tier", () =>
    {
        var game = LoadedGame();
        var robot = game == null ? null : PlanterRobots(game.Value.Agents).FirstOrDefault(found => found.Npc.InstanceId == robotId).Robot;

        if (robot == null || tier < 0 || tier >= RobotTiers) return false;

        robot.SetSowPreset(tier, seedId);
        robot.SetSowFertPreset(tier, fertId);

        var isSet = robot.GetSowPreset(tier) == seedId && robot.GetSowFertPreset(tier) == fertId;

        _log(isSet ? $"robot {robotId} tier {tier} now sows {seedId} with {fertId}" : $"robot {robotId} did not take seed {seedId} / fertilizer {fertId} on tier {tier}");

        return isSet;
    });

    private FarmRead ReadWorld()
    {
        var game = LoadedGame();

        if (game == null) return new FarmRead.NoGame();

        var (world, config, agents, role) = game.Value;
        var isChinese = GameLanguage.IsChinese();

        if (!IsHome(agents)) return new FarmRead.NotReady(Texts.Away.Pick(isChinese));

        var catalog = Catalog(config, isChinese);

        _hasRecheckedThisRead = false;

        var roleId = role.InstanceId;
        var areaNames = new Dictionary<int, string>();
        var pots = ReadPots(world, config, agents, catalog, roleId, areaNames);
        var robots = ReadRobots(config, agents);
        var owned = ReadOwned(world._ItemManager, catalog, roleId);
        var playerFloor = Floors.Level(agents.GetAgentFloor(roleId));

        return new FarmRead.Ready(new FarmWorld(catalog, pots, robots, owned, areaNames, playerFloor, SaveSlot.CurrentKey())
        {
            Stamina = ReadStamina(role),
            ChoreCost = ReadChoreCost(),
            ShowIds = ReadShowIds(pots),
        });
    }

    private float? ReadStamina(LeadingRole role)
    {
        try
        {
            return AgentTools.GetAgentComponent<AttributeComponent>(role)?.GetTotalValue_Float(AttrName.Stamina);
        }
        catch (Exception ex)
        {
            ReportOnce("stamina read", ex);

            return null;
        }
    }

    private int ReadChoreCost()
    {
        try
        {
            return GameKey.GlobalSetting_PlantActionCost;
        }
        catch (Exception ex)
        {
            ReportOnce("chore cost read", ex);

            return 0;
        }
    }

    /// <summary>The HUD's plant list names pots by their shown model's id; the page matches its rows to pots with these.</summary>
    private Dictionary<long, long> ReadShowIds(List<Pot> pots)
    {
        var ids = new Dictionary<long, long>();

        try
        {
            var models = GameSingleton.Existing<GameCore.HotUpdate.Battle.Show.BattleShowWorld>()?._AgentModelManager;

            if (models == null) return ids;

            foreach (var pot in pots)
            {
                var model = models.GetAgentModelByLogicInstanceId(pot.Id);

                if (model != null) ids[pot.Id] = model.InstanceId;
            }
        }
        catch (Exception ex)
        {
            ReportOnce("show id read", ex);
        }

        return ids;
    }

    private static (BattleLogicWorld World, ConfigManager Config, AgentManager Agents, LeadingRole Role)? LoadedGame()
    {
        var world = GameSingleton.Existing<BattleLogicWorld>();
        var config = GameSingleton.Existing<ConfigManager>();
        var agents = world?._AgentManager;
        var role = agents?.GetLeadingRole();

        if (world == null || config == null || agents == null || role == null || world._GameTimeManager?.WorldStateType != WorldStateType.PostDisaster) return null;

        return (world, config, agents, role);
    }

    /// <summary>Every pot the plant HUD would show: placed, at home, on an unlocked floor and in an unlocked area.</summary>
    private List<Pot> ReadPots(BattleLogicWorld world, ConfigManager config, AgentManager agents, FarmCatalog catalog, long roleId, Dictionary<int, string> areaNames)
    {
        var pots = new List<Pot>();
        var furnitures = agents.GetAllFurnitures();
        var claims = world._PlantChoreManager?._claims;

        for (var i = 0; furnitures != null && i < furnitures.Count; i++)
        {
            var furniture = furnitures[i];

            // The pot kind check is one read; the shown checks are several, and most furniture is not a pot.
            if (furniture == null || !catalog.Kinds.ContainsKey(furniture.AgentConfigId) || !IsShownPot(world, agents, furniture)) continue;

            try
            {
                var plant = AgentTools.GetAgentComponent<PlantComponent>(furniture);

                if (plant == null) continue;

                var robotId = claims != null && claims.TryGetValue(furniture.InstanceId, out var claimedBy) ? claimedBy : 0L;
                var pending = plant.IsPendingPlant || plant.IsPendingFertWork() || world._ActionManager?.HasActionInQueue(roleId, PlantAction, furniture.InstanceId) == true;

                pots.Add(ReadPot(config, catalog, furniture, plant, ReadArea(furniture, areaNames), robotId, pending));
            }
            catch (Exception ex)
            {
                ReportOnce("pot read", ex);
            }
        }

        return pots;
    }

    private static bool IsShownPot(BattleLogicWorld world, AgentManager agents, Furniture furniture) =>
        furniture.IsShow && agents.IsHomeMap(furniture.MapConfigId) && FloorTagConstants.IsMapPointUnlocked(world._TagManager, furniture.MapConfigId) && AreaUnlockConstants.IsFurnitureAreaUnlocked(furniture);

    private Pot ReadPot(ConfigManager config, FarmCatalog catalog, Furniture furniture, PlantComponent plant, int area, long robotId, bool isPending)
    {
        var state = StateOf(plant.State);
        var kind = catalog.Kinds[furniture.AgentConfigId];
        var isGrowing = state == PotState.Growing;
        var now = plant.GetCurrentTotalSeconds();
        var remain = isGrowing ? TimeToRipe(plant, now) : 0;
        var total = isGrowing ? plant.GetActualGrowthTotalSeconds() : 0;
        var progress = state switch
        {
            PotState.Growing => total > 0 ? 1f - (float)remain / total : 0f,
            PotState.Ripe or PotState.Withered or PotState.Poor => 1f,
            _ => 0f,
        };

        return new Pot(
            furniture.InstanceId,
            furniture.AgentConfigId,
            Floors.Level(furniture.MapConfigId),
            area,
            state,
            state == PotState.Empty ? 0 : plant.PlantConfigId,
            (Problems)(int)plant.AnomalyFlags & KnownProblems,
            progress,
            isGrowing ? remain / SecondsPerHour : null,
            state == PotState.Ripe ? HoursToRot(config, plant, now) : null,
            state == PotState.Empty ? 0 : plant.FertItemConfigId,
            isPending,
            robotId,
            !kind.NeedsPower || IsPowered(plant),
            state == PotState.Empty ? Unsuitable(config, catalog, furniture) : new HashSet<int>());
    }

    /// <summary>A stalled crop does not grow, so the stall so far is added back, as the HUD's countdown does.</summary>
    private static int TimeToRipe(PlantComponent plant, int now)
    {
        var remain = plant.GetRemainSeconds();
        var stallStart = plant.AnomalyStallStartSeconds;

        return plant.AnomalyFlags == 0 || stallStart <= 0 ? remain : remain + Math.Max(0, now - stallStart);
    }

    /// <summary>A ripe crop rots when its harvest window runs out; a crop with no window never does.</summary>
    private float? HoursToRot(ConfigManager config, PlantComponent plant, int now)
    {
        if (plant.NeverWithers() || plant.MatureStartTotalSeconds <= 0) return null;

        var crop = config.Get_Config_Plant(plant.PlantConfigId);
        var window = crop == null ? 0 : plant.GetHarvestWindowSeconds(crop);

        if (window <= 0) return null;

        return Math.Max(0, plant.MatureStartTotalSeconds + window - now) / SecondsPerHour;
    }

    private bool IsPowered(PlantComponent plant)
    {
        try
        {
            return plant.IsOwnerPowered();
        }
        catch (Exception ex)
        {
            ReportOnce("power check", ex);

            return true;
        }
    }

    /// <summary>
    /// The crops this pot's light and temperature cannot grow, by the game's own check. It asks once per crop, which is
    /// costly for a home of empty pots, so each pot's answer is reused for half a minute.
    /// </summary>
    private HashSet<int> Unsuitable(ConfigManager config, FarmCatalog catalog, Furniture furniture)
    {
        var key = (furniture.InstanceId, furniture.AgentConfigId);
        var now = UnityEngine.Time.unscaledTime;

        // One stale pot is checked again per read, so a home of empty pots never asks all of them in the same frame.
        if (_unsuitable.TryGetValue(key, out var kept) && (now - kept.At < SuitabilitySeconds || _hasRecheckedThisRead)) return kept.Crops;

        if (_unsuitable.ContainsKey(key)) _hasRecheckedThisRead = true;

        var unsuitable = CheckSuitability(config, catalog, furniture);

        _unsuitable[key] = (now, unsuitable);

        return unsuitable;
    }

    private HashSet<int> CheckSuitability(ConfigManager config, FarmCatalog catalog, Furniture furniture)
    {
        var unsuitable = new HashSet<int>();

        try
        {
            foreach (var plantId in catalog.Crops.Keys)
            {
                var crop = config.Get_Config_Plant(plantId);

                if (crop != null && !PlantComponent.IsEnvironmentSuitable(furniture, crop)) unsuitable.Add(plantId);
            }
        }
        catch (Exception ex)
        {
            ReportOnce("suitability check", ex);
        }

        return unsuitable;
    }

    private int ReadArea(Furniture furniture, Dictionary<int, string> areaNames)
    {
        try
        {
            var area = EnvAreaHelper.GetFurnitureAreaId(furniture);

            if (area > 0 && !areaNames.ContainsKey(area)) areaNames[area] = NonEmpty(EnvAreaHelper.GetAreaDisplayName(area)) ?? "";

            return area;
        }
        catch (Exception ex)
        {
            ReportOnce("area read", ex);

            return 0;
        }
    }

    private static PotState StateOf(PlantState state) => state switch
    {
        PlantState.Growing => PotState.Growing,
        PlantState.Mature => PotState.Ripe,
        PlantState.Withered => PotState.Withered,
        PlantState.Poor => PotState.Poor,
        _ => PotState.Empty,
    };

    private List<Robot> ReadRobots(ConfigManager config, AgentManager agents)
    {
        var robots = new List<Robot>();

        foreach (var (npc, component) in PlanterRobots(agents))
        {
            try
            {
                var tiers = Enumerable.Range(0, RobotTiers).Select(tier => new RobotTier(RobotSowRules.CapacityOfTier(tier), component.GetSowPreset(tier), component.GetSowFertPreset(tier))).ToList();
                var name = RobotName(config, npc.AgentConfigId) ?? Texts.RobotNumber.Say(GameLanguage.IsChinese(), robots.Count + 1);

                robots.Add(new Robot(npc.InstanceId, name, Floors.Level(agents.GetAgentFloor(npc.InstanceId)), tiers, component.Battery, component.BatteryMax, component.MasterOn));
            }
            catch (Exception ex)
            {
                ReportOnce("robot read", ex);
            }
        }

        return robots;
    }

    private IEnumerable<(GameCore.HotUpdate.Battle.Logic.DynamicNpc Npc, RobotComponent Robot)> PlanterRobots(AgentManager agents)
    {
        var found = new List<(GameCore.HotUpdate.Battle.Logic.DynamicNpc, RobotComponent)>();

        try
        {
            var npcs = agents.GetDynamicNpcs();

            for (var i = 0; npcs != null && i < npcs.Count; i++)
            {
                var npc = npcs[i];

                if (npc == null || !npc.IsRobot || npc.RobotKind != RobotKind.Plant) continue;

                var component = AgentTools.GetAgentComponent<RobotComponent>(npc);

                if (component != null) found.Add((npc, component));
            }
        }
        catch (Exception ex)
        {
            ReportOnce("robot list", ex);
        }

        return found;
    }

    private static string? RobotName(ConfigManager config, int configId)
    {
        var table = config._Config_Player_Dict;

        if (table == null || !table.ContainsKey(configId)) return null;

        var row = table[configId];

        return row == null ? null : Translate(config, row.Name) ?? NonEmpty(row.Name_Local);
    }

    /// <summary>
    /// Seeds and fertilizer the player can reach: their bag and every home storage the game counts for building costs,
    /// skipping storage the game keeps out of that pool.
    /// </summary>
    private static Dictionary<int, int> ReadOwned(ItemManager? items, FarmCatalog catalog, long roleId)
    {
        var owned = new Dictionary<int, int>();
        var available = items?.GetAllAvailableItems(roleId);

        for (var i = 0; available != null && i < available.Count; i++)
        {
            var item = available[i];

            if (item == null || item.ItemCount <= 0) continue;
            if (!catalog.Seeds.ContainsKey(item.ItemConfigId) && !catalog.Fertilizers.ContainsKey(item.ItemConfigId)) continue;
            if (item.OwnerId != roleId && ItemManager.IsExcludedFromCostPool(item.OwnerId)) continue;

            owned[item.ItemConfigId] = (owned.TryGetValue(item.ItemConfigId, out var count) ? count : 0) + item.ItemCount;
        }

        return owned;
    }

    /// <summary>The tables only change with the language, so the catalog is built once per game and language.</summary>
    private FarmCatalog Catalog(ConfigManager config, bool isChinese)
    {
        var key = (config.Pointer, isChinese);

        if (_catalog != null && _catalogKey == key) return _catalog;

        _catalog = new FarmCatalog(ReadKinds(config), ReadCrops(config), ReadSeeds(config), ReadFertilizers(config));
        _unsuitable.Clear();
        _catalogKey = key;
        _log($"read {_catalog.Kinds.Count} pot kinds, {_catalog.Crops.Count} crops, {_catalog.Seeds.Count} seeds, {_catalog.Fertilizers.Count} fertilizers");

        return _catalog;
    }

    private Dictionary<int, PotKind> ReadKinds(ConfigManager config)
    {
        var kinds = new Dictionary<int, PotKind>();
        var rows = config._Config_Furniture_Dict;

        if (rows == null) return kinds;

        foreach (var row in rows.Values)
        {
            if (row == null || row.PlantFurnitureID <= 0) continue;

            var plant = config.Get_Config_FurniturePlant(row.PlantFurnitureID);

            if (plant == null) continue;

            var name = Translate(config, row.Name) ?? $"Pot {row.ID}";

            kinds[row.ID] = new PotKind(row.ID, name, Icons().Resolve(NonEmpty(row.ICON)), plant.Capacity, plant.AddLight, plant.AddHeat, plant.NeedPower, plant.GrowthFaster);
        }

        return kinds;
    }

    private Dictionary<int, Crop> ReadCrops(ConfigManager config)
    {
        var crops = new Dictionary<int, Crop>();
        var rows = config._Config_Plant_Dict;

        if (rows == null) return crops;

        foreach (var row in rows.Values)
        {
            if (row == null) continue;

            var name = Translate(config, row.Name) ?? NonEmpty(row.Name_Local) ?? $"Crop {row.ID}";
            var harvest = row.Gain != null && row.Gain.Count > 0 ? config.Get_Config_Item(row.Gain[0]) : null;

            crops[row.ID] = new Crop(row.ID, name, ItemIcon(harvest), row.Size, row.GrowthTime / SecondsPerHour, row.HarvestTime / SecondsPerHour, GreenhouseResearchPlant.IsResearchPlant(row.ID));
        }

        return crops;
    }

    private Dictionary<int, SeedItem> ReadSeeds(ConfigManager config)
    {
        var seeds = new Dictionary<int, SeedItem>();

        foreach (var item in Items(config, SeedCategory))
        {
            if (item.Plant > 0) seeds[item.ID] = new SeedItem(item.ID, ItemName(config, item), ItemIcon(item), item.Plant);
        }

        return seeds;
    }

    private Dictionary<int, Fertilizer> ReadFertilizers(ConfigManager config) =>
        Items(config, FertilizerCategory).ToDictionary(item => item.ID, item => new Fertilizer(item.ID, ItemName(config, item), ItemIcon(item), item.Plant_Fast));

    private static List<Config_Item> Items(ConfigManager config, int category)
    {
        var items = new List<Config_Item>();
        var rows = config._Config_Item_Dict;

        if (rows == null) return items;

        foreach (var row in rows.Values)
        {
            if (row != null && row.Category == category) items.Add(row);
        }

        return items;
    }

    private bool IsHome(AgentManager agents)
    {
        try
        {
            return agents.IsHomeMap(ExploreSearchStateManager.GetCurrentMapId());
        }
        catch (Exception ex)
        {
            ReportOnce("home check", ex);

            return false;
        }
    }

    private static Furniture? FindPot(AgentManager agents, long potId)
    {
        var furnitures = agents.GetAllFurnitures();

        for (var i = 0; furnitures != null && i < furnitures.Count; i++)
        {
            if (furnitures[i]?.InstanceId == potId) return furnitures[i];
        }

        return null;
    }

    private static string Queued((BattleLogicWorld World, ConfigManager Config, AgentManager Agents, LeadingRole Role) game, int actionId, long potId) =>
        game.World._ActionManager?.HasActionInQueue(game.Role.InstanceId, actionId, potId) == true ? " (queued)" : " (not seen in the queue yet)";

    private static int ChoreAction(TendAction action) => action switch
    {
        TendAction.Pests => 1613,
        TendAction.Weeds => 1614,
        TendAction.Water => 1615,
        TendAction.Clear => 1616,
        TendAction.Harvest => 1617,
        TendAction.Uproot => 1618,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "unknown chore"),
    };

    private bool Press(string operation, Func<bool> press)
    {
        try
        {
            return press();
        }
        catch (Exception ex)
        {
            ReportOnce(operation, ex);

            return false;
        }
    }

    private static string ItemName(ConfigManager config, Config_Item item) =>
        Translate(config, item.ItemName) ?? NonEmpty(item.ItemName_Local) ?? NonEmpty(item.ItemName) ?? $"Item {item.ID}";

    private string? ItemIcon(Config_Item? item) => item == null ? null : Icons().Resolve(NonEmpty(item.WebIcon) ?? NonEmpty(item.Icon));

    private IconIndex Icons() => _icons ??= WebIcons.Load(_log);

    private static string? Translate(ConfigManager config, string? key)
    {
        if (string.IsNullOrEmpty(key)) return null;

        var text = config.GetLocalTxt(key);

        return string.IsNullOrEmpty(text) || text == key ? null : text;
    }

    private static string? NonEmpty(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;

    private void ReportOnce(string operation, Exception ex)
    {
        if (_reportedFailures.Add(operation)) _log($"{operation} failed (each kind reported once): {ex}");
    }
}
