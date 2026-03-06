using System;
using System.Collections.Generic;
using System.Text;

namespace RestDemo
{
    public class AvlObject 
    {
        public double? Latitude { get; private set; }

        /// <summary>
        /// Longitude of the reported location in decimal degrees where + values are East.
        /// </summary>
        public double? Longitude { get; private set; }

        /// <summary>
        /// Meters above mean sea level where + values are above sea level.
        /// </summary>
        public double? Altitude { get; private set; }

        /// <summary>
        /// Speed along the heading in kilometers-per-hour where + values are Forward.
        /// </summary>
        public double? Speed { get; private set; }

        /// <summary>
        /// Speed in the vertical direction in kilometers-per-hour where + values are Up.
        /// </summary>
        public double? SpeedVertical { get; private set; }

        /// <summary>
        /// Heading clockwise from true North in decimal degrees.
        /// </summary>
        public double? Heading { get; private set; }

        /// <summary>
        /// The Unix Epoch time of this GPS update. If not provided GPSTime is assumed to be when this function was called.
        /// </summary>
        public long? GpsTime { get; private set; }

        /// <summary>
        /// Number of satellites used to determine the location
        /// </summary>
        public int? Satellites { get; private set; }

        /// <summary>
        /// Accuracy or precision of the location in meters
        /// </summary>
        public double? Accuracy { get; private set; }

        /// <summary>
        /// The filter to use in the packet so that the message is sent to the correct instance of Tracker. Defaults to wild if not provided.
        /// </summary>
        public string Filter { get; private set; }

        /// <summary>
        /// GPS formats/protocols. 0&#x3D;UNKNOWN, 1&#x3D;TAIP, 2&#x3D;NMEA, 3&#x3D;OSKY, 4&#x3D;BLUE, 5&#x3D;RNAP, 6&#x3D; OMAMLP, 7&#x3D;CUSTOM
        /// </summary>
        public string GpsFormat { get; private set; }

        /// <summary>
        /// Type of accuracy provided. 0 &#x3D; UNKNOWN , 1&#x3D;DOP(Dilution of precision), 2&#x3D;MTR(Accuracy is in meters)
        /// </summary>
        public string AccuracyType { get; private set; }

        /// <summary>
        /// GPS time type. 0 &#x3D; UNKNOWN , 1&#x3D;SSM(Seconds since midnight), 2&#x3D;SEC(Epoch seconds), 3&#x3D;MLS(Epoch milliseconds)\&quot;
        /// </summary>
        public string GpsTimeType { get; private set; }

        /// <summary>
        /// The Device Type of the ID. Defaults to not used. Device type. 0 &#x3D; Unknown, 1 &#x3D; Other, 2 &#x3D; GPS Device, 3 &#x3D; Radio Alias, 4 &#x3D; HT Radio, 5 &#x3D; SMT, or 6 &#x3D; MDT, 7 &#x3D; Notification Device. The cad DeviceType parameter list maps the device type number to a configurable display name.
        /// </summary>
        public int? DeviceType { get; private set; }

        public AvlObject(double? latitude, double? longitude, double? altitude, double? speed, double? speedVertical, double? heading, long? gpsTime, int? satellites, double? accuracy, string filter, string gpsFormat, string accuracyType, string gpsTimeType, int? deviceType)
        {
            Latitude = latitude;
            Longitude = longitude;
            Altitude = altitude;
            Speed = speed;
            SpeedVertical = speedVertical;
            Heading = heading;
            GpsTime = gpsTime;
            Satellites = satellites;
            Accuracy = accuracy;
            Filter = filter;
            GpsFormat = gpsFormat;
            AccuracyType = accuracyType;
            GpsTimeType = gpsTimeType;
            DeviceType = deviceType;
        }
    }
}
