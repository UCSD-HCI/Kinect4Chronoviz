using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace KinectDataCapture
{
    class ChronoVizXML
    {
        XmlDocument doc;
        XmlElement root;
        public enum DataSourceType
        {
            CSVDataSource,
            VideoDataSource,
            SenseCamDataSource,
        };
        public enum DataSetType
        {
            DataTypeTimeSeries,
            DataTypeGeographicLat,
            DataTypeGeographicLon,
            DataTypeImageSequence,
            DataTypeAnnotationTime,
            DataTypeAnnotationEndTime,
            DataTypeAnnotationTitle,
            DataTypeAnnotationCategory,
            DataTypeAnnotation,
            DataTypeAnotoTraces,
            DataTypeAudio,
            DataTypeTranscript,
            DataTypeSpatialX,
            DataTypeSpatialY
        };

        public ChronoVizXML()
        {
            doc = new XmlDocument();
            root = doc.CreateElement("chronoVizDocumentTemplate");
            doc.AppendChild(root);
        }

        public void addDataSource(DataSourceType type, string filename, Dictionary<ChronoVizXML.DataSetType, Tuple<string, string>> dataSets )
        {
            /*
             *         <dataSource>
            <filePath>TopCamera/rgb/TopCameraRGB.csv</filePath>
            <type>CSVDataSource</type>
            <timeCoding>Relative</timeCoding>
            <timeColumn>Time</timeColumn>
            <startTime>0.0</startTime>
            <dataSets>
                <dataSet>
                    <variableName>FileName</variableName>
                    <dataLabel>TopCameraImages</dataLabel>
                    <dataType>DataTypeImageSequence</dataType>
                </dataSet>
            </dataSets>
        </dataSource>
             * */
            XmlElement dataSource = doc.CreateElement("dataSource");
            XmlElement filePath = doc.CreateElement("filePath");
            XmlElement typeEl = doc.CreateElement("type");
            XmlElement timeCoding = doc.CreateElement("timeCoding");
            XmlElement startTime = doc.CreateElement("startTime");

            filePath.InnerText = filename;
            typeEl.InnerText = type.ToString();
            timeCoding.InnerText = "Relative";
            startTime.InnerText = "0.0";

            dataSource.AppendChild(filePath);
            dataSource.AppendChild(typeEl);
            dataSource.AppendChild(timeCoding);
            dataSource.AppendChild(startTime);

            root.AppendChild(dataSource);
            
        }

        
        public override string ToString()
        {
            return doc.OuterXml;
        }

    }

}
