using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web;
using ChicagoSTDriveMgr.Config;
using NLog;

namespace ChicagoSTDriveMgr.Helpers
{
    internal class PhotoHostingHelper
    {
        public const string UriProtocol = "http";
        public const string FileContentKey = "File";
        private readonly static HttpClient _client;
        private const string HttpHeaderFieldNameRequireAuthentication = "X-RequireAuthentication";
        private const string HttpHeaderCompanyIdRequireAuthentication = "X-Company-ID";
        private const string HttpHeaderFieldNameFileTags = "X-FileTags";

        public static Dictionary<string, string> AddHeaders = new Dictionary<string, string>();
        static PhotoHostingHelper()
        {
            _client = new HttpClient();
            //https://techblog.willshouse.com/2012/01/03/most-common-user-agents/
            //https://github.com/browscap/browscap/issues/113
            _client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/4.0 (compatible; MSIE 7.0; Windows NT 6.3; Trident/7.0; .NET4.0E; .NET4.0C)");
        }

        private static string CreateServerFileUri(string photoExchangeAddress, string srcFileName, bool isNew = false)
        {
            var fileName = Path.GetFileNameWithoutExtension(srcFileName);

            if (string.IsNullOrWhiteSpace(photoExchangeAddress))
                return null;

            return $"{photoExchangeAddress}{(photoExchangeAddress.EndsWith(Path.AltDirectorySeparatorChar.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal) ? string.Empty : Path.AltDirectorySeparatorChar.ToString(CultureInfo.InvariantCulture))}{(isNew ? Guid.NewGuid().ToString().Replace("-", "") : fileName)}";
        }

        public static bool CopyContentIntoFileOnServerByUri(AppConfig appConfig, long idDistributor, string srcFileName, byte[] fileContent, string contentType, out string destFileName, out string errorMessage, bool isNew, string filetag)
        {
            destFileName = srcFileName;
            errorMessage = null;

            destFileName = destFileName?.Trim();

            if (fileContent == null || fileContent.Length == 0)
            {
                errorMessage = "file fileContent is empry";
                return false;
            }
            if (string.IsNullOrEmpty(filetag))
            {
                errorMessage = "filetag is empry";
                return false;
            }

            if (!destFileName.StartsWith(UriProtocol, StringComparison.Ordinal))
                destFileName = CreateServerFileUri(appConfig.PhotoHostingUri.ContainsKey(idDistributor) ? appConfig.PhotoHostingUri[idDistributor] : null, destFileName, isNew);

            if (string.IsNullOrWhiteSpace(destFileName))
            {
                errorMessage = "destFileName is empry";
                return false;
            }

            string extension;

            if (!string.IsNullOrWhiteSpace(extension = Path.GetExtension(destFileName)))
                destFileName = destFileName.Remove(destFileName.LastIndexOf(extension, StringComparison.Ordinal));
            var httpHeaderCompanyIdRequireAuthentication = new KeyValuePair<string, string>(HttpHeaderCompanyIdRequireAuthentication, appConfig.CompanyId);
            if (!string.IsNullOrWhiteSpace(appConfig.CompanyId) && !AddHeaders.Contains(httpHeaderCompanyIdRequireAuthentication))
                AddHeaders.Add(HttpHeaderCompanyIdRequireAuthentication, appConfig.CompanyId);
            if (appConfig.UploadMM || appConfig.UploadPromo)
            {
                var httpHeaderFieldNameRequireAuthentication = new KeyValuePair<string, string>(HttpHeaderFieldNameRequireAuthentication, true.ToString());
                if (!AddHeaders.Contains(httpHeaderFieldNameRequireAuthentication))
                    AddHeaders.Add(HttpHeaderFieldNameRequireAuthentication, true.ToString());
                else
                    AddHeaders[HttpHeaderFieldNameRequireAuthentication] = true.ToString();
            }
            if (!string.IsNullOrWhiteSpace(filetag))
            {
                var httpHeaderFieldNameFileTags = new KeyValuePair<string, string>(HttpHeaderFieldNameFileTags, filetag);
                if (!AddHeaders.Contains(httpHeaderFieldNameFileTags))
                    AddHeaders.Add(HttpHeaderFieldNameFileTags, filetag);
                else
                    AddHeaders[HttpHeaderFieldNameFileTags] = filetag;
            }
            var resultUpload = HttpUploadFile(appConfig, destFileName, Path.GetFileName(srcFileName), fileContent, contentType, isNew ? "POST" : "PUT", AddHeaders);
            errorMessage += " " + resultUpload.ErrorMessage;
            return resultUpload.Result && resultUpload.StatusCode == (isNew ? HttpStatusCode.Created : HttpStatusCode.OK);
        }

