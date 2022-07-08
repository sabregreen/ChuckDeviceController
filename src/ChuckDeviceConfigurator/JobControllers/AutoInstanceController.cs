﻿namespace ChuckDeviceConfigurator.JobControllers
{
    using System.Collections.Concurrent;
    using System.Threading.Tasks;

    using Microsoft.EntityFrameworkCore;

    using ChuckDeviceConfigurator.Services.Jobs;
    using ChuckDeviceConfigurator.Services.Tasks;
    using ChuckDeviceController.Data;
    using ChuckDeviceController.Data.Contexts;
    using ChuckDeviceController.Data.Entities;
    using ChuckDeviceController.Extensions;
    using ChuckDeviceController.Geometry;
    using ChuckDeviceController.Geometry.Extensions;
    using ChuckDeviceController.Geometry.Models;

    public class PokestopWithMode
    {
        public Pokestop? Pokestop { get; set; }

        public bool IsAlternative { get; set; }
    }

    public class AutoInstanceController : IJobController
    {
        #region Constants

        private const uint OneHourS = 3600;
        private const uint OneDayS = OneHourS * 24;
        private const ushort MaxSpinAttempts = 5; // TODO: Make 'MaxSpinAttempts' configurable via Instance.Data
        private const uint DelayLogoutS = 900; // TODO: Make 'DelayLogout' configurable via Instance.Data
        private const ushort SpinRangeM = 80; // TODO: Revert back to 40m once reverted ingame
        private const ulong DefaultDistance = 10000000000000000;
        private const ushort CooldownLimitS = 7200; // Two hours
        private const uint SuspensionTimeLimitS = 2592000; // Account suspension time period

        #endregion

        #region Variables

        private readonly ILogger<AutoInstanceController> _logger;
        private readonly IDbContextFactory<MapDataContext> _mapFactory;
        private readonly IDbContextFactory<DeviceControllerContext> _deviceFactory;

        private readonly List<PokestopWithMode> _allStops;
        private readonly List<PokestopWithMode> _todayStops;
        private readonly Dictionary<PokestopWithMode, byte> _todayStopsAttempts;
        private List<ulong> _bootstrapCellIds;
        private readonly System.Timers.Timer _timer;
        private int _bootstrapTotalCount = 0;
        private ulong _completionDate = 0;
        private ulong _lastCompletionCheck = DateTime.UtcNow.ToTotalSeconds() - OneHourS;
        private bool _shouldExit;

        private readonly Dictionary<string, string> _accounts;
        private readonly Dictionary<string, bool> _lastMode;

        #endregion

        #region Properties

        public string Name { get; }

        public IReadOnlyList<MultiPolygon> MultiPolygon { get; }

        public ushort MinimumLevel { get; }

        public ushort MaximumLevel { get; }

        public string GroupName { get; }

        public bool IsEvent { get; }

        public short TimeZoneOffset { get; }

        public AutoInstanceType Type { get; }

        public uint SpinLimit { get; }

        public bool IgnoreS2CellBootstrap { get; }

        public bool RequireAccountEnabled { get; set; } // TODO: Make 'RequireAccountEnabled' configurable via Instance.Data

        public bool UseWarningAccounts { get; set; }

        public QuestMode QuestMode { get; }

        #endregion

        #region Events

        public event EventHandler<AutoInstanceCompleteEventArgs> InstanceComplete;
        private void OnInstanceComplete(string instanceName, ulong completionTimestamp)
        {
            InstanceComplete?.Invoke(this, new AutoInstanceCompleteEventArgs(instanceName, completionTimestamp));
        }

        #endregion

        #region Constructor

        public AutoInstanceController(
            IDbContextFactory<MapDataContext> mapFactory,
            IDbContextFactory<DeviceControllerContext> deviceFactory,
            Instance instance,
            List<MultiPolygon> multiPolygon,
            short timezoneOffset)
        {
            Name = instance.Name;
            MultiPolygon = multiPolygon;
            MinimumLevel = instance.MinimumLevel;
            MaximumLevel = instance.MaximumLevel;
            GroupName = instance.Data?.AccountGroup ?? null;
            IsEvent = instance.Data?.IsEvent ?? false;
            SpinLimit = instance.Data?.SpinLimit ?? 1000;
            Type = AutoInstanceType.Quest;
            IgnoreS2CellBootstrap = instance.Data?.IgnoreS2CellBootstrap ?? false;
            TimeZoneOffset = timezoneOffset;
            UseWarningAccounts = instance.Data?.UseWarningAccounts ?? false;
            /*
            QuestMode = instance.Data?.QuestMode == "normal"
                ? QuestMode.Normal
                : instance.Data?.QuestMode == "alternative"
                    ? QuestMode.Alternative
                    : QuestMode.Both;
            */
            QuestMode = instance.Data?.QuestMode ?? QuestMode.Normal;
            QuestMode = QuestMode.Alternative; // TODO: Dev

            _logger = new Logger<AutoInstanceController>(LoggerFactory.Create(x => x.AddConsole()));
            _mapFactory = mapFactory;
            _deviceFactory = deviceFactory;

            _allStops = new List<PokestopWithMode>();
            _todayStops = new List<PokestopWithMode>();
            _todayStopsAttempts = new Dictionary<PokestopWithMode, byte>();
            _bootstrapCellIds = new List<ulong>();

            _accounts = new Dictionary<string, string>();
            _lastMode = new Dictionary<string, bool>();

            var (localTime, timeLeft) = GetSecondsUntilMidnight();
            _timer = new System.Timers.Timer(timeLeft * 1000);
            _timer.Elapsed += async (sender, e) => await ClearQuestsAsync();
            _timer.Start();

            _logger.LogInformation($"[{Name}] Clearing Quests in {timeLeft:N0}s at 12:00 AM (Currently: {localTime})");

            UpdateAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            if (!IgnoreS2CellBootstrap)
            {
                BootstrapAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            }
        }

        #endregion

        #region Public Methods

        public async Task<ITask> GetTaskAsync(string uuid, string? accountUsername = null, Account? account = null, bool isStartup = false)
        {
            switch (Type)
            {
                case AutoInstanceType.Quest:
                    if (_bootstrapCellIds.Count > 0)
                    {
                        var bootstrapTask = await GetBootstrapTaskAsync();
                        return bootstrapTask;
                    }

                    if (string.IsNullOrEmpty(accountUsername) && RequireAccountEnabled)
                    {
                        _logger.LogWarning($"[{Name}] No username specified for device '{uuid}', ignoring...");
                        return null;
                    }

                    if (account == null && RequireAccountEnabled)
                    {
                        _logger.LogWarning($"[{Name}] No account specified for device '{uuid}', ignoring...");
                        return null;
                    }

                    // TODO: Lock _todayStops
                    if (_allStops.Count == 0)
                    {
                        return null;
                    }

                    await CheckIfCompletedAsync(uuid);

                    PokestopWithMode? pokestop = null;
                    Coordinate? lastCoord = null;
                    try
                    {
                        lastCoord = Cooldown.GetLastLocation(account, uuid);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"[{Name}] Failed to get last location for device '{uuid}'");
                        return null;
                    }

                    if (lastCoord != null)
                    {
                        var (closest, mode) = GetNextClosestPokestop(lastCoord, accountUsername ?? uuid);
                        if (closest == null)
                        {
                            return null;
                        }

                        if ((mode ?? false) && !(closest?.IsAlternative ?? false))
                        {
                            _logger.LogDebug($"[{Name}] [{accountUsername ?? "?"}] Switching quest mode from {((mode ?? false) ? "alternative" : "none")} to normal.");
                            PokestopWithMode? closestAr = null;
                            double closestArDistance = DefaultDistance;
                            var arStops = _allStops.Where(x => x.Pokestop.IsArScanEligible)
                                                   .ToList();

                            foreach (var stop in arStops)
                            {
                                var coord = new Coordinate(stop.Pokestop.Latitude, stop.Pokestop.Longitude);
                                var dist = lastCoord.DistanceTo(coord);
                                if (dist < closestArDistance)
                                {
                                    closestAr = stop;
                                    closestArDistance = dist;
                                }
                            }

                            if (closestAr != null && closestAr?.Pokestop != null)
                            {
                                closestAr.IsAlternative = closest?.IsAlternative ?? false;
                                closest = closestAr;
                                _logger.LogDebug($"[{Name}] [{accountUsername ?? "?"}] Scanning AR eligible Pokestop {closest?.Pokestop?.Id}");
                            }
                            else
                            {
                                _logger.LogDebug($"[{Name}] [{accountUsername ?? "?"}] No AR eligible Pokestop found to scan");
                            }
                        }

                        pokestop = closest;

                        var nearbyStops = new List<PokestopWithMode> { pokestop };
                        var pokestopCoord = new Coordinate(pokestop.Pokestop.Latitude, pokestop.Pokestop.Longitude);
                        var todayStopsC = _todayStops;
                        foreach (var stop in todayStopsC)
                        {
                            var distance = pokestopCoord.DistanceTo(new Coordinate(stop.Pokestop.Latitude, stop.Pokestop.Longitude));
                            if (pokestop.IsAlternative == stop.IsAlternative && distance <= SpinRangeM)
                            {
                                nearbyStops.Add(stop);
                            }
                        }

                        // TODO: Lock nearbyStops
                        foreach (var stop in nearbyStops)
                        {
                            var index = _todayStops.IndexOf(stop);
                            if (index > -1)
                            {
                                _todayStops.RemoveAt(index);
                            }
                        }
                    }
                    else
                    {
                        // TODO: Lock _todayStops
                        PokestopWithMode? stop = _todayStops.FirstOrDefault();
                        if (stop == null)
                        {
                            return null;
                        }

                        pokestop = stop;
                        _todayStops.RemoveAt(0);
                    }

                    double delay;
                    ulong encounterTime;
                    try
                    {
                        var result = Cooldown.SetCooldownAsync(
                            account,
                            new Coordinate(pokestop.Pokestop.Latitude, pokestop.Pokestop.Longitude)
                        );
                        delay = result.Delay;
                        encounterTime = result.EncounterTime;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"[{Name}] [{uuid}] Failed to calculate cooldown time for device");
                        // TODO: Lock _todayStops
                        _todayStops.Add(pokestop);
                        return null;
                    }

                    if (delay >= DelayLogoutS && account != null)
                    {
                        // TODO: Lock _todayStops
                        _todayStops.Add(pokestop);
                        // TODO: Lock _accounts
                        string newUsername;
                        try
                        {
                            var newAccount = await GetAccountAsync(
                                uuid,
                                new Coordinate(pokestop.Pokestop.Latitude, pokestop.Pokestop.Longitude)
                            );
                            if (!_accounts.ContainsKey(uuid))
                            {
                                newUsername = newAccount?.Username;
                                _accounts.Add(uuid, newAccount?.Username);
                                _logger.LogDebug($"[{Name}] [{uuid}] Over logout delay. Switching account from {accountUsername ?? "?"} to {newUsername ?? "?"}");
                            }
                            else
                            {
                                newUsername = _accounts[uuid];
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"[{Name}] [{uuid}] Failed to get account for device in advance");
                        }

                        return new SwitchAccountTask
                        {
                            Area = Name,
                            Action = DeviceActionType.SwitchAccount,
                            MinimumLevel = MinimumLevel,
                            MaximumLevel = MaximumLevel,
                        };
                    }
                    else if (delay >= DelayLogoutS)
                    {
                        _logger.LogWarning($"[{Name}] [{uuid}] Ignoring over logout delay, because no account is specified");
                    }

                    try
                    {
                        if (!string.IsNullOrEmpty(accountUsername))
                        {
                            // Increment account spin count
                            await Cooldown.SetSpinCountAsync(_deviceFactory, accountUsername);
                        }

                        await Cooldown.SetEncounterAsync(
                            _deviceFactory,
                            account,
                            new Coordinate(pokestop.Pokestop.Latitude, pokestop.Pokestop.Longitude),
                            encounterTime
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"[{Name}] [{uuid}] Failed to store cooldown: {ex}");
                        // TODO: Lock _todayStops
                        _todayStops.Add(pokestop);
                        return null;
                    }

                    // TODO: Lock _todayStopsAttempts
                    IncrementSpinAttempt(pokestop);

                    await CheckIfCompletedAsync(uuid);

                    // TODO: Lock _lastMode
                    var modeKey = accountUsername ?? uuid;
                    if (!_lastMode.ContainsKey(modeKey))
                    {
                        _lastMode.Add(modeKey, pokestop.IsAlternative);
                    }
                    else
                    {
                        _lastMode[modeKey] = pokestop.IsAlternative;
                    }

                    // TODO: setArQuestTarget(uuid, timestamp, pokestop.IsAlternative);
                    var task = GetQuestTask(pokestop, delay);
                    return task;
            }
            return null;
        }

        public async Task<string> GetStatusAsync()
        {
            switch (Type)
            {
                case AutoInstanceType.Quest:
                    // TODO: Lock _boostrapCellIds
                    if (_bootstrapCellIds.Count > 0)
                    {
                        var totalCount = _bootstrapTotalCount;
                        var count = totalCount - _bootstrapCellIds.Count;
                        var percentage = totalCount > 0
                            ? Convert.ToDouble((double)count / (double)totalCount) * 100
                            : 100;

                        var bootstrapStatus = $"Bootstrapping {count:N0}/{totalCount:N0} ({Math.Round(percentage, 1)}%)";
                        return bootstrapStatus;
                    }

                    // TODO: Lock _allStops
                    var ids = _allStops.Select(x => x.Pokestop.Id).ToList();
                    var currentCountDb = await GetPokestopQuestCountAsync(ids, QuestMode);
                    // TODO: Lock _allStops
                    var maxCount = _allStops.Count;
                    var currentCount = maxCount - _todayStops.Count;

                    var percent = maxCount > 0
                        ? Convert.ToDouble((double)currentCount / (double)maxCount) * 100
                        : 100;
                    var percentReal = maxCount > 0
                        ? Convert.ToDouble((double)currentCountDb / (double)maxCount) * 100
                        : 100;

                    var completedDate = _completionDate.FromSeconds();
                    var status = $"Status: {currentCountDb:N0}|{currentCount:N0}/{maxCount:N0} " +
                        $"({Math.Round(percentReal, 1)}|" +
                        $"{Math.Round(percent, 1)}%)" +
                        $"{(_completionDate != default ? $", Completed @ {completedDate}" : "")}";
                    return status;
            }
            return null;
        }

        public void Reload()
        {
            UpdateAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public void Stop()
        {
            _shouldExit = true;
            _timer.Stop();
        }

        #endregion

        #region Private Methods

        private async Task BootstrapAsync()
        {
            _logger.LogInformation($"[{Name}] Checking Bootstrap Status...");

            var start = DateTime.UtcNow;
            var total = 0;
            var missingCellIds = new List<ulong>();
            var allCellIds = new List<ulong>();

            // Get all known cells from the database
            var existingCells = await GetS2CellsAsync();
            var existingCellIds = existingCells.Select(cell => cell.Id).ToList();

            // Loop through all geofences and get s2cells within each geofence
            foreach (var polygon in MultiPolygon)
            {
                // Get maximum amount of S2 level 15 cells within this geofence
                var s2Cells = polygon.GetS2CellIds(15, int.MaxValue);
                var s2CellIds = s2Cells.Select(cell => cell.Id).ToList();
                total += s2CellIds.Count;
                allCellIds.AddRange(s2CellIds);
            }

            // Loop through all S2Cells within the geofence and filter any missing
            foreach (var s2CellId in allCellIds)
            {
                // Check if we don't already have the S2Cell in the database
                if (!existingCellIds.Contains(s2CellId))
                {
                    missingCellIds.Add(s2CellId);
                }
            }

            var found = total - missingCellIds.Count;
            var time = DateTime.UtcNow.Subtract(start).TotalSeconds;
            _logger.LogInformation($"[{Name}] Bootstrap Status: {found:N0}/{total:N0} after {time:N0} seconds");

            // TODO: Lock _bootstrapCellIds
            _bootstrapCellIds = missingCellIds.Distinct().ToList();
            _bootstrapTotalCount = total;
        }

        private async Task UpdateAsync()
        {
            switch (Type)
            {
                case AutoInstanceType.Quest:
                    _allStops.Clear();
                    foreach (var polygon in MultiPolygon)
                    {
                        try
                        {
                            var bbox = polygon.GetBoundingBox();
                            var stops = await GetPokestopsInBoundsAsync(bbox);
                            foreach (var stop in stops)
                            {
                                if (!GeofenceService.InPolygon(polygon, stop.Latitude, stop.Longitude))
                                    continue;

                                if (QuestMode == QuestMode.Normal || QuestMode == QuestMode.Both)
                                {
                                    _allStops.Add(new PokestopWithMode
                                    {
                                        Pokestop = stop,
                                        IsAlternative = false,
                                    });
                                }
                                if (QuestMode == QuestMode.Alternative || QuestMode == QuestMode.Both)
                                {
                                    _allStops.Add(new PokestopWithMode
                                    {
                                        Pokestop = stop,
                                        IsAlternative = true,
                                    });
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
                    // TODO: Check sorting
                    /*
                    _allStops.Sort((a, b) =>
                    {
                        var coordA = new Coordinate(a.Pokestop.Latitude, a.Pokestop.Longitude);
                        var coordB = new Coordinate(b.Pokestop.Latitude, b.Pokestop.Longitude);
                        var distanceA = coordA.DistanceTo(coordB);
                        var distanceB = coordB.DistanceTo(coordA);
                        var distance = Convert.ToInt32(((distanceA + distanceB) * 100) / 2);
                        return distance;
                    });
                    */
                    
                    foreach (var stop in _allStops)
                    {
                        // Check that the Pokestop does not have quests already found and that it is enabled
                        if (stop.Pokestop.Enabled &&
                            (!stop.IsAlternative && stop.Pokestop.QuestType == null) ||
                            (stop.IsAlternative && stop.Pokestop.AlternativeQuestType == null))
                        {
                            // Add Pokestop if it's not already in the list
                            if (!_todayStops.Contains(stop))
                            {
                                _todayStops.Add(stop);
                            }
                        }
                    }
                    break;
            }
        }

        private async Task ClearQuestsAsync()
        {
            _timer.Stop();
            var (localTime, timeLeft) = GetSecondsUntilMidnight();
            var now = localTime.ToTotalSeconds();
            // Timer interval cannot be set to 0, calculate one full day
            // in seconds to use for the next quest clearing interval.
            _timer.Interval = (timeLeft == 0 ? OneDayS : timeLeft) * 1000;
            _timer.Start();

            if (_shouldExit)
                return;

            if (_allStops.Count == 0)
            {
                _logger.LogWarning($"[{Name}] Tried clearing quests but no pokestops.");
                return;
            }

            // Clear quests
            _logger.LogInformation($"[{Name}] Clearing Pokestop Quests...");
            var pokestopIds = _allStops.Select(stop => stop.Pokestop.Id).ToList();
            using (var context = _mapFactory.CreateDbContext())
            {
                var pokestops = context.Pokestops.Where(pokestop => pokestopIds.Contains(pokestop.Id))
                                                 .ToList();
                pokestops.ForEach(pokestop =>
                {
                    pokestop.QuestConditions = null;
                    pokestop.QuestRewards = null;
                    pokestop.QuestTarget = null;
                    pokestop.QuestTemplate = null;
                    pokestop.QuestTimestamp = null;
                    pokestop.QuestTitle = null;
                    pokestop.QuestType = null;

                    pokestop.AlternativeQuestConditions = null;
                    pokestop.AlternativeQuestRewards = null;
                    pokestop.AlternativeQuestTarget = null;
                    pokestop.AlternativeQuestTemplate = null;
                    pokestop.AlternativeQuestTimestamp = null;
                    pokestop.AlternativeQuestTitle = null;
                    pokestop.AlternativeQuestType = null;
                    context.Update(pokestop);
                });
                await context.SaveChangesAsync();
            }

            await UpdateAsync();
        }

        private async Task<BootstrapTask> GetBootstrapTaskAsync()
        {
            var target = _bootstrapCellIds.LastOrDefault();
            if (target == default)
            {
                return null;
            }

            _bootstrapCellIds.RemoveAt(_bootstrapCellIds.Count - 1);

            var center = target.S2LatLngFromId();
            var coord = new Coordinate(center.LatDegrees, center.LngDegrees);
            var cellIds = center.GetLoadedS2CellIds();

            // TODO: Lock _bootstrapCellIds
            foreach (var cellId in cellIds)
            {
                var index = _bootstrapCellIds.IndexOf(cellId.Id);
                if (index > 0)
                {
                    _bootstrapCellIds.RemoveAt(index);
                }
            }

            if (_bootstrapCellIds.Count == 0)
            {
                // TODO: Lock _bootstrapCellIds
                await BootstrapAsync();
                if (_bootstrapCellIds.Count == 0)
                {
                    await UpdateAsync();
                }
            }

            return new BootstrapTask
            {
                Area = Name,
                Action = DeviceActionType.ScanRaid,
                Latitude = coord.Latitude,
                Longitude = coord.Longitude,
                MinimumLevel = MinimumLevel,
                MaximumLevel = MaximumLevel,
            };
        }

        private QuestTask GetQuestTask(PokestopWithMode pokestop, double delay = 0)
        {
            return new QuestTask
            {
                Area = Name,
                Action = DeviceActionType.ScanQuest,
                DeployEgg = false,
                Latitude = pokestop?.Pokestop?.Latitude ?? 0,
                Longitude = pokestop?.Pokestop?.Longitude ?? 0,
                Delay = delay,
                MinimumLevel = MinimumLevel,
                MaximumLevel = MaximumLevel,
                QuestType = (pokestop?.IsAlternative ?? false)
                    ? "ar"
                    : "normal",
            };
        }

        private (PokestopWithMode?, bool?) GetNextClosestPokestop(Coordinate lastCoord, string modeKey)
        {
            PokestopWithMode? closestOverall = null;
            double closestOverallDistance = DefaultDistance;

            PokestopWithMode? closestNormal = null;
            double closestNormalDistance = DefaultDistance;

            PokestopWithMode? closestAlternative = null;
            double closestAlternativeDistance = DefaultDistance;

            // TODO: Lock _todayStops
            var todayStopsC = _todayStops;
            if (todayStopsC.Count == 0)
            {
                return (null, null);
            }

            foreach (var stop in todayStopsC)
            {
                var coord = new Coordinate(stop.Pokestop.Latitude, stop.Pokestop.Longitude);
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
            // TODO: Lock _lastMode
            var key = modeKey;
            bool? mode = _lastMode.ContainsKey(key)
                ? _lastMode[key]
                : null;
            if (mode == null)
            {
                closest = closestOverall;
            }
            else if (_lastMode.ContainsKey(key) && !_lastMode[key])
            {
                closest = closestNormal ?? closestOverall;
            }
            else
            {
                closest = closestAlternative ?? closestOverall;
            }

            return (closest, mode);
        }

        private async Task CheckIfCompletedAsync(string uuid)
        {
            if (_todayStops.Count > 0)
            {
                return;
            }

            var now = DateTime.UtcNow.ToTotalSeconds();
            if (!(_lastCompletionCheck >= 600))
            {
                if (_completionDate == 0)
                {
                    _completionDate = now;
                }
                OnInstanceComplete(Name, _completionDate);
                return;
            }

            _lastCompletionCheck = now;
            var ids = _allStops.Select(x => x.Pokestop.Id).ToList();

            var newStops = new List<Pokestop>();
            try
            {
                // Get Pokestops within S2 cells
                newStops = await GetPokestopsByIdsAsync(ids);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[{Name}] [{uuid}] Failed to get today stops: {ex}");
                return;
            }

            // TODO: Lock newStops
            foreach (var stop in newStops)
            {
                var isNormal = QuestMode == QuestMode.Normal || QuestMode == QuestMode.Both;
                var isAlternative = QuestMode == QuestMode.Alternative || QuestMode == QuestMode.Both;
                if (isNormal || isAlternative)
                {
                    var pokestopWithMode = new PokestopWithMode
                    {
                        Pokestop = stop,
                        IsAlternative = isAlternative,
                    };
                    var count = _todayStopsAttempts.ContainsKey(pokestopWithMode)
                        ? _todayStopsAttempts[pokestopWithMode]
                        : 0;
                    if (stop.Enabled && count <= MaxSpinAttempts &&
                        (
                            stop.QuestType == null ||
                            stop.AlternativeQuestType == null
                        )
                    )
                    {
                        _todayStops.Add(pokestopWithMode);
                    }
                }
                /*
                if (QuestMode == QuestMode.Normal || QuestMode == QuestMode.Both)
                {
                    var pokestopWithMode = new PokestopWithMode
                    {
                        Pokestop = stop,
                        IsAlternative = false,
                    };
                    var count = _todayStopsAttempts.ContainsKey(pokestopWithMode)
                        ? _todayStopsAttempts[pokestopWithMode]
                        : 0;
                    if (stop.QuestType == null && stop.Enabled && count <= MaxSpinAttempts)
                    {
                        _todayStops.Add(pokestopWithMode);
                    }
                }

                if (QuestMode == QuestMode.Alternative || QuestMode == QuestMode.Both)
                {
                    var pokestopWithMode = new PokestopWithMode
                    {
                        Pokestop = stop,
                        IsAlternative = true,
                    };
                    var count = _todayStopsAttempts.ContainsKey(pokestopWithMode)
                        ? _todayStopsAttempts[pokestopWithMode]
                        : 0;
                    if (stop.AlternativeQuestType == null && stop.Enabled && count <= MaxSpinAttempts)
                    {
                        _todayStops.Add(pokestopWithMode);
                    }
                }
                */
            }

            if (_todayStops.Count == 0)
            {
                _logger.LogInformation($"[{Name}] [{uuid}] Quest instance complete");
                if (_completionDate == 0)
                {
                    _completionDate = now;
                }

                // Call OnInstanceComplete event
                OnInstanceComplete(Name, now);
            }
        }

        private void IncrementSpinAttempt(PokestopWithMode pokestop)
        {
            if (_todayStopsAttempts.ContainsKey(pokestop))
            {
                var tries = _todayStopsAttempts[pokestop];
                _todayStopsAttempts[pokestop] = Convert.ToByte(tries == byte.MaxValue ? 10 : tries + 1);
            }
            else
            {
                _todayStopsAttempts.Add(pokestop, 1);
            }
        }

        private (DateTime, double) GetSecondsUntilMidnight()
        {
            var localTime = DateTime.UtcNow.AddHours(TimeZoneOffset);
            var timeLeft = DateTime.Today.AddDays(1).Subtract(localTime).TotalSeconds;
            var seconds = Math.Round(timeLeft);
            return (localTime, seconds);
        }

        #endregion

        #region Database Helpers

        #region Account

        private async Task<Account> GetAccountAsync(string uuid, Coordinate encounterTarget)
        {
            if (_accounts.ContainsKey(uuid))
            {
                var username = _accounts[uuid];
                _accounts.Remove(uuid);
                return await GetAccountAsync(username);
            }

            var account = await GetNewAccountAsync(
                MinimumLevel,
                MaximumLevel,
                UseWarningAccounts,
                SpinLimit,
                noCooldown: true,
                GroupName
            );
            return account;
        }

        private async Task<Account> GetAccountAsync(string username)
        {
            using (var context = _deviceFactory.CreateDbContext())
            {
                var account = await context.Accounts.FindAsync(username);
                return account;
            }
        }

        private async Task<Account> GetNewAccountAsync(ushort minLevel = 0, ushort maxLevel = 35, bool ignoreWarning = false, uint spins = 3500, bool noCooldown = true, string? group = null)
        {
            var now = DateTime.UtcNow.ToTotalSeconds();

            using (var context = _deviceFactory.CreateDbContext())
            {
                var account = context.Accounts.FirstOrDefault(a =>
                    a.Level >= minLevel &&
                    a.Level <= maxLevel &&
                    a.Spins < spins &&
                    !string.IsNullOrEmpty(group)
                        ? a.GroupName == group
                        : string.IsNullOrEmpty(a.GroupName) &&
                    noCooldown
                        ? (a.LastEncounterTime == null || now - a.LastEncounterTime >= CooldownLimitS)
                        : (a.LastEncounterTime == null || a.LastEncounterTime != null) &&
                    ignoreWarning
                        ? string.IsNullOrEmpty(a.Failed) || a.Failed == "GPR_RED_WARNING"
                        : (a.Failed == null && a.FirstWarningTimestamp == null) ||
                          (a.Failed == "GPR_RED_WARNING" && a.WarnExpireTimestamp != null &&
                           a.WarnExpireTimestamp != 0 && a.WarnExpireTimestamp <= now) ||
                          (a.Failed == "suspended" && a.FailedTimestamp <= now - SuspensionTimeLimitS)
                );
                return await Task.FromResult(account);
            }
        }

        #endregion

        #region Pokestops

        private async Task<List<Pokestop>> GetPokestopsByIdsAsync(List<string> pokestopIds)
        {
            if (pokestopIds.Count > 10000)
            {
                var list = new List<Pokestop>();
                var count = Convert.ToInt64(Math.Ceiling(Convert.ToDouble(pokestopIds.Count) / 10000.0));
                for (var i = 0; i < count; i++)
                {
                    var start = 10000 * i;
                    var end = Math.Min(10000 * (i + 1) - 1, pokestopIds.Count - 1);
                    var splice = pokestopIds.GetRange(start, end);
                    var spliceResult = await GetPokestopsByIdsAsync(splice);
                    if (spliceResult != null)
                    {
                        list.AddRange(spliceResult);
                    }
                }
                return list;
            }

            if (pokestopIds.Count == 0)
            {
                return new List<Pokestop>();
            }

            using (var context = _mapFactory.CreateDbContext())
            {
                var pokestops = context.Pokestops.Where(stop => pokestopIds.Contains(stop.Id)).ToList();
                return pokestops;
            }
            //return new List<Pokestop>();
        }

        private async Task<List<Pokestop>> GetPokestopsInBoundsAsync(BoundingBox bbox)
        {
            using (var context = _mapFactory.CreateDbContext())
            {
                var pokestops = context.Pokestops.Where(stop =>
                    stop.Latitude >= bbox.MinimumLatitude &&
                    stop.Latitude <= bbox.MaximumLatitude &&
                    stop.Longitude >= bbox.MinimumLongitude &&
                    stop.Longitude <= bbox.MaximumLongitude &&
                    !stop.Deleted
                ).ToList();
                return await Task.FromResult(pokestops);
            }
        }

        private async Task<ulong> GetPokestopQuestCountAsync(List<string> pokestopIds, QuestMode mode)
        {
            if (pokestopIds.Count > 10000)
            {
                var result = 0ul;
                var count = Convert.ToInt64(Math.Ceiling(Convert.ToDouble(pokestopIds.Count) / 10000.0));
                for (var i = 0; i < count; i++)
                {
                    var start = 10000 * i;
                    var end = Math.Min(10000 * (i + 1) - 1, pokestopIds.Count - 1);
                    var splice = pokestopIds.GetRange(start, end);
                    var spliceResult = await GetPokestopQuestCountAsync(splice, mode);
                    result += spliceResult;
                }
                return result;
            }

            using (var context = _mapFactory.CreateDbContext())
            {
                var count = context.Pokestops.Count(stop =>
                    pokestopIds.Contains(stop.Id) &&
                    mode == QuestMode.Normal
                        ? stop.QuestType != null
                        : mode == QuestMode.Alternative
                            ? stop.AlternativeQuestType != null
                            : stop.QuestType != null || stop.AlternativeQuestType != null
                );
                return (ulong)count;
            }
        }

        #endregion

        private async Task<List<Cell>> GetS2CellsAsync()
        {
            using (var context = _mapFactory.CreateDbContext())
            {
                var cells = context.Cells.ToList();
                return await Task.FromResult(cells);
            }
        }

        #endregion
    }

    public sealed class AutoInstanceCompleteEventArgs : EventArgs
    {
        public string InstanceName { get; }

        public ulong CompletionTimestamp { get; }

        public AutoInstanceCompleteEventArgs(string instanceName, ulong completionTimestamp)
        {
            InstanceName = instanceName;
            CompletionTimestamp = completionTimestamp;
        }
    }

    public class Cooldown
    {
        public static double GetCooldownAmount(double distanceM)
        {
            return Math.Min(Convert.ToInt32(distanceM / 9.8), 7200);
        }

        public static Coordinate GetLastLocation(Account account, string uuid)
        {
            double? lat = null;
            double? lon = null;
            if (account != null)
            {
                lat = account.LastEncounterLatitude;
                lon = account.LastEncounterLongitude;
            }
            if (lat == null || lon == null)
            {
                return null;
            }
            return new Coordinate(lat.Value, lon.Value);
        }

        public static CooldownResult SetCooldownAsync(Account account, Coordinate location)
        {
            double? lastLat = null;
            double? lastLon = null;
            ulong? lastEncounterTime = null;
            if (account != null)
            {
                lastLat = account.LastEncounterLatitude;
                lastLon = account.LastEncounterLongitude;
                lastEncounterTime = account.LastEncounterTime;
            }

            double delay;
            ulong encounterTime;
            var now = DateTime.UtcNow.ToTotalSeconds();

            if (lastLat == null || lastLon == null || lastEncounterTime == null)
            {
                delay = 0;
                encounterTime = now;
            }
            else
            {
                var lastCoord = new Coordinate(lastLat ?? 0, lastLon ?? 0);
                var distance = lastCoord.DistanceTo(location);
                var cooldownTime = Convert.ToUInt64(lastEncounterTime + GetCooldownAmount(distance));
                encounterTime = cooldownTime < now
                    ? now
                    : cooldownTime;
                delay = encounterTime - now;
            }
            return new CooldownResult(delay, encounterTime);
        }

        public static async Task SetEncounterAsync(IDbContextFactory<DeviceControllerContext> factory, Account account, Coordinate location, ulong encounterTime)
        {
            if (account == null)
            {
                Console.WriteLine($"Failed to set account last encounter info, account was null");
                return;
            }

            using (var context = factory.CreateDbContext())
            {
                context.Attach(account);
                account.LastEncounterLatitude = location.Latitude;
                account.LastEncounterLongitude = location.Longitude;
                account.LastEncounterTime = encounterTime;
                context.Entry(account).Property(p => p.LastEncounterLatitude).IsModified = true;
                context.Entry(account).Property(p => p.LastEncounterLongitude).IsModified = true;
                context.Entry(account).Property(p => p.LastEncounterTime).IsModified = true;

                await context.SaveChangesAsync();
            }
        }

        public static async Task SetSpinCountAsync(IDbContextFactory<DeviceControllerContext> factory, string accountUsername)
        {
            if (string.IsNullOrEmpty(accountUsername))
            {
                Console.WriteLine($"Failed to set account spin count, account username was null");
                return;
            }

            using (var context = factory.CreateDbContext())
            {
                var account = await context.Accounts.FindAsync(accountUsername);
                if (account == null)
                {
                    Console.WriteLine($"Failed to increase account spin count, unable to retrieve account");
                    return;
                }

                context.Attach(account);
                account.Spins++;
                context.Entry(account).Property(p => p.Spins).IsModified = true;
                await context.SaveChangesAsync();
            }
        }
    }

    public class CooldownResult
    {
        public double Delay { get; set; }

        public ulong EncounterTime { get; set; }

        public CooldownResult(double delay, ulong encounterTime)
        {
            Delay = delay;
            EncounterTime = encounterTime;
        }
    }
}