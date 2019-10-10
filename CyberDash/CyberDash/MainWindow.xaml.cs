using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

using SharperOSC;
using CyberDash.Utilities;
using ElgatoStreamDeckSupport;
using MjpegProcessor;

//[assembly: System.Windows.Media.DisableDpiAwareness]
namespace CyberDash
{
    public partial class MainWindow : Window
    {
        private BackgroundWorker oscSender = new BackgroundWorker();
        private BackgroundWorker oscReceiver = new BackgroundWorker();

        public static readonly bool EMAIL_LOG_ENABLED = true;

        private readonly int AUTO_DATA_PORT = 5806;
        private readonly int LOG_DATA_PORT = 5805;
        private readonly string ROBOT_IP = "10.1.95.2";
        //private readonly string ROBOT_IP = "192.168.215.1";

        private bool runThread = true;

        private readonly CKStreamDeck streamDeck;

        private bool Enabled { get; set; } = false;

        private Thread cameraErrorCheckThread;

        private object dataParserLock = new object();

        private static readonly Uri LimelightFrontAddr = new Uri("http://10.1.95.11:5800");
        private static readonly Uri LimelightBackAddr = new Uri("http://10.1.95.12:5800");

        private bool camera1Error = false;
        private bool camera2Error = false;

        private MjpegDecoder mjpegParser1 = new MjpegDecoder();
        private MjpegDecoder mjpegParser2 = new MjpegDecoder();

        private TimeoutTimer heartbeatTimer = new TimeoutTimer(1);

        private object cameraLock = new object();

        public MainWindow()
        {
            InitializeComponent();

            try
            {
                //streamDeck = new CKStreamDeck(1);
            }
            catch (Exception ex)
            {

            }

            mjpegParser1.FrameReady += mjpeg1_FrameReady;
            mjpegParser1.Error += mjpeg1_Error;
            mjpegParser1.ParseStream(LimelightFrontAddr);

            mjpegParser2.FrameReady += mjpeg2_FrameReady;
            mjpegParser2.Error += mjpeg2_Error;
            mjpegParser2.ParseStream(LimelightBackAddr);

            img1Viewer.Stretch = Stretch.Fill;
            RenderOptions.SetBitmapScalingMode(img1Viewer, BitmapScalingMode.LowQuality);
            img2Viewer.Stretch = Stretch.Fill;
            RenderOptions.SetBitmapScalingMode(img2Viewer, BitmapScalingMode.LowQuality);

            oscSender.DoWork += oscSenderWorker_DoWork;
            oscSender.RunWorkerCompleted += oscSenderWorker_RunWorkerCompleted;

            oscReceiver.DoWork += oscReceiverWorker_DoWork;
            oscReceiver.RunWorkerCompleted += oscReceiverWorker_RunWorkerCompleted;

            CyberDashMainWindow.Width = SystemParameters.PrimaryScreenWidth;
            CyberDashMainWindow.Height = SystemParameters.PrimaryScreenHeight * 0.772;
            CyberDashMainWindow.Left = 0;
            CyberDashMainWindow.Top = 0;

            System.Windows.Threading.DispatcherTimer refocusTimer = new System.Windows.Threading.DispatcherTimer();
            refocusTimer.Tick += refocusTimer_Tick;
            refocusTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            refocusTimer.Start();
        }

        private void refocusTimer_Tick(object sender, EventArgs e)
        {
            if (Enabled)
            {
                this.Activate();
                this.Focus();
            }
        }


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

            cameraErrorCheckThread = new Thread(() =>
            {
                while (runThread)
                {
                    try
                    {
                        CheckStream();
                        Thread.Sleep(100);
                    }
                    catch (Exception)
                    {

                    }
                }
            });

            cameraErrorCheckThread.Start();

            oscSender.RunWorkerAsync();
            oscReceiver.RunWorkerAsync();

            this.Activate();
            this.Focus();
        }

