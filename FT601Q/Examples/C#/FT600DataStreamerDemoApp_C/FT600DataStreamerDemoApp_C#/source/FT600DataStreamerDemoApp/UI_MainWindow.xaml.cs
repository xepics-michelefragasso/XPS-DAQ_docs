/*
** UI_MainWindow.xaml.cs
**
** Copyright © 2016 Future Technology Devices International Limited
**
** C# Source file for Demo Application.
**
** Author: FTDI
** Project: C# Data Streamer Demo Application
** Module: Main entry
** Overview:
** 1. UI_MainWindow class implements event handlers which are called when UI controls are clicked, selected or changed.
** 2. UI_MainWindow class converts the event to a TASK item and sends it to the Task Manager thread queue.
** 3. APP_TaskManager dequeues a TASK item from its TASK queue and deploys the TASK to the Worker threads for read or write transfers.
** 4. APP_WorkerThread executes the TASK item and calls the completion handler of the Task Manager to inform about the task completion.
** 
** History:
**  1.0.0.0	- Initial version
**
*/

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Threading;
using System.Management;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using DemoUtility;



namespace DemoApp
{
    /// <summary>
    /// Interaction logic for UI_MainWindow.xaml
    /// </summary>
    public partial class UI_MainWindow : Window
    {
        public UI_MainWindow()
        {
            InitializeComponent();

            // Initialize log file and display environment including USB devices connected
            LogFile.Set(szLogFileName, true);
            LogFile.LogEnvironment();
            LogFile.LogUSBDevicesConnected();

            // Initialize UI controls
            InitializeControls();
            TextBoxOutputColor(false);

            // Initialize hot-plugging support
            //oHotPlug = new HotPlug();
            //oHotPlug.Register(HotPlugDeviceInserted, HotPlugDeviceRemoved);

            // Initialize task manager thread
            oTaskManager = new TaskManager();

            oTaskManager.TaskQueueEvent = new AutoResetEvent(false);

            threadTaskManager = new Thread(new ThreadStart(oTaskManager.Run));
            threadTaskManager.Start();
            oTaskManager.SetUICallback(Callback);

            // Pass a task to task manager thread
            TaskAddDetect();
        }

        private void InitializeControls()
        {
            oControl = new Controls();

            oControl.SetOpenClose(ButtonOpen, ButtonClose);

            oControl.SetOpenBy(
                new List<Tuple<RadioButton, TextBox>>
                {
                    new Tuple<RadioButton, TextBox>(RadioButtonDescription, TextBoxDescription),
                    new Tuple<RadioButton, TextBox>(RadioButtonSerialNumber, TextBoxSerialNumber),
                    new Tuple<RadioButton, TextBox>(RadioButtonIndex, TextBoxIndex)
                });

            var Pipe1 = new Tuple<byte, CheckBox, TextBox, Button, Button, TextBox, TextBox>
                     (0x02,
                     CheckBoxEP02,
                     TextBoxPacketSizeEP02,
                     ButtonStartEP02,
                     ButtonStopEP02,
                     TextBoxRateEP02,
					 TextBoxQueueSizeEP02);

            var Pipe2 = new Tuple<byte, CheckBox, TextBox, Button, Button, TextBox, TextBox>
                     (0x82,
                     CheckBoxEP82,
                     TextBoxPacketSizeEP82,
                     ButtonStartEP82,
                     ButtonStopEP82,
                     TextBoxRateEP82,
                     TextBoxQueueSizeEP82);

            oControl.SetPipeTransfer(
                new List<Tuple<byte, CheckBox, TextBox, Button, Button, TextBox, TextBox>>
                {
                    Pipe1, Pipe2
                });
        }


        #region TaskAdditions
        /// <summary>
        /// Add a task Detect
        /// </summary>
        private void TaskAddDetect()
        {
            oTaskManager.AddTask(new Task(
                CallbackDebug
                ));
        }

        /// <summary>
        /// Add a task Open
        /// </summary>
        private void TaskAddOpen(byte bOption, string szOption)
        {
            oTaskManager.AddTask(new Task(
                CallbackDebug, bOption, szOption
                ));
        }

        /// <summary>
        /// Add a task Close
        /// </summary>
        private void TaskAddClose()
        {
            oTaskManager.AddTask(new Task());
        }

