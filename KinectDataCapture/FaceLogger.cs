using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Diagnostics;

using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit.FaceTracking;

namespace KinectDataCapture
{

    class FaceLogger
    {
        private const uint MaxMissedFrames = 100;

        private readonly Dictionary<int, SkeletonFaceTracker> trackedSkeletons = new Dictionary<int, SkeletonFaceTracker>();

        private byte[] colorImage;

        private ColorImageFormat colorImageFormat = ColorImageFormat.Undefined;

        private short[] depthImage;

        private DepthImageFormat depthImageFormat = DepthImageFormat.Undefined;

        private bool disposed;

        private Skeleton[] skeletonData;

        public KinectSensor Kinect;



        public FaceLogger(KinectSensor kinectSensor)
        {
            this.Kinect = kinectSensor;

        }

        public struct HeadPose3D
        {
            public bool valid;
            public double pitch;
            public double roll;
            public double yaw;
        }



        public void sendNewFrames(ColorImageFrame colorImageFrame, DepthImageFrame depthImageFrame, SkeletonFrame skeletonFrame)
        {
            // Check for image format changes.  The FaceTracker doesn't
            // deal with that so we need to reset.
            if (this.depthImageFormat != depthImageFrame.Format)
            {
                this.ResetFaceTracking();
                this.depthImage = null;
                this.depthImageFormat = depthImageFrame.Format;
            }

            if (this.colorImageFormat != colorImageFrame.Format)
            {
                this.ResetFaceTracking();
                this.colorImage = null;
                this.colorImageFormat = colorImageFrame.Format;
            }

            // Create any buffers to store copies of the data we work with
            if (this.depthImage == null)
            {
                this.depthImage = new short[depthImageFrame.PixelDataLength];
            }

            if (this.colorImage == null)
            {
                this.colorImage = new byte[colorImageFrame.PixelDataLength];
            }

            // Get the skeleton information
            if (this.skeletonData == null || this.skeletonData.Length != skeletonFrame.SkeletonArrayLength)
            {
                this.skeletonData = new Skeleton[skeletonFrame.SkeletonArrayLength];
            }

            colorImageFrame.CopyPixelDataTo(this.colorImage);
            depthImageFrame.CopyPixelDataTo(this.depthImage);
            skeletonFrame.CopySkeletonDataTo(this.skeletonData);

            // Update the list of trackers and the trackers with the current frame information
            this.RemoveOldTrackers(skeletonFrame.FrameNumber);
            foreach (Skeleton skeleton in this.skeletonData)
            {
                if (skeleton.TrackingState == SkeletonTrackingState.Tracked
                    || skeleton.TrackingState == SkeletonTrackingState.PositionOnly)
                {
                    // We want keep a record of any skeleton, tracked or untracked.
                    if (!this.trackedSkeletons.ContainsKey(skeleton.TrackingId))
                    {
                        this.trackedSkeletons.Add(skeleton.TrackingId, new SkeletonFaceTracker());
                    }
                }
            }


        }

        public HeadPose3D getSkeletonHeadPose(Skeleton skeleton, int skelFrameNumber)
        {
            HeadPose3D headPose = new HeadPose3D();
            headPose.valid = false;

            // Give each tracker the upated frame.
            SkeletonFaceTracker skeletonFaceTracker;
            if (this.trackedSkeletons.TryGetValue(skeleton.TrackingId, out skeletonFaceTracker))
            {
                headPose = skeletonFaceTracker.OnFrameReady(this.Kinect, colorImageFormat, colorImage, depthImageFormat, depthImage, skeleton);
                skeletonFaceTracker.LastTrackedFrame = skelFrameNumber;
            }
            return headPose;
        }

        /// Clear out any trackers for skeletons we haven't heard from for a while
        /// </summary>
        private void RemoveOldTrackers(int currentFrameNumber)
        {
            var trackersToRemove = new List<int>();

            foreach (var tracker in this.trackedSkeletons)
            {
                uint missedFrames = (uint)currentFrameNumber - (uint)tracker.Value.LastTrackedFrame;
                if (missedFrames > MaxMissedFrames)
                {
                    // There have been too many frames since we last saw this skeleton
                    trackersToRemove.Add(tracker.Key);
                }
            }

            foreach (int trackingId in trackersToRemove)
            {
                this.RemoveTracker(trackingId);
            }
        }

