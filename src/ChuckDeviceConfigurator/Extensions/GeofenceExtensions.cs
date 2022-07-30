﻿namespace ChuckDeviceConfigurator.Extensions
{
    using ChuckDeviceController.Data.Entities;
    using ChuckDeviceController.Extensions.Json;
    using ChuckDeviceController.Geometry.Models;

    public static class GeofenceExtensions
    {
        public static List<Coordinate> ConvertToCoordinates(this IReadOnlyList<Geofence> geofences)
        {
            var coords = new List<Coordinate>();
            foreach (var geofence in geofences)
            {
                var result = ConvertToCoordinates(geofence);
                if (result == null)
                    continue;

                coords.AddRange(result);
            }
            return coords;
        }

        public static List<Coordinate> ConvertToCoordinates(this Geofence geofence)
        {
            var coords = new List<Coordinate>();
            var area = geofence.Data?.Area;
            if (area is null)
            {
                Console.WriteLine($"Failed to parse geofence '{geofence.Name}' coordinates");
                return null;
            }
            string areaJson = Convert.ToString(area);
            var coordsArray = (List<Coordinate>)
            (
                area is List<Coordinate>
                    ? area
                    : areaJson.FromJson<List<Coordinate>>()
            );
            coords.AddRange(coordsArray);
            return coords;
        }

        public static (List<MultiPolygon>, List<List<Coordinate>>) ConvertToMultiPolygons(
            this IReadOnlyList<Geofence> geofences)
        {
            var multiPolygons = new List<MultiPolygon>();
            var coordinates = new List<List<Coordinate>>();
            foreach (var geofence in geofences)
            {
                var result = ConvertToMultiPolygons(geofence);
                if (result.Item1 == null || result.Item2 == null)
                    continue;

                multiPolygons.AddRange(result.Item1);
                coordinates.AddRange(result.Item2);
            }
            return (multiPolygons, coordinates);
        }

        public static (List<MultiPolygon>, List<List<Coordinate>>) ConvertToMultiPolygons(
            this Geofence geofence)
        {
            var multiPolygons = new List<MultiPolygon>();
            var coordinates = new List<List<Coordinate>>();

            var area = geofence.Data?.Area;
            if (area is null)
            {
                Console.WriteLine($"Failed to parse coordinates for geofence '{geofence.Name}'");
                return default;
            }
            string areaJson = Convert.ToString(area);
            var coordsArray = (List<List<Coordinate>>)
            (
                area is List<List<Coordinate>>
                    ? area
                    : areaJson.FromJson<List<List<Coordinate>>>()
            );
            coordinates.AddRange(coordsArray);

            var areaArrayEmptyInner = new List<MultiPolygon>();
            foreach (var coordList in coordsArray)
            {
                var multiPolygon = new MultiPolygon();
                Coordinate? first = null;
                Coordinate? last = null;
                for (var i = 0; i < coordList.Count; i++)
                {
                    var coord = coordList[i];
                    if (i == 0)
                        first = coord;
                    else if (i == coordList.Count - 1)
                        last = coord;

                    multiPolygon.Add(new Polygon(coord.Latitude, coord.Longitude));
                }
                // Check if the first and last coordinates are not null and are the same, if
                // not add the first coordinate to the end of the list
                if (first != null && last != null && first.CompareTo(last) != 0)
                {
                    // Insert first coordinate at the end of the list
                    multiPolygon.Add(new Polygon(first.Latitude, first.Longitude));
                }
                areaArrayEmptyInner.Add(multiPolygon);
            }
            multiPolygons.AddRange(areaArrayEmptyInner);
            return (multiPolygons, coordinates);
        }
    }
}