        /// <summary>
        /// Add a task Exit
        /// </summary>
        private void TaskAddExit()
        {
            oTaskManager.AddTask(new Task(1));
        }


        /// <summary>
        /// Add a task Start transfer
        /// </summary>
        private void TaskAddStartTransfer(Tuple<byte, CheckBox, TextBox, Button, Button, TextBox, TextBox> Pipe)
        {
            oTaskManager.AddTask(new Task(
                CallbackDebug, CallbackRate,
                oControl.oPipeTransfer.GetPipeID(Pipe),
                oControl.oPipeTransfer.GetSize(Pipe),
                oControl.oPipeTransfer.GetChannelID(Pipe),
                oControl.oPipeTransfer.GetQueueSize(Pipe),
                (bool)CheckBoxAsync.IsChecked,
                oControl.DataPatternType,
                oControl.PatternValue
                ));
        }

        /// <summary>
        /// Add a task Stop transfer
        /// </summary>
        private void TaskAddStopTransfer(Tuple<byte, CheckBox, TextBox, Button, Button, TextBox, TextBox> Pipe)
        {
            oTaskManager.AddTask(new Task(
				oControl.oPipeTransfer.GetPipeID(Pipe),
                oControl.oPipeTransfer.GetChannelID(Pipe)
				));
        }

        /// <summary>
        /// Add a task TestMode
        /// </summary>
        private void TaskAddTestMode(byte bOption, string szOption, UInt32 ulSize)
        {
            oTaskManager.AddTask(new Task(
                CallbackDebug,
                bOption, szOption,
                ulSize,
                ulDefaultQueueSize,
                (bool)CheckBoxAsync.IsChecked
                ));
        }
        #endregion


        #region ButtonEventsOpenClose
        /// <summary>
        /// Event handler for button Open
        /// </summary>
        private void EventButtonOpen(object sender, RoutedEventArgs e)
        {
            if (oControl != null)
            {
                string szOption = "";
                byte bOption = 0;

                oControl.oOpenBy.GetSelectedOption(ref bOption, ref szOption);
                TaskAddOpen(bOption, szOption);

                LogFile.Log("Open by {0} [{1}]", ((Controls.OpenBy.Type)bOption).ToString(), szOption);
            }
        }

        /// <summary>
        /// Event handler for button Close
        /// </summary>
        private void EventButtonClose(object sender, RoutedEventArgs e)
        {
            if (oControl != null)
            {
                oControl.oOpenClose.SetDisable();
                oControl.oPipeTransfer.ExecuteAllPipes(StopPipe);
                TaskAddClose();

                LogFile.Log("Close");
            }
        }

        /// <summary>
        /// Event handler for radio button OpenBy options
        /// </summary>
        private void EventButtonOpenBy(object sender, RoutedEventArgs e)
        {
            if (oControl != null)
            {
                oControl.oOpenBy.Select((RadioButton)sender);
            }
        }
        #endregion


        #region ButtonEventsStartStopTransfers
        /// <summary>
        /// Event handler for button Start transfers
        /// </summary>
        private void EventButtonStart(object sender, RoutedEventArgs e)
        {
            if (oControl != null)
            {
                var Pipe = oControl.oPipeTransfer.FindPipe((Button)sender);
                StartPipe(Pipe);
            }
        }

        /// <summary>
        /// Event handler for button Stop transfers
        /// </summary>
        private void EventButtonStop(object sender, RoutedEventArgs e)
        {
            if (oControl != null)
            {
                var Pipe = oControl.oPipeTransfer.FindPipe((Button)sender);
                StopPipe(Pipe);
            }
        }

        private void StartPipe(Tuple<byte, CheckBox, TextBox, Button, Button, TextBox, TextBox> Pipe)
        {
            if (oControl != null)
            {
                if (oControl.oPipeTransfer.IsAPipeRunning(Pipe))
                {
                    TextBoxOutput("Error: Another pipe is still ongoing!\n");
                    return;
                }

                if (oControl.oPipeTransfer.IsEnabled(Pipe) && oControl.oPipeTransfer.IsChecked(Pipe))
                {
                    oControl.oPipeTransfer.SetPipeState(Controls.PipeState.Start, Pipe, true);
                    TaskAddStartTransfer(Pipe);
                    LogFile.Log("Start 0x{0:X2}", oControl.oPipeTransfer.GetPipeID(Pipe));
                }
            }
        }

