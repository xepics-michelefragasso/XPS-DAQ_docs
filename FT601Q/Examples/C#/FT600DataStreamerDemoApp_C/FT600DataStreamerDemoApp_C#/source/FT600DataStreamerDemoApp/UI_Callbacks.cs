/*
** UI_Callbacks.cs
**
** Copyright © 2016 Future Technology Devices International Limited
**
** C# Source file for Demo Application.
**
** Author: FTDI
** Project: C# Data Streamer Demo Application
** Module: Callback implementation
**
** History:
**  1.0.0.0	- Initial version
**
*/

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Threading;
using DemoUtility;



namespace DemoApp
{
    public delegate void DelegateCallbackTask(Task.TaskResult oResult);
    public delegate void DelegateCallbackTextBoxDebug(string szDebug);
    public delegate void DelegateCallbackTextBoxRate(byte bPipe, UInt32 ulRateMBps);
    public delegate void DelegeteCallbackHotPlug();

    public partial class UI_MainWindow : Window
    {
        /// <summary>
        /// Callback function called by the thread manager
        /// </summary>
        public void Callback(Task.TaskResult oResult)
        {
            switch (oResult.eType)
            {
                case Task.TaskType.Detect:
                    {
                        CallbackDetect(oResult.bResult, oResult.ulNumDevices);
                        break;
                    }

                case Task.TaskType.Open:
                    {
                        CallbackOpen(oResult.bResult, oResult.bNumWritePipes, oResult.bNumReadPipes, oResult.bTransferResult, oResult.bUsb3);
                        break;
                    }
                case Task.TaskType.Close:
                    {
                        CallbackClose(oResult.bResult);
                        break;
                    }
                case Task.TaskType.Start:
                    {
                        CallbackTransfer(oResult.bPipe, oResult.bResult);
                        break;
                    }
                case Task.TaskType.Stop:
                    {
                        CallbackAbortTransfer(oResult.bPipe, oResult.bResult);
                        break;
                    }
                case Task.TaskType.TestMode:
                    {
                        CallbackTestMode(oResult.bResult);
                        break;
                    }
            }
        }

        /// <summary>
        /// Internal helper function called by Callback()
        /// </summary>
        private void CallbackDetect(bool bResult, UInt32 ulNumDevices)
        {
            if (bResult && ulNumDevices > 0)
            {
                LogFile.Log("CallbackDetect success={0}\r\n", bResult);

                // Automatically open the device

                string szOption = "";
                byte bOption = 0;

                oControl.oOpenBy.SelectDefault();
                oControl.oOpenBy.GetSelectedOption(ref bOption, ref szOption);
                if (UInt32.Parse(szOption) >= ulNumDevices)
                {
                    szOption = "0";
                    oControl.oOpenBy.SetOptionValue(bOption, szOption);
                }

                TaskAddOpen(bOption, szOption);
            }
            else
            {
                TextBoxOutputReset();
                TextBoxOutput("No device is connected!\n");
                LogFile.Log("CallbackDetect success={0}\r\n", bResult);
            }
        }

        /// <summary>
        /// Internal helper function called by Callback()
        /// </summary>
        private void CallbackOpen(bool bResult, byte bNumWritePipes, byte bNumReadPipes, bool bTransferResult, bool bUsb3)
        {
            if (bResult)
            {
                oControl.oOpenClose.SetState(Controls.State.Open);
                oControl.oOpenBy.SetState(Controls.State.Open);
                oControl.oPipeTransfer.SetState(Controls.State.Open, bNumWritePipes, bNumReadPipes, bUsb3);
                TextBoxOutputColor(true);
                CheckBoxAsync.IsEnabled = true;

                if (CheckBoxTestMode.IsChecked == true)
                {
                    TextBoxOutput("\nTest mode started!\n\n");
                    LogFile.Log("Test mode is enabled\r\n");

                    oControl.oPipeTransfer.SetState(Controls.State.Close, 0, 0, bUsb3);
                    CheckBoxAsync.IsEnabled = false;

                    HotPlug2.Unregister();
                    //oHotPlug.Unregister();

                    string szOption = "";
                    byte bOption = 0;
                    oControl.oOpenBy.GetSelectedOption(ref bOption, ref szOption);
                    TaskAddTestMode(bOption, szOption, oControl.oPipeTransfer.GetSaveSize());
                }

                CheckBoxTestMode.IsEnabled = false;
            }
            else
            {
                if (bNumWritePipes == 0 && bNumReadPipes == 0)
                {
                    TextBoxOutput("No device is connected!\n");
                }
                else if (!bTransferResult)
                {
                    TextBoxOutput("\r\n");
                    TextBoxOutput("ERROR: Device can be opened but basic streaming failed!\r\n");
                    TextBoxOutput("\r\n");
                    TextBoxOutput("Possible causes:\r\n");
                    TextBoxOutput("1. FPGA is for 245 mode but chip was not configured to 245 mode.\r\n");
                    TextBoxOutput("2. FPGA is for 600 mode but chip was configured to 245 mode.\r\n");
                    TextBoxOutput("3. No FPGA is connected to the PCB board.\r\n");
                    TextBoxOutput("4. FPGA image is not correct.\n   (Ex: Data Loopback FPGA is used instead of Data Streamer FPGA)\r\n");
                    TextBoxOutput("5. FPGA image is not sending data. (Ex: FPGA has bugs. Try to reset FPGA.)\r\n");
                    TextBoxOutput("\r\n\r\n");
                }
            }

            LogFile.Log("CallbackOpen success={0}\r\n", bResult);
        }

