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
    [Migration("20220924025125_AddApiKeyEntity")]
    partial class AddApiKeyEntity
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "6.0.8")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            modelBuilder.Entity("ChuckDeviceController.Data.Entities.Account", b =>
                {
                    b.Property<string>("Username")
                        .HasColumnType("varchar(255)")
                        .HasColumnName("username");

                    b.Property<bool?>("Banned")
                        .HasColumnType("tinyint(1)")
                        .HasColumnName("banned");

                    b.Property<ulong?>("CreationTimestamp")
                        .HasColumnType("bigint unsigned")
                        .HasColumnName("creation_timestamp");

                    b.Property<string>("Failed")
                        .HasColumnType("longtext")
                        .HasColumnName("failed");

                    b.Property<ulong?>("FailedTimestamp")
                        .HasColumnType("bigint unsigned")
                        .HasColumnName("failed_timestamp");

                    b.Property<ulong?>("FirstWarningTimestamp")
                        .HasColumnType("bigint unsigned")
                        .HasColumnName("first_warning_timestamp");

                    b.Property<string>("GroupName")
                        .HasColumnType("longtext")
                        .HasColumnName("group");

                    b.Property<double?>("LastEncounterLatitude")
                        .HasColumnType("double")
                        .HasColumnName("last_encounter_lat");

                    b.Property<double?>("LastEncounterLongitude")
                        .HasColumnType("double")
                        .HasColumnName("last_encounter_lon");

                    b.Property<ulong?>("LastEncounterTime")
                        .HasColumnType("bigint unsigned")
                        .HasColumnName("last_encounter_time");

                    b.Property<ulong?>("LastUsedTimestamp")
                        .HasColumnType("bigint unsigned")
                        .HasColumnName("last_used_timestamp");

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
                        .HasColumnName("suspended_message_acknowledged");

                    b.Property<ushort>("Tutorial")
                        .HasColumnType("smallint unsigned")
                        .HasColumnName("tutorial");

                    b.Property<bool?>("Warn")
                        .HasColumnType("tinyint(1)")
                        .HasColumnName("warn");

                    b.Property<ulong?>("WarnExpireTimestamp")
                        .HasColumnType("bigint unsigned")
                        .HasColumnName("warn_expire_timestamp");

                    b.Property<bool?>("WarnMessageAcknowledged")
                        .HasColumnType("tinyint(1)")
                        .HasColumnName("warn_message_acknowledged");

                    b.Property<bool?>("WasSuspended")
                        .HasColumnType("tinyint(1)")
                        .HasColumnName("was_suspended");

                    b.HasKey("Username");

                    b.ToTable("account");
                });

            modelBuilder.Entity("ChuckDeviceController.Data.Entities.ApiKey", b =>
                {
                    b.Property<uint>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int unsigned")
                        .HasColumnName("id");

                    b.Property<ulong>("ExpirationTimestamp")
                        .HasColumnType("bigint unsigned")
                        .HasColumnName("expiration_timestamp");

                    b.Property<string>("Key")
                        .IsRequired()
                        .HasColumnType("longtext")
                        .HasColumnName("key");

                    b.Property<string>("Scope")
                        .IsRequired()
                        .HasColumnType("longtext")
                        .HasColumnName("scope");

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

                    b.HasKey("Name");

                    b.ToTable("assignment_group");
                });

            modelBuilder.Entity("ChuckDeviceController.Data.Entities.Device", b =>
                {
                    b.Property<string>("Uuid")
                        .HasColumnType("varchar(255)")
                        .HasColumnName("uuid");

                    b.Property<string>("AccountUsername")
                        .HasColumnType("longtext")
                        .HasColumnName("account_username");

                    b.Property<string>("InstanceName")
                        .HasColumnType("longtext")
                        .HasColumnName("instance_name");

                    b.Property<bool>("IsPendingAccountSwitch")
                        .HasColumnType("tinyint(1)")
                        .HasColumnName("pending_account_switch");

                    b.Property<string>("LastHost")
                        .HasColumnType("longtext")
                        .HasColumnName("last_host");

                    b.Property<double?>("LastLatitude")
                        .HasColumnType("double")
                        .HasColumnName("last_lat");

                    b.Property<double?>("LastLongitude")
                        .HasColumnType("double")
                        .HasColumnName("last_lon");

                    b.Property<ulong?>("LastSeen")
                        .HasColumnType("bigint unsigned")
                        .HasColumnName("last_seen");

                    b.HasKey("Uuid");

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
                        .HasColumnName("name");

                    b.Property<string>("Data")
                        .IsRequired()
                        .HasColumnType("longtext")
                        .HasColumnName("data");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasColumnType("longtext")
                        .HasColumnName("type");

                    b.HasKey("Name");

                    b.ToTable("geofence");
                });

            modelBuilder.Entity("ChuckDeviceController.Data.Entities.Instance", b =>
                {
                    b.Property<string>("Name")
                        .HasColumnType("varchar(255)")
                        .HasColumnName("name");

                    b.Property<string>("Data")
                        .HasColumnType("longtext")
                        .HasColumnName("data");

                    b.Property<string>("Geofences")
                        .IsRequired()
                        .HasColumnType("longtext")
                        .HasColumnName("geofences");

                    b.Property<ushort>("MaximumLevel")
                        .HasColumnType("smallint unsigned")
                        .HasColumnName("max_level");

                    b.Property<ushort>("MinimumLevel")
                        .HasColumnType("smallint unsigned")
                        .HasColumnName("min_level");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasColumnType("longtext")
                        .HasColumnName("type");

                    b.HasKey("Name");

                    b.ToTable("instance");
                });

            modelBuilder.Entity("ChuckDeviceController.Data.Entities.IvList", b =>
                {
                    b.Property<string>("Name")
                        .HasColumnType("varchar(255)")
                        .HasColumnName("name");

                    b.Property<string>("PokemonIds")
                        .IsRequired()
                        .HasColumnType("longtext")
                        .HasColumnName("pokemon_ids");

                    b.HasKey("Name");

                    b.ToTable("iv_list");
                });

            modelBuilder.Entity("ChuckDeviceController.Data.Entities.Plugin", b =>
                {
                    b.Property<string>("Name")
                        .HasColumnType("varchar(255)")
                        .HasColumnName("name");

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
                        .HasColumnName("name");

                    b.Property<string>("Data")
                        .HasColumnType("longtext")
                        .HasColumnName("data");

                    b.Property<double>("Delay")
                        .HasColumnType("double")
                        .HasColumnName("delay");

                    b.Property<bool>("Enabled")
                        .HasColumnType("tinyint(1)")
                        .HasColumnName("enabled");

                    b.Property<string>("Geofences")
                        .IsRequired()
                        .HasColumnType("longtext")
                        .HasColumnName("geofences");

                    b.Property<string>("Types")
                        .IsRequired()
                        .HasColumnType("longtext")
                        .HasColumnName("types");

                    b.Property<string>("Url")
                        .IsRequired()
                        .HasColumnType("longtext")
                        .HasColumnName("url");

                    b.HasKey("Name");

                    b.ToTable("webhook");
                });
#pragma warning restore 612, 618
        }
    }
}