        public void CheckStream()
        {
            if (camera1Error)
            {
                lock (cameraLock)
                {
                    mjpegParser1.ParseStream(LimelightFrontAddr);
                    camera1Error = false;
                }
            }
            if (camera2Error)
            {
                lock (cameraLock)
                {
                    mjpegParser2.ParseStream(LimelightBackAddr);
                    camera2Error = false;
                }
            }
        }

        private void mjpeg1_FrameReady(object sender, FrameReadyEventArgs e)
        {
            img1Viewer.Source = e.BitmapImage;
        }

        void mjpeg1_Error(object sender, ErrorEventArgs e)
        {
            lock (cameraLock)
            {
                mjpegParser1.StopStream();
                camera1Error = true;
            }
        }

        private void mjpeg2_FrameReady(object sender, FrameReadyEventArgs e)
        {
            img2Viewer.Source = e.BitmapImage;
        }

        void mjpeg2_Error(object sender, ErrorEventArgs e)
        {
            lock (cameraLock)
            {
                mjpegParser2.StopStream();
                camera2Error = true;
            }
        }

        private void oscSenderWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            UDPSender autoDataSender = null;
            UDPSender heartbeatDataSender = null;
            bool reinit = true;

            var heartbeatMsg = new OscMessage("/RegisterRequestor");

            while (runThread)
            {
                try
                {
                    if (autoDataSender == null || heartbeatDataSender == null || reinit)
                    {
                        try
                        {
                            try
                            {
                                autoDataSender.Close();
                            }
                            catch (Exception)
                            {

                            }
                            try
                            {
                                heartbeatDataSender.Close();
                            }
                            catch (Exception)
                            {

                            }

                            autoDataSender = new UDPSender(ROBOT_IP, AUTO_DATA_PORT);
                            heartbeatDataSender = new UDPSender(ROBOT_IP, LOG_DATA_PORT);
                            reinit = false;
                        }
                        catch (Exception)
                        {
                            Console.WriteLine("Couldn't create udp sender");
                        }
                    }

                    int autoStartPositionIndex = -1;
                    int autoModeIndex = -1;
                    Dispatcher.Invoke(() => autoStartPositionIndex = cboAutoStartSelection.SelectedIndex);
                    Dispatcher.Invoke(() => autoModeIndex = cboAutoMode.SelectedIndex);

                    var message = new OscMessage("/AutoData",
                        (int)autoStartPositionIndex,
                        (int)autoModeIndex);
                    autoDataSender.Send(message);

                    if (heartbeatTimer.isTimedOut())
                    {
                        heartbeatDataSender.Send(heartbeatMsg);
                        heartbeatTimer.reset();
                    }

                    Thread.Sleep(100);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    try
                    {
                        autoDataSender.Close();
                    }
                    catch (Exception)
                    {

                    }
                    try
                    {
                        heartbeatDataSender.Close();
                    }
                    catch (Exception)
                    {

                    }
                    reinit = true;
                }
            }

