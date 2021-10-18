using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace SendResponseToCustomer
{
    public class Function
    {
        private static readonly RegionEndpoint primaryRegion = RegionEndpoint.EUWest2;
        private static RegionEndpoint bucketRegion = RegionEndpoint.EUWest1;
        private static RegionEndpoint sqsRegion = RegionEndpoint.EUWest1;
        private static readonly String secretName = "nbcGlobal";
        private static readonly String secretAlias = "AWSCURRENT";

        private static string caseReference;
        private static string taskToken;
        private static string cxmEndPoint;
        private static string cxmAPIKey;
        private static string CXMAPIName;
        private static string dynamoTable;
        private static string sqsEmailURL;
        private static string templateBucket;
        private Boolean liveInstance = false;
        private Boolean west = true;

        private Secrets secrets = null;

        public async Task FunctionHandler(object input, ILambdaContext context)
        {
            west = true;
            if (await GetSecrets())
            {
                Boolean suppressResponse = false;

                JObject o = JObject.Parse(input.ToString());
                caseReference = (string)o.SelectToken("CaseReference");
                taskToken = (string)o.SelectToken("TaskToken");

                String fromStatus = (String)o.SelectToken("FromStatus");
                String transition = (String)o.SelectToken("Transition");


                if (caseReference.ToLower().Contains("emn"))
                {
                    west = false;
                    bucketRegion = RegionEndpoint.EUWest2;
                    sqsRegion = RegionEndpoint.EUWest2;
                }

                if (fromStatus.Equals(transition))
                {
                    suppressResponse = true;
                    Console.WriteLine("supressing response : " + caseReference);
                }

                Console.WriteLine("caseReference : " + caseReference);

                try
                {
                    if (context.InvokedFunctionArn.ToLower().Contains("prod"))
                    {
                        liveInstance = true;
                    }
                }
                catch (Exception)
                {
                }

                if (liveInstance)
                {
                    templateBucket = secrets.templateBucketLive;
                    if (west)
                    {
                        cxmEndPoint = secrets.cxmEndPointLive;
                        cxmAPIKey = secrets.cxmAPIKeyLive;
                        CXMAPIName = secrets.cxmAPINameWest;
                        dynamoTable = secrets.wncEMACasesLive;
                    }
                    else
                    {
                        cxmEndPoint = secrets.cxmEndPointLiveNorth;
                        cxmAPIKey = secrets.cxmAPIKeyLiveNorth;
                        CXMAPIName = secrets.cxmAPINameNorth;
                        dynamoTable = secrets.nncEMNCasesLive;
                    }
                    sqsEmailURL = secrets.SqsEmailURLLive;
                    CaseDetails caseDetailsLive = await GetCaseDetailsAsync();
                    await ProcessCaseAsync(caseDetailsLive, suppressResponse);
                    await SendSuccessAsync();
                }
                else
                {
                    templateBucket = secrets.templateBucketLive;
                    if (west)
                    {
                        cxmEndPoint = secrets.cxmEndPointTest;
                        cxmAPIKey = secrets.cxmAPIKeyTest;
                        CXMAPIName = secrets.cxmAPINameWest;
                        dynamoTable = secrets.wncEMACasesTest;
                    }
                    else
                    {
                        cxmEndPoint = secrets.cxmEndPointTestNorth;
                        cxmAPIKey = secrets.cxmAPIKeyTestNorth;
                        CXMAPIName = secrets.cxmAPINameNorth;
                        dynamoTable = secrets.nncEMNCasesTest;
                    }
                    
                    sqsEmailURL = secrets.SqsEmailURLTest;
                    CaseDetails caseDetailsTest = await GetCaseDetailsAsync();
                    await ProcessCaseAsync(caseDetailsTest, suppressResponse);
                    await SendSuccessAsync();
                }
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

        private async Task<CaseDetails> GetCaseDetailsAsync()
        {
            CaseDetails caseDetails = new CaseDetails();
            HttpClient cxmClient = new HttpClient();
            cxmClient.BaseAddress = new Uri(cxmEndPoint);
            string requestParameters = "key=" + cxmAPIKey;
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "/api/service-api/" + CXMAPIName + "/case/" + caseReference + "?" + requestParameters);
            try
            {
                HttpResponseMessage response = cxmClient.SendAsync(request).Result;
                if (response.IsSuccessStatusCode)
                {
                    HttpContent responseContent = response.Content;
                    String responseString = responseContent.ReadAsStringAsync().Result;
                    JObject caseSearch = JObject.Parse(responseString);
                    caseDetails.customerName = (String)caseSearch.SelectToken("values.first-name") + " " + (String)caseSearch.SelectToken("values.surname");
                    caseDetails.contactResponse = (String)caseSearch.SelectToken("values.contact_response");
                    caseDetails.customerEmail = (String)caseSearch.SelectToken("values.email");
                    caseDetails.staffName = (String)caseSearch.SelectToken("values.agents_name");
                    caseDetails.transitionTo = (String)caseSearch.SelectToken("values.new_case_status");
                    caseDetails.serviceArea = (String)caseSearch.SelectToken("values.service_area_4");
                    caseDetails.sentiment = (String)caseSearch.SelectToken("values.sentiment");
                    caseDetails.recommendationAccuracy = (String)caseSearch.SelectToken("values.recommendation_accuracy");
                    caseDetails.EnquiryDetails = (String)caseSearch.SelectToken("values.enquiry_details");
                    if (!west)
                    {
                        caseDetails.customerEmail = (String)caseSearch.SelectToken("values.email_1");
                    }
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

        private async Task<Boolean> ProcessCaseAsync(CaseDetails caseDetails, Boolean supporessResponse)
        {
            Boolean success = true;
            try
            {
                if (!String.IsNullOrEmpty(caseDetails.contactResponse))
                {
                    if (await StoreToDynamoAsync(caseReference, "ActualService", caseDetails.serviceArea) &&
                       await StoreToDynamoAsync(caseReference, "ActualSentiment", caseDetails.sentiment) &&
                       await StoreToDynamoAsync(caseReference, "ActualResponse", caseDetails.contactResponse) &&
                       await StoreToDynamoAsync(caseReference, "RecommendationAccuracy", caseDetails.recommendationAccuracy))
                    {
                        String emailBody = await FormatEmailAsync(caseDetails);
                        if (!String.IsNullOrEmpty(emailBody))
                        {
                            //TODO remove norbert hardcoding
                            if (await SendMessageAsync(emailBody, caseDetails, "updates@northampton.gov.uk", supporessResponse))
                            //if (await SendMessageAsync(emailBody, caseDetails, "norbert@northampton.digital"))
                            {
                                switch (caseDetails.transitionTo.ToLower())
                                {
                                    case "close":
                                        await TransitionCaseAsync("close-case");
                                        break;
                                    case "awaiting_customer_response":
                                        await TransitionCaseAsync("awaiting-customer");
                                        break;
                                    default:
                                        await SendFailureAsync("Enexpected New Case Status for " + caseReference, caseDetails.transitionTo.ToLower());
                                        Console.WriteLine("Enexpected New Case Status for " + caseReference + " : " + caseDetails.transitionTo.ToLower());
                                        success = false;
                                        break;
                                }
                            }
                            else
                            {
                                success = false;
                            }
                        }
                        else
                        {
                            await SendFailureAsync("Empty Message Body : " + caseReference, "ProcessCaseAsync");
                            Console.WriteLine("ERROR : ProcessCaseAsyn : Empty Message Body : " + caseReference);
                            success = false;
                        }
                    }
                    else
                    {
                        success = false;
                    }
                }
                else
                {
                    await SendFailureAsync("Empty Response : " + caseReference, "ProcessCaseAsync");
                    Console.WriteLine("ERROR : ProcessCaseAsyn : Empty Response : " + caseReference);
                    success = false;
                }
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
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/api/service-api/" + CXMAPIName + "/case/" + caseReference + "/transition/" + transitionTo + "?" + requestParameters);
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

        private async Task<String> FormatEmailAsync(CaseDetails caseDetails)
        {
            String emailBody = "";
            IAmazonS3 client = new AmazonS3Client(bucketRegion);
            try
            {
                GetObjectRequest objectRequest = new GetObjectRequest
                {
                    BucketName = templateBucket,
                    Key = "email-staff-response.txt"
                };
                using (GetObjectResponse objectResponse = await client.GetObjectAsync(objectRequest))
                using (Stream responseStream = objectResponse.ResponseStream)
                using (StreamReader reader = new StreamReader(responseStream))
                {
                    emailBody = reader.ReadToEnd();
                }
                emailBody = emailBody.Replace("AAA", caseReference);
                emailBody = emailBody.Replace("CCC", HttpUtility.HtmlEncode(caseDetails.contactResponse));
                emailBody = emailBody.Replace("DDD", HttpUtility.HtmlEncode(caseDetails.customerName));
                emailBody = emailBody.Replace("FFF", HttpUtility.HtmlEncode(await GetContactFromDynamoAsync(caseReference)));
                emailBody = emailBody.Replace("NNN", HttpUtility.HtmlEncode(caseDetails.staffName));
            }
            catch (Exception error)
            {
                await SendFailureAsync(" Reading Response Template", error.Message);
                Console.WriteLine("ERROR : FormatEmailAsync : Reading Response Template : " + error.Message);
                Console.WriteLine("ERROR : FormatEmailAsync : " + error.StackTrace);
            }
            return emailBody;
        }

        private async Task<Boolean> SendMessageAsync(String emailBody, CaseDetails caseDetails, String emailFrom, Boolean suppressResponse)
        {
            if (!suppressResponse)
            {
                try
                {
                    AmazonSQSClient amazonSQSClient = new AmazonSQSClient(sqsRegion);
                    try
                    {
                        SendMessageRequest sendMessageRequest = new SendMessageRequest();
                        sendMessageRequest.QueueUrl = sqsEmailURL;
                        sendMessageRequest.MessageBody = emailBody;
                        Dictionary<string, MessageAttributeValue> MessageAttributes = new Dictionary<string, MessageAttributeValue>();
                        MessageAttributeValue messageTypeAttribute1 = new MessageAttributeValue();
                        messageTypeAttribute1.DataType = "String";
                        messageTypeAttribute1.StringValue = caseDetails.customerName;
                        MessageAttributes.Add("Name", messageTypeAttribute1);
                        MessageAttributeValue messageTypeAttribute2 = new MessageAttributeValue();
                        messageTypeAttribute2.DataType = "String";
                        messageTypeAttribute2.StringValue = caseDetails.customerEmail;
                        MessageAttributes.Add("To", messageTypeAttribute2);
                        MessageAttributeValue messageTypeAttribute3 = new MessageAttributeValue();
                        messageTypeAttribute3.DataType = "String";
                        messageTypeAttribute3.StringValue = secrets.organisationName + " : Your Call Number is " + caseReference; ;
                        MessageAttributes.Add("Subject", messageTypeAttribute3);
                        MessageAttributeValue messageTypeAttribute4 = new MessageAttributeValue();
                        messageTypeAttribute4.DataType = "String";
                        messageTypeAttribute4.StringValue = emailFrom;
                        MessageAttributes.Add("From", messageTypeAttribute4);
                        sendMessageRequest.MessageAttributes = MessageAttributes;
                        SendMessageResponse sendMessageResponse = await amazonSQSClient.SendMessageAsync(sendMessageRequest);
                    }
                    catch (Exception error)
                    {
                        await SendFailureAsync("Error sending SQS message", error.Message);
                        Console.WriteLine("ERROR : SendMessageAsync : Error sending SQS message : '{0}'", error.Message);
                        Console.WriteLine("ERROR : SendMessageAsync : " + error.StackTrace);
                        return false;
                    }
                }
                catch (Exception error)
                {
                    await SendFailureAsync("Error starting AmazonSQSClient", error.Message);
                    Console.WriteLine("ERROR : SendMessageAsync :  Error starting AmazonSQSClient : '{0}'", error.Message);
                    Console.WriteLine("ERROR : SendMessageAsync : " + error.StackTrace);
                    return false;
                }
            }
            return true;
        }

        private async Task<String> GetContactFromDynamoAsync(String caseReference)
        {
            try
            {
                AmazonDynamoDBClient dynamoDBClient = new AmazonDynamoDBClient(primaryRegion);
                Table productCatalog = Table.LoadTable(dynamoDBClient, dynamoTable);
                GetItemOperationConfig config = new GetItemOperationConfig
                {
                    AttributesToGet = new List<string> { "InitialContact" },
                    ConsistentRead = true
                };
                Document document = await productCatalog.GetItemAsync(caseReference, config);
                return document["InitialContact"].AsPrimitive().Value.ToString();
            }
            catch (Exception error)
            {
                Console.WriteLine("ERROR : GetContactFromDynamoAsync : " + error.Message);
                Console.WriteLine(error.StackTrace);
                return "";
            }
        }

        private async Task<Boolean> StoreToDynamoAsync(String caseReference, String fieldName, String fieldValue)
        {
            if (String.IsNullOrEmpty(fieldValue))
            {
                fieldValue = " ";
            }
            try
            {
                AmazonDynamoDBClient dynamoDBClient = new AmazonDynamoDBClient(primaryRegion);
                UpdateItemRequest dynamoRequest = new UpdateItemRequest
                {
                    TableName = dynamoTable,
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
        public String customerName { get; set; } = "";
        public String contactResponse { get; set; } = "";
        public String customerEmail { get; set; } = "";
        public String staffName { get; set; } = "";
        public String transitionTo { get; set; } = "";
        public String serviceArea { get; set; } = "";
        public String sentiment { get; set; } = "";
        public String recommendationAccuracy { get; set; } = "";
        public string EnquiryDetails { get; set; } = "";
    }

    public class Secrets
    {
        public string cxmEndPointTest { get; set; }
        public string cxmEndPointLive { get; set; }
        public string cxmAPIKeyTest { get; set; }
        public string cxmAPIKeyLive { get; set; }
        public string SqsEmailURLLive { get; set; }
        public string SqsEmailURLTest { get; set; }
        public string cxmEndPointTestNorth { get; set; }
        public string cxmEndPointLiveNorth { get; set; }
        public string cxmAPIKeyTestNorth { get; set; }
        public string cxmAPIKeyLiveNorth { get; set; }
        public string cxmAPINameNorth { get; set; }
        public string cxmAPINameWest { get; set; }
        public string organisationName { get; set; }
        public string templateBucketLive { get; set; }
        public string templateBucketTest { get; set; }
        public string wncEMACasesLive { get; set; }
        public string wncEMACasesTest { get; set; }
        public string nncEMNCasesLive { get; set; }
        public string nncEMNCasesTest { get; set; }
    }
}