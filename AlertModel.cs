using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace AlertsDemo
{
    /// <summary>
    /// The type of resource that was supplied
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1008:EnumsShouldHaveZeroValue")]
    public enum ResourceTypes
    {
        /// None
        None,
        /// AgencyEventId
        AgencyEventId,
        /// UnitId
        UnitId,
        /// ApplicationName
        ApplicationName,
        /// Other
        Other
    };

    /// <summary>
    /// The type of alert that will be created. This determines which category it will appear on in the panel
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1008:EnumsShouldHaveZeroValue")]
    public enum AlertTypes
    {
        /// Unit Alert
        Unit,
        /// Event Alert
        Event,
        /// System Alert
        System,
        /// Other Alerts
        Other
    };
	/// <summary>
	/// The Alert visibility.
	/// </summary>	
	public class AlertVisibility
	{
		private List<string> _groups = new List<string>();

		/// <summary>
		/// Groups which see an alert.
		/// </summary>
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1002:DoNotExposeGenericLists")]
		[JsonProperty(PropertyName = "groups")]
		public List<string> Groups { get { return _groups; } }

		private List<AlertEmployeeData> _employees = new List<AlertEmployeeData>();

		/// <summary>
		/// Employees which see an alert.
		/// </summary>
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1002:DoNotExposeGenericLists")]
		[JsonProperty(PropertyName = "employees")]
		public List<AlertEmployeeData> Employees { get { return _employees; } }

	}
	/// <summary>
	/// Defines an employee and the employee's 'flagged' state.
	/// </summary>	
	public class AlertEmployeeData
	{
		private bool _isFlagged;

		/// <summary>
		/// Flag which allows consumers to filter on only flagged alerts, e.g. a user can flag an alert and filter on the flag.
		/// </summary>
		[JsonProperty(PropertyName = "isFlagged", Required = Required.Always)]
		public bool IsFlagged { get { return _isFlagged; } set { _isFlagged = value; } }

		private int _employeeId;

		/// <summary>
		/// The EmployeeId.
		/// </summary>
		[JsonProperty(PropertyName = "employeeId", Required = Required.Always)]
		public int EmployeeId { get { return _employeeId; } set { _employeeId = value; } }

	}
	public class AlertModel
    {
		/// <summary>
		/// Unique alert ID.
		/// </summary>
		int AlertId { get; set; }

		/// <summary>
		/// Short title of the alert
		/// </summary>
		string Subject { get; set; }

		/// <summary>
		/// Longer message of the alert
		/// </summary>
		string Message { get; set; }

		/// <summary>
		/// alertType
		/// </summary>
		AlertTypes AlertType { get; set; }

		/// <summary>
		/// visibility
		/// </summary>
		AlertVisibility Visibility { get; set; }

		/// <summary>
		/// The priority of this alert 0-9
		/// </summary>
		int Priority { get; set; }

		/// <summary>
		/// A UnitId, AgencyEventId, Application name, or another resource that is related to this alert. Not required.
		/// </summary>
		string Resource { get; set; }

		/// <summary>
		/// resourceType
		/// </summary>
		ResourceTypes ResourceType { get; set; }

		/// <summary>
		/// The terminal/computer name the application is executing on. This can be used in conjunction with the 'resource'. Not required.
		/// </summary>
		string ApplicationTerminal { get; set; }

		/// <summary>
		/// The name the application. This can be used in conjunction with the 'ApplicationTerminal'. Not required.
		/// </summary>
		string ApplicationName { get; set; }

		/// <summary>
		/// The timer associated with the alert. Optional.
		/// </summary>
		long? TimerId { get; set; }

		/// <summary>
		/// DateTime when the alert was created.
		/// </summary>
		DateTimeOffset CreatedTime { get; set; }

		/// <summary>
		/// Last update datetime. Optional.
		/// </summary>
		DateTimeOffset? UpdatedTime { get; set; }

		/// <summary>
		/// JSON key value pairs representing custom data.
		/// </summary>
		string CustomData { get; set; }
	}
}
