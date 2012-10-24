using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;

namespace KinectDataCapture
{
    class VocabularyParser
    {


        public VocabularyParser() { 
        
        }


        public ArrayList parseFile(string fileName) { 
        
            ArrayList terms = new ArrayList();
            if (File.Exists(fileName)){
            
                TextReader reader = new StreamReader(fileName);
                string line;
                while((line = reader.ReadLine()) !=null){
                    terms.Add(line);
                }
            }
            return terms;
        }
    }

}
