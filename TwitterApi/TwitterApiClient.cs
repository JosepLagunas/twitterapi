﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TwitterClient.Models;
using System.Globalization;
using Utils;

namespace TwitterApi
{
    public class TweetStreamArgs : EventArgs
    {
        public Tweet Tweet;

        public TweetStreamArgs(Tweet Tweet)
        {
            this.Tweet = Tweet;
        }
    }

    public class MapBoxCoordinates
    {
        public decimal longitude1 { get; }
        public decimal latitude1 { get; }
        public decimal longitude2 { get; }
        public decimal latitude2 { get; }
    
        public MapBoxCoordinates(decimal longitude1, decimal latitude1, decimal longitude2, decimal latitude2)
        {
            this.longitude1 = longitude1;
            this.latitude1 = latitude1;
            this.longitude2 = longitude2;
            this.latitude2 = latitude2;
        }
    }
    public class TwitterApiClient
    {
        private string baseAPIRestUrl;
        private string basePublicStreamAPI;
        private string baseUrlTweetLinks;
    
        private string oauth_consumer_key;
        
        private string oauth_signature_method;
        private string oauth_token;
        private string oauth_version;
        
        private static object locker = new object();
        private DateTime epochUtc;
        private HMACSHA1 sigHasher;

        private bool credentialsSet;
        
        private static TwitterApiClient _instance;

        public delegate void StreamTweetByHashtagHandler(object sender, TweetStreamArgs e);

        public event StreamTweetByHashtagHandler streamTweetByHashTagEvent;

        private enum UseCase
        {
            Tweet = 0,
            DirectMessage = 1,
            ReTweet = 2,
        };

        private TwitterApiClient()
        {
            baseAPIRestUrl = "https://api.twitter.com/1.1/";
            baseUrlTweetLinks = "https://twitter.com/TwitterDev/status/";
            basePublicStreamAPI = "https://stream.twitter.com/1.1/";

            epochUtc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            credentialsSet = false;
        }
             

        public static TwitterApiClient getInstance()
        {
            if (_instance == null)
            {
                lock (locker)
                {
                    if (_instance == null)
                    {
                        _instance = new TwitterApiClient();
                    }
                }
            }
            return _instance;
        }

        public void setCredentials(string consumerKey, string consumerSecretKey, string accessToken, string accessTokenSecret)
        {            
            oauth_consumer_key = consumerKey;
            oauth_token = accessToken;
           
            oauth_signature_method = "HMAC-SHA1";
            
            oauth_version = "1.0";
            sigHasher = new HMACSHA1(new ASCIIEncoding().GetBytes(string.Format("{0}&{1}", Uri.EscapeDataString(consumerSecretKey), 
                Uri.EscapeDataString(accessTokenSecret))));
           
            credentialsSet = true;
        }
        #region "APIREST"
        public async Task<string> Tweet(string message)
        {
            checkCredentials();

            var data = new Dictionary<string, string>
            {
                { "status", message },{ "trim_user", "1" }
            };

            return await SendToTwitter("statuses/update.json", data, UseCase.Tweet);
        }

        public async Task<string> DirectMessage(string user, string message)
        {
            checkCredentials();
            var data = new Dictionary<string, string>
            {
                { "status", String.Format("D {0} {1}", user, message) },{ "trim_user", "1" }
            };
            return await SendToTwitter("statuses/update.json", data, UseCase.DirectMessage);
        }

        public async Task<string> ReTweetLastMessage(string user)
        {
            checkCredentials();
            var data = new Dictionary<string, string>
            {
                { "status", String.Format("RETWEET {0}", user) },{"trim_user", "1" }
            };
            return await SendToTwitter("statuses/update.json", data, UseCase.ReTweet);
        }

        public async Task<string> ReTweetMessage(long tweetId)
        {
            checkCredentials();
            var data = new Dictionary<string, string>
            {
                {"trim_user", "1" }
            };
            return await SendToTwitterDoWork(String.Format("statuses/retweet/{0}.json", tweetId.ToString()));
        }

