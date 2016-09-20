using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;

namespace AstekBatchService.Scheduler
{
    class Utility
    {
        // static holder for instance, need to use lambda to construct since constructor private
        private static readonly Lazy<Utility> _instance = new Lazy<Utility>(() => new Utility());

        private static Type __type = System.Reflection.MethodBase.GetCurrentMethod().DeclaringType;

        // private to prevent direct instantiation
        private Utility() { }

        // accessor for instance
        public static Utility Instance
        {
            get
            {
                return _instance.Value;
            }
        }

        #region Remote Logging
        public void LogTimeSpan(DateTime startTime, DateTime endTime, string serviceName)
        {
            try
            {
                TimeSpan tsTimeTaken = endTime - startTime;
                StringBuilder logMessage = new StringBuilder();

                // start of line
                logMessage.Append("^");

                // append data
                logMessage.AppendFormat("{3}~{1}~{2}~",
                    DateTime.Now.ToShortDateString(), serviceName, startTime.ToString("HH:mm:ss"), Environment.MachineName);

                if (tsTimeTaken.Hours != 0)
                {
                    logMessage.AppendFormat("{0:hh} h {0:mm} min {0:ss} sec {0:fff} msec", tsTimeTaken);
                }
                else
                    if (tsTimeTaken.Minutes != 0)
                    {
                        logMessage.AppendFormat("{0:mm} min {0:ss} sec {0:fff} msec", tsTimeTaken);
                    }
                    else
                        if (tsTimeTaken.Seconds != 0)
                        {
                            logMessage.AppendFormat("{0:ss} sec {0:fff} msec", tsTimeTaken);
                        }
                        else
                        {
                            logMessage.AppendFormat("{0:fff} msec", tsTimeTaken);
                        }

                // end of line
                logMessage.Append("$");

                Instance.Logger(logMessage.ToString());

                Manager.Log(__type, "Task logged on remote server");
            }
            catch (Exception ex)
            {
                Manager.Log(__type, ex);
            }
        }

