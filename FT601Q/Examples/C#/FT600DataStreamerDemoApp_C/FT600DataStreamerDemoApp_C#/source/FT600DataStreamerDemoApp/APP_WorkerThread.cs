/*
** APP_WorkerThread.cs
**
** Copyright © 2016 Future Technology Devices International Limited
**
** C# Source file for Demo Application.
**
** Author: FTDI
** Project: C# Data Streamer Demo Application
** Module: Worker thread implementation
**
** History:
**  1.0.0.0	- Initial version
**
*/

using System;
using System.Windows;
using System.Windows.Threading;
using System.Threading;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using DemoUtility;
using FTD3XX_NET;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace DemoApp
{
#region Worker Thread Class
    /// <summary>
    /// Worker thread class implementation
    /// </summary>
    public abstract class WorkerThread
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public WorkerThread(
            FTDI _d3xxDevice,
            ref TransferParams _oParams
            )
        {
            d3xxDevice = _d3xxDevice;
            oParams.bChannelIndex = _oParams.bChannelIndex;
            oParams.bPipe = _oParams.bPipe;
            oParams.bAsync = _oParams.bAsync;
            oParams.bStress = _oParams.bStress;
            oParams.ulPacketSize = _oParams.ulPacketSize;
            oParams.ulQueueSize = _oParams.ulQueueSize;
            oParams.fxnCallbackCompleted = _oParams.fxnCallbackCompleted;
            oParams.fxnCallbackDebug = _oParams.fxnCallbackDebug;
            oParams.fxnCallbackRate = _oParams.fxnCallbackRate;
            oParams.fileDirectory = _oParams.fileDirectory;
            oParams.fileName = _oParams.fileName;
            oParams.bTestMode = _oParams.bTestMode;
            oParams.ulDataPattern = _oParams.ulDataPattern;
            oParams.ulDataPatternType = _oParams.ulDataPatternType;
        }

        /// <summary>
        /// Thread entry function
        /// </summary>
        public void Run()
        {
            FTDI.FT_STATUS ftStatus = FTDI.FT_STATUS.FT_OTHER_ERROR;
            UInt32 ulTotalBytesTransferred = 0;
            UInt32 ulBytesTransferred = 0;
            DateTime timeStart;
            DateTime timeEnd;
            bool bResult = true;
            threadCompleted = new ManualResetEvent(false);
            UInt32 ulRate = 0;
            UInt32 ulLastCharUsed = 0;

            LogFile.Log("Worker[0x{0:X2}] begin...[{1}][{2}]", oParams.bPipe, oParams.ulPacketSize, oParams.ulQueueSize);
            bThreadRunning = true;

#region Synchronous Transfer
            if (!oParams.bAsync)
            {
                // synchronous transfer

                byte[] bBuffer = new byte[oParams.ulPacketSize];
                UInt32[] ulBuffer = new UInt32[oParams.ulPacketSize / sizeof (UInt32)];

                if(oParams.ulDataPatternType == (UInt32) Controls.PatternType.PatternRandom)
                {
                    GenerateRandomData(ref ulBuffer, oParams.ulPacketSize);
                    Buffer.BlockCopy(ulBuffer, 0, bBuffer, 0, bBuffer.Length);
                }

                if (oParams.ulDataPatternType == (UInt32)Controls.PatternType.PatternFixed)
                {
                    GenerateFixedData(ref ulBuffer, oParams.ulPacketSize, oParams.ulDataPattern);
                    Buffer.BlockCopy(ulBuffer, 0, bBuffer, 0, bBuffer.Length);
                }

                timeStart = DateTime.Now;
                ulTotalBytesTransferred = 0;


                while (!IsStopped() && Application.Current != null)
                {


                    for (UInt32 i = 0; i < oParams.ulQueueSize; i++)
                    {
                        ulBytesTransferred = 0;
                        if (oParams.ulDataPatternType == (UInt32)Controls.PatternType.PatternIncremental)
                        {
                            GenerateIncrementalData(ref ulBuffer, oParams.ulPacketSize, ref ulLastCharUsed);
                            Buffer.BlockCopy(ulBuffer, 0, bBuffer, 0, bBuffer.Length);
                        }

                        ftStatus = TransferSync(oParams.bPipe, bBuffer, oParams.ulPacketSize, ref ulBytesTransferred);
                        if (ftStatus != FTDI.FT_STATUS.FT_OK)
                        {
                            AbortTransfer(oParams.bPipe);
                            bResult = false;
                            break;
                        }

                        ulTotalBytesTransferred += ulBytesTransferred;

                        if (ulBytesTransferred != oParams.ulPacketSize)
                        {
                            bResult = false;
                            break;
                        }

                        if (IsStopped())
                        {
                            break;
                        }
                    }

                    timeEnd = DateTime.Now;

                    if (!bResult)
                    {
                        break;
                    }

                    TimeSpan timediff = timeEnd - timeStart;
                    if (timediff.Seconds >= 1)
                    {
                        timeStart = DateTime.Now;
                        ulRate = LogStatistics(timediff, ulTotalBytesTransferred);

                        ulTotalBytesTransferred = 0;

                    }

                    if (oParams.bTestMode)
                    {
                        break;
                    }
                }
            }
#endregion

#region Asynchronous Transfer
            else
            {
                // asynchronous transfer

                NativeOverlapped[] listOverlapped = new NativeOverlapped[oParams.ulQueueSize];
                List<byte[]> listBuffer = new List<byte[]>();
                Int32 i = 0;


                for (i = 0; i < oParams.ulQueueSize; i++)
                {
                    byte[] bBuffer = new byte[oParams.ulPacketSize];
                    listBuffer.Add(bBuffer);
                }
                d3xxDevice.SetStreamPipe(oParams.bPipe, oParams.ulPacketSize);

                // Queue up the initial batch of requests
                timeStart = DateTime.Now;
                for (i = 0; i < oParams.ulQueueSize; i++)
                {
                    ftStatus = TransferAsync(oParams.bPipe, listBuffer[i], oParams.ulPacketSize, ref ulBytesTransferred, ref listOverlapped[i]);
                    if (ftStatus != FTDI.FT_STATUS.FT_IO_PENDING)
                    {
                        AbortTransfer(oParams.bPipe);
                        d3xxDevice.ClearStreamPipe(oParams.bPipe);
                        threadCompleted.Set();
                        bThreadRunning = false;
                        LogToUI(bResult);
                        LogFile.Log("Worker[0x{0:X2}] end...\r\n", oParams.bPipe);
                        return;
                    }
                }
                i = 0;

                // Infinite transfer loop
                while (Application.Current != null)
                {
                    if (IsStopped()) { AbortTransfer(oParams.bPipe); break; }

                    ulBytesTransferred = 0;

                    // Wait for transfer to finish
                    ftStatus = d3xxDevice.WaitAsync(ref listOverlapped[i], ref ulBytesTransferred, true);
                    if (ftStatus != FTDI.FT_STATUS.FT_OK)
                    {
                        AbortTransfer(oParams.bPipe);
                        break;
                    }

                    if (IsStopped()) { AbortTransfer(oParams.bPipe); break; }

                    ulTotalBytesTransferred += ulBytesTransferred;

                    // Roll-over
                    if (++i == oParams.ulQueueSize)
                    {
                        timeEnd = DateTime.Now;
                        TimeSpan timediff = timeEnd - timeStart;
                        if (timediff.Seconds >= 1)
                        {
                            timeStart = DateTime.Now;
                            ulRate = LogStatistics(timediff, ulTotalBytesTransferred);

                            ulTotalBytesTransferred = 0;

                        }
                        if (oParams.bTestMode) { break; }

                        i = 0;
                    }
                    else if (oParams.bTestMode) 
                    { 
                        continue; 
                    }

                    if (IsStopped()) { AbortTransfer(oParams.bPipe); break; }

                    // Resubmit to keep requests full
                    Int32 j = (i == 0 ? (Int32)oParams.ulQueueSize - 1 : i - 1);
                    ftStatus = TransferAsync(oParams.bPipe, listBuffer[j], oParams.ulPacketSize, ref ulBytesTransferred, ref listOverlapped[j]);
                    if (ftStatus != FTDI.FT_STATUS.FT_IO_PENDING)
                    {
                        AbortTransfer(oParams.bPipe);
                        break;
                    }

                    if (IsStopped()) { AbortTransfer(oParams.bPipe); break; }
                }

                AbortTransfer(oParams.bPipe);
                d3xxDevice.ClearStreamPipe(oParams.bPipe);
            }
#endregion

            threadCompleted.Set();
            bThreadRunning = false;

            LogToUI(bResult, ulRate);

            LogFile.Log("Worker[0x{0:X2}] begin...[{1}][{2}]", oParams.bPipe, oParams.ulPacketSize, oParams.ulQueueSize);
        }

        /// <summary>
        /// Async entry function
        /// </summary>
        public void AsyncTask()
        {
            FTDI.FT_STATUS ftStatus = FTDI.FT_STATUS.FT_OTHER_ERROR;
            List<byte[]> ioBuffer = new List<byte[]>();
            ManualResetEvent[] completionEvent = new ManualResetEvent[oParams.ulQueueSize];
            NativeOverlapped[] nol = new NativeOverlapped[oParams.ulQueueSize];
            GCHandle[] handle = new GCHandle[oParams.ulQueueSize];
            GCHandle[] ov_handle   = new GCHandle[oParams.ulQueueSize];
            UInt32 ulTotalBytesTransferred = 0;
            UInt32 ulBytesTransferred = 0;
            DateTime timeStart;
            DateTime timeEnd;
            UInt32 ulRate = 0;
            bool bResult = true;
            Int32 i = 0;

            if(threadCompleted != null) 
            {
                LogFile.Log("AsyncTask[0x{0:X2}] one instance already running...[{1}][{2}]", oParams.bPipe, oParams.ulPacketSize, oParams.ulQueueSize);
                return; 
            }
            threadCompleted = new ManualResetEvent(false);
            bThreadRunning = true;

            var info = d3xxDevice.DataPipeInformation;


            byte[] bBufferCopy = new byte[oParams.ulPacketSize];

            d3xxDevice.SetPipeTimeout(oParams.bPipe, 0);
            var res = d3xxDevice.SetStreamPipe(oParams.bPipe, oParams.ulPacketSize);
            if (res != FTDI.FT_STATUS.FT_OK)
            {

            }
            LogFile.Log("AsyncTask[0x{0:X2}] begin...[{1}][{2}]", oParams.bPipe, oParams.ulPacketSize, oParams.ulQueueSize);


            try
            {
                for (i = 0; i < oParams.ulQueueSize; i++)
                {
                    byte[] bBuffer = new byte[oParams.ulPacketSize];
                    ioBuffer.Add(bBuffer);
                    completionEvent[i] = new ManualResetEvent(false);
                    nol[i] = new NativeOverlapped();
                    nol[i].EventHandle = completionEvent[i].SafeWaitHandle.DangerousGetHandle();
                }

                timeStart = DateTime.Now;
                // Queue up the initial batch of requests
                for (i = 0; i < oParams.ulQueueSize; i++)
                {
                    completionEvent[i].Reset();
                    nol[i].InternalHigh = IntPtr.Zero;
                    nol[i].InternalLow = IntPtr.Zero;
                    nol[i].OffsetHigh = 0;
                    nol[i].OffsetLow = 0;

                    handle[i] = GCHandle.Alloc(ioBuffer[i], GCHandleType.Pinned);
                    ov_handle[i] = GCHandle.Alloc(nol[i], GCHandleType.Pinned);

                    ftStatus = TransferAsync(oParams.bPipe, handle[i].AddrOfPinnedObject(), oParams.ulPacketSize, ref ulBytesTransferred, ov_handle[i].AddrOfPinnedObject());
                    if (ftStatus != FTDI.FT_STATUS.FT_IO_PENDING)
                    {
                        AbortTransfer(oParams.bPipe);
                        d3xxDevice.ClearStreamPipe(oParams.bPipe);
                        //LogToUI(bResult);
                        //LogFile.Log("AsyncTask[0x{0:X2}] end...\r\n", oParams.bPipe);
                        handle[i].Free();
                        ov_handle[i].Free();

                        threadCompleted.Set();
                        bThreadRunning = false;
                        return;
                    }
                }

                i = 0;
                while (!IsStopped() && Application.Current != null)
                {
                    ftStatus = d3xxDevice.WaitAsync(ov_handle[i].AddrOfPinnedObject(), ref ulBytesTransferred, true);
                    if (ftStatus != FTDI.FT_STATUS.FT_OK)
                    {
                        if(IsStopped())
                        {
                            break;
                        }
                        AbortTransfer(oParams.bPipe);
                        d3xxDevice.ClearStreamPipe(oParams.bPipe);
                        LogToUI(bResult);
                        break;
                    }

                    ulTotalBytesTransferred += ulBytesTransferred;

                    // performing an operation with the received data
                    //for (int k = 0; k < ioBuffer[i].Length; k++)
                    //{
                    //    bBufferCopy[k] = ioBuffer[i][k];
                    //}
                    // access pinned buffer only.
                    Marshal.Copy(handle[i].AddrOfPinnedObject(), bBufferCopy, 0, bBufferCopy.Length);

                    completionEvent[i].Reset();
                    nol[i].InternalHigh = IntPtr.Zero;
                    nol[i].InternalLow = IntPtr.Zero;
                    nol[i].OffsetHigh = 0;
                    nol[i].OffsetLow = 0;

                    ftStatus = TransferAsync(oParams.bPipe, handle[i].AddrOfPinnedObject(), oParams.ulPacketSize, ref ulBytesTransferred, ov_handle[i].AddrOfPinnedObject());
                    if (ftStatus != FTDI.FT_STATUS.FT_IO_PENDING)
                    {
                        if (IsStopped())
                        {
                            break;
                        }

                        AbortTransfer(oParams.bPipe);
                        d3xxDevice.ClearStreamPipe(oParams.bPipe);
                        LogToUI(bResult);
                        break;

                    }

                    if (++i == oParams.ulQueueSize)
                    {
                        timeEnd = DateTime.Now;
                        TimeSpan timediff = timeEnd - timeStart;
                        if (timediff.Seconds >= 1)
                        {
                            ulRate = LogStatistics(timediff, ulTotalBytesTransferred);

                            ulTotalBytesTransferred = 0;
                            timeStart = DateTime.Now;

                        }
                        i = 0;
                    }
                }

                if(IsStopped())
                { /* should not exit the thread before all requests returns */
                    for (i = 0; i < oParams.ulQueueSize; i++)
                    {
                        ftStatus = d3xxDevice.WaitAsync(ov_handle[i].AddrOfPinnedObject(), ref ulBytesTransferred, true);
                    }
                }
                if (!IsStopped())
                {
                    res = AbortTransfer(oParams.bPipe);
                    if (res != FTDI.FT_STATUS.FT_OK)
                    {
                        System.Diagnostics.Debug.WriteLine($"AsyncTask[0x{0:X2}] Abort error {res.ToString()} ...\r\n", oParams.bPipe);
                    }
                    /* should not exit the thread before all requests returns */
                    for (i = 0; i < oParams.ulQueueSize; i++)
                    {
                        ftStatus = d3xxDevice.WaitAsync(ov_handle[i].AddrOfPinnedObject(), ref ulBytesTransferred, true);
                    }

                }
                
                res = d3xxDevice.ClearStreamPipe(oParams.bPipe);
                if (res != FTDI.FT_STATUS.FT_OK)
                {
                    System.Diagnostics.Debug.WriteLine($"AsyncTask[0x{0:X2}] Clear Stream error {res.ToString()} ...\r\n", oParams.bPipe);
                }

                for (i = 0; i < oParams.ulQueueSize; ++i)
                {
                    if (handle[i].IsAllocated)
                    {
                        handle[i].Free();
                        ov_handle[i].Free();
                    }
                }


            }
            catch (ThreadAbortException)
            {
            }
            LogToUI(bResult, ulRate);

            threadCompleted.Set();
            bThreadRunning = false;

            LogFile.Log("AsyncTask[0x{0:X2}] end...[{1}][{2}]", oParams.bPipe, oParams.ulPacketSize, oParams.ulQueueSize);
        }


        /// <summary>
        /// Stop the thread
        /// </summary>
        public void Stop()
        {
            UInt16 Counter = 0;
            LogFile.Log("Worker[0x{0:X2}] stop ...", oParams.bPipe);

            SetStop();
            Thread.Sleep(1000);

            while(bThreadRunning)
            {
                if (Counter >= 5)
                    break;

                Thread.Sleep(1000);
                Counter++;
            }
            if (bThreadRunning)
            {
                AbortTransfer(oParams.bPipe);

                if (threadCompleted != null)
                {
                    threadCompleted.WaitOne();

                }
            }
            

            LogFile.Log("Worker[0x{0:X2}] stop done...", oParams.bPipe);
        }

        /// <summary>
        /// Check if the thread is still running
        /// </summary>
        public bool IsRunning()
        {
            return bThreadRunning;
        }


        //// <summary>
        /// Aborts the submitted transfers on a given pipe.
        /// </summary>
        protected abstract
            FTDI.FT_STATUS AbortTransfer(byte bPipe);


        /// <summary>
        /// Abstract function to be override by Writer and Reader threads
        /// </summary>
        protected abstract 
            FTDI.FT_STATUS TransferSync
            (byte bPipe, byte[] bBuffer, UInt32 ulPacketSize, ref UInt32 ulBytesTransferred);

        /// <summary>
        /// Abstract function to be override by Writer and Reader threads
        /// </summary>
        protected abstract
            FTDI.FT_STATUS TransferAsync
            (byte bPipe, IntPtr bBuffer, UInt32 ulPacketSize, ref UInt32 ulBytesTransferred, IntPtr oOverlapped);

        /// <summary>
        /// Abstract function to be override by Writer and Reader threads
        /// </summary>
        protected abstract
            FTDI.FT_STATUS TransferAsync
            (byte bPipe, byte[] bBuffer, UInt32 ulPacketSize, ref UInt32 ulBytesTransferred, ref NativeOverlapped oOverlapped);

        /// <summary>
        /// Internal function to generate data pattern
        /// </summary>
        /// 
        private void GenerateIncrementalData(ref UInt32[] bBuffer, UInt32 ulBytesToCopy,ref UInt32 ulLastCharUsed)
        {
           // UInt32* pulBuffer = (UInt32*)bBuffer;
            for(UInt32 i = 0; i < ulBytesToCopy / sizeof(UInt32); i++)
            {
                bBuffer[i] = ulLastCharUsed++;
            }

        }

        private void GenerateFixedData(ref UInt32[] bBuffer, UInt32 ulBytesToCopy, UInt32 ulFixedData)
        {
            // UInt32* pulBuffer = (UInt32*)bBuffer;
            for (UInt32 i = 0; i < ulBytesToCopy / sizeof(UInt32); i++)
            {
                bBuffer[i] = ulFixedData;
            }

        }

        private void GenerateRandomData(ref UInt32[] bBuffer, UInt32 ulBytesToCopy)
        {
            Random random = new Random();
            // UInt32* pulBuffer = (UInt32*)bBuffer;
            for (UInt32 i = 0; i < ulBytesToCopy / sizeof(UInt32); i++)
            {
                bBuffer[i] = (UInt32)random.Next();
            }

        }

        /// <summary>
        /// Internal function to signal stopping of thread
        /// </summary>
        private void SetStop()
        {
            lock (lockStop)
            {
                bStop = true;
            }
            LogFile.Log("SetStop[0x{0:X2}] done...bStop:{1}", oParams.bPipe, bStop);
        }

        /// <summary>
        /// Internal function to signal stopping of thread
        /// </summary>
        private bool IsStopped()
        {
            lock (lockStop)
            {
                if(bStop)
                    LogFile.Log("IsStopped[0x{0:X2}] stop done...", oParams.bPipe);

                return bStop;
            }
        }

        /// <summary>
        /// Internal function to call back the thread manager
        /// </summary>
        private async void LogToUI(bool bResult, UInt32 ulRate = 0)
        {
            if (oParams.fxnCallbackCompleted != null)
            {
                Task task = new Task(oParams.bPipe, oParams.bChannelIndex);
                LogFile.Log("LogToUI result: {0} \r\n", task.Type);

                if (oParams.bTestMode)
                {
                    task.SetResult(bResult, oParams.bPipe, ulRate);
                }
                else
                {
                    task.SetResult(bResult);
                }

                if (Application.Current != null)
                {

                    await Application.Current.Dispatcher.BeginInvoke(
                                    DispatcherPriority.Normal,
                                    new DelegateCallbackTask(oParams.fxnCallbackCompleted),
                                    task.Result
                                    );
                    LogFile.Log("LogToUI done\r\n");


                }
                return;
            }
            LogFile.Log("LogToUI Failed\r\n");
        }

        /// <summary>
        /// Internal function to update the UI with the result
        /// </summary>
        private void LogRate(UInt32 ulRate)
        {
            if (oParams.fxnCallbackRate != null)
            {
                if (Application.Current != null)
                {
                    Application.Current.Dispatcher.BeginInvoke(
                        DispatcherPriority.Background,
                        new DelegateCallbackTextBoxRate(oParams.fxnCallbackRate),
                        oParams.bPipe,
                        ulRate
                        );
                }
            }
        }

        /// <summary>
        /// Internal function to update the UI with the result
        /// </summary>
        private UInt32 LogStatistics(TimeSpan timeDiff, UInt32 ulTotalBytesTransferred)
        {
            UInt32 ulDurationInMs = 0;
            UInt32 ulRate = 0;

            ulDurationInMs = (UInt32)timeDiff.TotalMilliseconds;
            ulRate = (UInt32)((ulTotalBytesTransferred / (double)ulDurationInMs) * 0.001);
            LogProgress(ulTotalBytesTransferred, ulDurationInMs, ulRate);
            LogRate(ulRate);

            return ulRate;
        }
		
        /// <summary>
        /// Internal function to update the UI with the result
        /// </summary>
        private async void LogProgress(UInt32 ulBytesTransferred, UInt32 ulDurationInMs, UInt32 ulRate)
        {
            if (oParams.fxnCallbackDebug != null)
            {
                if (oParams.bPipe == 0x82)
                {
                    string szLog = String.Format("Read[0x{3:x}] {0} bytes in {1} ms or {2} MBps\n",
                   ulBytesTransferred, ulDurationInMs, ulRate, oParams.bPipe);

                    if (Application.Current != null)
                    {
                        await Application.Current.Dispatcher.BeginInvoke(
                            DispatcherPriority.Background,
                            new DelegateCallbackTextBoxDebug(oParams.fxnCallbackDebug),
                            szLog
                            );
                    }
                }
                else //if (oParams.bPipe == 0x82)
                {
                    string szLog = String.Format("Wrote[0x{3:x}] {0} bytes in {1} ms or {2} MBps\n",
                   ulBytesTransferred, ulDurationInMs, ulRate, oParams.bPipe);

                    if (Application.Current != null)
                    {
                        await Application.Current.Dispatcher.BeginInvoke(
                            DispatcherPriority.Background,
                            new DelegateCallbackTextBoxDebug(oParams.fxnCallbackDebug),
                            szLog
                            );
                    }

                }
            }
        }

        /// <summary>
        /// Transfer param structure submitted by task manager thread
        /// </summary>
        public struct TransferParams
        {
            public byte bChannelIndex;
            public byte bPipe;
            public bool bAsync;
            public bool bStress;
            public bool bTestMode;
            public UInt32 ulPacketSize;
            public UInt32 ulQueueSize;
            public UInt32 ulDataPatternType;
            public UInt32 ulDataPattern;
            public DelegateCallbackTask fxnCallbackCompleted;
            public DelegateCallbackTextBoxDebug fxnCallbackDebug;
            public DelegateCallbackTextBoxRate fxnCallbackRate;
            public string fileDirectory;
            public string fileName;

            public bool IsWritePipe()
            {
                if (bPipe < 0x80)
                {
                    return true;
                }
                return false;
            }
        }

        private TransferParams oParams;
        protected FTDI d3xxDevice;
        private bool bStop = false;
        private static readonly Object lockStop = new Object();
        private ManualResetEvent threadCompleted;
        private bool bThreadRunning = false;

    }
