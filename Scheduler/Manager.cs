using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Windows.Forms;

namespace AstekBatchService.Scheduler
{
    class Manager
    {
        #region Params
        // base application path
        public string basePath = String.Empty;
        public readonly string syncExt = ".sync";
        public List<ScheduledTask> scheduledTasks = new List<ScheduledTask>();
        #endregion

        #region Enum
        public enum ServerName
        {
            Merle, Diocletien
        };
        #endregion

        #region Log4net

        // Log4net logging object
        private static Type __type = System.Reflection.MethodBase.GetCurrentMethod().DeclaringType;
        private static readonly log4net.ILog appLog = log4net.LogManager.GetLogger(__type);

        private static void Log(Type type, log4net.Core.Level level, string message, Exception exception)
        {
            log4net.GlobalContext.Properties["ClassName"] = type;
            if (level == log4net.Core.Level.Error)
            {
                Manager.appLog.Error(message, exception);
            }
            else if (level == log4net.Core.Level.Warn)
            {
                Manager.appLog.Warn(message, exception);
            }
            else if (level == log4net.Core.Level.Info)
            {
                Manager.appLog.Info(message, exception);
            }
        }

        public static void Log(Type type, log4net.Core.Level level, string message)
        {
            Log(type, level, message, null);
        }

        public static void Log(Type type, string message)
        {
            Log(type, log4net.Core.Level.Info, message, null);
        }

        public static void Log(Type type, Exception exception)
        {
            Log(type, log4net.Core.Level.Error, exception.Message, exception);
        } 
        #endregion

        #region Tools
        /// <summary>
        /// Create subfolders required by the Batch
        /// </summary>
        /// <param name="basePath"></param>
        public void CreateSubDirectories()
        {
            // create subfolders (max 5) required by this batch process
            List<string> subDirectories = new List<string>(5);
            subDirectories.Add("FDT");
            subDirectories.Add("log");

            foreach (var subDirectory in subDirectories)
            {
                Directory.CreateDirectory(Path.Combine(basePath, subDirectory));
            }
        }

        /// <summary>
        /// Check if servers are accessible
        /// </summary>
        /// <param name="serverName">Merle or Diocletien</param>
        private bool CheckServerAlive(ServerName server)
        {
            bool alive = false;
            try
            {
                var accessContent = File.ReadAllText(
                    ConfigurationManager.AppSettings[String.Format("Server.Alive.{0}", server.ToString())]);

                if ("1".Equals(accessContent))
                {
                    alive = true;
                    Log(__type, String.Format("{0} is OK", server.ToString()));
                }
            }
            catch (Exception ex)
            {
                Log(__type,
                    log4net.Core.Level.Error, "Server " + server.ToString() + " error : " + ex.Message, ex);
            }
            return alive;
        }

        /// <summary>
        /// Check both servers and notify by mail
        /// </summary>
        /// <returns></returns>
        public void CheckServers()
        {
            bool merle = CheckServerAlive(Manager.ServerName.Merle);
            bool diocletien = CheckServerAlive(Manager.ServerName.Diocletien);

            if (merle && diocletien)
            {
                return;
            }
            else
            {
                // send mail
                var subject = String.Format("[ASTEK BATCH] SERVERS NOT ACCESSIBLE {0}", DateTime.Today.ToString("dd/MM/yyyy"));
                var content = String.Format("Merle : {0}<br />Diocletien : {1}<br /><br />PC : {2}", merle, diocletien, Environment.MachineName);
                Log(__type, content);

                Utility.Instance.SendMail(subject, content);
            }
        }

        public void LoadScheduler()
        {
            // clear list
            scheduledTasks.Clear();

            try
            {
                // load the Scheduler.ini file
                foreach (var line in File.ReadAllLines(Path.Combine(basePath, "Scheduler.ini"), Encoding.UTF8))
                {
                    if (!String.IsNullOrEmpty(line.Trim()) && !line.StartsWith("--", StringComparison.InvariantCultureIgnoreCase))
                    {
                        // -- Time (24H);Frequency (Hourly|Daily|<Weekly>);Task type (GSHEET|FDT|FILESYNC|MAILLOG|SYNCTOY);Task name;Machine name
                        // 08:00;Daily;FILESYNC;SPID-OCEANE;ASTEKPC54

                        // valid line and not a commented one
                        var values = line.Split(';');

                        ScheduledTask scheduledTask = new ScheduledTask(values[0], values[1], values[2], values[3], values[4]);

                        // add task to list
                        scheduledTasks.Add(scheduledTask);
                    }
                }
            }
            catch (Exception ex)
            {
                Manager.Log(__type, ex);
            }
        }

