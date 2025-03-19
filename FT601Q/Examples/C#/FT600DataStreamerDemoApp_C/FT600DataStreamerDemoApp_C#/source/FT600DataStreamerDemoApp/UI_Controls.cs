/*
** UI_Controls.cs
**
** Copyright © 2016 Future Technology Devices International Limited
**
** C# Source file for Demo Application.
**
** Author: FTDI
** Project: C# Data Streamer Demo Application
** Module: UI Controls
**
** History:
**  1.0.0.0	- Initial version
**
*/

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;



namespace DemoApp
{
    public class Controls
    {
        /// <summary>
        /// Class for Open and Close button controls
        /// </summary>
        public class OpenClose
        {
            public OpenClose(Button _btnOpen, Button _btnClose)
            {
                btnOpen = _btnOpen;
                btnClose = _btnClose;
                state = State.Close;
            }

            public void SetState(State _state)
            {
                state = _state;

                if (state == State.Open)
                {
                    btnOpen.IsEnabled = false;
                    btnClose.IsEnabled = true;
                }
                else
                {
                    btnOpen.IsEnabled = true;
                    btnClose.IsEnabled = false;
                }
            }

            public void SetDisable()
            {
                btnOpen.IsEnabled = false;
                btnClose.IsEnabled = false;
            }

            public bool IsOpen()
            {
                if (state == State.Open)
                {
                    return true;
                }

                return false;
            }

            private Button btnOpen;
            private Button btnClose;
            private State state;
        }

        /// <summary>
        /// Class for OpenBy options controls
        /// </summary>
        public class OpenBy
        {
            public OpenBy(List<Tuple<RadioButton, TextBox>> _ListOpenBy)
            {
                ListOpenBy = _ListOpenBy;
            }

            public void Select(RadioButton radioBtn)
            {
                var OpenBy = ListOpenBy.Find(x => x.Item1 == radioBtn);

                OpenBy.Item2.IsEnabled = (bool)OpenBy.Item1.IsChecked;
                if (OpenBy.Item2.IsEnabled == false)
                {
                    OpenBy.Item2.Foreground = System.Windows.Media.Brushes.Black;
                }
                else
                {
                    OpenBy.Item2.Foreground = System.Windows.Media.Brushes.White;
                }
            }

            public void SelectDefault()
            {
                if (ListOpenBy[(byte)Type.Index].Item1.IsChecked == false)
                {
                    foreach (var OpenBy in ListOpenBy)
                    {
                        if (OpenBy.Item1.IsChecked == true)
                        {
                            OpenBy.Item1.IsChecked = false;
                            OpenBy.Item2.IsEnabled = false;
                            OpenBy.Item2.Foreground = System.Windows.Media.Brushes.Black;
                        }
                    }

                    ListOpenBy[(byte)Type.Index].Item1.IsChecked = true;
                    ListOpenBy[(byte)Type.Index].Item2.IsEnabled = true;
                    ListOpenBy[(byte)Type.Index].Item2.Foreground = System.Windows.Media.Brushes.White;
                }
            }

            public void GetSelectedOption(ref byte bOption, ref string strOption)
            {
                byte i = 0;

                foreach (var OpenBy in ListOpenBy)
                {
                    if (OpenBy.Item1.IsChecked == true)
                    {
                        strOption = OpenBy.Item2.Text;
                        bOption = i++;
                        break;
                    }

                    i++;
                }
            }

            public void SetOptionValue(byte bOption, string strOption)
            {
                if (bOption < ListOpenBy.Count)
                {
                    ListOpenBy[bOption].Item2.Text = strOption;
                }
            }

            public void SetState(State state)
            {
                bool bEnable = (state == State.Open ? false : true);

                foreach (var OpenBy in ListOpenBy)
                {
                    OpenBy.Item2.IsEnabled = (bool)OpenBy.Item1.IsChecked;
                    OpenBy.Item1.IsEnabled = bEnable;

                    if (OpenBy.Item2.IsEnabled == false)
                    {
                        OpenBy.Item2.Foreground = System.Windows.Media.Brushes.Black;
                    }
                    else
                    {
                        OpenBy.Item2.Foreground = System.Windows.Media.Brushes.White;
                    }
                }
            }

            public enum Type
            {
                Description,
                SerialNumber,
                Index,
                Count,
            };

            private List<Tuple<RadioButton, TextBox>> ListOpenBy;
        }

        /// <summary>
        /// Class for Pipe transfer controls
        /// </summary>
        public class PipeTransfer
        {
            public PipeTransfer(
                List<Tuple<byte, CheckBox, TextBox, Button, Button, TextBox, TextBox>> _ListPipes
                )
            {
                ListPipes = _ListPipes;
            }