#endregion


    /// <summary>
    /// Writer thread class implementation
    /// </summary>
    public class WriterThread : WorkerThread
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public WriterThread(FTDI _d3xxDevice, ref TransferParams _oParams) 
            : base(_d3xxDevice, ref _oParams)
        {
        }


        //// <summary>
        /// Aborts the submitted transfer on a given pipe.
        /// </summary>
        protected override 
            FTDI.FT_STATUS AbortTransfer(byte bPipe)
        {
            FTDI.FT_STATUS status;
            UInt32 ulGPIOMask, ulGPIOData;
            ulGPIOMask = 2;
            ulGPIOData = 2;

            d3xxDevice.WriteGPIO(ulGPIOMask, ulGPIOData);
            status = d3xxDevice.AbortPipe(bPipe);
            d3xxDevice.WriteGPIO(ulGPIOMask, 0);

            return status;


        }


        /// <summary>
        /// Override function for the abstract function in the base class
        /// </summary>
        protected override 
            FTDI.FT_STATUS TransferSync
            (byte bPipe, byte[] bBuffer, UInt32 ulPacketSize, ref UInt32 ulBytesTransferred)
        {
            return d3xxDevice.WritePipe(bPipe, bBuffer, ulPacketSize, ref ulBytesTransferred);
        }

        /// <summary>
        /// Override function for the abstract function in the base class
        /// </summary>
        protected override
            FTDI.FT_STATUS TransferAsync
            (byte bPipe, IntPtr bBuffer, UInt32 ulPacketSize, ref UInt32 ulBytesTransferred, IntPtr oOverlapped)
        {
            return d3xxDevice.WritePipeEx(bPipe, bBuffer, ulPacketSize, ref ulBytesTransferred, oOverlapped);
        }
        /// <summary>
         /// Override function for the abstract function in the base class
         /// </summary>
        protected override
            FTDI.FT_STATUS TransferAsync
            (byte bPipe, byte[] bBuffer, UInt32 ulPacketSize, ref UInt32 ulBytesTransferred, ref NativeOverlapped oOverlapped)
        {
            return d3xxDevice.WritePipeEx(bPipe, bBuffer, ulPacketSize, ref ulBytesTransferred, ref oOverlapped);
        }
    }


    /// <summary>
    /// Reader thread class implementation
    /// </summary>
    public class ReaderThread : WorkerThread
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public ReaderThread(FTDI _d3xxDevice, ref TransferParams _oParams)
            : base(_d3xxDevice, ref _oParams)
        {
        }


        //// <summary>
        /// Aborts the submitted transfer on a given pipe.
        /// </summary>
        protected override
            FTDI.FT_STATUS AbortTransfer(byte bPipe)
        {
            FTDI.FT_STATUS status = FTDI.FT_STATUS.FT_OK;
            UInt32 ulGPIOMask, ulGPIOData;
            ulGPIOMask = 1;
            ulGPIOData = 1;

            d3xxDevice.WriteGPIO(ulGPIOMask, ulGPIOData);
            Thread.Sleep(1000);
            status = d3xxDevice.AbortPipe(bPipe);
            d3xxDevice.WriteGPIO(ulGPIOMask, 0);

            return status;


        }

        /// <summary>
        /// Override function for the abstract function in the base class
        /// </summary>
        protected override 
            FTDI.FT_STATUS TransferSync
            (byte bPipe, byte[] bBuffer, UInt32 ulPacketSize, ref UInt32 ulBytesTransferred)
        {
            return d3xxDevice.ReadPipe(bPipe, bBuffer, ulPacketSize, ref ulBytesTransferred);
        }
        /// <summary>
        /// Override function for the abstract function in the base class
        /// </summary>
        protected override
            FTDI.FT_STATUS TransferAsync
            (byte bPipe, IntPtr bBuffer, UInt32 ulPacketSize, ref UInt32 ulBytesTransferred, IntPtr oOverlapped)
        {
            return d3xxDevice.ReadPipeEx(bPipe, bBuffer, ulPacketSize, ref ulBytesTransferred, oOverlapped);
        }

        /// <summary>
        /// Override function for the abstract function in the base class
        /// </summary>
        protected override
            FTDI.FT_STATUS TransferAsync
            (byte bPipe, byte[] bBuffer, UInt32 ulPacketSize, ref UInt32 ulBytesTransferred, ref NativeOverlapped oOverlapped)
        {
            return d3xxDevice.ReadPipeEx(bPipe, bBuffer, ulPacketSize, ref ulBytesTransferred, ref oOverlapped);
        }
    }
}
