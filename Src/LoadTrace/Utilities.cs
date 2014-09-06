using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Data;
using System.Data.SqlClient;

using Microsoft.SqlServer.Management.Trace;

namespace LoadTrace
{

    
    partial class Program
    {
        public static void WriteLog(string message)
        {
            Console.WriteLine(message);
         
        }

        public static void WriteLog(string message, params object[] args)
        {
            Console.WriteLine(message, args);
        }

        public static void WriteVerboseLog(string message)
        {
            if (VERBOSE)
                Console.WriteLine(message);
        }

        public static void WriteVerboseLog(string message, params object[] args)
        {
            if (VERBOSE)
                Console.WriteLine(message, args);
        }
    }

    class Utilities
    {
        public static string GetConnectionString()
        {
            if (Program.CONNECTION_STRING.Length > 0)
                return Program.CONNECTION_STRING;

            SqlConnectionStringBuilder conn = new SqlConnectionStringBuilder();
            conn.DataSource = Program.SERVER;
            conn.InitialCatalog = Program.DATABASE;
            conn.ApplicationName = "TraceLoad.EXE";
            conn.IntegratedSecurity = true;
            conn.ConnectTimeout = 10;
            return conn.ConnectionString;

        }

        public static string GetServerNameFromTrace(string traceName)
        {
            TraceFile t = new TraceFile();
            t.InitializeAsReader(traceName);

            // check if the column exists
            DataTable columns = t.GetSchemaTable();
            bool foundServerName = false;
            foreach (DataRow r in columns.Rows)
            {
                if (r["ColumnName"].ToString() == "ServerName")
                    foundServerName = true;
            }

            if (!foundServerName)
            {
                t.Close();
                t.Dispose();
                throw new NoServerNameException();
            }
            int count = 0;
            string serverName;


            while (t.Read())
            {
                serverName = t.GetString(t.GetOrdinal("ServerName"));
                if (serverName != null)
                {
                    if (serverName.Length > 0)
                    {
                        t.Close();
                        t.Dispose();
                        return serverName;
                    }
                }
                count++;

            }
            t.Close();
            t.Dispose();
            throw new NoServerNameException();
            
        }

        internal static void TestDatabaseServer(string connectionString)
        {
            SqlConnection conn = new SqlConnection(connectionString);
            conn.Open();
            SqlCommand cmd = new SqlCommand("SELECT * FROM dbo.DatabaseVersion;", conn);
            SqlDataReader rdr = cmd.ExecuteReader(CommandBehavior.CloseConnection);

            while (rdr.Read())
            {

            }
            rdr.Close();

        }

        internal static string CleanUpServerName(string serverName)
        {
            // if we get MACHINE\INSTANCE and they are equal, just return the first bit.
            if (serverName.Contains(@"\"))
            {
                string machineName = serverName.Substring(0, serverName.IndexOf(@"\"));
                string instanceName = serverName.Substring(serverName.IndexOf(@"\") + 1, serverName.Length - machineName.Length - 1);
                if (machineName == instanceName)
                    return machineName;
                else
                    return serverName;
            }
            else
                return serverName;
        }
    }
}
