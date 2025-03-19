/*
** APP_LogFile.cs
**
** Copyright © 2016 Future Technology Devices International Limited
**
** C# Source file for Logfile utility
**
** Author: FTDI
** Project: C# Logfile utility
** Module: Log file implementation
**
** History:
**  1.0.0.0	- Initial version
**
*/

using System;
using System.IO;
using System.Threading;
using System.Reflection;
using System.Management;



namespace DemoUtility
{
    public class LogFile
    {
        /// <summary>
        /// Set the file path and file append option for debugging log file
        /// </summary>
        public static bool Set(string filename, bool append)
        {
            if (filename == null || filename.Equals(""))
            {
                return false;
            }

            var directory = Environment.CurrentDirectory;
            if (directory == null)
            {
                return false;
            }

            filePath = directory + "\\" + filename;
            bFileAppend = append;

            return true;
        }

        /// <summary>
        /// Log data to the log file
        /// </summary>
        public static void Log(string format, params object[] parameters)
        {
            try
            {
                using (var fileLog = new StreamWriter(filePath, bFileAppend))
                {
                    fileLog.Write(DateTime.Now.ToString("[0x{0:X8}][hh:mm:ss.ff] "),
                        Thread.CurrentThread.ManagedThreadId);

                    fileLog.WriteLine(format, parameters);
                }
            }
            catch (IOException)
            {
                return;
            }
        }

        /// <summary>
        /// Log data to the log file given a specific directory and filename
        /// </summary>
        public static void Log(byte[] buffer, string directory, string filename, bool append)
        {
            string path = directory + "\\" + filename;

            try
            {
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (FileStream fileStream = new FileStream(path, append ? FileMode.Append : FileMode.Create))
                {
                    fileStream.Write(buffer, 0, buffer.Length);
                }
            }
            catch (IOException ex)
            {
                LogFile.Log(ex.ToString());
            }
        }

        /// <summary>
        /// Log the system environment
        /// </summary>
        public static void LogEnvironment()
        {
            LogFile.Log("*****************************************************************");
            LogFile.Log("TEST ENVIRONMENT:");
            LogFile.Log("\tMachineName: {0}", Environment.MachineName);
            LogFile.Log("\tUserName:    {0}", Environment.UserName);
            LogFile.Log("\tOSVersion:   {0}", Environment.OSVersion.ToString());
            LogFile.Log("\t64-bit:      {0}", Environment.Is64BitOperatingSystem.ToString());
            LogFile.Log("\tProcessors:  {0}", Environment.ProcessorCount);
            LogFile.Log("\tCLRVersion:  {0}", Environment.Version.ToString());
            LogFile.Log("\tRunDate:     {0}", DateTime.Now.ToString("MM/dd/yyyy"));
            LogFile.Log("\tRunTime:     {0}", DateTime.Now.ToString("hh:mm:ss.ff tt"));
            LogFile.Log("\tAppName:     {0}", Assembly.GetExecutingAssembly().GetName().Name);
            LogFile.Log("\tAppVersion:  {0}", Assembly.GetExecutingAssembly().GetName().Version);
            LogFile.Log("*****************************************************************\r\n");
        }

        /// <summary>
        /// Log the all USB devices connected
        /// </summary>
        public static void LogUSBDevicesConnected()
        {
            ManagementObjectCollection collection;

            using (var searcher = new ManagementObjectSearcher(@"Select * From Win32_USBHub"))
            {
                collection = searcher.Get();
            }

            LogFile.Log("USB DEVICES CONNECTED:\r\n");
            if (collection != null)
            {
                foreach (var device in collection)
                {
                    LogFile.Log("\t{0} ", (string)device.GetPropertyValue("Description"));
                    LogFile.Log("\t{0} ", (string)device.GetPropertyValue("DeviceID"));
                    LogFile.Log("");
                }
            }

            collection.Dispose();
        }

        private static bool bFileAppend = false;
        private static string filePath = "";
    }
}
