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
        public string DeviceId;
        /*FaceDetection faceDetection; */

        private string wavFilename;
        private Stream audioStream;
        private object lockObj = new object();
        KinectAudioSource audioSource;
        double curAudioAngle;
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


        FaceLogger Faces;

        string curDir;
        public const string appStatusFileName = "appStatus.txt";
        public const string audioAngleFileName = "audioAngle.txt";
        public const string audioStateFileName = "audioState.txt";
        public const string audioRecordingFilename = "audioRecording.txt";
        public const string speechRecognitionFileName = "speechRecognition.txt";
        public const string depthTrackingFileName = "_depthTracking.txt";
        public const string bodyTrackingFileName = "_bodyTracking.txt";
        public const string jointVelocityFileName = "_jointVeloicty.txt";
        public const string jointAccelerationFileName = "_jointAcceleration.txt";
        public const string twoBodyProximityFileName = "twoBodyProximity.txt";
        public const string faceTrackingDepthFileName = "faceTrackingDepth.txt";
        public const string colorImagesFileName = "colorImages.txt";

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
            //this.WindowState = System.Windows.WindowState.Maximized;
            updateAppStatus("Window loaded");

            // Create recording ImageBrush
            recordingBrush = new ImageBrush();

            recordingBrush.ImageSource =
                new BitmapImage(new Uri(@"Recording.jpg", UriKind.Relative));

            //nui = new Runtime();
            //faceDetection = new FaceDetection(this); 

            // Walk through KinectSensors to find the first one with a Connected status
            // Walk through KinectSensors to find all
            
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
                if (nearMode.IsChecked == true)
                {
                    kinectSensor.DepthStream.Range = DepthRange.Near;
                    updateAppStatus("Setting Kinect to Near Mode");
                }
                else
                {
                    kinectSensor.DepthStream.Range = DepthRange.Default;
                    updateAppStatus("Setting Kinect to Deafault Mode");
                }
            }
            else
            {
                nearMode.IsChecked = false;  
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
            Faces = new FaceLogger(newKinect, lblHeadPose);

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
            updateAppStatus("Capturing data");

            //should make capture settings on UI non-modifyable after this point
            if (!logging)
            {
                updateAppStatus("Starting logging");
                resetFramesLogged();


                getLoggingDirectory();
                //curDir = path + "\\" + DateTime.Now.Month + "-" + DateTime.Now.Day + " " + DateTime.Now.Hour + "." + DateTime.Now.Minute + "." + DateTime.Now.Millisecond;
                loggerQueue = new LoggerQueue(curDir);

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
                
                //Stop recording Video;
                //videoLog.StopRecording();

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

        private string getLoggingDirectory()
        {
            //Create parent directory if it does not exist
            string path = Environment.GetEnvironmentVariable("userprofile") + "\\Desktop\\Kinect_Logs";
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            //Get Device ID name
            int index = DeviceId.LastIndexOf("\\");
            string deviceId = DeviceId.Substring(index + 1, DeviceId.Length - index - 1);

            //Build new folder for current logging session MMDDYY HH.MM.SS.mmm
            string curDir = path + "\\" + deviceId 
                + "\\" + DateTime.Now.Month + "-" + DateTime.Now.Day + "-" + DateTime.Now.Year + " "
                + DateTime.Now.Hour + "." + DateTime.Now.Minute + "." + DateTime.Now.Second + "." + DateTime.Now.Millisecond;

            return curDir;
        }

        public void initializeVideoStreams()
        {
            kinectSensor.ColorStream.Enable();

            kinectSensor.DepthStream.Enable(DepthImageFormat.Resolution320x240Fps30);

            kinectSensor.SkeletonStream.Enable();


            kinectSensor.Start();

            lastTime = DateTime.Now;


            kinectSensor.DepthFrameReady += new EventHandler<DepthImageFrameReadyEventArgs>(nui_DepthFrameReady);
            kinectSensor.SkeletonFrameReady += new EventHandler<SkeletonFrameReadyEventArgs>(nui_SkeletonFrameReady);
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
                logDepthData(minDepths,maxDepths);
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
                loggerQueue.addToQueue(player + depthTrackingFileName, depthData);
            }
        }

        void logAudioRecordingData()
        {
            if (!logging)
                return;

            loggerQueue.addToQueue(audioRecordingFilename, wavFilename);
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

        void nui_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            //Get skeleton Frame
            SkeletonFrame skeletonFrame = e.OpenSkeletonFrame();
            if (skeletonFrame == null)
            {
                return;
            }

            int iSkeleton = 0;
            int numSkeletons = 0;
            Brush userBrush = skeletonBrushes[numSkeletons % skeletonBrushes.Length];
            Polyline figure;
            Skeleton skeleton;
            Brush[] brushes = new Brush[6];
            brushes[0] = new SolidColorBrush(Color.FromRgb(255, 0, 0));
            brushes[1] = new SolidColorBrush(Color.FromRgb(0, 255, 0));
            brushes[2] = new SolidColorBrush(Color.FromRgb(64, 255, 255));
            brushes[3] = new SolidColorBrush(Color.FromRgb(255, 255, 64));
            brushes[4] = new SolidColorBrush(Color.FromRgb(255, 64, 255));
            brushes[5] = new SolidColorBrush(Color.FromRgb(128, 128, 255));

            Point3f lastHeadPosition = new Point3f { X = 0, Y = 0, Z = 0 };
            double headDistance = -1;

            Point3f lastSpinePosition = new Point3f { X = 0, Y = 0, Z = 0 };
            double spineDistance = -1;
            
            depthSkelCanvas.Children.Clear();
            if (curBodyTrackingState != null)
            {
                curBodyTrackingState.Clear();
            }
            else { 
                curBodyTrackingState = new Dictionary<int,Dictionary<Joint,Point3f>>();
            }

            ImageBrush imageBrush = new ImageBrush(curDepthImage);
            if (!logging)
            {
                depthSkelCanvas.Background = imageBrush;
            }
            else
            {
                depthSkelCanvas.Background = recordingBrush;
            }
            Skeleton[] skeletonData = new Skeleton[6];
            skeletonFrame.CopySkeletonDataTo(skeletonData);

            if (true)
            {
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

                        //For each joint
                        foreach (Joint joint in skeleton.Joints)
                        {
                            if (joint.TrackingState == JointTrackingState.Tracked)
                            {
                                Point3f jointPoint = new Point3f();
                                jointPoint.X = joint.Position.X;
                                jointPoint.Y = joint.Position.Y;
                                jointPoint.Z = joint.Position.Z;
                                jointPoint.tracked = true;
                                jointPoint.time = DateTime.Now;
                                jointPositions.Add(joint, jointPoint);

                                if (joint.JointType == JointType.Head)
                                {
                                    headDistance = Distance3D(jointPoint, lastHeadPosition);
                                    lastHeadPosition = jointPoint;
                                }
                                if (joint.JointType == JointType.Spine)
                                {
                                    spineDistance = Distance3D(jointPoint, lastSpinePosition);
                                    lastSpinePosition = jointPoint;
                                }

                                //Draw joint
                                if (!logging)
                                {
                                    Point jointPos = GetJointPoint(joint);
                                    Ellipse jointBlob = new Ellipse() { Fill = userBrush, Height = 20, Width = 20 };
                                    Canvas.SetLeft(jointBlob, jointPos.X - 10);
                                    Canvas.SetTop(jointBlob, jointPos.Y - 10);
                                    depthSkelCanvas.Children.Add(jointBlob);
                                }
                                

                            }
                            else
                            {
                                Point3f jointPoint = new Point3f();
                                jointPoint.X = joint.Position.X;
                                jointPoint.Y = joint.Position.Y;
                                jointPoint.Z = joint.Position.Z;
                                jointPoint.tracked = false;
                                jointPoint.time = DateTime.Now;
                                jointPositions.Add(joint, jointPoint);
                            }
                        }

                        //Draw stick figure

                        //Draw head and torso
                        if (!logging)
                        {
                            figure = CreateFigure(skeleton, userBrush, new[] { JointType.Head, JointType.ShoulderCenter, JointType.ShoulderLeft, JointType.Spine,
                                                                                    JointType.ShoulderRight, JointType.ShoulderCenter, JointType.HipCenter
                                                                                    });
                            depthSkelCanvas.Children.Add(figure);

                            figure = CreateFigure(skeleton, userBrush, new[] { JointType.HipLeft, JointType.HipRight });
                            depthSkelCanvas.Children.Add(figure);

                            //Draw left leg
                            figure = CreateFigure(skeleton, userBrush, new[] { JointType.HipCenter, JointType.HipLeft, JointType.KneeLeft, JointType.AnkleLeft, JointType.FootLeft });
                            depthSkelCanvas.Children.Add(figure);

                            //Draw right leg
                            figure = CreateFigure(skeleton, userBrush, new[] { JointType.HipCenter, JointType.HipRight, JointType.KneeRight, JointType.AnkleRight, JointType.FootRight });
                            depthSkelCanvas.Children.Add(figure);

                            //Draw left arm
                            figure = CreateFigure(skeleton, userBrush, new[] { JointType.ShoulderLeft, JointType.ElbowLeft, JointType.WristLeft, JointType.HandLeft });
                            depthSkelCanvas.Children.Add(figure);

                            //Draw right arm
                            figure = CreateFigure(skeleton, userBrush, new[] { JointType.ShoulderRight, JointType.ElbowRight, JointType.WristRight, JointType.HandRight });
                            depthSkelCanvas.Children.Add(figure);
                        }
                        curBodyTrackingState.Add(iSkeleton, jointPositions);
                    }
                    iSkeleton++;
                } // for each skeleton
            }
            logSkeletonJointPositions();
            logVelocity();
            //logBodyProximity(headDistance, spineDistance);
        }

        void nui_AllFramesReady(object sender, AllFramesReadyEventArgs e)
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
                loggerQueue.addToQueue(skeletonID + jointVelocityFileName, dataString);
                
            }
            lastBodyTrackingState.Clear();
            lastBodyTrackingState = new Dictionary<int, Dictionary<Joint, Point3f>>(curBodyTrackingState);
        }


        void logBodyProximity(double headDistance, double spineDistance) {

            if (!logging)
                return;

            string sHeadDistance = "";
            string sSpineDistance = "";

            if (headDistance != -1) {
                sHeadDistance = headDistance.ToString();
                sSpineDistance = spineDistance.ToString();
            }
            loggerQueue.addToQueue(twoBodyProximityFileName, sHeadDistance + "," + sSpineDistance + ",");

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
            
                loggerQueue.addToQueue(skeletonID + bodyTrackingFileName, dataString);
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

            if ((DateTime.Now.Subtract(lastColorFrameSaveTime)).Milliseconds > colorFrameSaveRate)
            {
                if (logging)
                {
                    addFrameLogged();
                    //BitmapSource bmapSource = BitmapSource.Create(colorFrame.Width, colorFrame.Height, 96, 96, PixelFormats.Bgr32, null, Image, colorFrame.Width * colorFrame.BytesPerPixel);
                    saveColorFrameToJpg(source);
                    //BitmapSource bmapSource = BitmapSource.Create(colorFrame.Width, colorFrame.Height, 96, 96, PixelFormats.Bgr32, null, Image, colorFrame.Width * colorFrame.BytesPerPixel);
                    saveDepthFrameToJpg(curDepthImage);

                    lastColorFrameSaveTime = DateTime.Now;
                }
            }

        }

        void saveColorFrameToJpg(BitmapSource image) {

            if (!logging)
                return;

            int width = 128;
            int height = width;
            int stride = width / 8;
            byte[] pixels = new byte[height * stride];

            // Define the image palette
            BitmapPalette myPalette = BitmapPalettes.Halftone256;

            string path = curDir + "\\frames\\rgb";
            Directory.CreateDirectory(path);
            string fileName = colorImageNum + ".jpg";

            FileStream stream = new FileStream(path + "\\" + fileName, FileMode.Create);
            JpegBitmapEncoder encoder = new JpegBitmapEncoder();
            encoder.FlipHorizontal = true;
            encoder.FlipVertical = false;
            encoder.QualityLevel = 100;
            encoder.Frames.Add(BitmapFrame.Create(image));
            encoder.Save(stream);

            loggerQueue.addToQueue(colorImagesFileName, fileName);
            colorImageNum++;
        
        }

        void saveDepthFrameToJpg(BitmapSource image)
        {

            if (!logging)
                return;

            int width = 128;
            int height = width;
            int stride = width / 8;
            byte[] pixels = new byte[height * stride];

            // Define the image palette
            BitmapPalette myPalette = BitmapPalettes.Halftone256;

            string path = curDir + "\\frames\\depth";
            Directory.CreateDirectory(path);
            string fileName = colorImageNum + ".jpg";

            FileStream stream = new FileStream(path + "\\" + fileName, FileMode.Create);
            JpegBitmapEncoder encoder = new JpegBitmapEncoder();
            encoder.FlipHorizontal = true;
            encoder.FlipVertical = false;
            encoder.QualityLevel = 100;
            encoder.Frames.Add(BitmapFrame.Create(image));
            encoder.Save(stream);

            loggerQueue.addToQueue(colorImagesFileName, fileName);
            colorImageNum++;

        }

        void logFaceDepthData(ArrayList faceFrames) {
            if (!logging)
                return;

            for (int i = 0; i < faceFrames.Count; i++)
            {
                FaceFrame faceFrame = (FaceFrame)faceFrames[i];
                System.Drawing.Rectangle rect = faceFrame.rect;
                int centroidX = (rect.X + (rect.Width / 2));
                int centroidY = (rect.Y + (rect.Height / 2));
                string faceData = centroidX + "," + centroidY + "," + faceFrame.depth + "," + faceFrame.player;
                loggerQueue.addToQueue(faceTrackingDepthFileName, faceData);

            }
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
                loggerQueue.addToQueue(audioAngleFileName, angle);
            }
        }

        public void updateAudioState(string audioState) {
            if (logging)
            {
                loggerQueue.addToQueue(audioStateFileName, audioState);
            }
        }

        public void updateSpeechRecognized(string recognized) {
            if (logging)
            {
                loggerQueue.addToQueue(speechRecognitionFileName, recognized);
            }
        }

        public void updateAppStatus(string status)
        {
            appStatus.Text += "\n" + status;
            if (logging) {
                loggerQueue.addToQueue(appStatusFileName, status);
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
            loggerQueue.addToQueue(audioRecordingFilename, filename);
        }

        void audioSource_SoundAngleChanged(object sender, SoundSourceAngleChangedEventArgs e)
        {
            
            string info = e.Angle + "," + e.ConfidenceLevel;
            if (logging)
                loggerQueue.addToQueue(audioAngleFileName, info);
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
                figure.Points.Add(GetJointPoint(skeleton.Joints[joints[i]]));
            }

            return figure;
        }


        private Point GetJointPoint(Joint joint)
        {
            DepthImagePoint point = this.kinectSensor.MapSkeletonPointToDepth(joint.Position, this.kinectSensor.DepthStream.Format);
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
