﻿// <auto-generated />
using System;
using ChuckDeviceController.Data.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace ChuckDeviceConfigurator.Migrations
{
    [DbContext(typeof(ControllerDbContext))]
    [Migration("20230110081747_AddDeviceInstanceConstraint")]
    partial class AddDeviceInstanceConstraint
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.1")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            MySqlModelBuilderExtensions.HasCharSet(modelBuilder, "utf8mb4", DelegationModes.ApplyToAll);

            modelBuilder.Entity("ChuckDeviceController.Data.Entities.Account", b =>
                {
                    b.Property<string>("Username")
                        .HasColumnType("varchar(255)")
                        .HasColumnName("username");

                    b.Property<ulong?>("CreationTimestamp")
                        .HasColumnType("bigint unsigned")
                        .HasColumnName("creation_timestamp")
                        .HasAnnotation("Relational:JsonPropertyName", "creation_timestamp");

                    b.Property<string>("Failed")
                        .HasColumnType("varchar(255)")
                        .HasColumnName("failed");

                    b.Property<ulong?>("FailedTimestamp")
                        .HasColumnType("bigint unsigned")
                        .HasColumnName("failed_timestamp")
                        .HasAnnotation("Relational:JsonPropertyName", "failed_timestamp");

                    b.Property<ulong?>("FirstWarningTimestamp")
                        .HasColumnType("bigint unsigned")
                        .HasColumnName("first_warning_timestamp")
                        .HasAnnotation("Relational:JsonPropertyName", "first_warning_timestamp");

                    b.Property<string>("GroupName")
                        .HasColumnType("varchar(255)")
                        .HasColumnName("group")
                        .HasAnnotation("Relational:JsonPropertyName", "group");

                    b.Property<bool?>("HasWarn")
                        .HasColumnType("tinyint(1)")
                        .HasColumnName("warn")
                        .HasAnnotation("Relational:JsonPropertyName", "warn");

                    b.Property<bool?>("IsBanned")
                        .HasColumnType("tinyint(1)")
                        .HasColumnName("banned")
                        .HasAnnotation("Relational:JsonPropertyName", "banned");

                    b.Property<double?>("LastEncounterLatitude")
                        .HasPrecision(18, 6)
                        .HasColumnType("double")
                        .HasColumnName("last_encounter_lat")
                        .HasAnnotation("Relational:JsonPropertyName", "last_encounter_lat");

                    b.Property<double?>("LastEncounterLongitude")
                        .HasPrecision(18, 6)
                        .HasColumnType("double")
                        .HasColumnName("last_encounter_lon")
                        .HasAnnotation("Relational:JsonPropertyName", "last_encounter_lon");

                    b.Property<ulong?>("LastEncounterTime")
                        .HasColumnType("bigint unsigned")
                        .HasColumnName("last_encounter_time")
                        .HasAnnotation("Relational:JsonPropertyName", "last_encounter_time");

                    b.Property<ulong?>("LastUsedTimestamp")
                        .HasColumnType("bigint unsigned")
                        .HasColumnName("last_used_timestamp")
                        .HasAnnotation("Relational:JsonPropertyName", "last_used_timestamp");

                    b.Property<ushort>("Level")
                        .HasColumnType("smallint unsigned")
                        .HasColumnName("level");

                    b.Property<string>("Password")
                        .IsRequired()
                        .HasColumnType("longtext")
                        .HasColumnName("password");

                    b.Property<uint>("Spins")
                        .HasColumnType("int unsigned")
                        .HasColumnName("spins");

                    b.Property<bool?>("SuspendedMessageAcknowledged")
                        .HasColumnType("tinyint(1)")
                        .HasColumnName("suspended_message_acknowledged")
                        .HasAnnotation("Relational:JsonPropertyName", "suspended_message_acknowledged");

                    b.Property<ushort>("Tutorial")
                        .HasColumnType("smallint unsigned")
                        .HasColumnName("tutorial");

                    b.Property<ulong?>("WarnExpireTimestamp")
                        .HasColumnType("bigint unsigned")
                        .HasColumnName("warn_expire_timestamp")
                        .HasAnnotation("Relational:JsonPropertyName", "warn_expire_timestamp");

                    b.Property<bool?>("WarnMessageAcknowledged")
                        .HasColumnType("tinyint(1)")
                        .HasColumnName("warn_message_acknowledged")
                        .HasAnnotation("Relational:JsonPropertyName", "warn_message_acknowledged");

                    b.Property<bool?>("WasSuspended")
                        .HasColumnType("tinyint(1)")
                        .HasColumnName("was_suspended")
                        .HasAnnotation("Relational:JsonPropertyName", "was_suspended");

                    b.HasKey("Username");

                    b.HasIndex("Failed");

                    b.HasIndex("FailedTimestamp");

                    b.HasIndex("FirstWarningTimestamp");

                    b.HasIndex("GroupName");

                    b.HasIndex("HasWarn");

                    b.HasIndex("Level");

                    b.HasIndex("WarnExpireTimestamp");

                    b.HasIndex("WasSuspended");

                    b.ToTable("account");
                });

            modelBuilder.Entity("ChuckDeviceController.Data.Entities.ApiKey", b =>
                {
                    b.Property<uint>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int unsigned")
                        .HasColumnName("id")
                        .HasAnnotation("Relational:JsonPropertyName", "id");

                    b.Property<ulong>("ExpirationTimestamp")
                        .HasColumnType("bigint unsigned")
                        .HasColumnName("expiration_timestamp")
                        .HasAnnotation("Relational:JsonPropertyName", "expiration_timestamp");

                    b.Property<bool>("IsEnabled")
                        .HasColumnType("tinyint(1)")
                        .HasColumnName("enabled")
                        .HasAnnotation("Relational:JsonPropertyName", "enabled");

                    b.Property<string>("Key")
                        .IsRequired()
                        .HasColumnType("longtext")
                        .HasColumnName("key")
                        .HasAnnotation("Relational:JsonPropertyName", "key");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("longtext")
                        .HasColumnName("name")
                        .HasAnnotation("Relational:JsonPropertyName", "name");

                    b.Property<int>("Scope")
                        .HasColumnType("int")
                        .HasColumnName("scope")
                        .HasAnnotation("Relational:JsonPropertyName", "scope");

                    b.HasKey("Id");

                    b.HasIndex("ExpirationTimestamp");

                    b.ToTable("api_key");
                });

            modelBuilder.Entity("ChuckDeviceController.Data.Entities.Assignment", b =>
                {
                    b.Property<uint>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int unsigned")
                        .HasColumnName("id");

                    b.Property<DateTime?>("Date")
                        .HasColumnType("datetime(6)")
                        .HasColumnName("date");

                    b.Property<string>("DeviceGroupName")
                        .HasColumnType("longtext")
                        .HasColumnName("device_group_name");

                    b.Property<string>("DeviceUuid")
                        .HasColumnType("longtext")
                        .HasColumnName("device_uuid");

                    b.Property<bool>("Enabled")
                        .HasColumnType("tinyint(1)")
                        .HasColumnName("enabled");

                    b.Property<string>("InstanceName")
                        .IsRequired()
                        .HasColumnType("varchar(255)")
                        .HasColumnName("instance_name");

                    b.Property<string>("SourceInstanceName")
                        .HasColumnType("longtext")
                        .HasColumnName("source_instance_name");

                    b.Property<uint>("Time")
                        .HasColumnType("int unsigned")
                        .HasColumnName("time");

                    b.HasKey("Id");

                    b.HasIndex("InstanceName");

                    b.ToTable("assignment");
                });

            modelBuilder.Entity("ChuckDeviceController.Data.Entities.AssignmentGroup", b =>
                {
                    b.Property<string>("Name")
                        .HasColumnType("varchar(255)")
                        .HasColumnName("name");

                    b.Property<string>("AssignmentIds")
                        .IsRequired()
                        .HasColumnType("longtext")
                        .HasColumnName("assignment_ids");

                    b.Property<bool>("Enabled")
                        .HasColumnType("tinyint(1)")
                        .HasColumnName("enabled");

                    b.HasKey("Name");

                    b.ToTable("assignment_group");
                });

            modelBuilder.Entity("ChuckDeviceController.Data.Entities.Device", b =>
                {
                    b.Property<string>("Uuid")
                        .HasColumnType("varchar(255)")
                        .HasColumnName("uuid")
                        .HasAnnotation("Relational:JsonPropertyName", "uuid");

                    b.Property<string>("AccountUsername")
                        .HasColumnType("varchar(255)")
                        .HasColumnName("account_username")
                        .HasAnnotation("Relational:JsonPropertyName", "account_username");

                    b.Property<string>("InstanceName")
                        .HasColumnType("varchar(255)")
                        .HasColumnName("instance_name")
                        .HasAnnotation("Relational:JsonPropertyName", "instance_name");

                    b.Property<bool>("IsPendingAccountSwitch")
                        .HasColumnType("tinyint(1)")
                        .HasColumnName("pending_account_switch")
                        .HasAnnotation("Relational:JsonPropertyName", "pending_account_switch");

                    b.Property<string>("LastHost")
                        .HasColumnType("longtext")
                        .HasColumnName("last_host")
                        .HasAnnotation("Relational:JsonPropertyName", "last_host");

                    b.Property<double?>("LastLatitude")
                        .HasPrecision(18, 6)
                        .HasColumnType("double")
                        .HasColumnName("last_lat")
                        .HasAnnotation("Relational:JsonPropertyName", "last_lat");

                    b.Property<double?>("LastLongitude")
                        .HasPrecision(18, 6)
                        .HasColumnType("double")
                        .HasColumnName("last_lon")
                        .HasAnnotation("Relational:JsonPropertyName", "last_lon");

                    b.Property<ulong?>("LastSeen")
                        .HasColumnType("bigint unsigned")
                        .HasColumnName("last_seen")
                        .HasAnnotation("Relational:JsonPropertyName", "last_seen");

                    b.HasKey("Uuid");

                    b.HasIndex("AccountUsername");

                    b.HasIndex("InstanceName");

                    b.HasIndex("LastSeen");

                    b.ToTable("device");
                });

            modelBuilder.Entity("ChuckDeviceController.Data.Entities.DeviceGroup", b =>
                {
                    b.Property<string>("Name")
                        .HasColumnType("varchar(255)")
                        .HasColumnName("name");

                    b.Property<string>("DeviceUuids")
                        .IsRequired()
                        .HasColumnType("longtext")
                        .HasColumnName("device_uuids");

                    b.HasKey("Name");

                    b.ToTable("device_group");
                });

            modelBuilder.Entity("ChuckDeviceController.Data.Entities.Geofence", b =>
                {
                    b.Property<string>("Name")
                        .HasColumnType("varchar(255)")
                        .HasColumnName("name")
                        .HasAnnotation("Relational:JsonPropertyName", "name");

                    b.Property<string>("Data")
                        .HasColumnType("longtext")
                        .HasColumnName("data")
                        .HasAnnotation("Relational:JsonPropertyName", "data");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasColumnType("longtext")
                        .HasColumnName("type")
                        .HasAnnotation("Relational:JsonPropertyName", "type");

                    b.HasKey("Name");

                    b.ToTable("geofence");
                });

            modelBuilder.Entity("ChuckDeviceController.Data.Entities.Instance", b =>
                {
                    b.Property<string>("Name")
                        .HasColumnType("varchar(255)")
                        .HasColumnName("name")
                        .HasAnnotation("Relational:JsonPropertyName", "name");

                    b.Property<string>("Data")
                        .HasColumnType("longtext")
                        .HasColumnName("data")
                        .HasAnnotation("Relational:JsonPropertyName", "data");

                    b.Property<string>("Geofences")
                        .IsRequired()
                        .HasColumnType("longtext")
                        .HasColumnName("geofences")
                        .HasAnnotation("Relational:JsonPropertyName", "geofences");

                    b.Property<ushort>("MaximumLevel")
                        .HasColumnType("smallint unsigned")
                        .HasColumnName("max_level")
                        .HasAnnotation("Relational:JsonPropertyName", "max_level");

                    b.Property<ushort>("MinimumLevel")
                        .HasColumnType("smallint unsigned")
                        .HasColumnName("min_level")
                        .HasAnnotation("Relational:JsonPropertyName", "min_level");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasColumnType("longtext")
                        .HasColumnName("type")
                        .HasAnnotation("Relational:JsonPropertyName", "type");

                    b.HasKey("Name");

                    b.ToTable("instance");
                });

            modelBuilder.Entity("ChuckDeviceController.Data.Entities.IvList", b =>
                {
                    b.Property<string>("Name")
                        .HasColumnType("varchar(255)")
                        .HasColumnName("name")
                        .HasAnnotation("Relational:JsonPropertyName", "name");

                    b.Property<string>("PokemonIds")
                        .IsRequired()
                        .HasColumnType("longtext")
                        .HasColumnName("pokemon_ids")
                        .HasAnnotation("Relational:JsonPropertyName", "pokemon_ids");

                    b.HasKey("Name");

                    b.ToTable("iv_list");
                });

            modelBuilder.Entity("ChuckDeviceController.Data.Entities.Plugin", b =>
                {
                    b.Property<string>("Name")
                        .HasColumnType("varchar(255)")
                        .HasColumnName("name");

                    b.Property<string>("FullPath")
                        .IsRequired()
                        .HasColumnType("longtext")
                        .HasColumnName("full_path");

                    b.Property<string>("State")
                        .IsRequired()
                        .HasColumnType("longtext")
                        .HasColumnName("state");

                    b.HasKey("Name");

                    b.ToTable("plugin");
                });

            modelBuilder.Entity("ChuckDeviceController.Data.Entities.Webhook", b =>
                {
                    b.Property<string>("Name")
                        .HasColumnType("varchar(255)")
                        .HasColumnName("name")
                        .HasAnnotation("Relational:JsonPropertyName", "name");

                    b.Property<string>("Data")
                        .HasColumnType("longtext")
                        .HasColumnName("data")
                        .HasAnnotation("Relational:JsonPropertyName", "data");

                    b.Property<double>("Delay")
                        .HasColumnType("double")
                        .HasColumnName("delay")
                        .HasAnnotation("Relational:JsonPropertyName", "delay");

                    b.Property<bool>("Enabled")
                        .HasColumnType("tinyint(1)")
                        .HasColumnName("enabled")
                        .HasAnnotation("Relational:JsonPropertyName", "enabled");

                    b.Property<string>("Geofences")
                        .IsRequired()
                        .HasColumnType("longtext")
                        .HasColumnName("geofences")
                        .HasAnnotation("Relational:JsonPropertyName", "geofences");

                    b.Property<int>("Types")
                        .HasColumnType("int")
                        .HasColumnName("types")
                        .HasAnnotation("Relational:JsonPropertyName", "types");

                    b.Property<string>("Url")
                        .IsRequired()
                        .HasColumnType("longtext")
                        .HasColumnName("url")
                        .HasAnnotation("Relational:JsonPropertyName", "url");

                    b.HasKey("Name");

                    b.ToTable("webhook");
                });

            modelBuilder.Entity("ChuckDeviceController.Data.Entities.Device", b =>
                {
                    b.HasOne("ChuckDeviceController.Data.Entities.Account", "Account")
                        .WithMany()
                        .HasForeignKey("AccountUsername");

                    b.HasOne("ChuckDeviceController.Data.Entities.Instance", "Instance")
                        .WithMany("Devices")
                        .HasForeignKey("InstanceName")
                        .OnDelete(DeleteBehavior.SetNull);

                    b.Navigation("Account");

                    b.Navigation("Instance");
                });

            modelBuilder.Entity("ChuckDeviceController.Data.Entities.Instance", b =>
                {
                    b.Navigation("Devices");
                });
#pragma warning restore 612, 618
        }
    }
}
