using System.Net;
using System.IO;
using System;
using System.Windows.Forms;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Ellipsis.Api
{
    class Connect
    {
        private void DisplayLoginError()
        {
            string message = "Incorrect username and/or password.";
            string title = "Login failed";
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        public void SetUsername(string text)
        {
            this.username = text;
        }

        public void SetPassword(string text)
        {
            this.password = text;
        }

        public bool GetStatus()
        {
            return this.logged_in;
        }

        public string GetLoginToken()
        {
            return this.login_token;
        }

        public JObject GetPath(string pathId, bool isFolder, string pageStart, bool isRoot)
        {
            string url;

            List<string> queryParams = new List<string>();

            if (pageStart != null)
            {
                queryParams.Add($"pageStart={pageStart}");
            }

            if (!isRoot)
            {
                if (isFolder)
                {
                    queryParams.Add("type=[\"raster\", \"vector\", \"folder\"]");
                    string queryParamString = String.Join('&', queryParams);
                    url = $"{Ellipsis.Api.Settings.ApiUrl}/v3/path/{pathId}/folder/list?{queryParamString}";
                }
                else
                {
                    url = $"{Ellipsis.Api.Settings.ApiUrl}/v3/path/{pathId}";
                }
            }
            else
            {
                queryParams.Add("type=[\"raster\", \"vector\", \"folder\"]");
                string queryParamString = String.Join('&', queryParams);

                url = $"{Ellipsis.Api.Settings.ApiUrl}/v3/account/root/{pathId}?{queryParamString}";
            }

            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(url);

            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "GET";
            httpWebRequest.Headers.Add("Authorization", "Bearer " + login_token);
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            if (httpResponse.StatusDescription != "OK") return null;

            using (var reader = new StreamReader(httpResponse.GetResponseStream()))
            {
                string responseFromServer = reader.ReadToEnd();
                // Display the content.
                try
                {
                    JObject data = JObject.Parse(responseFromServer);
                    return data;
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                }
            }
            return null;
        }

        public JObject SearchByName(string name, string pageStart)
        {
            var httpWebRequest = (HttpWebRequest)WebRequest.Create($"{Ellipsis.Api.Settings.ApiUrl}/v3/path?root=[\"myDrive\"]&text={name}");

            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "GET";
            httpWebRequest.Headers.Add("Authorization", "Bearer " + login_token);
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            if (httpResponse.StatusDescription != "OK") return null;

            using (var reader = new StreamReader(httpResponse.GetResponseStream()))
            {
                try
                {
                    return JObject.Parse(reader.ReadToEnd());
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                }
            }
            return null;
        }

        public bool LogoutRequest()
        {
            this.username = "";
            this.password = "";
            this.login_token = "";
            this.logged_in = false;
            return false;
        }

        public bool LoginRequest()
        {
            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create($"{Ellipsis.Api.Settings.ApiUrl}/v3/account/login");
            httpWebRequest.Method = "POST";
            httpWebRequest.Accept = "application/json";
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.KeepAlive = true;
            httpWebRequest.Headers.Add("Keep-Alive: 3155760000");

            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                string json = JsonConvert.SerializeObject(new { username = this.username, password = this.password });
                streamWriter.Write(json);
                streamWriter.Flush();
                streamWriter.Close();
            }


            HttpWebResponse httpResponse;
            try
            {
                httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            }
            catch (Exception e)
            {
                DisplayLoginError();
                return false;
            }

            this.logged_in = true;
            Stream dataStream = httpResponse.GetResponseStream();
            StreamReader reader = new StreamReader(dataStream);
            string responseFromServer = reader.ReadToEnd();
            // Display the content.
            try
            {
                dynamic data = JObject.Parse(responseFromServer);
                login_token = data["token"];
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
            return true;
            /*
            //Retrieve your cookie that id's your session
            //response.Cookies
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
            {
                Console.WriteLine(reader.ReadToEnd());
            }
            return false;
            */
        }

        private string username;
        private string password;
        private string login_token;
        private bool logged_in = false;
    }
}
