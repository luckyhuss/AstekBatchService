using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Configurator
{
    static class Program
    {
        private static Type __type = System.Reflection.MethodBase.GetCurrentMethod().DeclaringType;
        private static Manager manager = new Manager();        

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // get base directory path
            manager.basePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().CodeBase);
            manager.basePath = new Uri(manager.basePath).LocalPath;

            Manager.Log(__type, "Configurator Application launched");

            Manager.Log(__type, "username : " + WindowsIdentity.GetCurrent().Name);

            
            if (args.Length > 1 && "--type".Equals(args[0]))
            {
                Manager.Log(__type, String.Concat("Command : ", args[1]));
                // check type of batch to launch
                // e.g. --type DOWNLOAD "path_to_download"
                // e.g. --type UPLOAD "filename" "path_to_upload"
                // e.g. --type LOG "data_to_log"
                switch (args[1])
                {
                    case "DOWNLOAD":
                        if (args.Length > 2)
                        {
                            var pathToDowload = args[2];
                            try
                            {
                                var destinationPath = Path.Combine(manager.basePath, "files", Path.GetFileName(pathToDowload));
                                File.Copy(pathToDowload, destinationPath, true);

                                if (File.Exists(destinationPath))
                                {
                                    Manager.Log(__type, String.Concat("File downloaded successfully : ", Path.GetFileName(destinationPath)));
                                }
                            }
                            catch (Exception ex)
                            {
                                Manager.Log(__type, ex);
                            }
                        }
                        else
                            Manager.Log(__type, log4net.Core.Level.Warn, "Path not specified for DOWNLOAD command");
                        break;
                    case "UPLOAD":
                        if (args.Length > 3)
                        {
                            var fileToUpload = args[2];
                            var pathToUpload = args[3];
                            try
                            {
                                pathToUpload = Path.Combine(pathToUpload, fileToUpload);
                                fileToUpload = Path.Combine(manager.basePath, "files", fileToUpload);
                                File.Copy(fileToUpload, pathToUpload, true);

                                if (File.Exists(pathToUpload))
                                {
                                    Manager.Log(__type, String.Concat("File uploaded successfully : ", Path.GetFileName(pathToUpload)));
                                }
                            }
                            catch (Exception ex)
                            {
                                Manager.Log(__type, ex);
                            }
                        }
                        else
                            Manager.Log(__type, log4net.Core.Level.Warn, "Filename/Path not specified for UPLOAD command");
                        break;
                    case "LOG":
                        if (args.Length > 2)
                        {
                            manager.LogToServer(args[2]);
                            Manager.Log(__type, String.Concat("Log update successfully : ", args[2]));
                        }
                        else
                            Manager.Log(__type, log4net.Core.Level.Warn, "Message not specified for LOG command");
                        break;
                    default:
                        Manager.Log(__type, log4net.Core.Level.Warn, String.Concat("Invalid flag : ", args[1]));
                        return;
                }
            }
            else
            {
                Application.Run(new FormMain());
            }
            Manager.Log(__type, "Configurator Application exited");
        }
    }
}
