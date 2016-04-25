using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Linq;
using System.Text;
using System.Runtime.Serialization.Json;

// Code provided "as is" with no guarantee of any kind. 
// Written by Ben Rood at ICM Direct.

    namespace CrowdEmotionClientCS
{

    /// <summary>
    /// C# client for CrowdEmotion REST API
    /// Usage:
    /// call constructor once per session 
    /// var client = new CrowdEmotionClientCS.CrowdEmotionClient("XXX", "YYY");
    /// then call methods any number of times
    /// </summary>
    public class CrowdEmotionClient
    {
        private CrowdEmotionJSON _crowdEmotionJSON;

        /// <summary>
        /// The single constructor; use once and then call any methods any number of times
        /// </summary>
        public CrowdEmotionClient(string username, string password)
        {
            _crowdEmotionJSON = new CrowdEmotionJSON();
            string json = _crowdEmotionJSON.GetLoginJSON(username, password);
            Login login = Deserialize<Login>(json);
            _crowdEmotionJSON.StoreLogin(login);
        }

        /// <summary>
        /// Standard json -> object Deserializing code
        /// </summary>
        private T Deserialize<T>(string json)
        {
            using (MemoryStream ms = new MemoryStream(Encoding.Unicode.GetBytes(json)))
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
                return (T)serializer.ReadObject(ms);
            }
        }

        // the functions below all work the same way and more can be added as required
        //----------------------------------------------------------------------------        

        public List<ResearchItem> GetResearch(int? limit = null)
        {
            if (!limit.HasValue)
                limit = int.MaxValue;

            string url = string.Format("research?limit={0}", limit);
            string json = _crowdEmotionJSON.JsonForUrl(url);
            return Deserialize<List<ResearchItem>>(json);
        }

        public CrowdEmotionResponse GetResponse(int responseId)
        {
            string url = string.Format("response/{0}", responseId);
            string json = _crowdEmotionJSON.JsonForUrl(url);
            return Deserialize<CrowdEmotionResponse>(json);
        }

        public List<CrowdEmotionResponse> GetResponses(int? mediaId, int? limit = null)
        {
            if (!limit.HasValue)
                limit = int.MaxValue;

            string url = string.Format("response?limit={0}", limit);

            if (mediaId.HasValue)
                url += "&where={\"media_id\":\"" + mediaId.Value.ToString() + "\"}";

            string json = _crowdEmotionJSON.JsonForUrl(url); 
            return Deserialize<List<CrowdEmotionResponse>>(json);
        }        

        public List<MediaItem> GetMedia(int? limit = null)
        {
            if (!limit.HasValue)
                limit = int.MaxValue;

            string url = string.Format("media?limit={0}", limit);
            string json = _crowdEmotionJSON.JsonForUrl(url);
            return Deserialize<List<MediaItem>>(json);
        }

        public List<Respondent> GetRespondents(int researchId, int? limit = null)
        {
            if (!limit.HasValue)
                limit = int.MaxValue;
            
            string url = string.Format("respondent?research_id={0}&limit={1}", researchId, limit);
            string json = _crowdEmotionJSON.JsonForUrl(url);
            return Deserialize<List<Respondent>>(json);
        }

        public Respondent GetRespondent(int respondentId)
        {
            string url = string.Format("respondent/{0}", respondentId);
            string json = _crowdEmotionJSON.JsonForUrl(url);
            return Deserialize<Respondent>(json);
        }

        public void DeleteRespondent(int respondentId)
        {
            string url = string.Format("respondent/{0}", respondentId);
            string json = _crowdEmotionJSON.JsonForUrl(url, "DELETE");
        }

        public List<Respondent> GetRespondentByName(string name)
        {
            string url = string.Format("respondent?name={0}", name);
            string json = _crowdEmotionJSON.JsonForUrl(url);
            return Deserialize<List<Respondent>>(json);
        }


