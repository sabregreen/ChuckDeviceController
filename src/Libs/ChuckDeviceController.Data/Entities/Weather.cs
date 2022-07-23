﻿namespace ChuckDeviceController.Data.Entities
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    using POGOProtos.Rpc;
    using WeatherCondition = POGOProtos.Rpc.GameplayWeatherProto.Types.WeatherCondition;

    using ChuckDeviceController.Data.Contracts;
    using ChuckDeviceController.Extensions;
    using ChuckDeviceController.Geometry.Extensions;

    [Table("weather")]
    public partial class Weather : BaseEntity, ICoordinateEntity
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
        public WeatherCondition GameplayCondition { get; set; }

        [Column("wind_direction")]
        public ushort WindDirection { get; set; }

        [Column("cloud_level")]
        public ushort CloudLevel { get; set; }

        [Column("rain_level")]
        public ushort RainLevel { get; set; }

        [Column("wind_level")]
        public ushort WindLevel { get; set; }

        [Column("snow_level")]
        public ushort SnowLevel { get; set; }

        [Column("fog_level")]
        public ushort FogLevel { get; set; }

        [Column("special_effect_level")]
        public ushort SpecialEffectLevel { get; set; }

        [Column("severity")]
        public ushort? Severity { get; set; }

        [Column("warn_weather")]
        public bool? WarnWeather { get; set; }

        [Column("updated")]
        public ulong Updated { get; set; }

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
            GameplayCondition = weatherData.GameplayWeather.GameplayCondition;
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

        public bool Update(Weather? oldWeather = null)
        {
            var now = DateTime.UtcNow.ToTotalSeconds();
            Updated = now;

            var result = oldWeather == null ||
                oldWeather.GameplayCondition != GameplayCondition ||
                oldWeather.WarnWeather != WarnWeather;

            if (result)
            {
                // TODO: Webhooks
            }
            return result;
        }

        #endregion
    }
}