        //TODO: метод - практически полная копипаста Chicago2.Core.Helpers.Utils.HttpUploadFile
        public static ResultMessage HttpUploadFile(AppConfig appConfig, string url, string fileName, byte[] fileContent, 
            string contentType, string method, Dictionary<string, string> additionalHeaders)
        {
            var result = new ResultMessage { StatusCode = HttpStatusCode.InternalServerError };

            Core.WriteToLog(appConfig.Logger, LogLevel.Debug, $"Uploading \"{fileName}\" starting...");
            var startTime = DateTime.Now;
            try
            {
                using (var requestContent = new MultipartFormDataContent($"{new string('-', 27)}{DateTime.Now.Ticks.ToString("x")}"))
                {
                    using (var imageContent = new ByteArrayContent(fileContent))
                    {
                        imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
                        if (!string.IsNullOrEmpty(fileName))
                            requestContent.Add(imageContent, "File", Path.GetFileName(fileName));
                        else
                            requestContent.Add(imageContent, "File");
                        if (additionalHeaders?.Any() == true)
                            foreach (var header in additionalHeaders)
                                requestContent.Headers.Add(header.Key, header.Value);

                        using (var response = string.Compare(method, HttpMethod.Put.Method, StringComparison.InvariantCultureIgnoreCase) == 0 ?
                            _client.PutAsync(url, requestContent) :
                            _client.PostAsync(url, requestContent))
                        {
                            result.StatusCode = response.Result.StatusCode;
                            if (result.StatusCode == HttpStatusCode.Forbidden)
                            {
                                result.ErrorMessage = "03 Forbidden – you don’t have permission to access on this server.";
                                Core.WriteToLog(appConfig.Logger, LogLevel.Debug, result.ErrorMessage);
                                result.Result = false;
                            }
                            else
                                result.Result = true;
                        }
                    }
                }
                return result;
            }
            catch (AggregateException e)
            {
                ProcessingAggregateException(appConfig, e, result);
            }
            catch (Exception e)
            {
                result.ErrorMessage = e.Message;
                Core.WriteToLog(appConfig.Logger, LogLevel.Debug, result.ErrorMessage);
            }
            Core.WriteToLog(appConfig.Logger, LogLevel.Debug, $"Uploading {method} \"{url}\" finished with statusCode \"{result.StatusCode}\" ({(int)result.StatusCode}) ({(DateTime.Now - startTime).TotalMilliseconds} ms)");
            return result;
        }

        internal protected static void ProcessingAggregateException(AppConfig appConfig, AggregateException e, ResultMessage result)
        {
            foreach (var ex in e.Flatten().InnerExceptions)
            {
                if (ex.InnerException is WebException)
                {
                    if ((ex.InnerException as WebException).Response != null)
                        result.StatusCode = (((WebException)ex.InnerException).Response as HttpWebResponse).StatusCode;
                }
                else if (ex.InnerException is HttpException)
                {
                    result.StatusCode = (HttpStatusCode)(ex.InnerException as HttpException).GetHttpCode();
                }
                result.ErrorMessage += " " + ex.Message + "; " + ex.InnerException?.Message;
                Core.WriteToLog(appConfig.Logger, LogLevel.Debug, ex.Message + "; " + ex.InnerException?.Message);
            }
        }



        //TODO: метод - практически полная копипаста Chicago2.Core.Helpers.Utils.LoadContentByUri
        public static byte[] HttpDownloadFile(AppConfig appConfig, string url, out string errorMessage)
        {
            errorMessage = String.Empty;

            if (String.IsNullOrWhiteSpace(url))
            {
                errorMessage = !url.ToLower().StartsWith(UriProtocol)
                    ? $"Invalid URL: \"{url}\""
                    : String.Empty;
                return null;
            }
            url = url.Trim();

            var extension = Path.GetExtension(url);

            if (!String.IsNullOrEmpty(extension))
                url = url.Replace(extension, String.Empty);

            var statusCode = HttpStatusCode.InternalServerError;

            Core.WriteToLog(appConfig.Logger, LogLevel.Debug, $"Downloading \"{url}\" starting...");
            var startTime = DateTime.Now;

            try
            {
                byte[] content = null;
                using (var response = _client.GetAsync(url).Result)
                {
                    if ((statusCode = response.StatusCode) == HttpStatusCode.OK)
                    {
                        if ((content = response.Content.ReadAsByteArrayAsync().Result) == null)
                            errorMessage = $"Response stream is null (\"{url}\")";
                    }
                    else
                        errorMessage = HttpWorkerRequest.GetStatusDescription((int)response.StatusCode);
                }
                return content;
            }
            catch (AggregateException e)
            {
                foreach (var ex in e.Flatten().InnerExceptions)
                {
                    if (ex.InnerException is WebException)
                    {
                        if ((ex.InnerException as WebException).Response != null)
                            statusCode = (((WebException)ex.InnerException).Response as HttpWebResponse).StatusCode;
                        Core.WriteToLog(appConfig.Logger, LogLevel.Error, errorMessage = ex.InnerException.Message);
                    }
                    else
                        errorMessage = $"{ex.GetType().Name}: \"{ex.Message}\"\r\n(\"{url}\")";
                }
            }
            catch (Exception e)
            {
                errorMessage = $"{e.GetType().Name}: \"{e.Message}\"\r\n(\"{url}\")";
            }
            Core.WriteToLog(appConfig.Logger, LogLevel.Debug, $"Downloading \"{url}\" finished with statusCode \"{statusCode}\" ({(int)statusCode}) ({(DateTime.Now - startTime).TotalMilliseconds} ms)");
            return null;
        }
    }
}