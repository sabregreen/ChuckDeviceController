﻿namespace ChuckDeviceController.Data.Entities;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using POGOProtos.Rpc;

using ChuckDeviceController.Caching.Memory;
using ChuckDeviceController.Common;
using ChuckDeviceController.Data.Abstractions;
using ChuckDeviceController.Extensions;
using ChuckDeviceController.Geometry.Extensions;

[Table("weather")]
public partial class Weather : BaseEntity, IWeather, ICoordinateEntity, IWebhookEntity
{
    #region Properties

    [
        Column("id"),
        Key,
        DatabaseGenerated(DatabaseGeneratedOption.None),
    ]
    public long Id { get; set; }

    [Column("level")]
    public ushort Level { get; set; }

    [Column("latitude")]
    public double Latitude { get; set; }

    [Column("longitude")]
    public double Longitude { get; set; }

    [Column("gameplay_condition")]
    public ushort GameplayCondition { get; set; }

    [Column("cloud_level")]
    public ushort CloudLevel { get; set; }

    [Column("rain_level")]
    public ushort RainLevel { get; set; }

    [Column("snow_level")]
    public ushort SnowLevel { get; set; }

    [Column("fog_level")]
    public ushort FogLevel { get; set; }

    [Column("wind_level")]
    public ushort WindLevel { get; set; }

    [Column("wind_direction")]
    public ushort WindDirection { get; set; }

    [Column("warn_weather")]
    public bool? WarnWeather { get; set; }

    [Column("special_effect_level")]
    public ushort SpecialEffectLevel { get; set; }

    [Column("severity")]
    public ushort? Severity { get; set; }

    [Column("updated")]
    public ulong Updated { get; set; }

    [NotMapped]
    public bool SendWebhook { get; set; }

    [NotMapped]
    public bool HasChanges { get; set; }

    #endregion

    #region Constructors

    public Weather()
    {
    }

    public Weather(ClientWeatherProto weatherData)
    {
        var now = DateTime.UtcNow.ToTotalSeconds();
        var s2cell = weatherData.S2CellId.S2CellFromId();
        var center = s2cell.RectBound.Center;
        var alert = weatherData.Alerts?.FirstOrDefault();
        Id = weatherData.S2CellId;
        Level = s2cell.Level;
        Latitude = center.LatDegrees;
        Longitude = center.LngDegrees;
        GameplayCondition = Convert.ToUInt16(weatherData.GameplayWeather.GameplayCondition);
        WindDirection = Convert.ToUInt16(weatherData.DisplayWeather.WindDirection);
        CloudLevel = Convert.ToUInt16(weatherData.DisplayWeather.CloudLevel);
        RainLevel = Convert.ToUInt16(weatherData.DisplayWeather.RainLevel);
        WindLevel = Convert.ToUInt16(weatherData.DisplayWeather.WindLevel);
        SnowLevel = Convert.ToUInt16(weatherData.DisplayWeather.SnowLevel);
        FogLevel = Convert.ToUInt16(weatherData.DisplayWeather.FogLevel);
        SpecialEffectLevel = Convert.ToUInt16(weatherData.DisplayWeather.SpecialEffectLevel);
        Severity = Convert.ToUInt16(weatherData.Alerts?.FirstOrDefault()?.Severity ?? null);
        WarnWeather = alert?.WarnWeather;
        Updated = now;
    }

    #endregion

    #region Public Methods

    public async Task UpdateAsync(Weather? oldWeather, IMemoryCacheService memCache)
    {
        Updated = DateTime.UtcNow.ToTotalSeconds();

        if (oldWeather == null)
        {
            SendWebhook = true;
            HasChanges = true;
        }
        else if (oldWeather.GameplayCondition != GameplayCondition || 
                 oldWeather.WarnWeather != WarnWeather ||
                 oldWeather.Severity != Severity ||
                 oldWeather.SpecialEffectLevel != SpecialEffectLevel)
        {
            SendWebhook = true;
            HasChanges = true;
        }

        // Cache weather cell entity by id
        memCache.Set(Id, this);

        await Task.CompletedTask;
    }

    public dynamic? GetWebhookData(string type)
    {
        switch (type.ToLower())
        {
            case "weather":
                var s2cell = Id.S2CellFromId();
                var polygon = new List<List<double>>();
                for (var i = 0; i <= 3; i++)
                {
                    var vertex = s2cell.GetVertex(i);
                    var coord = vertex.ToCoordinate();
                    polygon.Add(new List<double> { coord.Latitude, coord.Longitude });
                }
                return new
                {
                    type = WebhookHeaders.Weather,
                    message = new
                    {
                        s2_cell_id = Id,
                        latitude = Latitude,
                        longitude = Longitude,
                        polygon,
                        gameplay_condition = GameplayCondition,
                        wind_direction = WindDirection,
                        cloud_level = CloudLevel,
                        rain_level = RainLevel,
                        wind_level = WindLevel,
                        snow_level = SnowLevel,
                        fog_level = FogLevel,
                        special_effect_level = SpecialEffectLevel,
                        severity = Severity,
                        warn_weather = WarnWeather,
                        updated = Updated,
                    },
                };
        }

        Console.WriteLine($"Received unknown weather webhook payload type: {type}, returning null");
        return null;
    }

    #endregion
}