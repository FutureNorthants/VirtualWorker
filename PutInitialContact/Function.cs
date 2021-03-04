using System.Collections.Generic;
using Amazon.Lambda.Core;
using System;
using System.Threading.Tasks;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace PutInitialContact
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
        private static String tableName;

        private Secrets secrets = null;


        public async Task FunctionHandler(object input, ILambdaContext context)
        {
            if (await GetSecrets())
            {
                JObject o = JObject.Parse(input.ToString());
                caseReference = (string)o.SelectToken("CaseReference");
                taskToken = (string)o.SelectToken("TaskToken");  
                
                try
                {
                    if (context.InvokedFunctionArn.ToLower().Contains("prod"))
                    {
                        if (caseReference.ToLower().Contains("ema"))
                        {
                            tableName = secrets.wncEMACasesLive;
                            cxmEndPoint = secrets.cxmEndPointLive;
                            cxmAPIKey = secrets.cxmAPIKeyLive;
                        }
                        if (caseReference.ToLower().Contains("emn"))
                        {
                            tableName = secrets.nncEMNCasesLive;
                            cxmEndPoint = secrets.cxmEndPointLiveNorth;
                            cxmAPIKey = secrets.cxmAPIKeyLiveNorth;
                        }                        
                    }
                    else
                    {
                        if (caseReference.ToLower().Contains("ema"))
                        {
                            tableName = secrets.wncEMACasesTest;
                            cxmEndPoint = secrets.cxmEndPointTest;
                            cxmAPIKey = secrets.cxmAPIKeyTest;
                        }
                        if (caseReference.ToLower().Contains("emn"))
                        {
                            tableName = secrets.nncEMNCasesTest;
                            cxmEndPoint = secrets.cxmEndPointTestNorth;
                            cxmAPIKey = secrets.cxmAPIKeyTestNorth;
                        }
                    }
                }
                catch (Exception)
                {
                    if (caseReference.ToLower().Contains("ema"))
                    {
                        tableName = secrets.wncEMACasesTest;
                        cxmEndPoint = secrets.cxmEndPointTest;
                        cxmAPIKey = secrets.cxmAPIKeyTest;
                    }
                    if (caseReference.ToLower().Contains("emn"))
                    {
                        tableName = secrets.nncEMNCasesTest;
                        cxmEndPoint = secrets.cxmEndPointTestNorth;
                        cxmAPIKey = secrets.cxmAPIKeyTestNorth;
                    }
                }
                if (await GetCaseDetailsAsync())
                {
                    await SendSuccessAsync();
                }
            }
        }

        private async Task<Boolean> GetCaseDetailsAsync()
        {
            Boolean success = false;
            CaseDetails caseDetails = await GetCustomerContactAsync(cxmEndPoint, cxmAPIKey, caseReference, taskToken);
            try
            {
                if (!String.IsNullOrEmpty(caseDetails.customerContact))
                {
                    success = await StoreContactToDynamoAsync(caseReference, caseDetails.customerContact);
                }
            }
            catch (Exception error)
            {
                await SendFailureAsync(error.Message, "GetCaseDetailsAsync");
                Console.WriteLine("ERROR : GetCaseDetailsAsync :  " + error.Message);
                success = false; ;
            }
            return success;
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

        private async Task<CaseDetails> GetCustomerContactAsync(String cxmEndPoint, String cxmAPIKey, String caseReference, String taskToken)
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

        private async Task<Boolean> StoreContactToDynamoAsync(String caseReference, String initialContact)
        {
            try
            {
                AmazonDynamoDBClient dynamoDBClient = new AmazonDynamoDBClient(primaryRegion);
                UpdateItemRequest dynamoRequest = new UpdateItemRequest
                {
                    TableName = tableName,
                    Key = new Dictionary<string, AttributeValue>
                        {
                              { "CaseReference", new AttributeValue { S = caseReference }}
                        },
                    ExpressionAttributeNames = new Dictionary<string, string>()
                    {
                        {"#Field", "InitialContact"}
                    },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>()
                    {
                        {":Value",new AttributeValue {S = initialContact}}
                    },

                    UpdateExpression = "SET #Field = :Value"
                };
                await dynamoDBClient.UpdateItemAsync(dynamoRequest);
                return true;
            }
            catch (Exception error)
            {
                Console.WriteLine("ERROR : StoreContactToDynamoDB :" + error.Message);
                Console.WriteLine(error.StackTrace);
                return false;
            }
        }

    }
    class CaseDetails
    {
        public String customerContact { get; set; } = "";
    }

    public class Secrets
    {
        public string cxmEndPointTest { get; set; }
        public string cxmEndPointLive { get; set; }
        public string cxmAPIKeyTest { get; set; }
        public string cxmAPIKeyLive { get; set; }
        public string wncEMACasesLive { get; set; }
        public string wncEMACasesTest { get; set; }
        public string nncEMNCasesLive { get; set; }
        public string nncEMNCasesTest { get; set; }
        public string cxmEndPointLiveNorth { get; set; }
        public string cxmEndPointTestNorth { get; set; }
        public string cxmAPIKeyTestNorth { get; set; }
        public string cxmAPIKeyLiveNorth { get; set; }
    }
}