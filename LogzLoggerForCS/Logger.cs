using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Newtonsoft.Json;
using System.Net;

namespace LogzLoggerForCS
{
    

    public class Logger
    {
        
        private string dirPath;
        private string logName;
        private Dictionary<KeyValuePair <string, object>, int> logData; //containts the log records and the byte size per record
        private bool writeLogToFile;
        private readonly Object SyncKey1=new Object(); //handles syncing of writing to log
        private readonly Object SyncKey2 = new Object(); // handles syncing of writing to file

        #region constructors
        public Logger()
        //log will be also written to a file in the project Assembly folder
        {
            try
            {
                writeLogToFile = false;
                logData = new Dictionary<KeyValuePair<string, object>, int>();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        public Logger( string logName) : this()
        //log will be also written to a file in the project Assembly folder
        {
            try
            {
                writeLogToFile = true;
                this.dirPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)+"\\Logs"; //deafault is Assembly path
                this.logName = logName+".json";
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        public  Logger(string logName , string dir) : this(logName)
        //log will be also written to a file in the given dir path
        {
            try
            {
                this.dirPath = dir;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        #endregion constructors




        public void Append(string key, object val)
        {
            try
            {

                //add log record 
                var record = new KeyValuePair<string, object>(key,val);
                string jsonRecord = JsonConvert.SerializeObject(record);
                int bytesCount = System.Text.ASCIIEncoding.Unicode.GetByteCount(jsonRecord);
                lock(SyncKey1)
                {
                    logData.Add(record, bytesCount);
                }
                    

                //update log file
                if (writeLogToFile)
                    UpdateLogFile();  
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }


        private string getFilePath()
        {
            return Path.Combine(dirPath, logName);
        }

    

       

        public Dictionary<KeyValuePair<string, object>, int> getLogData()
        //returns a copy of the log data
        {
            lock(SyncKey1)
            {
                if (logData != null && logData.Count > 0)
                {
                    Dictionary<KeyValuePair<string, object>, int> clone = new Dictionary<KeyValuePair<string, object>, int>();
                    foreach (var record in logData)
                    {
                        var cloneValue = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(record.Key.Value)); //clone object
                        clone.Add(new KeyValuePair<string, object>(record.Key.Key, cloneValue), record.Value);

                    }

                    return clone;
                }
            }
            //else
            return null;
        }
        
        public string LogInJsonFormat()
        //method returns the log data in a json format
        {
            List<KeyValuePair<string, object>> list = new List<KeyValuePair<string, object>>();
            lock (SyncKey1)
            {
                list = logData.Keys.ToList();
            }

            IDictionary<string, object> dictionary = 
            list.ToDictionary(pair => pair.Key, pair => pair.Value);
            string jsonLog = JsonConvert.SerializeObject(dictionary);
            return jsonLog;

        }

        private void UpdateLogFile()
        //method writes the log to a file in a json format
        {
            try
            {
                lock(SyncKey2)
                {
                    if (!Directory.Exists(dirPath))
                        Directory.CreateDirectory(dirPath);
                    string jsonData = LogInJsonFormat();
                    StreamWriter file = new StreamWriter(getFilePath(), false);
                    file.WriteLine(jsonData);
                    file.Close();
                }
                
            }
            catch(Exception ex)
            {
                throw ex;
            }
            
        }

        

    }
}
