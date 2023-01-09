﻿namespace ChuckDeviceController.JobControllers;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using ChuckDeviceController.Collections;
using ChuckDeviceController.Common;
using ChuckDeviceController.Common.Jobs;
using ChuckDeviceController.Common.Tasks;
using ChuckDeviceController.Data.Common;
using ChuckDeviceController.Data.Entities;
using ChuckDeviceController.Data.Extensions;
using ChuckDeviceController.Data.Repositories.Dapper;
using ChuckDeviceController.Extensions;
using ChuckDeviceController.Geometry;
using ChuckDeviceController.Geometry.Extensions;
using ChuckDeviceController.Geometry.Models;
using ChuckDeviceController.Geometry.Models.Abstractions;
using ChuckDeviceController.JobControllers.Models;
using ChuckDeviceController.JobControllers.Tasks;
using ChuckDeviceController.JobControllers.Utilities;

public class AutoInstanceController : IJobController
{
    #region Variables

    private readonly ILogger<AutoInstanceController> _logger;
    private readonly IDapperUnitOfWork _uow;

    private readonly SafeCollection<PokestopWithMode> _allStops = new();
    private readonly SafeCollection<PokestopWithMode> _todayStops = new();
    private readonly ConcurrentDictionary<(string, bool), byte> _todayStopsAttempts = new(); // (PokestopId, IsAlternative)
    private readonly SafeCollection<ulong> _bootstrapCellIds = new();
    private readonly ConcurrentDictionary<string, string> _accounts = new();
    private readonly ConcurrentDictionary<string, bool> _lastMode = new();
    private readonly SafeCollection<string> _ignorePokestopIds = new();

    private readonly System.Timers.Timer _timer;
    private int _bootstrapTotalCount = 0;
    private ulong _completionDate = 0;
    private ulong _lastCompletionCheck = DateTime.UtcNow.ToTotalSeconds() - Strings.SixtyMinutesS;

    #endregion

    #region Properties

    public string Name { get; }

    public IReadOnlyList<IMultiPolygon> MultiPolygons { get; }

    public ushort MinimumLevel { get; }

    public ushort MaximumLevel { get; }

    public string GroupName { get; }

    public bool IsEvent { get; }

    public short TimeZoneOffset { get; }

    public AutoInstanceType AutoType { get; }

    public uint SpinLimit { get; }

    public bool IgnoreS2CellBootstrap { get; }

    public bool RequireAccountEnabled { get; }

    public bool UseWarningAccounts { get; }

    public QuestMode QuestMode { get; }

    public byte MaximumSpinAttempts { get; }

    public ushort LogoutDelay { get; }

    #endregion

    #region Events

    public event EventHandler<AutoInstanceCompleteEventArgs>? InstanceComplete;
    private void OnInstanceComplete(string instanceName, ulong completionTimestamp, AutoInstanceType instanceType = AutoInstanceType.Quest)
    {
        InstanceComplete?.Invoke(this, new AutoInstanceCompleteEventArgs(instanceName, completionTimestamp, instanceType));
    }

    #endregion

    #region Constructor

    public AutoInstanceController(
        IDapperUnitOfWork uow,
        Instance instance,
        IReadOnlyList<IMultiPolygon> multiPolygons,
        short timeZoneOffset = Strings.DefaultTimeZoneOffset)
    {
        Name = instance.Name;
        MultiPolygons = multiPolygons;
        MinimumLevel = instance.MinimumLevel;
        MaximumLevel = instance.MaximumLevel;
        GroupName = instance.Data?.AccountGroup ?? Strings.DefaultAccountGroup;
        IsEvent = instance.Data?.IsEvent ?? Strings.DefaultIsEvent;
        SpinLimit = instance.Data?.SpinLimit ?? Strings.DefaultSpinLimit;
        AutoType = AutoInstanceType.Quest;
        IgnoreS2CellBootstrap = instance.Data?.IgnoreS2CellBootstrap ?? Strings.DefaultIgnoreS2CellBootstrap;
        TimeZoneOffset = timeZoneOffset;
        UseWarningAccounts = instance.Data?.UseWarningAccounts ?? Strings.DefaultUseWarningAccounts;
        QuestMode = instance.Data?.QuestMode ?? Strings.DefaultQuestMode;
        MaximumSpinAttempts = instance.Data?.MaximumSpinAttempts ?? Strings.DefaultMaximumSpinAttempts;
        LogoutDelay = instance.Data?.LogoutDelay == 0
            ? Strings.DefaultLogoutDelay
            : instance.Data?.LogoutDelay ?? Strings.DefaultLogoutDelay;
        RequireAccountEnabled = true; // TODO: Make 'RequireAccountEnabled' configurable via Instance.Data

        _logger = new Logger<AutoInstanceController>(LoggerFactory.Create(x => x.AddConsole()));
        _uow = uow;

        var (localTime, timeLeft) = GetSecondsUntilMidnight();
        timeLeft = timeLeft <= 0 ? 1 : timeLeft;
        _timer = new System.Timers.Timer(timeLeft * 1000);
        _timer.Elapsed += async (sender, e) => await ClearQuestsAsync();
        _timer.Start();

        _logger.LogInformation("[{Name}] Clearing Quests in {TimeLeft:N0}s at 12:00 AM (Currently: {LocalTime})", Name, timeLeft, localTime);

        Task.Run(UpdateAsync).Wait();

        if (!IgnoreS2CellBootstrap)
        {
            Task.Run(BootstrapAsync).Wait();
        }
    }

