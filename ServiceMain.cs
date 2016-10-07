using AstekBatchService.Scheduler;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AstekBatchService
{
    public partial class ServiceMain : ServiceBase
    {
        #region Params
        private static Type __type = System.Reflection.MethodBase.GetCurrentMethod().DeclaringType;

        private System.Timers.Timer timerDelay;
        private Manager manager = new Manager();
        private DateTime serviceStart = DateTime.MinValue;
        FileSystemWatcher schedulerWatcher = new FileSystemWatcher();
        FileSystemWatcher syncFileWatcher = new FileSystemWatcher(); 
        #endregion

        #region Initializer
        public ServiceMain()
        {
            Manager.Log(__type, String.Format("Service initializing by {0}...", WindowsIdentity.GetCurrent().Name));

            InitializeComponent();
            
            // get base directory path
            manager.basePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().CodeBase);
            manager.basePath = new Uri(manager.basePath).LocalPath;
            
            // set file watcher for scheduler and sync file
            schedulerWatcher.Path = syncFileWatcher.Path = manager.basePath;
            schedulerWatcher.Filter = "Scheduler.ini";
            syncFileWatcher.Filter = "FileSync.ini";
            schedulerWatcher.NotifyFilter = syncFileWatcher.NotifyFilter = NotifyFilters.LastWrite;
            schedulerWatcher.Changed += new FileSystemEventHandler(OnChangedScheduler);
            syncFileWatcher.Changed += new FileSystemEventHandler(OnChangedSyncFiles);
            schedulerWatcher.EnableRaisingEvents = syncFileWatcher.EnableRaisingEvents = true;

            // set timer
            timerDelay = new System.Timers.Timer(60000); // check time every minute
            timerDelay.Elapsed += new System.Timers.ElapsedEventHandler(timerDelay_Elapsed);

            Manager.Log(__type, "File watchers and timers are set");

            // check if servers are OK
            manager.CheckServers();
        }
        #endregion

        #region Action launcher
        void timerDelay_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            // check if Saturday or Sunday
            if (DateTime.Today.DayOfWeek == DayOfWeek.Saturday || DateTime.Today.DayOfWeek == DayOfWeek.Sunday)
                return;

            foreach (var scheduledTask in manager.scheduledTasks)
            {
                // check machine name first
                if (!Environment.MachineName.Equals(scheduledTask.Machine))
                    continue;
                
                DateTime dtEvent = new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day, scheduledTask.Time.Hour, scheduledTask.Time.Minute, 0);
                DateTime dtNow = new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day, DateTime.Now.Hour, DateTime.Now.Minute, 0);

                if ("Hourly".Equals(scheduledTask.Frequency))
                {
                    // set hour to current hour
                    dtEvent = new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day, DateTime.Now.Hour, scheduledTask.Time.Minute, 0);
                }
                else if (!"Daily".Equals(scheduledTask.Frequency))
                {                    
                    DateTime plannedDate = new DateTime();
                    if (DateTime.TryParse(scheduledTask.Frequency, out plannedDate))
                    {
                        // specific date 07/10/2016

                        // adjust time by -5min
                        dtEvent = dtEvent.AddMinutes(-5);

                        // check if today is not that day
                        if (DateTime.Today != plannedDate)
                        {
                            // do not process this "date" task
                            continue;
                        }
                    }
                    else
                    {
                        // therefore weekly tasks, check if day is correct
                        DayOfWeek plannedDay = (DayOfWeek)Enum.Parse(typeof(DayOfWeek), scheduledTask.Frequency);
                        
                        if (DateTime.Today.DayOfWeek != plannedDay)
                        {
                            // do not process this weekly task
                            continue;
                        }
                    }
                }

                // check time now
                if (dtNow.Equals(dtEvent))
                {
                    // check if merle && diocletien are available; connect to merle && diocletien if needed
                    //ConnectToServers();

                    Manager.Log(__type, String.Format("Task : {0}/{1}", scheduledTask.Type, scheduledTask.Name));

                    // check type of batch to launch
                    // e.g. --type GSHEET
                    // e.g. --type FDT
                    switch (scheduledTask.Type)
                    {
                        case "GSHEET":
                            manager.GoogleSheet();
                            break;
                        case "FDT":
                            manager.FicheDactivite();
                            break;
                        case "SYNCTOY":
                            manager.SyncToy(scheduledTask.Name);
                            break;
                        case "FILESYNC":
                            manager.FileSync(scheduledTask.Name);
                            break;
                        case "MAILLOG":
                            Utility.Instance.SendMailLog();
                            break;
                        case "HIBERNATE":
                            manager.HibernateWindows();
                            break;
                        case "ISALIVE":
                            manager.CheckServers();
                            break;
                        default:
                            return;
                    }
                }
            }
        }
        #endregion

        #region Start/Stop
        protected override void OnStart(string[] args)
        {
            serviceStart = DateTime.Now;            
            timerDelay.Enabled = true;

            // create subfolders
            manager.CreateSubDirectories();
            Manager.Log(__type, "Subfolders successfully created");

            // generate the required "sync" files for FreeFileSync
            manager.GenerateSyncFiles();
            Manager.Log(__type, "SyncFile templates successfully generated");

            // load the scheduler data
            manager.LoadScheduler();
            Manager.Log(__type, "Scheduler successfully loaded");
            Manager.Log(__type, "Service running ...");
        }

        protected override void OnStop()
        {
            TimeSpan tsRunning = new TimeSpan();
            tsRunning = DateTime.Now.Subtract(serviceStart);

            var logMessage = String.Format("{0} days {1} hours {2} minutes",
                tsRunning.Days, tsRunning.Hours, tsRunning.Minutes);

            if (0.Equals(tsRunning.Days) && 0.Equals(tsRunning.Hours) && 0.Equals(tsRunning.Minutes))
            {
                logMessage = String.Format("{0} seconds",
                    tsRunning.Seconds);
            }
            else if (0.Equals(tsRunning.Days) && 0.Equals(tsRunning.Hours))
            {
                logMessage = String.Format("{0} minutes {1} seconds",
                    tsRunning.Minutes, tsRunning.Seconds);
            }
            else if (0.Equals(tsRunning.Days))
            {
                logMessage = String.Format("{0} hours {1} minutes {2} seconds",
                    tsRunning.Hours, tsRunning.Minutes, tsRunning.Seconds);
            }

            Manager.Log(__type, String.Format("Service ended (Runned for {0})", logMessage));
            timerDelay.Enabled = false;
        }
        #endregion

        #region File watchers
        /// <summary>
        /// Reload Scheduler on file change for .ini
        /// </summary>
        /// <param name="source"></param>
        /// <param name="e"></param>
        private void OnChangedScheduler(object source, FileSystemEventArgs e)
        {
            // wait for 10 second for lock on the file to be released
            Thread.Sleep(10000);

            // load the scheduler data
            manager.LoadScheduler();
            Manager.Log(__type, "Scheduler successfully reloaded");
        }

        /// <summary>
        /// Generate Sync files on file change for .ini
        /// </summary>
        /// <param name="source"></param>
        /// <param name="e"></param>
        private void OnChangedSyncFiles(object source, FileSystemEventArgs e)
        {
            // wait for 10 second for lock on the file to be released
            Thread.Sleep(10000);

            // generate the required "sync" files for FreeFileSync
            manager.GenerateSyncFiles();
            Manager.Log(__type, "SyncFile templates successfully regenerated");
        }
        #endregion
    }
}