        private void StopPipe(Tuple<byte, CheckBox, TextBox, Button, Button, TextBox, TextBox> Pipe)
        {
            if (oControl != null)
            {
                if (!oControl.oPipeTransfer.IsEnabled(Pipe) && oControl.oPipeTransfer.IsChecked(Pipe))
                {
                    oControl.oPipeTransfer.SetPipeState(Controls.PipeState.Stop, Pipe, true);
                    TaskAddStopTransfer(Pipe);

                    LogFile.Log("Stop 0x{0:X2}", oControl.oPipeTransfer.GetPipeID(Pipe));
                }
            }
        }
        #endregion


        #region CheckBoxEventsPipeEnableDisable
        /// <summary>
        /// Event handler for checkbox Pipe transfers
        /// </summary>
        private void EventCheckboxPipe(object sender, RoutedEventArgs e)
        {
            if (oControl != null)
            {
                var Pipe = oControl.oPipeTransfer.FindPipe((CheckBox)sender);
                oControl.oPipeTransfer.SetPipeState(Controls.PipeState.Select, Pipe, oControl.oPipeTransfer.IsChecked(Pipe));

                LogFile.Log("Select 0x{0:X2} {1}", oControl.oPipeTransfer.GetPipeID(Pipe), oControl.oPipeTransfer.IsChecked(Pipe));
            }
        }
        #endregion

        #region DataPattern
        /// <summary>
        /// Event handler for Data pattern selection
        /// </summary>
        private void EventButtonDataPatternFixedChange(object sender, RoutedEventArgs e)
        {
            //TextBox textBox = (TextBox)sender;
            TextBoxPattern.IsEnabled = true;
            if(oControl != null)
            {
                oControl.DataPatternType = (UInt32) Controls.PatternType.PatternFixed;
                oControl.PatternValue = UInt32.Parse(TextBoxPattern.Text);
            }
        }

        private void EventButtonDataPatternRandomChange(object sender, RoutedEventArgs e)
        {
            TextBoxPattern.IsEnabled = false;
            if (oControl != null)
            {
                oControl.DataPatternType = (UInt32)Controls.PatternType.PatternRandom;
            }
        }

        private void EventButtonDataPatternIncrementChange(object sender, RoutedEventArgs e)
        {
            TextBoxPattern.IsEnabled = false;
            if (oControl != null)
            {
                oControl.DataPatternType = (UInt32)Controls.PatternType.PatternIncremental;
            }
        }


        private void EventTextBoxDataPatternChanged(object sender, TextChangedEventArgs e)
        {
            if (oControl != null)
            {
                if (!string.IsNullOrEmpty(TextBoxPattern.Text))
                    oControl.PatternValue = UInt32.Parse(TextBoxPattern.Text);
            }
        }

        private void EventButtonDeviceTypeFT600(object sender, RoutedEventArgs e)
        {
            oControl.DataPatternType &= 0xFFFF; /* lower bits for pattern type and higher bits for device type */
            oControl.DataPatternType |= (((UInt32)Controls.DeviceType.FT600) << 16);

        }

        private void EventButtonDeviceTypeFT601(object sender, RoutedEventArgs e)
        {
            oControl.DataPatternType &= 0xFFFF; /* lower bits for pattern type and higher bits for device type */
            oControl.DataPatternType |= (((UInt32)Controls.DeviceType.FT601) << 16);
        }
        #endregion

        #region TextBoxCheckInput
        /// <summary>
        /// Event handler for textbox OpenBy Index
        /// </summary>
        private void EventTextBoxIndex(object sender, TextChangedEventArgs e)
        {
            textBoxChanged(sender, e, ulDefaultIndex);
        }

        /// <summary>
        /// Event handler for textbox Pipe transfer sizes
        /// </summary>
        private void EventTextBoxPacketSize(object sender, TextChangedEventArgs e)
        {
            if (true)
            {
                textBoxChanged(sender, e, ulDefaultPacketSizeAsync);
            }
            else
            {
                textBoxChanged(sender, e, 262144);
            }
        }

