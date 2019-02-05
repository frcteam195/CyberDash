using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using SharperOSC;
using Emgu.CV;
using Emgu.CV.Structure;
using SharpDX.DirectInput;
using SharpDX.XInput;
using CyberDash.Utilities;
using System.Management;
using System.Net.Sockets;
using System.Net;

namespace CyberDash
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static readonly double DRIVER_STATION_HEIGHT = 240;
        private BackgroundWorker oscSender = new BackgroundWorker();
        private BackgroundWorker oscReceiver = new BackgroundWorker();

        private readonly int CAMERA_DATA_PORT = 5801;
        private readonly int AUTO_DATA_PORT = 5805;
        private readonly int JOYSTICK_DATA_PORT = 5806;
        private readonly string ROBOT_IP = "10.1.95.2";
        //private readonly string ROBOT_IP = "10.0.2.91";
        private bool runThread = true;

        private List<FRCJoystick> joysticks = new List<FRCJoystick>();
        private object joystickLock = new object();

        private bool Enabled { get; set; } = false;

        private Thread joystickCaptureThread;
        private Thread cameraCaptureThread;

        public MainWindow()
        {
            InitializeComponent();

            enumerateJoysticks();

            oscSender.DoWork += oscSenderWorker_DoWork;
            oscSender.RunWorkerCompleted += oscSenderWorker_RunWorkerCompleted;

            oscReceiver.DoWork += oscReceiverWorker_DoWork;
            oscReceiver.RunWorkerCompleted += oscReceiverWorker_RunWorkerCompleted;

            joystickCaptureThread = new Thread(() =>
            {
                UDPSender udpSender = null;
                try
                {
                    udpSender = new UDPSender(ROBOT_IP, JOYSTICK_DATA_PORT);
                }
                catch (Exception)
                {
                    MessageBox.Show("Error creating UDP Sender. Make sure your IP Address and port settings are correct!", "Dashboard", MessageBoxButton.OK, MessageBoxImage.Error);
                    Environment.Exit(1);
                }
                ThreadRateControl trc = new ThreadRateControl();

                List<OscMessage> messageList = new List<OscMessage>();
                object lockObject = new object();
                trc.Start();
                while (runThread)
                {
                    messageList.Clear();
                    lock (joystickLock)
                    {
                        Parallel.ForEach(joysticks, (j) =>
                        {
                            try
                            {
                                List<object> lO = new List<object>();
                                if (j != null)
                                {
                                    if (j.xJoystick != null)
                                    {
                                        Gamepad gamepad = j.xJoystick.GetState().Gamepad;
                                        lO.Add((int)gamepad.LeftThumbX);
                                        lO.Add((int)gamepad.LeftThumbY);
                                        lO.Add((int)(gamepad.LeftTrigger * 128.5));
                                        lO.Add((int)(gamepad.RightTrigger * 128.5));
                                        lO.Add((int)gamepad.RightThumbX);
                                        lO.Add((int)gamepad.RightThumbY);

                                        bool[] buttonArr = new bool[10];
                                        buttonArr[0] = gamepad.Buttons.HasFlag(GamepadButtonFlags.A);
                                        buttonArr[1] = gamepad.Buttons.HasFlag(GamepadButtonFlags.B);
                                        buttonArr[2] = gamepad.Buttons.HasFlag(GamepadButtonFlags.X);
                                        buttonArr[3] = gamepad.Buttons.HasFlag(GamepadButtonFlags.Y);
                                        buttonArr[4] = gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder);
                                        buttonArr[5] = gamepad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder);
                                        buttonArr[6] = gamepad.Buttons.HasFlag(GamepadButtonFlags.Start);
                                        buttonArr[7] = gamepad.Buttons.HasFlag(GamepadButtonFlags.Back);
                                        buttonArr[8] = gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftThumb);
                                        buttonArr[9] = gamepad.Buttons.HasFlag(GamepadButtonFlags.RightThumb);
                                        lO.Add(convertBoolArrToLong(buttonArr));

                                        gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp);
                                        gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight);
                                        gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadDown);
                                        gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft);

                                        int pov = -1;
                                        switch ((ushort)gamepad.Buttons)
                                        {
                                            case 1:
                                                pov = 0;
                                                break;
                                            case 9:
                                                pov = 45;
                                                break;
                                            case 8:
                                                pov = 90;
                                                break;
                                            case 10:
                                                pov = 135;
                                                break;
                                            case 2:
                                                pov = 180;
                                                break;
                                            case 6:
                                                pov = 225;
                                                break;
                                            case 4:
                                                pov = 270;
                                                break;
                                            case 5:
                                                pov = 315;
                                                break;
                                            default:
                                                pov = -1;
                                                break;
                                        }
                                        lO.Add(pov * 100);
                                        lO.Add((long)DateTime.UtcNow.ToUniversalTime().Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds);
                                    }
                                    else
                                    {
                                        JoystickState js = j.dJoystick.GetCurrentState();
                                        lO.Add(js.X - 32768);
                                        lO.Add(js.Y - 32768);
                                        lO.Add(js.Z - 32768);
                                        lO.Add(js.RotationX - 32768);
                                        lO.Add(js.RotationY - 32768);
                                        lO.Add(js.RotationZ - 32768);
                                        lO.Add(convertBoolArrToLong(js.Buttons));
                                        lO.Add(js.PointOfViewControllers[0]);
                                        lO.Add((long)DateTime.UtcNow.ToUniversalTime().Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds);
                                    }
                                    lock (lockObject)
                                    {
                                        messageList.Add(new OscMessage("/Joysticks/" + j.Index, lO.ToArray()));
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.ToString());
                                //TODO: Invoke joystick refresh
                            }
                        });
                    }
                    OscBundle messageBundle = new OscBundle((ulong)1, messageList.ToArray());
                    udpSender.Send(messageBundle);

                    trc.DoRateControl(10);
                }
                udpSender.Close();
            });

            joystickCaptureThread.Start();

            cameraCaptureThread = new Thread(() =>
            {
                UdpClient listener = new UdpClient(CAMERA_DATA_PORT);
                IPEndPoint groupEP = new IPEndPoint(IPAddress.Any, CAMERA_DATA_PORT);
                Dispatcher.Invoke(() => {
                    imgViewer.Stretch = Stretch.Uniform;
                    RenderOptions.SetBitmapScalingMode(imgViewer, BitmapScalingMode.LowQuality);
                });
                while (runThread)
                {
                    byte[] receive_byte_array = listener.Receive(ref groupEP);
                    Dispatcher.Invoke(() => {
                        imgViewer.Source = ToImage(receive_byte_array);
                    });
                }
            });

            cameraCaptureThread.Start();

            System.Windows.Threading.DispatcherTimer refreshViewTimer = new System.Windows.Threading.DispatcherTimer();
            refreshViewTimer.Tick += refreshViewTimer_Tick;
            refreshViewTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            refreshViewTimer.Start();

            System.Windows.Threading.DispatcherTimer refocusTimer = new System.Windows.Threading.DispatcherTimer();
            refocusTimer.Tick += refocusTimer_Tick;
            refocusTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            refocusTimer.Start();
        }

        public BitmapImage ToImage(byte[] array)
        {
            using (var ms = new System.IO.MemoryStream(array))
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad; // here
                image.StreamSource = ms;
                image.EndInit();
                return image;
            }
        }

        private long convertBoolArrToLong(bool[] arr)
        {
            int maxSize = arr.Length > 64 ? 64 : arr.Length;
            long val = 0;
            for (int i = 0; i < maxSize; i++)
            {
                val |= arr[i] ? (long)(1 << i) : 0;
            }
            return val;
        }

        private void refreshViewTimer_Tick(object sender, EventArgs e)
        {
            if (!Enabled)
            {
                lstJoystick.Items.Refresh();
            }
        }

        private void refocusTimer_Tick(object sender, EventArgs e)
        {
            if (Enabled)
            {
                this.Activate();
                this.Focus();
            }
        }

        private void enumerateJoysticks()
        {
            DirectInput directInput = new DirectInput();
            List<FRCJoystick> prevJoysticks = new List<FRCJoystick>(joysticks);
            List<string> deviceIds = GetXInputCapableDevices();
            lock (joystickLock)
            {
                joysticks.Clear();
                foreach (DeviceInstance deviceInstance in directInput.GetDevices(SharpDX.DirectInput.DeviceType.Gamepad, DeviceEnumerationFlags.AllDevices))
                {
                    ulong Data1 = BitConverter.ToUInt32(deviceInstance.ProductGuid.ToByteArray(), 0);
                    if (!deviceIds.Exists(x => x.Contains(Data1.ToString("X"))))
                    {
                        if (prevJoysticks.Exists(x => x.dJoystick.Information.InstanceGuid == deviceInstance.InstanceGuid))
                        {
                            joysticks.Add(prevJoysticks.Find(x => x.dJoystick.Information.InstanceGuid == deviceInstance.InstanceGuid));
                        }
                        else
                        {
                            joysticks.Add(new FRCJoystick(new Joystick(directInput, deviceInstance.InstanceGuid)));
                        }
                    }
                }

                foreach (DeviceInstance deviceInstance in directInput.GetDevices(SharpDX.DirectInput.DeviceType.Joystick, DeviceEnumerationFlags.AllDevices))
                {
                    ulong Data1 = BitConverter.ToUInt32(deviceInstance.ProductGuid.ToByteArray(), 0);
                    if (!deviceIds.Exists(x => x.Contains(Data1.ToString("X"))))
                    {
                        if (prevJoysticks.Exists(x => x.dJoystick.Information.InstanceGuid == deviceInstance.InstanceGuid))
                        {
                            joysticks.Add(prevJoysticks.Find(x => x.dJoystick.Information.InstanceGuid == deviceInstance.InstanceGuid));
                        }
                        else
                        {
                            joysticks.Add(new FRCJoystick(new Joystick(directInput, deviceInstance.InstanceGuid)));
                        }
                    }
                }
            }

            Controller[] controllers = new[] { new Controller(UserIndex.One), new Controller(UserIndex.Two), new Controller(UserIndex.Three), new Controller(UserIndex.Four) };
            foreach (Controller c in controllers)
            {
                if (c.IsConnected)
                {
                    joysticks.Add(new FRCJoystick(c));
                }
            }

            lstJoystick.ItemsSource = joysticks;
            updateJoystickIndeces();
        }

        private List<string> GetXInputCapableDevices()
        {
            List<string> deviceIds = new List<string>();

            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\cimv2", "SELECT * FROM Win32_PnPEntity");
                foreach (ManagementObject queryObj in searcher.Get())
                {

                    if (queryObj["DeviceID"] != null)
                    {
                        string pGuid = "";
                        string s = queryObj["DeviceID"].ToString();
                        if (s.Contains("IG_"))
                        {
                            string[] sArr = s.Split(new string[] { "\\", "&", "_" }, StringSplitOptions.RemoveEmptyEntries);
                            if (sArr.Length >= 4)
                            {
                                pGuid = sArr[4].TrimStart(new Char[] { '0' }) + sArr[2];
                            }
                        }
                        else
                            continue;
                        deviceIds.Add(pGuid);
                    }
                }
            }
            catch (ManagementException e)
            {
                Console.WriteLine("An error occurred while querying for WMI data: " + e.Message);
            }
            return deviceIds;
        }

        private void moveJoystick(bool up)
        {
            if (lstJoystick.SelectedIndex >= 0)
            {
                FRCJoystick f = (FRCJoystick)lstJoystick.SelectedItem;
                int pIdx = lstJoystick.SelectedIndex;
                lock (joystickLock)
                {
                    joysticks.RemoveAt(up ? pIdx-- : pIdx++);
                    pIdx = pIdx < 0 ? 0 : pIdx;
                    pIdx = pIdx >= joysticks.Count ? joysticks.Count : pIdx;
                    joysticks.Insert(pIdx, f);
                }
            }
            updateJoystickIndeces();
        }

        private void updateJoystickIndeces()
        {
            foreach (FRCJoystick f in lstJoystick.Items)
            {
                f.Index = lstJoystick.Items.IndexOf(f);
            }
            lstJoystick.Items.Refresh();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var sWidth = SystemParameters.VirtualScreenWidth;
            var sHeight = SystemParameters.VirtualScreenHeight;
            this.MaxHeight = this.MinHeight = this.Height = sHeight - DRIVER_STATION_HEIGHT;
            this.MaxWidth = this.MinWidth = this.Width = sWidth;
            Logic.Move();
            oscSender.RunWorkerAsync();
            oscReceiver.RunWorkerAsync();

            this.Activate();
            this.Focus();
        }

        private void oscSenderWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            UDPSender udpSender = null;
            try
            {
                udpSender = new UDPSender(ROBOT_IP, AUTO_DATA_PORT);
            }
            catch (Exception)
            {
                MessageBox.Show("Error creating UDP Sender. Make sure your IP Address and port settings are correct!", "Dashboard", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }

            while (runThread)
            {
                int autoStartPositionIndex = -1;
                int autoModeIndex = -1;
                Dispatcher.Invoke(() => autoStartPositionIndex = cboAutoStartSelection.SelectedIndex);
                Dispatcher.Invoke(() => autoStartPositionIndex = cboAutoMode.SelectedIndex);

                var message = new OscMessage("/AutoData",
                    (int)autoStartPositionIndex,
                    (int)autoModeIndex);
                udpSender.Send(message);

                Thread.Sleep(100);
            }

            udpSender.Close();
        }

        private void oscSenderWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            exitTriggered(1);
        }

        private void oscReceiverWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var udpListener = new UDPListener(AUTO_DATA_PORT);
            OscMessage messageReceived = null;
            bool prevEnabled = false;
            CKLogHandler ckLogger = null;

            while (runThread)
            {
                messageReceived = (OscMessage)udpListener.Receive();
                if (messageReceived != null)
                {
                    switch (messageReceived.Address)
                    {
                        case "/LogData":
                            try
                            {

                                try
                                {
                                    string hasCubeString = messageReceived.Arguments.First(s => s.ToString().ToLower().Contains("hascube")).ToString();
                                    bool hasCube = hasCubeString.Split(':')[1].Split(';')[0].ToLower().Equals("true");
                                    Dispatcher.Invoke(() => ledHasCube.IsActive = hasCube);

                                    string hasArmFaultString = messageReceived.Arguments.First(s => s.ToString().ToLower().Contains("armfault")).ToString();
                                    bool hasArmFault = hasArmFaultString.Split(':')[1].Split(';')[0].ToLower().Equals("true");
                                    Dispatcher.Invoke(() => ledArmFault.IsActive = hasArmFault);

                                    string hasElevatorFaultString = messageReceived.Arguments.First(s => s.ToString().ToLower().Contains("elevatorfault")).ToString();
                                    bool hasElevatorFault = hasElevatorFaultString.Split(':')[1].Split(';')[0].ToLower().Equals("true");
                                    Dispatcher.Invoke(() => ledElevatorFault.IsActive = hasElevatorFault);

                                    string hasClimberFaultString = messageReceived.Arguments.First(s => s.ToString().ToLower().Contains("climberfault")).ToString();
                                    bool hasClimberFault = hasClimberFaultString.Split(':')[1].Split(';')[0].ToLower().Equals("true");
                                    Dispatcher.Invoke(() => ledClimberFault.IsActive = hasClimberFault);
                                }
                                catch (Exception ex)
                                {

                                }

                                string hasEnabledString = messageReceived.Arguments.First(s => s.ToString().ToLower().Contains("enabled")).ToString();
                                Enabled = hasEnabledString.Split(':')[1].Split(';')[0].ToLower().Equals("true");

                                if (prevEnabled != Enabled && Enabled)
                                {
                                    if (ckLogger == null)
                                    {
                                        ckLogger = new CKLogHandler(@"C:\Logs\OSCLog_" + GetTimestamp(DateTime.Now) + ".csv");
                                        ckLogger.StartLogging();
                                    }
                                    prevEnabled = Enabled;
                                }

                                if (ckLogger != null)
                                {
                                    try
                                    {
                                        List<string> listString;
                                        if (!ckLogger.HeadersWritten)
                                        {
                                            var ls = new List<object>();
                                            ls.AddRange(messageReceived.Arguments);
                                            listString = ls.Select(s => s.ToString().Split(':')[0]).ToList();
                                            ckLogger.WriteCSVHeaders(listString);
                                        }

                                        listString = messageReceived.Arguments.Select(s => s.ToString().Split(':')[1].Split(';')[0]).ToList();
                                        ckLogger.LogData(listString);
                                    } catch (Exception ex)
                                    {

                                    }
                                }

                                if (prevEnabled != Enabled && !Enabled)
                                {
                                    if (ckLogger != null)
                                        ckLogger.StopLogging();
                                    ckLogger = null;
                                    prevEnabled = Enabled;
                                }
                            }
                            catch (Exception) { }
                            break;
                        default:
                            break;
                    }
                }

                Thread.Sleep(5);
            }

            udpListener.Close();
        }

        private void oscReceiverWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            exitTriggered(1);
        }

        private void exitTriggered(int exitCode)
        {
            try
            {

            } catch (Exception)
            {

            }

            Environment.Exit(exitCode);
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            runThread = false;
            int waitCounter = 0;
            while ((oscReceiver.IsBusy || oscSender.IsBusy) && waitCounter++ < 5)
                Thread.Sleep(1000);
        }

        public static String GetTimestamp(DateTime value)
        {
            return value.ToString("yyyy-MM-dd-HH-mm-ss-ffff");
        }

        private void cmdCalibrate_Click(object sender, RoutedEventArgs e)
        {

        }

        private void cmdRefresh_Click(object sender, RoutedEventArgs e)
        {
            enumerateJoysticks();
        }

        private void cmdUp_Click(object sender, RoutedEventArgs e)
        {
            moveJoystick(true);
        }

        private void cmdDown_Click(object sender, RoutedEventArgs e)
        {
            moveJoystick(false);
        }
    }

    static class Logic
    {
        [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
        public static extern IntPtr SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int x, int Y, int cx, int cy, int wFlags);

        public static void Move()
        {
            const short SWP_NOSIZE = 1;
            const short SWP_NOZORDER = 0X4;
            const int SWP_SHOWWINDOW = 0x0040;

            Process[] processes = Process.GetProcesses(".");
            foreach (var process in processes)
            {
                IntPtr handle = process.MainWindowHandle;
                if (handle != IntPtr.Zero)
                {
                    SetWindowPos(handle, 0, 0, 0, 0, 0, SWP_NOZORDER | SWP_NOSIZE | SWP_SHOWWINDOW);
                }
            }
        }
    }
}
