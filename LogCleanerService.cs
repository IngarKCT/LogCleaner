using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Timers;
using System.Threading.Tasks;

public enum ServiceState
{
    SERVICE_STOPPED = 0x00000001,
    SERVICE_START_PENDING = 0x00000002,
    SERVICE_STOP_PENDING = 0x00000003,
    SERVICE_RUNNING = 0x00000004,
    SERVICE_CONTINUE_PENDING = 0x00000005,
    SERVICE_PAUSE_PENDING = 0x00000006,
    SERVICE_PAUSED = 0x00000007,
}

[StructLayout(LayoutKind.Sequential)]
public struct ServiceStatus
{
    public int dwServiceType;
    public ServiceState dwCurrentState;
    public int dwControlsAccepted;
    public int dwWin32ExitCode;
    public int dwServiceSpecificExitCode;
    public int dwCheckPoint;
    public int dwWaitHint;
};

namespace LogCleaner
{
    // sort files by LastWriteTIme, oldest first
    public class WriteTimeComparer : IComparer<FileInfo>
    {
        int IComparer<FileInfo>.Compare(FileInfo x, FileInfo y)
        {
            if (x.LastWriteTime > y.LastWriteTime)
            {
                return 1;
            }
            else if (x.LastWriteTime < y.LastWriteTime)
            {
                return -1;
            }
            return 0;
        }
    }

    public partial class LogCleanerService : ServiceBase
    {
        private int eventId = 0;

        // default log level DEBUG
        private int logLevel = 1;

        // default to testmode off
        private int testMode = 0;

        // run cleanup every 5 minutes (300 seconds)
        const int cleanIntervalSecondsDefault = 300;

        private int cleanIntervalSeconds = cleanIntervalSecondsDefault;

        // clean up files older than 1 day (60 minutes * 24 hours)
        private int cleanAgeMinutes = 60 * 24;

        // clean up files if directory size is larger than 50MiB
        private int cleanSizeMegabytes = 50;

        // directories to watch
        private string[] directories = { };

        // location of the configuration
        const string configregkey = "HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Services\\LogCleaner";

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetServiceStatus(System.IntPtr handle, ref ServiceStatus serviceStatus);

        public LogCleanerService()
        {
            InitializeComponent();

            // initialize event log
            eventlog = new System.Diagnostics.EventLog();
            if (!System.Diagnostics.EventLog.SourceExists("LogCleaner"))
            {
                System.Diagnostics.EventLog.CreateEventSource("LogCleaner", "Application");
            }
            eventlog.Source = "LogCleaner";
            eventlog.Log = "Application";

            ReadSettings();

            WriteSettings();

            eventId++;
        }

        private void ReadSettings()
        {
            // read default configuration from Computer\HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\LogCleaner
            try
            {                
                logLevel = Int32.Parse(Registry.GetValue(configregkey, "LogLevel", logLevel.ToString()).ToString());
                testMode = Int32.Parse(Registry.GetValue(configregkey, "TestMode", testMode.ToString()).ToString());
                cleanIntervalSeconds = Int32.Parse(Registry.GetValue(configregkey, "CleanIntervalSeconds", cleanIntervalSeconds.ToString()).ToString());
                cleanAgeMinutes = Int32.Parse(Registry.GetValue(configregkey, "CleanAgeMinutes", cleanAgeMinutes.ToString()).ToString());
                cleanSizeMegabytes = Int32.Parse(Registry.GetValue(configregkey, "CleanSizeMegabytes", cleanSizeMegabytes.ToString()).ToString());
                directories = (string[])Registry.GetValue(configregkey, "CleanDirectories", directories);
            }
            catch (Exception ex)
            {
                eventlog.WriteEntry("Could not read Service configuration " + ex.Message , EventLogEntryType.Warning, eventId);
            }

            if (cleanIntervalSeconds <= 0)
            {
                cleanIntervalSeconds = cleanIntervalSecondsDefault;
            }
        }

        private void WriteSettings()
        {
            // write settings to registry
            try
            {
                Registry.SetValue(configregkey, "LogLevel", logLevel.ToString(), RegistryValueKind.String);
                // Don't write the TestMode value, this is a debug option that has to be added explicitly
                // Registry.SetValue(configregkey, "TestMode", testMode.ToString(), RegistryValueKind.String);
                Registry.SetValue(configregkey, "CleanIntervalSeconds", cleanIntervalSeconds.ToString(), RegistryValueKind.String);
                Registry.SetValue(configregkey, "CleanAgeMinutes", cleanAgeMinutes.ToString(), RegistryValueKind.String);
                Registry.SetValue(configregkey, "CleanSizeMegabytes", cleanSizeMegabytes.ToString(), RegistryValueKind.String);
                Registry.SetValue(configregkey, "CleanDirectories", directories, RegistryValueKind.MultiString);
            }
            catch
            {
                eventlog.WriteEntry("Could not save Service configuration", EventLogEntryType.Warning, eventId);
            }

        }

