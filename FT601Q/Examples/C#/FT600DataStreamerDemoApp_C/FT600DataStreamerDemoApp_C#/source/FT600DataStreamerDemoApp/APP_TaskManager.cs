/*
** APP_TaskManager.cs
**
** Copyright © 2016 Future Technology Devices International Limited
**
** C# Source file for Demo Application.
**
** Author: FTDI
** Project: C# Data Streamer Demo Application
** Module: Task Manager thread implementation
**
** History:
**  1.0.0.0	- Initial version
**
*/

using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using System.Threading;
using DemoUtility;
using FTD3XX_NET;



namespace DemoApp
{
    public class TaskManager
    {
        /// <summary>
        /// Entry point of the task manager thread
        /// </summary>
        public void Run()
        {
            bool bResult = false;
            bool bExit = false;

            LogFile.Log("TaskManager begin...");

            objectWriter = new WriterThread[ulMaxChannels];
            threadWriter = new Thread[ulMaxChannels];
            objectReader = new ReaderThread[ulMaxChannels];
            threadReader = new Thread[ulMaxChannels];

            
            while (Application.Current != null && bExit == false)
            {
                TaskQueueEvent.WaitOne();
                LogFile.Log("TaskManager Task Queued");
                if (queueTasks.Count == 0)
                {
                    LogFile.Log("TaskManager Task count is empty");

                    Thread.Sleep(32);
                    continue;
                }

                // Dequeue a task
                Task task = RemoveTask();
                if (task == null)
                {
                    LogFile.Log("TaskManager task was null...");
                    continue;
                }

                // Process a task
                switch (task.Param.eType)
                {
                    case Task.TaskType.Detect:
                        {
                            UInt32 ulNumDevicesConnected;

                            bResult = ProcessDetect(task.Param.oTransferParams.fxnCallbackDebug, out ulNumDevicesConnected);

                            task.SetResult(bResult, ulNumDevicesConnected);
                            CallUICallback(task, bResult);
                            break;
                        }

                    case Task.TaskType.Open:
                        {
                            bResult = ProcessOpen(task.Param.oTransferParams.fxnCallbackDebug, 
                                task.Param.bOpenBy, task.Param.OpenByString);

                            if (bResult)
                            {
                                byte bNumWritePipes = 0, bNumReadPipes = 0;
                                bool bTransferResult = false;
                                bool bUsb3 = true;

                                GetNumPipes(ref bNumWritePipes, ref bNumReadPipes);

                                //bTransferResult = DoSimpleTransfer(bNumWritePipes, bNumReadPipes, ref bUsb3);
                                //if (!bTransferResult)
                                //{
                                //    ProcessClose();
                                //    bResult = false;
                                //}

                                task.SetResult(bResult, bNumWritePipes, bNumReadPipes, bTransferResult, bUsb3);
                            }

                            CallUICallback(task, bResult);
                            break;
                        }

                    case Task.TaskType.Close:
                        {
                            bResult = ProcessClose();

                            CallUICallback(task, bResult);
                            break;
                        }

                    case Task.TaskType.Start:
                        {
                            bResult = ProcessTransfer(task.Param.oTransferParams);

                            // UI Callback will be called on the completion routine
                            break;
                        }

                    case Task.TaskType.Stop:
                        {
                            bResult = ProcessAbortTransfer(task.Param.oTransferParams);

                            //CallUICallback(task, bResult);
                            break;
                        }

                    case Task.TaskType.TestMode:
                        {
                            bResult = ProcessTestMode(task.Param.oTransferParams,
                                task.Param.bOpenBy, task.Param.OpenByString);

                            CallUICallback(task, bResult);
                            break;
                        }
                    case Task.TaskType.Exit:
                        {
                            bExit = true;
                            break;
                        }

                }
            }

            LogFile.Log("TaskManager end...\r\n");
        }

