﻿namespace ChuckDeviceController.Data.Factories
{
    using System;

    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

    using ChuckDeviceController.Data.Contexts;
    using ChuckDeviceController.Extensions;

    public static class DbContextFactory
    {
        public static DeviceControllerContext CreateDeviceControllerContext(string connectionString)// where T : DbContext
        {
            try
            {
                var optionsBuilder = new DbContextOptionsBuilder<DeviceControllerContext>();
                optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

                var ctx = new DeviceControllerContext(optionsBuilder.Options);
                //ctx.ChangeTracker.AutoDetectChangesEnabled = false;
                return ctx;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RawSql] Result: {ex.Message}");
                Environment.Exit(0);
            }
            return null;
        }

        public static ValueConverter<T, string> CreateJsonValueConverter<T>()
        {
            return new ValueConverter<T, string>
            (
                v => v.ToJson(true),
                v => v.FromJson<T>()
            );
        }
    }
}