        public async Task<string> ReTweetMessage(long tweetId, string message)
        {
            checkCredentials();
            var data = new Dictionary<string, string>
            {
                { "status", String.Format("{0} " +  baseUrlTweetLinks + "{1}", message, tweetId.ToString()) },{"trim_user", "1" }
            };
            return await SendToTwitterDoWork("statuses/update.json", data);
        }
        #endregion

        #region "Public Stream API"

        public void GetTweetsByHashtags(IEnumerable<string> hashtags, IEnumerable<string> languages, IEnumerable<MapBoxCoordinates> mapBoxCoordinates)
        {
            checkCredentials();

            if ((hashtags == null || hashtags.Count() == 0) 
                && (languages == null || languages.Count() == 0) 
                && (mapBoxCoordinates == null || mapBoxCoordinates.Count() == 0))
            {
                throw new ArgumentException("At least one keyword must exists.");
            }

            hashtags = (hashtags == null) ? new List<string>() : hashtags;
            languages = (languages == null) ? new List<string>() : languages;
            mapBoxCoordinates = (mapBoxCoordinates == null) ? new List<MapBoxCoordinates>() : mapBoxCoordinates;

            string keywords = String.Join(",", hashtags.Select(str => str.Trim()));
            string langs = String.Join(",", languages.Select(str => str.Trim()));

            string coordinates = String.Join(",", mapBoxCoordinates.Select(
                coord => String.Format(new CultureInfo("en-US"), "{0:0.000000},{1:0.000000},{2:0.000000},{3:0.000000}", coord.longitude1, coord.latitude1, coord.longitude2, coord.latitude2)));

            var data = new Dictionary<string, string>();

            if (keywords != "")
            {
                data.Add("track", keywords);
            }
            if (langs != "")
            {
                data.Add("language", langs);
            }
            if (coordinates != "")
            {
                data.Add("locations", coordinates);
            }

            string url = "statuses/filter.json";

            SendToStreamTwitterDoWork(url, data);
        }

        public void GetTweetsByHashtags(IEnumerable<string> hashtags, IEnumerable<string> languages)
        {
            GetTweetsByHashtags(hashtags, languages, null);
        }

        public void GetTweetsByHashtags(IEnumerable<string> hashtags)
        {
            GetTweetsByHashtags(hashtags, null, null);
        }
        #endregion
        private void checkCredentials()
        {
            if (!credentialsSet)
            {
                throw new ArgumentNullException("No Twitter credentials found. Set first credentials using setCredentials().");
            }
        }

        private async Task<string> SendToTwitter(string url, Dictionary<string, string> data, UseCase useCase)
        {
            switch (useCase)
            {
                case UseCase.DirectMessage:
                case UseCase.ReTweet:
                    {
                        data.Add("enable_dm_commands", "true");
                        break;
                    }
                case UseCase.Tweet:
                    {
                        data.Add("enable_dm_commands", "false");
                        break;
                    }                    
            }
            return await SendToTwitterDoWork(url, data);
        }