        /// <summary>
        /// Add task to the queue of the task manager
        /// </summary>
        public void AddTask(Task newTask)
        {
            LogFile.Log("AddTask...{0}", newTask.Type);
            lock (queueLock)
            {
                queueTasks.Enqueue(newTask);
            }
            TaskQueueEvent.Set();

        }

        /// <summary>
        /// Internal function to remove task from the queue of the task manager
        /// </summary>
        private Task RemoveTask()
        {
            lock (queueLock)
            {
                Task taskItem = queueTasks.Dequeue();
                LogFile.Log("RemoveTask...{0}", taskItem.Type);
                return taskItem;
            }
        }

        /// <summary>
        /// Internal function for task Detect 
        /// </summary>
        private bool ProcessDetect(DelegateCallbackTextBoxDebug fxnCallbackDebug, out UInt32 ulNumDevicesConnected)
        {
            FTDI.FT_STATUS ftStatus = FTDI.FT_STATUS.FT_OTHER_ERROR;


            LogFile.Log("ProcessDetect...");

            ftStatus = d3xxDevice.CreateDeviceInfoList(out ulNumDevicesConnected);
            if (ftStatus != FTDI.FT_STATUS.FT_OK)
            {
                return false;
            }

            if (ulNumDevicesConnected == 0)
            {
                return false;
            }

            List<FTDI.FT_DEVICE_INFO> DeviceInfoList;
            ftStatus = d3xxDevice.GetDeviceInfoList(out DeviceInfoList);
            if (ftStatus != FTDI.FT_STATUS.FT_OK)
            {
                return false;
            }

            LogInfo(fxnCallbackDebug, "List of Connected Devices!\n\n");

            int i = 0;
            foreach (var DeviceInfo in DeviceInfoList)
            {
                string Description = d3xxDevice.GetDescription(DeviceInfo);
                string SerialNumber = d3xxDevice.GetSerialNumber(DeviceInfo);

                LogInfo(fxnCallbackDebug, String.Format("Device[{0:d}]\n", i++));
                LogInfo(fxnCallbackDebug, String.Format(
                    "\tFlags: 0x{0:X} [{1}] | Type: {2:d} | ID: 0x{3:X8} | ftHandle: 0x{4:X}\n",
                    DeviceInfo.Flags, DeviceInfo.Flags == 0x4 ? "USB3" : "USB2", 
                    DeviceInfo.Type, DeviceInfo.ID, DeviceInfo.ftHandle));
                LogInfo(fxnCallbackDebug, String.Format("\tDescription: {0}\n", Description));
                LogInfo(fxnCallbackDebug, String.Format("\tSerialNumber: {0}\n", SerialNumber));
            }

            return true;
        }

