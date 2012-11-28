using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;

namespace KinectDataCapture
{
    public enum LogFileType
    {
        AppStatusFile,
        AudioAngleFile,
        AudioStateFile,
        AudioRecordingFile,
        SpeechRecognitionFile,
        DepthTrackFile,
        BodyTrackFile,
        HeadTrackFile,
        ColorImageFile,
        DepthImageFile
    };

    class LogFileManager
    {


        public struct LogFileTypeInfo {
            public String basename;
            public String extension;
            public LogFileTypeInfo(String bname, String ext, Boolean isNum)
            {
                basename = bname;
                extension = ext;
            }

        };
        public struct LogFile
        {
            public LogFileType type;
            public bool isNumbered;
            public int num;
            public string fullFilename;
            public LogFile(LogFileType type, bool isNum, int num, string ff)
            {
                this.type = type;
                this.isNumbered = isNum;
                this.num = num;
                this.fullFilename = ff;
            }
        }

        private Dictionary<LogFileType, LogFileTypeInfo> logFileTemplates;
        private Dictionary<Tuple<LogFileType, int>, string> currentLogFiles;
        private string directory;
        private string prefix;
        private LoggerQueue logQueue;

        public LogFileManager(string kinectId, string prefix)
        {
            logFileTemplates = new Dictionary<LogFileType, LogFileTypeInfo>();
            logFileTemplates.Add(LogFileType.AppStatusFile,     new LogFileTypeInfo("appStatus", "csv", false));
            logFileTemplates.Add(LogFileType.AudioAngleFile,    new LogFileTypeInfo("audioAngle", "csv", false));
            logFileTemplates.Add(LogFileType.AudioStateFile,    new LogFileTypeInfo("audioState", "csv", false));
            logFileTemplates.Add(LogFileType.AudioRecordingFile,new LogFileTypeInfo("audioRecording", "csv", false));
            logFileTemplates.Add(LogFileType.DepthTrackFile,    new LogFileTypeInfo("depthTracking", "csv", true));
            logFileTemplates.Add(LogFileType.BodyTrackFile,     new LogFileTypeInfo("bodyTracking", "csv", true));
            logFileTemplates.Add(LogFileType.HeadTrackFile,     new LogFileTypeInfo("headTracking", "csv", true));
            logFileTemplates.Add(LogFileType.ColorImageFile,    new LogFileTypeInfo("colorImages", "csv", false));
            logFileTemplates.Add(LogFileType.DepthImageFile,    new LogFileTypeInfo("depthImages", "csv", false));

            currentLogFiles = new Dictionary<Tuple<LogFileType, int>, string>();

            genDirectory(kinectId);
            this.prefix = prefix;

            logQueue = new LoggerQueue();

        }

        public string genDirectory(string kinectDeviceId)
        {
            //Create parent directory if it does not exist
            string path = Environment.GetEnvironmentVariable("userprofile") + "\\Desktop\\Kinect_Logs";
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            //Get Device ID name
            int index = kinectDeviceId.LastIndexOf("\\");
            string deviceId = kinectDeviceId.Substring(index + 1, kinectDeviceId.Length - index - 1);

            //Build new folder for current logging session MMDDYY HH.MM.SS.mmm
            directory = path + "\\" + deviceId
                + "\\" + DateTime.Now.Year + DateTime.Now.Month + DateTime.Now.Day + " " +
                + DateTime.Now.Hour + "." + DateTime.Now.Minute + "." + DateTime.Now.Second + "." + DateTime.Now.Millisecond;

            Directory.CreateDirectory(directory);
            return directory;
        }

        public string getDirectory()
        {
            return directory;
        }

        //
        public string createLogFile(LogFileType filetype, Boolean isNum, int num)
        {
            LogFileTypeInfo fileTemplate = new LogFileTypeInfo();
            logFileTemplates.TryGetValue(filetype, out fileTemplate);
            string relativeFilename = "";
            if (filetype == LogFileType.ColorImageFile)
            {
                relativeFilename += "frames\\rgb\\";
                Directory.CreateDirectory(directory + "\\frames\\rgb");
            }
            else if (filetype == LogFileType.DepthImageFile)
            {
                relativeFilename += "frames\\depth\\";
                Directory.CreateDirectory(directory + "\\frames\\depth");
            }
            relativeFilename += prefix + "_";
            if (isNum)
            {
                relativeFilename += num + "_";
            }
            relativeFilename += fileTemplate.basename + "." + fileTemplate.extension;

            //TODO check if file exists 
            currentLogFiles.Add(new Tuple<LogFileType, int>(filetype, num), relativeFilename);
            logQueue.addNewFile(directory + "\\" + relativeFilename, getColumnHeader(filetype));

            return relativeFilename;
        }
        
        public bool createLogFile(LogFileType filetype)
        {
            this.createLogFile(filetype, false, 0);
            return true;
        }

        public void closeLogs()
        {
            logQueue.endLogging();
        }

