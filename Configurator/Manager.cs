using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;

namespace Configurator
{
    class Manager
    {
        // Log4net logging object
        private static Type __type = System.Reflection.MethodBase.GetCurrentMethod().DeclaringType;
        private static readonly log4net.ILog appLog = log4net.LogManager.GetLogger(__type);

        // base application path
        public string basePath = String.Empty;

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

        /// <summary>
        /// Gets the path & filename of Log file
        /// </summary>
        private string LogPathFilename
        {
            get
            {
                return Path.Combine(ConfigurationManager.AppSettings["Path.Log.Remote"], "AppLog",
                    String.Concat(DateTime.Today.ToString("yyyy-MM-dd"), ".log"));
            }
        }

        public void LogToServer(string logMessage)
        {
            //var logPathFilenameRandom = String.Format("{0}{1}", LogPathFilename, new Random().Next(0, 1000));

            StringBuilder newLogMessage = new StringBuilder();
            if (File.Exists(LogPathFilename))
            {
                // read old logged message if file exists
                newLogMessage = new StringBuilder(File.ReadAllText(LogPathFilename, Encoding.UTF8));
            }

            // append new log message
            newLogMessage.AppendLine(logMessage);

            // delete old file because of permission access error
            File.Delete(LogPathFilename);

            // Write the string to a file.append mode is enabled so that the log
            // lines get appended to  log file rather than wiping content when writing the log
            File.AppendAllText(LogPathFilename, newLogMessage.ToString(), Encoding.UTF8);
        }
    }
}