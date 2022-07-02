﻿namespace ChuckDeviceController.Geometry.Models
{
    public class BoundingBox
    {
        public double MinimumLatitude { get; set; } // minX

        public double MaximumLatitude { get; set; } // maxX

        public double MinimumLongitude { get; set; } // minY

        public double MaximumLongitude { get; set; } // maxY
    }
}