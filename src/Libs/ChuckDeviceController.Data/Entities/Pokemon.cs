﻿namespace ChuckDeviceController.Data.Entities
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    using Microsoft.EntityFrameworkCore;
    using POGOProtos.Rpc;

    using ChuckDeviceController.Data.Contexts;
    using ChuckDeviceController.Data.Contracts;
    using ChuckDeviceController.Extensions;
    using ChuckDeviceController.Geometry.Extensions;

    [Table("pokemon")]
    public class Pokemon : BaseEntity, ICoordinateEntity, IComparable
    {
        #region Constants

        public const uint DefaultTimeUnseenS = 1200;
        public const uint DefaultTimeReseenS = 600;
        public const uint DittoPokemonId = 132;
        public const uint WeatherBoostMinLevel = 6;
        public const uint WeatherBoostMinIvStat = 4;
        public const bool PvpEnabled = true; // TODO: Make 'EnablePvp' configurable via config
        public const bool CellPokemonEnabled = true; // TODO: Make 'CellPokemonEnabled' configurable via config
        public const bool SaveSpawnpointLastSeen = true; // TODO: Make 'SaveSpawnpointLastSeen' configurable via config
        public const bool WeatherIvClearingEnabled = true; // TODO: Make 'WeatherIvClearingEnabled' configurable via config

        #endregion

        #region Properties

        [
            Column("id"),
            Key,
            DatabaseGenerated(DatabaseGeneratedOption.None),
        ]
        public string Id { get; set; }

        [Column("pokemon_id")]
        public uint PokemonId { get; set; }

        [Column("lat")]
        public double Latitude { get; set; }

        [Column("lon")]
        public double Longitude { get; set; }

        [Column("spawn_id")]
        public ulong? SpawnId { get; set; }

        [Column("expire_timestamp")]
        public ulong ExpireTimestamp { get; set; }

        [Column("atk_iv")]
        public ushort? AttackIV { get; set; }

        [Column("def_iv")]
        public ushort? DefenseIV { get; set; }

        [Column("sta_iv")]
        public ushort? StaminaIV { get; set; }

        [
            DatabaseGenerated(DatabaseGeneratedOption.Computed),
            Column("iv"),
        ]
        public double? IV { get; set; }

        [Column("move_1")]
        public ushort? Move1 { get; set; }

        [Column("move_2")]
        public ushort? Move2 { get; set; }

        [Column("gender")]
        public ushort? Gender { get; set; }

        [Column("form")]
        public ushort? Form { get; set; }

        [Column("costume")]
        public ushort? Costume { get; set; }

        [Column("cp")]
        public ushort? CP { get; set; }

        [Column("level")]
        public ushort? Level { get; set; }

        [Column("weight")]
        public double? Weight { get; set; }

        [Column("size")]
        public double? Size { get; set; }

        [Column("weather")]
        public ushort? Weather { get; set; }

        [Column("shiny")]
        public bool? IsShiny { get; set; }

        [Column("username")]
        public string? Username { get; set; }

        [Column("pokestop_id")]
        public string? PokestopId { get; set; }

        [Column("first_seen_timestamp")]
        public ulong? FirstSeenTimestamp { get; set; }

        [Column("updated")]
        public ulong Updated { get; set; }

        [Column("changed")]
        public ulong Changed { get; set; }

        [Column("cell_id")]
        public ulong CellId { get; set; }

        [Column("expire_timestamp_verified")]
        public bool IsExpireTimestampVerified { get; set; }

        [Column("capture_1")]
        public double? Capture1 { get; set; }

        [Column("capture_2")]
        public double? Capture2 { get; set; }

        [Column("capture_3")]
        public double? Capture3 { get; set; }

        [Column("is_ditto")]
        public bool IsDitto { get; set; }

        [Column("display_pokemon_id")]
        public uint? DisplayPokemonId { get; set; }

        // TODO: public dynamic Pvp { get; set; }

        [Column("base_height")]
        public double BaseHeight { get; set; }

        [Column("base_weight")]
        public double BaseWeight { get; set; }

        [Column("is_event")]
        public bool IsEvent { get; set; }

        [Column("seen_type")]
        public SeenType SeenType { get; set; }

        [NotMapped]
        public bool HasChanges { get; set; }

        [NotMapped]
        public bool HasIvChanges { get; set; }

        [NotMapped]
        public bool IsNewPokemon { get; set; }

        [NotMapped]
        public bool IsNewPokemonWithIV { get; set; }

        /// <summary>
        /// Gets a value determining whether the Pokemon was first seen more
        /// than 10 minutes ago and is close to expiring.
        /// </summary>
        [NotMapped]
        public bool IsExpirationSoon =>
            DateTime.UtcNow.ToTotalSeconds() - (FirstSeenTimestamp ?? 1) >= DefaultTimeReseenS;

        #endregion

        #region Constructors

        public Pokemon()
        {
        }

        public Pokemon(MapDataContext context, WildPokemonProto wildPokemon, ulong cellId, ulong timestampMs, string username, bool isEvent)
        {
            IsEvent = isEvent;
            Id = wildPokemon.EncounterId.ToString();
            PokemonId = Convert.ToUInt16(wildPokemon.Pokemon.PokemonId);
            Latitude = wildPokemon.Latitude;
            Longitude = wildPokemon.Longitude;
            var spawnId = Convert.ToUInt64(wildPokemon.SpawnPointId, 16);
            Gender = Convert.ToUInt16(wildPokemon.Pokemon.PokemonDisplay.Gender);
            Form = Convert.ToUInt16(wildPokemon.Pokemon.PokemonDisplay.Form);
            if (wildPokemon.Pokemon.PokemonDisplay != null)
            {
                Costume = Convert.ToUInt16(wildPokemon.Pokemon.PokemonDisplay.Costume);
                Weather = Convert.ToUInt16(wildPokemon.Pokemon.PokemonDisplay.WeatherBoostedCondition);
            }
            Username = username;
            SpawnId = spawnId;
            CellId = cellId;
            SeenType = SeenType.Wild;

            UpdateSpawnpointAsync(context, wildPokemon, timestampMs, spawnId).ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        public Pokemon(MapDataContext context, NearbyPokemonProto nearbyPokemon, ulong cellId, string username, bool isEvent)
        {
            Id = Convert.ToString(nearbyPokemon.EncounterId);

            // Figure out where the Pokemon is
            double lat;
            double lon;
            if (string.IsNullOrEmpty(PokestopId))
            {
                if (!CellPokemonEnabled)
                {
                    return;
                }
                // Set Pokemon location to S2 cell coordinate as an approximation
                var latlng = cellId.ToCoordinate();
                lat = latlng.Latitude;
                lon = latlng.Longitude;
                SeenType = SeenType.NearbyCell;
            }
            else
            {
                var pokestop = context.Pokestops.FindAsync(nearbyPokemon.FortId).Result;
                if (pokestop == null)
                {
                    Console.WriteLine($"Failed to fetch Pokestop for nearby Pokemon '{Id}' to find location, skipping");
                    return;
                }
                lat = pokestop.Latitude;
                lon = pokestop.Longitude;
                SeenType = SeenType.NearbyStop;
            }

            Latitude = lat;
            Longitude = lon;
            PokemonId = Convert.ToUInt16(nearbyPokemon.PokedexNumber);
            PokestopId = nearbyPokemon.FortId;
            if (nearbyPokemon.PokemonDisplay != null)
            {
                Form = Convert.ToUInt16(nearbyPokemon.PokemonDisplay.Form);
                Costume = Convert.ToUInt16(nearbyPokemon.PokemonDisplay.Costume);
                Weather = Convert.ToUInt16(nearbyPokemon.PokemonDisplay.WeatherBoostedCondition);
                Gender = Convert.ToUInt16(nearbyPokemon.PokemonDisplay.Gender);
            }
            IsEvent = isEvent;
            Username = username;
            CellId = cellId;
            IsExpireTimestampVerified = false;
        }

        public Pokemon(MapDataContext context, MapPokemonProto mapPokemon, ulong cellId, string username, bool isEvent)
        {
            var encounterId = Convert.ToUInt64(mapPokemon.EncounterId);
            Id = encounterId.ToString();
            PokemonId = Convert.ToUInt32(mapPokemon.PokedexTypeId);

            var spawnpointId = mapPokemon.SpawnpointId;
            // Get Pokestop via spawnpoint id
            var pokestop = context.Pokestops.FindAsync(spawnpointId).Result; // TODO: Need to double check this
            if (pokestop == null)
            {
                Console.WriteLine($"Failed to fetch Pokestop by spawnpoint ID '{spawnpointId}' for map/lure Pokemon '{Id}' to find location, skipping");
                return;
            }
            PokestopId = pokestop.Id;
            Latitude = pokestop.Latitude;
            Longitude = pokestop.Longitude;

            if (mapPokemon.PokemonDisplay != null)
            {
                Gender = Convert.ToUInt16(mapPokemon.PokemonDisplay.Gender);
                Form = Convert.ToUInt16(mapPokemon.PokemonDisplay.Form);
                Costume = Convert.ToUInt16(mapPokemon.PokemonDisplay.Costume);
                Weather = Convert.ToUInt16(mapPokemon.PokemonDisplay.WeatherBoostedCondition);
            }

            Username = username;
            if (mapPokemon.ExpirationTimeMs > 0)
            {
                ExpireTimestamp = Convert.ToUInt64((0 + Convert.ToUInt64(mapPokemon.ExpirationTimeMs)) / 1000);
                IsExpireTimestampVerified = true;
            }
            else
            {
                IsExpireTimestampVerified = false;
            }

            IsEvent = isEvent;
            SeenType = SeenType.LureWild;
            CellId = cellId;
        }

        #endregion

        #region Public Methods

        public async Task AddEncounterAsync(MapDataContext context, EncounterOutProto encounterData, string username)
        {
            var pokemonId = Convert.ToUInt32(encounterData.Pokemon.Pokemon.PokemonId);
            var cp = Convert.ToUInt16(encounterData.Pokemon.Pokemon.Cp);
            var move1 = Convert.ToUInt16(encounterData.Pokemon.Pokemon.Move1);
            var move2 = Convert.ToUInt16(encounterData.Pokemon.Pokemon.Move2);
            var size = Convert.ToDouble(encounterData.Pokemon.Pokemon.HeightM);
            var weight = Convert.ToDouble(encounterData.Pokemon.Pokemon.WeightKg);
            var atkIv = Convert.ToUInt16(encounterData.Pokemon.Pokemon.IndividualAttack);
            var defIv = Convert.ToUInt16(encounterData.Pokemon.Pokemon.IndividualDefense);
            var staIv = Convert.ToUInt16(encounterData.Pokemon.Pokemon.IndividualStamina);
            var costume = Convert.ToUInt16(encounterData.Pokemon.Pokemon.PokemonDisplay.Costume);
            var form = Convert.ToUInt16(encounterData.Pokemon.Pokemon.PokemonDisplay.Form);
            var gender = Convert.ToUInt16(encounterData.Pokemon.Pokemon.PokemonDisplay.Gender);
            var weather = Convert.ToUInt16(encounterData.Pokemon.Pokemon.PokemonDisplay.WeatherBoostedCondition);
            var lat = Convert.ToDouble(encounterData.Pokemon.Latitude);
            var lon = Convert.ToDouble(encounterData.Pokemon.Longitude);

            if (pokemonId != PokemonId ||
                cp != CP ||
                move1 != Move1 ||
                move2 != Move2 ||
                size != Size ||
                weight != Weight ||
                AttackIV != atkIv ||
                DefenseIV != defIv ||
                StaminaIV != staIv ||
                Costume != Costume ||
                Form != form ||
                Gender != gender ||
                Weather != weather)
            {
                HasChanges = true;
                HasIvChanges = true;
            }

            PokemonId = pokemonId;
            CP = cp;
            Move1 = move1;
            Move2 = move2;
            Size = size;
            Weight = weight;
            AttackIV = atkIv;
            DefenseIV = defIv;
            StaminaIV = staIv;
            Costume = costume;
            Form = form;
            Gender = gender;
            Weather = weather;
            Latitude = lat;
            Longitude = lon;

            IsShiny = encounterData.Pokemon.Pokemon.PokemonDisplay.Shiny;
            Username = username;

            if (HasIvChanges)
            {
                // Although capture change values are player specific, set them to the Pokemon
                // Should remove them eventually though.
                if (encounterData.CaptureProbability != null)
                {
                    Capture1 = encounterData.CaptureProbability.CaptureProbability[0];
                    Capture2 = encounterData.CaptureProbability.CaptureProbability[1];
                    Capture3 = encounterData.CaptureProbability.CaptureProbability[2];
                }

                // Calculate Pokemon level from provided CP multiplier value
                var cpMultiplier = encounterData.Pokemon.Pokemon.CpMultiplier;
                ushort level;
                if (cpMultiplier < 0.734)
                {
                    level = Convert.ToUInt16(Math.Round(58.35178527 * cpMultiplier * cpMultiplier - 2.838007664 * cpMultiplier + 0.8539209906));
                }
                else
                {
                    level = Convert.ToUInt16(Math.Round(171.0112688 * cpMultiplier - 95.20425243));
                }

                Level = level;
                IsDitto = IsDittoDisguised(
                    Id,
                    PokemonId,
                    Level ?? 0,
                    Weather ?? 0,
                    AttackIV ?? 0,
                    DefenseIV ?? 0,
                    StaminaIV ?? 0);
                if (IsDitto)
                {
                    // Set default Ditto attributes
                    SetDittoAttributes(PokemonId, Weather ?? 0, Level ?? 0);
                }
            }

            var wildPokemon = encounterData.Pokemon;
            var spawnId = Convert.ToUInt64(wildPokemon.SpawnPointId, 16);
            SpawnId = spawnId;

            var now = DateTime.UtcNow.ToTotalSeconds();
            var timestampMs = now * 1000;
            await UpdateSpawnpointAsync(context, wildPokemon, timestampMs, spawnId);

            SeenType = SeenType.Encounter;
            Updated = now;
            Changed = Updated;
        }

        public void AddDiskEncounter(DiskEncounterOutProto diskEncounterData, string username)
        {
            var pokemonId = Convert.ToUInt32(diskEncounterData.Pokemon.PokemonId);
            var cp = Convert.ToUInt16(diskEncounterData.Pokemon.Cp);
            var move1 = Convert.ToUInt16(diskEncounterData.Pokemon.Move1);
            var move2 = Convert.ToUInt16(diskEncounterData.Pokemon.Move2);
            var size = Convert.ToDouble(diskEncounterData.Pokemon.HeightM);
            var weight = Convert.ToDouble(diskEncounterData.Pokemon.WeightKg);
            var atkIv = Convert.ToUInt16(diskEncounterData.Pokemon.IndividualAttack);
            var defIv = Convert.ToUInt16(diskEncounterData.Pokemon.IndividualDefense);
            var staIv = Convert.ToUInt16(diskEncounterData.Pokemon.IndividualStamina);
            var costume = Convert.ToUInt16(diskEncounterData.Pokemon.PokemonDisplay.Costume);
            var form = Convert.ToUInt16(diskEncounterData.Pokemon.PokemonDisplay.Form);
            var gender = Convert.ToUInt16(diskEncounterData.Pokemon.PokemonDisplay.Gender);
            //var weather = Convert.ToUInt16(diskEncounterData.Pokemon.PokemonDisplay.WeatherBoostedCondition);

            if (pokemonId != PokemonId ||
                cp != CP ||
                move1 != Move1 ||
                move2 != Move2 ||
                size != Size ||
                weight != Weight ||
                AttackIV != atkIv ||
                DefenseIV != defIv ||
                StaminaIV != staIv ||
                Costume != Costume ||
                Form != form ||
                Gender != gender)
            {
                HasChanges = true;
                HasIvChanges = true;
            }

            PokemonId = pokemonId;
            CP = cp;
            Move1 = move1;
            Move2 = move2;
            Size = size;
            Weight = weight;
            AttackIV = atkIv;
            DefenseIV = defIv;
            StaminaIV = staIv;
            Costume = costume;
            Form = form;
            Gender = gender;
            //Weather = weather;

            IsShiny = diskEncounterData.Pokemon.PokemonDisplay.Shiny;
            Username = username;

            if (HasIvChanges)
            {
                if (diskEncounterData.CaptureProbability != null)
                {
                    Capture1 = diskEncounterData.CaptureProbability.CaptureProbability[0];
                    Capture2 = diskEncounterData.CaptureProbability.CaptureProbability[1];
                    Capture3 = diskEncounterData.CaptureProbability.CaptureProbability[2];
                }
                var cpMultiplier = diskEncounterData.Pokemon.CpMultiplier;
                ushort level;
                if (cpMultiplier < 0.734)
                {
                    level = Convert.ToUInt16(Math.Round(58.35178527 * cpMultiplier * cpMultiplier - 2.838007664 * cpMultiplier + 0.8539209906));
                }
                else
                {
                    level = Convert.ToUInt16(Math.Round(171.0112688 * cpMultiplier - 95.20425243));
                }
                Level = level;
                IsDitto = IsDittoDisguised(
                    Id,
                    PokemonId,
                    Level ?? 0,
                    Weather ?? 0,
                    AttackIV ?? 0,
                    DefenseIV ?? 0,
                    StaminaIV ?? 0);
                if (IsDitto)
                {
                    SetDittoAttributes(PokemonId, Weather ?? 0, Level ?? 0);
                }
            }

            SeenType = SeenType.LureEncounter;
            Updated = DateTime.UtcNow.ToTotalSeconds();
            Changed = Updated;
        }

        public async Task UpdateAsync(MapDataContext context, bool updateIv = false)
        {
            var updateIV = updateIv;
            var setIvForWeather = false;
            var now = DateTime.UtcNow.ToTotalSeconds();
            Updated = now;

            //context.Attach(this);

            Pokemon? oldPokemon = null;
            try
            {
                oldPokemon = await context.Pokemon.FindAsync(Id);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error Pokemon.UpdateAsync: {ex}");
            }

            if (IsEvent && AttackIV == null)
            {
                Pokemon? oldPokemonNoEvent = null;
                try
                {
                    oldPokemonNoEvent = await context.Pokemon.FindAsync(Id); // IsEvent: false
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error Pokemon.UpdateAsync: {ex}");
                }
                if (oldPokemonNoEvent != null && oldPokemonNoEvent.AttackIV != null &&
                    ((Weather == 0 || Weather == null) && (oldPokemonNoEvent.Weather == 0 || oldPokemonNoEvent.Weather == null)) ||
                    (Weather != 0 && oldPokemonNoEvent.Weather != 0))
                {
                    AttackIV = oldPokemonNoEvent.AttackIV;
                    DefenseIV = oldPokemonNoEvent.DefenseIV;
                    StaminaIV = oldPokemonNoEvent.StaminaIV;
                    Level = oldPokemonNoEvent.Level;
                    CP = null;
                    Weight = null;
                    Size = null;
                    Move1 = null;
                    Move2 = null;
                    Capture1 = null;
                    Capture2 = null;
                    Capture3 = null;
                    updateIV = true;
                    //CalculatePvpRankings();

                    //context.Entry(this).Property(p => p.AttackIV).IsModified = true;
                    //context.Entry(this).Property(p => p.DefenseIV).IsModified = true;
                    //context.Entry(this).Property(p => p.StaminaIV).IsModified = true;
                    //context.Entry(this).Property(p => p.CP).IsModified = true;
                    //context.Entry(this).Property(p => p.Weight).IsModified = true;
                    //context.Entry(this).Property(p => p.Size).IsModified = true;
                    //context.Entry(this).Property(p => p.Move1).IsModified = true;
                    //context.Entry(this).Property(p => p.Move2).IsModified = true;
                    //context.Entry(this).Property(p => p.Level).IsModified = true;
                    //context.Entry(this).Property(p => p.Capture1).IsModified = true;
                    //context.Entry(this).Property(p => p.Capture2).IsModified = true;
                    //context.Entry(this).Property(p => p.Capture3).IsModified = true;
                }
            }
            if (IsEvent && !IsExpireTimestampVerified)
            {
                Pokemon? oldPokemonNoEvent = null;
                try
                {
                    oldPokemonNoEvent = await context.Pokemon.FindAsync(Id); // IsEvent: false
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error Pokemon.UpdateAsync: {ex}");
                }
                if (oldPokemonNoEvent != null && oldPokemonNoEvent.IsExpireTimestampVerified)
                {
                    ExpireTimestamp = oldPokemonNoEvent.ExpireTimestamp;
                    IsExpireTimestampVerified = oldPokemonNoEvent.IsExpireTimestampVerified;
                }
            }

            if (oldPokemon == null)
            {
                setIvForWeather = false;

                if (ExpireTimestamp == 0)
                {
                    ExpireTimestamp = now + DefaultTimeUnseenS;
                    //context.Entry(this).Property(p => p.ExpireTimestamp).IsModified = true;
                }
                FirstSeenTimestamp = now;
                Updated = now;
                Changed = now;

                //context.Entry(this).Property(p => p.FirstSeenTimestamp).IsModified = true;
                //context.Entry(this).Property(p => p.Updated).IsModified = true;
                //context.Entry(this).Property(p => p.Changed).IsModified = true;
            }
            else
            {
                if (FirstSeenTimestamp == null && oldPokemon.FirstSeenTimestamp > 0)
                {
                    FirstSeenTimestamp = oldPokemon.FirstSeenTimestamp;
                }

                //context.Entry(this).Property(p => p.FirstSeenTimestamp).IsModified = true;

                if (ExpireTimestamp == 0)// && oldPokemon.ExpireTimestamp > 0)
                {
                    var changed = DateTime.UtcNow.ToTotalSeconds();
                    var oldExpireDate = oldPokemon.ExpireTimestamp;
                    if (changed - oldExpireDate < DefaultTimeReseenS || /* TODO: Workaround -> */ oldPokemon.ExpireTimestamp == 0)
                    {
                        ExpireTimestamp = changed + DefaultTimeReseenS;
                    }
                    else
                    {
                        ExpireTimestamp = oldPokemon.ExpireTimestamp;
                    }
                    //context.Entry(this).Property(p => p.ExpireTimestamp).IsModified = true;
                }

                if (!IsExpireTimestampVerified && oldPokemon.IsExpireTimestampVerified)
                {
                    IsExpireTimestampVerified = oldPokemon.IsExpireTimestampVerified;
                    ExpireTimestamp = oldPokemon.ExpireTimestamp;
                }

                if (oldPokemon.PokemonId != PokemonId)
                {
                    if (oldPokemon.PokemonId != DittoPokemonId)
                    {
                        Console.WriteLine($"Pokemon {Id} changed from {oldPokemon.PokemonId} to {PokemonId}");
                    }
                    else if (oldPokemon.DisplayPokemonId != PokemonId)
                    {
                        Console.WriteLine($"Pokemon {Id} Ditto disguised as {oldPokemon.DisplayPokemonId} now seen as {PokemonId}");
                    }
                    else if (oldPokemon.DisplayPokemonId != null && oldPokemon.PokemonId != PokemonId)
                    {
                        Console.WriteLine($"Pokemon {Id} Ditto from {oldPokemon.PokemonId} to {PokemonId}");
                    }

                    //context.Entry(this).Property(p => p.PokemonId).IsModified = true;
                    //context.Entry(this).Property(p => p.DisplayPokemonId).IsModified = true;
                }

                if (oldPokemon.CellId > 0 && CellId == 0)
                {
                    CellId = oldPokemon.CellId;
                    //context.Entry(this).Property(p => p.CellId).IsModified = true;
                }

                if (oldPokemon.SpawnId != null)
                {
                    SpawnId = oldPokemon.SpawnId;
                    Latitude = oldPokemon.Latitude;
                    Longitude = oldPokemon.Longitude;

                    //context.Entry(this).Property(p => p.SpawnId).IsModified = true;
                    //context.Entry(this).Property(p => p.Latitude).IsModified = true;
                    //context.Entry(this).Property(p => p.Longitude).IsModified = true;
                }

                if (oldPokemon.PokestopId != null && PokestopId == null)
                {
                    PokestopId = oldPokemon.PokestopId;
                    //context.Entry(this).Property(p => p.PokestopId).IsModified = true;
                }

                //if (oldPokemon.Pvp != null && Pvp == null)
                //{
                //    Pvp = oldPokemon.Pvp;
                //    context.Entry(this).Property(p => p.Pvp).IsModified = true;
                //}

                if (updateIV && oldPokemon.AttackIV == null && AttackIV != null)
                {
                    Changed = now;
                }
                else
                {
                    Changed = oldPokemon.Changed;
                }
                //context.Entry(this).Property(p => p.Changed).IsModified = true;

                var weatherChanged = (oldPokemon.Weather == null || oldPokemon.Weather != 0) && (Weather > 0) ||
                    (Weather == null || Weather == 0) && (oldPokemon.Weather > 0);

                if (oldPokemon.AttackIV != null && AttackIV == null && !weatherChanged)
                {
                    setIvForWeather = false;
                    AttackIV = oldPokemon.AttackIV;
                    DefenseIV = oldPokemon.DefenseIV;
                    StaminaIV = oldPokemon.StaminaIV;
                    CP = oldPokemon.CP;
                    Weight = oldPokemon.Weight;
                    Size = oldPokemon.Size;
                    Move1 = oldPokemon.Move1;
                    Move2 = oldPokemon.Move2;
                    Level = oldPokemon.Level;
                    Capture1 = oldPokemon.Capture1;
                    Capture2 = oldPokemon.Capture2;
                    Capture3 = oldPokemon.Capture3;
                    IsShiny = oldPokemon.IsShiny;
                    SeenType = oldPokemon.SeenType;
                    IsDitto = IsDittoDisguised(oldPokemon);

                    context.Entry(this).Property(p => p.AttackIV).IsModified = true;
                    context.Entry(this).Property(p => p.DefenseIV).IsModified = true;
                    context.Entry(this).Property(p => p.StaminaIV).IsModified = true;
                    context.Entry(this).Property(p => p.CP).IsModified = true;
                    context.Entry(this).Property(p => p.Weight).IsModified = true;
                    context.Entry(this).Property(p => p.Size).IsModified = true;
                    context.Entry(this).Property(p => p.Move1).IsModified = true;
                    context.Entry(this).Property(p => p.Move2).IsModified = true;
                    context.Entry(this).Property(p => p.Level).IsModified = true;
                    context.Entry(this).Property(p => p.Capture1).IsModified = true;
                    context.Entry(this).Property(p => p.Capture2).IsModified = true;
                    context.Entry(this).Property(p => p.Capture3).IsModified = true;
                    context.Entry(this).Property(p => p.IsShiny).IsModified = true;
                    context.Entry(this).Property(p => p.SeenType).IsModified = true;
                    context.Entry(this).Property(p => p.IsDitto).IsModified = true;

                    if (IsDitto)
                    {
                        Console.WriteLine($"oldPokemon {Id} Ditto found, disguised as {PokemonId}");
                        SetDittoAttributes(PokemonId, oldPokemon.Weather ?? 0, oldPokemon.Level ?? 0);

                        context.Entry(this).Property(p => p.DisplayPokemonId).IsModified = true;
                        context.Entry(this).Property(p => p.PokemonId).IsModified = true;
                        context.Entry(this).Property(p => p.Form).IsModified = true;
                        context.Entry(this).Property(p => p.Costume).IsModified = true;
                        context.Entry(this).Property(p => p.Gender).IsModified = true;
                        context.Entry(this).Property(p => p.Move1).IsModified = true;
                        context.Entry(this).Property(p => p.Move2).IsModified = true;
                        context.Entry(this).Property(p => p.Weight).IsModified = true;
                        context.Entry(this).Property(p => p.Size).IsModified = true;
                    }
                }
                else if ((AttackIV != null && oldPokemon.AttackIV == null) ||
                    (CP != null && oldPokemon.CP == null) || HasIvChanges)
                {
                    setIvForWeather = false;
                    updateIV = true;
                }
                else if (weatherChanged && oldPokemon.AttackIV != null && WeatherIvClearingEnabled)
                {
                    Console.WriteLine($"Pokemon {Id} changed weather boost state. Clearing IVs.");
                    setIvForWeather = true;
                    AttackIV = null;
                    DefenseIV = null;
                    StaminaIV = null;
                    CP = null;
                    Weight = null;
                    Size = null;
                    Move1 = null;
                    Move2 = null;
                    Level = null;
                    Capture1 = null;
                    Capture2 = null;
                    Capture3 = null;
                    //Pvp = null;

                    //context.Entry(this).Property(p => p.AttackIV).IsModified = true;
                    //context.Entry(this).Property(p => p.DefenseIV).IsModified = true;
                    //context.Entry(this).Property(p => p.StaminaIV).IsModified = true;
                    //context.Entry(this).Property(p => p.CP).IsModified = true;
                    //context.Entry(this).Property(p => p.Weight).IsModified = true;
                    //context.Entry(this).Property(p => p.Size).IsModified = true;
                    //context.Entry(this).Property(p => p.Move1).IsModified = true;
                    //context.Entry(this).Property(p => p.Move2).IsModified = true;
                    //context.Entry(this).Property(p => p.Level).IsModified = true;
                    //context.Entry(this).Property(p => p.Capture1).IsModified = true;
                    //context.Entry(this).Property(p => p.Capture2).IsModified = true;
                    //context.Entry(this).Property(p => p.Capture3).IsModified = true;
                    //context.Entry(this).Property(p => p.Pvp).IsModified = true;

                    Console.WriteLine($"Weather-Boosted state changed. Clearing IVs");
                }
                else
                {
                    setIvForWeather = false;
                }

                // TODO: Check shouldUpdate

                if (updateIV || setIvForWeather)
                {
                    //context.Entry(this).Property(p => p.AttackIV).IsModified = true;
                    //context.Entry(this).Property(p => p.DefenseIV).IsModified = true;
                    //context.Entry(this).Property(p => p.StaminaIV).IsModified = true;
                    //context.Entry(this).Property(p => p.CP).IsModified = true;
                    //context.Entry(this).Property(p => p.Weight).IsModified = true;
                    //context.Entry(this).Property(p => p.Size).IsModified = true;
                    //context.Entry(this).Property(p => p.Move1).IsModified = true;
                    //context.Entry(this).Property(p => p.Move2).IsModified = true;
                    //context.Entry(this).Property(p => p.Level).IsModified = true;
                    //context.Entry(this).Property(p => p.Capture1).IsModified = true;
                    //context.Entry(this).Property(p => p.Capture2).IsModified = true;
                    //context.Entry(this).Property(p => p.Capture3).IsModified = true;
                    //context.Entry(this).Property(p => p.IsShiny).IsModified = true;
                    //context.Entry(this).Property(p => p.DisplayPokemonId).IsModified = true;
                    //context.Entry(this).Property(p => p.Pvp).IsModified = true;
                }

                if (oldPokemon.PokemonId == DittoPokemonId && PokemonId != DittoPokemonId)
                {
                    //context.Entry(this).Property(p => p.PokemonId).IsModified = true;
                    //context.Entry(this).Property(p => p.DisplayPokemonId).IsModified = true;
                    Console.WriteLine($"Pokemon {Id} Ditto changed from {oldPokemon.PokemonId} to {PokemonId}");
                }

                Updated = now;
                //context.Entry(this).Property(p => p.Updated).IsModified = true;
            }

            if (setIvForWeather)
            {
                // TODO: Webhook
                IsNewPokemon = true;
            }
            else if (oldPokemon == null)
            {
                // TODO: Webhook
                IsNewPokemon = true;
                IsNewPokemonWithIV = AttackIV != null;
            }
            else if (updateIV && ((oldPokemon.AttackIV == null && AttackIV != null) || oldPokemon.HasIvChanges))
            {
                // TODO: Webhook
                oldPokemon.HasIvChanges = false;
                IsNewPokemonWithIV = true;
            }

            // TODO: Cache pokemon
        }

        #endregion

        #region Private Methods

        private async Task UpdateSpawnpointAsync(MapDataContext context, WildPokemonProto wild, ulong timestampMs, ulong spawnId)
        {
            var now = DateTime.UtcNow.ToTotalSeconds();
            if (wild.TimeTillHiddenMs <= 90000 && wild.TimeTillHiddenMs > 0)
            {
                ExpireTimestamp = Convert.ToUInt64((timestampMs + Convert.ToUInt64(wild.TimeTillHiddenMs)) / 1000);
                IsExpireTimestampVerified = true;
                var date = timestampMs.FromMilliseconds();
                var secondOfHour = date.Second + (date.Minute * 60);

                var spawnpoint = new Spawnpoint
                {
                    Id = spawnId,
                    Latitude = Latitude,
                    Longitude = Longitude,
                    DespawnSecond = Convert.ToUInt16(secondOfHour),
                    LastSeen = SaveSpawnpointLastSeen ? now : null,
                    Updated = now,
                };
                await spawnpoint.UpdateAsync(context, update: true);

                if (context.Spawnpoints.AsNoTracking().Any(s => s.Id == spawnId))
                {
                    context.Spawnpoints.Update(spawnpoint);
                }
                else
                {
                    await context.Spawnpoints.AddAsync(spawnpoint);
                }
            }
            else
            {
                IsExpireTimestampVerified = false;
            }

            if (!IsExpireTimestampVerified && spawnId > 0)
            {
                var spawnpoint = await context.Spawnpoints.FindAsync(SpawnId);
                if (spawnpoint != null && spawnpoint.DespawnSecond != null)
                {
                    var despawnSecond = spawnpoint.DespawnSecond;
                    var timestampS = timestampMs / 1000;
                    var date = timestampS.FromMilliseconds();
                    var secondOfHour = date.Second + (date.Minute * 60);
                    var despawnOffset = despawnSecond - secondOfHour;
                    if (despawnSecond < secondOfHour)
                        despawnOffset += 3600;

                    // Update spawnpoint last_seen if enabled
                    if (SaveSpawnpointLastSeen)
                    {
                        context.Attach(spawnpoint);
                        context.Entry(spawnpoint).Property(p => p.LastSeen).IsModified = true;
                        spawnpoint.LastSeen = now;
                        context.Spawnpoints.Update(spawnpoint);
                    }

                    ExpireTimestamp = timestampS + (ulong)despawnOffset;
                    IsExpireTimestampVerified = true;
                }
                else
                {
                    var newSpawnpoint = new Spawnpoint
                    {
                        Id = spawnId,
                        Latitude = Latitude,
                        Longitude = Longitude,
                        DespawnSecond = null,
                        LastSeen = now,
                        Updated = now,
                    };
                    await newSpawnpoint.UpdateAsync(context, update: true);

                    if (context.Spawnpoints.AsNoTracking().Any(s => s.Id == spawnId))
                    {
                        context.Spawnpoints.Update(newSpawnpoint);
                    }
                    else
                    {
                        await context.Spawnpoints.AddAsync(newSpawnpoint);
                    }
                }
            }
        }

        #endregion

        #region Ditto Detection

        private void SetDittoAttributes(uint displayPokemonId, ushort weather, ushort level)
        {
            ushort moveTransformFast = 242;
            ushort moveStruggleCharge = 133;
            DisplayPokemonId = displayPokemonId;
            PokemonId = DittoPokemonId;
            Form = 0;
            Move1 = moveTransformFast;
            Move2 = moveStruggleCharge;
            Gender = 3;
            Costume = 0;
            Size = null;
            Weight = null;
            if (weather == 0 && level > 30)
            {
                Console.WriteLine($"Pokemon {Id} weather boosted Ditto - reset IV is needed");
            }
        }

        public static bool IsDittoDisguised(Pokemon pokemon)
        {
            return IsDittoDisguised(
                pokemon.Id,
                pokemon.PokemonId,
                pokemon.Level ?? 0,
                pokemon.Weather ?? 0,
                pokemon.AttackIV ?? 0,
                pokemon.DefenseIV ?? 0,
                pokemon.StaminaIV ?? 0
            );
        }

        public static bool IsDittoDisguised(string id, uint pokemonId, ushort level, ushort weather, ushort atkIv, ushort defIv, ushort staIv)
        {
            if (pokemonId == DittoPokemonId)
            {
                Console.WriteLine($"Pokemon {id} was already detected as Ditto.");
                return true;
            }

            var isUnderLevelBoosted = level > 0 && level < WeatherBoostMinLevel;
            var isUnderIvStatBoosted = level > 0 &&
                (atkIv < WeatherBoostMinIvStat ||
                 defIv < WeatherBoostMinIvStat ||
                 staIv < WeatherBoostMinIvStat);
            var isWeatherBoosted = weather > 0;
            var isOverLevel = level > 30;

            if (isWeatherBoosted)
            {
                if (isUnderLevelBoosted || isUnderIvStatBoosted)
                {
                    Console.WriteLine($"Pokemon {id} Ditto found, disguised as {pokemonId}");
                    return true;
                }
            }
            else
            {
                if (isOverLevel)
                {
                    Console.WriteLine($"Pokemon {id} weather boosted Ditto found, disguised as {pokemonId}");
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Helper Methods

        public static string SeenTypeToString(SeenType type)
        {
            return type switch
            {
                SeenType.Unset => "unset",
                SeenType.Encounter => "encounter",
                SeenType.Wild => "wild",
                SeenType.NearbyStop => "nearby_stop",
                SeenType.NearbyCell => "nearby_cell",
                SeenType.LureWild => "lure_wild",
                SeenType.LureEncounter => "lure_encounter",
                _ => type.ToString(),
            };
        }

        public static SeenType StringToSeenType(string seenType)
        {
            return seenType.ToLower() switch
            {
                "unset" => SeenType.Unset,
                "encounter" => SeenType.Encounter,
                "wild" => SeenType.Wild,
                "nearby_stop" => SeenType.NearbyStop,
                "nearby_cell" => SeenType.NearbyCell,
                "lure_wild" => SeenType.LureWild,
                "lure_encounter" => SeenType.LureEncounter,
                _ => SeenType.Unset,
            };
        }

        #endregion

        public int CompareTo(object? obj)
        {
            if (obj == null)
                return -1;

            var other = (Pokemon)obj;
            var result = Id.CompareTo(other.Id);
            if (result != 0)
            {
                return result;
            }

            result = PokemonId.CompareTo(other.PokemonId);
            if (result != 0)
            {
                return result;
            }

            return 0;
        }
    }
}