            autoDataSender.Close();
            heartbeatDataSender.Close();
        }

        private void oscSenderWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            exitTriggered(1);
        }

        private void oscReceiverWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            UDPListener udpListener = null;
            bool reinit = true;
            OscMessage messageReceived = null;
            bool prevEnabled = false;
            CKLogHandler ckLogger = null;
            while (runThread)
            {
                try {
                    if (udpListener == null || reinit)
                    {
                        try
                        {
                            udpListener = new UDPListener(LOG_DATA_PORT);
                            reinit = false;
                        }
                        catch (Exception)
                        {
                            Console.WriteLine("Couldn't create udp listener");
                        }
                    }

                    messageReceived = (OscMessage)udpListener.Receive();
                    if (messageReceived != null)
                    {
                        switch (messageReceived.Address)
                        {
                            case "/LogData":
                                try
                                {
                                    List<CyberDataItem> dataList = new List<CyberDataItem>();

                                    for (int i = 0; i < messageReceived.Arguments.Count; i+=2)
                                    {
                                        object key = messageReceived.Arguments[i];
                                        object value = messageReceived.Arguments[i+1];
                                        dataList.Add(new CyberDataItem(key.ToString(), value.ToString()));
                                    }

                                    dataList.Sort();

                                    try
                                    {
                                        CyberDataItem cdiVisionSystemFaulted = dataList.First(s => s.Key.ToLower().Contains("isvisionsystemfaulted"));
                                        bool hasVisionSystemFault = cdiVisionSystemFaulted.Value.ToLower().Equals("true");
                                        Dispatcher.InvokeAsync(() => ledVisionFault.IsActive = hasVisionSystemFault);

                                        CyberDataItem cdiTurretSystemFaulted = dataList.First(s => s.Key.ToLower().Contains("isturretfaulted"));
                                        bool hasTurretFault = cdiTurretSystemFaulted.Value.ToLower().Equals("true");
                                        Dispatcher.InvokeAsync(() => ledTurretFault.IsActive = hasTurretFault);

                                        CyberDataItem cdiElevatorFaulted = dataList.First(s => s.Key.ToLower().Contains("iselevatorfaulted"));
                                        bool hasElevatorFault = cdiElevatorFaulted.Value.ToLower().Equals("true");
                                        Dispatcher.InvokeAsync(() => ledElevatorFault.IsActive = hasElevatorFault);

                                        CyberDataItem cdiHatchArmFaulted = dataList.First(s => s.Key.ToLower().Contains("ishatcharmfaulted"));
                                        bool hasHatchArmFault = cdiHatchArmFaulted.Value.ToLower().Equals("true");
                                        Dispatcher.InvokeAsync(() => ledHatchArmFault.IsActive = hasHatchArmFault);

                                        CyberDataItem cdiDriveFaulted = dataList.First(s => s.Key.ToLower().Contains("isdrivefaulted"));
                                        bool hasDriveFault = cdiDriveFaulted.Value.ToLower().Equals("true");
                                        Dispatcher.InvokeAsync(() => ledDriveFault.IsActive = hasDriveFault);

                                        CyberDataItem cdiBallIntakeArmFaulted = dataList.First(s => s.Key.ToLower().Contains("isballintakearmfaulted"));
                                        bool hasBallIntakeArmFault = cdiBallIntakeArmFaulted.Value.ToLower().Equals("true");
                                        Dispatcher.InvokeAsync(() => ledBallIntakeArmFault.IsActive = hasBallIntakeArmFault);
                                    }
                                    catch (Exception ex)
                                    {
#if DEBUG
                                        Console.WriteLine(ex.ToString());
#endif
                                    }

                                    Enabled = dataList.First(s => s.Key.ToLower().Contains("enabled")).Value.ToLower().Equals("true");

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
                                            List<string> logDataList;
                                            if (!ckLogger.HeadersWritten)
                                            {
                                                logDataList = dataList.Select(s => s.Key).ToList();
                                                ckLogger.WriteCSVHeaders(logDataList);
                                            }

                                            logDataList = dataList.Select(s => s.Value).ToList();
                                            ckLogger.LogData(logDataList);
                                        }
                                        catch (Exception ex)
                                        {
#if DEBUG
                                            Console.WriteLine(ex.ToString());
#endif
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
                                catch (Exception ex) {
#if DEBUG
                                    Console.WriteLine(ex.ToString());
#endif
                                }
                                break;
                            default:
                                break;
                        }
                    }

                    Thread.Sleep(1);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    try
                    {
                        udpListener.Close();
                    }
                    catch (Exception)
                    {

                    }
                    reinit = true;
                }
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

            }
            catch (Exception)
            {

            }

            Environment.Exit(exitCode);
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            runThread = false;
            int waitCounter = 0;
            while ((oscReceiver.IsBusy || oscSender.IsBusy) && waitCounter++ < 3)
                Thread.Sleep(500);
        }

        public static String GetTimestamp(DateTime value)
        {
            return value.ToString("yyyy-MM-dd-HH-mm-ss-ffff");
        }

        private void CyberDashMainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) && e.Key == Key.OemPeriod)
            {
                Window_Closing(sender, null);
                exitTriggered(0);
            }

            if (e.Key == Key.R)
            {
                mjpeg1_Error(null, null);
                mjpeg2_Error(null, null);
                Console.WriteLine("Key R Pressed");
            }
        }

    }
}
