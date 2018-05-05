using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Reflection;
using System.Data;

using Microsoft.SqlServer.Management.Trace;
using NDesk.Options;





namespace LoadTrace
{


	partial class Program
	{

#region Static variables
		public const string SMO_SQLSERVER2008_ASSEMBLY = "Microsoft.SqlServer.ConnectionInfoExtended, Version=10.0.0.0, Culture=neutral, PublicKeyToken=89845dcd8080cc91";
		public static TraceDimension ApplicationLookup;
		public static TraceDimension ServerLookup;
		public static TraceDimension LoginLookup;
		public static TraceDimension HostLookup;

		public static string SERVER = "";
		public static string DATABASE = "";
		public static string FILES = "";
		public static bool VERBOSE = false;
		public static string WORK_DIR = "";
		public static string ARCHIVE_DIR = "";
		public static bool REPROCESS = false;
		public static bool RESET_PERMISSIONS = false;
        public static bool CLEAN_UP_SERVER_NAME = false;
        public static bool SAVE_LOGIN_EVENTS = false;

		public static string CONNECTION_STRING = "";

		//public static LoadTraceSection CONFIGURATION;

#endregion
		
		static int Main(string[] args)
		{

			// for performance testing
//#if DEBUG
			//args = new string[8] { @"/s", @"L70\KATMAI", @"/db", @"TraceRepository", @"/files", @"C:\Projects\SqlUtilities\LoadTrace\Login Traces\BigTrace\LoginCapture_20MB.trc", "/reprocess", "/verbose" };
//#endif
			if (!ParseParameters(args))
			{
#if DEBUG
				Console.WriteLine("Press any key to continue....");
				Console.ReadLine();
#endif
				return 1;
			}

			


			Version v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
			WriteLog("Launching [{0}]", v.ToString());
			WriteLog("Server: {0}", SERVER);
			WriteLog("Database: {0}", DATABASE);
			// WriteLog("File(s): {0}", FILES);
			if (WORK_DIR.Length > 0)
				WriteLog("Working Directory: {0}", WORK_DIR);
			if (ARCHIVE_DIR.Length > 0)
				WriteLog("Archive Directory: {0}", ARCHIVE_DIR);
			if (REPROCESS)
				WriteLog("We are reprocessing files.");
			if (RESET_PERMISSIONS)
				WriteLog("We are resettting permissions on unreadable files.");

			CONNECTION_STRING = Utilities.GetConnectionString();

            try
            {
                Utilities.TestDatabaseServer(CONNECTION_STRING);
            }
            catch
            {
                WriteLog("Error connecting to the database server.");
#if DEBUG
                Console.WriteLine("Press any key to continue....");
                Console.ReadLine();
                return 1;
#endif

            }

			//Assembly assembly = Assembly.Load(SMO_SQLSERVER2008_ASSEMBLY);
			//Type type = assembly.GetType("Microsoft.SqlServer.Management.Trace.TraceFile");
			//IDataReader runnable = Activator.CreateInstance(type) as IDataReader;

			ApplicationLookup = new TraceDimension("ApplicationName", "ApplicationDim", "ApplicationID", "ApplicationName", 256);
			ApplicationLookup.GetDatabaseValues(CONNECTION_STRING);

			ServerLookup = new TraceDimension("ServerName", "ServerDim", "ServerID", "ServerName", 256);
			ServerLookup.GetDatabaseValues(CONNECTION_STRING);

			LoginLookup = new TraceDimension("LoginName", "LoginDim", "LoginID", "LoginName", 256);
			LoginLookup.GetDatabaseValues(CONNECTION_STRING);

			HostLookup = new TraceDimension("HostName", "HostDim", "HostID", "HostName", 256);
			HostLookup.GetDatabaseValues(CONNECTION_STRING);


            // TODO: Process any files in the working directory
			
			ProcessServers();

			

#if DEBUG
			Console.WriteLine("Press any key to continue....");
			Console.ReadLine();
#endif
            return 0 ;
		}

		static bool ParseParameters (string[] args)
		{

			LoadTraceConfig.AppConfigFile = (LoadTraceSection)System.Configuration.ConfigurationManager.GetSection("LoadTrace");
			VERBOSE = LoadTraceConfig.AppConfigFile.Verbose;
			REPROCESS = LoadTraceConfig.AppConfigFile.Reprocess;
			RESET_PERMISSIONS = LoadTraceConfig.AppConfigFile.ResetPermissions;
			SERVER = LoadTraceConfig.AppConfigFile.Server;
			DATABASE = LoadTraceConfig.AppConfigFile.Database;
			WORK_DIR = LoadTraceConfig.AppConfigFile.WorkDirectory;
			ARCHIVE_DIR = LoadTraceConfig.AppConfigFile.ArchiveDirectory;
            CLEAN_UP_SERVER_NAME = LoadTraceConfig.AppConfigFile.CleanUpServerName;
            SAVE_LOGIN_EVENTS = LoadTraceConfig.AppConfigFile.SaveLoginEvents;

            // command-line settings would override the app.config settings
			var p = new OptionSet() {
			{ "v|verbose",  z => VERBOSE = true },
			{ "reprocess", z => REPROCESS = true},
			{ "resetperms", z => RESET_PERMISSIONS = true},
			{ "s|server=",   z =>  SERVER = z  },
			{ "file|files=",   z =>  FILES = z  },
			{ "db|database=",   z =>  DATABASE = z  },
			{ "workdir=", z => WORK_DIR = z},
			{ "archivedir=", z => ARCHIVE_DIR = z}
			};


			List<string> extra = p.Parse(args);

			if (SERVER.Length == 0 || DATABASE.Length == 0 || LoadTraceConfig.AppConfigFile.Servers.Count == 0)
			{
				Version v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

				Console.WriteLine(@"LoadTrace.EXE ({0})

All parameters and configuration are set in LOADTRACE.EXE.CONFIG
",
												 v.ToString());

//Optional Parameters that over
//--------------------------------------------------------------
///s          ServerName
///db         Database
///files      FileWildCard (usually enclosed in quotes)
///v          (Verbose Outout)
///archivedir ArchiveDir (Trace files are moved here after processing)
///reprocess  (Reprocess any existing trace files that match the wildcard
//            or are found in the work directory.)
//",
//                                                 v.ToString());


				return false;
			}

			return true; 
		}

	   
	}
}
