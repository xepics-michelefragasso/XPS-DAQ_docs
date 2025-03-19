/*
** APP_Task.cs
**
** Copyright © 2016 Future Technology Devices International Limited
**
** C# Source file for Demo Application.
**
** Author: FTDI
** Project: C# Demo Application
** Module: Task implementation
**
** History:
**  1.0.0.0	- Initial version
**
*/

using System;



namespace DemoApp
{
    public class Task
    {
        public Task(
            DelegateCallbackTextBoxDebug fxnCallbackDebug, 
            byte bOpenBy, 
            string OpenByString
            )
        {
            oParam.eType = TaskType.Open;
            oParam.OpenByString = OpenByString;
            oParam.bOpenBy = bOpenBy;
            oParam.oTransferParams.fxnCallbackDebug = fxnCallbackDebug;
        }

        public Task(
            DelegateCallbackTextBoxDebug fxnCallbackDebug,
            DelegateCallbackTextBoxRate fxnCallbackRate,
            byte bPipe,
            UInt32 ulPacketSize,
            byte bChannelIndex,
            UInt32 ulQueueSize,
            bool bAsync,
            UInt32 ulPatternType,
            UInt32 ulDataPattern
            )
        {
            oParam.eType = TaskType.Start;
            oParam.oTransferParams.bChannelIndex = bChannelIndex;
            oParam.oTransferParams.bPipe = bPipe;
            oParam.oTransferParams.ulPacketSize = ulPacketSize;
            oParam.oTransferParams.fxnCallbackDebug = fxnCallbackDebug;
            oParam.oTransferParams.fxnCallbackRate = fxnCallbackRate;
            oParam.oTransferParams.bAsync = bAsync;
            oParam.oTransferParams.bStress = false;
            oParam.oTransferParams.ulQueueSize = ulQueueSize;
            oParam.oTransferParams.ulDataPattern = ulDataPattern;
            oParam.oTransferParams.ulDataPatternType = ulPatternType;
        }
		
        public Task(
            DelegateCallbackTextBoxDebug fxnCallbackDebug,
            DelegateCallbackTextBoxRate fxnCallbackRate,
            byte bPipe,
            UInt32 ulPacketSize,
            byte bChannelIndex,
            bool bStress
            )
        {
            oParam.eType = TaskType.Start;
            oParam.oTransferParams.bChannelIndex = bChannelIndex;
            oParam.oTransferParams.bPipe = bPipe;
            oParam.oTransferParams.ulPacketSize = ulPacketSize;
            oParam.oTransferParams.fxnCallbackDebug = fxnCallbackDebug;
            oParam.oTransferParams.fxnCallbackRate = fxnCallbackRate;
            oParam.oTransferParams.bAsync = false;
            oParam.oTransferParams.bStress = bStress;
        }

        public Task(
            byte bPipe,
            byte bChannelIndex
            )
        {
            oParam.eType = TaskType.Stop;
            oParam.oTransferParams.bChannelIndex = bChannelIndex;
            oParam.oTransferParams.bPipe = bPipe;
        }

        public Task(
            )
        {
            oParam.eType = TaskType.Close;
        }

        public Task(
            Int32 ulType
            )
        {
            if(ulType == 1)
                oParam.eType = TaskType.Exit;
        }

        public Task(
            DelegateCallbackTextBoxDebug fxnCallbackDebug
            )
        {
            oParam.eType = TaskType.Detect;
            oParam.oTransferParams.fxnCallbackDebug = fxnCallbackDebug;
        }

        public Task(
            DelegateCallbackTextBoxDebug fxnCallbackDebug,
            byte bOpenBy, 
            string OpenByString,
            UInt32 ulPacketSize,
            UInt32 ulQueueSize,
            bool bAsync
            )
        {
            oParam.eType = TaskType.TestMode;
            oParam.oTransferParams.fxnCallbackDebug = fxnCallbackDebug;
            oParam.OpenByString = OpenByString;
            oParam.bOpenBy = bOpenBy;
            oParam.oTransferParams.ulPacketSize = ulPacketSize;
            oParam.oTransferParams.ulQueueSize = ulQueueSize;
            oParam.oTransferParams.bAsync = bAsync;
        }

        public void SetResult(bool bResult)
        {
            oResult.eType = oParam.eType;
            oResult.bPipe = oParam.oTransferParams.bPipe;
            oResult.bResult = bResult;
        }

        public void SetResult(bool bResult, UInt32 ulNumDevices)
        {
            oResult.eType = TaskType.Detect;
            oResult.bResult = bResult;
            oResult.ulNumDevices = ulNumDevices;
        }

        public void SetResult(bool bResult, byte bPipe)
        {
            oResult.eType = TaskType.Start;
            oResult.bPipe = bPipe;
            oResult.bResult = bResult;
        }

        public void SetResult(bool bResult, byte bPipe, UInt32 ulMBps)
        {
            oResult.eType = TaskType.TestMode;
            oResult.bPipe = bPipe;
            oResult.bResult = bResult;
            oResult.ulMBps = ulMBps;
        }

        public void SetResult(bool bResult, byte bChannelIndex, byte bPipe, UInt32 ulBytesTransferred)
        {
            oResult.eType = TaskType.Start;
            oResult.bChannelIndex = bChannelIndex;
            oResult.bPipe = bPipe;
            oResult.bResult = bResult;
            oResult.ulBytesTransferred = ulBytesTransferred;
        }

        public void SetResult(bool bResult, byte bNumWritePipes, byte bNumReadPipes, bool bTransferResult, bool bUsb3)
        {
            oResult.eType = TaskType.Open;
            oResult.bResult = bResult;
            oResult.bNumWritePipes = bNumWritePipes;
            oResult.bNumReadPipes = bNumReadPipes;
            oResult.bTransferResult = bTransferResult;
            oResult.bUsb3 = bUsb3;
        }

        public void SetResult(bool bResult, UInt32 ulNumDevices, string szDescription, string szSerialNumber)
        {
            oResult.eType = oParam.eType;
            oResult.bResult = bResult;
            oResult.ulNumDevices = ulNumDevices;
            oResult.szDescription = szDescription;
            oResult.szSerialNumber = szSerialNumber;
        }

        public class TaskParam
        {
            public TaskType eType;
            public byte bOpenBy;
            public string OpenByString;
            public WorkerThread.TransferParams oTransferParams;
        }

        public class TaskResult
        {
            public bool IsWritePipe()
            {
                if (bPipe < 0x80)
                {
                    return true;
                }

                return false;
            }

            public TaskType eType;
            public bool bResult;
            public byte bChannelIndex;
            public byte bPipe;
            public byte bNumWritePipes;
            public byte bNumReadPipes;
            public UInt32 ulBytesTransferred;
            public bool bLoopbackCompleted;
            public bool bTransferResult;
            public UInt32 ulNumDevices;
            public string szDescription;
            public string szSerialNumber;
            public bool bTestMode;
            public bool bUsb3;
            public UInt32 ulMBps;
        }

        public enum TaskType
        {
            Detect,
            Open,
            Close,
            Start,
            Stop,
            TestMode,
            Exit,
        }

        public TaskType Type
        {
            get
            {
                return oParam.eType;
            }
        }

        public TaskParam Param
        {
            get
            {
                return oParam;
            }
        }

        public TaskResult Result
        {
            get
            {
                return oResult;
            }
        }

        private TaskParam oParam = new TaskParam();
        private TaskResult oResult = new TaskResult();
    }
}