        public void log(LogFileType logFileType, int num, string logData)
        {
            string filename;
            if (!currentLogFiles.TryGetValue(new Tuple<LogFileType, int>(logFileType, num), out filename))
            {
                filename = this.createLogFile(logFileType, true, num);
                
            }
            logQueue.addToQueue(directory + "\\" + filename, logData);
        }


        //Log function takes the type and data. If file does not exist is calls create log file.
        public void log(LogFileType logFileType, string logData)
        {
            string filename;
            if (!currentLogFiles.TryGetValue(new Tuple<LogFileType, int>(logFileType, 0), out filename))
            {
                filename = this.createLogFile(logFileType, false, 0);

            }
            logQueue.addToQueue(directory + "\\" + filename, logData);
        }

        string getColumnHeader(LogFileType filetype)
        {

            string columnHeader = "";
            if (filetype == LogFileType.AppStatusFile)
            {
                columnHeader = "Time,StatusMessage";
            }
            else if (filetype == LogFileType.HeadTrackFile)
            {
                columnHeader = "Time,Pitch,Roll,Yaw";
            }
            else if (filetype == LogFileType.AudioAngleFile)
            {
                columnHeader = "Time,AudioAngle,AudioConfidence";
            }
            else if (filetype == LogFileType.AudioRecordingFile)
            {
                columnHeader = "Time,FileName";
            }
            else if (filetype == LogFileType.DepthTrackFile)
            {
                columnHeader = "Time,MinDepth,MaxDepth";
            }
            else if (filetype == LogFileType.BodyTrackFile)
            {
                columnHeader = "Time,HipCenterX,HipCenterY,HipCenterZ,SpineX,SpineY,SpineZ,ShoulderCenterX,ShoulderCenterY,ShoulderCenterZ,HeadX,HeadY,HeadZ,ShoulderLeftX,ShoulderLeftY,ShoulderLeftZ,ElbowLeftX,ElbowLeftY,ElbowLeftZ,WristLeftX,WristLeftY,WristLeftZ,HandLeftX,HandLeftY,HandLeftZ,ShoulderRightX,ShoulderRightY,ShoulderRightZ,ElbowRightX,ElbowRightY,ElbowRightZ,WristRightX,WristRightY,WristRightZ,HandRightX,HandRightY,HandRightZ,HipLeftX,HipLeftY,HipLeftZ,KneeLeftX,KneeLeftY,KneeLeftZ,AnkleLeftX,AnkleLeftY,AnkleLeftZ,FootLeftX,FootLeftY,FootLeftZ,HipRightX,HipRightY,HipRightZ,KneeRightX,KneeRightY,KneeRightZ,AnkleRightX,AnkleRightY,AnkleRightZ,FootRightX,FootRightY,FootRightZ,";
            }
            else if ((filetype == LogFileType.ColorImageFile) || (filetype == LogFileType.DepthImageFile))
            {
                columnHeader = "Time,FileName";
            }
            return columnHeader;

        }

        public void generateChronovizTemplate()
        {
            //Create a chronoviz xml
            ChronoVizXML xml = new ChronoVizXML();
            string filePath = directory + "\\" + prefix + ".chronoviztemplate";

            //Add log files to template
            foreach (KeyValuePair<Tuple<LogFileType, int>, string> kvp in currentLogFiles)
            {
                addLogFileToTemplate(xml, kvp.Key.Item1, kvp.Value);
            }

            //Save file
            xml.saveToFile(filePath);
          
        }

