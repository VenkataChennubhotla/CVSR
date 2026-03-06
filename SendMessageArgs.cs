using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DeccanDemo
{
    public class SendMessageArgs
    {
        /// <summary>
        /// Gets or Sets Subject
        /// </summary>
        /// <value>Message subject line</value>
        public string Subject { get; set; }

        /// <summary>
        /// Gets or Sets MessageBody
        /// </summary>
        /// <value>The list of comma-separated strings comprising the message body</value>
        public List<string> MessageBody { get; set; } = new List<string>();

        /// <summary>
        /// Gets or Sets ScheduleAt
        /// </summary>
        /// <value>Send message at a later time</value>
        public DateTimeOffset? ScheduleAt { get; set; }

        /// <summary>
        /// Gets or Sets AgencyEventId
        /// </summary>
        /// <value>Event number to reference in the message</value>
        public string AgencyEventId { get; set; }

        /// <summary>
        /// Gets or Sets UnitForEvent
        /// </summary>
        /// <value>Reference the event assigned to this unit in the message</value>
        public string UnitForEvent { get; set; }

        /// <summary>
        /// Gets or Sets Priority
        /// </summary>
        /// <value>Message priority</value>
        public int? Priority { get; set; }

        /// <summary>
        /// Gets or Sets IsReceiptRequested
        /// </summary>
        /// <value>True if receipt is requested</value>
        public bool? IsReceiptRequested { get; set; }

        /// <summary>
        /// Gets or Sets NoAttachments
        /// </summary>
        /// <value>Do not send attachments from event</value>
        public bool? NoAttachments { get; set; }

        /// <summary>
        /// Gets or Sets LastNames
        /// </summary>
        /// <value>The comma-separated last names of employee recipients</value>
        public List<string> LastNames { get; set; } = new List<string>();

        /// <summary>
        /// Gets or Sets AlternateEmpIDs
        /// </summary>
        /// <value>The comma-separated alternate IDs of employee recipients</value>
        public List<string> AlternateEmpIDs { get; set; } = new List<string>();

        /// <summary>
        /// Gets or Sets EmpIDs
        /// </summary>
        /// <value>The comma-separated IDs of employee recipients</value>
        public List<string> EmpIDs { get; set; } = new List<string>();

        /// <summary>
        /// Gets or Sets Emails
        /// </summary>
        /// <value>The comma-separated email addresses of recipients</value>
        public List<string> Emails { get; set; } = new List<string>();

        /// <summary>
        /// Gets or Sets AlphaPagers
        /// </summary>
        /// <value>The comma-separated IDs of alphanumeric pager recipients</value>
        public List<string> AlphaPagers { get; set; } = new List<string>();

        /// <summary>
        /// Gets or Sets PersonalPagers
        /// </summary>
        /// <value>The comma-separated IDs of personal pager recipients</value>
        public List<string> PersonalPagers { get; set; } = new List<string>();

        /// <summary>
        /// Gets or Sets StationPrintQueues
        /// </summary>
        /// <value>The comma-separated IDs of station print queue recipients</value>
        public List<string> StationPrintQueues { get; set; } = new List<string>();

        /// <summary>
        /// Gets or Sets StationPagers
        /// </summary>
        /// <value>The comma-separated IDs of station pager recipients</value>
        public List<string> StationPagers { get; set; } = new List<string>();

        /// <summary>
        /// Gets or Sets Groups
        /// </summary>
        /// <value>The comma-separated IDs of static or dynamic recipients</value>
        public List<string> Groups { get; set; } = new List<string>();

        /// <summary>
        /// Gets or Sets Terminals
        /// </summary>
        /// <value>The comma-separated terminal or unit recipients</value>
        public List<string> Terminals { get; set; } = new List<string>();

        /// <summary>
        /// Gets or Sets ReplyMessageId
        /// </summary>
        /// <value>The id of the message to replay</value>
        public int? ReplyMessageId { get; set; }
    }
}