        public void HibernateWindows()
        {
            Manager.Log(__type, "System hybernating ...");
            // hibernate Windows
            Application.SetSuspendState(PowerState.Hibernate, true, true);
        }
        #endregion

        #region Processing
        public void GenerateSyncFiles()
        {
            var batchFileSync = Path.Combine(ConfigurationManager.AppSettings["Path.FileSync"], "Batch");
            var logFileSync = Path.Combine(ConfigurationManager.AppSettings["Path.Log.Remote"], "SyncLog");
            var templateFile = Path.Combine(batchFileSync, "__TEMPLATE.sync");
            
            try
            {
                // load the FileSync.ini file
                foreach (var line in File.ReadAllLines(Path.Combine(basePath, "FileSync.ini"), Encoding.UTF8))
                {
                    if (!line.StartsWith("--", StringComparison.InvariantCultureIgnoreCase))
                    {
                        // -- MachineName;FolderPairName (sync file name);SyncType (Update/Mirror/TwoWay);Left (copied from);Right (update to);Files / Folders to Exclude
                        // ASTEKPC54;ASPIN-LIVRAISON;Update;\\merle\ws\Projects\vivop\40-Projets\ASPIN\00 - COMMUN\11 - Livraisons;\\diocletien\Projets\VIVOP\40-Projets\ASPIN\00 - COMMUN\11 - Livraisons;Archives&temp_livraison&xxx-G0R0C0[-lotx]-DATE-PFx-COMMENTAIRE

                        // valid line and not a commented one
                        var values = line.Split(';');
                        var syncFilename = String.Concat(values[0], "-", values[1], syncExt);

                        StringBuilder stringBuilderExclude = new StringBuilder();
                        var exclude = String.Empty;

                        if (!String.IsNullOrEmpty(values[5]))
                        {
                            // prepare excluded folders/files list
                            foreach (var item in values[5].Split('&'))
                            {
                                // check if it is a folder or sub-folder *\temp\*
                                if (!item.Contains('.'))
                                {
                                    stringBuilderExclude.AppendFormat(@"*\{0}\*", item).Append(";");
                                }
                                else
                                {
                                    stringBuilderExclude.Append(item).Append(";");
                                }
                            }

                            // remove last ;
                            stringBuilderExclude = stringBuilderExclude.Remove(stringBuilderExclude.Length - 1, 1);

                            // add Item XML tag
                            exclude = String.Format("<Item>{0}</Item>", stringBuilderExclude.ToString());
                        }

                        // read template file
                        var __template = File.ReadAllText(templateFile, Encoding.UTF8);

                        var syncFileContent = String.Format(__template,
                            values[2], values[3], values[4], logFileSync, exclude);
                        File.WriteAllText(Path.Combine(batchFileSync, syncFilename), syncFileContent, Encoding.UTF8);
                    }
                }
            }
            catch (Exception ex)
            {
                Manager.Log(__type, ex);
            }
        }

