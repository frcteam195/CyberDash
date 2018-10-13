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

        private readonly int PORT = 5805;
        private readonly string ROBOT_IP = "10.1.95.2";
        private bool runThread = true;

        private DrawingImageProvider _bitmapSourceProvider;
        private MediaConnector _connector;
        private MJPEGConnection _mjpegConnection;

        private static readonly string CAMERA_STR = "http://10.1.95.11/axis-cgi/mjpg/video.cgi?fps=20&compression=85&resolution=640x480";
        private static readonly string CAMERA_USERNAME = "FRC";
        private static readonly string CAMERA_PASSWORD = "FRC";

        public MainWindow()
        {
            InitializeComponent();

            oscSender.DoWork += oscSenderWorker_DoWork;
            oscSender.RunWorkerCompleted += oscSenderWorker_RunWorkerCompleted;

            oscReceiver.DoWork += oscReceiverWorker_DoWork;
            oscReceiver.RunWorkerCompleted += oscReceiverWorker_RunWorkerCompleted;

            _connector = new MediaConnector();
            _bitmapSourceProvider = new DrawingImageProvider();
            cameraViewer.SetImageProvider(_bitmapSourceProvider);
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
                udpSender = new UDPSender(ROBOT_IP, PORT);
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
            var udpListener = new UDPListener(PORT);
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
                                bool enabled = hasEnabledString.Split(':')[1].Split(';')[0].ToLower().Equals("true");

                                if (prevEnabled != enabled && enabled)
                                {
                                    if (ckLogger == null)
                                    {
                                        ckLogger = new CKLogHandler(@"C:\Logs\OSCLog_" + GetTimestamp(DateTime.Now) + ".csv");
                                        ckLogger.StartLogging();
                                    }
                                    prevEnabled = enabled;
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

                                if (prevEnabled != enabled && !enabled)
                                {
                                    if (ckLogger != null)
                                        ckLogger.StopLogging();
                                    ckLogger = null;
                                    prevEnabled = enabled;
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
