/*
** APP_HotPlug.cs
**
** Copyright © 2016 Future Technology Devices International Limited
**
** C# Source file for Hotplug utility
**
** Author: FTDI
** Project: C# Hotplug utility
** Module: Hot plugging implementation
** Description: 
**  HotPlug  - Hotplugging using WMI ManagementEventWatcher
**  HotPlug2 - Hotplugging using Win32 RegisterDeviceNotification
**
** History:
**  1.0.0.0	- Initial version
**
*/

using System;
using System.Management;
using System.Runtime.InteropServices;



namespace DemoUtility
{
    /// <summary>
    /// USB Hot Plug detection using WMI ManagementEventWatcher
    /// </summary>
    public class HotPlug
    {
        /// <summary>
        /// Delegate for the callbacks to be registered
        /// </summary>
        public delegate void DelegateHotPlug(object sender, EventArrivedEventArgs e);

        /// <summary>
        /// Register for device notification on device plugging/plugout
        /// </summary>
        public void Register(DelegateHotPlug DeviceInsertedEvent, DelegateHotPlug DeviceRemovedEvent)
        {
            // setup the query to monitor insertion
            insertQuery = new WqlEventQuery(
                "SELECT * FROM __InstanceCreationEvent " +
                "WITHIN 2 " +
                "WHERE TargetInstance ISA 'Win32_USBHub'"
                );
            insertWatcher = new ManagementEventWatcher(insertQuery);
            insertWatcher.EventArrived += new EventArrivedEventHandler(DeviceInsertedEvent);
            insertWatcher.Start();

            // setup the query to monitor removal
            removeQuery = new WqlEventQuery(
                "SELECT * FROM __InstanceDeletionEvent " +
                "WITHIN 2 " +
                "WHERE TargetInstance ISA 'Win32_USBHub'"
                );
            removeWatcher = new ManagementEventWatcher(removeQuery);
            removeWatcher.EventArrived += new EventArrivedEventHandler(DeviceRemovedEvent);
            removeWatcher.Start();
        }

        /// <summary>
        /// Unregister for device notification on device plugging/plugout
        /// </summary>
        public void Unregister()
        {
            if (insertWatcher != null)
            {
                insertWatcher.Stop();
                insertWatcher.Dispose();
                insertWatcher = null;
            }

            if (removeWatcher != null)
            {
                removeWatcher.Stop();
                removeWatcher.Dispose();
                removeWatcher = null;
            }
        }

        private WqlEventQuery insertQuery = null;
        private WqlEventQuery removeQuery = null;
        private ManagementEventWatcher insertWatcher = null;
        private ManagementEventWatcher removeWatcher = null;
    }

    /// <summary>
    /// USB Hot Plug detection using Win32 RegisterDeviceNotification
    /// </summary>
    public class HotPlug2
    {
        /// <summary>
        /// Registers a window to receive notifications when USB devices are plugged or unplugged.
        /// </summary>
        /// <param name="windowHandle">Handle to the window receiving notifications.</param>
        public static void Register(IntPtr hWindow, Guid tGuid)
        {
            if (hNotification == IntPtr.Zero)
            {
                var dbi = new DevBroadcastDeviceinterface();
                dbi.DeviceType = DBT_DEVTYP_DEVICEINTERFACE;
                dbi.ClassGuid = tGuid;
                dbi.Size = Marshal.SizeOf(dbi);
                IntPtr pBuffer = Marshal.AllocHGlobal(dbi.Size);
                Marshal.StructureToPtr(dbi, pBuffer, true);

                hNotification = RegisterDeviceNotification(hWindow, pBuffer, 0);
            }
        }

        /// <summary>
        /// Unregisters the window for USB device notifications
        /// </summary>
        public static void Unregister()
        {
            if (hNotification != IntPtr.Zero)
            {
                UnregisterDeviceNotification(hNotification);
                hNotification = IntPtr.Zero;
            }
        }

        /// <summary>
        /// Delegates for Win32 RegisterDeviceNotification and UnregisterDeviceNotification
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr RegisterDeviceNotification(IntPtr recipient, IntPtr notificationFilter, int flags);
        [DllImport("user32.dll")]
        private static extern bool UnregisterDeviceNotification(IntPtr handle);

        /// <summary>
        /// Structure for the WndProc callback function corresponding to the lParam parameter
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Size = 284)]
        public struct DevBroadcastDeviceinterface
        {
            internal int Size;
            internal int DeviceType;
            internal int Reserved;
            internal Guid ClassGuid;
            internal short Name;
        }

        public const int DBT_DEVICEARRIVAL = 0x8000;        // system detected a new device        
        public const int DBT_DEVICEREMOVECOMPLETE = 0x8004; // device is gone      
        public const int WM_DEVICECHANGE = 0x0219;          // device change event      
        private const int DBT_DEVTYP_DEVICEINTERFACE = 5;
        private static IntPtr hNotification = IntPtr.Zero;
    }
}