        /// <summary>
        /// Internal function for task Open
        /// </summary>
        private bool ProcessOpen(DelegateCallbackTextBoxDebug fxnCallbackDebug, byte bOpenBy, string OpenByString)
        {
            FTDI.FT_STATUS ftStatus = FTDI.FT_STATUS.FT_OTHER_ERROR;
            bool bResult = false;
            string szMsg;


            LogFile.Log("ProcessOpen...");

            if (d3xxDevice.IsOpen)
            {
                return false;
            }

            switch ((Controls.OpenBy.Type)bOpenBy)
            {
                case Controls.OpenBy.Type.Description:
                    {
                        szMsg = String.Format("\nDevice with Description [" + OpenByString + "] opened ");
                        ftStatus = d3xxDevice.OpenByDescription(OpenByString);
                        if (ftStatus != FTDI.FT_STATUS.FT_OK)
                        {
                            Thread.Sleep(1000);
                            ftStatus = d3xxDevice.OpenByDescription(OpenByString);
                        }
                        break;
                    }
                case Controls.OpenBy.Type.SerialNumber:
                    {
                        szMsg = String.Format("\nDevice with Serial Number [" + OpenByString + "] opened ");
                        ftStatus = d3xxDevice.OpenBySerialNumber(OpenByString);
                        if (ftStatus != FTDI.FT_STATUS.FT_OK)
                        {
                            Thread.Sleep(1000);
                            ftStatus = d3xxDevice.OpenBySerialNumber(OpenByString);
                        }
                        break;
                    }
                case Controls.OpenBy.Type.Index: // fall-through
                default:
                    {
                        szMsg = String.Format("\nDevice at Index [" + OpenByString + "] opened ");
                        ftStatus = d3xxDevice.OpenByIndex(UInt32.Parse(OpenByString));
                        if (ftStatus != FTDI.FT_STATUS.FT_OK)
                        {
                            Thread.Sleep(1000);
                            ftStatus = d3xxDevice.OpenByIndex(UInt32.Parse(OpenByString));
                        }
                        break;
                    }
            }

            if (ftStatus == FTDI.FT_STATUS.FT_OK)
            {
                bResult = true;
                LogInfo(fxnCallbackDebug, String.Format(szMsg + "successfully!\n"));

                if (!d3xxDevice.IsUSB3)
                {
                    LogInfo(fxnCallbackDebug, 
                        "Warning: Device is connected using USB 2 cable and/or through a USB 2 host controller!\n\n");
                }

                LogInfo(fxnCallbackDebug, String.Format(
                    "Device Firmware Version: {0:X4}!\n", d3xxDevice.FirmwareVersion));

                LogInfo(fxnCallbackDebug, String.Format(
                    "D3XX Driver Version: {0:X8} | D3XX Library Version: {1:X8}!\n\n", 
                    d3xxDevice.DriverVersion, d3xxDevice.LibraryVersion));

                LogDeviceInformation();


                var conf = new FTDI.FT_60XCONFIGURATION();
                if (d3xxDevice.GetChipConfiguration(conf) != FTDI.FT_STATUS.FT_OK)
                {
                    return bResult;
                }

                if(conf.FIFOMode == 0)
                {
                    UInt32 ulGPIOMask, ulGPIOData;
                    ulGPIOMask = 3;
                    ulGPIOData = 3;
                    d3xxDevice.EnableGPIO(ulGPIOMask, ulGPIOData);
                    d3xxDevice.WriteGPIO(ulGPIOMask, 0);
                }
            }
            else
            {
                LogInfo(fxnCallbackDebug, String.Format($"Error: {ftStatus.ToString()}\n"));

                System.Diagnostics.Debug.WriteLine($"Error: {ftStatus.ToString()}\n");
            }

            return bResult;
        }