        /// <summary>
        /// Event handler for textbox Pipe transfer queue size
        /// </summary>
        private void EventTextBoxQueueSize(object sender, TextChangedEventArgs e)
        {
            textBoxChanged(sender, e, ulDefaultQueueSize);
        }
		
        /// <summary>
        /// Event handler for textbox Stress test size
        /// </summary>
        private void EventTextBoxStressSize(object sender, TextChangedEventArgs e)
        {
            textBoxChanged(sender, e, ulDefaultStressSize);
        }

        private void textBoxChanged(object sender, TextChangedEventArgs e, UInt32 ulDefaultValue)
        {
            TextBox textBox = (TextBox)sender;
            UInt32 num = 0;
            bool success = false;


            if (!string.IsNullOrEmpty(textBox.Text))
            {
                success = UInt32.TryParse(textBox.Text, out num);
                if (success & num > 0)
                {
                    textBox.Text.Trim();
                }
                else
                {
                    textBox.Text = ulDefaultValue.ToString();
                    textBox.SelectionStart = textBox.Text.Length;
                }
            }
        }
        #endregion


        #region TextBoxEventsDebugOutput
        /// <summary>
        /// Event handler for button ClearOutput
        /// </summary>
        private void EventButtonOutput(object sender, RoutedEventArgs e)
        {
            TextBoxOutputReset();
        }

        public void TextBoxOutputReset()
        {
            TextBoxDebug.Text = "";
        }

        public void TextBoxOutput(string str)
        {
            if (TextBoxDebug.LineCount > ulTextBoxDebugMaxLines)
            {
                TextBoxDebug.Text = "";
            }

            if (TextBoxDebug.Foreground == Brushes.Red)
            {
                TextBoxDebug.Foreground = Brushes.Black;
            }

            TextBoxDebug.Text += str;
            TextBoxDebug.Focus();
            TextBoxDebug.CaretIndex = TextBoxDebug.Text.Length;
            TextBoxDebug.ScrollToEnd();
        }

        public void TextBoxOutputErr(string str)
        {
            TextBoxDebug.Foreground = Brushes.Red;
            TextBoxDebug.Text += str;
            TextBoxDebug.Focus();
            TextBoxDebug.CaretIndex = TextBoxDebug.Text.Length;
            TextBoxDebug.ScrollToEnd();
        }

        public void TextBoxOutputColor(bool bOpen)
        {
            if (bOpen)
            {
                TextBoxDebug.Background = Brushes.White;
                TextBoxDebug.Foreground = Brushes.Black;
            }
            else
            {
                var bc = new BrushConverter();
                TextBoxDebug.Background = (Brush)bc.ConvertFrom("#FF0C7DAF");
                TextBoxDebug.Foreground = Brushes.White;
            }
        }
        #endregion


        #region HotPlugEvents
        ///// <summary>
        ///// Event handler for hotplug device plugin
        ///// </summary>
        //private void HotPlugDeviceInserted(object sender, EventArrivedEventArgs e)
        //{
        //    LogFile.Log("HotPlugDeviceInserted...");

        //    var instance = (ManagementBaseObject)e.NewEvent["TargetInstance"];

        //    foreach (var property in instance.Properties)
        //    {
        //        //LogFile.Log("{0} : {1}", 
        //        //    property.Name  != null ? (string)property.Name.ToString() : "NULL", 
        //        //    property.Value != null ? (string)property.Value.ToString() : "NULL");

        //        if (property.Name != null && property.Name.Equals("Caption"))
        //        {
        //            if (property.Value != null && !property.Value.Equals("USB Composite Device"))
        //            {
        //                ProcessHotPlugDeviceInserted();
        //                break;
        //            }
        //        }
        //    }
        //}

        ///// <summary>
        ///// Event handler for hotplug device plugout
        ///// </summary>
        //private void HotPlugDeviceRemoved(object sender, EventArrivedEventArgs e)
        //{
        //    LogFile.Log("HotPlugDeviceRemoved...");

        //    var instance = (ManagementBaseObject)e.NewEvent["TargetInstance"];

        //    foreach (var property in instance.Properties)
        //    {
        //        //LogFile.Log("{0} : {1}",
        //        //    property.Name != null ? (string)property.Name.ToString() : "NULL",
        //        //    property.Value != null ? (string)property.Value.ToString() : "NULL");

