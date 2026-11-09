using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using ExiledToolkit.Game;
using ExiledToolkit.Kit;
using ExiledToolkit.Web;
using FarmHub.Core;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;

namespace FarmHub.Adapters;

/// <summary>
/// The game's side of every press. Pots are read the way the game's plant HUD finds them; a press calls the same
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

    // The periodic read and the catalog build each run a slice per frame, each slice stopping once it has used this
    // much of the frame. A step is one pot, one catalog entry, or this many of the cheap entries in between.
    private const double SliceMilliseconds = 0.5;
    private const int RowsPerStep = 64;
    private const int CropsPerStep = 4;
    private const int ShowIdsPerStep = 16;
    private const string NoIconsNote = "no web icons, crops and items will show letters instead: ";

    private static readonly long SliceTicks = (long)(SliceMilliseconds * Stopwatch.Frequency / 1000.0);
    private static readonly Func<bool> NeverSpent = () => false;
    private static readonly FarmRead NoGameRead = new FarmRead.NoGame();

    /// <summary>A pot that is not empty is never checked for crops it cannot grow; they all share this set, which is never changed.</summary>
    private static readonly HashSet<int> NoCrops = new();

    private readonly Action<string> _log;
    private readonly HashSet<string> _reportedFailures = new();
    private readonly Func<bool> _isSliceSpent;
    private long _sliceEndsAt;

    private Task<(IconIndex Icons, string Note)>? _iconScan;
    private IconIndex? _icons;

    private (IntPtr Config, bool IsChinese) _catalogKey;
    private FarmCatalog? _catalog;
    private List<Config_Plant> _cropRows = new();
    private Dictionary<int, Config_Plant> _cropRowsById = new();

    private SlicedJob? _build;
    private (IntPtr Config, bool IsChinese) _buildKey;
    private FarmCatalog? _built;
    private List<Config_Plant> _builtCropRows = new();
    private bool _isSyncRead;

    private ReadRun? _read;

    // Names come from the tables and the language, so they hold until the catalog is built again or another game loads.
    private readonly Dictionary<int, string> _areaNames = new();
    private readonly Dictionary<int, string?> _robotNames = new();

    /// <summary>A pot's light and heat change only when the player builds or moves something, so its check is kept a while.</summary>
    private readonly Dictionary<(long PotId, int KindId), (float At, HashSet<int> Crops)> _unsuitable = new();
    private (IntPtr World, string? SaveKey) _memoKey;

    public GameFarm(Action<string> log)
    {
        _log = log;
        _isSliceSpent = () => Stopwatch.GetTimestamp() >= _sliceEndsAt;
    }

    /// <summary>
    /// The farm read a slice per frame, so no frame carries all of it: null while it is under way, the read once it is
    /// done. The next call starts a new read. A read whose game went away starts over.
    /// </summary>
    public FarmRead? AdvanceRead()
    {
        try
        {
            AreIconsIn();

            var game = LoadedGame();

            if (game == null)
            {
                _read = null;

                return NoGameRead;
            }

            if (_read == null || _read.World != game.Value.World.Pointer) _read = StartRead(game.Value, isSync: false);

            var run = _read;

            _sliceEndsAt = Stopwatch.GetTimestamp() + SliceTicks;

            if (!run.Job.Advance(_isSliceSpent)) return null;

            _read = null;

            return run.Result ?? throw new InvalidOperationException("the read ended without a result");
        }
        catch (Exception ex)
        {
            _read = null;
            ReportOnce("read", ex);

            return new FarmRead.NotReady(Texts.ReadFailed.Pick(GameLanguage.IsChinese()));
        }
    }

    /// <summary>The next AdvanceRead starts a fresh read, so a change just made is in what it finds.</summary>
    public void RestartRead() => _read = null;

    /// <summary>The whole farm, read now. Presses use it: each reads the farm right before it acts.</summary>
    public FarmRead Read()
    {
        try
        {
            AreIconsIn();

            var game = LoadedGame();

            if (game == null) return NoGameRead;

            var run = StartRead(game.Value, isSync: true);

            _isSyncRead = true;

            try
            {
                if (!run.Job.Advance(NeverSpent)) throw new InvalidOperationException("the read stopped to wait");
            }
            finally
            {
                _isSyncRead = false;
            }

            return run.Result ?? throw new InvalidOperationException("the read ended without a result");
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

    /// <summary>
    /// Whether one pot is still empty and free right now, read the way the farm read reads it: for a press made on a read
    /// a few frames old, so a pot the player or a robot took since is left alone.
    /// </summary>
    public bool IsStillPlantable(long potId)
    {
        try
        {
            var game = LoadedGame();
            var furniture = game == null ? null : FindPot(game.Value.Agents, potId);
            var plant = furniture == null ? null : PlantOf(furniture);

            if (game == null || plant == null || StateOf(plant._State_k__BackingField) != PotState.Empty) return false;

            var world = game.Value.World;
            var claims = world._PlantChoreManager?._claims;

            if (claims != null && claims.TryGetValue(potId, out var claimedBy) && claimedBy != 0L) return false;

            return !(plant._IsPendingPlant_k__BackingField || plant.IsPendingFertWork() || world._ActionManager?.HasActionInQueue(game.Value.Role._InstanceId_k__BackingField, PlantAction, potId) == true);
        }
        catch (Exception ex)
        {
            ReportOnce("pot recheck", ex);

            return false;
        }
    }

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
        RobotComponent? robot = null;

        if (game != null)
        {
            foreach (var found in PlanterRobots(game.Value.Agents))
            {
                if (found.Npc._InstanceId_k__BackingField != robotId) continue;

                robot = found.Robot;
                break;
            }
        }

        if (robot == null || tier < 0 || tier >= RobotTiers) return false;

        robot.SetSowPreset(tier, seedId);
        robot.SetSowFertPreset(tier, fertId);

        var isSet = robot.GetSowPreset(tier) == seedId && robot.GetSowFertPreset(tier) == fertId;

        _log(isSet ? $"robot {robotId} tier {tier} now sows {seedId} with {fertId}" : $"robot {robotId} did not take seed {seedId} / fertilizer {fertId} on tier {tier}");

        return isSet;
    });

    private ReadRun StartRead(Game game, bool isSync)
    {
        var run = new ReadRun(game.World.Pointer, isSync, UnityEngine.Time.unscaledTime);

        run.Job = new SlicedJob(ReadSteps(game, run));

        return run;
    }

    /// <summary>
    /// The read's steps: a step per pot (and per few crops of an empty pot's suitability check), and a step per batch of
    /// the furniture, robots, items and HUD ids in between. Only the catalog build ever waits, for the icon scan.
    /// </summary>
    private IEnumerable<bool> ReadSteps(Game game, ReadRun run)
    {
        var (world, config, agents, role) = (game.World, game.Config, game.Agents, game.Role);
        var isChinese = GameLanguage.IsChinese();

        if (!IsHome(agents))
        {
            run.Result = new FarmRead.NotReady(Texts.Away.Pick(isChinese));

            yield break;
        }

        FarmCatalog? catalog;

        while ((catalog = CatalogFor(config, isChinese, run.IsSync ? NeverSpent : _isSliceSpent)) == null)
        {
            if (run.IsSync) throw new InvalidOperationException("the catalog was not built");

            yield return false;
        }

        var cropRows = _cropRows;
        var cropRowsById = _cropRowsById;
        var roleId = role._InstanceId_k__BackingField;
        var saveKey = SaveSlot.CurrentKey();

        KeepMemoFor(run.World, saveKey);

        // A fresh list the game builds for each call, so it is safe to hold across frames.
        var furnitures = agents.GetAllFurnitures();
        var count = furnitures?.Count ?? 0;
        var claims = world._PlantChoreManager?._claims;
        var tags = world._TagManager;
        var pots = new List<Pot>();
        var areaNames = new Dictionary<int, string>();
        var scanned = 0;

        for (var i = 0; i < count; i++)
        {
            if (++scanned >= RowsPerStep)
            {
                scanned = 0;

                yield return true;
            }

            var furniture = furnitures![i];

            // The pot kind check is one read; the shown checks are several, and most furniture is not a pot.
            if (furniture == null || !catalog.Kinds.TryGetValue(furniture._AgentConfigId_k__BackingField, out var kind) || !IsShownPot(tags, agents, furniture)) continue;

            var plant = PlantOf(furniture);

            if (plant == null) continue;

            IReadOnlySet<int> unsuitable = NoCrops;

            if (StateOf(plant._State_k__BackingField) == PotState.Empty)
            {
                var key = (furniture._InstanceId_k__BackingField, kind.Id);

                // One stale pot is checked again per read, so a home of empty pots never asks all of them in one read.
                if (_unsuitable.TryGetValue(key, out var kept) && (run.Now - kept.At < SuitabilitySeconds || run.HasRechecked))
                {
                    unsuitable = kept.Crops;
                }
                else
                {
                    if (_unsuitable.ContainsKey(key)) run.HasRechecked = true;

                    var found = new HashSet<int>();

                    // The game's check, once per crop, is the costly part of a read, so it is spread over steps.
                    for (var crop = 0; crop < cropRows.Count; crop++)
                    {
                        if (!CheckCrop(furniture, cropRows[crop], found)) break;
                        if (crop % CropsPerStep == CropsPerStep - 1) yield return true;
                    }

                    _unsuitable[key] = (run.Now, found);
                    unsuitable = found;
                }
            }

            if (ReadPot(world, kind, furniture, plant, roleId, claims, cropRowsById, areaNames, unsuitable) is { } pot) pots.Add(pot);

            scanned = 0;

            yield return true;
        }

        var robots = new List<Robot>();

        foreach (var (npc, component) in PlanterRobots(agents))
        {
            if (ReadRobot(config, agents, npc, component, isChinese, robots.Count + 1) is { } robot) robots.Add(robot);

            yield return true;
        }

        var owned = new Dictionary<int, int>();
        var available = world._ItemManager?.GetAllAvailableItems(roleId);
        var items = available?.Count ?? 0;

        for (var i = 0; i < items; i++)
        {
            if (i % RowsPerStep == RowsPerStep - 1) yield return true;

            AddOwned(available![i], catalog, roleId, owned);
        }

        var playerFloor = Floors.Level(agents.GetAgentFloor(roleId));
        var stamina = ReadStamina(role);
        var choreCost = ReadChoreCost();

        yield return true;

        var showIds = new Dictionary<long, long>();

        for (var from = 0; from < pots.Count; from += ShowIdsPerStep)
        {
            if (!AddShowIds(pots, from, showIds)) break;

            yield return true;
        }

        run.Result = new FarmRead.Ready(new FarmWorld(catalog, pots, robots, owned, areaNames, playerFloor, saveKey)
        {
            Stamina = stamina,
            ChoreCost = choreCost,
            ShowIds = showIds,
        });
    }

    /// <summary>Pot checks and room names belong to one game: another save or world starts them over.</summary>
    private void KeepMemoFor(IntPtr world, string? saveKey)
    {
        if (_memoKey.World == world && _memoKey.SaveKey == saveKey) return;

        _memoKey = (world, saveKey);
        _unsuitable.Clear();
        _areaNames.Clear();
    }

    private PlantComponent? PlantOf(Furniture furniture)
    {
        try
        {
            return AgentTools.GetAgentComponent<PlantComponent>(furniture);
        }
        catch (Exception ex)
        {
            ReportOnce("pot read", ex);

            return null;
        }
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

    /// <summary>
    /// The HUD's plant list names pots by their shown model's id; the page matches its rows to pots with these. One batch
    /// of pots per call; false once there is nothing more to add (no shown world, or a failure).
    /// </summary>
    private bool AddShowIds(List<Pot> pots, int from, Dictionary<long, long> ids)
    {
        try
        {
            var models = GameSingleton.Existing<GameCore.HotUpdate.Battle.Show.BattleShowWorld>()?._AgentModelManager;

            if (models == null) return false;

            for (var i = from; i < pots.Count && i < from + ShowIdsPerStep; i++)
            {
                var model = models.GetAgentModelByLogicInstanceId(pots[i].Id);

                if (model != null) ids[pots[i].Id] = model._InstanceId_k__BackingField;
            }

            return true;
        }
        catch (Exception ex)
        {
            ReportOnce("show id read", ex);

            return false;
        }
    }

    private static Game? LoadedGame()
    {
        var world = GameSingleton.Existing<BattleLogicWorld>();
        var config = GameSingleton.Existing<ConfigManager>();
        var agents = world?._AgentManager;
        var role = agents?.GetLeadingRole();

        if (world == null || config == null || agents == null || role == null || world._GameTimeManager?.WorldStateType != WorldStateType.PostDisaster) return null;

        return new Game(world, config, agents, role);
    }

    /// <summary>Every pot the plant HUD would show: placed, at home, on an unlocked floor and in an unlocked area.</summary>
    private static bool IsShownPot(TagManager? tags, AgentManager agents, Furniture furniture)
    {
        var map = furniture._MapConfigId_k__BackingField;

        return furniture._IsShow_k__BackingField && agents.IsHomeMap(map) && FloorTagConstants.IsMapPointUnlocked(tags, map) && AreaUnlockConstants.IsFurnitureAreaUnlocked(furniture);
    }

    private Pot? ReadPot(
        BattleLogicWorld world, PotKind kind, Furniture furniture, PlantComponent plant, long roleId,
        Il2CppSystem.Collections.Generic.Dictionary<long, long>? claims, Dictionary<int, Config_Plant> cropRows, Dictionary<int, string> areaNames, IReadOnlySet<int> unsuitable)
    {
        try
        {
            var potId = furniture._InstanceId_k__BackingField;
            var robotId = claims != null && claims.TryGetValue(potId, out var claimedBy) ? claimedBy : 0L;
            var isPending = plant._IsPendingPlant_k__BackingField || plant.IsPendingFertWork() || world._ActionManager?.HasActionInQueue(roleId, PlantAction, potId) == true;
            var area = ReadArea(furniture, areaNames);
            var state = StateOf(plant._State_k__BackingField);
            var isGrowing = state == PotState.Growing;
            var now = isGrowing || state == PotState.Ripe ? plant.GetCurrentTotalSeconds() : 0;
            var remain = isGrowing ? TimeToRipe(plant, now) : 0;
            var total = isGrowing ? plant.GetActualGrowthTotalSeconds() : 0;
            var progress = state switch
            {
                PotState.Growing => total > 0 ? 1f - (float)remain / total : 0f,
                PotState.Ripe or PotState.Withered or PotState.Poor => 1f,
                _ => 0f,
            };

            return new Pot(
                potId,
                kind.Id,
                Floors.Level(furniture._MapConfigId_k__BackingField),
                area,
                state,
                state == PotState.Empty ? 0 : plant._PlantConfigId_k__BackingField,
                (Problems)(int)plant._AnomalyFlags_k__BackingField & KnownProblems,
                progress,
                isGrowing ? remain / SecondsPerHour : null,
                state == PotState.Ripe ? HoursToRot(cropRows, plant, now) : null,
                state == PotState.Empty ? 0 : plant._FertItemConfigId_k__BackingField,
                isPending,
                robotId,
                !kind.NeedsPower || IsPowered(plant),
                unsuitable);
        }
        catch (Exception ex)
        {
            ReportOnce("pot read", ex);

            return null;
        }
    }

    /// <summary>A stalled crop does not grow, so the stall so far is added back, as the HUD's countdown does.</summary>
    private static int TimeToRipe(PlantComponent plant, int now)
    {
        var remain = plant.GetRemainSeconds();
        var stallStart = plant._AnomalyStallStartSeconds_k__BackingField;

        return plant._AnomalyFlags_k__BackingField == 0 || stallStart <= 0 ? remain : remain + Math.Max(0, now - stallStart);
    }

    /// <summary>A ripe crop rots when its harvest window runs out; a crop with no window never does.</summary>
    private static float? HoursToRot(Dictionary<int, Config_Plant> cropRows, PlantComponent plant, int now)
    {
        var matureStart = plant._MatureStartTotalSeconds_k__BackingField;

        if (plant.NeverWithers() || matureStart <= 0) return null;

        var window = cropRows.TryGetValue(plant._PlantConfigId_k__BackingField, out var crop) ? plant.GetHarvestWindowSeconds(crop) : 0;

        if (window <= 0) return null;

        return Math.Max(0, matureStart + window - now) / SecondsPerHour;
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
    /// One crop of an empty pot's suitability check, by the game's test of the pot's light and temperature. False
    /// when the test failed, which ends the pot's check with the crops found so far.
    /// </summary>
    private bool CheckCrop(Furniture furniture, Config_Plant crop, HashSet<int> unsuitable)
    {
        try
        {
            if (!PlantComponent.IsEnvironmentSuitable(furniture, crop)) unsuitable.Add(crop.ID);

            return true;
        }
        catch (Exception ex)
        {
            ReportOnce("suitability check", ex);

            return false;
        }
    }

    private int ReadArea(Furniture furniture, Dictionary<int, string> areaNames)
    {
        try
        {
            var area = EnvAreaHelper.GetFurnitureAreaId(furniture);

            if (area > 0 && !areaNames.ContainsKey(area))
            {
                if (!_areaNames.TryGetValue(area, out var name))
                {
                    name = GameNames.NonEmpty(EnvAreaHelper.GetAreaDisplayName(area)) ?? "";
                    _areaNames[area] = name;
                }

                areaNames[area] = name;
            }

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

    private Robot? ReadRobot(ConfigManager config, AgentManager agents, GameCore.HotUpdate.Battle.Logic.DynamicNpc npc, RobotComponent component, bool isChinese, int number)
    {
        try
        {
            var tiers = new RobotTier[RobotTiers];

            for (var tier = 0; tier < RobotTiers; tier++) tiers[tier] = new RobotTier(RobotSowRules.CapacityOfTier(tier), component.GetSowPreset(tier), component.GetSowFertPreset(tier));

            var id = npc._InstanceId_k__BackingField;
            var name = RobotName(config, npc._AgentConfigId_k__BackingField) ?? Texts.RobotNumber.Say(isChinese, number);

            return new Robot(id, name, Floors.Level(agents.GetAgentFloor(id)), tiers, component.Battery, component.BatteryMax, component.MasterOn);
        }
        catch (Exception ex)
        {
            ReportOnce("robot read", ex);

            return null;
        }
    }

    private List<(GameCore.HotUpdate.Battle.Logic.DynamicNpc Npc, RobotComponent Robot)> PlanterRobots(AgentManager agents)
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

    /// <summary>A robot kind's name, kept per kind until the catalog is built again (a new game or language).</summary>
    private string? RobotName(ConfigManager config, int configId)
    {
        if (_robotNames.TryGetValue(configId, out var known)) return known;

        var row = config._Config_Player_Dict is { } table && table.TryGetValue(configId, out var found) ? found : null;
        var name = row == null ? null : GameNames.Translate(config, row.Name) ?? GameNames.NonEmpty(row.Name_Local);

        _robotNames[configId] = name;

        return name;
    }

    /// <summary>
    /// Seeds and fertilizer the player can reach: their bag and every home storage the game counts for building costs,
    /// skipping storage the game keeps out of that pool.
    /// </summary>
    private static void AddOwned(ItemData? item, FarmCatalog catalog, long roleId, Dictionary<int, int> owned)
    {
        if (item == null) return;

        var count = item._ItemCount_k__BackingField;
        var itemId = item._ItemConfigId_k__BackingField;

        if (count <= 0) return;
        if (!catalog.Seeds.ContainsKey(itemId) && !catalog.Fertilizers.ContainsKey(itemId)) return;

        var ownerId = item._OwnerId_k__BackingField;

        if (ownerId != roleId && ItemManager.IsExcludedFromCostPool(ownerId)) return;

        owned[itemId] = (owned.TryGetValue(itemId, out var held) ? held : 0) + count;
    }

    /// <summary>
    /// The tables only change with the language, so the catalog is built once per game and language. Building it reads
    /// every pot kind, crop, seed and fertilizer, so it runs a slice at a time; null until it is all read. A read for a
    /// press passes NeverSpent, which builds whatever is left at once.
    /// </summary>
    private FarmCatalog? CatalogFor(ConfigManager config, bool isChinese, Func<bool> isSliceSpent)
    {
        var key = (config.Pointer, isChinese);

        if (_catalog != null && _catalogKey == key) return _catalog;

        if (_build == null || _buildKey != key)
        {
            _build = new SlicedJob(CatalogSteps(config));
            _buildKey = key;
            _built = null;
        }

        var build = _build;

        try
        {
            if (!build.Advance(isSliceSpent)) return null;
        }
        catch (Exception)
        {
            _build = null;

            throw;
        }

        _build = null;
        _catalog = _built ?? throw new InvalidOperationException("the catalog build ended without a catalog");
        _catalogKey = key;
        _built = null;
        _cropRows = _builtCropRows;
        _cropRowsById = new Dictionary<int, Config_Plant>(_cropRows.Count);

        foreach (var row in _cropRows) _cropRowsById[row.ID] = row;

        _builtCropRows = new List<Config_Plant>();
        _unsuitable.Clear();
        _areaNames.Clear();
        _robotNames.Clear();
        _log($"read {_catalog.Kinds.Count} pot kinds, {_catalog.Crops.Count} crops, {_catalog.Seeds.Count} seeds, {_catalog.Fertilizers.Count} fertilizers");

        return _catalog;
    }

    /// <summary>
    /// The catalog's reads: the tables in batches, then one pot kind, crop, seed or fertilizer per step. The tables are
    /// read straight, not through ConfigManager.Get_Config_*, which write an error to the game's log for every id they
    /// do not have. The config tables never change while a game runs, so a table is walked across frames. Waits first
    /// for the icon scan, since every name comes with its icon; a read for a press waits for it there and then.
    /// </summary>
    private IEnumerable<bool> CatalogSteps(ConfigManager config)
    {
        while (!AreIconsIn())
        {
            if (_isSyncRead) WaitForIcons();
            else yield return false;
        }

        var potRows = new List<Config_Furniture>();
        var seen = 0;

        if (config._Config_Furniture_Dict is { } furnitureRows)
        {
            foreach (var row in furnitureRows.Values)
            {
                if (row != null && row.PlantFurnitureID > 0) potRows.Add(row);
                if (++seen % RowsPerStep == 0) yield return true;
            }
        }

        var kinds = new Dictionary<int, PotKind>();
        var potPlants = config._Config_FurniturePlant_Dict;

        foreach (var row in potRows)
        {
            var plant = potPlants != null && potPlants.TryGetValue(row.PlantFurnitureID, out var found) ? found : null;

            if (plant != null)
            {
                var name = GameNames.Translate(config, row.Name) ?? $"Pot {row.ID}";

                kinds[row.ID] = new PotKind(row.ID, name, Icons().Resolve(GameNames.NonEmpty(row.ICON)), plant.Capacity, plant.AddLight, plant.AddHeat, plant.NeedPower, plant.GrowthFaster);
            }

            yield return true;
        }

        var cropRows = new List<Config_Plant>();

        if (config._Config_Plant_Dict is { } plantRows)
        {
            foreach (var row in plantRows.Values)
            {
                if (row != null) cropRows.Add(row);
                if (++seen % RowsPerStep == 0) yield return true;
            }
        }

        var crops = new Dictionary<int, Crop>();

        foreach (var row in cropRows)
        {
            var name = GameNames.Translate(config, row.Name) ?? GameNames.NonEmpty(row.Name_Local) ?? $"Crop {row.ID}";
            var harvest = row.Gain != null && row.Gain.Count > 0 ? ItemRow(config, row.Gain[0]) : null;

            crops[row.ID] = new Crop(row.ID, name, ItemIcon(harvest), row.Size, row.GrowthTime / SecondsPerHour, row.HarvestTime / SecondsPerHour, GreenhouseResearchPlant.IsResearchPlant(row.ID));

            yield return true;
        }

        var seedRows = new List<Config_Item>();
        var fertRows = new List<Config_Item>();

        if (config._Config_Item_Dict is { } itemRows)
        {
            foreach (var row in itemRows.Values)
            {
                if (row != null && row.Category == SeedCategory) seedRows.Add(row);
                else if (row != null && row.Category == FertilizerCategory) fertRows.Add(row);

                if (++seen % RowsPerStep == 0) yield return true;
            }
        }

        var seeds = new Dictionary<int, SeedItem>();

        foreach (var item in seedRows)
        {
            if (item.Plant > 0) seeds[item.ID] = new SeedItem(item.ID, ItemName(config, item), ItemIcon(item), item.Plant);

            yield return true;
        }

        var fertilizers = new Dictionary<int, Fertilizer>();

        foreach (var item in fertRows)
        {
            fertilizers.Add(item.ID, new Fertilizer(item.ID, ItemName(config, item), ItemIcon(item), item.Plant_Fast));

            yield return true;
        }

        _builtCropRows = cropRows;
        _built = new FarmCatalog(kinds, crops, seeds, fertilizers);
    }

    private static Config_Item? ItemRow(ConfigManager config, int id) =>
        config._Config_Item_Dict is { } items && items.TryGetValue(id, out var item) ? item : null;

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
            if (furnitures[i]?._InstanceId_k__BackingField == potId) return furnitures[i];
        }

        return null;
    }

    private static string Queued(Game game, int actionId, long potId) =>
        game.World._ActionManager?.HasActionInQueue(game.Role._InstanceId_k__BackingField, actionId, potId) == true ? " (queued)" : " (not seen in the queue yet)";

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
        GameNames.ItemName(config, item) ?? $"Item {item.ID}";

    private string? ItemIcon(Config_Item? item) => item == null ? null : Icons().Resolve(GameNames.NonEmpty(item.WebIcon) ?? GameNames.NonEmpty(item.Icon));

    private IconIndex Icons() => _icons ?? IconIndex.Empty;

    /// <summary>
    /// Whether the web icons are indexed. The first call starts the scan on a worker thread (the game ships well over a
    /// thousand icon files), so it is done long before a save is loaded; what it found is logged here, on the main thread.
    /// </summary>
    private bool AreIconsIn()
    {
        if (_icons != null) return true;

        _iconScan ??= StartIconScan();

        if (!_iconScan.IsCompleted) return false;

        var (icons, note) = _iconScan.Status == TaskStatus.RanToCompletion
            ? _iconScan.Result
            : (IconIndex.Empty, NoIconsNote + _iconScan.Exception?.GetBaseException().Message);

        _icons = icons;
        _iconScan = null;
        _log(note);

        return true;
    }

    /// <summary>A read for a press cannot wait for a later frame, so it waits for the scan here. Only the first seconds of a game can get this far.</summary>
    private void WaitForIcons()
    {
        _iconScan ??= StartIconScan();

        try
        {
            _iconScan.Wait();
        }
        catch (AggregateException)
        {
            // AreIconsIn reports it.
        }

        AreIconsIn();
    }

    private static Task<(IconIndex Icons, string Note)> StartIconScan()
    {
        try
        {
            return WebIcons.StartScan();
        }
        catch (Exception ex)
        {
            return Task.FromResult((IconIndex.Empty, NoIconsNote + ex.Message));
        }
    }

    private void ReportOnce(string operation, Exception ex)
    {
        if (_reportedFailures.Add(operation)) _log($"{operation} failed (each kind reported once): {ex}");
    }

    /// <summary>The loaded game's world, tables, agents and player.</summary>
    private readonly struct Game
    {
        public Game(BattleLogicWorld world, ConfigManager config, AgentManager agents, LeadingRole role)
        {
            World = world;
            Config = config;
            Agents = agents;
            Role = role;
        }

        public BattleLogicWorld World { get; }

        public ConfigManager Config { get; }

        public AgentManager Agents { get; }

        public LeadingRole Role { get; }
    }

    /// <summary>One read of the farm in progress: its world, its clock, its one suitability recheck, and what it found.</summary>
    private sealed class ReadRun
    {
        public ReadRun(IntPtr world, bool isSync, float now)
        {
            World = world;
            IsSync = isSync;
            Now = now;
        }

        public IntPtr World { get; }

        public bool IsSync { get; }

        public float Now { get; }

        public SlicedJob Job { get; set; } = null!;

        public bool HasRechecked { get; set; }

        public FarmRead? Result { get; set; }
    }
}
