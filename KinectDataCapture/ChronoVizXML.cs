using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;

namespace KinectDataCapture
{



    class ChronoVizXML
    {
        XmlDocument doc;
        XmlElement root;
        XmlElement dataSourcesEl;
        public enum DataSourceType
        {
            CSV,
            Video,
            SenseCam,
        };



        public ChronoVizXML()
        {
            doc = new XmlDocument();
            root = doc.CreateElement("chronoVizDocumentTemplate");
            doc.AppendChild(root);
            dataSourcesEl = doc.CreateElement("dataSources");
            root.AppendChild(dataSourcesEl);
        }

        public void addDataSource(DataSourceType type, string filename, LinkedList<ChronoVizDataSet> dataSets )
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

            //Create Data Source
            XmlElement dataSource = doc.CreateElement("dataSource");

            //Create Data Source Elements
            XmlElement filePath = doc.CreateElement("filePath");
            filePath.InnerText = filename;
            dataSource.AppendChild(filePath);

            XmlElement typeEl = doc.CreateElement("type");
            typeEl.InnerText = type.ToString() + "DataSource";
            dataSource.AppendChild(typeEl);

            XmlElement timeCoding = doc.CreateElement("timeCoding");
            timeCoding.InnerText = "Relative";
            dataSource.AppendChild(timeCoding);

            XmlElement startTime = doc.CreateElement("startTime");
            startTime.InnerText = "0.0";
            dataSource.AppendChild(startTime);
          
            if (type == DataSourceType.CSV)
            {
                XmlElement timeColumn = doc.CreateElement("timeColumn");
                timeColumn.InnerText = "Time";
                dataSource.AppendChild(timeColumn);
            }
            

            XmlElement dataSetsEl = doc.CreateElement("dataSets");
            //Create Data Sets
            foreach (ChronoVizDataSet ds in dataSets)
            {
                XmlElement dataSetEl = doc.CreateElement("dataSet");

                XmlElement variableNameEl = doc.CreateElement("variableName");
                XmlElement dataLabelEl = doc.CreateElement("dataLabel");
                XmlElement dataTypeEl = doc.CreateElement("dataType");

                variableNameEl.InnerText = ds.columnName;
                dataLabelEl.InnerText = ds.dataLabel;
                dataTypeEl.InnerText = "DataType" + ds.dataType.ToString();

                dataSetEl.AppendChild(variableNameEl);
                dataSetEl.AppendChild(dataLabelEl);
                dataSetEl.AppendChild(dataTypeEl);

                dataSetsEl.AppendChild(dataSetEl);

            }

            if (!dataSetsEl.IsEmpty)
                dataSource.AppendChild(dataSetsEl);
            dataSourcesEl.AppendChild(dataSource);            
        }


        public bool saveToFile(string filepath)
        {
            Boolean result = false;
            try
            {
                FileStream fileStream =
                  new FileStream(filepath, FileMode.Create);
                XmlTextWriter textWriter =
                  new XmlTextWriter(fileStream, Encoding.Unicode);
                textWriter.Formatting = Formatting.Indented;
                doc.Save(textWriter);
                result = true;
            }
            catch (System.IO.DirectoryNotFoundException ex)
            {
                System.ArgumentException argEx = new System.ArgumentException("XMLFile path is not valid", filepath, ex);
                throw argEx;
            }
            catch (System.Exception ex)
            {
                System.ArgumentException argEx = new System.ArgumentException("XML File write failed", filepath, ex);
                throw argEx;
            }
            return result;
        }
    }
    public struct ChronoVizDataSet
    {
        public enum Type
        {
            TimeSeries,
            GeographicLat,
            GeographicLon,
            ImageSequence,
            AnnotationTime,
            AnnotationEndTime,
            AnnotationTitle,
            AnnotationCategory,
            Annotation,
            AnotoTraces,
            Audio,
            Transcript,
            SpatialX,
            SpatialY
        };
        public Type dataType;
        public string columnName;
        public string dataLabel;

        public ChronoVizDataSet(Type type, string cname, string vname)
        {
            this.dataType = type;
            this.columnName = cname;
            this.dataLabel = vname;
        }

    }

}
