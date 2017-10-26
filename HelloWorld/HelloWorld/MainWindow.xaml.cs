﻿using System;
using System.Windows;
using System.Windows.Media;
using System.Threading;
using System.Drawing;

namespace HelloWorld
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Thread processingThread;
        private PXCMSenseManager senseManager;
        private PXCMHandModule hand;
        private PXCMHandConfiguration handConfig;
        private PXCMHandData handData;
        private PXCMHandData.GestureData gestureData;
        //private bool handWaving;
        private bool handTrigger;
        private int msgTimer;
        private string gestureName;

        public MainWindow()
        {
            InitializeComponent();
            //handWaving = false;
            handTrigger = false;
            msgTimer = 0;
            gestureName = "";
            // Instantiate and initialize the SenseManager
            senseManager = PXCMSenseManager.CreateInstance();
            senseManager.EnableStream(PXCMCapture.StreamType.STREAM_TYPE_COLOR, 640, 480, 30);
            senseManager.EnableHand();
            senseManager.Init();

            // Configure the Hand Module
            hand = senseManager.QueryHand();
            handConfig = hand.CreateActiveConfiguration();
            //handConfig.EnableGesture("wave");
            String[] enabledGestures = { "thumb_down", "thumb_up", "swipe_left", "swipe_right" };
            foreach (String gesture in enabledGestures)
            {
                if (!handConfig.IsGestureEnabled(gesture))
                {
                    handConfig.EnableGesture(gesture);
                }
            }
            handConfig.EnableAllAlerts();
            handConfig.ApplyChanges();

            // Start the worker thread
            processingThread = new Thread(new ThreadStart(ProcessingThread));
            processingThread.Start();
        }
        
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            lblMessage.Content = "(thumb_down, thumb_up, swipe_left, swipe_right, please choose one gesture)";
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            processingThread.Abort();
            if (handData != null) handData.Dispose();
            handConfig.Dispose();
            senseManager.Dispose();
        }

        private void ProcessingThread()
        {
            // Start AcquireFrame/ReleaseFrame loop
            while (senseManager.AcquireFrame(true) >= pxcmStatus.PXCM_STATUS_NO_ERROR)
            {
                PXCMCapture.Sample sample = senseManager.QuerySample();
                Bitmap colorBitmap;
                PXCMImage.ImageData colorData;

                // Get color image data
                sample.color.AcquireAccess(PXCMImage.Access.ACCESS_READ, PXCMImage.PixelFormat.PIXEL_FORMAT_RGB24, out colorData);
                colorBitmap = colorData.ToBitmap(0, sample.color.info.width, sample.color.info.height);

                // Retrieve gesture data
                hand = senseManager.QueryHand();

                if (hand != null)
                {
                    // Retrieve the most recent processed data
                    handData = hand.CreateOutput();
                    handData.Update();
                    //handWaving = handData.IsGestureFired("wave", out gestureData);
                    int firedGesturesNumber = handData.QueryFiredGesturesNumber();
                    for (int i = 0; i < firedGesturesNumber; i++)
                    {
                        //PXCMHandData.GestureData gestureData;

                        if (handData.QueryFiredGestureData(i, out gestureData) == pxcmStatus.PXCM_STATUS_NO_ERROR)
                        {
                            // Getting the gesture name

                            gestureName = gestureData.name;
                            // Getting the gesture state
                            //PXCMHandData.GestureStateType gestureState = gestureData.state;

                            //Console.WriteLine("Gesture \"{0}\" was detected. State:{1}", gestureName, gestureState);
                            //Console.WriteLine("--------------------");
                        }

                    }
                }
                                
                // Update the user interface
                UpdateUI(colorBitmap);

                // Release the frame
                if (handData != null) handData.Dispose();
                colorBitmap.Dispose();
                sample.color.ReleaseAccess(colorData);
                senseManager.ReleaseFrame();
            }
        }

        private void UpdateUI(Bitmap bitmap)
        {
            this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(delegate()
            {
                if (bitmap != null)
                {
                    // Mirror the color stream Image control
                    imgColorStream.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
                    ScaleTransform mainTransform = new ScaleTransform();
                    mainTransform.ScaleX = -1;
                    mainTransform.ScaleY = 1;
                    imgColorStream.RenderTransform = mainTransform;
                    
                    // Display the color stream
                    imgColorStream.Source = ConvertBitmap.BitmapToBitmapSource(bitmap);

                    // Update the screen message
                    if (gestureName != null)
                    {
                        lblMessage.Content = "the gesture name is " + gestureName;
                        handTrigger = true;
                    }

                    // Reset the screen message after ~50 frames
                    if (handTrigger)
                    {
                        msgTimer++;

                        if (msgTimer >= 200)
                        {
                            lblMessage.Content = "(thumb_down, thumb_up, swipe_left, swipe_right, please choose one gesture)";
                            msgTimer = 0;
                            handTrigger = false;
                            gestureName = "";
                        }
                    }
                }
            }));
        }
    }
}