        //        if (property.Name != null && property.Name.Equals("DeviceID"))
        //        {
        //            string strValue = (string)property.Value;

        //            if (strValue != null && oTaskManager.IsDevicePath(strValue))
        //            {
        //                ProcessHotPlugDeviceRemoved();
        //                break;
        //            }
        //        }
        //    }
        //}

        /// <summary>
        /// Initialize hook for window messages for hot plug processing
        /// </summary>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            hWindowSource = PresentationSource.FromVisual(this) as HwndSource;
            hWindowSource.AddHook(WndProc);

            HotPlug2.Register(hWindowSource.Handle, TaskManager.GetGuid());
        }

        /// <summary>
        /// Method that receives window messages for hot plug processing
        /// </summary>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam, ref bool handled)
        {
            if (msg == HotPlug2.WM_DEVICECHANGE)
            {

                if (lparam == IntPtr.Zero)
                {
                    return IntPtr.Zero;
                }
                var obj = (HotPlug2.DevBroadcastDeviceinterface)Marshal.PtrToStructure(lparam, typeof(HotPlug2.DevBroadcastDeviceinterface));
                Int32 lOffset = (Marshal.SizeOf(typeof(UInt32)) * 3 + Marshal.SizeOf(typeof(Guid)));
                Int32 lSize = Marshal.ReadInt32(lparam, 0) - lOffset - 4;

                if(lSize <= 0)
                {
                    return IntPtr.Zero;
                }

                byte[] Name = new byte[lSize];
                for (Int32 i = 0; i < lSize; i++)
                {
                    Name[i] = Marshal.ReadByte(lparam, lOffset + i);
                }
                string devicePath = System.Text.Encoding.Unicode.GetString(Name);

                switch ((Int32)wparam)
                {
                    case HotPlug2.DBT_DEVICEARRIVAL:
                        {
                            LogFile.Log("Device attached! [{0}] [{1}]", devicePath, TaskManager.GetGuid().ToString());

                            if (devicePath.Contains(TaskManager.GetGuid().ToString()))
                            {
                                ProcessHotPlugDeviceInserted();
                            }

                            break;
                        }
                    case HotPlug2.DBT_DEVICEREMOVECOMPLETE:
                        {
                            LogFile.Log("Device detached! [{0}]", devicePath);

                            if (oTaskManager.IsDevicePathEx(devicePath))
                            {
                                ProcessHotPlugDeviceRemoved();
                            }

                            break;
                        }
                }
            }

            handled = false;
            return IntPtr.Zero;
        }

        private void ProcessHotPlugDeviceInserted()
        {
            if (Application.Current != null)
            {
                LogFile.Log("HotPlugDeviceInserted process...");

                Thread.Sleep(100);

                Application.Current.Dispatcher.BeginInvoke(
                    DispatcherPriority.Normal,
                    new DelegeteCallbackHotPlug(CallbackDeviceInserted)
                    );
            }
        }

        private void ProcessHotPlugDeviceRemoved()
        {
            if (Application.Current != null)
            {
                LogFile.Log("HotPlugDeviceRemoved process...");

                Application.Current.Dispatcher.BeginInvoke(
                    DispatcherPriority.Background,
                    new DelegeteCallbackHotPlug(CallbackDeviceRemoved)
                    );
            }
        }

        /// <summary>
        /// Event handler for application closing
        /// </summary>
        private void HandleApplicationClosing(object sender, CancelEventArgs e)
        {
            LogFile.Log("Application closing...");

            //if (oHotPlug != null)
            //{
            //    oHotPlug.Unregister();
            //    oHotPlug = null;
            //}
            TaskAddExit();
        }
        #endregion


		private const string szLogFileName = "FT600DataStreamerDemoApp.txt";
        private const UInt32 ulDefaultPacketSize = 16777216;
        private const UInt32 ulDefaultPacketSizeAsync = 262144;
        private const UInt32 ulDefaultStressSize = 65536;
        private const UInt32 ulDefaultQueueSize = 16;		
        private const UInt32 ulDefaultIndex = 0;
        private const UInt32 ulTextBoxDebugMaxLines = 1000;
        private Thread threadTaskManager;
        private TaskManager oTaskManager;
        //private HotPlug oHotPlug;
        private Controls oControl;
        private HwndSource hWindowSource;




    }
}
