using System;

namespace RestDemo
{
    //Use Json2Csharp.com to convert the json payloads from the documentation into a c# class
    //Then you can just deserialize whatever you want :)
    public class ActiveUnitData
    {
        public int StagingAreaId { get; set; }
        public bool IsSharedCrew { get; set; }
        public DateTime StatusChangeTime { get; set; }
        public string ChangeComment { get; set; }
        public string FacilityEntranceId { get; set; }
        public int SymbolNumber { get; set; }
        public int UpdateCount { get; set; }
        public string OldAgencyId { get; set; }
        public double TransportLatitude { get; set; }
        public double Latitude { get; set; }
        public string OldDispatchGroup { get; set; }
        public int TotalPatients { get; set; }
        public string UnitId { get; set; }
        public int ShiftStartDelta { get; set; }
        public string UnitType { get; set; }
        public string DispatchGroup { get; set; }
        public int MaxEnrouteTime { get; set; }
        public bool AreEmployeesTracked { get; set; }
        public int TotalEventCount { get; set; }
        public string TransportLocation { get; set; }
        public int MaxDispatchTime { get; set; }
        public int Status { get; set; }
        public bool IsAvailableForRecommend { get; set; }
        public string Mileage { get; set; }
        public int DelayTime { get; set; }
        public double Longitude { get; set; }
        public string StationId { get; set; }
        public int TotalStationMoves { get; set; }
        public string DispatchNumber { get; set; }
        public string LineupName { get; set; }
        public string ExtendedUnitAttributes { get; set; }
        public double UnitHealth { get; set; }
        public string GroupLeaderUnitId { get; set; }
        public bool HasBeenTemporarilyTransferred { get; set; }
        public string RelocatedCrewId { get; set; }
        public int TransportPriority { get; set; }
        public string CrewId { get; set; }
        public bool IsOutOfComplianceIfDispatched { get; set; }
        public bool IsInEmergency { get; set; }
        public int MaxAcknowledgeTime { get; set; }
        public int StatusedCommonEventId { get; set; }
        public string OutOfServiceTypeCode { get; set; }
        public int TotalUnavailableTime { get; set; }
        public int ShiftDuration { get; set; }
        public string Warning { get; set; }
        public bool HasLocationChanged { get; set; }
        public string LockingTerminal { get; set; }
        public string AgencyId { get; set; }
        public bool HasFailedToRespond { get; set; }
        public DateTime LogonTime { get; set; }
        public bool IsLateRun { get; set; }
        public bool IsLoggedOffOnClear { get; set; }
        public string Beat { get; set; }
        public int TotalEventTime { get; set; }
        public DateTime ShiftStart { get; set; }
        public bool IsUnavailable { get; set; }
        public string AttributesOrEquipmentChanged { get; set; }
        public bool HasAmCourtAppearance { get; set; }
        public string StatusedAgencyEventId { get; set; }
        public bool HasPmCourtAppearance { get; set; }
        public int AlarmTime { get; set; }
        public int MaxWeight { get; set; }
        public int MaxWidth { get; set; }
        public string PermanentLocation { get; set; }
        public string PermanentStationId { get; set; }
        public string RoleDesignator { get; set; }
        public string CustomData { get; set; }
        public int DispatchAlarmLevel { get; set; }
        public bool HasNameChanged { get; set; }
        public string StatusedAgencyEventTypeCode { get; set; }
        public bool HasAlarmSounded { get; set; }
        public string CoveringUnitId { get; set; }
        public string Zone { get; set; }
        public int DefaultAvailableStatus { get; set; }
        public bool IsLocked { get; set; }
        public string VehicleId { get; set; }
        public double TransportLongitude { get; set; }
        public int TotalEmergencyEventCount { get; set; }
        public int TotalTransports { get; set; }
        public int TotalAvailableStationTime { get; set; }
        public string StatusedAgencyEventSubtypeCode { get; set; }
        public string AssignedAgencyEventId { get; set; }
        public string SpecialContact { get; set; }
        public int MaxArriveTime { get; set; }
        public string Bay { get; set; }
        public int MaxHeight { get; set; }
        public int NumberOfPatients { get; set; }
        public string Location { get; set; }
    }
}