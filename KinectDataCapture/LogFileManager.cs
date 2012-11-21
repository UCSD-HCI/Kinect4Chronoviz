using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

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
        private static int fileHandleIdx = 0;
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

        public bool createLogFile(LogFileType filetype, Boolean isNum, int num)
        {
            LogFileTypeInfo fileTemplate = new LogFileTypeInfo();
            logFileTemplates.TryGetValue(filetype, out fileTemplate);
            string newFilename = directory + "\\";
            if (filetype == LogFileType.ColorImageFile)
            {
                newFilename += "frames\\rgb\\";
                Directory.CreateDirectory(directory + "\\frames\\rgb");
            }
            else if (filetype == LogFileType.DepthImageFile)
            {
                newFilename += "frames\\depth\\";
                Directory.CreateDirectory(directory + "\\frames\\depth");
            }
            newFilename += prefix + "_";
            if (isNum)
            {
                newFilename += num + "_";
            }
            newFilename += fileTemplate.basename + "." + fileTemplate.extension;
            //TODO check if file exists 
            currentLogFiles.Add(new Tuple<LogFileType, int>(filetype, num), newFilename);
            logQueue.addNewFile(newFilename, getColumnHeader(filetype));
            return true;
        }
        
        public bool createLogFile(LogFileType filetype)
        {
            this.createLogFile(filetype, false, 0);
            return true;
        }

        public void log(LogFileType logFileType, int num, string logData)
        {
            string filename;
            if (!currentLogFiles.TryGetValue(new Tuple<LogFileType, int>(logFileType, num), out filename))
            {
                this.createLogFile(logFileType, true, num);
            }
            logQueue.addToQueue(filename, logData);
        }

        public void log(LogFileType logFileType, string logData)
        {
            string filename;
            if (!currentLogFiles.TryGetValue(new Tuple<LogFileType, int>(logFileType, 0), out filename))
            {
                this.createLogFile(logFileType, false, 0);
            }
            logQueue.addToQueue(filename, logData);
            if (logFileType == LogFileType.ColorImageFile)
                generateChronovizTemplate();
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
            else if (filetype == LogFileType.AudioStateFile)
            {
                columnHeader = "Time,SpeechState,AudioAngle";
            }
            else if (filetype == LogFileType.AudioRecordingFile)
            {
                columnHeader = "Time,Filename";
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
            ChronoVizXML xml = new ChronoVizXML();
            StreamWriter writer = File.CreateText(directory + "\\" + prefix + ".chronoviztemplate");

            string filename;
            currentLogFiles.TryGetValue(new Tuple<LogFileType, int>(LogFileType.ColorImageFile, 0), out filename);
            addLogFileToTemplate(xml, LogFileType.ColorImageFile, filename);

            writer.WriteLine(xml.ToString());
            writer.Close();            
        }

        public void addLogFileToTemplate(ChronoVizXML xml, LogFileType type, string filename)
        {

            Dictionary<ChronoVizXML.DataSetType, Tuple<string, string>> dataSets = new Dictionary<ChronoVizXML.DataSetType, Tuple<string, string>>();
            switch (type)
            {
                case LogFileType.ColorImageFile:
                    dataSets.Add(ChronoVizXML.DataSetType.DataTypeImageSequence, new Tuple<string, string>("FileName", prefix + "RgbImages"));
                    xml.addDataSource(ChronoVizXML.DataSourceType.CSVDataSource, filename, dataSets);
                    break;
                default:
                    break;

            }

        }
    }
}
