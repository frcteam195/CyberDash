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
using Ozeki.Media;
using SharpDX.DirectInput;
using SharpDX.XInput;
using CyberDash.Utilities;

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

        private readonly int AUTO_DATA_PORT = 5805;
        private readonly int JOYSTICK_DATA_PORT = 5806;
        private readonly string ROBOT_IP = "10.1.95.2";
        private bool runThread = true;

        private DrawingImageProvider _bitmapSourceProvider;
        private MediaConnector _connector;
        private MJPEGConnection _mjpegConnection;

        private static readonly string CAMERA_STR = "http://10.1.95.11/axis-cgi/mjpg/video.cgi?fps=20&compression=85&resolution=640x480";
        private static readonly string CAMERA_USERNAME = "FRC";
        private static readonly string CAMERA_PASSWORD = "FRC";
        private List<FRCJoystick> joysticks = new List<FRCJoystick>();
        private object joystickLock = new object();

        private bool Enabled { get; set; } = false;

        private Thread joystickCaptureThread;

        public MainWindow()
        {
            InitializeComponent();

            enumerateJoysticks();

            oscSender.DoWork += oscSenderWorker_DoWork;
            oscSender.RunWorkerCompleted += oscSenderWorker_RunWorkerCompleted;

            oscReceiver.DoWork += oscReceiverWorker_DoWork;
            oscReceiver.RunWorkerCompleted += oscReceiverWorker_RunWorkerCompleted;

            _connector = new MediaConnector();
            _bitmapSourceProvider = new DrawingImageProvider();
            cameraViewer.SetImageProvider(_bitmapSourceProvider);

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
                            JoystickState js = j.dJoystick.GetCurrentState();
                            List<object> lO = new List<object>();
                            lO.Add(js.X);
                            lO.Add(js.Y);
                            lO.Add(js.Z);
                            lO.Add(js.RotationX);
                            lO.Add(js.RotationY);
                            lO.Add(js.RotationZ);
                            lO.Add(convertBoolArrToLong(js.Buttons));
                            lO.Add(js.PointOfViewControllers[0]);
                            lO.Add((long)DateTime.UtcNow.ToUniversalTime().Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds);
                            lock (lockObject)
                            {
                                messageList.Add(new OscMessage("/Joysticks/" + j.Index, lO.ToArray()));
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

            System.Windows.Threading.DispatcherTimer refreshViewTimer = new System.Windows.Threading.DispatcherTimer();
            refreshViewTimer.Tick += refreshViewTimer_Tick;
            refreshViewTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            refreshViewTimer.Start();

            System.Windows.Threading.DispatcherTimer refocusTimer = new System.Windows.Threading.DispatcherTimer();
            refocusTimer.Tick += refocusTimer_Tick;
            refocusTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            refocusTimer.Start();
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
            lock (joystickLock)
            {
                joysticks.Clear();
                foreach (DeviceInstance deviceInstance in directInput.GetDevices(SharpDX.DirectInput.DeviceType.Gamepad, DeviceEnumerationFlags.AllDevices))
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

                foreach (DeviceInstance deviceInstance in directInput.GetDevices(SharpDX.DirectInput.DeviceType.Joystick, DeviceEnumerationFlags.AllDevices))
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

            lstJoystick.ItemsSource = joysticks;
            updateJoystickIndeces();
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

        private void connectCamera()
        {
            if (_mjpegConnection != null)
                _mjpegConnection.Disconnect();

            var config = new OzConf_P_MJPEGClient(CAMERA_STR, CAMERA_USERNAME, CAMERA_PASSWORD);
            _mjpegConnection = new MJPEGConnection(config);
            //_mjpegConnection.Connect();
            //_connector.Connect(_mjpegConnection.VideoChannel, _bitmapSourceProvider);
            //cameraViewer.Start();
        }

        private void disconnectCamera()
        {
            if (_mjpegConnection == null) return;
            _mjpegConnection.Disconnect();
            cameraViewer.Stop();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var sWidth = SystemParameters.VirtualScreenWidth;
            var sHeight = SystemParameters.VirtualScreenHeight;
            this.MaxHeight = this.MinHeight = this.Height = sHeight - DRIVER_STATION_HEIGHT;
            this.MaxWidth = this.MinWidth = this.Width = sWidth;
            Logic.Move();
           //oscSender.RunWorkerAsync();
            oscReceiver.RunWorkerAsync();

            try
            {
                connectCamera();
            }
            catch (Exception)
            {

            }
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
                int autoIndex = 0;
                Dispatcher.Invoke(() => autoIndex = cboAutoStartSelection.SelectedIndex);

                var message = new OscMessage("/AutoData",
                    (int)autoIndex);
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
                disconnectCamera();
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
