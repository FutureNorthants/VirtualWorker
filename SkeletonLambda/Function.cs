using System.Collections.Generic;
using Amazon.Lambda.Core;
using System;
using Amazon;
using System.Threading.Tasks;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace SkeletonLambda
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
        private static String cxmAPICaseType;

        private Secrets secrets = null;
        private Boolean liveInstance = false;
        private Boolean west = true;


        public async Task FunctionHandler(object input, ILambdaContext context)
        {
            if (await GetSecrets())
            {
                liveInstance = false;
                west = true;
                JObject o = JObject.Parse(input.ToString());
                caseReference = (string)o.SelectToken("CaseReference");
                taskToken = (string)o.SelectToken("TaskToken");

                try
                {
                    if (context.InvokedFunctionArn.ToLower().Contains("prod"))
                    {
                        Console.WriteLine("Prod version");
                        liveInstance = true;
                    }
                }
                catch (Exception){}

                if (caseReference.ToLower().Contains("emn"))
                {
                    west = false;
                }

                if (liveInstance)
                {
                    if (west)
                    {
                        cxmEndPoint = secrets.cxmEndPointLive;
                        cxmAPIKey = secrets.cxmAPIKeyLive;
                        cxmAPIName = secrets.cxmAPINameWest;
                        cxmAPICaseType = secrets.cxmAPICaseTypeWestLive;
                    }
                    else
                    {
                        cxmEndPoint = secrets.cxmEndPointLiveNorth;
                        cxmAPIKey = secrets.cxmAPIKeyLiveNorth;
                        cxmAPIName = secrets.cxmAPINameNorth;
                        cxmAPICaseType = secrets.cxmAPICaseTypeNorthLive;
                    }

                }
                else
                {
                    if (west)
                    {
                        cxmEndPoint = secrets.cxmEndPointTest;
                        cxmAPIKey = secrets.cxmAPIKeyTest;
                        cxmAPIName = secrets.cxmAPINameWest;
                        cxmAPICaseType = secrets.cxmAPICaseTypeWest;
                    }
                    else
                    {
                        cxmEndPoint = secrets.cxmEndPointTestNorth;
                        cxmAPIKey = secrets.cxmAPIKeyTestNorth;
                        cxmAPIName = secrets.cxmAPINameNorth;
                        cxmAPICaseType = secrets.cxmAPICaseTypeNorth;
                    }
                }

                if (await ProcessCaseAsync())
                {
                    await SendSuccessAsync();
                }
            }
        }

        private async Task<Boolean> ProcessCaseAsync()
        {
            CaseDetails caseDetails = await GetCaseAsync(cxmEndPoint, cxmAPIKey, caseReference, taskToken);
            //Start of your code
            await UpdateCaseDetailsAsync("your-field", "your-field-value");
            //End of your code
            return true;
        }


        private async Task<Boolean> UpdateCaseDetailsAsync(String fieldName,String fieldValue)
        {
            HttpClient cxmClient = new HttpClient();
            cxmClient.BaseAddress = new Uri(cxmEndPoint);
            String uri = "/api/service-api/" + cxmAPIName + "/case/" + caseReference + "/edit?key=" + cxmAPIKey;
            Dictionary<string, string> cxmPayload = new Dictionary<string, string>
            {
                { fieldName, fieldValue }
            };
            String json = JsonConvert.SerializeObject(cxmPayload, Formatting.Indented);
            StringContent content = new StringContent(json);
            HttpResponseMessage response = await cxmClient.PatchAsync(uri, content);
            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (Exception error)
            {
                await SendFailureAsync(error.Message, "UpdateCaseDetailsAsync");
                Console.WriteLine("ERROR : UpdateCaseDetailsAsync :  " + error.Message);
                return false;
            }
            return true;
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


        private async Task<CaseDetails> GetCaseAsync(String cxmEndPoint, String cxmAPIKey, String caseReference, String taskToken)
        {
            CaseDetails caseDetails = new CaseDetails();
            HttpClient cxmClient = new HttpClient();
            cxmClient.BaseAddress = new Uri(cxmEndPoint);
            String requestParameters = "key=" + cxmAPIKey;
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "/api/service-api/norbert/case/" + caseReference + "?" + requestParameters);
            try
            {
                HttpResponseMessage response = cxmClient.SendAsync(request).Result;
                if (response.IsSuccessStatusCode)
                {
                    HttpContent responseContent = response.Content;
                    String responseString = responseContent.ReadAsStringAsync().Result;
                    JObject caseSearch = JObject.Parse(responseString);
                    caseDetails.customerContact = (String)caseSearch.SelectToken("values.enquiry_details");
                }
                else
                {
                    await SendFailureAsync("Getting case details for " + caseReference, response.StatusCode.ToString());
                    Console.WriteLine("ERROR : GetStaffResponseAsync : " + request.ToString());
                    Console.WriteLine("ERROR : GetStaffResponseAsync : " + response.StatusCode.ToString());
                }
            }
            catch (Exception error)
            {
                await SendFailureAsync("Getting case details for " + caseReference, error.Message);
                Console.WriteLine("ERROR : GetStaffResponseAsync : " + error.StackTrace);
            }
            return caseDetails;
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
    class CaseDetails
    {
        public String customerContact { get; set; } = "";
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
        public String cxmAPICaseTypeNorth { get; set; }
        public String cxmAPICaseTypeWest { get; set; }
        public String cxmAPICaseTypeWestLive { get; set; }
        public String cxmAPICaseTypeNorthLive { get; set; }
        public String homeDomain { get; set; }
        public String botPersona1 { get; set; }
        public String botPersona2 { get; set; }
        public String loopPreventIdentifier { get; set; }
        public String sigParseKey { get; set; }
        public String wncAttachmentBucketLive { get; set; }
        public String wncAttachmentBucketTest { get; set; }
        public String nncAttachmentBucketLive { get; set; }
        public String nncAttachmentBucketTest { get; set; }
        public String wncEMACasesLive { get; set; }
        public String wncEMACasesTest { get; set; }
        public String nncEMNCasesLive { get; set; }
        public String nncEMNCasesTest { get; set; }
        public String WNCContactUsMappingTable { get; set; }
        public String NNCContactUsMappingTable { get; set; }
    }
}