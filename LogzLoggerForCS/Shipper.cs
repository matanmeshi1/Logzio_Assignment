using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System.Net;
using System.IO;
using System.Threading;

namespace LogzLoggerForCS
{
    public enum RequestType { http, https };

    public class Shipper
    {
        public string type;
        private string Token;
        private RequestType requestType;
        private const int maxBytePerRowRequest = 500000;
        private const int maxRowsPerRequest = 20;
        private const int maxRetries=5; //number of reries if request fails
        private const int delayBetweenRetries=500; // in mili seconds
        private string errorMsg;
        private Exception exception;

        #region constructors
        public Shipper(string logzioToken, RequestType requestType) : this(logzioToken)
        {
            this.requestType = requestType;
        }

        public Shipper(string logzioToken)
        {
            this.Token = logzioToken;
            this.requestType = RequestType.http;
            this.exception = new Exception();
            this.type = "";
            this.errorMsg = "";
        }

        //TODO add constructor with retry times defind

        #endregion 

        public bool Ship(Logger Log)
        //this method ships a log to Logz.io
        {
            try
            {
                //build URI for request
                Dictionary<string, string> Params = new Dictionary<string, string>();
                Params.Add("token", Token);
                if (type != null)
                    Params.Add("type", type);
                var uri = UriConstructor(requestType, Params);

                //get log info
                Dictionary<KeyValuePair<string, object>, int> logData = Log.getLogData();

                //send request
                bool response = ShipLogData(uri, logData, 0);
                return response;
            }
            catch(Exception ex)
            {
                errorMsg = ex.Message + "\n";
                if (ex.InnerException != null)
                {
                    errorMsg += ex.InnerException.Message;
                }
                exception = ex;
                return false;
            }
            
        }

        private string UriConstructor(RequestType requestType, Dictionary<string, string> parameters)
        //method constructs the request URI
        {
            string baseUri = "http://listener.logz.io:8070/"; //deafault

            if (requestType == RequestType.http)
            {
                baseUri = "http://listener.logz.io:8070/";
            }
            if (requestType == RequestType.https)
            {
                baseUri = "https://listener.logz.io:8071/";
            }

            string Uri = baseUri;
            for (int i = 0; i < parameters.Count; i++)
            {
                if (i == 0)
                    Uri += "?";
                else
                    Uri += "&";
                Uri += parameters.ElementAt(i).Key + "=" + parameters.ElementAt(i).Value;
            }

            return Uri;
        }

        private HttpWebResponse sendRequest(string uri, string body,int attempt)
        //method sends a request to Logz.io api

        {
            try
            {

                var request = (HttpWebRequest)WebRequest.Create(uri);
                request.ContentType = "application/json";
                request.Method = "POST";

                using (var streamWriter = new StreamWriter(request.GetRequestStream()))
                {
                    streamWriter.Write(body);
                }

                var response = (HttpWebResponse)request.GetResponse();
                return response;
            }
            catch (System.Net.WebException ex)
            {
                //try to make the request a few more times
                if (attempt < maxRetries)
                {
                    Thread.Sleep(delayBetweenRetries);
                    return sendRequest(uri, body, attempt + 1);
                }
                else
                {

                    using (var streamReader = new StreamReader(ex.Response.GetResponseStream()))
                    {
                        var res = streamReader.ReadToEnd();
                        errorMsg = res;
                        exception =  ex;
                    }
                    return (HttpWebResponse)ex.Response;

                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private bool ShipLogData(string uri, Dictionary<KeyValuePair<string, object>, int> logData,int startIndex)
        //method ships log data to logz.io 
        //method splits the log to multiple requests if it's longer than  allowed in a single request
        {
            try
            {
                bool errorStatus = false; //the status of the log shipping
                List<Dictionary<string, object>> requestBody = new List<Dictionary<string, object>>();
                Dictionary<string,object> currentRow =new Dictionary<string, object>();
                requestBody.Add(currentRow);
                int currentRowBytes = 0;
                //int recordIndex = startIndex;

                for(int i= startIndex; i<logData.Count;i++)
                {
                    //recordIndex = i;
                    var record = logData.ElementAt(i);
                    if(currentRowBytes+record.Value<maxBytePerRowRequest)
                    //add record to log row
                    {
                        currentRow.Add(record.Key.Key, record.Key.Value);
                        currentRowBytes += record.Value;
                    }
                    else //current log row is full
                    {
                        if(requestBody.Count< maxRowsPerRequest)
                        {
                            //add current row to body and create a new row
                            currentRow = new Dictionary<string, object>();
                            requestBody.Add(currentRow);
                            currentRow.Add(record.Key.Key, record.Key.Value);
                            currentRowBytes = record.Value;
                        }
                        else
                        {
                            //request body is full send another request
                            errorStatus= !ShipLogData(uri, logData, i);
                            break;
                        }
                    }
                }

                //send request
                var response = sendRequest(uri, createLogzioBody(requestBody),1);
                
                if (errorStatus==false && response.StatusCode== HttpStatusCode.OK)
                {
                    return true;
                }
                return false;
            }
            catch(Exception ex)
            {
                throw ex;
            }
        }
        private string createLogzioBody(List<Dictionary<string, object>> list)
        //method gets a list of dictionaries and converts each dictionary into a json that represents a row in the logz.io request format
        {

            string jsonBody = "";
            for (int i = 0; i < list.Count; i++)
            {
                jsonBody += JsonConvert.SerializeObject(list[i]);
                if (i != list.Count - 1)
                    jsonBody += "\n";
            }
            return jsonBody;
        }
        public string  getErrorMsg()
        {
            if(errorMsg!=null)
                return errorMsg;
            return "";
        }
        public Exception getException()
        {
            
            return  exception;
        }
    }
}