        /// <summary>
        /// Internal function for logging chip configuration and device descriptor
        /// </summary>
        private void LogDeviceInformation()
        {
            var desc = d3xxDevice.DeviceDescriptor;
            LogFile.Log("\tDEVICE DESCRIPTOR");
            LogFile.Log("\tbLength                  : 0x{0:X2}   ({1:d})", desc.bLength, desc.bLength);
            LogFile.Log("\tbDescriptorType          : 0x{0:X2}", desc.bDescriptorType);
            LogFile.Log("\tbcdUSB                   : 0x{0:X4}   ({1})", desc.bcdUSB, desc.bcdUSB >= 0x0300 ? "USB 3" : "USB 2");
            LogFile.Log("\tbDeviceClass             : 0x{0:X2}", desc.bDeviceClass);
            LogFile.Log("\tbDeviceSubClass          : 0x{0:X2}", desc.bDeviceSubClass);
            LogFile.Log("\tbDeviceProtocol          : 0x{0:X2}", desc.bDeviceProtocol);
            LogFile.Log("\tbMaxPacketSize0          : 0x{0:X2}   ({1:d})", desc.bMaxPacketSize0, desc.bMaxPacketSize0);
            LogFile.Log("\tidVendor                 : 0x{0:X4}", desc.idVendor);
            LogFile.Log("\tidProduct                : 0x{0:X4}", desc.idProduct);
            LogFile.Log("\tbcdDevice                : 0x{0:X4}", desc.bcdDevice);
            LogFile.Log("\tiManufacturer            : 0x{0:X2}   ({1})", desc.iManufacturer, d3xxDevice.Manufacturer);
            LogFile.Log("\tiProduct                 : 0x{0:X2}   ({1})", desc.iProduct, d3xxDevice.ProductDescription);
            LogFile.Log("\tiSerialNumber            : 0x{0:X2}   ({1})", desc.iSerialNumber, d3xxDevice.SerialNumber);
            LogFile.Log("\tbNumConfigurations       : 0x{0:X2}", desc.bNumConfigurations);

            var conf = new FTDI.FT_60XCONFIGURATION();
            if (d3xxDevice.GetChipConfiguration(conf) != FTDI.FT_STATUS.FT_OK)
            {
                return;
            }

            LogFile.Log("\tCHIP CONFIGURATION");
            LogFile.Log("\tVendorID                 : 0x{0:X4}", conf.VendorID);
            LogFile.Log("\tProductID                : 0x{0:X4}", conf.ProductID);
            LogFile.Log("\tManufacturer             : " + conf.Manufacturer);
            LogFile.Log("\tDescription              : " + conf.Description);
            LogFile.Log("\tSerialNumber             : " + conf.SerialNumber);
            LogFile.Log("\tPowerAttributes          : 0x{0:X2}", conf.PowerAttributes);
            LogFile.Log("\tPowerConsumption         : 0x{0:X4}", conf.PowerConsumption);
            LogFile.Log("\tFIFOMode                 : 0x{0:X2}", conf.FIFOMode);
            LogFile.Log("\tChannelConfig            : 0x{0:X2}", conf.ChannelConfig);
            LogFile.Log("\tOptionalFeatureSupport   : 0x{0:X4}", conf.OptionalFeatureSupport);
            LogFile.Log("\tFlashEEPROMDetection     : 0x{0:X2}", conf.FlashEEPROMDetection);
        }

        /// <summary>
        /// Internal function for test if transfer is working
        /// </summary>
        private bool DoSimpleTransfer(byte bNumWritePipes, byte bNumReadPipes, ref bool bUsb3)
        {
            bUsb3 = d3xxDevice.IsUSB3;
            UInt32 transferBytes = bUsb3 == true ? (UInt32)16777216 : (UInt32)4194304;
            byte[] writeBytes = new byte[transferBytes];
            byte[] readBytes = new byte[transferBytes];
            bool bRet = true;
            Int32 lTimeOutMs = 2000;
            byte bPipe = 0;

            LogFile.Log("DoSimpleTransfer begin...");

            var Event = new AutoResetEvent(false);

            if (bNumWritePipes > 0)
            {
                bPipe = 0x02;

                System.Threading.Tasks.Task.Run(delegate
                {
                    UInt32 bytesWritten = 0;
                    d3xxDevice.WritePipe(bPipe, writeBytes, (UInt32)writeBytes.Length, ref bytesWritten);
                    Event.Set();
                });

                if (!Event.WaitOne(lTimeOutMs))
                {
                    d3xxDevice.AbortPipe(bPipe);
                    bRet = false;
                }
            }

            if (bNumReadPipes > 0 && bRet)
            {
                bPipe = 0x82;

                System.Threading.Tasks.Task.Run(delegate
                {
                    UInt32 bytesRead = 0;
                    d3xxDevice.ReadPipe(bPipe, readBytes, (UInt32)readBytes.Length, ref bytesRead);
                    Event.Set();
                });

                if (!Event.WaitOne(lTimeOutMs))
                {
                    d3xxDevice.AbortPipe(bPipe);
                    bRet = false;
                }
            }

            LogFile.Log("DoSimpleTransfer end...");

            return bRet;
        }