    #endregion

    #region Public Methods

    public async Task<ITask> GetTaskAsync(TaskOptions options)
    {
        switch (AutoType)
        {
            case AutoInstanceType.Quest:
                if (_bootstrapCellIds.Count > 0)
                {
                    var bootstrapTask = await CreateBootstrapTaskAsync();
                    return bootstrapTask;
                }

                if (string.IsNullOrEmpty(options.AccountUsername) && RequireAccountEnabled)
                {
                    _logger.LogWarning("[{Name}] No username specified for device '{Uuid}', ignoring...", Name, options.Uuid);
                    return null!;
                }

                if (options.Account == null && RequireAccountEnabled)
                {
                    _logger.LogWarning("[{Name}] No account specified for device '{Uuid}', ignoring...", Name, options.Uuid);
                    return null!;
                }

                if (_allStops.Count == 0)
                {
                    return null!;
                }

                await CheckCompletionStatusAsync(options.Uuid);

                PokestopWithMode? pokestop;
                Coordinate? lastCoord;
                try
                {
                    lastCoord = Cooldown.GetLastLocation(options.Account, options.Uuid);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[{Name}] Failed to get last location for device '{options.Uuid}': {ex}");
                    return null!;
                }

                if (lastCoord != null)
                {
                    var closest = HandleModeSwitch(lastCoord, options.Uuid, options.AccountUsername);
                    if (closest == null)
                    {
                        return null!;
                    }

                    pokestop = closest;

                    var nearbyStops = new List<PokestopWithMode> { pokestop };
                    var pokestopCoord = pokestop.Pokestop.ToCoordinate();
                    var todayStopsC = _todayStops;
                    foreach (var todayStop in todayStopsC)
                    {
                        var distance = pokestopCoord.DistanceTo(todayStop.Pokestop.ToCoordinate());
                        if (pokestop.IsAlternative == todayStop.IsAlternative && distance <= Strings.SpinRangeM)
                        {
                            nearbyStops.Add(todayStop);
                        }
                    }

                    foreach (var stop in nearbyStops)
                    {
                        _todayStops.Remove(stop);
                    }
                }
                else
                {
                    PokestopWithMode? stop = _todayStops.TakeFirst();
                    if (stop == null)
                    {
                        return null!;
                    }

                    pokestop = stop;
                }

                var delay = Strings.DefaultLogoutDelay;
                var encounterTime = DateTime.UtcNow.ToTotalSeconds();
                try
                {
                    var result = Cooldown.SetCooldown(options.Account, pokestop.Pokestop.ToCoordinate());
                    if (result != null)
                    {
                        delay = Convert.ToUInt16(result.Delay);
                        encounterTime = result.EncounterTime;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[{Name}] [{options.Uuid}] Failed to calculate cooldown time for device: {ex}");
                    var result = _todayStops.TryAdd(pokestop);
                    if (!result)
                    {
                        // Failed to add pokestop
                    }
                    return null!;
                }

                if (delay >= LogoutDelay)
                {
                    if (options.Account != null)
                    {
                        // Delay is too high, switch accounts
                        return await HandlePokestopDelayAsync(pokestop, options.Uuid, options.AccountUsername);
                    }

                    _logger.LogWarning("[{Name}] [{Uuid}] Ignoring over logout delay, no account is specified", Name, options.Uuid);
                }

                try
                {
                    if (!string.IsNullOrEmpty(options.AccountUsername))
                    {
                        // Increment account spin count
                        await Cooldown.SetSpinCountAsync(_uow, options.AccountUsername);
                    }

                    await Cooldown.SetEncounterAsync(
                        _uow,
                        (Account?)options.Account,
                        pokestop.Pokestop.ToCoordinate(),
                        encounterTime
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[{Name}] [{options.Uuid}] Failed to store cooldown: {ex}");
                    var result = _todayStops.TryAdd(pokestop);
                    if (!result)
                    {
                        // Failed to add pokestop
                    }
                    return null!;
                }

                IncrementSpinAttempt(pokestop);

                await CheckCompletionStatusAsync(options.Uuid);

                var modeKey = options.AccountUsername ?? options.Uuid;
                if (!_lastMode.ContainsKey(modeKey))
                {
                    _lastMode.AddOrUpdate(modeKey, pokestop.IsAlternative, (key, oldValue) => pokestop.IsAlternative);
                }
                else
                {
                    _lastMode[modeKey] = pokestop.IsAlternative;
                }

                // TODO: setArQuestTarget(uuid, timestamp, pokestop.IsAlternative);
                var task = CreateQuestTask(pokestop, delay);
                return task;
        }
        return null!;
    }

    public async Task<string> GetStatusAsync()
    {
        switch (AutoType)
        {
            case AutoInstanceType.Quest:
                if (_bootstrapCellIds.Any())
                {
                    var totalCount = _bootstrapTotalCount;
                    var foundCount = totalCount - _bootstrapCellIds.Count;
                    var percentage = foundCount > 0 && totalCount > 0
                        ? Convert.ToDouble((double)foundCount / totalCount) * 100
                        : 0;

                    var bootstrapStatus = $"Bootstrapping: {foundCount:N0}/{totalCount:N0} ({Math.Round(percentage, 2)}%)";
                    return bootstrapStatus;
                }

                var ids = _allStops.Select(x => x.Pokestop.Id).ToList();
                var currentCountDb = await GetPokestopQuestCountAsync(ids, QuestMode);

                var maxCount = _allStops.Count;
                var currentCount = maxCount - _todayStops.Count;

                var percent = currentCount > 0 && maxCount > 0
                    ? Convert.ToDouble((double)currentCount / maxCount) * 100
                    : 0;
                var percentReal = currentCountDb > 0 && maxCount > 0
                    ? Convert.ToDouble((double)currentCountDb / maxCount) * 100
                    : 0;

                var completedDate = _completionDate
                    .FromSeconds()
                    .ToLocalTime()
                    .ToString("hh:mm:ss tt");
                var ignoredStatus = _ignorePokestopIds.Count > 0
                    ? $", Ign: {_ignorePokestopIds.Count:N0}"
                    : string.Empty;
                var isCompleted = _completionDate != default;
                var html = Utils.GetQueueLink(Name, displayText: "Queue", basePath: "/Instance/QuestQueue", html: true);
                var status = $"{(isCompleted ? $"Status: " : $"{html}: {_todayStops.Count:N0},")} " +
                    $"{currentCountDb:N0}|{currentCount:N0}/{maxCount:N0}" +
                    ignoredStatus +
                    $" ({Math.Round(percentReal, 1)}|{Math.Round(percent, 1)}%)" +
                    $"{(isCompleted ? $", Completed @ {completedDate}" : "")}";
                return status;
        }
        return null!;
    }

    public async Task ReloadAsync()
    {
        _logger.LogDebug("[{Name}] Reloading instance", Name);

        await UpdateAsync();
    }

    public async Task StopAsync()
    {
        _logger.LogDebug("[{Name}] Stopping instance", Name);

        _timer.Stop();

        await Task.CompletedTask;
    }

    /// <summary>
    /// Clears all Pokestop quest data that has been found
    /// </summary>
    public async Task ClearQuestsAsync()
    {
        _timer.Stop();
        var (localTime, timeLeft) = GetSecondsUntilMidnight();
        //var now = localTime.ToTotalSeconds();
        // Timer interval cannot be set to 0, calculate one full day
        // in seconds to use for the next quest clearing interval.
        _timer.Interval = (timeLeft <= 0 ? Strings.OneDayS : timeLeft) * 1000;
        _timer.Start();

        if (_allStops.Count == 0)
        {
            _logger.LogWarning("[{Name}] Tried clearing quests but no pokestops with quests.", Name);
            return;
        }

        // Clear quests
        var pokestopCount = _allStops.Count;
        _logger.LogInformation("[{Name}] Clearing Quests for {Count:N0} Pokestops...", Name, pokestopCount);

        var sw = new Stopwatch();
        sw.Start();
        await ClearPokestopQuestsAsync(_allStops);
        sw.Stop();
        var totalSeconds = Math.Round(sw.Elapsed.TotalSeconds, 4);
        _logger.LogInformation("[{Name}] {Count:N0} Pokestop Quests have been cleared, time taken: {TotalSeconds}s", Name, pokestopCount, totalSeconds);

        await UpdateAsync();
    }

    #endregion

    #region Quest Queue Management

    public IReadOnlyList<PokestopWithMode> GetQueue() => _todayStops.ToList();

    public void RemoveFromQueue(string pokestopId)
    {
        // Add pokestop to ignore list
        if (!_ignorePokestopIds.Contains(pokestopId))
        {
            var result = _ignorePokestopIds.TryAdd(pokestopId);
            if (!result)
            {
                // Failed to add pokestop ID to ignored pokestop cache
            }
        }

        // Check ignore list against todays list when retrieving next pokestop
        var stopToIgnore = _todayStops.FirstOrDefault(stop => stop.Pokestop.Id == pokestopId);
        if (stopToIgnore != null)
        {
            _todayStops.Remove(stopToIgnore);
            if (_allStops.Contains(stopToIgnore))
            {
                _allStops.Remove(stopToIgnore);
            }
        }
    }

    public void ClearQueue()
    {
        // Add all Pokestop ids from _todayStops to ignore list
        var todayStops = _todayStops;
        for (var i = 0; i < todayStops.Count; i++)
        {
            // Add pokestop to ignore list
            var stop = todayStops[i];
            if (stop == null)
                continue;

            if (!_ignorePokestopIds.Contains(stop.Pokestop.Id))
            {
                _ignorePokestopIds.Remove(stop.Pokestop.Id);
            }
        }

        //_allStops.Clear();
        _todayStops.Clear();
    }

    #endregion

    #region Private Methods

    private async Task BootstrapAsync()
    {
        _logger.LogInformation("[{Name}] Checking Bootstrap Status...", Name);

        var sw = new Stopwatch();
        sw.Start();

        var allCellIds = new List<ulong>();

        // Loop through all geofences and get s2cells within each geofence
        foreach (var polygon in MultiPolygons)
        {
            // Get maximum amount of S2 level 15 cells within this geofence
            var s2CellIds = polygon.GetS2CellIds(15, 15, int.MaxValue);
            allCellIds.AddRange(s2CellIds);
        }

        // Get all known cells from the database
        // Get S2 cells within multi polygon
        var existingCells = await GetS2CellsAsync(MultiPolygons);
        var existingCellIds = existingCells
            .Select(cell => cell.Id)
            .ToList();

        // Remove any duplicates
        allCellIds = allCellIds.Distinct().ToList();

        // Filter all existing S2 cells not already found 
        var missingCellIds = allCellIds
            .Where(cellId => !existingCellIds.Contains(cellId))
            //.Distinct()
            .ToList();

        var total = allCellIds.Count;
        var found = total - missingCellIds.Count;

        sw.Stop();
        var totalSeconds = Math.Round(sw.Elapsed.TotalSeconds, 4);
        _logger.LogInformation("[{Name}] Bootstrap Status: {Found:N0}/{Total:N0} after {TotalSeconds} seconds", Name, found, total, totalSeconds);

        _bootstrapCellIds.Clear();
        _bootstrapCellIds.AddRange(missingCellIds);
        _bootstrapTotalCount = total;
    }

    /// <summary>
    /// Updates the list of Pokestops available to spin for today that do not have quests found
    /// </summary>
    private async Task UpdateAsync()
    {
        switch (AutoType)
        {
            case AutoInstanceType.Quest:
                // Clear all existing cached Pokestops to fetch updated entities
                _allStops.Clear();

                // Loop through all specified geofences for Pokestops found within them
                foreach (var polygon in MultiPolygons)
                {
                    try
                    {
                        // Get all Pokestops within bounding box of geofence. Some Pokestops
                        // closely outside of geofence will also be returned
                        var bbox = polygon.GetBoundingBox();
                        var stops = await GetPokestopsInBoundsAsync(bbox, isEnabled: true);

                        //var isNormal = QuestMode == QuestMode.Normal || QuestMode == QuestMode.Both;
                        var isAlternative = QuestMode == QuestMode.Alternative || QuestMode == QuestMode.Both;
                        foreach (var stop in stops)
                        {
                            // Skip quests for any Pokestops the user has set to ignore via Quest queue
                            if (_ignorePokestopIds.Contains(stop.Id))
                                continue;

                            // Filter any Pokestops not within the geofence
                            if (!GeofenceService.InPolygon(polygon, stop.ToCoordinate()))
                                continue;

                            var result = _allStops.TryAdd(new PokestopWithMode(stop, isAlternative));
                            if (!result)
                            {
                                // Failed to add pokestop to all pokestops list
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"[{Name}] Error: {ex}");
                    }
                }

                _todayStops.Clear();
                _todayStopsAttempts.Clear();
                _completionDate = 0;

                // TODO: Provide starting coord/get last coord
                var polygonCoord = MultiPolygons[0][0];
                var startCoord = new Coordinate(polygonCoord[0], polygonCoord[1]);
                _allStops.Sort((a, b) =>
                {
                    var coordA = a.Pokestop.ToCoordinate();
                    var coordB = b.Pokestop.ToCoordinate();
                    var distanceA = coordA.DistanceTo(startCoord);
                    var distanceB = coordB.DistanceTo(startCoord);
                    var result = distanceA.CompareTo(distanceB);
                    return result;
                });

                // Loop through all Pokestops found within geofence to build list of Pokestops to
                // spin for today that do not have quests found
                foreach (var stop in _allStops)
                {
                    // Check that the Pokestop does not have quests already found
                    if ((!stop.IsAlternative && stop.Pokestop.QuestType == null) ||
                       (stop.IsAlternative && stop.Pokestop.AlternativeQuestType == null))
                    {
                        // Add Pokestop if it's not already in the list
                        if (!_todayStops.Contains(stop))
                        {
                            var result = _todayStops.TryAdd(stop);
                            if (!result)
                            {
                                // Failed to add pokestop
                            }
                        }
                    }
                }
                break;
        }
    }

    private (PokestopWithMode?, bool?) GetNextClosestPokestop(Coordinate lastCoord, string modeKey)
    {
        PokestopWithMode? closestOverall = null;
        double closestOverallDistance = Strings.DefaultDistance;

        PokestopWithMode? closestNormal = null;
        double closestNormalDistance = Strings.DefaultDistance;

        PokestopWithMode? closestAlternative = null;
        double closestAlternativeDistance = Strings.DefaultDistance;

        var todayStops = _todayStops;
        if (todayStops.Count == 0)
        {
            return (null, null);
        }

        foreach (var stop in todayStops)
        {
            // Skip quests for any Pokestops the user has set to ignore via Quest queue
            if (_ignorePokestopIds.Contains(stop.Pokestop.Id))
                continue;

            var coord = stop.Pokestop.ToCoordinate();
            var dist = lastCoord.DistanceTo(coord);
            if (dist < closestOverallDistance)
            {
                closestOverall = stop;
                closestOverallDistance = dist;
            }
            if (!stop.IsAlternative && dist < closestNormalDistance)
            {
                closestNormal = stop;
                closestNormalDistance = dist;
            }
            if (stop.IsAlternative && dist < closestAlternativeDistance)
            {
                closestAlternative = stop;
                closestAlternativeDistance = dist;
            }
        }

        PokestopWithMode? closest;
        var key = modeKey;
        bool? mode = _lastMode.TryGetValue(key, out var value) ? value : null;
        if (mode == null)
        {
            closest = closestOverall;
        }
        else if (_lastMode.TryGetValue(key, out var lastModeValue) && !lastModeValue)
        {
            closest = closestNormal ?? closestOverall;
        }
        else
        {
            closest = closestAlternative ?? closestOverall;
        }

        return (closest, mode);
    }

    private async Task CheckCompletionStatusAsync(string uuid)
    {
        if (_todayStops.Count > 0)
        {
            // Pokestop list still contains items, skip check since we know for sure we haven't finished
            return;
        }

        var now = DateTime.UtcNow.ToTotalSeconds();
        // Check if the last completion delta from current time is within the last 10 minutes,
        // if it is then another device completed the instance
        if (now - _lastCompletionCheck < Strings.TenMinutesS)
        {
            HandleOnCompletion(uuid);
            return;
        }

        _lastCompletionCheck = now;

        var ids = _allStops
            .Select(stop => stop.Pokestop.Id)
            .ToList();
        var newStops = new List<Pokestop>();
        try
        {
            // Get Pokestops within S2 cells
            newStops = (await GetPokestopsByIdsAsync(ids)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError($"[{Name}] [{uuid}] Failed to get today stops: {ex}");
            return;
        }

        foreach (var stop in newStops)
        {
            var isNormal = QuestMode == QuestMode.Normal || QuestMode == QuestMode.Both;
            var isAlternative = QuestMode == QuestMode.Alternative || QuestMode == QuestMode.Both;
            if (isNormal || isAlternative)
            {
                var pokestopWithMode = new PokestopWithMode(stop, isAlternative);
                var key = (stop.Id, isAlternative);
                var spinAttemptsCount = _todayStopsAttempts.TryGetValue(key, out var value)
                    ? value
                    : 0;
                // Check if Pokestop does not have any quests found and spin attempts is less
                // than or equal to max spin attempts allowed
                if (spinAttemptsCount <= MaximumSpinAttempts &&
                   ((stop.QuestType == null && isNormal) ||
                   (stop.AlternativeQuestType == null && isAlternative)))
                {
                    var result = _todayStops.TryAdd(pokestopWithMode);
                    if (!result)
                    {
                        // Failed to add pokestop
                    }
                }
            }
        }

        if (_todayStops.Count == 0)
        {
            HandleOnCompletion(uuid);
        }

        await Task.CompletedTask;
    }

    private void IncrementSpinAttempt(PokestopWithMode pokestop, byte amount = 1)
    {
        var key = (pokestop.Pokestop.Id, pokestop.IsAlternative);
        if (_todayStopsAttempts.ContainsKey(key))
        {
            var tries = _todayStopsAttempts[key];
            _todayStopsAttempts[key] = Convert.ToByte(tries == byte.MaxValue ? 10 : tries + amount);
        }
        else
        {
            _todayStopsAttempts.AddOrUpdate(key, amount, (key, oldValue) => amount);
        }
    }

    private (DateTime, double) GetSecondsUntilMidnight()
    {
        var localTime = DateTime.UtcNow.AddHours(TimeZoneOffset);
        var timeLeft = DateTime.Today.AddDays(1).Subtract(localTime).TotalSeconds;
        var seconds = Math.Round(timeLeft);
        return (localTime, seconds);
    }

    private async Task<SwitchAccountTask> HandlePokestopDelayAsync(PokestopWithMode pokestop, string uuid, string? accountUsername)
    {
        var result = _todayStops.TryAdd(pokestop);
        if (!result)
        {
            // Failed to add pokestop
        }
        string newUsername;
        try
        {
            var pokestopCoord = pokestop.Pokestop!.ToCoordinate();
            var newAccount = await GetAccountAsync(uuid, pokestopCoord);
            if (newAccount == null)
            {
                _logger.LogWarning("[{Name}] [{Uuid}] Failed to get new account from database for device to set cache", Name, uuid);
                return CreateSwitchAccountTask();
            }

            if (!_accounts.ContainsKey(uuid))
            {
                newUsername = newAccount.Username;
                _accounts.AddOrUpdate(uuid, newAccount.Username, (key, oldValue) => newAccount.Username);

                _logger.LogDebug("[{Name}] [{Uuid}] Over logout delay. Switching account from {AccountUsername} to {NewUsername}", Name, uuid, accountUsername ?? "?", newUsername ?? "?");
            }
            else
            {
                newUsername = _accounts[uuid];
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"[{Name}] [{uuid}] Failed to get account for device in advance: {ex}");
        }

        return CreateSwitchAccountTask();
    }

    private PokestopWithMode? HandleModeSwitch(Coordinate lastCoord, string uuid, string? accountUsername)
    {
        var (closest, mode) = GetNextClosestPokestop(lastCoord, accountUsername ?? uuid);
        if (closest == null)
        {
            return null;
        }

        if ((mode ?? false) && !(closest?.IsAlternative ?? false))
        {
            var modeName = (mode ?? false) ? "alternative" : "none";
            _logger.LogDebug("[{Name}] [{AccountUsername}] Switching quest mode from {ModeName} to normal.", Name, accountUsername ?? "?", modeName);

            PokestopWithMode? closestAr = null;
            double closestArDistance = Strings.DefaultDistance;
            var arStops = _allStops
                .Where(stop => stop.Pokestop.IsArScanEligible)
                .ToList();

            foreach (var stop in arStops)
            {
                var coord = stop.Pokestop.ToCoordinate();
                var dist = lastCoord.DistanceTo(coord);
                if (dist < closestArDistance)
                {
                    closestAr = stop;
                    closestArDistance = dist;
                }
            }

            if (closestAr?.Pokestop != null)
            {
                closestAr.IsAlternative = closest?.IsAlternative ?? false;
                closest = closestAr;
                _logger.LogDebug("[{Name}] [{AccountUsername}] Scanning AR eligible Pokestop {Id}", Name, accountUsername ?? "?", closest?.Pokestop?.Id);
            }
            else
            {
                _logger.LogDebug("[{Name}] [{AccountUsername}] No AR eligible Pokestop found to scan", Name, accountUsername ?? "?");
            }
        }

        return closest;
    }

    private void HandleOnCompletion(string uuid)
    {
        _logger.LogInformation("[{Name}] [{Uuid}] Quest instance complete", Name, uuid);

        if (_completionDate == 0)
        {
            _completionDate = DateTime.UtcNow.ToTotalSeconds();
        }

        // Call OnInstanceComplete event
        OnInstanceComplete(Name, _completionDate);
    }

    #endregion

    #region Task Creators

    private async Task<BootstrapTask> CreateBootstrapTaskAsync()
    {
        var targetCellId = _bootstrapCellIds.TakeLast();
        if (targetCellId == default)
        {
            return null!;
        }

        var center = targetCellId.S2LatLngFromId();
        var coord = new Coordinate(center.LatDegrees, center.LngDegrees);
        var cellIds = center.GetLoadedS2CellIds();

        foreach (var cellId in cellIds)
        {
            _bootstrapCellIds.Remove(cellId.Id);
        }

        if (_bootstrapCellIds.Count == 0)
        {
            await BootstrapAsync();
            if (_bootstrapCellIds.Count == 0)
            {
                await UpdateAsync();
            }
        }

        return new BootstrapTask
        {
            Action = DeviceActionType.ScanRaid,
            Latitude = coord.Latitude,
            Longitude = coord.Longitude,
            MinimumLevel = MinimumLevel,
            MaximumLevel = MaximumLevel,
        };
    }

    private QuestTask CreateQuestTask(PokestopWithMode pokestop, double delay = 0)
    {
        return new QuestTask
        {
            Action = DeviceActionType.ScanQuest,
            Latitude = pokestop?.Pokestop?.Latitude ?? 0,
            Longitude = pokestop?.Pokestop?.Longitude ?? 0,
            MinimumLevel = MinimumLevel,
            MaximumLevel = MaximumLevel,
            DeployEgg = false,
            Delay = delay,
            QuestType = (pokestop?.IsAlternative ?? false)
                ? "ar"
                : "normal",
        };
    }

    private SwitchAccountTask CreateSwitchAccountTask()
    {
        return new SwitchAccountTask
        {
            Action = DeviceActionType.SwitchAccount,
            MinimumLevel = MinimumLevel,
            MaximumLevel = MaximumLevel,
        };
    }

    #endregion

    #region Database Helpers

    // TODO: Create extensions

    private async Task<Account?> GetAccountAsync(string uuid, Coordinate encounterTarget)
    {
        // TODO: Check account against encounterTarget to see if too far
        if (_accounts.TryGetValue(uuid, out var value))
        {
            var username = value;
            var result = _accounts.TryRemove(uuid, out var _);
            if (!result)
            {
                // Failed to remove account from cache
                _logger.LogWarning("[{Name}] [{Uuid}] Failed to remove account '{Username}' from cache, it may already be removed.", Name, uuid, username);
            }
            var existingAccount = await _uow.Accounts.FindAsync(username);
            return existingAccount;
        }

        var account = await GetNewAccountAsync(
            MinimumLevel,
            MaximumLevel,
            UseWarningAccounts,
            SpinLimit,
            noCooldown: true,
            GroupName,
            Strings.CooldownLimitS,
            Strings.SuspensionTimeLimitS
        );
        return account;
    }

    private async Task<Account?> GetNewAccountAsync(
        ushort minLevel = 0, ushort maxLevel = 35, bool ignoreWarning = false, uint spins = 3500,
        bool noCooldown = true, string? group = null, ushort cooldownLimitS = Strings.CooldownLimitS,
        uint suspensionTimeLimitS = Strings.SuspensionTimeLimitS)
    {
        var now = DateTime.UtcNow.ToTotalSeconds();
        var account = await _uow.Accounts.FirstOrDefaultAsync(x =>
            // Meet level requirements for instance
            (x.Level >= minLevel && x.Level <= maxLevel) &&
            // Is under total spins
            x.Spins < spins &&
            // Matches event group name
            (!string.IsNullOrEmpty(group)
                ? x.GroupName == group
                : x.GroupName == null) &&
            // Cooldown
            (x.LastEncounterTime == null || (noCooldown
                ? now - x.LastEncounterTime >= cooldownLimitS
                : x.LastEncounterTime != null)) &&
            (ignoreWarning
                // Has warning 
                ? (x.Failed == null || x.Failed == "GPR_RED_WARNING")
                // Has no account warnings or are expired already
                : (x.Failed == null && x.FirstWarningTimestamp == null) ||
                  (x.Failed == "GPR_RED_WARNING" && x.WarnExpireTimestamp > 0 && x.WarnExpireTimestamp <= now) ||
                  (x.Failed == "suspended" && x.FailedTimestamp <= now - suspensionTimeLimitS))
        );
        return account;
    }

    private async Task ClearPokestopQuestsAsync(IEnumerable<PokestopWithMode> pokestops)
    {
        var stops = pokestops.Select(x => x.Pokestop);
        var result = await _uow.Pokestops.UpdateRangeAsync(stops, mappings: new()
        {
            ["id"] = x => x.Id,
            ["quest_conditions"] = x => null!,
            ["quest_rewards"] = x => null!,
            ["quest_target"] = x => null!,
            ["quest_template"] = x => null!,
            ["quest_timestamp"] = x => null!,
            ["quest_title"] = x => null!,
            ["quest_type"] = x => null!,
            ["alternative_quest_conditions"] = x => null!,
            ["alternative_quest_rewards"] = x => null!,
            ["alternative_quest_target"] = x => null!,
            ["alternative_quest_template"] = x => null!,
            ["alternative_quest_timestamp"] = x => null!,
            ["alternative_quest_title"] = x => null!,
            ["alternative_quest_type"] = x => null!,
        });
        _logger.LogInformation("[{Name}] Clear Quests Result: {Result}", Name, result);
    }

    private async Task<ulong> GetPokestopQuestCountAsync(List<string> pokestopIds, QuestMode mode, uint maxBatchCount = 10000)
    {
        if (pokestopIds.Count > maxBatchCount)
        {
            var result = 0ul;
            var batchSize = Convert.ToInt64(Math.Ceiling(Convert.ToDouble(pokestopIds.Count) / maxBatchCount));
            for (var i = 0; i < batchSize; i++)
            {
                var start = maxBatchCount * i;
                var end = Math.Max(maxBatchCount * i, pokestopIds.Count - 1);
                var splice = pokestopIds.GetRange((int)start, (int)end);
                var spliceResult = await GetPokestopQuestCountAsync(splice, mode);
                result += spliceResult;
            }
            return result;
        }

        var pokestops = await GetPokestopsByIdsAsync(pokestopIds);
        var count = pokestops.Count(stop => HasPokestopQuestByType(stop, mode));
        return Convert.ToUInt64(count);
    }

    private async Task<IEnumerable<Pokestop>> GetPokestopsByIdsAsync(IEnumerable<string> pokestopIds, bool isEnabled = true, bool isDeleted = false)
    {
        var pokestops = await _uow.Pokestops.FindAsync(pokestop =>
            pokestopIds.Contains(pokestop.Id) &&
            isEnabled == pokestop.IsEnabled &&
            isDeleted == pokestop.IsDeleted
        );
        return pokestops;
    }

    private async Task<IEnumerable<Pokestop>> GetPokestopsInBoundsAsync(IBoundingBox bbox, bool isEnabled = true)
    {
        var pokestops = await _uow.Pokestops.FindAsync(pokestop =>
            pokestop.Latitude >= bbox.MinimumLatitude &&
            pokestop.Longitude >= bbox.MinimumLongitude &&
            pokestop.Latitude <= bbox.MaximumLatitude &&
            pokestop.Longitude <= bbox.MaximumLongitude &&
            isEnabled == pokestop.IsEnabled &&
            !pokestop.IsDeleted
        );
        return pokestops;
    }

    private static bool HasPokestopQuestByType(Pokestop pokestop, QuestMode mode)
    {
        var result = mode == QuestMode.Normal
            ? pokestop.QuestType != null
            : mode == QuestMode.Alternative
                ? pokestop.AlternativeQuestType != null
                : pokestop.QuestType != null || pokestop.AlternativeQuestType != null;
        return result;
    }

    /// <summary>
    /// Get all S2 cells within bounding box
    /// </summary>
    /// <param name="context"></param>
    /// <param name="bbox"></param>
    private async Task<IEnumerable<Cell>> GetS2CellsAsync(IBoundingBox bbox)
    {
        var cells = await _uow.Cells.FindAsync(cell =>
            cell.Latitude >= bbox.MinimumLatitude &&
            cell.Longitude >= bbox.MinimumLongitude &&
            cell.Latitude <= bbox.MaximumLatitude &&
            cell.Longitude <= bbox.MaximumLongitude
        );
        return cells;
    }

    /// <summary>
    /// Get all S2 cells within geofence polygon
    /// </summary>
    /// <param name="context"></param>
    /// <param name="multiPolygon"></param>
    /// <returns></returns>
    private async Task<IEnumerable<Cell>> GetS2CellsAsync(IMultiPolygon multiPolygon)
    {
        var bbox = multiPolygon.GetBoundingBox();
        // Get S2 cells within geofence bounding box
        var bboxCells = await GetS2CellsAsync(bbox);
        // Filter S2 cells outside of geofence polygon
        var cellsInArea = bboxCells
            .Where(cell => GeofenceService.InPolygon(multiPolygon, cell.ToCoordinate()))
            .ToList();
        return cellsInArea;
    }

    /// <summary>
    /// Get all S2 cells within geofence polygons
    /// </summary>
    /// <param name="context"></param>
    /// <param name="multiPolygons"></param>
    /// <returns></returns>
    private async Task<IEnumerable<Cell>> GetS2CellsAsync(IEnumerable<IMultiPolygon> multiPolygons)
    {
        var cells = multiPolygons
            .SelectMany(multiPolygon => GetS2CellsAsync(multiPolygon).Result)
            .ToList();
        //foreach (var multiPolygon in multiPolygons)
        //{
        //    var list = await GetS2CellsAsync(context, multiPolygon);
        //    cells.AddRange(list);
        //}
        return await Task.FromResult(cells);
    }

    #endregion
}