        public List<TimeSeriesItem> GetTimeSeriesItems(int responseId, IEnumerable<int> metricIds = null, int? limit = null)
        {
            if (!limit.HasValue)
                limit = int.MaxValue;
            
            string url = string.Format("timeseries?response_id={0}&limit={1}&normalize=false", responseId, limit);
            if (metricIds != null && metricIds.Count() > 0)
                url += "&metric_id=" + string.Join(",", metricIds.ToArray());
            string json = _crowdEmotionJSON.JsonForUrl(url);
            return Deserialize<List<TimeSeriesItem>>(json);
        }
        
    }


    /// <summary>
    /// Handles web communication with CrowdEmotion (should not need to be modified)
    /// </summary>
    internal class CrowdEmotionJSON
    {
        const string BASE_URL = "https://api.crowdemotion.co.uk/v1/";

        private string _userId;     // } store values returned from the login
        private string _token;      // } 

        /// <summary>
        /// Returns json from POST call to user/login
        /// </summary>
        internal string GetLoginJSON(string username, string password)
        {
            string url = BASE_URL + "user/login";
            HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
            request.Method = "POST";
            request.ContentType = "application/json; charset=utf-8";

            using (StreamWriter writer = new StreamWriter(request.GetRequestStream()))
            {
                string json = "\"username\":\"{0}\",\"password\":\"{1}\"";
                json = string.Format(json, username, password);
                json = "{" + json + "}";
                writer.Write(json);
                writer.Flush();
            }
            return RequestContents(request);
        }

        /// <summary>
        /// Returns json from GET call to the url
        /// </summary>
        internal string JsonForUrl(string url, string method = "GET")
        {
            HttpWebRequest request = WebRequest.Create(BASE_URL + url) as HttpWebRequest;
            request.Method = method;
            request.ContentLength = 0;

            var headers = AuthHeaders(url, method);
            foreach (string key in headers.Keys)
                request.Headers.Add(key, headers[key]);

            return RequestContents(request);
        }

        /// <summary>
        /// Store authorization tokens for any future calls
        /// </summary>
        internal void StoreLogin(Login login)
        {
            _userId = login.userId;
            _token = login.token;
        }

        /// <summary>
        /// Get return string from the HttpWebRequest
        /// </summary>
        private string RequestContents(HttpWebRequest request)
        {
            using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
            {
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        /// <summary>
        /// All authorization Headers
        /// </summary>
        private Dictionary<string, string> AuthHeaders(string path, string httpMethod)
        {
            Dictionary<string, string> rtn = new Dictionary<string, string>();

            string now = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ"); // ISO
            string nonce = Nonce();
            string pathAndMethod = path + "," + httpMethod;
            string authToken = AuthToken(pathAndMethod, nonce, now);

            rtn.Add("Authorization", _userId + ":" + authToken);
            rtn.Add("x-ce-rest-date", now);
            rtn.Add("nonce", nonce);
            return rtn;
        }

        /// <summary>
        /// String of 21 random characters, used in authorization Headers
        /// </summary>
        private string Nonce()
        {
            var rnd = new Random();
            var chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            return new string(chars.Select(c => chars[rnd.Next(chars.Length)]).Take(21).ToArray());
        }

        /// <summary>
        /// Used in authorization Headers
        /// </summary>
        private string AuthToken(string pathAndMethod, string nonce, string time)
        {
            string concat = _token + ":" + pathAndMethod + "," + time + "," + nonce;
            System.Security.Cryptography.SHA256Managed crypt = new System.Security.Cryptography.SHA256Managed();
            byte[] hashed = crypt.ComputeHash(Encoding.UTF8.GetBytes(concat));
            return Convert.ToBase64String(hashed);
        }

    }


    //---------------------------
    // Data transfer classes used
    //---------------------------

    /// <summary>
    /// Output from POST to /user/login
    /// </summary>
    public class Login
    {
        public string userId;
        public string token;
    }

    /// <summary>
    /// Output from GET to /timeseries
    /// </summary>
    public class TimeSeriesItem
    {
        public int responseId;
        public int metricId;
        public string metricName;
        public int startIndex;
        public int endIndex;
        public int stepSize;
        public string customMessage;
        public List<string> data;
        public List<double> EmotionValues
        {
            get
            {
                return this.data.Where(x => !string.IsNullOrEmpty(x)).Select(x => double.Parse(x)).ToList();
            }
        }
    }

    /// <summary>
    /// Output from GET to /respondent
    /// </summary>
    public class Respondent
    {
        public int id;
        public string name;
        public object customData;
        public string key;
        public int? research_id;
        public int user_id;
    }

    /// <summary>
    /// Output from GET to /research
    /// </summary>
    public class ResearchItem
    {
        public int id;
        public string title;
    }

    /// <summary>
    /// Output from GET to /media
    /// </summary>
    public class MediaItem
    {
        public int id;
        public string name;
        public string mediaPath;
        public string path;
        public int research_id;
        public int lengthMS;
        public float length;
        public object tags;
    }


    /// <summary>
    /// Output from GET to /response
    /// </summary>
    public class CrowdEmotionResponse
    {
        public int id;
        public int? research_id;
        public int media_id;
        public int? respondent_id;
        public int? user_id;
        public int? researchId;
        public int mediaId;
        public int? respondentId;
        public int? userId;
        public bool complete;
        public int company_id;
    }
}    