        private async Task<string> SendToTwitterDoWork(string url)
        {
            Dictionary<string, string> data = new Dictionary<string, string>();
            return await SendToTwitterDoWork(url, data);
        }
        private async Task<string> SendToTwitterDoWork(string url, Dictionary<string, string> data)
        {
            url = baseAPIRestUrl + url;
            return await ConsumeAPIDoWork(url, data);
        }
        private void SendToStreamTwitterDoWork(string url, Dictionary<string, string> data)
        {
            url = basePublicStreamAPI + url;
            ConsumeStreamAPIDoWork(url, data);
        }
        private async void ConsumeStreamAPIDoWork(string url, Dictionary<string, string> data)
        {
            AddOauthParameters(url, data);

            // Build the OAuth HTTP Header from the data.
            string oAuthHeader = GenerateOAuthHeader(data, url);
                        
            MemoryStream ms = new MemoryStream();
            byte[] postParameters = Encoding.UTF8.GetBytes(String.Join("&", data.Where(kvp => !kvp.Key.StartsWith("oauth_")).Select(kvp => kvp.Key + "=" + kvp.Value)));

            using (StreamContent streamContent = new StreamContent(new MemoryStream(postParameters)))
            {
                streamContent.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
                streamContent.Headers.Add("Content-Length", postParameters.Length.ToString());

                using (HttpClient httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromMilliseconds(Timeout.Infinite);
                    httpClient.DefaultRequestHeaders.Add("Authorization", oAuthHeader);
                    httpClient.BaseAddress = new Uri(url);

                    var request = new HttpRequestMessage(HttpMethod.Post, url);

                    request.Content = streamContent;

                    await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ContinueWith(async responseTask =>
                    {
                        using (HttpResponseMessage response = responseTask.Result)
                        {
                            await response.Content.ReadAsStreamAsync().ContinueWith(streamTask =>
                            {
                                using (StreamReader streamReader = new StreamReader(streamTask.Result))
                                {
                                    while (!streamReader.EndOfStream)
                                    {
                                        string json = streamReader.ReadLine();
                                        if (SerializationServices.getInstance.tryParse<Tweet>(json, out Tweet tweet))
                                        {
                                            streamTweetByHashTagEvent?.Invoke(this, new TweetStreamArgs(tweet));
                                        }
                                    }
                                }
                            });
                        }
                    });

                }
            }
        }
                
        private async Task<string> ConsumeAPIDoWork(string url, Dictionary<string, string> data)
        {
            AddOauthParameters(url, data);

            // Build the OAuth HTTP Header from the data.
            string oAuthHeader = GenerateOAuthHeader(data, url);

            // Build the form data (exclude OAuth stuff that's already in the header).
            var formData = new FormUrlEncodedContent(data.Where(kvp => !kvp.Key.StartsWith("oauth_")));

            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.Add("Authorization", oAuthHeader);

                HttpResponseMessage httpResp = await http.PostAsync(url, formData);
                string respBody = await httpResp.Content.ReadAsStringAsync();

                return respBody;
            }

        }
               
        private string GenerateSignature(string url, Dictionary<string, string> data)
        {
            var sigString = string.Join(
                "&",
                data
                    .Union(data)
                    .Select(kvp => string.Format("{0}={1}", Uri.EscapeDataString(kvp.Key), Uri.EscapeDataString(kvp.Value)))
                    .OrderBy(s => s)
            );

            var fullSigData = string.Format(
                "{0}&{1}&{2}",
                "POST",
                Uri.EscapeDataString(url),
                Uri.EscapeDataString(sigString.ToString())
            );

            return Convert.ToBase64String(sigHasher.ComputeHash(new ASCIIEncoding().GetBytes(fullSigData.ToString())));
        }

        private void AddOauthParameters(string url, Dictionary<string, string> data)
        {
            var timestamp = (int)((DateTime.UtcNow - epochUtc).TotalSeconds);

            data.Add("oauth_consumer_key", oauth_consumer_key);
            data.Add("oauth_nonce", Convert.ToBase64String(Encoding.ASCII.GetBytes(oauth_consumer_key + ":" + timestamp)));
            data.Add("oauth_signature_method", oauth_signature_method);
            data.Add("oauth_timestamp", timestamp.ToString());
            data.Add("oauth_token", oauth_token);
            data.Add("oauth_version", oauth_version);

            // Generate the OAuth signature and add it to our payload.
            data.Add("oauth_signature", GenerateSignature(url, data));
        }

        private string GenerateOAuthHeader(Dictionary<string, string> data, string url)
        {
            string result = "OAuth " + string.Join(
                ", ",
                data
                    .Where(kvp => kvp.Key.StartsWith("oauth_"))
                    .Select(kvp => string.Format("{0}=\"{1}\"", Uri.EscapeDataString(kvp.Key), Uri.EscapeDataString(kvp.Value)))
                    .OrderBy(s => s)
            );
            
            return result;
        }

    }    
}