        private long scanSubdirectories(string dirfullname, List<FileInfo> globalfileinfolist)
        {
            // calculate total size of all files in the parent directory
            long dirsize = 0;
            DirectoryInfo dirinfo = new DirectoryInfo(dirfullname);
            FileInfo[] fileinfo = dirinfo.GetFiles();
            foreach (FileInfo f in fileinfo)
            {
                globalfileinfolist.Add(f);
                dirsize += f.Length;
            }

            // scan subdirectories
            string[] subdirlist = Directory.GetDirectories(dirfullname);
            for (int s = 0; s < subdirlist.Length; ++s)
            {
                dirsize += scanSubdirectories(subdirlist[s], globalfileinfolist);

                if ((Directory.GetFiles(subdirlist[s]).Length == 0) && (Directory.GetDirectories(subdirlist[s]).Length == 0))
                {
                    // delete empty subdirectories (only if they were already empty)
                    if (logLevel > 1)
                    {
                        eventlog.WriteEntry("Removing empty directory " + subdirlist[s], EventLogEntryType.Information, eventId);
                    }
                    // make sure we're not in test mode
                    if (testMode == 0)
                    {
                        try
                        {
                            // delete directory
                            Directory.Delete(subdirlist[s]);
                        }
                        catch (Exception ex)
                        {
                            eventlog.WriteEntry("Could not delete directory " + subdirlist[s] + " " + ex.Message, EventLogEntryType.Error, eventId);
                        }
                    }
                }
            }

            return dirsize;
        }

        private void scanDirectory(string dirfullname)
        {
            List<FileInfo> fileinfolist = new List<FileInfo>();

            // calculate disk usage if this directory (including files in subdirectories)
            long dirsize = scanSubdirectories(dirfullname, fileinfolist);

            // size in megabytes
            if (dirsize >= cleanSizeMegabytes * 1024 * 1024)
            {
                if (logLevel > 1)
                {
                    eventlog.WriteEntry("Directory " + dirfullname + " contains " + (dirsize / (1024 * 1024)) + " MiB and exceeds " + cleanSizeMegabytes + " MiB", EventLogEntryType.Warning, eventId);
                }
                else if (logLevel > 0)
                {
                    eventlog.WriteEntry("Cleaning " + dirfullname, EventLogEntryType.Information, eventId);
                }

                // sort files by Last Write time, oldest first
                WriteTimeComparer fileinfocompare = new WriteTimeComparer();
                fileinfolist.Sort(fileinfocompare);

                DateTime now = DateTime.Now;

                foreach (FileInfo f in fileinfolist)
                {
                    // caculate the age of the file in minutes
                    long ticks = now.Ticks - f.LastWriteTime.Ticks;
                    TimeSpan elapsed = new TimeSpan(ticks);

                    if ((elapsed.TotalMinutes >= cleanAgeMinutes) && (dirsize >= cleanSizeMegabytes * 1024 * 1024))
                    {
                        if (logLevel > 1)
                        {
                            eventlog.WriteEntry("Deleting " + f.FullName + " age " + Math.Floor(elapsed.TotalMinutes) + " minutes", EventLogEntryType.Information, eventId);
                        }

                        // subtract the size from the file to be deleted from the total directory size
                        dirsize -= f.Length;

                        // make sure we're not in test mode
                        if (testMode == 0)
                        {
                            try
                            {
                                // delete file
                                File.Delete(f.FullName);
                            }
                            catch (Exception ex)
                            {
                                eventlog.WriteEntry("Could not delete file " + f.FullName + " " + ex.Message, EventLogEntryType.Error, eventId);
                            }
                        }

                    }
                }
            }
            else
            {
                // size limit hasn't been exceeddd
                if (logLevel > 1)
                {
                    eventlog.WriteEntry("Watching " + dirfullname + " " + (dirsize / (1024 * 1024)) + "MiB", EventLogEntryType.Information, eventId);
                }
            }
        }

        protected override void OnStart(string[] args)
        {
            if (logLevel > 1)
            {
                eventlog.WriteEntry("LogCleaner cleaning every " + cleanIntervalSeconds.ToString() + " seconds.", EventLogEntryType.Information, eventId);
            }

            if (logLevel > 0)
            {
                if (directories.Length == 0)
                {
                    eventlog.WriteEntry("No directories to watch", EventLogEntryType.Warning);
                }
                else if (directories.Length == 1)
                {
                    eventlog.WriteEntry("1 directory to watch", EventLogEntryType.Information);
                }
                else
                {
                    eventlog.WriteEntry(directories.Length + " directories to watch", EventLogEntryType.Information);
                }
            }

            ServiceStatus serviceStatus = new ServiceStatus();

            // Update the service state to Start Pending.
            serviceStatus.dwCurrentState = ServiceState.SERVICE_START_PENDING;
            // TODO read docs on dwWaitHint (timeout before kill)
            // serviceStatus.dwWaitHint = 100000;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);

            // Set up a timer that triggers every minute.
            Timer timer = new Timer();
            timer.Interval = cleanIntervalSeconds * 1000; // timeout in seconds
            timer.Elapsed += new ElapsedEventHandler(this.OnTimer);
            timer.Start();

            // Update the service state to Running.
            serviceStatus.dwCurrentState = ServiceState.SERVICE_RUNNING;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);

            eventId++;
        }

        protected override void OnStop()
        {
            if (logLevel > 1)
            {
                eventlog.WriteEntry("LogCleaner stop");
            }

            ServiceStatus serviceStatus = new ServiceStatus();

            /*
            // Update the service state to Stop Pending.            
            serviceStatus.dwCurrentState = ServiceState.SERVICE_STOP_PENDING;
            serviceStatus.dwWaitHint = 100000;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);
            */

            // Update the service state to Stopped.
            serviceStatus.dwCurrentState = ServiceState.SERVICE_STOPPED;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);
        }

        public void OnTimer(object sender, ElapsedEventArgs args)
        {

            ReadSettings();

            if (logLevel > 1)
            {
                eventlog.WriteEntry("Cleaning files older than " + cleanAgeMinutes + " minutes in directories larger than " + cleanSizeMegabytes + " MiB", EventLogEntryType.Information, eventId);
            }

            for (int d = 0; d < directories.Length; ++d)
            {
                string dir = directories[d];

                if (Directory.Exists(dir))
                {
                    scanDirectory(dir);

                }
            }

            ++eventId;
        }
    }
}