        /// <summary>
        /// Internal function for task Close
        /// </summary>
        private bool ProcessClose()
        {
            LogFile.Log("ProcessClose...");

            if (d3xxDevice.IsOpen)
            {
                var res = d3xxDevice.Close();
                if (res != FTDI.FT_STATUS.FT_OK)
                {
                    System.Diagnostics.Debug.WriteLine($"Close Error: {res.ToString()}");
                }
            }

            //if (queueTasks != null)
            //{
            //    queueTasks.Clear();
            //}
            LogFile.Log("ProcessClose...end");

            return true;
        }

        /// <summary>
        /// Internal function for task Transfer
        /// </summary>
        private bool ProcessTransfer(WorkerThread.TransferParams oParams)
        {
            LogFile.Log("ProcessTransfer[0x{0:X2}]...", oParams.bPipe);

            oParams.fxnCallbackCompleted = ProcessTransferCompletion;

            if (oParams.IsWritePipe())
            {
                if(objectWriter[oParams.bChannelIndex] != null)
                {
                    LogFile.Log("ProcessTransfer[0x{0:X2}]...Cannot start as its already running", oParams.bPipe);
                    return false;
                }
                objectWriter[oParams.bChannelIndex] = new WriterThread(d3xxDevice, ref oParams);

                if (oParams.bAsync)
                    threadWriter[oParams.bChannelIndex] = new Thread(new ThreadStart(objectWriter[oParams.bChannelIndex].AsyncTask));
                else
                    threadWriter[oParams.bChannelIndex] = new Thread(new ThreadStart(objectWriter[oParams.bChannelIndex].Run));

                threadWriter[oParams.bChannelIndex].Start();

            }
            else
            {
                if (objectReader[oParams.bChannelIndex] != null)
                {
                    LogFile.Log("ProcessTransfer[0x{0:X2}]...Cannot start as its already running", oParams.bPipe);
                    return false;
                }

                objectReader[oParams.bChannelIndex] = new ReaderThread(d3xxDevice, ref oParams);
                if (oParams.bAsync)
                    threadReader[oParams.bChannelIndex] = new Thread(new ThreadStart(objectReader[oParams.bChannelIndex].AsyncTask));
                else
                    threadReader[oParams.bChannelIndex] = new Thread(new ThreadStart(objectReader[oParams.bChannelIndex].Run));

                threadReader[oParams.bChannelIndex].Start();
            }



            return true;
        }

        /// <summary>
        /// Internal function for task Transfer completion routine
        /// </summary>
        private void ProcessTransferCompletion(Task.TaskResult oResult)
        {
            LogFile.Log("ProcessTransferCompletion[0x{0:X2}]...", oResult.bPipe);

            if (oResult.eType == Task.TaskType.TestMode)
            {
                if (oResult.bPipe < 0x80)
                {
                    ulWriteMBps = oResult.ulMBps;
                }
                else
                {
                    ulReadMBps = oResult.ulMBps;
                }
            }
            else
            {
                if (Application.Current != null)
                {
                    Application.Current.Dispatcher.Invoke(
                        DispatcherPriority.ApplicationIdle,
                        new DelegateCallbackTask(UICallback),
                        oResult
                        );
                }
            }
        }

        /// <summary>
        /// Internal function for task abort Transfer
        /// </summary>
        private bool ProcessAbortTransfer(WorkerThread.TransferParams oParams)
        {
            LogFile.Log("ProcessAbortTransfer[0x{0:X2}]...", oParams.bPipe);

            if (oParams.IsWritePipe())
            {
                if (objectWriter[oParams.bChannelIndex] != null)
                {
                    objectWriter[oParams.bChannelIndex].Stop();
                    objectWriter[oParams.bChannelIndex] = null;
                }
            }
            else
            {
                if (objectReader[oParams.bChannelIndex] != null)
                {
                    objectReader[oParams.bChannelIndex].Stop();
                    objectReader[oParams.bChannelIndex] = null;

                }
            }

            return true;
        }

