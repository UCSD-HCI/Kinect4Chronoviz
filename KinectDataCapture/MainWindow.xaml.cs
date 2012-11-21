using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Kinect;
using System.Threading;
using System.ComponentModel;


namespace KinectDataCapture
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        KinectSensor kinectSensor;
        AudioManager audioManager = null;

        int sampleRate;
        DateTime lastTimeDepth;
        DateTime lastTimeSkeleton;
        int depthSampleRate = 500;
        int skeletonSampleRate = 500;

        public BitmapSource curDepthImage;
        public DepthImageFrame curDepthPlanarImage;
        public CoordinateMapper coordMap;
        public DepthImageFormat depthImageFormat = DepthImageFormat.Resolution320x240Fps30;

        public string DeviceId;
        private LogFileManager logger;
        /*FaceDetection faceDetection; */

        /* --- AUDIO RELATED STATE --- */
        private string wavFilename;
        private Stream audioStream;
        private object lockObj = new object();
        KinectAudioSource audioSource;
        double curAudioAngle = 0;
        double curAudioConfidence = 0;
        int curNumPeople;

        int totalFrames = 0;
        int lastFrames = 0;
        DateTime lastTime = DateTime.MaxValue;

        int colorFrameSaveRate = 100; //milliseconds
        DateTime lastColorFrameSaveTime;
        int colorImageNum = 0;

        int faceDetectionSampleRate = 500; //milliseconds
        DateTime lastFaceDetectionSample;

        int velocitySampleRate = 500;  //milliseconds
        DateTime lastVelocitySample;

        LoggerQueue loggerQueue;
        //VideoLogger videoLog;

        /* -- FACE TRACKING STATE --- */
            bool recordHeadPose = true;
            bool recordColorImages = true;
            bool recordDepthImages = true;


        FaceLogger Faces;

        public SkeletonTrackingMode skeletonTrackingMode { get; set; }

        string curDir;


        Brush[] skeletonBrushes = new Brush[] { Brushes.Indigo, Brushes.DodgerBlue, Brushes.Purple, Brushes.Pink, Brushes.Goldenrod, Brushes.Crimson };

        const int RED_IDX = 2;
        const int GREEN_IDX = 1;
        const int BLUE_IDX = 0;
        public static byte[] depthFrame32 = new byte[320 * 240 * 4];


        Dictionary<JointType, Brush> jointColors = new Dictionary<JointType, Brush>() { 
            {JointType.HipCenter, new SolidColorBrush(Color.FromRgb(169, 176, 155))},
            {JointType.Spine, new SolidColorBrush(Color.FromRgb(169, 176, 155))},
            {JointType.ShoulderCenter, new SolidColorBrush(Color.FromRgb(168, 230, 29))},
            {JointType.Head, new SolidColorBrush(Color.FromRgb(200, 0,   0))},
            {JointType.ShoulderLeft, new SolidColorBrush(Color.FromRgb(79,  84,  33))},
            {JointType.ElbowLeft, new SolidColorBrush(Color.FromRgb(84,  33,  42))},
            {JointType.WristLeft, new SolidColorBrush(Color.FromRgb(255, 126, 0))},
            {JointType.HandLeft, new SolidColorBrush(Color.FromRgb(215,  86, 0))},
            {JointType.ShoulderRight, new SolidColorBrush(Color.FromRgb(33,  79,  84))},
            {JointType.ElbowRight, new SolidColorBrush(Color.FromRgb(33,  33,  84))},
            {JointType.WristRight, new SolidColorBrush(Color.FromRgb(77,  109, 243))},
            {JointType.HandRight, new SolidColorBrush(Color.FromRgb(37,   69, 243))},
            {JointType.HipLeft, new SolidColorBrush(Color.FromRgb(77,  109, 243))},
            {JointType.KneeLeft, new SolidColorBrush(Color.FromRgb(69,  33,  84))},
            {JointType.AnkleLeft, new SolidColorBrush(Color.FromRgb(229, 170, 122))},
            {JointType.FootLeft, new SolidColorBrush(Color.FromRgb(255, 126, 0))},
            {JointType.HipRight, new SolidColorBrush(Color.FromRgb(181, 165, 213))},
            {JointType.KneeRight, new SolidColorBrush(Color.FromRgb(71, 222,  76))},
            {JointType.AnkleRight, new SolidColorBrush(Color.FromRgb(245, 228, 156))},
            {JointType.FootRight, new SolidColorBrush(Color.FromRgb(77,  109, 243))}
        };

        bool logging = false;


        Dictionary<int, Dictionary<Joint, Point3f>> curBodyTrackingState;
        Dictionary<int, Dictionary<Joint, Point3f>> lastBodyTrackingState = null;

        struct SkeletonPosition {
            public Point3f hipCenter;
            public Point3f spine;
            public Point3f head;
            public Point3f shoulderCenter;
            public Point3f shoulderLeft;
            public Point3f shoulderRight;
            public Point3f elbowLeft;
            public Point3f elbowRight;
            public Point3f wristLeft;
            public Point3f wristRight;
            public Point3f handLeft;
            public Point3f handRight;
            public Point3f hipLeft;
            public Point3f hipRight;
            public Point3f kneeLeft;
            public Point3f kneeRight;
            public Point3f ankleLeft;
            public Point3f ankleRight;
            public Point3f footLeft;
            public Point3f footRight;
        }

        struct Point3f{
            public float X;
            public float Y;
            public float Z;
            public bool tracked;
            public DateTime time;
            public string toString() {
                return "(" + X + "," + Y + "," + Z + ")";
            }
        }

        Point3f skeletonAHead;
        Point3f skeletonBHead;
        Point3f skeletonASpine;
        Point3f skeletonBSpine;

        ImageBrush recordingBrush;

        int framesSaved = 0;

        private const string RecognizerId = "SR_MS_en-US_Kinect_10.0";


        public MainWindow()
        {
            InitializeComponent();
        } 

        private void Window_Loaded(object sender, EventArgs e)
        {
            DisableOptionsForKinect();
            updateAppStatus("Window loaded");

            // Create recording ImageBrush
            recordingBrush = new ImageBrush();

            recordingBrush.ImageSource =
                new BitmapImage(new Uri(@"Recording.jpg", UriKind.Relative));

 

            // Walk through KinectSensors to find the first one with a Connected status
            var availableKinects = (from k in KinectSensor.KinectSensors
                                               where k.Status == KinectStatus.Connected
                                               select k);
            foreach (var kinect in availableKinects)
            {
                kinectChooser.Items.Add(kinect.DeviceConnectionId.ToString());
                if (kinectChooser.SelectedValue == null)
                {
                    kinectChooser.SelectedIndex = 0;
                }
            }
            


        }

        private void setNearMode(object sender, RoutedEventArgs e)
        {
            if (kinectSensor != null)
            {
                if (chkNearMode.IsChecked == true)
                {
                    kinectSensor.DepthStream.Range = DepthRange.Near;
                    updateAppStatus("Setting Kinect to Near Mode");
                }
                else
                {
                    kinectSensor.DepthStream.Range = DepthRange.Default;
                    updateAppStatus("Setting Kinect to Default Mode");
                }
            }
            else
            {
                chkNearMode.IsChecked = false;  
            }
        }
        private void setSeatedMode(object sender, RoutedEventArgs e)
        {
            if (kinectSensor != null)
            {
                if (chkSeatedMode.IsChecked == true)
                {
                    kinectSensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                    updateAppStatus("Setting Skeleton to Seated Mode");
                    skeletonTrackingMode = SkeletonTrackingMode.Seated;

                }
                else
                {
                    kinectSensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;
                    updateAppStatus("Setting Skeleton to Default Mode");
                    skeletonTrackingMode = SkeletonTrackingMode.Default;
                }
            }
            else
            {
                chkSeatedMode.IsChecked = false;
            }
        }
        private void setRecordColorImages(object sender, RoutedEventArgs e)
        {
            if (!logging)
            {
                if (recordColorChk.IsChecked == true)
                {
                    updateAppStatus("Enabling recording of color images.");
                    recordColorImages = true;
                }
                else
                {
                    updateAppStatus("Disabling recording of color images.");
                    recordColorImages = false;
                }
            }
        }
        private void setRecordDepthImages(object sender, RoutedEventArgs e)
        {
            if (!logging)
            {
                if (recordDepthChk.IsChecked == true)
                {
                    updateAppStatus("Enabling recording of depth images.");
                    recordDepthImages = true;
                }
                else
                {
                    updateAppStatus("Disabling recording of depth images.");
                    recordDepthImages = false;
                }
            }
        }
        private void setRecordHeadPose(object sender, RoutedEventArgs e)
        {
            if (!logging)
            {
                if (recordHeadChk.IsChecked == true)
                {
                    updateAppStatus("Enabling recording of head pose.");
                    recordHeadPose = true;
                }
                else
                {
                    updateAppStatus("Disabling recording of head pose.");
                    recordHeadPose = false;
                }
            }
        }
        private void selectKinect_Click(object sender, RoutedEventArgs e)
        {
            string kinectId = kinectChooser.SelectedValue.ToString();
            KinectSensor firstKinect = null;
            var availableKinects = (from k in KinectSensor.KinectSensors
                                    where k.Status == KinectStatus.Connected
                                    select k);
            foreach (var kinect in availableKinects)
            {
                if (kinect.DeviceConnectionId == kinectId)
                {
                    firstKinect = kinect;
                    DeviceId = kinectId;
                }
            }

            if (firstKinect != null)
                SetNewKinect(firstKinect);

            KinectSensor.KinectSensors.StatusChanged += KinectSensors_StatusChanged;

            EnableOptionsForKinect();
        }

        private void DisableOptionsForKinect()
        {
            chkNearMode.IsEnabled = false;
            chkSeatedMode.IsEnabled = false;
            recordDepthChk.IsEnabled = false;
            recordColorChk.IsEnabled = false;
            recordHeadChk.IsEnabled = false;
        }
        private void EnableOptionsForKinect()
        {
            chkNearMode.IsEnabled = true;
            chkSeatedMode.IsEnabled = true;
            recordDepthChk.IsEnabled = true;
            recordColorChk.IsEnabled = true;
            recordHeadChk.IsEnabled = true;
        }

        private void SetNewKinect(KinectSensor newKinect)
        {
            if (kinectSensor != newKinect)
            {
                if (kinectSensor != null)
                    StopKinect(kinectSensor);

                if (newKinect != null)
                {
                    kinectSensor = newKinect;
                    OpenKinect(newKinect);
                }
            }

            
        }

        private void OpenKinect(KinectSensor newKinect)
        {
            updateAppStatus("Creating audio manager");
            //audioManager = new AudioManager(this);
            audioSource = CreateAudioSource();

            updateAppStatus("Initializing video streams");
            initializeVideoStreams();

            updateAppStatus("Initializing Face Tracking");
            Faces = new FaceLogger(newKinect);

            updateAppStatus("Ready to capture");
            newKinect.Start();
            audioStream = audioSource.Start();
        }

        private void StopKinect(KinectSensor oldKinect)
        {
            oldKinect.Stop();
        }

        void KinectSensors_StatusChanged(object sender, StatusChangedEventArgs e)
        {
            if (e.Sensor != null && e.Status == KinectStatus.Connected)
            {
                SetNewKinect(e.Sensor);
            }
            else if (e.Sensor == kinectSensor)
            {
                // The current Kinect isn't connected anymore
                SetNewKinect(null);
            }
        }
        private void startButton_Click(object sender, RoutedEventArgs e)
        {

            if (kinectSensor == null)
            {
                updateAppStatus("Please select a kinect device first.");
                return;
            }
            updateAppStatus("Capturing data");

            //should make capture settings on UI non-modifyable after this point
            if (!logging)
            {
                updateAppStatus("Starting logging");
                resetFramesLogged();

                //Create a log file manager
                logger = new LogFileManager(kinectSensor.DeviceConnectionId, FilePrefixTbx.Text);


                //Start recording audio                
                wavFilename = curDir + "\\audio.wav";
                logAudioRecording("audio.wav");
                Thread thread = new Thread(new ThreadStart(RecordKinectAudio));
                thread.Priority = ThreadPriority.Highest;
                thread.Start();

                //Start recording video
                //videoLog = new VideoLogger("test.avi");


                //Update UI
                startButton.Content = "Stop Logging";
                nofFrames.Content = "Frames loggged: ";
                logging = true;
            }
            else {
                updateAppStatus("\nStopped logging");
                
                //moveImageInfoFile();

                if (loggerQueue != null)
                {
                    loggerQueue.endLogging();
                    loggerQueue = null;
                }


                //Update UI
                startButton.Content = "Start Logging";
                logging = false;
                IsAudioRecording = false;
            }
        }
        /*
        private void moveImageInfoFile()
        {
            string sourceFile = System.IO.Path.Combine(curDir, colorImagesFileName);
            string destFile;
            if (recordDepthImages)
            {
                destFile = curDir + "\\frames\\depth\\depth" + colorImagesFileName;
                System.IO.File.Copy(sourceFile, destFile, true);
            }
            if (recordColorImages)
            {
                destFile = curDir + "\\frames\\rgb\\rgb" + colorImagesFileName;
                System.IO.File.Copy(sourceFile, destFile, true);
            }
        }
        */


        public void initializeVideoStreams()
        {
            kinectSensor.ColorStream.Enable();

            
            kinectSensor.DepthStream.Enable(depthImageFormat);

            kinectSensor.SkeletonStream.Enable();

            coordMap = new CoordinateMapper(kinectSensor);

            kinectSensor.Start();

            lastTime = DateTime.Now;


            kinectSensor.DepthFrameReady += new EventHandler<DepthImageFrameReadyEventArgs>(nui_DepthFrameReady);
            //kinectSensor.SkeletonFrameReady += new EventHandler<SkeletonFrameReadyEventArgs>(nui_SkeletonFrameReady);
            kinectSensor.ColorFrameReady += new EventHandler<ColorImageFrameReadyEventArgs>(nui_ColorFrameReady);
            kinectSensor.AllFramesReady += new EventHandler<AllFramesReadyEventArgs>(nui_AllFramesReady);
            
        }

            
        // Converts a 16-bit grayscale depth frame which includes player indexes into a 32-bit frame
        // that displays different players in different colors
        byte[] convertDepthFrame(short[] depthFrame16)
        {

            Dictionary<int,int> minDepths = new Dictionary<int,int>();
            Dictionary<int,int> maxDepths = new Dictionary<int,int>();

            for (int i16 = 0, i32 = 0; i16 < depthFrame16.Length && i32 < depthFrame32.Length; i16 ++, i32 += 4)
            {
                int player = depthFrame16[i16] & DepthImageFrame.PlayerIndexBitmask;
                int realDepth = depthFrame16[i16] >> DepthImageFrame.PlayerIndexBitmaskWidth;

                if (minDepths.ContainsKey(player))
                {
                    int minDepth = minDepths[player];
                    if (minDepth > realDepth)
                    {
                        minDepths.Remove(player);
                        minDepths.Add(player, realDepth);
                    }
                }
                else {
                    minDepths.Add(player, realDepth);
                }

                if (maxDepths.ContainsKey(player))
                {
                    int maxDepth = maxDepths[player];
                    if (maxDepth < realDepth)
                    {
                        maxDepths.Remove(player);
                        maxDepths.Add(player, realDepth);
                    }
                }
                else {
                    maxDepths.Add(player, realDepth);                
                }
                

                // transform 13-bit depth information into an 8-bit intensity appropriate
                // for display (we disregard information in most significant bit)
                byte intensity = (byte)(255 - (255 * realDepth / 0x0fff));

                depthFrame32[i32 + RED_IDX] = 0;
                depthFrame32[i32 + GREEN_IDX] = 0;
                depthFrame32[i32 + BLUE_IDX] = 0;

                // choose different display colors based on player
                switch (player)
                {
                    case 0:
                        depthFrame32[i32 + RED_IDX] = (byte)(intensity / 2);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(intensity / 2);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(intensity / 2);
                        break;
                    case 1:
                        depthFrame32[i32 + RED_IDX] = intensity;
                        break;
                    case 2:
                        depthFrame32[i32 + GREEN_IDX] = intensity;
                        break;
                    case 3:
                        depthFrame32[i32 + RED_IDX] = (byte)(intensity / 4);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(intensity);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(intensity);
                        break;
                    case 4:
                        depthFrame32[i32 + RED_IDX] = (byte)(intensity);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(intensity);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(intensity / 4);
                        break;
                    case 5:
                        depthFrame32[i32 + RED_IDX] = (byte)(intensity);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(intensity / 4);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(intensity);
                        break;
                    case 6:
                        depthFrame32[i32 + RED_IDX] = (byte)(intensity / 2);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(intensity / 2);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(intensity);
                        break;
                    case 7:
                        depthFrame32[i32 + RED_IDX] = (byte)(255 - intensity);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(255 - intensity);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(255 - intensity);
                        break;
                }
            }
            if (logging) {
                //logDepthData(minDepths,maxDepths);
            }
            return depthFrame32;
        }

        void logDepthData(Dictionary<int, int> minDepths, Dictionary<int, int> maxDepths)
        {
            if (!logging)
                return;

            IDictionaryEnumerator minDepthInfo = minDepths.GetEnumerator();
            while(minDepthInfo.MoveNext()){
                String depthData = "";
                int player = (int)minDepthInfo.Entry.Key;
                depthData += minDepths[player];
                if (maxDepths.ContainsKey(player)) {
                    depthData += "," + maxDepths[player];
                }
                //logger.log(LogFileType.DepthImageFile, player, depthData);
            }
        }

        void logAudioRecordingData()
        {
            if (!logging)
                return;

            logger.log(LogFileType.AudioRecordingFile, wavFilename);
        }

        void nui_DepthFrameReady(object sender, DepthImageFrameReadyEventArgs e)
        {
            curDepthPlanarImage = e.OpenDepthImageFrame();
            
            if (curDepthPlanarImage == null)
            {
                return; 
            }

            short[] rawDepthData = new short[curDepthPlanarImage.PixelDataLength];
            curDepthPlanarImage.CopyPixelDataTo(rawDepthData);

            byte[] convertedDepthFrame = convertDepthFrame(rawDepthData);
            byte[] test = new byte[640 * 480 * 4];
            BitmapSource depthImage = BitmapSource.Create(
                curDepthPlanarImage.Width, curDepthPlanarImage.Height, 
                96, 96, PixelFormats.Bgr32, null,
                convertedDepthFrame, curDepthPlanarImage.Width * 4);
            //BitmapSource depthImage = BitmapSource.Create(
            //    640, 480,
            //    96, 96, PixelFormats.Bgr32, null,
            //    convertedDepthFrame, 640 * 4);

            curDepthImage = depthImage;

            //Save audio at each depthFrame
            string info = curAudioAngle + "," + curAudioConfidence;
            if (logging)
                logger.log(LogFileType.AudioAngleFile, info);

        }



        private Point getDisplayPosition(Joint joint)
        {
            ColorImagePoint colPoint = kinectSensor.MapSkeletonPointToColor(joint.Position, ColorImageFormat.RgbResolution640x480Fps30);
            return new Point(colPoint.X, colPoint.Y);
        }

        Polyline getBodySegment(Microsoft.Kinect.JointCollection joints, Brush brush, params JointType[] ids)
        {
            PointCollection points = new PointCollection(ids.Length);
            for (int i = 0; i < ids.Length; ++i)
            {
                points.Add(getDisplayPosition(joints[ids[i]]));
            }

            Polyline polyline = new Polyline();
            polyline.Points = points;
            polyline.Stroke = brush;
            polyline.StrokeThickness = 5;
            return polyline;
        }

        //void nui_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        void nui_AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            bool allFramesGood = false;
            // Initialize data arrays
            Skeleton[] skeletonData = new Skeleton[6];
            ColorImageFrame colorImageFrame = null;
            DepthImageFrame depthImageFrame = null;
            SkeletonFrame skeletonFrame = null;

            //Open skeleton frame
            skeletonFrame = e.OpenSkeletonFrame();
            if (skeletonFrame == null)
            {
                return;
            }
            if (recordHeadPose)
            {
                colorImageFrame = e.OpenColorImageFrame();
                depthImageFrame = e.OpenDepthImageFrame();

                if (colorImageFrame == null || depthImageFrame == null || skeletonFrame == null)
                {
                    allFramesGood = false;
                }
                else
                {
                    allFramesGood = true;
                    Faces.sendNewFrames(colorImageFrame, depthImageFrame, skeletonFrame);
                }

                
            }

            
            //Initialize variables
            int iSkeleton = 0;
            int numSkeletons = 0;
            Brush userBrush = skeletonBrushes[numSkeletons % skeletonBrushes.Length];
            Skeleton skeleton;
            Brush[] brushes = new Brush[6];
            brushes[0] = new SolidColorBrush(Color.FromRgb(255, 0, 0));
            brushes[1] = new SolidColorBrush(Color.FromRgb(0, 255, 0));
            brushes[2] = new SolidColorBrush(Color.FromRgb(64, 255, 255));
            brushes[3] = new SolidColorBrush(Color.FromRgb(255, 255, 64));
            brushes[4] = new SolidColorBrush(Color.FromRgb(255, 64, 255));
            brushes[5] = new SolidColorBrush(Color.FromRgb(128, 128, 255));

            //Clear tracking state or initialize
            if (curBodyTrackingState != null)
            {
                curBodyTrackingState.Clear();
            }
            else
            {
                curBodyTrackingState = new Dictionary<int, Dictionary<Joint, Point3f>>();
            }

            //Create Imagebrush for depth image (recording brush to save)
            ImageBrush imageBrush = new ImageBrush(curDepthImage);
            if (!logging)
            {
                depthSkelCanvas.Background = imageBrush;
            }
            else
            {
                depthSkelCanvas.Background = recordingBrush;
            }

            //Copy skeleton data
            skeletonFrame.CopySkeletonDataTo(skeletonData);

            //Clear children
            depthSkelCanvas.Children.Clear();

            //For each skeleton
            for (int i = 0; i < skeletonData.Length; i++)
            {
                //Create a dictionary of jointPositions
                skeleton = skeletonData[i];
                numSkeletons++;
                Dictionary<Joint, Point3f> jointPositions = new Dictionary<Joint, Point3f>();

                //If tracking this skeleton
                if (SkeletonTrackingState.Tracked == skeleton.TrackingState)
                {
                    //For each joint, add to joint positions and draw
                    foreach (Joint joint in skeleton.Joints)
                    {
                        if (joint.TrackingState == JointTrackingState.Tracked)
                        {
                            addJoint(jointPositions, joint);                            
                            if (!logging)
                                drawJoint(depthSkelCanvas, userBrush, joint);
                        }
                        else //Add all points so that csv lines up
                        {                                
                            addJoint(jointPositions, joint);                                
                        }
                    }
                    //Draw Stick Figure
                    if (!logging)
                    {
                        drawStickFigure(depthSkelCanvas, userBrush, skeleton, skeletonTrackingMode);
                    }
                    if (recordHeadPose && allFramesGood)
                    {
                        //Get face tracking points
                        FaceLogger.HeadPose3D headPose = Faces.getSkeletonHeadPose(skeleton, skeletonFrame.FrameNumber);
                        if (headPose.valid)
                        {
                            //add to joints
                            if (headPose.valid)
                                lblHeadPose.Content = "Head Pose: (" + Math.Round(headPose.pitch) + "," + Math.Round(headPose.roll) + "," + Math.Round(headPose.yaw) + ")";
                            else
                                lblHeadPose.Content = "Head Pose: ";
                            
                            string dataString = headPose.pitch + "," + headPose.roll + "," + headPose.yaw;
                            if (logging)
                                logger.log(LogFileType.HeadTrackFile, skeleton.TrackingId, dataString);
                            else
                            {
                                drawHeadPosition(depthSkelCanvas, userBrush, skeleton, headPose);
                            }
                        }
                    }


                    //Add the whole of joint positions to curBodyTrackingState
                    curBodyTrackingState.Add(skeleton.TrackingId, jointPositions);
                }
                iSkeleton++;
            } // for each skeleton
        
            logSkeletonJointPositions();
            //logVelocity();
        //logBodyProximity(headDistance, spineDistance);
            
    }







        private static Joint addJoint(Dictionary<Joint, Point3f> jointPositions, Joint joint)
        {
            //Add joint to JointPositions
            Point3f jointPoint = new Point3f();
            jointPoint.X = joint.Position.X;
            jointPoint.Y = joint.Position.Y;
            jointPoint.Z = joint.Position.Z;
            jointPoint.tracked = (joint.TrackingState == JointTrackingState.Tracked);
            jointPoint.time = DateTime.Now;
            jointPositions.Add(joint, jointPoint);
            return joint;
        }

        private Joint drawJoint(Canvas skeletonCanvas, Brush userBrush, Joint joint)
        {
            Point jointPos = getJointPoint(joint);
            Ellipse jointBlob = new Ellipse() { Fill = userBrush, Height = 20, Width = 20 };
            Canvas.SetLeft(jointBlob, jointPos.X - 10);
            Canvas.SetTop(jointBlob, jointPos.Y - 10);
            skeletonCanvas.Children.Add(jointBlob);
            return joint;
        }

        private void drawHeadPosition(Canvas skeletonCanvas, Brush userBrush, Skeleton skeleton, FaceLogger.HeadPose3D headPose)
        {
            Polyline figure = new Polyline();

            figure.StrokeThickness = 8;
            figure.Stroke = new SolidColorBrush(System.Windows.Media.Colors.YellowGreen);
            
            //Get head
            double radius = 150;
            Point headLocation = getJointPoint(skeleton.Joints[JointType.Head]);            
            Point PitchLine = new Point(Math.Round(headLocation.X - (headPose.yaw * radius / 90)),
                                        Math.Round(headLocation.Y - (headPose.pitch * radius / 90)));
            drawJoint(skeletonCanvas, new SolidColorBrush(System.Windows.Media.Colors.YellowGreen), skeleton.Joints[JointType.Head]);
            figure.Points.Add(headLocation);
            figure.Points.Add(PitchLine);
            skeletonCanvas.Children.Add(figure);

        }
        private void drawStickFigure(Canvas skeletonCanvas, Brush userBrush, Skeleton skeleton, SkeletonTrackingMode trackingMode)
        {
            Polyline figure;

            //Draw head and shoulders
            figure = CreateFigure(skeleton, userBrush, new[] { JointType.Head, JointType.ShoulderCenter, JointType.ShoulderLeft });
            skeletonCanvas.Children.Add(figure);
            figure = CreateFigure(skeleton, userBrush, new[] { JointType.ShoulderCenter, JointType.ShoulderRight });
            skeletonCanvas.Children.Add(figure);

            //Draw left arm
            figure = CreateFigure(skeleton, userBrush, new[] { JointType.ShoulderLeft, JointType.ElbowLeft, JointType.WristLeft, JointType.HandLeft });
            skeletonCanvas.Children.Add(figure);

            //Draw right arm
            figure = CreateFigure(skeleton, userBrush, new[] { JointType.ShoulderRight, JointType.ElbowRight, JointType.WristRight, JointType.HandRight });
            skeletonCanvas.Children.Add(figure);

            if (trackingMode != SkeletonTrackingMode.Seated)
            {
                //Draw spine
                figure = CreateFigure(skeleton, userBrush, new[] { JointType.ShoulderCenter, JointType.Spine,  JointType.HipCenter });
                skeletonCanvas.Children.Add(figure);
                figure = CreateFigure(skeleton, userBrush, new[] {JointType.ShoulderLeft, JointType.Spine, JointType.ShoulderRight });
                skeletonCanvas.Children.Add(figure);

                //Draw hip
                figure = CreateFigure(skeleton, userBrush, new[] { JointType.HipLeft, JointType.HipRight });
                skeletonCanvas.Children.Add(figure);

                //Draw left leg
                figure = CreateFigure(skeleton, userBrush, new[] { JointType.HipCenter, JointType.HipLeft, JointType.KneeLeft, JointType.AnkleLeft, JointType.FootLeft });
                skeletonCanvas.Children.Add(figure);

                //Draw right leg
                figure = CreateFigure(skeleton, userBrush, new[] { JointType.HipCenter, JointType.HipRight, JointType.KneeRight, JointType.AnkleRight, JointType.FootRight });
                skeletonCanvas.Children.Add(figure);


            }

        }
        
        void Oldnui_AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            // Initialize data arrays
            byte[] colorPixelData = new byte[kinectSensor.ColorStream.FramePixelDataLength];
            short[] depthPixelData = new short[kinectSensor.DepthStream.FramePixelDataLength];
            Skeleton[] skeletonData = new Skeleton[6];
            ColorImageFrame colorImageFrame = null;
            DepthImageFrame depthImageFrame = null;
            SkeletonFrame skeletonFrame = null;

            try
            {
                colorImageFrame = e.OpenColorImageFrame();
                depthImageFrame = e.OpenDepthImageFrame();
                skeletonFrame = e.OpenSkeletonFrame();

                if (colorImageFrame == null || depthImageFrame == null || skeletonFrame == null)
                {
                    return;
                }

                Faces.sendNewFrames(colorImageFrame, depthImageFrame, skeletonFrame);

            }
            finally
            {
                if (colorImageFrame != null)
                {
                    colorImageFrame.Dispose();
                }

                if (depthImageFrame != null)
                {
                    depthImageFrame.Dispose();
                }

                if (skeletonFrame != null)
                {
                    skeletonFrame.Dispose();
                }
            }
        }

        void logVelocity() {

            if (!logging)
                return;

            if ((DateTime.Now.Subtract(lastVelocitySample)).Milliseconds > velocitySampleRate)
            {
                lastVelocitySample = DateTime.Now;
            }
            else {
                return;
            }
            
            //case for first call to this method
            if (lastBodyTrackingState == null) {
                lastBodyTrackingState = new Dictionary<int, Dictionary<Joint, Point3f>>(curBodyTrackingState);
                return;
            }

            IDictionaryEnumerator curSkeletonData = curBodyTrackingState.GetEnumerator();

            //calculate distance between curren bodyTracking state and lastBodyTrackingState

            while (curSkeletonData.MoveNext())
            {
                string dataString = "";
                int skeletonID = (int)curSkeletonData.Entry.Key;

                //compare between the same skeleton
                if(lastBodyTrackingState.ContainsKey(skeletonID) && curBodyTrackingState.ContainsKey(skeletonID)){

                    Dictionary<Joint,Point3f> curJointPositions = curBodyTrackingState[skeletonID];
                    Dictionary<Joint, Point3f> lastJointPositions = lastBodyTrackingState[skeletonID];

                    //iterate through all joints, and compute distance if both joints are tracked
                    IDictionaryEnumerator curJoints = curJointPositions.GetEnumerator();

                    while (curJoints.MoveNext())
                    {
                        Joint curJointID = (Joint)curJoints.Entry.Key;
                        Point3f curPosition = (Point3f)curJoints.Entry.Value;

                        if(curPosition.tracked){
                            if (lastJointPositions.ContainsKey(curJointID))
                            {
                                Point3f lastPosition = (Point3f)lastJointPositions[curJointID];
                                if (lastPosition.tracked)
                                {
                                    double distance = Distance3D(lastPosition, curPosition);
                                    double velocity = Velocity(distance, lastPosition.time, curPosition.time);
                                    dataString += velocity + ",";
                                }
                                else {
                                    dataString += ",";
                                }
                            }
                            else {
                                dataString += ",";
                            }
                        }
                        else{
                            dataString += ",";
                        }
                    }
                }
                
            }
            lastBodyTrackingState.Clear();
            lastBodyTrackingState = new Dictionary<int, Dictionary<Joint, Point3f>>(curBodyTrackingState);
        }


        void logSkeletonJointPositions() {

            if (!logging)
                return;

            if (curBodyTrackingState == null)
                return;

            IDictionaryEnumerator skeletons = curBodyTrackingState.GetEnumerator();

            while(skeletons.MoveNext()){
                int skeletonID = (int)skeletons.Entry.Key;
                Dictionary<Joint, Point3f> curJointPositions = (Dictionary<Joint, Point3f>)skeletons.Entry.Value;
                IDictionaryEnumerator joints = curJointPositions.GetEnumerator();

                string dataString = "";
                while(joints.MoveNext()){
                    Joint jointID = (Joint)joints.Entry.Key;
                    Point3f position = (Point3f)joints.Entry.Value;
                    if (position.tracked)
                    {
                        dataString += position.X + "," + position.Y + "," + position.Z + ",";
                    }
                    else {
                        dataString += ",,,";
                    }
                }
            
                logger.log(LogFileType.BodyTrackFile, skeletonID, dataString);
            }
        }

        void nui_ColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {


            // 32-bit per pixel, RGBA image
            ColorImageFrame colorFrame = e.OpenColorImageFrame();
            if (colorFrame == null)
            {
                return;
            }
            byte[] Image = new byte[colorFrame.PixelDataLength];
            colorFrame.CopyPixelDataTo(Image);

            
            BitmapSource source = BitmapSource.Create(
                colorFrame.Width, colorFrame.Height, 96, 96, PixelFormats.Bgr32, null, Image, colorFrame.Width * colorFrame.BytesPerPixel);




            if (!logging)
            {
                ImageBrush imageBrush = new ImageBrush(source);
                video.Background = imageBrush;
            }
            else
            {
                video.Background = recordingBrush;
                //videoLog.AddFrame(source);
            }

            if ((DateTime.Now.Subtract(lastColorFrameSaveTime)).TotalMilliseconds > colorFrameSaveRate)
            {
                if (logging)
                {
                    addFrameLogged();
                    saveColorFrameToJpg(source);
                    saveDepthFrameToJpg(curDepthImage);

                    lastColorFrameSaveTime = DateTime.Now;
                }
            }

        }

        void saveColorFrameToJpg(BitmapSource image) {

            if ((!logging) || (!recordColorImages))
                return;

            int width = 128;
            int height = width;
            int stride = width / 8;
            byte[] pixels = new byte[height * stride];

            // Define the image palette
            BitmapPalette myPalette = BitmapPalettes.Halftone256;

            string path = logger.getDirectory() + "\\frames\\rgb";
            Directory.CreateDirectory(path);
            string fileName = colorImageNum + ".jpg";

            FileStream stream = new FileStream(path + "\\" + fileName, FileMode.Create);
            JpegBitmapEncoder encoder = new JpegBitmapEncoder();
            encoder.FlipHorizontal = true;
            encoder.FlipVertical = false;
            encoder.QualityLevel = 100;
            encoder.Frames.Add(BitmapFrame.Create(image));
            encoder.Save(stream);

            logger.log(LogFileType.ColorImageFile, fileName);
            colorImageNum++;
        
        }

        void saveDepthFrameToJpg(BitmapSource image)
        {

            if ((!logging) || (!recordDepthImages))
                return;

            int width = 128;
            int height = width;
            int stride = width / 8;
            byte[] pixels = new byte[height * stride];

            // Define the image palette
            BitmapPalette myPalette = BitmapPalettes.Halftone256;

            string path = logger.getDirectory() + "\\frames\\depth";
            Directory.CreateDirectory(path);
            string fileName = colorImageNum + ".jpg";

            FileStream stream = new FileStream(path + "\\" + fileName, FileMode.Create);
            JpegBitmapEncoder encoder = new JpegBitmapEncoder();
            encoder.FlipHorizontal = true;
            encoder.FlipVertical = false;
            encoder.QualityLevel = 100;
            encoder.Frames.Add(BitmapFrame.Create(image));
            encoder.Save(stream);

            logger.log(LogFileType.DepthImageFile, fileName);
            colorImageNum++;

        }

     


        void sampleImageData(object sender, EventArgs e) {

            //loggerQueue.addToQueue(depthTrackingFileName, player + "=" + realDepth);

        }


        private void Window_Closed(object sender, EventArgs e)
        {
            if(loggerQueue != null){
                loggerQueue.endLogging();
                }
            if (IsAudioRecording)
            {
                IsAudioRecording = false;
            }
            if (audioManager != null)
                audioManager.Stop();
            kinectSensor.Stop();
            audioSource.Stop();
            Environment.Exit(0);
        }

        public void updateAudioAngle(string angle) {
            if (logging)
            {
                logger.log(LogFileType.AudioAngleFile, angle);
            }
        }

        public void updateAudioState(string audioState) {
            if (logging)
            {
                logger.log(LogFileType.AudioStateFile, audioState);
            }
        }

        public void updateAppStatus(string status)
        {
            appStatus.Text += "\n" + status;
            if (logging) {
                logger.log(LogFileType.AppStatusFile, status);
                appStatus.ScrollToEnd();
            }
        }

        public void addFrameLogged()
        {
            framesSaved++;
            nofFrames.Content = "Frames logged:    " + framesSaved;
        }



        public void resetFramesLogged()
        {
            framesSaved=0;
            nofFrames.Content = "Frames logged:    " + framesSaved;
        }


        double Distance3D(Point3f point1, Point3f point2)
        {
            double squared1 = Math.Pow((point2.X - point1.X), 2);
            double squared2 = Math.Pow((point2.Y - point1.Y), 2);
            double squared3 = Math.Pow((point2.Z - point1.Z), 2);
            double sum = squared1 + squared2 + squared3;
            double result = Math.Sqrt(sum);
            return result;
        }


        double Velocity(double distance, DateTime time1, DateTime time2) {

                
            TimeSpan timeSpan = time2.Subtract(time1);
            float fTime = (timeSpan.Seconds * 1000) + timeSpan.Milliseconds;

            if (distance == 0)
            {
                return 0;
            }
            else
            {
                return distance / fTime;
            }
        }

        double Acceleration(double velocity, DateTime time1, DateTime time2) {
            TimeSpan timeSpan = time2.Subtract(time1);
            float fTime = (timeSpan.Seconds * 1000) + timeSpan.Milliseconds;

            return 0;
        }
        


        public void setSampleRate(int sampleRate_arg)
        {
            sampleRate = sampleRate_arg;
        }

        public int getSampleRate()
        {
            return sampleRate;
        }

        private void appStatus_Scroll(object sender, System.Windows.Controls.Primitives.ScrollEventArgs e)
        {

        }
        #region audioRecording


        private void RecordKinectAudio()
        {
            lock (lockObj)
            {
                IsAudioRecording = true;

                using (var fileStream =
                new FileStream(wavFilename, FileMode.Create))
                {
                    RecorderHelper.WriteWavFile(audioSource, fileStream, audioStream);
                }

                IsAudioRecording = false;
            }
        }

        private KinectAudioSource CreateAudioSource()
        {
            audioSource = kinectSensor.AudioSource;
            audioSource.BeamAngleMode = BeamAngleMode.Adaptive;
            audioSource.NoiseSuppression = true;
            audioSource.AutomaticGainControlEnabled = true;
            //Start recording beamchange
            audioSource.SoundSourceAngleChanged += new EventHandler<SoundSourceAngleChangedEventArgs>(audioSource_SoundAngleChanged);

            return audioSource;
        }

        private void logAudioRecording(string filename)
        {
            logger.log(LogFileType.AudioRecordingFile, filename);
        }

        void audioSource_SoundAngleChanged(object sender, SoundSourceAngleChangedEventArgs e)
        {
            curAudioAngle = e.Angle;
            curAudioConfidence = e.ConfidenceLevel;
        }


        private bool IsAudioRecording
        {
            get
            {
                return RecorderHelper.IsRecording;
            }
            set
            {
                if (RecorderHelper.IsRecording != value)
                {
                    RecorderHelper.IsRecording = value;
                }
            }
        }
        #endregion

        #region skeleton drawing
        private Polyline CreateFigure(Skeleton skeleton, Brush brush, JointType[] joints)
        {
            Polyline figure = new Polyline();

            figure.StrokeThickness = 8;
            figure.Stroke = brush;

            for (int i = 0; i < joints.Length; i++)
            {
                figure.Points.Add(getJointPoint(skeleton.Joints[joints[i]]));
            }

            return figure;
        }


        private Point getJointPoint(Joint joint)
        {

            DepthImagePoint point = coordMap.MapSkeletonPointToDepthPoint(joint.Position, depthImageFormat);
            
            point.X *= (int)this.depthSkelCanvas.ActualWidth / kinectSensor.DepthStream.FrameWidth;
            point.Y *= (int)this.depthSkelCanvas.ActualHeight / kinectSensor.DepthStream.FrameHeight;

            return new Point(point.X, point.Y);
        }
        #endregion

        #region properties

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }

        private double _beamAngle;

        public double BeamAngle
        {
            get { return _beamAngle; }
            set
            {
                _beamAngle = value;
                OnPropertyChanged("BeamAngle");
            }
        }

        #endregion

        private void appStatus_TextChanged(object sender, TextChangedEventArgs e)
        {
            appStatus.SelectionStart = appStatus.Text.Length;
            appStatus.ScrollToEnd();
        }

        private void VideoCaption_TextChanged(object sender, TextChangedEventArgs e)
        {

        }






        
    }


}