        public void addLogFileToTemplate(ChronoVizXML xml, LogFileType type, string filename)
        {
            LinkedList<ChronoVizDataSet> dataSets = new LinkedList<ChronoVizDataSet>();
            filename = filename.Replace("\\", "/");
            switch (type)
            {
                case LogFileType.ColorImageFile:
                    dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.ImageSequence, "FileName", prefix + "RgbImages"));
                    xml.addDataSource(ChronoVizXML.DataSourceType.CSV, filename, dataSets);
                    break;
                case LogFileType.DepthImageFile:
                    dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.ImageSequence, "FileName", prefix + "DepthImages"));
                    xml.addDataSource(ChronoVizXML.DataSourceType.CSV, filename, dataSets);
                    break;
                case LogFileType.AudioAngleFile:
                    dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.TimeSeries, "AudioAngle", prefix + "AudioAngle"));
                    dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.TimeSeries, "AudioConfidence", prefix + "AudioConfidence"));
                    xml.addDataSource(ChronoVizXML.DataSourceType.CSV, filename, dataSets);
                    break;
                case LogFileType.AudioRecordingFile:                                
                    //Currently does not load from name in Audio log file
                    xml.addDataSource(ChronoVizXML.DataSourceType.Video, "audio.wav", dataSets);
                    break;
                case LogFileType.HeadTrackFile:
                    dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.TimeSeries, "Pitch", prefix + "Pitch"));
                    dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.TimeSeries, "Roll", prefix + "Roll"));
                    dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.TimeSeries, "Yaw", prefix + "Yaw"));
                    xml.addDataSource(ChronoVizXML.DataSourceType.CSV, filename, dataSets);
                    break;
                case LogFileType.BodyTrackFile:
                    populateBodyDataSets(xml, filename, dataSets);
                    xml.addDataSource(ChronoVizXML.DataSourceType.CSV, filename, dataSets);
                    break;
                default:
                    break;

            }

        }


        private void populateBodyDataSets(ChronoVizXML xml, string filename, LinkedList<ChronoVizDataSet> dataSets)
        {
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialX, "HipCenterX", prefix + "HipCenterX"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialY, "HipCenterY", prefix + "HipCenterY"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.TimeSeries, "HipCenterZ", prefix + "HipCenterZ"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialX, "SpineX", prefix + "SpineX"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialY, "SpineY", prefix + "SpineY"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.TimeSeries, "SpineZ", prefix + "SpineZ"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialX, "ShoulderCenterX", prefix + "ShoulderCenterX"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialY, "ShoulderCenterY", prefix + "ShoulderCenterY"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.TimeSeries, "ShoulderCenterZ", prefix + "ShoulderCenterZ"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialX, "HeadX", prefix + "HeadX"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialY, "HeadY", prefix + "HeadY"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.TimeSeries, "HeadZ", prefix + "HeadZ"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialX, "ShoulderLeftX", prefix + "ShoulderLeftX"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialY, "ShoulderLeftY", prefix + "ShoulderLeftY"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.TimeSeries, "ShoulderLeftZ", prefix + "ShoulderLeftZ"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialX, "ElbowLeftX", prefix + "ElbowLeftX"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialY, "ElbowLeftY", prefix + "ElbowLeftY"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.TimeSeries, "ElbowLeftZ", prefix + "ElbowLeftZ"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialX, "WristLeftX", prefix + "WristLeftX"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialY, "WristLeftY", prefix + "WristLeftY"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.TimeSeries, "WristLeftZ", prefix + "WristLeftZ"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialX, "HandLeftX", prefix + "HandLeftX"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialY, "HandLeftY", prefix + "HandLeftY"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.TimeSeries, "HandLeftZ", prefix + "HandLeftZ"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialX, "ShoulderRightX", prefix + "ShoulderRightX"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialY, "ShoulderRightY", prefix + "ShoulderRightY"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.TimeSeries, "ShoulderRightZ", prefix + "ShoulderRightZ"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialX, "ElbowRightX", prefix + "ElbowRightX"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialY, "ElbowRightY", prefix + "ElbowRightY"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.TimeSeries, "ElbowRightZ", prefix + "ElbowRightZ"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialX, "WristRightX", prefix + "WristRightX"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialY, "WristRightY", prefix + "WristRightY"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.TimeSeries, "WristRightZ", prefix + "WristRightZ"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialX, "HandRightX", prefix + "HandRightX"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialY, "HandRightY", prefix + "HandRightY"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.TimeSeries, "HandRightZ", prefix + "HandRightZ"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialX, "HipLeftX", prefix + "HipLeftX"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialY, "HipLeftY", prefix + "HipLeftY"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.TimeSeries, "HipLeftZ", prefix + "HipLeftZ"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialX, "KneeLeftX", prefix + "KneeLeftX"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialY, "KneeLeftY", prefix + "KneeLeftY"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.TimeSeries, "KneeLeftZ", prefix + "KneeLeftZ"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialX, "AnkleLeftX", prefix + "AnkleLeftX"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialY, "AnkleLeftY", prefix + "AnkleLeftY"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.TimeSeries, "AnkleLeftZ", prefix + "AnkleLeftZ"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialX, "FootLeftX", prefix + "FootLeftX"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialY, "FootLeftY", prefix + "FootLeftY"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.TimeSeries, "FootLeftZ", prefix + "FootLeftZ"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialX, "HipRightX", prefix + "HipRightX"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialY, "HipRightY", prefix + "HipRightY"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.TimeSeries, "HipRightZ", prefix + "HipRightZ"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialX, "KneeRightX", prefix + "KneeRightX"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialY, "KneeRightY", prefix + "KneeRightY"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.TimeSeries, "KneeRightZ", prefix + "KneeRightZ"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialX, "AnkleRightX", prefix + "AnkleRightX"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialY, "AnkleRightY", prefix + "AnkleRightY"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.TimeSeries, "AnkleRightZ", prefix + "AnkleRightZ"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialX, "FootRightX", prefix + "FootRightX"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.SpatialY, "FootRightY", prefix + "FootRightY"));
            dataSets.AddLast(new ChronoVizDataSet(ChronoVizDataSet.Type.TimeSeries, "FootRightZ", prefix + "FootRightZ"));
         
        }
    }
}
