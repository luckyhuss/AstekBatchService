using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AstekBatchService.Scheduler
{
    class ScheduledTask
    {
        // -- Time (24H);Task type;Task name;Machine name
        // 08:00;FILESYNC;SPID-OCEANE;ASTEKPC54

        // GSHEET|FDT|FILESYNC {BatchName}|SYNCTOY {SyncName}|MAILLOG

        private DateTime _time;        
        private readonly char _timeSplitter = ':';

        public ScheduledTask(string time, string frequency, string type, string name, string machine)
        {
            SetTime = time;
            Frequency = frequency;
            Type = type;
            Name = name;
            Machine = machine;
        }

        private string SetTime
        {
            set
            {
                if (value.Contains(_timeSplitter))
                {
                    DateTime dtEvent = new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day, 
                        Convert.ToInt32(value.Split(_timeSplitter)[0]), Convert.ToInt32(value.Split(_timeSplitter)[1]), 0);
                    _time = dtEvent;
                }
                else
                {
                    _time = DateTime.MinValue;
                }
            }
        }

        public DateTime Time
        {
            get { return _time; }
        }

        public string Frequency
        {
            get;
            private set;
        }

        public string Type
        {
            get;
            private set;
        }

        public string Name
        {
            get;
            private set;
        }

        public string Machine
        {
            get;
            private set;
        }
    }
}
