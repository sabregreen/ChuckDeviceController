﻿namespace ChuckDeviceController.JobControllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Logging;

    using Chuck.Common;
    using Chuck.Common.JobControllers;
    using Chuck.Data.Contexts;
    using Chuck.Data.Entities;
    using Chuck.Data.Factories;
    using Chuck.Data.Repositories;
    using Chuck.Geometry.Geofence.Models;
    using ChuckDeviceController.JobControllers.Instances;
    using ChuckDeviceController.Services;

    public interface IInstanceController
    {
        Task Start();

        #region Instances

        IJobController GetInstanceController(string uuid);

        Task<string> GetInstanceStatus(Instance instance);

        Task AddInstance(Instance instance);

        Task ReloadInstance(Instance newInstance, string oldInstanceName);

        void ReloadAll();

        Task RemoveInstance(string instanceName);

        #endregion

        #region Devices

        void AddDevice(Device device);

        Task RemoveDevice(Device device);

        void RemoveDevice(string uuid);

        void ReloadDevice(Device newDevice, string oldDeviceUuid);

        List<string> GetDeviceUuidsInInstance(string instanceName);

        #endregion

        List<Pokemon> GetIVQueue(string name);

        void GotPokemon(Pokemon pokemon); // TODO: Rename to ReceivedPokemon

        void GotIV(Pokemon pokemon); // TODO: Rename to ReceivedPokemonIV
    }


    public class InstanceController : IInstanceController
    {
        #region Variables

        private readonly DeviceControllerContext _context;
        private readonly ILogger<InstanceController> _logger;
        private readonly IDictionary<string, Device> _devices;
        private readonly IDictionary<string, IJobController> _instances;
        private readonly DeviceRepository _deviceRepository;
        private readonly InstanceRepository _instanceRepository;
        private readonly IAssignmentController _assignmentController;

        private readonly object _instancesLock = new();
        private readonly object _devicesLock = new();

        #endregion

        /*
        #region Singleton

        private static InstanceController _instance;
        public static InstanceController Instance =>
            _instance ??= new InstanceController();

        #endregion
        */

        public IReadOnlyDictionary<string, IJobController> Instances =>
            (IReadOnlyDictionary<string, IJobController>)_instances;

        #region Constructor

        public InstanceController(
            DeviceControllerContext context,
            IAssignmentController assignmentController,
            ILogger<InstanceController> logger)
        {
            _context = context;
            _assignmentController = assignmentController;
            _devices = new Dictionary<string, Device>();
            //_deviceRepository = new DeviceRepository(DbContextFactory.CreateDeviceControllerContext(Startup.DbConfig.ToString()));
            //_instanceRepository = new InstanceRepository(DbContextFactory.CreateDeviceControllerContext(Startup.DbConfig.ToString()));
            _deviceRepository = new DeviceRepository(_context);
            _instanceRepository = new InstanceRepository(_context);

            _logger = logger;
            _logger.LogInformation("Starting instances...");
        }

        #endregion

        #region Public Methods

        public async Task Start()
        {
            var instances = await _instanceRepository.GetAllAsync().ConfigureAwait(false);
            var devices = await _deviceRepository.GetAllAsync().ConfigureAwait(false);
            foreach (var instance in instances)
            {
                if (!ThreadPool.QueueUserWorkItem(async _ =>
                {
                    _logger.LogInformation($"Starting {instance.Name}");
                    await AddInstance(instance).ConfigureAwait(false);
                    _logger.LogInformation($"Started {instance.Name}");
                    foreach (var device in devices.AsEnumerable().Where(d => string.Compare(d.InstanceName, instance.Name, true) == 0))
                    {
                        AddDevice(device);
                    }
                }))
                {
                    _logger.LogError($"Failed to start instance {instance.Name}");
                }
            }
            _logger.LogInformation("Done starting instances");
        }

        #region Instances

        public IJobController GetInstanceController(string uuid)
        {
            if (string.IsNullOrEmpty(uuid))
            {
                return null;
            }
            lock (_devicesLock)
            {
                if (!_devices.ContainsKey(uuid))
                {
                    _logger.LogWarning($"[{uuid}] Not assigned an instance!");
                    return null;
                }

                var device = _devices[uuid];
                var instanceName = device.InstanceName;
                if (device == null && string.IsNullOrEmpty(instanceName))
                    return null;

                return GetInstanceControllerByName(instanceName);
            }
        }

        public async Task<string> GetInstanceStatus(Instance instance)
        {
            if (!_instances.ContainsKey(instance.Name))
            {
                // Instance not started yet
                return "Starting...";
            }
            try
            {
                var instanceController = _instances[instance.Name];
                if (instanceController != null)
                {
                    return await instanceController.GetStatus().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get instance status for instance {instance.Name}: {ex}");
            }
            return "Error";
        }

        public async Task AddInstance(Instance instance)
        {
            IJobController instanceController = null;
            var geofences = GeofenceController.Instance.GetGeofences(instance.Geofences);
            if (geofences == null)
            {
                // Failed to get geofence, skip?
                _logger.LogError($"[{instance.Name}] Failed to get geofences for instance, make sure it is assign at least one");
                return;
            }
            switch (instance.Type)
            {
                case InstanceType.CirclePokemon:
                case InstanceType.CircleRaid:
                case InstanceType.SmartCircleRaid:
                    try
                    {
                        var coords = new List<Coordinate>();
                        foreach (var geofence in geofences)
                        {
                            var area = geofence?.Data?.Area;
                            if (area is null)
                            {
                                _logger.LogError($"[{instance.Name}] Failed to get teleport circles, skipping...");
                                continue;
                            }
                            var coordsArray = (List<Coordinate>)
                            (
                                area is List<Coordinate>
                                    ? area
                                    : JsonSerializer.Deserialize<List<Coordinate>>(Convert.ToString(area))
                            );
                            coords.AddRange(coordsArray);
                        }
                        var minLevel = instance.MinimumLevel;
                        var maxLevel = instance.MaximumLevel;
                        switch (instance.Type)
                        {
                            case InstanceType.CirclePokemon:
                                instanceController = new CircleInstanceController(instance.Name, coords, CircleType.Pokemon, instance.Data.CircleRouteType, minLevel, maxLevel, instance.Data.AccountGroup, instance.Data.IsEvent);
                                break;
                            case InstanceType.CircleRaid:
                                instanceController = new CircleInstanceController(instance.Name, coords, CircleType.Raid, CircleRouteType.Default, minLevel, maxLevel, instance.Data.AccountGroup, instance.Data.IsEvent);
                                break;
                            case InstanceType.SmartCircleRaid:
                                // TODO: SmartCircleRaidInstanceController
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"[{instance.Name}] Error: {ex}");
                    }
                    break;
                case InstanceType.AutoQuest:
                case InstanceType.PokemonIV:
                case InstanceType.Bootstrap:
                case InstanceType.FindTTH:
                    try
                    {
                        var multiPolygons = new List<MultiPolygon>();
                        var coordinates = new List<List<Coordinate>>();
                        foreach (var geofence in geofences)
                        {
                            var area = geofence?.Data?.Area;
                            if (area is null)
                            {
                                _logger.LogError($"[{instance.Name}] Failed to get geofence area, skipping...");
                                continue;
                            }
                            var coordsArray = (List<List<Coordinate>>)
                            (
                                area is List<List<Coordinate>>
                                    ? area
                                    : JsonSerializer.Deserialize<List<List<Coordinate>>>(json: Convert.ToString(area))
                            );
                            coordinates.AddRange(coordsArray);

                            var areaArrayEmptyInner = new List<MultiPolygon>();
                            foreach (var coords in coordsArray)
                            {
                                var multiPolygon = new MultiPolygon();
                                Coordinate first = null;
                                for (var i = 0; i < coords.Count; i++)
                                {
                                    var coord = coords[i];
                                    if (i == 0)
                                    {
                                        first = coord;
                                    }
                                    multiPolygon.Add(new Polygon(coord.Latitude, coord.Longitude));
                                }
                                if (first != null)
                                {
                                    multiPolygon.Add(new Polygon(first.Latitude, first.Longitude));
                                }
                                areaArrayEmptyInner.Add(multiPolygon);
                            }
                            multiPolygons.AddRange(areaArrayEmptyInner);
                        }
                        var minLevel = instance.MinimumLevel;
                        var maxLevel = instance.MaximumLevel;
                        switch (instance.Type)
                        {
                            case InstanceType.AutoQuest:
                                var timezone = instance.Data.Timezone;
                                var timezoneOffset = 0;
                                if (!string.IsNullOrEmpty(timezone))
                                {
                                    var tz = TimeZoneService.Instance.Timezones.ContainsKey(timezone) ? TimeZoneService.Instance.Timezones[timezone] : null;
                                    if (tz != null)
                                    {
                                        var tzData = TimeZoneService.Instance.Timezones[timezone];
                                        timezoneOffset = instance.Data.EnableDst
                                            ? tzData.Dst * 3600
                                            : tzData.Utc * 3600;
                                    }
                                }
                                var spinLimit = instance.Data.SpinLimit ?? 3500;
                                var retryLimit = instance.Data.QuestRetryLimit ?? 5;
                                var ignoreBootstrap = instance.Data.IgnoreS2CellBootstrap;
                                instanceController = new AutoInstanceController(instance.Name, multiPolygons, AutoType.Quest, timezoneOffset, minLevel, maxLevel, spinLimit, retryLimit, ignoreBootstrap, instance.Data.AccountGroup, instance.Data.IsEvent);
                                ((AutoInstanceController)instanceController).InstanceComplete += (sender, e) => _assignmentController.InstanceControllerDone(e.InstanceName);
                                break;
                            case InstanceType.PokemonIV:
                                var ivList = IVListController.Instance.GetIVList(instance.Data.IVList)?.PokemonIDs ?? new List<uint>();
                                var ivQueueLimit = instance.Data.IVQueueLimit ?? 100;
                                instanceController = new IVInstanceController(instance.Name, multiPolygons, ivList, minLevel, maxLevel, ivQueueLimit, instance.Data.AccountGroup, instance.Data.IsEvent);
                                break;
                            case InstanceType.Bootstrap:
                                var circleSize = instance.Data.CircleSize ?? 70;
                                var fastBootstrapMode = instance.Data.FastBootstrapMode;
                                instanceController = new BootstrapInstanceController(instance.Name, coordinates, minLevel, maxLevel, circleSize, fastBootstrapMode, instance.Data.AccountGroup, instance.Data.IsEvent);
                                break;
                            case InstanceType.FindTTH:
                                instanceController = new SpawnpointFinderInstanceController(instance.Name, coordinates, minLevel, maxLevel, instance.Data.AccountGroup, instance.Data.IsEvent);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"[{instance.Name}] Error: {ex}");
                    }
                    break;
            }
            lock (_instancesLock)
            {
                _instances[instance.Name] = instanceController;
            }
            await Task.CompletedTask;
        }

        public async Task ReloadInstance(Instance newInstance, string oldInstanceName)
        {
            lock (_instancesLock)
            {
                var oldInstance = _instances[oldInstanceName];
                if (oldInstance != null)
                {
                    foreach (var (uuid, device) in _devices)
                    {
                        if (string.Compare(device.InstanceName, oldInstance.Name, true) == 0)
                        {
                            device.InstanceName = newInstance.Name;
                            _devices[uuid] = device;
                        }
                    }
                }
            }
            await RemoveInstance(oldInstanceName);
            await AddInstance(newInstance).ConfigureAwait(false);
        }

        public void ReloadAll()
        {
            lock (_instancesLock)
            {
                foreach (var (_, instanceController) in _instances)
                {
                    instanceController.Reload();
                }
            }
        }

        public async Task RemoveInstance(string instanceName)
        {
            lock (_instancesLock)
            {
                _instances[instanceName]?.Stop();
                _instances[instanceName] = null;
                _instances.Remove(instanceName);
                var devices = _devices.Where(d => string.Compare(d.Value.InstanceName, instanceName, true) == 0);
                foreach (var device in devices)
                {
                    _devices[device.Key] = null;
                }
            }
            await _assignmentController.Start().ConfigureAwait(false);
        }

        #endregion

        #region Devices

        public void AddDevice(Device device)
        {
            lock (_devicesLock)
            {
                if (!_devices.ContainsKey(device.Uuid))
                {
                    _devices.Add(device.Uuid, device);
                }
            }
        }

        public async Task RemoveDevice(Device device)
        {
            RemoveDevice(device.Uuid);
            await _assignmentController.Start().ConfigureAwait(false);
        }

        public void RemoveDevice(string uuid)
        {
            lock (_devicesLock)
            {
                if (_devices.ContainsKey(uuid))
                {
                    _devices.Remove(uuid);
                }
            }
        }

        public void ReloadDevice(Device newDevice, string oldDeviceUuid)
        {
            RemoveDevice(oldDeviceUuid);
            AddDevice(newDevice);
        }

        public List<string> GetDeviceUuidsInInstance(string instanceName)
        {
            lock (_devicesLock)
            {
                var uuids = new List<string>();
                foreach (var (uuid, device) in _devices)
                {
                    if (string.Compare(device.InstanceName, instanceName, true) == 0)
                    {
                        uuids.Add(uuid);
                    }
                }
                return uuids;
            }
        }

        #endregion

        public List<Pokemon> GetIVQueue(string name)
        {
            lock (_instancesLock)
            {
                if (_instances.ContainsKey(name))
                {
                    var instance = _instances[name];
                    if (instance is IVInstanceController iv)
                        return iv.GetQueue();
                }
                return new List<Pokemon>();
            }
        }

        public void GotPokemon(Pokemon pokemon)
        {
            lock (_instancesLock)
            {
                foreach (var (_, instanceController) in _instances)
                {
                    if (instanceController is IVInstanceController iv)
                    {
                        iv.AddPokemon(pokemon);
                    }
                }
            }
        }

        public void GotIV(Pokemon pokemon)
        {
            lock (_instancesLock)
            {
                foreach (var (_, instanceController) in _instances)
                {
                    if (instanceController is IVInstanceController iv)
                    {
                        iv.GotIV(pokemon);
                    }
                }
            }
        }

        #endregion

        #region Private Methods

        private IJobController GetInstanceControllerByName(string name)
        {
            lock (_instancesLock)
            {
                if (!_instances.ContainsKey(name))
                    return null;

                return _instances[name];
            }
        }

        private async Task RemoveInstance(Instance instance)
        {
            await RemoveInstance(instance.Name).ConfigureAwait(false);
        }

        #endregion
    }
}