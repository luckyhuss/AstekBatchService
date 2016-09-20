using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("AstekBatchService")]
[assembly: AssemblyDescription("Launch different tasks at different time for ASTEK")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Anwar Buchoo (luckyhuss@msn.com)")]
[assembly: AssemblyProduct("AstekBatchService")]
[assembly: AssemblyCopyright("Anwar Buchoo (luckyhuss@msn.com) ©  2015")]
[assembly: AssemblyTrademark("Anwar Buchoo (luckyhuss@msn.com)")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("0dfe846e-0ff4-424a-a1f8-25087426dc1e")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers 
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("2.5.2.0")]
[assembly: AssemblyFileVersion("2.5.2.0")]

// ABO 06/08/2015 - Log4Net activation
[assembly: log4net.Config.XmlConfigurator(ConfigFile = "log4net.config", Watch = true)]