        private void Logger(string logMessage)
        {
            //var logPathFilenameRandom = String.Format("{0}{1}", LogPathFilename, new Random().Next(0, 1000));

            StringBuilder newLogMessage = new StringBuilder();
            if (File.Exists(Instance.LogPathFilename))
            {
                // read old logged message if file exists
                newLogMessage = new StringBuilder(File.ReadAllText(Instance.LogPathFilename, Encoding.UTF8));
            }

            // append new log message
            newLogMessage.AppendLine(logMessage);

            // delete old file because of permission access error
            File.Delete(Instance.LogPathFilename);

            // Write the string to a file.append mode is enabled so that the log
            // lines get appended to  log file rather than wiping content when writing the log
            File.AppendAllText(Instance.LogPathFilename, newLogMessage.ToString(), Encoding.UTF8);
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
        #endregion

        #region Mail
        /// <summary>
        /// Send a mail
        /// </summary>
        public void SendMail(string subject, string content)
        {
            try
            {

                Manager.Log(__type, "Sending email ...");

                // mail object
                MailMessage mail = new MailMessage();

                //noreply@astek.mu
                MailAddress from = new MailAddress(
                    ConfigurationManager.AppSettings["Mail.From"].Split(':')[0],
                    ConfigurationManager.AppSettings["Mail.From"].Split(':')[1]);

                mail.From = from;

                foreach (var emailGroup in ConfigurationManager.AppSettings["Mail.To"].Split('|'))
                {
                    MailAddress to = new MailAddress(
                        emailGroup.Split(':')[0],
                        emailGroup.Split(':')[1]);

                    mail.To.Add(to);
                }

                SmtpClient client = new SmtpClient(ConfigurationManager.AppSettings["Mail.SMTPServer"]);
                //client.Port = 25;
                client.DeliveryMethod = SmtpDeliveryMethod.Network;
                client.UseDefaultCredentials = false;
                mail.Subject = subject;
                mail.IsBodyHtml = true;
                mail.BodyEncoding = Encoding.UTF8;

                mail.Body = content;
                client.Send(mail);

                Manager.Log(__type, "Email sent");
            }
            catch (Exception ex)
            {
                Manager.Log(__type, ex);
            }
        }

        /// <summary>
        /// Send Log file in a mail
        /// </summary>
        public void SendMailLog()
        {
            var serviceName = "Mailing Log";
            DateTime startTime = DateTime.Now;
            Console.WriteLine("{0} started @ {1}", serviceName, startTime.ToString("HH:mm:ss.FFF"));

            try
            {
                Manager.Log(__type, "Building log email ...");

                #region Build mail message
                StringBuilder message = new StringBuilder();

                message.AppendLine("<HTML>");
                message.AppendLine("	<HEAD>");
                message.AppendLine("		<STYLE type=\"text/css\">");
                message.AppendLine("		    body { font-size: 12px; color: #000; font-family: \"Arial\"; text-decoration: none; }");
                message.AppendLine("		    table { border: 1px solid #060;}");
                message.AppendLine("			th { font-size: 14px; font-weight: bold; text-align: left; padding-left:10px; color: #FFF; background-color: #060; }");
                message.AppendLine("			td { font-size: 12px; vertical-align: top; }");
                message.AppendLine("			.bg1 { background-color: #FFF;}");
                message.AppendLine("			.bg2 { background-color: #DBF0E2;}");
                message.AppendLine("			pre { font-size: 11px; font-family: \"Arial\"; }");
                message.AppendLine("		</STYLE>");
                message.AppendLine("	</HEAD>");

                message.AppendLine(" 	<BODY>");

                message.AppendLine("        <DIV>Bonjour,<br />Ci-après le log pour l'exécution des batchs du jour.<BR /><BR /></DIV>");

                // read log file
                if (File.Exists(Instance.LogPathFilename))
                {
                    // read logged message if file exists
                    var logMessage = File.ReadAllText(Instance.LogPathFilename, Encoding.UTF8);

                    // TABLE
                    message.AppendLine(" 	    <TABLE cellSpacing=0 cellPadding=0 width=\"100%\">");

                    // THEAD
                    message.AppendLine("            <THEAD>");
                    message.AppendLine("                <TR><TH>Machine</TH><TH>Tâche</TH><TH>Début</TH><TH>Durée</TH></TR>");
                    message.AppendLine(" 	        </THEAD>");

                    // TBODY
                    message.AppendLine(" 	        <TBODY>");

                    var bgColor = "bg2"; // default first line color
                    foreach (var line in logMessage.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        // ^MAU-LORAN001~Download "Fiches d'activité" (GoogleDoc)~11:25:47~10 sec 087 msec$
                        var cleanLine = line.Replace("^", String.Empty).Replace("$", String.Empty);
                        string[] values = cleanLine.Split('~');
                        message.AppendFormat("                <TR class=\"{4}\"><TD>{0}</TD><TD>{1}</TD><TD>{2}</TD><TD>{3}</TD></TR>",
                            values[0], values[1], values[2], values[3], bgColor);
                        message.AppendLine();

                        bgColor = "bg2".Equals(bgColor) ? "bg1" : "bg2";
                    }

                    message.AppendLine(" 	        </TBODY>");
                    message.AppendLine(" 	    </TABLE>");
                }

                message.AppendLine("        <PRE><BR /><B>*Synchronisation :</B> entre diocletien et merle (2 way).</PRE>");
                message.AppendLine("        <DIV><BR /><BR />Cordialement,<BR />Astek Batch</DIV>");

                message.AppendLine(" 	</BODY>");
                message.AppendLine("</HTML>");
                #endregion

                var subject = String.Format("[ASTEK BATCH] Daily report {0}", DateTime.Today.ToString("dd/MM/yyyy"));
                SendMail(subject, message.ToString());

                // log at the end and do not send in the current outgoing email
                DateTime endTime = DateTime.Now;
                Console.WriteLine("{0} ended @ {1}", serviceName, endTime.ToString("HH:mm:ss.FFF"));
                Instance.LogTimeSpan(startTime, endTime, serviceName);
            }
            catch (Exception ex)
            {
                Manager.Log(__type, ex);
            }
        }
        #endregion
    }
}