        /// <summary>
        /// Update Google Sheet reference on remote server
        /// </summary>
        public void GoogleSheet()
        {
            var serviceName = "Mise à jour Google DOC_ID \"Fiches d'activité\"";
            DateTime startTime = DateTime.Now;

            try
            {
                var gsheetLocalPath = Path.Combine(ConfigurationManager.AppSettings["Path.GSheet.Local"], DateTime.Today.Year.ToString());
                var gsheetRemotePath = Path.Combine(ConfigurationManager.AppSettings["Path.GSheet.Remote"], DateTime.Today.Year.ToString());

                Manager.Log(__type, "Updating Google DOC_ID ...");

                if (!Directory.Exists(gsheetLocalPath))
                {
                    // exit because not the correct machine
                    Manager.Log(__type, @"Run only on \\MAU-LORAN001. Exiting now.");
                    return;
                }

                var gsheetFiles = Directory.GetFiles(gsheetLocalPath, "*.gsheet");
                var gsheetDocIds = new List<KeyValuePair<string, string>>(15); // 12 months max

                foreach (var gsheetFile in gsheetFiles)
                {
                    var filenameDocId = Path.GetFileNameWithoutExtension(gsheetFile);
                    foreach (var line in File.ReadAllText(gsheetFile, Encoding.UTF8).Replace("\"", String.Empty).Split(','))
                    {
                        var keyValue = new KeyValuePair<string, string>(line.Split(':')[0].Trim(), line.Split(':')[1].Trim());
                        if ("doc_id".Equals(keyValue.Key, StringComparison.InvariantCultureIgnoreCase))
                        {
                            gsheetDocIds.Add(new KeyValuePair<string, string>(String.Concat(filenameDocId, ".ini"), keyValue.Value));
                        }
                    }
                }

                Manager.Log(__type, "All DOC_ID fetched from local machine");

                Manager.Log(__type, "Uploading each DOC_ID on remote server");

                if (!Directory.Exists(gsheetRemotePath))
                {
                    Directory.CreateDirectory(gsheetRemotePath);
                }

                foreach (var gsheetKeyValue in gsheetDocIds)
                {
                    File.WriteAllText(Path.Combine(gsheetRemotePath, gsheetKeyValue.Key), gsheetKeyValue.Value, Encoding.UTF8);
                }

                DateTime endTime = DateTime.Now;
                Utility.Instance.LogTimeSpan(startTime, endTime, serviceName);
                Manager.Log(__type, "Google DOC_ID updated successfully");
            }
            catch (Exception ex)
            {
                Manager.Log(__type, ex);
            }
        }
        
        /// <summary>
        /// Download Suivi from Google Doc and upload
        /// </summary>
        public void FicheDactivite()
        {
            try
            {
                int downloadRetry = 0;
            start:
                downloadRetry++;
                var serviceName = "Téléchargement et synchronisation \"Fiches d'activité\" (GoogleDoc:{0}KB@{1}retry)";
                DateTime startTime = DateTime.Now;
                Console.WriteLine("{0} started @ {1}", serviceName, startTime.ToString("HH:mm:ss.FFF"));

                var gsheetDocIdRemotePath = Path.Combine(ConfigurationManager.AppSettings["Path.GSheet.Remote"], DateTime.Today.Year.ToString());
                var gsheetDocIdIni = Directory.GetFiles(gsheetDocIdRemotePath, "*.ini").SingleOrDefault(filename => filename.Contains(DateTime.Today.ToString("yyyy-MM")));

                Manager.Log(__type, "Downloading FDT from Google ...");

                if (!String.IsNullOrEmpty(gsheetDocIdIni))
                {
                    // ini file found with docId
                    // read DocID from ini file
                    var fdtGoogleDocID = File.ReadAllText(gsheetDocIdIni, Encoding.UTF8);
                    var fdtGoogleDocDownloadUrl = String.Format(ConfigurationManager.AppSettings["Url.GoogleDoc.Export"], fdtGoogleDocID);

                    //var fdtFilename = String.Format("Activité ASPIN_SPID {0}.xlsx", DateTime.Today.ToString("yyyy-MM"));
                    var fdtFilename = Path.GetFileNameWithoutExtension(gsheetDocIdIni);
                    var fdtLocalPathFilename = Path.Combine(basePath, "FDT", fdtFilename);
                    var fdtRemotePathFilename = Path.Combine(
                        ConfigurationManager.AppSettings["Path.FDT.Remote"],
                        DateTime.Today.Year.ToString());

                    LaunchProcess(
                        Path.Combine(basePath, @"wget\wget.exe"),
                        String.Format("--no-check-certificate -O \"{0}\" {1}", fdtFilename, fdtGoogleDocDownloadUrl),
                        Path.Combine(basePath, "FDT"));

                    // check filesize before continuing
                    FileInfo fileInfo = new FileInfo(fdtLocalPathFilename);
                    if (fileInfo.Length == 0)
                    {
                        // loop back
                        goto start;
                    }
                    else
                    {
                        // create remote if not exist
                        if (!Directory.Exists(fdtRemotePathFilename))
                        {
                            Directory.CreateDirectory(fdtRemotePathFilename);
                        }

                        fdtRemotePathFilename = Path.Combine(fdtRemotePathFilename, fdtFilename);

                        serviceName = String.Format(serviceName, fileInfo.Length / 1024, downloadRetry);
                        File.Copy(fdtLocalPathFilename, fdtRemotePathFilename, true);
                        Manager.Log(__type, "FDT file uploaded on remote server");
                    }
                }
                else
                {
                    // no file for the current month
                    Manager.Log(__type, "No FDT for the current month. Check GoogleDoc");
                    serviceName = String.Format(serviceName, "KO", -1);
                }

                DateTime endTime = DateTime.Now;
                Console.WriteLine("{0} ended @ {1}", serviceName, endTime.ToString("HH:mm:ss.FFF"));
                Utility.Instance.LogTimeSpan(startTime, endTime, serviceName);
                Manager.Log(__type, "FDT downloaded from Google");
            }
            catch (Exception ex)
            {
                Manager.Log(__type, ex);
            }
        }

