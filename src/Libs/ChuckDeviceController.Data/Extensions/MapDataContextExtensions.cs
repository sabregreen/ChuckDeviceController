﻿namespace ChuckDeviceController.Data.Extensions
{
    using System;
    using System.Collections.Generic;

    using ChuckDeviceController.Data.Contexts;
    using ChuckDeviceController.Data.Entities;
    using ChuckDeviceController.Geometry;
    using ChuckDeviceController.Geometry.Models;

    public static class MapDataContextExtensions
    {
        #region Clear Quests

        /// <summary>
        /// Clear all Pokestops with quests
        /// </summary>
        /// <param name="context"></param>
        public static async Task ClearQuestsAsync(this MapDataContext context)
        {
            var pokestopIds = context.Pokestops.Select(stop => stop.Id)
                                               .ToList();
            await ClearQuestsAsync(context, pokestopIds);
        }

        /// <summary>
        /// Clear all Pokestops with quests by Pokestop IDs
        /// </summary>
        /// <param name="context"></param>
        /// <param name="pokestopIds"></param>
        public static async Task ClearQuestsAsync(this MapDataContext context, IEnumerable<string> pokestopIds)
        {
            var pokestopsToUpsert = new List<Pokestop>();
            var pokestops = GetPokestopsByIds(context, pokestopIds);
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

                pokestopsToUpsert.Add(pokestop);
            });
            await context.Pokestops.BulkUpdateAsync(pokestopsToUpsert, options =>
            {
                options.TemporaryTableUseTableLock = true;
                options.UseTableLock = true;
                options.ColumnPrimaryKeyExpression = p => p.Id;
                options.ColumnInputExpression = p => new
                {
                    p.Id,

                    p.QuestConditions,
                    p.QuestRewards,
                    p.QuestTarget,
                    p.QuestTemplate,
                    p.QuestTimestamp,
                    p.QuestTitle,
                    p.QuestType,

                    p.AlternativeQuestConditions,
                    p.AlternativeQuestRewards,
                    p.AlternativeQuestTarget,
                    p.AlternativeQuestTemplate,
                    p.AlternativeQuestTimestamp,
                    p.AlternativeQuestTitle,
                    p.AlternativeQuestType,
                };
            });
        }

        /// <summary>
        /// Clear all Pokestop quests within geofences
        /// </summary>
        /// <param name="context"></param>
        /// <param name="multiPolygons"></param>
        public static async Task ClearQuestsAsync(this MapDataContext context, IEnumerable<MultiPolygon> multiPolygons)
        {
            foreach (var multiPolygon in multiPolygons)
            {
                await ClearQuestsAsync(context, multiPolygon);
            }
        }

        /// <summary>
        /// Clear all Pokestop quests within geofence
        /// </summary>
        /// <param name="context"></param>
        /// <param name="multiPolygon"></param>
        public static async Task ClearQuestsAsync(this MapDataContext context, MultiPolygon multiPolygon)
        {
            // TODO: Use GeofenceService.InMultiPolygon instead
            var bbox = multiPolygon.GetBoundingBox();
            await ClearQuestsAsync(context, bbox);
        }

        /// <summary>
        /// Clear all Pokestop quests within bounding box
        /// </summary>
        /// <param name="context"></param>
        /// <param name="bbox"></param>
        public static async Task ClearQuestsAsync(this MapDataContext context, BoundingBox bbox)
        {
            var pokestopIds = context.Pokestops.Where(stop =>
                stop.Latitude >= bbox.MinimumLatitude &&
                stop.Longitude >= bbox.MinimumLongitude &&
                stop.Latitude <= bbox.MaximumLatitude &&
                stop.Longitude <= bbox.MaximumLongitude
            )
                .Select(stop => stop.Id)
                .ToList();
            await ClearQuestsAsync(context, pokestopIds);
        }

        #endregion

        #region Pokestops

        public static async Task<ulong> GetPokestopQuestCountAsync(this MapDataContext context, List<string> pokestopIds, QuestMode mode)
        {
            if (pokestopIds.Count > 10000)
            {
                // TODO: Benchmark if batching is necessary with Z.EntityFramework library (which already has it's own batching options/logic)
                var result = 0ul;
                var batchSize = Convert.ToInt64(Math.Ceiling(Convert.ToDouble(pokestopIds.Count) / 10000.0));
                for (var i = 0; i < batchSize; i++)
                {
                    var start = 10000 * i;
                    var end = Math.Max(10000 * i, pokestopIds.Count - 1);
                    var splice = pokestopIds.GetRange(start, end);
                    var spliceResult = await GetPokestopQuestCountAsync(context, splice, mode);
                    result += spliceResult;
                }
                return result;
            }

            var pokestops = GetPokestopsByIds(context, pokestopIds);
            var count = pokestops.LongCount(stop => HasPokestopQuestByType(stop, mode));
            return await Task.FromResult(Convert.ToUInt64(count));
        }

        public static async Task<List<Pokestop>> GetPokestopsInBoundsAsync(this MapDataContext context, BoundingBox bbox, bool isEnabled = true)
        {
            var pokestops = context.Pokestops.Where(stop =>
                stop.Latitude >= bbox.MinimumLatitude &&
                stop.Longitude >= bbox.MinimumLongitude &&
                stop.Latitude <= bbox.MaximumLatitude &&
                stop.Longitude <= bbox.MaximumLongitude &&
                isEnabled && stop.IsEnabled &&
                //    ? stop.IsEnabled
                //    : stop.IsEnabled || !stop.IsEnabled &&
                !stop.IsDeleted
            ).ToList();
            return await Task.FromResult(pokestops);
        }

        public static List<Pokestop> GetPokestopsByIds(this MapDataContext context, IEnumerable<string> pokestopIds, bool isEnabled = true, bool isDeleted = false)
        {
            var pokestops = context.Pokestops
                                   .Where(pokestop => pokestopIds.Contains(pokestop.Id))
                                   .Where(pokestop => isEnabled == pokestop.IsEnabled)
                                   .Where(pokestop => isDeleted == pokestop.IsDeleted)
                                   .ToList();
            return pokestops;
        }

        #endregion

        #region S2 Cells

        /// <summary>
        /// Get all S2 cells
        /// </summary>
        /// <param name="context"></param>
        public static async Task<List<Cell>> GetS2CellsAsync(this MapDataContext context)
        {
            var cells = context.Cells.ToList();
            return await Task.FromResult(cells);
        }

        /// <summary>
        /// Get all S2 cells within bounding box
        /// </summary>
        /// <param name="context"></param>
        /// <param name="bbox"></param>
        public static async Task<List<Cell>> GetS2CellsAsync(this MapDataContext context, BoundingBox bbox)
        {
            var cells = context.Cells.Where(cell =>
                cell.Latitude >= bbox.MinimumLatitude &&
                cell.Longitude >= bbox.MinimumLongitude &&
                cell.Latitude <= bbox.MaximumLatitude &&
                cell.Longitude <= bbox.MaximumLongitude
            ).ToList();
            return await Task.FromResult(cells);
        }

        /// <summary>
        /// Get all S2 cells within geofence polygon
        /// </summary>
        /// <param name="context"></param>
        /// <param name="multiPolygon"></param>
        /// <returns></returns>
        public static async Task<List<Cell>> GetS2CellsAsync(this MapDataContext context, MultiPolygon multiPolygon)
        {
            var bbox = multiPolygon.GetBoundingBox();
            // Get S2 cells within geofence bounding box
            var bboxCells = await GetS2CellsAsync(context, bbox);
            // Filter S2 cells outside of geofence polygon
            var cellsInArea = bboxCells.Where(cell => GeofenceService.InPolygon(multiPolygon, cell.ToCoordinate()))
                                       .ToList();
            return cellsInArea;
        }

        /// <summary>
        /// Get all S2 cells within geofence polygons
        /// </summary>
        /// <param name="context"></param>
        /// <param name="multiPolygons"></param>
        /// <returns></returns>
        public static async Task<List<Cell>> GetS2CellsAsync(this MapDataContext context, IEnumerable<MultiPolygon> multiPolygons)
        {
            var cells = new List<Cell>();
            foreach (var multiPolygon in multiPolygons)
            {
                var list = await GetS2CellsAsync(context, multiPolygon);
                cells.AddRange(list);
            }
            return cells;
        }

        #endregion

        #region Private Methods

        private static bool HasPokestopQuestByType(Pokestop pokestop, QuestMode mode)
        {
            var result = mode == QuestMode.Normal
                ? pokestop.QuestType != null
                : mode == QuestMode.Alternative
                    ? pokestop.AlternativeQuestType != null
                    : pokestop.QuestType != null || pokestop.AlternativeQuestType != null;
            return result;
        }

        #endregion
    }
}