using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.SqlServer.Management.Trace;
using System.Data;
using System.Data.SqlClient;
using System.IO;

using System.Diagnostics;

using Ionic.Zip;
using System.Text.RegularExpressions;

namespace LoadTrace
{
    partial class Program
    {
        public static int rowCount = 0;

        public static void ProcessTraceFile(string traceFileName, string appConfigServerName)
        {
            Stopwatch fileTimer = new Stopwatch();
            fileTimer.Start();

            string serverName;

            // if no servername in the trace file, use the name from app.config
            try { serverName = Utilities.GetServerNameFromTrace(traceFileName); }
            catch (NoServerNameException)
            {
                serverName = appConfigServerName;
                WriteVerboseLog("Using server name from app.config: {0}", serverName);
            }

            if (CLEAN_UP_SERVER_NAME)
                serverName = Utilities.CleanUpServerName(serverName);

            WriteVerboseLog("Trace for : {0}", serverName);
            int serverID = SaveServerName(serverName);
            int traceFileID = SaveTrace(traceFileName, serverID);
            
            TraceFile t = new TraceFile();
            t.InitializeAsReader(traceFileName);

            string eventClass;

            Dictionary<LoginKey, LoginDetails> loginSummary = new Dictionary<LoginKey, LoginDetails>();

            rowCount = 0;

            while (t.Read())
            {
                rowCount++;
                eventClass = t.GetString(t.GetOrdinal("EventClass"));
                switch (eventClass)
                {
                    case "Trace Start":
                        ProcessTraceStart(ref t, serverName);
                        break;

                    case "Audit Login":
                        ProcessLogin(appConfigServerName, ref t, ref loginSummary);
                        break;

                    //case "ExistingConnection":
                    //    ProcessLogin(appConfigServerName, ref t, ref loginSummary);
                    //    break;

                    default:
                        // Console.WriteLine("Unknown: {0}", eventClass);
                        break;
                }
            }
            t.Close();
            t.Dispose();


            // Load the rows
           
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                using (SqlCommand cmd = new SqlCommand(@"INSERT dbo.TraceFileLoginSummary (TraceFileID, HostID, ApplicationID, LoginID, FirstLogin, LastLogin, LoginCount)
VALUES (@TraceFileID, @HostID, @ApplicationID, @LoginID, @FirstLogin, @LastLogin, @LoginCount)", conn))
                {
                    conn.Open();

                    foreach (LoginKey key in loginSummary.Keys)
                    {
                        LoginDetails details = loginSummary[key];

                        // Consider using prepared SQL here
                        cmd.Parameters.AddWithValue("@TraceFileID", traceFileID);
                        cmd.Parameters.AddWithValue("@HostID", key.HostID);
                        cmd.Parameters.AddWithValue("@ApplicationID", key.ApplicationID);
                        cmd.Parameters.AddWithValue("@LoginID", key.LoginID);
                        cmd.Parameters.AddWithValue("@FirstLogin", details.FirstLogin);
                        cmd.Parameters.AddWithValue("@LastLogin", details.LastLogin);
                        cmd.Parameters.AddWithValue("@LoginCount", details.LoginCount);
                        cmd.ExecuteNonQuery();
                        cmd.Parameters.Clear();
                        
                    }
                    conn.Close();
                }

            }

                // run the stored procedure to generate summaries
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                using (SqlCommand cmd = new SqlCommand("dbo.TraceFileSummary_Populate", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    conn.Open();
                    //cmd.Parameters.AddWithValue("@TraceFileID", traceFileID);
                    cmd.ExecuteNonQuery();
                    conn.Close();
                }
            }

                fileTimer.Stop();

            // if the archive directory is specified the move the file
            // Compress the files as they are archived
            if (ARCHIVE_DIR.Length > 0)
            {

                ArchiveFile(traceFileName, serverName);
            }
            
            //WriteVerboseLog("Rows: {0:#,##0}", traceTable.Rows.Count);
            WriteVerboseLog("Seconds: {0:#,##0.0}", fileTimer.Elapsed.TotalSeconds);
            WriteVerboseLog("Summary Rows: {0}", loginSummary.Count);
            WriteLog("{0} : {1} ({2:#,##0} rows)", Path.GetFileName(traceFileName), serverName, rowCount);
        }

        private static void ArchiveFile(string traceFileName, string serverName)
        {
            WriteVerboseLog("Archiving {0}", Path.GetFileName(traceFileName));
            string targetDir = ARCHIVE_DIR;
            string serverNameAsPath = serverName.Replace(@"\", "_");
            targetDir = Path.Combine(targetDir, serverNameAsPath);
            if (!Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);
            string destinationFileName = Path.Combine(targetDir, Path.GetFileName(traceFileName));
            System.IO.File.Move(traceFileName, destinationFileName);

            // move the file to a zip file
            FileInfo traceFile = new FileInfo(destinationFileName);
            DateTime fileTimeStamp = traceFile.LastWriteTime;
            string monthStamp = String.Format("{0}_{1}", fileTimeStamp.Year, fileTimeStamp.Month.ToString("00"));
            string zipFileName = Path.Combine(targetDir, serverNameAsPath + "_" + monthStamp + ".zip");
            WriteVerboseLog("Zip File: {0}", zipFileName);

            // will throw exception if we add a duplicate file to the archive
            using (ZipFile zip = new ZipFile(zipFileName))
            {
                // If I don't use this parameter I get a corrupted ZIP file
                zip.ParallelDeflateThreshold = -1;
                
                zip.AddFile(destinationFileName, "");
                zip.Save();
            }
            // delete the file once we've put it in the archive
            System.IO.File.Delete(destinationFileName);
        }

        private static void ProcessTraceStart(ref TraceFile traceFile, string serverName)
        {
            //Console.WriteLine("Trace Start." );
            // write the trace file info to the table
            
        }
        private static void ProcessLogin(string serverName, ref TraceFile traceFile, ref Dictionary<LoginKey, LoginDetails> loginSummary)
        {

            DateTime startTime = new DateTime(1900,1,1);
            try
            {
                startTime = traceFile.GetDateTime(traceFile.GetOrdinal("StartTime"));
            }
            catch (NullReferenceException)
            {
                // sometimes we don't get a STartTime for system spids.  Just skip them.
                if (traceFile.GetInt32(traceFile.GetOrdinal("SPID")) <= 50)
                    return; 

            }

            int hostID, appID, loginID;
            hostID = HostLookup.GetIndex(ref traceFile, CONNECTION_STRING); 
            appID = ApplicationLookup.GetIndex(ref traceFile, CONNECTION_STRING);
            loginID = LoginLookup.GetIndex(ref traceFile, CONNECTION_STRING); 

            LoginKey lk = new LoginKey();
            lk.HostID = hostID;
            lk.ApplicationID = appID;
            lk.LoginID = loginID;

            LoginDetails d;
            
            if (loginSummary.TryGetValue(lk, out d))
            {
                if (startTime > d.LastLogin)
                    d.LastLogin = startTime;
                d.LoginCount++;
                loginSummary[lk] = d;
            }
            else
            {
                d = new LoginDetails();
                d.FirstLogin = startTime;
                d.LastLogin = startTime;
                d.LoginCount = 1;
                loginSummary.Add(lk, d);
            }

            // Write to detail repository
            if (SAVE_LOGIN_EVENTS)
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    using (SqlCommand cmd = new SqlCommand(@"INSERT dbo.LoginEvent (StartTime, HostID, ApplicationID, LoginID, ServerName) 
                                                    VALUES (@StartTime, @HostID, @ApplicationID, @LoginID, @ServerName)", conn))
                    {
                        conn.Open();

                        cmd.Parameters.AddWithValue("StartTime", startTime);
                        cmd.Parameters.AddWithValue("HostID", hostID);
                        cmd.Parameters.AddWithValue("ApplicationID", appID);
                        cmd.Parameters.AddWithValue("LoginID", loginID);
                        cmd.Parameters.AddWithValue("ServerName", serverName);
                        cmd.ExecuteNonQuery();
                        cmd.Parameters.Clear();

                        conn.Close();
                    }

                }
            }


        }

        private static int SaveTrace(string traceFileName, int serverID)
        {
            string fileNameOnly = Path.GetFileName(traceFileName);

            int foundTraceFileID = GetTraceFileID(traceFileName, serverID);
            if (foundTraceFileID != 0)
            {
                if (REPROCESS)
                { DeleteTrace(foundTraceFileID, serverID); }
                else
                    throw new DuplicateTraceExcption();
            }

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {

                using (SqlCommand cmd = new SqlCommand("INSERT dbo.TraceFile (ServerID, TraceFileName) VALUES (@ServerID, @TraceFileName); SELECT TraceFileID = CAST(SCOPE_IDENTITY() AS INT);", conn))
                {
                    
                    cmd.Parameters.AddWithValue("@ServerID", serverID);
                    SqlParameter fileNameParameter = new SqlParameter("@TraceFileName", SqlDbType.NVarChar, 128);
                    fileNameParameter.Value = fileNameOnly;
                    cmd.Parameters.Add(fileNameParameter);
                    conn.Open();
                    SqlDataReader rdr = cmd.ExecuteReader(CommandBehavior.CloseConnection);
                    rdr.Read();
                    int traceFileID = rdr.GetInt32(rdr.GetOrdinal("TraceFileID"));
                    rdr.Close();
                    return traceFileID;
                }
            }

        }

        private static void DeleteTrace(int traceFileID, int serverID)
        {
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();

                // delete the rows for the trace
                using (SqlCommand cmd = new SqlCommand(@"SET NOCOUNT ON; 
DELETE FROM dbo.TraceFileLoginSummary WHERE TraceFileID = @TraceFileID; 
DELETE FROM dbo.TraceFile WHERE TraceFileID = @TraceFileID; ", conn))
                {
                    // cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@TraceFileID", traceFileID);
                    cmd.ExecuteNonQuery();
                }

                // reset the summary rows for the trace
                //using (SqlCommand cmd = new SqlCommand("dbo.TraceFileSummary_Populate", conn))
                //{
                //    cmd.CommandType = CommandType.StoredProcedure;
                //    cmd.Parameters.AddWithValue("@TraceFileID", traceFileID);
                //    cmd.ExecuteNonQuery();
                //}
                conn.Close();
            }
        }

        private static int GetTraceFileID(string traceFileName, int serverID)
        {

            string fileNameOnly = Path.GetFileName(traceFileName);
            int traceFileID;

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                using (SqlCommand cmd = new SqlCommand("SELECT TraceFileID FROM dbo.TraceFile WHERE TraceFileName = @TraceFileName AND ServerID = @ServerID ", conn))

                {
                    

                    cmd.Parameters.AddWithValue("@ServerID", serverID);
                    SqlParameter fileNameParameter = new SqlParameter("@TraceFileName", SqlDbType.NVarChar, 128);
                    fileNameParameter.Value = fileNameOnly;
                    cmd.Parameters.Add(fileNameParameter);
                    conn.Open();
                    SqlDataReader rdr = cmd.ExecuteReader(CommandBehavior.CloseConnection);
                    if (rdr.HasRows)
                    {
                        rdr.Read();
                        traceFileID = rdr.GetInt32(rdr.GetOrdinal("TraceFileID"));
                    }
                    else
                        traceFileID = 0;
                    rdr.Close();

                }
}
            return traceFileID;
        }

        private static int SaveServerName(string serverName)
        {
            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                string sql = @"SET NOCOUNT ON;
IF EXISTS(SELECT * FROM dbo.ServerDim WHERE ServerName = @Value)
    SELECT  ServerID FROM dbo.ServerDim WHERE ServerName = @Value;
ELSE
  BEGIN

    INSERT dbo.ServerDim (ServerName)
    VALUES (@Value);

    SELECT  ServerID = CAST(SCOPE_IDENTITY() AS INT);
  END";


                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    SqlParameter serverNameParameter = new SqlParameter("@Value", SqlDbType.NVarChar, 256);
                    serverNameParameter.Value = serverName;
                    cmd.Parameters.Add(serverNameParameter);
                    conn.Open();
                    SqlDataReader rdr = cmd.ExecuteReader(CommandBehavior.CloseConnection);
                    rdr.Read();
                    int traceFileID = rdr.GetInt32(rdr.GetOrdinal("ServerID"));
                    rdr.Close();
                    return traceFileID;
                }
            }

        }


    }
}