        private void RemoveTracker(int trackingId)
        {
            this.trackedSkeletons[trackingId].Dispose();
            this.trackedSkeletons.Remove(trackingId);
        }

        private void ResetFaceTracking()
        {
            foreach (int trackingId in new List<int>(this.trackedSkeletons.Keys))
            {
                this.RemoveTracker(trackingId);
            }
        }

        private class SkeletonFaceTracker : IDisposable
        {
            private static FaceTriangle[] faceTriangles;

            private EnumIndexableCollection<FeaturePoint, PointF> facePoints;

            private FaceTracker faceTracker;

            private bool lastFaceTrackSucceeded;

            private SkeletonTrackingState skeletonTrackingState;

            public int LastTrackedFrame { get; set; }

            public void Dispose()
            {
                if (this.faceTracker != null)
                {
                    this.faceTracker.Dispose();
                    this.faceTracker = null;
                }
            }

                    /*
            public void DrawFaceModel(DrawingContext drawingContext)
            {
                if (!this.lastFaceTrackSucceeded || this.skeletonTrackingState != SkeletonTrackingState.Tracked)
                {
                    return;
                }

                var faceModelPts = new List<Point>();
                var faceModel = new List<FaceModelTriangle>();

                for (int i = 0; i < this.facePoints.Count; i++)
                {
                    faceModelPts.Add(new Point(this.facePoints[i].X + 0.5f, this.facePoints[i].Y + 0.5f));
                }

                foreach (var t in faceTriangles)
                {
                    var triangle = new FaceModelTriangle();
                    triangle.P1 = faceModelPts[t.First];
                    triangle.P2 = faceModelPts[t.Second];
                    triangle.P3 = faceModelPts[t.Third];
                    faceModel.Add(triangle);
                }

                var faceModelGroup = new GeometryGroup();
                for (int i = 0; i < faceModel.Count; i++)
                {
                    var faceTriangle = new GeometryGroup();
                    faceTriangle.Children.Add(new LineGeometry(faceModel[i].P1, faceModel[i].P2));
                    faceTriangle.Children.Add(new LineGeometry(faceModel[i].P2, faceModel[i].P3));
                    faceTriangle.Children.Add(new LineGeometry(faceModel[i].P3, faceModel[i].P1));
                    faceModelGroup.Children.Add(faceTriangle);
                }

                drawingContext.DrawGeometry(Brushes.LightYellow, new Pen(Brushes.LightYellow, 1.0), faceModelGroup);
            }*/

            /// <summary>
            /// Updates the face tracking information for this skeleton
            /// </summary>
            internal  HeadPose3D OnFrameReady(KinectSensor kinectSensor, ColorImageFormat colorImageFormat, byte[] colorImage, DepthImageFormat depthImageFormat, short[] depthImage, Skeleton skeletonOfInterest)
            {
                HeadPose3D hp = new HeadPose3D();
                hp.valid = false;
                this.skeletonTrackingState = skeletonOfInterest.TrackingState;

                if (this.skeletonTrackingState != SkeletonTrackingState.Tracked)
                {
                    // nothing to do with an untracked skeleton.
                    return hp;
                }

                if (this.faceTracker == null)
                {
                    try
                    {
                        this.faceTracker = new FaceTracker(kinectSensor);
                    }
                    catch (InvalidOperationException)
                    {
                        // During some shutdown scenarios the FaceTracker
                        // is unable to be instantiated.  Catch that exception
                        // and don't track a face.
                        Debug.WriteLine("AllFramesReady - creating a new FaceTracker threw an InvalidOperationException");
                        this.faceTracker = null;
                    }
                }

                if (this.faceTracker != null)
                {
                    FaceTrackFrame frame = this.faceTracker.Track(
                        colorImageFormat, colorImage, depthImageFormat, depthImage, skeletonOfInterest);

                    this.lastFaceTrackSucceeded = frame.TrackSuccessful;
                    if (this.lastFaceTrackSucceeded)
                    {
                        //if (faceTriangles == null)
                        //{
                        //    // only need to get this once.  It doesn't change.
                        //    faceTriangles = frame.GetTriangles();
                        //}
                        hp.valid = true;
                        hp.pitch = frame.Rotation.X;
                        hp.roll = frame.Rotation.Y;
                        hp.yaw = frame.Rotation.Z;
                        return hp;
                    }
                }
                return hp;
            }

            private struct FaceModelTriangle
            {
                public Point P1;
                public Point P2;
                public Point P3;
            }
        }
    }
    

}