        /// <summary>
        /// Internal helper function called by Callback()
        /// </summary>
        private void CallbackClose(bool bResult)
        {
            oControl.oOpenClose.SetState(Controls.State.Close);
            oControl.oOpenBy.SetState(Controls.State.Close);
            oControl.oPipeTransfer.SetState(Controls.State.Close);
            TextBoxOutputColor(false);

            CheckBoxAsync.IsEnabled = false;
            CheckBoxTestMode.IsEnabled = true;

            LogFile.Log("CallbackClose success={0}\r\n", bResult);
        }

        /// <summary>
        /// Internal helper function called by Callback()
        /// </summary>
        private void CallbackTransfer(byte bPipe, bool bResult)
        {
            var Pipe = oControl.oPipeTransfer.FindPipe(bPipe);
            oControl.oPipeTransfer.SetPipeState(Controls.PipeState.CallbackStart, Pipe, true);
            TextBoxOutput(String.Format("{0} transfer stopped!\n", oControl.oPipeTransfer.GetPipeID(Pipe) < 0x80 ? "Write" : "Read"));

            LogFile.Log("CallbackTransfer success={0} pipe=0x{1:X2}\r\n", bResult, bPipe);
        }

        /// <summary>
        /// Internal helper function called by Callback()
        /// </summary>
        private void CallbackAbortTransfer(byte bPipe, bool bResult)
        {
            var Pipe = oControl.oPipeTransfer.FindPipe(bPipe);
            oControl.oPipeTransfer.SetPipeState(Controls.PipeState.CallbackStop, Pipe, true);
            TextBoxOutput(String.Format("{0} transfer stopped!\n", oControl.oPipeTransfer.GetPipeID(Pipe) < 0x80 ? "Write" : "Read"));

            LogFile.Log("CallbackAbortTransfer success={0} pipe=0x{1:X2}", bResult, bPipe);
        }

        /// <summary>
        /// Callback function called by the thread manager
        /// </summary>
        public void CallbackDebug(string str)
        {
            TextBoxOutput(str);

            LogFile.Log(str);
        }

        /// <summary>
        /// Callback function called by the thread worker
        /// </summary>
        public void CallbackRate(byte bPipe, UInt32 ulRateMBps)
        {
            var Pipe = oControl.oPipeTransfer.FindPipe(bPipe);
            oControl.oPipeTransfer.SetRate(Pipe, ulRateMBps);
        }

        /// <summary>
        /// Callback function for hot plugging called by the system
        /// </summary>
        public void CallbackDeviceInserted()
        {
            TextBoxOutput("Device is plugged!\r\n\r\n");
            LogFile.Log("Device is plugged!");

            oControl.oOpenBy.SelectDefault();
            TaskAddDetect();

            LogFile.Log("CallbackDeviceInserted");
        }

        /// <summary>
        /// Callback function for hot plugging called by the system
        /// </summary>
        public void CallbackDeviceRemoved()
        {
            TextBoxOutput("Plug-in device! Application will detect it automatically!\r\n\r\n");
            LogFile.Log("Device is unplugged!");

            oControl.oPipeTransfer.ExecuteAllPipes(StopPipe);
            TaskAddClose();

            LogFile.Log("CallbackDeviceRemoved");
        }

        /// <summary>
        /// Internal helper function called by Callback()
        /// </summary>
        private void CallbackTestMode(bool bResult)
        {
            oControl.oOpenClose.SetState(Controls.State.Close);
            oControl.oOpenBy.SetState(Controls.State.Close);
            oControl.oPipeTransfer.SetState(Controls.State.Close);
            TextBoxOutputColor(false);

            CheckBoxAsync.IsEnabled = false;
            CheckBoxTestMode.IsEnabled = true;
            CheckBoxTestMode.IsChecked = false;

            HotPlug2.Register(hWindowSource.Handle, TaskManager.GetGuid());
            //oHotPlug.Register(HotPlugDeviceInserted, HotPlugDeviceRemoved);

            TextBoxOutput(String.Format("\nTest mode completed!\n\n"));
            LogFile.Log("CallbackTestMode success={0}\r\n", bResult);
        }
		
    }
}
