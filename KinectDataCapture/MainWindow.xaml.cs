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

        /* --- APPLICATION STATE --- */
        bool logging = false;

        //Kinect variables
        KinectSensor kinectSensor;
        public string DeviceId;
        public DepthImageFrame curDepthPlanarImage;
        public CoordinateMapper coordMap;


        //Depth variables
        int sampleRate;
        DateTime lastTimeDepth;
        int depthSampleRate = 500;
        int skeletonSampleRate = 500;
        DateTime lastTimeSkeleton;

        public BitmapSource curDepthImage;
        public DepthImageFormat depthImageFormat = DepthImageFormat.Resolution320x240Fps30;

        private LogFileManager logger;
        /*FaceDetection faceDetection; */

        /* --- AUDIO RELATED STATE --- */
        private string wavFilename;
        private Stream audioStream;
        private object lockObj = new object();
        KinectAudioSource audioSource;
        double curAudioAngle = 0;
        double curAudioConfidence = 0;

        /* --- COLOR FRAMES --- */
        int colorFrameSaveRate = 100; //milliseconds
        DateTime lastColorFrameSaveTime;
        int colorImageNum = 0;
        int framesSaved = 0;

        /*--- DEPTH FRAMES ---*/
        public static byte[] depthFrame32 = new byte[320 * 240 * 4];
        ImageBrush recordingBrush;
        const int RED_IDX = 2;
        const int GREEN_IDX = 1;
        const int BLUE_IDX = 0;

        /* -- FACE TRACKING STATE --- */
        FaceLogger Faces;
        bool recordHeadPose = true;
        bool recordColorImages = true;
        bool recordDepthImages = true;

        /*-- SKELETON TRACKING --- */
        public SkeletonTrackingMode skeletonTrackingMode { get; set; }
        Brush[] skeletonBrushes = new Brush[] { Brushes.Indigo, Brushes.DodgerBlue, Brushes.Purple, Brushes.Pink, Brushes.Goldenrod, Brushes.Crimson };
        Dictionary<int, Dictionary<Joint, Point3f>> currentTrackedSkeletons;
        //Maps the local skeleton index against the tracking id from Kinect
        Dictionary<int, int> trackedSkeletonsMapper;
        private int localTrackingNum;

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

        public MainWindow()
        {
            InitializeComponent();
        } 

        private void Window_Loaded(object sender, EventArgs e)
        {
            

            // Create recording ImageBrush
            recordingBrush = new ImageBrush();

            recordingBrush.ImageSource =
                new BitmapImage(new Uri(@"Recording.jpg", UriKind.Relative));

            // Add all kinect sensors to UI
            var availableKinects = (from k in KinectSensor.KinectSensors
                                               where k.Status == KinectStatus.Connected
                                               select k);
            foreach (var kinect in availableKinects)
            {
                kinectChooser.Items.Add(kinect.DeviceConnectionId.ToString());
            }
            if (kinectChooser.SelectedValue == null)
            {
                kinectChooser.SelectedIndex = 0;
            }

            UIUpdate();
            UIUpdateAppStatus("WindowLoaded");
            //Initialize localSkeletonTracking
            initLocalSkeletonMapper();



        }

        #region UI Functions and handlers
        
        //Functions
        private void UIUpdate()
        {
            if (kinectSensor == null)
            {
                UIEnableSelectionOfKinectOptions(false);
            }
            else
            {
                UIEnableSelectionOfKinectOptions(true);
            }
            if (kinectSensor != null)
            {
                if (kinectSensor.DepthStream.Range == DepthRange.Near)
                {
                    chkNearMode.IsChecked = true;
                }
                else
                {
                    chkNearMode.IsChecked = false;
                }
                if (kinectSensor.SkeletonStream.TrackingMode == SkeletonTrackingMode.Seated)
                {
                    chkSeatedMode.IsChecked = true;
                }
                else
                {
                    chkSeatedMode.IsChecked = false;
                }
                recordColorChk.IsChecked = recordColorImages;
                recordDepthChk.IsChecked = recordDepthImages;
                recordHeadChk.IsChecked = recordHeadPose;
            }

        }
        public void UIUpdateAppStatus(string status)
        {
            appStatus.Text += "\n" + status;
            if (logging)
            {
                logger.log(LogFileType.AppStatusFile, status);
                appStatus.ScrollToEnd();
            }
        }
        public void UIUpdateNumHeadsTracked(int numHeads)
        {
            lblHeadsTracked.Content = "Heads Tracked : " + numHeads;
        }

        public void UIUpdateNumSkeletonsTracked(int numSkeletons)
        {
            lblSkeletonsTracked.Content = "SkeletonsTracked : " + numSkeletons;
        }

        public void UIAddNumFramesLogged()
        {
            framesSaved++;
            lblNumberOfFramesLogged.Content = "Frames logged : " + framesSaved;
        }
        public void UIResetFramesLogged()
        {
            framesSaved = 0;
            lblNumberOfFramesLogged.Content = "Frames logged : " + framesSaved;
        }
        private void UIEnableSelectionOfKinectOptions(bool isEnabled)
        {

            if (isEnabled)
            {
                chkNearMode.IsEnabled = true;
                chkSeatedMode.IsEnabled = true;
                recordDepthChk.IsEnabled = true;
                recordColorChk.IsEnabled = true;
                recordHeadChk.IsEnabled = true;
                startButton.IsEnabled = true;
            }
            else
            {
                chkNearMode.IsEnabled = false;
                chkSeatedMode.IsEnabled = false;
                recordDepthChk.IsEnabled = false;
                recordColorChk.IsEnabled = false;
                recordHeadChk.IsEnabled = false;
                startButton.IsEnabled = false;
            }
        }

        //Handlers
        private void appStatus_TextChanged(object sender, TextChangedEventArgs e)
        {
            appStatus.SelectionStart = appStatus.Text.Length;
            appStatus.ScrollToEnd();
        }

        private void setNearMode(object sender, RoutedEventArgs e)
        {
            if (kinectSensor != null)
            {
                if (chkNearMode.IsChecked == true)
                {
                    kinectSensor.DepthStream.Range = DepthRange.Near;
                    UIUpdateAppStatus("Setting Kinect to Near Mode");
                }
                else
                {
                    kinectSensor.DepthStream.Range = DepthRange.Default;
                    UIUpdateAppStatus("Setting Kinect to Default Mode");
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
                    UIUpdateAppStatus("Setting Skeleton to Seated Mode");
                    skeletonTrackingMode = SkeletonTrackingMode.Seated;

                }
                else
                {
                    kinectSensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;
                    UIUpdateAppStatus("Setting Skeleton to Default Mode");
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
                    UIUpdateAppStatus("Enabling recording of color images.");
                    recordColorImages = true;
                }
                else
                {
                    UIUpdateAppStatus("Disabling recording of color images.");
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
                    UIUpdateAppStatus("Enabling recording of depth images.");
                    recordDepthImages = true;
                }
                else
                {
                    UIUpdateAppStatus("Disabling recording of depth images.");
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
                    UIUpdateAppStatus("Enabling recording of head pose.");
                    recordHeadPose = true;
                }
                else
                {
                    UIUpdateAppStatus("Disabling recording of head pose.");
                    recordHeadPose = false;
                }
            }
        }
        private void selectKinect_Click(object sender, RoutedEventArgs e)
        {
            string kinectId = kinectChooser.SelectedValue.ToString();
            KinectSensor selectedKinect = null;

            //Get list of available kinects
            var availableKinects = (from k in KinectSensor.KinectSensors
                                    where k.Status == KinectStatus.Connected
                                    select k);

            //Find the one the user selected.
            foreach (var kinect in availableKinects)
            {
                if (kinect.DeviceConnectionId == kinectId)
                {
                    selectedKinect = kinect;
                    DeviceId = kinectId;
                }
            }

            if (selectedKinect != null)
                SwitchToKinect(selectedKinect);

            KinectSensor.KinectSensors.StatusChanged += KinectSensors_StatusChanged;

            
            UIUpdate();
        }

        private void startButton_Click(object sender, RoutedEventArgs e)
        {

            if (kinectSensor == null)
            {
                UIUpdateAppStatus("Please select a kinect device first.");
                return;
            }
            UIUpdateAppStatus("Capturing data");

            //should make capture settings on UI non-modifyable after this point
            if (!logging)
            {
                UIUpdateAppStatus("Starting logging");
                UIResetFramesLogged();

                //Create a log file manager
                logger = new LogFileManager(kinectSensor.DeviceConnectionId, FilePrefixTbx.Text);

                //Start recording audio                
                wavFilename = System.IO.Path.Combine(logger.getDirectory(), "audio.wav");
                logAudioRecording("audio.wav");
                Thread thread = new Thread(new ThreadStart(RecordKinectAudio));
                thread.Priority = ThreadPriority.Highest;
                thread.Start();

                //Initialize localSkeletonTracking
                trackedSkeletonsMapper.Clear();


                //Update UI
                startButton.Content = "Stop Logging";
                
                logging = true;
            }
            else
            {
                UIUpdateAppStatus("\nStopped logging");

                logger.generateChronovizTemplate();
                logger.closeLogs();

                //Update UI
                startButton.Content = "Start Logging";
                logging = false;
                IsAudioRecording = false;
            }
        }
        private void Window_Closed(object sender, EventArgs e)
        {

            if (IsAudioRecording)
            {
                IsAudioRecording = false;
            }
            audioSource.Stop();
            kinectSensor.Stop();
            if (logger != null)
            {
                logger.closeLogs();
            }
            Environment.Exit(0);
        }

        #endregion

        #region Kinect Functions
        //Toggles Kinect, can switch from null to new, new to null
        //and old to new sensor.
        private void SwitchToKinect(KinectSensor newKinect)
        {
            //If new sensor is different than current
            if (kinectSensor != newKinect)
            {
                //If current is not null
                if (kinectSensor != null)
                {
                    //Stop old sensor
                    StopKinect(kinectSensor);
                }

                //If new is not null
                if (newKinect != null)
                {
                    //Start new sensor
                    kinectSensor = newKinect;
                    OpenKinect(newKinect);
                }
            }

            
        }

        private void OpenKinect(KinectSensor newKinect)
        {
            UIUpdateAppStatus("Creating audio manager");
            //audioManager = new AudioManager(this);
            audioSource = CreateAudioSource();

            UIUpdateAppStatus("Initializing video streams");
            initializeVideoStreams();

            UIUpdateAppStatus("Initializing Face Tracking");
            Faces = new FaceLogger(newKinect);

            UIUpdateAppStatus("Initializing Audio Sources");
            audioStream = audioSource.Start();

            newKinect.Start();
            UIUpdateAppStatus("New Kinect started(" + DeviceId + ")");
            UIUpdateAppStatus("Ready to capture");
        }

        private void StopKinect(KinectSensor oldKinect)
        {
            oldKinect.Stop();
        }

        void KinectSensors_StatusChanged(object sender, StatusChangedEventArgs e)
        {
            if (e.Sensor != null && e.Status == KinectStatus.Connected)
            {
                SwitchToKinect(e.Sensor);
            }
            else if (e.Sensor == kinectSensor)
            {
                // The current Kinect isn't connected anymore
                SwitchToKinect(null);
            }
        }

        public void initializeVideoStreams()
        {
            kinectSensor.ColorStream.Enable();


            kinectSensor.DepthStream.Enable(depthImageFormat);

            kinectSensor.SkeletonStream.Enable();

            coordMap = new CoordinateMapper(kinectSensor);

            kinectSensor.Start();

            kinectSensor.DepthFrameReady += new EventHandler<DepthImageFrameReadyEventArgs>(nui_DepthFrameReady);
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
            return depthFrame32;
        }
        #endregion

        #region Frame Ready Handlers

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
                    UIAddNumFramesLogged();
                    logColorFrameToJpg(source);
                    logDepthFrameToJpg(curDepthImage);

                    lastColorFrameSaveTime = DateTime.Now;
                }
            }
            if (colorFrame != null)
                colorFrame.Dispose();

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

            if (curDepthPlanarImage != null)
                curDepthPlanarImage.Dispose();
        }        

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
            int numHeads = 0;
            Skeleton skeleton;
            Brush userBrush;
            Brush[] brushes = new Brush[6];
            brushes[0] = new SolidColorBrush(Color.FromRgb(255, 0, 0));
            brushes[1] = new SolidColorBrush(Color.FromRgb(0, 255, 0));
            brushes[2] = new SolidColorBrush(Color.FromRgb(64, 255, 255));
            brushes[3] = new SolidColorBrush(Color.FromRgb(255, 255, 64));
            brushes[4] = new SolidColorBrush(Color.FromRgb(255, 64, 255));
            brushes[5] = new SolidColorBrush(Color.FromRgb(128, 128, 255));

            //Clear tracking state or initialize
            if (currentTrackedSkeletons != null)
            {
                currentTrackedSkeletons.Clear();
                            }
            else
            {
                currentTrackedSkeletons = new Dictionary<int, Dictionary<Joint, Point3f>>();
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
                userBrush = skeletonBrushes[numSkeletons % skeletonBrushes.Length];
                

                //If tracking this skeleton
                if (SkeletonTrackingState.Tracked == skeleton.TrackingState)
                {
                    Dictionary<Joint, Point3f> jointPositions = new Dictionary<Joint, Point3f>();
                    numSkeletons++;

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
                                numHeads++;

                            string dataString = headPose.pitch + "," + headPose.roll + "," + headPose.yaw;

                            if (logging)
                                logger.log(LogFileType.HeadTrackFile, getLocalSkeletonId(skeleton.TrackingId), dataString);
                            else
                            {
                                drawHeadPosition(depthSkelCanvas, userBrush, skeleton, headPose);
                            }
                        }
                    }


                    //Add the whole of joint positions to curBodyTrackingState
                    currentTrackedSkeletons.Add(getLocalSkeletonId(skeleton.TrackingId), jointPositions);
                }
                iSkeleton++;
            } // for each skeleton

            logSkeletonJointPositions();
            UIUpdateNumHeadsTracked(numHeads);
            UIUpdateNumSkeletonsTracked(numSkeletons);
            //logVelocity();
            //logBodyProximity(headDistance, spineDistance);
            if (skeletonFrame != null)
                skeletonFrame.Dispose();
            if (colorImageFrame != null)
                colorImageFrame.Dispose();
            if (depthImageFrame != null)
                depthImageFrame.Dispose();

        }

        private void initLocalSkeletonMapper()
        {
            trackedSkeletonsMapper = new Dictionary<int,int>();
            localTrackingNum = 0;
        }
        private int getLocalSkeletonId(int skeletonTrackingId)
        {
            int localTrackingId = 0;
            if (trackedSkeletonsMapper.TryGetValue(skeletonTrackingId, out localTrackingId))
                return localTrackingId;
            else
            {
                int newLocalSkeletonId = localTrackingNum;
                trackedSkeletonsMapper.Add(skeletonTrackingId, newLocalSkeletonId);
                localTrackingNum++;
                return newLocalSkeletonId;
            }


        }

        #endregion

        #region Kinect Joints Handling

        Polyline getBodySegment(Microsoft.Kinect.JointCollection joints, Brush brush, params JointType[] ids)
        {
            PointCollection points = new PointCollection(ids.Length);
            for (int i = 0; i < ids.Length; ++i)
            {
                ColorImagePoint colPoint = coordMap.MapSkeletonPointToColorPoint(joints[ids[i]].Position, ColorImageFormat.RgbResolution640x480Fps30);
                points.Add(new Point(colPoint.X, colPoint.Y));
            }

            Polyline polyline = new Polyline();
            polyline.Points = points;
            polyline.Stroke = brush;
            polyline.StrokeThickness = 5;
            return polyline;
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
        #endregion

        #region Drawing Functions

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

        #region Logging Functions

        void logSkeletonJointPositions() {

            if (!logging)
                return;

            if (currentTrackedSkeletons == null)
                return;

            IDictionaryEnumerator skeletons = currentTrackedSkeletons.GetEnumerator();

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
                        dataString += -position.X + "," + -position.Y + "," + position.Z + ",";
                    }
                    else {
                        dataString += ",,,";
                    }
                }
            
                logger.log(LogFileType.BodyTrackFile, skeletonID, dataString);
            }
        }

        void logDepthData(Dictionary<int, int> minDepths, Dictionary<int, int> maxDepths)
        {
            if (!logging)
                return;

            IDictionaryEnumerator minDepthInfo = minDepths.GetEnumerator();
            while (minDepthInfo.MoveNext())
            {
                String depthData = "";
                int player = (int)minDepthInfo.Entry.Key;
                depthData += minDepths[player];
                if (maxDepths.ContainsKey(player))
                {
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

        void logColorFrameToJpg(BitmapSource image)
        {

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

        void logDepthFrameToJpg(BitmapSource image)
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






        #endregion
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






       
    }


}
