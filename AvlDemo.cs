using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RestDemo
{
    public class AvlDemoParams
    {
        public int Packets { get; set; }
        public bool SendDevice { get; set; }
        public bool SendUnit { get; set; }
        public bool SendEmployee { get; set; }
        public Unit Unit { get; set; }
        public Employee Employee { get; set; }
        public Device Device { get; set; }
        public Incrementlatlon IncrementLatLon { get; set; }
    }

    public class Unit
    {
        public string Id { get; set; }
        public string DeviceId { get; set; }
        public int DeviceType { get; set; }
        public Incrementlatlon IncrementLatLon { get; set; }
    }

    public class Employee
    {
        public string Id { get; set; }
        public string DeviceId { get; set; }
        public int DeviceType { get; set; }
        public Incrementlatlon IncrementLatLon { get; set; }
    }

    public class Device
    {
        public string Id { get; set; }
    }

    public class Incrementlatlon
    {
        public Startinglatlon StartingLatLon { get; set; }
        public Incrementby IncrementBy { get; set; }
        public int Delay { get; set; }
    }

    public class Startinglatlon
    {
        public float Latitude { get; set; }
        public float Longitude { get; set; }
    }

    public class Incrementby
    {
        public float Latitude { get; set; }
        public float Longitude { get; set; }
    }
}