        /// <summary>
        /// Internal function for task test mode
        /// </summary>
        private bool ProcessTestMode(WorkerThread.TransferParams oParams, byte bOpenBy, string OpenByString)
        {
            FTDI.FT_STATUS ftStatus = FTDI.FT_STATUS.FT_OTHER_ERROR;
            byte bStartIndex = 0;
            byte bChannelConf = 0;
            DelegateCallbackTextBoxDebug Debug = oParams.fxnCallbackDebug;


            LogFile.Log("ProcessTestMode...");

            oParams.bTestMode = true;
            oParams.bAsync = false;
            oParams.fxnCallbackDebug = null;
            oParams.fxnCallbackCompleted = ProcessTransferCompletion;

            // Save the current configuration
            var origConf = new FTDI.FT_60XCONFIGURATION();
            ftStatus = d3xxDevice.GetChipConfiguration(origConf);
            if (ftStatus != FTDI.FT_STATUS.FT_OK)
            {
                d3xxDevice.Close();
                return false;
            }
            bChannelConf = origConf.ChannelConfig;

            // Prepare to handle 245 mode
            if (origConf.FIFOMode == (byte)FTDI.FT_60XCONFIGURATION_FIFO_MODE.MODE_245)
            {
                bStartIndex = (byte)FTDI.FT_60XCONFIGURATION_CHANNEL_CONFIG.ONE;
            }

            LogInfo(Debug, "Performance for all channel configurations will be measured.\n");
            LogInfo(Debug, String.Format("FIFO Mode: {0}\n", 
                ((FTDI.FT_60XCONFIGURATION_FIFO_MODE)origConf.FIFOMode).ToString()));

            // Execute for all channel configuration
            for (byte i = bStartIndex; i < (byte)FTDI.FT_60XCONFIGURATION_CHANNEL_CONFIG.COUNT; i++)
            {
                // Set configuration
                origConf.ChannelConfig = i;
                d3xxDevice.SetChipConfiguration(origConf);
                d3xxDevice.Close();
                Thread.Sleep(1000);

                // Open device
                switch ((Controls.OpenBy.Type)bOpenBy)
                {
                    case Controls.OpenBy.Type.Description:
                        {
                            ftStatus = d3xxDevice.OpenByDescription(OpenByString);
                            break;
                        }
                    case Controls.OpenBy.Type.SerialNumber:
                        {
                            ftStatus = d3xxDevice.OpenBySerialNumber(OpenByString);
                            break;
                        }
                    case Controls.OpenBy.Type.Index: // fall-through
                    default:
                        {
                            ftStatus = d3xxDevice.OpenByIndex(UInt32.Parse(OpenByString));
                            break;
                        }
                }
                if (ftStatus != FTDI.FT_STATUS.FT_OK)
                {
                    break;
                }

                // Get configuration
                var conf = new FTDI.FT_60XCONFIGURATION();
                ftStatus = d3xxDevice.GetChipConfiguration(conf);
                if (ftStatus != FTDI.FT_STATUS.FT_OK)
                {
                    break;
                }

                ulWriteMBps = 0;
                ulReadMBps = 0;

                // Execute a write pipe
                foreach (var Pipe in d3xxDevice.DataPipeInformation)
                {
                    oParams.bPipe = Pipe.PipeId;
                    if (Pipe.PipeId < 0x80)
                    {
                        objectWriter[oParams.bChannelIndex] = new WriterThread(d3xxDevice, ref oParams);
                        threadWriter[oParams.bChannelIndex] = new Thread(new ThreadStart(objectWriter[oParams.bChannelIndex].Run));
                        threadWriter[oParams.bChannelIndex].Start();
                        threadWriter[oParams.bChannelIndex].Join(1500);
                        while (objectWriter[oParams.bChannelIndex].IsRunning() && Application.Current != null);
                        Thread.Sleep(100);
                        break;
                    }
                }

                // Execute a read pipe
                foreach (var Pipe in d3xxDevice.DataPipeInformation)
                {
                    oParams.bPipe = Pipe.PipeId;
                    if (Pipe.PipeId > 0x80)
                    {
                        objectReader[oParams.bChannelIndex] = new ReaderThread(d3xxDevice, ref oParams);
                        threadReader[oParams.bChannelIndex] = new Thread(new ThreadStart(objectReader[oParams.bChannelIndex].Run));
                        threadReader[oParams.bChannelIndex].Start();
                        threadWriter[oParams.bChannelIndex].Join(1500);
                        while (objectReader[oParams.bChannelIndex].IsRunning() && Application.Current != null) ;
                        Thread.Sleep(100);
                        break;
                    }
                }

                LogInfo(Debug, String.Format("Channel: {0}\tOUT: {1} MBps, IN: {2} MBps\n",
                    ((FTDI.FT_60XCONFIGURATION_CHANNEL_CONFIG)conf.ChannelConfig).ToString(),
                    ulWriteMBps, ulReadMBps));
            }

            origConf.ChannelConfig = bChannelConf;
            if (d3xxDevice.IsOpen)
            {
                d3xxDevice.SetChipConfiguration(origConf);
                d3xxDevice.Close();
                Thread.Sleep(1000);
            }

            LogFile.Log("ProcessTestMode done...");
            return (ftStatus == FTDI.FT_STATUS.FT_OK);
        }

