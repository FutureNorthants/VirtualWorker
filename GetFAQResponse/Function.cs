using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace GetFAQResponse
{
    public class Function
    {
        private static readonly RegionEndpoint primaryRegion = RegionEndpoint.EUWest2;
        private static readonly String secretName = "nbcGlobal";
        private static readonly String secretAlias = "AWSCURRENT";

        private static Boolean continueProcessing = true;
        private static String caseReference;
        private static String taskToken;
        private static String cxmEndPoint;
        private static String cxmAPIKey;
        private static String tableName;
        private static String cxmAPIName;
        private static CaseDetails caseDetails;
        private Secrets secrets = null;
        private Boolean liveInstance = false;
        private Boolean west = true;

        private static int minConfidenceLevel;
        private static int minAutoRespondLevel;
        private static String qnaURL;
        private static String qnaAuthorization;

        public async Task FunctionHandler(object input, ILambdaContext context)
        {
            if (await GetSecrets())
            {
                try
                {
                    if (!Int32.TryParse(secrets.minResponseConfidenceLevel, out minConfidenceLevel))
                    {
                        await SendFailureAsync("minConfidenceLevel not numeric : " + secrets.minResponseConfidenceLevel, "Secrets Error");
                        Console.WriteLine("ERROR : minConfidenceLevel not numeric : " + secrets.minResponseConfidenceLevel);
                        continueProcessing = false;
                    }
                }
                catch (Exception error)
                {
                    await SendFailureAsync("minConfidenceLevel Parse Error : " + secrets.minResponseConfidenceLevel, error.Message);
                    Console.WriteLine("ERROR : minConfidenceLevel Parse Error : " + secrets.minResponseConfidenceLevel + " : " + error.Message);
                    continueProcessing = false;
                }

                try
                {
                    if (!Int32.TryParse(secrets.minAutoResponseLevel, out minAutoRespondLevel))
                    {
                        await SendFailureAsync("minAutoRespondLevel not numeric : " + secrets.minAutoResponseLevel, "Lambda Parameter Error");
                        Console.WriteLine("ERROR : minAutoRespondLevel not numeric : " + secrets.minAutoResponseLevel);
                        continueProcessing = false;
                    }
                }
                catch (Exception error)
                {
                    await SendFailureAsync("minAutoRespondLevel Parse Error : " + secrets.minAutoResponseLevel, error.Message);
                    Console.WriteLine("ERROR : minAutoRespondLevel Parse Error : " + secrets.minAutoResponseLevel + " : " + error.Message);
                    continueProcessing = false;
                }


                if (String.IsNullOrEmpty(secrets.nbcQNAurl))
                {
                    await SendFailureAsync("qnaURL parameter not set", "Secrets Parameter Error");
                    Console.WriteLine("ERROR : qnaURL parameter not set");
                    continueProcessing = false;
                }
                else
                {
                    qnaURL = secrets.nbcQNAurl;
                }
                if (String.IsNullOrEmpty(secrets.nbcQNAauth))
                {
                    await SendFailureAsync("qnaAuthorization parameter not set", "Secrets Parameter Error");
                    Console.WriteLine("ERROR : qnaAuthorization parameter not set");
                    continueProcessing = false;
                }
                else
                {
                    qnaAuthorization = secrets.nbcQNAauth;
                }
                if (continueProcessing)
                {
                    JObject o = JObject.Parse(input.ToString());
                    caseReference = (string)o.SelectToken("CaseReference");
                    Console.WriteLine(caseReference + " : Started");
                    taskToken = (string)o.SelectToken("TaskToken");
                    try
                    {
                        if (context.InvokedFunctionArn.ToLower().Contains("prod"))
                        {
                            Console.WriteLine(caseReference + " : Prod version");
                            liveInstance = true;
                        }
                    }
                    catch (Exception)
                    {
                    }

                    if (liveInstance)
                    {
                        if (caseReference.ToLower().Contains("ema"))
                        {
                            tableName = secrets.wncEMACasesLive;
                            cxmEndPoint = secrets.cxmEndPointLive;
                            cxmAPIKey = secrets.cxmAPIKeyLive;
                            cxmAPIName = secrets.cxmAPINameWest;
                        }
                        if (caseReference.ToLower().Contains("emn"))
                        {
                            west = false;
                            tableName = secrets.nncEMNCasesLive;
                            cxmEndPoint = secrets.cxmEndPointLiveNorth;
                            cxmAPIKey = secrets.cxmAPIKeyLiveNorth;
                            cxmAPIName = secrets.cxmAPINameNorth;
                        }

                        caseDetails = await GetCaseDetailsAsync();
                        if (west)
                        {
                            if (await GetProposedResponse() && await UpdateCaseDetailsAsync() && await TransitionCaseAsync())
                            {
                                await SendSuccessAsync();
                            }
                        }
                        else
                        {
                            await TransitionCaseAsync();
                            await SendSuccessAsync();
                        }
                    }
                    else
                    {
                        if (caseReference.ToLower().Contains("ema"))
                        {
                            tableName = secrets.wncEMACasesTest;
                            cxmEndPoint = secrets.cxmEndPointTest;
                            cxmAPIKey = secrets.cxmAPIKeyTest;
                            cxmAPIName = secrets.cxmAPINameWest;
                        }
                        if (caseReference.ToLower().Contains("emn"))
                        {
                            west = false;
                            tableName = secrets.nncEMNCasesTest;
                            cxmEndPoint = secrets.cxmEndPointTestNorth;
                            cxmAPIKey = secrets.cxmAPIKeyTestNorth;
                            cxmAPIName = secrets.cxmAPINameNorth;
                        }

                        caseDetails = await GetCaseDetailsAsync();
                        if (west)
                        {
                            if (await GetProposedResponse() && await UpdateCaseDetailsAsync() && await TransitionCaseAsync())
                            {
                                await SendSuccessAsync();
                            }
                        }
                        else
                        {
                            await TransitionCaseAsync();
                            await SendSuccessAsync();
                        }
                    }
                }
            }
        }

        private async Task<CaseDetails> GetCaseDetailsAsync()
        {
            CaseDetails caseDetails = new CaseDetails();
            HttpClient cxmClient = new HttpClient();
            cxmClient.BaseAddress = new Uri(cxmEndPoint);
            String requestParameters = "key=" + cxmAPIKey;
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "/api/service-api/" + cxmAPIName + "/case/" + caseReference + "?" + requestParameters);
            try
            {
                HttpResponseMessage response = cxmClient.SendAsync(request).Result;
                if (response.IsSuccessStatusCode)
                {
                    HttpContent responseContent = response.Content;
                    String responseString = responseContent.ReadAsStringAsync().Result;
                    JObject caseSearch = JObject.Parse(responseString);
                    caseDetails.customerContact = (String)caseSearch.SelectToken("values.enquiry_details");
                    try
                    {
                        caseDetails.bundle = (String)caseSearch.SelectToken("values.merge_into_pdf");
                    }
                    catch (Exception) { }

                    String temp = (String)caseSearch.SelectToken("values.unitary");
                    try
                    {
                        if (((String)caseSearch.SelectToken("values.unitary")).ToLower().Equals("true"))
                        {
                            caseDetails.unitary = true;
                        }
                        else
                        {
                            caseDetails.unitary = false;
                        }
                    }
                    catch (Exception) { }
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

        private async Task<Boolean> GetProposedResponse()
        {
            HttpClient qnaClient = new HttpClient();
            qnaClient.DefaultRequestHeaders.Add("Authorization", qnaAuthorization);
            HttpResponseMessage responseMessage = await qnaClient.PostAsync(
                 qnaURL,
                 new StringContent("{'question':'" + HttpUtility.UrlEncode(caseDetails.customerContact) + "'}", Encoding.UTF8, "application/json"));
            responseMessage.EnsureSuccessStatusCode();
            string responseBody = await responseMessage.Content.ReadAsStringAsync();
            dynamic jsonResponse = JObject.Parse(responseBody);
            caseDetails.proposedResponse = jsonResponse.answers[0].answer;
            int score = 0;
            try
            {
                if (Int32.TryParse(((String)(jsonResponse.answers[0].score)).Split('.')[0], out score))
                {
                    caseDetails.proposedResponseConfidence = score;
                }
                else
                {
                    await SendFailureAsync("qna Score not numeric : " + jsonResponse.answers[0].score, "QNA Error");
                    Console.WriteLine("ERROR : qna Score not numeric : " + jsonResponse.answers[0].score);
                    return false;
                }
            }
            catch (Exception error)
            {
                await SendFailureAsync("qna Score Parse Error : " + jsonResponse.answers[0].score, error.Message);
                Console.WriteLine("ERROR : qna Score Parse Error : " + jsonResponse.answers[0].score + " : " + error.Message);
                return false;
            }
            return true;
        }

        private async Task<Boolean> UpdateCaseDetailsAsync()
        {
            HttpClient cxmClient = new HttpClient();
            cxmClient.BaseAddress = new Uri(cxmEndPoint);
            String uri = "/api/service-api/" + cxmAPIName + "/case/" + caseReference + "/edit?key=" + cxmAPIKey;
            Dictionary<string, string> cxmPayload;
            if (caseDetails.proposedResponseConfidence < minConfidenceLevel)
            {
                cxmPayload = new Dictionary<string, string>
                {
                    { "response-confidence", caseDetails.proposedResponseConfidence.ToString()}
                };
            }
            else
            {
                //TODO secrets!
                if (minAutoRespondLevel < caseDetails.proposedResponseConfidence)
                //if (90 < caseDetails.proposedResponseConfidence)
                {
                    cxmPayload = new Dictionary<string, string>
                    {
                        { "contact-response", caseDetails.proposedResponse },
                        { "response-confidence", caseDetails.proposedResponseConfidence.ToString()},
                        { "new-case-status", "close"},
                        { "staff-name", "Norbert"}
                    };
                }
                else
                {
                    cxmPayload = new Dictionary<string, string>
                    {
                        { "contact-response", caseDetails.proposedResponse },
                        { "response-confidence", caseDetails.proposedResponseConfidence.ToString()}
                    };
                }
                await StoreStringResponseToDynamoAsync(caseReference, "ProposedResponse", caseDetails.proposedResponse);
                await StoreNumericResponseToDynamoAsync(caseReference, "ProposedResponseConfidence", caseDetails.proposedResponseConfidence.ToString());
            }

            String json = JsonConvert.SerializeObject(cxmPayload, Formatting.Indented);
            StringContent content = new StringContent(json);

            HttpResponseMessage response = await cxmClient.PatchAsync(uri, content);

            try
            {
                response.EnsureSuccessStatusCode();
                return true;
            }
            catch (Exception error)
            {
                await SendFailureAsync(error.Message, "UpdateCaseDetailsAsync");
                Console.WriteLine("ERROR : UpdateCaseDetailsAsync :  " + error.Message);
                return false;
            }
        }

        private async Task<Boolean> StoreStringResponseToDynamoAsync(String caseReference, String fieldName, String fieldValue)
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
                        {"#Field", fieldName}
                    },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>()
                    {
                        {":Value",new AttributeValue {S = fieldValue}}
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

        private async Task<Boolean> StoreNumericResponseToDynamoAsync(String caseReference, String fieldName, String fieldValue)
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
                        {"#Field", fieldName}
                    },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>()
                    {
                        {":Value",new AttributeValue {N = fieldValue}}
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


        private async Task<Boolean> TransitionCaseAsync()
        {
            if (caseDetails.bundle.Equals("yes"))
            {
                return true;
            }
            Boolean success = false;
            String transitionTo;
            if (caseDetails.unitary)
            {
                transitionTo = "awaiting-location-confirmation";
            }
            else
            {
                if (minAutoRespondLevel < caseDetails.proposedResponseConfidence)
                {
                    transitionTo = "automated-response";
                }
                else
                {
                    transitionTo = "awaiting-review";
                }
            }

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

    public class CaseDetails
    {
        public String customerContact { get; set; } = "";
        public String proposedResponse { get; set; } = "";
        public String bundle { get; set; } = "";
        public int proposedResponseConfidence { get; set; } = 0;
        public Boolean unitary { get; set; } = false;
    }

    public class Secrets
    {
        public String cxmEndPointTest { get; set; }
        public String cxmEndPointLive { get; set; }
        public String cxmAPIKeyTest { get; set; }
        public String cxmAPIKeyLive { get; set; }
        public String wncEMACasesLive { get; set; }
        public String nncEMNCasesLive { get; set; }
        public String wncEMACasesTest { get; set; }
        public String nncEMNCasesTest { get; set; }
        public String cxmAPINameWest { get; set; }
        public String cxmEndPointTestNorth { get; set; }
        public String cxmEndPointLiveNorth { get; set; }
        public String cxmAPIKeyTestNorth { get; set; }
        public String cxmAPIKeyLiveNorth { get; set; }
        public String cxmAPINameNorth { get; set; }
        public String minAutoResponseLevel { get; set; }
        public String nbcQNAurl { get; set; }
        public String nbcQNAauth { get; set; }
        public String minResponseConfidenceLevel { get; set; }
    }
}
