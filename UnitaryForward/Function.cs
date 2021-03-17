using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Amazon;
using Amazon.Lambda.Core;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace UnitaryForward
{
    public class Function
    {
        private static readonly RegionEndpoint primaryRegion = RegionEndpoint.EUWest2;
        private static readonly String secretName = "nbcGlobal";
        private static readonly String secretAlias = "AWSCURRENT";

        private static String caseReference;
        private static String taskToken;
        private static String cxmEndPoint;
        private static String cxmAPIKey;
        private static String cxmAPIName;
        private Boolean liveInstance = false;

        private Secrets secrets = null;

        public async Task FunctionHandler(object input, ILambdaContext context)
        {
            if (await GetSecrets())
            {
                JObject o = JObject.Parse(input.ToString());
                caseReference = (string)o.SelectToken("CaseReference");
                taskToken = (string)o.SelectToken("TaskToken");
                Console.WriteLine("caseReference : " + caseReference);
                try
                {
                    if (context.InvokedFunctionArn.ToLower().Contains("prod"))
                    {
                        liveInstance = true;
                    }
                }
                catch (Exception)
                { }

                if (liveInstance)
                {
                    if (caseReference.ToLower().Contains("ema"))
                    {
                        cxmEndPoint = secrets.cxmEndPointLive;
                        cxmAPIKey = secrets.cxmAPIKeyLive;
                        cxmAPIName = secrets.cxmAPINameWest;
                    }
                    if (caseReference.ToLower().Contains("emn"))
                    {
                        cxmEndPoint = secrets.cxmEndPointLiveNorth;
                        cxmAPIKey = secrets.cxmAPIKeyLiveNorth;
                        cxmAPIName = secrets.cxmAPINameNorth;
                    }
                }
                else
                {
                    if (caseReference.ToLower().Contains("ema"))
                    {
                        cxmEndPoint = secrets.cxmEndPointTest;
                        cxmAPIKey = secrets.cxmAPIKeyTest;
                        cxmAPIName = secrets.cxmAPINameWest;
                    }
                    if (caseReference.ToLower().Contains("emn"))
                    {
                        cxmEndPoint = secrets.cxmEndPointTestNorth;
                        cxmAPIKey = secrets.cxmAPIKeyTestNorth;
                        cxmAPIName = secrets.cxmAPINameNorth;

                    }
                }
                await ProcessCaseAsync();
                await SendSuccessAsync();
            }
            Console.WriteLine("Completed");
        }

        private async Task<Boolean> GetSecrets()
        {
            IAmazonSecretsManager client = new AmazonSecretsManagerClient(primaryRegion);

            GetSecretValueRequest request = new GetSecretValueRequest();
            request.SecretId = secretName;
            request.VersionStage = secretAlias;

            try
            {
                GetSecretValueResponse response = await client.GetSecretValueAsync(request);
                secrets = JsonConvert.DeserializeObject<Secrets>(response.SecretString);
                return true;
            }
            catch (Exception error)
            {
                await SendFailureAsync("GetSecrets", error.Message);
                Console.WriteLine("ERROR : GetSecretValue : " + error.Message);
                Console.WriteLine("ERROR : GetSecretValue : " + error.StackTrace);
                return false;
            }
        }

        private async Task<Boolean> ProcessCaseAsync()
        {
            Boolean success = true;
            try
            {
                UpdateCase("manual-review", true);
                await TransitionCaseAsync("awaiting-location-confirmation");
            }
            catch (Exception)
            {
                return false;
            }
            return success;
        }

        private async Task<Boolean> TransitionCaseAsync(String transitionTo)
        {
            Boolean success = false;
            HttpClient cxmClient = new HttpClient();
            cxmClient.BaseAddress = new Uri(cxmEndPoint);
            string requestParameters = "key=" + cxmAPIKey;
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/api/service-api/" + cxmAPIName + "/case/" + caseReference + "/transition/" + transitionTo + "?" + requestParameters);
            HttpResponseMessage response = cxmClient.SendAsync(request).Result;
            if (response.IsSuccessStatusCode)
            {
                success = true;
            }
            else
            {
                await SendFailureAsync("CXM Failed to transiton : " + caseReference + " to " + transitionTo, "TransitionCaseAsync");
                Console.WriteLine("ERROR CXM Failed to transiton : " + caseReference + " to " + transitionTo);
            }
            return success;
        }

        private Boolean UpdateCase(String fieldName, Boolean fieldValue)
        {
            Boolean success = true;

            String data = "{\"" + fieldName + "\":" + fieldValue.ToString().ToLower() +
                          "}";

            Console.WriteLine($"PATCH payload : " + data);

            String url = cxmEndPoint + "/api/service-api/" + cxmAPIName + "/case/" + caseReference + "/edit?key=" + cxmAPIKey;
            Encoding encoding = Encoding.Default;
            HttpWebRequest patchRequest = (HttpWebRequest)WebRequest.Create(url);
            patchRequest.Method = "PATCH";
            patchRequest.ContentType = "application/json; charset=utf-8";
            byte[] buffer = encoding.GetBytes(data);
            Stream dataStream = patchRequest.GetRequestStream();
            dataStream.Write(buffer, 0, buffer.Length);
            dataStream.Close();
            try
            {
                HttpWebResponse patchResponse = (HttpWebResponse)patchRequest.GetResponse();
                String result = "";
                using (StreamReader reader = new StreamReader(patchResponse.GetResponseStream(), Encoding.Default))
                {
                    result = reader.ReadToEnd();
                }
            }
            catch (Exception error)
            {
                success = false;
                Console.WriteLine(caseReference + " : " + error.ToString());
                Console.WriteLine(caseReference + " : Error updating CXM field " + fieldName + " with message : " + fieldValue);
            }
            return success;
        }

        private async Task SendSuccessAsync()
        {
            AmazonStepFunctionsClient client = new AmazonStepFunctionsClient();
            SendTaskSuccessRequest successRequest = new SendTaskSuccessRequest();
            successRequest.TaskToken = taskToken;
            Dictionary<String, String> result = new Dictionary<String, String>
            {
                { "Result"  , "Success"  },
                { "Message" , "Completed"}
            };

            string requestOutput = JsonConvert.SerializeObject(result, Formatting.Indented);
            successRequest.Output = requestOutput;
            try
            {
                await client.SendTaskSuccessAsync(successRequest);
            }
            catch (Exception error)
            {
                Console.WriteLine("ERROR : SendSuccessAsync : " + error.Message);
                Console.WriteLine("ERROR : SendSuccessAsync : " + error.StackTrace);
            }
            await Task.CompletedTask;
        }

        private async Task SendFailureAsync(String failureCause, String failureError)
        {
            AmazonStepFunctionsClient client = new AmazonStepFunctionsClient();
            SendTaskFailureRequest failureRequest = new SendTaskFailureRequest();
            failureRequest.Cause = failureCause;
            failureRequest.Error = failureError;
            failureRequest.TaskToken = taskToken;

            try
            {
                await client.SendTaskFailureAsync(failureRequest);
            }
            catch (Exception error)
            {
                Console.WriteLine("ERROR : SendFailureAsync : " + error.Message);
                Console.WriteLine("ERROR : SendFailureAsync : " + error.StackTrace);
            }
            await Task.CompletedTask;
        }
    }

    public class Secrets
    {
        public String cxmEndPointTest { get; set; }
        public String cxmEndPointLive { get; set; }
        public String cxmAPIKeyTest { get; set; }
        public String cxmAPIKeyLive { get; set; }
        public String cxmEndPointTestNorth { get; set; }
        public String cxmEndPointLiveNorth { get; set; }
        public String cxmAPIKeyTestNorth { get; set; }
        public String cxmAPIKeyLiveNorth { get; set; }
        public String cxmAPINameNorth { get; set; }
        public String cxmAPINameWest { get; set; }
    }
}