        /// <summary>
        /// Launch an executable/bat file on the host machine
        /// </summary>
        /// <param name="processName"></param>
        /// <param name="processArgs"></param>
        /// <param name="workingDirectory"></param>
        public void LaunchProcess(string processName, string processArgs, string workingDirectory)
        {
            try
            {
                var process = new System.Diagnostics.Process();
                process.StartInfo.FileName = processName;
                process.StartInfo.Arguments = processArgs;
                process.StartInfo.WorkingDirectory = workingDirectory;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.UseShellExecute = false;
                //process.StartInfo.CreateNoWindow = true;
                process.Start();
                process.StandardOutput.ReadToEnd();
            }
            catch (Exception ex)
            {
                Manager.Log(__type, ex);
            }
        }

        /// <summary>
        /// Launch the SyncToy task
        /// </summary>
        /// <param name="syncName"></param>
        public void SyncToy(string syncName)
        {
            Manager.Log(__type, "Synctoy called");
            throw new NotImplementedException("Implement the method first");
            //var serviceName = String.Format("Synchronisation \"{0}\"", syncName);
            //DateTime startTime = DateTime.Now;
            //Console.WriteLine("{0} started @ {1}", serviceName, startTime.ToString("HH:mm:ss.FFF"));

            // C:\Program Files\SyncToy 2.1\SyncToyCmd.exe
            //LaunchProcess(
            //        ConfigurationManager.AppSettings["Path.SyncToy.Executable"],
            //        String.Format("-R {0}", syncName.Equals("ALL") ? String.Empty : String.Format("\"{0}\"", syncName)), // add double quote
            //        basePath);

            //DateTime endTime = DateTime.Now;
            //Console.WriteLine("{0} ended @ {1}", serviceName, endTime.ToString("HH:mm:ss.FFF"));
            //Utility.Instance.LogTimeSpan(startTime, endTime, serviceName);
        }

        public void FileSync(string syncName)
        {
            var serviceName = String.Format("Synchronisation \"{0}\"", syncName);
            DateTime startTime = DateTime.Now;
            Console.WriteLine("{0} started @ {1}", serviceName, startTime.ToString("HH:mm:ss.FFF"));

            Manager.Log(__type, String.Format("File Sync in progress for {0} ...", syncName));

            LaunchProcess(
                    Path.Combine(ConfigurationManager.AppSettings["Path.FileSync"], "FreeFileSync.exe"),
                    String.Format("\"Batch\\{0}-{1}{2}\"", Environment.MachineName, syncName, syncExt), // add double quote to path
                    ConfigurationManager.AppSettings["Path.FileSync"]);

            DateTime endTime = DateTime.Now;
            Console.WriteLine("{0} ended @ {1}", serviceName, endTime.ToString("HH:mm:ss.FFF"));
            Utility.Instance.LogTimeSpan(startTime, endTime, serviceName);
            Manager.Log(__type, "File Sync completed");
        }

        public void CheckSyncLog(string syncName)
        {

        }
        #endregion
    }
}
