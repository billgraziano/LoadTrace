using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Data;
using Microsoft.SqlServer.Management.Trace;
using System.Data.SqlClient;

using System.Text.RegularExpressions;


namespace LoadTrace
{
    class TraceDimension
    {
        // replace with a case-insensitve Dictionary that matches SQL Server
        // Use TryGetValue to return the integer
        //public SortedList<string, int> ListItems;
        public Dictionary<string, int> ListItems;
        private string _traceColumn;
        private string _tableName;
        private string _indexColumn;
        private string _nameColumn;
        private int _parameterLength;

        public TraceDimension(string traceColumn, string tableName, string indexColumn, string nameColumn, int parameterLength)
        {
            // ListItems = new SortedList<string, int>(StringComparer.CurrentCultureIgnoreCase);
            ListItems = new Dictionary<string, int>(500, StringComparer.CurrentCultureIgnoreCase);
            _traceColumn = traceColumn;
            _tableName = tableName;
            _indexColumn = indexColumn;
            _nameColumn = nameColumn;
            _parameterLength = parameterLength;
        }

        //public TraceDimension(string traceColumn, string tableName, string indexColumn, string nameColumn, int parameterLength, string connectionString)
        //{
        //    TraceDimension t = new TraceDimension(traceColumn, tableName, indexColumn, nameColumn, parameterLength);
        //    t.GetDatabaseValues;
        //    return t;
        //}

        public int GetIndex(ref TraceFile traceFile, string connectionString)
        {
            string value = GetTraceColumn(ref traceFile);

            // Consider fixing up application names:
            // SQLAgent - TSQL JobStep (Job 0xB2C8EC999BD20749A955C693CCDD0077 : Step 1)
            // DatabaseMail - SQLAGENT - Id<12384>  <- Definitely this one.  Not sure about others.
            // Microsoft SQL Server Management Studio - Transact-SQL IntelliSense
            // Microsoft SQL Server Management Studio - Query
            // w3wp@/LM/W3SVC/2/Root-1-129814544568132731

            value = value.Left(256);

            // Clean up any application Names
            if (_traceColumn == "ApplicationName")
            {
                value = CleanUpAppName(value);
            }

            // if we have the value, return it
            int result = 0;
            ListItems.TryGetValue(value, out result);
            if (result != 0)
                return result;
            //if (ListItems.ContainsKey(value))
            //    return ListItems[value];
            else
            {
                int newID = SaveNewValue(connectionString, value);
                
                return newID; 
            }

            //return 0;
        }

        private static string CleanUpAppName(string appName)
        {
            string newAppName = appName;
            foreach (AppNameFixElement a in LoadTraceConfig.AppConfigFile.AppNameFixes)
            {
                Regex r = new Regex(a.Regex, RegexOptions.IgnoreCase);
                newAppName = r.Replace(newAppName, a.Replacement);
            }


            return newAppName;
        }

        private string GetTraceColumn(ref TraceFile traceFile)
        {
            // if the value is NULL, convert to an empty string
            string value = traceFile.IsDBNull(traceFile.GetOrdinal(_traceColumn))
                ? ""
                : traceFile.GetString(traceFile.GetOrdinal(_traceColumn)).Trim();
            return value;
        }

        private int SaveNewValue(string connectionString, string value)
        {
            // put it in the database
            string sql;
            int newID;
            sql = @"SET NOCOUNT ON;
IF EXISTS(SELECT * FROM dbo." + _tableName + @" WHERE " + _nameColumn + @" = @Value)
    SELECT  KeyValue = " + _indexColumn + @" FROM dbo." + _tableName + @" WHERE " + _nameColumn + @" = @Value;
ELSE
  BEGIN

    INSERT dbo." + _tableName + @" (" + _nameColumn + @")
    VALUES (@Value);

    SELECT  KeyValue = CAST(SCOPE_IDENTITY() AS INT);
  END";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.CommandType = CommandType.Text;
                //cmd.Parameters.Add(new SqlParameter("Value", value));
                SqlParameter parm = new SqlParameter("Value", SqlDbType.NVarChar, _parameterLength);
                parm.Value = value;
                cmd.Parameters.Add(parm);
                conn.Open();

                using (SqlDataReader rdr = cmd.ExecuteReader())
                {
                    rdr.Read();
                    newID = rdr.GetInt32(0);
                    rdr.Close();
                    conn.Close();
                }
            }
            ListItems.Add(value, newID);
            return newID;
        }

        public void GetDatabaseValues(string connectionString)
        {
            SqlConnection conn = new SqlConnection(connectionString);
            conn.Open();
            SqlCommand cmd = new SqlCommand("SELECT TOP 1000 " + _indexColumn + ", " + _nameColumn + " FROM dbo." + _tableName, conn);
            SqlDataReader rdr = cmd.ExecuteReader();

            while (rdr.Read())
            {
                string lookupValue = rdr.GetString(1);

                // only add the list item if it doesn't already exist
                if (!ListItems.ContainsKey(lookupValue))
                    ListItems.Add(lookupValue, rdr.GetInt32(0));
            }

            rdr.Close();
            conn.Close();
        }
    }

    public struct LoginKey
    {
        // http://stackoverflow.com/questions/562213/multi-column-primary-key-mapped-to-a-dictionary-aka-struct-vs-class
        public int HostID;
        public int ApplicationID;
        public int LoginID;

        public override bool Equals(object obj)
        {
            if (!(obj is LoginKey)) { return false;  }
            var other = (LoginKey)obj;
            return HostID == other.HostID &&
                ApplicationID == other.ApplicationID &&
                LoginID == other.LoginID;

        }
        public override int GetHashCode()
        {
            //http://stackoverflow.com/questions/371328/why-is-it-important-to-override-gethashcode-when-equals-method-is-overriden-in-c

            int hash = 13;
            hash = (hash * 7) + HostID.GetHashCode();
            hash = (hash * 7) + ApplicationID.GetHashCode();
            hash = (hash * 7) + LoginID.GetHashCode();

            return hash;
        }


    }

    public struct LoginDetails
    {
        public DateTime FirstLogin;
        public DateTime LastLogin;
        public int LoginCount;
    }
}