            public delegate void DelegateExecute(Tuple<byte, CheckBox, TextBox, Button, Button, TextBox, TextBox> Pipe);

            public void ExecuteAllPipes(DelegateExecute fxn)
            {
                foreach (var Pipe in ListPipes)
                {
                    fxn(Pipe);
                }
            }

            public bool IsAPipeRunning(Tuple<byte, CheckBox, TextBox, Button, Button, TextBox, TextBox> Pipe)
            {
                foreach (var PipeItem in ListPipes)
                {
                    if (GetPipeID(Pipe) != GetPipeID(PipeItem))
                    {
                        if (IsRunning(PipeItem))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            public void SetState(State state, byte bNumWritePipes = 0, byte bNumReadPipes = 0, bool bIsUSB3 = true)
            {
                bool bEnable = (state == State.Open ? true : false);

                if (bEnable)
                {
                    byte bNumWritePipeCount = 0;
                    byte bNumReadPipeCount = 0;

                    SaveSize((UInt32)262144);

                    foreach (var Pipe in ListPipes)
                    {
                        if (GetPipeID(Pipe) < 0x80)
                        {
                            if (bNumWritePipeCount < bNumWritePipes)
                            {
                                bNumWritePipeCount++;
                                SetCheck(Pipe, true);
                                SetEnable(Pipe, true);
                                SetSize(Pipe);
                            }
                            else
                            {
                                SetCheck(Pipe, false);
                                SetEnable(Pipe, false);
                            }
                        }
                        else
                        {
                            if (bNumReadPipeCount < bNumReadPipes)
                            {
                                bNumReadPipeCount++;
                                SetCheck(Pipe, true);
                                SetEnable(Pipe, true);
                                SetSize(Pipe);
                            }
                            else
                            {
                                SetCheck(Pipe, false);
                                SetEnable(Pipe, false);
                            }
                        }

                        SetPipeState(PipeState.CallbackOpen, Pipe, IsChecked(Pipe));
                        SetRate(Pipe, 0);
                    }
                }
                else
                {
                    foreach (var Pipe in ListPipes)
                    {
                        SetPipeState(PipeState.CallbackClose, Pipe, false);
                    }
                }
            }

            public void SetPipeState(
                PipeState state,
                Tuple<byte, CheckBox, TextBox, Button, Button, TextBox, TextBox> Pipe,
                bool bEnable)
            {
                switch (state)
                {
                    case PipeState.CallbackOpen: // fall-through
                    case PipeState.Select:
                        {
                            Pipe.Item3.IsEnabled = bEnable;
                            Pipe.Item4.IsEnabled = bEnable;
                            Pipe.Item5.IsEnabled = bEnable;
                            Pipe.Item6.IsEnabled = bEnable;
                            Pipe.Item7.IsEnabled = bEnable;
                            break;
                        }
                    case PipeState.Start:
                        {
                            Pipe.Item2.IsEnabled = false;
                            Pipe.Item3.IsEnabled = false;
                            Pipe.Item4.IsEnabled = false;
                            Pipe.Item5.IsEnabled = true;
                            Pipe.Item6.IsEnabled = true;
                            Pipe.Item7.IsEnabled = false;
                            break;
                        }
                    case PipeState.Stop:
                        {
                            Pipe.Item6.IsEnabled = false;
                            break;
                        }
                    case PipeState.CallbackStart: // fall-through
                    case PipeState.CallbackStop:  // fall-through
                    case PipeState.CallbackClose:
                        {
                            Pipe.Item2.IsEnabled = bEnable;
                            Pipe.Item3.IsEnabled = bEnable;
                            Pipe.Item4.IsEnabled = bEnable;
                            Pipe.Item5.IsEnabled = bEnable;
                            Pipe.Item6.IsEnabled = bEnable;
                            Pipe.Item7.IsEnabled = bEnable;
                            break;
                        }
                }
            }

            public List<Tuple<byte, CheckBox, TextBox, Button, Button, TextBox, TextBox>>
                GetListPipes()
            {
                return ListPipes;
            }

            public Tuple<byte, CheckBox, TextBox, Button, Button, TextBox, TextBox>
                FindPipe(byte pipe)
            {
                return ListPipes.Find(x => GetPipeID(x) == pipe);
            }

            public Tuple<byte, CheckBox, TextBox, Button, Button, TextBox, TextBox>
                FindPipe(Button btn)
            {
                return ListPipes.Find(x => GetStart(x) == btn || GetStop(x) == btn);
            }

            public Tuple<byte, CheckBox, TextBox, Button, Button, TextBox, TextBox>
                FindPipe(CheckBox cb)
            {
                return ListPipes.Find(x => GetEnabled(x) == cb);
            }

            public byte GetPipeID(
                Tuple<byte, CheckBox, TextBox, Button, Button, TextBox, TextBox> Pipe)
            {
                return Pipe.Item1;
            }

            public CheckBox GetEnabled(
                Tuple<byte, CheckBox, TextBox, Button, Button, TextBox, TextBox> Pipe)
            {
                return Pipe.Item2;
            }

            public void SetEnable(
                Tuple<byte, CheckBox, TextBox, Button, Button, TextBox, TextBox> Pipe, bool bEnable)
            {
                Pipe.Item2.IsEnabled = bEnable;
            }

            public bool IsEnabled(
                Tuple<byte, CheckBox, TextBox, Button, Button, TextBox, TextBox> Pipe)
            {
                return Pipe.Item2.IsEnabled;
            }

            public bool IsRunning(
                Tuple<byte, CheckBox, TextBox, Button, Button, TextBox, TextBox> Pipe)
            {
                return !Pipe.Item4.IsEnabled && Pipe.Item5.IsEnabled;
            }

            public void SetCheck(
                Tuple<byte, CheckBox, TextBox, Button, Button, TextBox, TextBox> Pipe, bool bEnable)
            {
                Pipe.Item2.IsChecked = bEnable;
            }

            public bool IsChecked(
                Tuple<byte, CheckBox, TextBox, Button, Button, TextBox, TextBox> Pipe)
            {
                return (bool)Pipe.Item2.IsChecked;
            }

            public UInt32 GetSize(
                Tuple<byte, CheckBox, TextBox, Button, Button, TextBox, TextBox> Pipe)
            {
                return UInt32.Parse(Pipe.Item3.Text);
            }

            public void SetSize(
                Tuple<byte, CheckBox, TextBox, Button, Button, TextBox, TextBox> Pipe)
            {
                if (Pipe.Item2.IsChecked == true)
                {
                    Pipe.Item3.Text = ulSaveSize.ToString();
                }
            }

            public void SaveSize(UInt32 ulSize)
            {
                ulSaveSize = ulSize;
            }

            public UInt32 GetSaveSize()
            {
                return ulSaveSize;
            }

            public Button GetStart(
                Tuple<byte, CheckBox, TextBox, Button, Button, TextBox, TextBox> Pipe)
            {
                return Pipe.Item4;
            }

            public Button GetStop(
                Tuple<byte, CheckBox, TextBox, Button, Button, TextBox, TextBox> Pipe)
            {
                return Pipe.Item5;
            }

            public UInt32 GetRate(
                Tuple<byte, CheckBox, TextBox, Button, Button, TextBox, TextBox> Pipe)
            {
                return UInt32.Parse(Pipe.Item6.Text);
            }

            public void SetRate(
                Tuple<byte, CheckBox, TextBox, Button, Button, TextBox, TextBox> Pipe, UInt32 ulRate)
            {
                Pipe.Item6.Text = ulRate.ToString();
            }

            public byte GetChannelID(
                Tuple<byte, CheckBox, TextBox, Button, Button, TextBox, TextBox> Pipe)
            {
                return 0;
            }
			
            public UInt32 GetQueueSize(
                Tuple<byte, CheckBox, TextBox, Button, Button, TextBox, TextBox> Pipe)
            {
                return UInt32.Parse(Pipe.Item7.Text);
            }

            private List<Tuple<byte, CheckBox, TextBox, Button, Button, TextBox, TextBox>> ListPipes;
            private UInt32 ulSaveSize = 0;
        }


        public void SetOpenClose(Button _btnOpen, Button _btnClose)
        {
            oOpenClose = new OpenClose(_btnOpen, _btnClose);
        }

        public void SetOpenBy(List<Tuple<RadioButton, TextBox>> _ListOpenBy)
        {
            oOpenBy = new OpenBy(_ListOpenBy);
        }

        public void SetPipeTransfer(
            List<Tuple<byte, CheckBox, TextBox, Button, Button, TextBox, TextBox>> _ListPipes
            )
        {
            oPipeTransfer = new PipeTransfer(_ListPipes);
        }


        public enum State
        {
            Open,
            Close
        };

        public enum PipeState
        {
            Select,
            Start,
            Stop,
            CallbackOpen,
            CallbackStart,
            CallbackStop,
            CallbackClose,
        };

        public enum PatternType
        {
            PatternIncremental,
            PatternRandom,
            PatternFixed,
        };

        public enum DeviceType
        {
            FT600 = 600,
            FT601 = 601,
        };
        public OpenClose oOpenClose;
        public OpenBy oOpenBy;
        public PipeTransfer oPipeTransfer;
        public UInt32 DataPatternType;
        public UInt32 PatternValue;
    }
}