        /// <summary>
        /// Internal function for retreiving the number of pipes
        /// </summary>
        private void GetNumPipes(ref byte bNumWritePipes, ref byte bNumReadPipes)
        {
            if (!d3xxDevice.IsOpen)
            {
                return;
            }

            foreach (var Pipe in d3xxDevice.DataPipeInformation)
            {
                if (Pipe.PipeId < 0x80)
                {
                    bNumWritePipes++;
                }
                else
                {
                    bNumReadPipes++;
                }
            }
        }

        /// <summary>
        /// Internal function for updating of the textbox debugging in UI
        /// </summary>
        private void LogInfo(DelegateCallbackTextBoxDebug fxnCallback, string strLog)
        {
            if (Application.Current != null)
            {
                Application.Current.Dispatcher.Invoke(
                    DispatcherPriority.Background,
                    new DelegateCallbackTextBoxDebug(fxnCallback),
                    strLog
                    );
            }
        }

        /// <summary>
        /// Internal function for updating of UI
        /// </summary>
        private void CallUICallback(Task task, bool bResult)
        {
            task.SetResult(bResult);

            if (Application.Current != null)
            {
                Application.Current.Dispatcher.Invoke(
                    DispatcherPriority.Background,
                    new DelegateCallbackTask(UICallback),
                    task.Result
                    );
            }
        }

        /// <summary>
        /// Set the UI callback
        /// </summary>
        public void SetUICallback(DelegateCallbackTask fxnCallback)
        {
            UICallback = fxnCallback;
        }

        /// <summary>
        /// Check if the path is the same for hotplugging
        /// </summary>
        public bool IsDevicePath(string szDevicePath)
        {
            if (!szDevicePath.Contains(d3xxDevice.SerialNumber))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Check if the path is the same for hotplugging
        /// Used for HotPlug2
        /// </summary>
        public bool IsDevicePathEx(string szDevicePath)
        {
            if (!d3xxDevice.IsOpen)
            {
                return false;
            }

            var ftStatus = d3xxDevice.IsDevicePath(szDevicePath);
            if (ftStatus != FTDI.FT_STATUS.FT_OK)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Get D3xx Guid
        /// </summary>
        public static Guid GetGuid()
        {
            return FTDI.FT_GUID;
        }

        private const UInt32 ulMaxChannels = 1;
        private WriterThread[] objectWriter;
        private Thread[] threadWriter;
        private ReaderThread[] objectReader;
        private Thread[] threadReader;
        private Object queueLock = new Object();
        private Queue<Task> queueTasks = new Queue<Task>(16);
        private FTDI d3xxDevice = new FTDI();
        private DelegateCallbackTask UICallback;
        private UInt32 ulReadMBps;
        private UInt32 ulWriteMBps;
        public AutoResetEvent TaskQueueEvent;

    }
}
