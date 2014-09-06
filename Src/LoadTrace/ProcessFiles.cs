using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.IO;
using System.Security.AccessControl;

namespace LoadTrace
{
    partial class Program
    {
        public static void ProcessFiles(string traceWildcard, string appConfigServerName)
        {

            // Log how many files we are going to process
            WriteNumberOfFiles(traceWildcard);
            // Process all the files in the wildcard parameter
            string wildcard = Path.GetFileName(traceWildcard);
            var files = Directory.EnumerateFiles(Path.GetDirectoryName(traceWildcard), Path.GetFileName(traceWildcard));

            // Need a way to handle files where we only need read-only
            // if we are only importing and not moving

            foreach (var f in files)
            {
                string fullFileName = f.ToString();

                if (IsFileUsable(f))
                {
                    // move it to a work directory and process there
                    if (WORK_DIR.Length > 0)
                    {
                        WriteVerboseLog("Moving {0}", Path.GetFileName(fullFileName));
                        string destinationFileName = Path.Combine(WORK_DIR, Path.GetFileName(fullFileName));
                        System.IO.File.Move(fullFileName, destinationFileName);
                        
                        // We've moved the file.  Start using that location.
                        fullFileName = destinationFileName;
                    }


                    WriteVerboseLog("Processing: " + Path.GetFileName(fullFileName));
                    try
                    {
                        ProcessTraceFile(fullFileName, appConfigServerName);
                    }
                    catch (DuplicateTraceExcption)
                    {
                        WriteLog("Skipping {0}: Already in the database.", Path.GetFileName(fullFileName));
                    }
                    catch (NoServerNameException)
                    {
                        WriteLog("Skipping {0}: No server name.", Path.GetFileName(fullFileName));
                    }
                }
            }
        }

        private static void WriteNumberOfFiles(string traceWildcard)
        {
            int fileCount = 0;
            long fileSize = 0;
            var files = Directory.EnumerateFiles(Path.GetDirectoryName(traceWildcard), Path.GetFileName(traceWildcard));
            foreach (var f in files)
            {
                fileCount++;
                FileInfo fi = new FileInfo(f);
                fileSize += fi.Length;
            }
            WriteLog("Found {0} files with {1:#,##0.0} MB", fileCount, ((float)fileSize / (1024.0 *1024.0)));
        }

        private static bool IsFileUsable(string fileName)
        {
            FileInfo file = new FileInfo(fileName);
            
            try
            {
                using (FileStream fileStream = new FileStream(fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    fileStream.Close();
                    return true;
                }
            }
            catch (UnauthorizedAccessException) // Reader doesn't have permissions on the file
            {
                try
                {
                    FileSecurity fSecurity = File.GetAccessControl(fileName);
                    fSecurity.AddAccessRule(new FileSystemAccessRule(@"L70\billgraziano", FileSystemRights.FullControl, AccessControlType.Allow));
                    File.SetAccessControl(fileName, fSecurity);
                    WriteLog("Updated permissions on {0}", file.Name);
                    return true;
                }
                catch (Exception ex)
                {
                    WriteLog(ex.Message);
                    WriteLog("{0} isn't accessbile", file.Name);
                    return false;
                }
                
            }
            catch (IOException) // Reader can't get exclusive access to the file
            {
                WriteLog("{0} is in use", file.Name);
                return false;
            }
            
            
        }

        public static void ProcessServers()
        {
            foreach (ServerElement s in LoadTraceConfig.AppConfigFile.Servers)
            {
                WriteLog("Processing: {0}", s.Name);
                ProcessFiles(s.traceWildcard, s.Name);
            }
        }
    }
}
