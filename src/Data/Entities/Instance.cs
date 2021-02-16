﻿namespace ChuckDeviceController.Data.Entities
{
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Text.Json.Serialization;

    using ChuckDeviceController.Data.Interfaces;

    [Table("instance")]
    public class Instance : BaseEntity, IAggregateRoot
    {
        [
            Column("name"),
            Key,
            JsonPropertyName("name"),
        ]
        public string Name { get; set; }

        [
            Column("type"),
            JsonPropertyName("type"),
        ]
        public InstanceType Type { get; set; }

        [
            Column("data"),
            JsonPropertyName("data"),
        ]
        public InstanceData Data { get; set; }

        [
            NotMapped,
            JsonPropertyName("count"),
        ]
        public int DeviceCount { get; set; }

        [
            NotMapped,
            JsonPropertyName("area_count"),
        ]
        public int AreaCount { get; set; }

        public static string InstanceTypeToString(InstanceType type)
        {
            return type switch
            {
                InstanceType.AutoQuest          => "auto_quest",
                InstanceType.CirclePokemon      => "circle_pokemon",
                InstanceType.CircleRaid         => "circle_raid",
                InstanceType.SmartCircleRaid    => "smart_raid",
                InstanceType.PokemonIV          => "pokemon_iv",
                InstanceType.Bootstrap          => "bootstrap",
                _ => type.ToString(),
            };
        }

        public static InstanceType StringToInstanceType(string instanceType)
        {
            return (instanceType.ToLower()) switch
            {
                "auto_quest"        => InstanceType.AutoQuest,
                "circle_pokemon"    => InstanceType.CirclePokemon,
                "circle_raid"       => InstanceType.CircleRaid,
                "smart_raid"        => InstanceType.SmartCircleRaid,
                "pokemon_iv"        => InstanceType.PokemonIV,
                "bootstrap"         => InstanceType.Bootstrap,
                _ => InstanceType.CirclePokemon,
            };
        }
    }
}