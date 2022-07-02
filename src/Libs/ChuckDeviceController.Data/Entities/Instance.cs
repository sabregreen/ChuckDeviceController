﻿namespace ChuckDeviceController.Data.Entities
{
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    [Table("instance")]
    public class Instance : BaseEntity
    {
        #region Properties

        [
            DisplayName("Name"),
            Column("name"),
            Key,
        ]
        public string Name { get; set; }

        [
            DisplayName("Instance Type"),
            Column("type"),
            Required,
        ]
        public InstanceType Type { get; set; }

        [
            DisplayName("Minimum Level"),
            Column("min_level"),
            Required,
        ]
        public ushort MinimumLevel { get; set; }

        [
            DisplayName("Maximum Level"),
            Column("max_level"),
            Required,
        ]
        public ushort MaximumLevel { get; set; }

        [
            DisplayName("Geofences"),
            Column("geofences"),
            Required,
        ]
        public List<string> Geofences { get; set; }

        [
            DisplayName("Data"),
            Column("data"),
        ]
        public InstanceData Data { get; set; }

        [NotMapped]
        public int DeviceCount { get; set; }

        [NotMapped]
        public int AreaCount { get; set; }

        #endregion

        #region Helper Methods

        public static string InstanceTypeToString(InstanceType type)
        {
            return type switch
            {
                InstanceType.AutoQuest => "auto_quest",
                InstanceType.CirclePokemon => "circle_pokemon",
                InstanceType.CircleRaid => "circle_raid",
                InstanceType.CircleSmartRaid => "smart_raid",
                InstanceType.PokemonIV => "pokemon_iv",
                InstanceType.Bootstrap => "bootstrap",
                InstanceType.FindTth => "find_tth",
                _ => type.ToString(),
            };
        }

        public static InstanceType StringToInstanceType(string instanceType)
        {
            return (instanceType.ToLower()) switch
            {
                "auto_quest" => InstanceType.AutoQuest,
                "circle_pokemon" => InstanceType.CirclePokemon,
                "circle_raid" => InstanceType.CircleRaid,
                "smart_raid" => InstanceType.CircleSmartRaid,
                "pokemon_iv" => InstanceType.PokemonIV,
                "bootstrap" => InstanceType.Bootstrap,
                "find_tth" => InstanceType.FindTth,
                _ => InstanceType.CirclePokemon,
            };
        }

        #endregion
    }
}