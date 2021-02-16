using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Amazon;
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
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Lex;
using Amazon.Lex.Model;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace CheckForLocation
{
    public class Function
    {
        private static readonly RegionEndpoint primaryRegion = RegionEndpoint.EUWest2;
        private static readonly RegionEndpoint bucketRegion = RegionEndpoint.EUWest2;
        private static readonly RegionEndpoint sqsRegion = RegionEndpoint.EUWest1;
        private static readonly String secretName = "nbcGlobal";
        private static readonly String secretAlias = "AWSCURRENT";

        private static String caseReference;
        private static String taskToken;
        private static String cxmEndPoint;
        private static String cxmAPIKey;
        private static String templateBucket;
        private static String sqsEmailURL;
        private static String postCodeURL;
        private static String caseTable;
        private static String sovereignEmailTable;
        private static String lexAlias = "UAT";
        private static String originalEmail = "";
        private static String myAccountEndPoint;
        private Boolean liveInstance = false;

        private Secrets secrets = null;

        public async Task FunctionHandler(object input, ILambdaContext context)
        {
            if (await GetSecrets())
            {
                Boolean suppressResponse = false;

                templateBucket = secrets.templateBucketTest;
                sqsEmailURL = secrets.sqsEmailURLTest;
                postCodeURL = secrets.postcodeURLTest;
                myAccountEndPoint = secrets.myAccountEndPointTest;

                caseTable = "MailBotCasesTest";
                sovereignEmailTable = "MailBotCouncilsTest";

                JObject o = JObject.Parse(input.ToString());
                caseReference = (string)o.SelectToken("CaseReference");
                taskToken = (string)o.SelectToken("TaskToken");

                Console.WriteLine("caseReference : " + caseReference);

                try
                {
                    if (context.InvokedFunctionArn.ToLower().Contains("prod"))
                    {
                        liveInstance = true;
                        templateBucket = secrets.templateBucketLive;
                        sqsEmailURL = secrets.sqsEmailURLLive;
                        postCodeURL = secrets.postcodeURLLive;
                        caseTable = "MailBotCasesLive";
                        sovereignEmailTable = "MailBotCouncilsLive";
                        lexAlias = "LIVE";
                        myAccountEndPoint = secrets.myAccountEndPointLive;
                    }
                }
                catch (Exception)
                {
                }

                if (liveInstance)
                {
                    cxmEndPoint = secrets.cxmEndPointLive;
                    cxmAPIKey = secrets.cxmAPIKeyLive;
                    CaseDetails caseDetailsLive = await GetCaseDetailsAsync();
                    await ProcessCaseAsync(caseDetailsLive, suppressResponse);
                    await SendSuccessAsync();
                }
                else
                {
                    cxmEndPoint = secrets.cxmEndPointTest;
                    cxmAPIKey = secrets.cxmAPIKeyTest;
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
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "/api/service-api/norbert/case/" + caseReference + "?" + requestParameters);
            try
            {
                HttpResponseMessage response = cxmClient.SendAsync(request).Result;
                if (response.IsSuccessStatusCode)
                {
                    HttpContent responseContent = response.Content;
                    String responseString = responseContent.ReadAsStringAsync().Result;
                    JObject caseSearch = JObject.Parse(responseString);
                    caseDetails.customerName = (String)caseSearch.SelectToken("values.first-name") + " " + (String)caseSearch.SelectToken("values.surname");
                    caseDetails.customerEmail = (String)caseSearch.SelectToken("values.email");
                    caseDetails.enquiryDetails = (String)caseSearch.SelectToken("values.enquiry_details");
                    caseDetails.customerHasUpdated = (Boolean)caseSearch.SelectToken("values.customer_has_updated");
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
                if (!String.IsNullOrEmpty(caseDetails.enquiryDetails))
                {
                    Location sovereignLocation = await CheckForLocationAsync(caseDetails.enquiryDetails);

                    if (sovereignLocation.Success)
                    {
                        originalEmail = await GetContactFromDynamoAsync(caseReference);
                        String service = await GetIntentFromLexAsync(originalEmail);
                        String sovereignCouncilName = sovereignLocation.SovereignCouncilName.ToLower();
                        if (service.ToLower().Contains("_"))
                        {
                            if (service.ToLower().Split('_')[0].Equals("county")||
                                service.ToLower().Split('_')[0].Equals("unitary"))
                            {
                                sovereignCouncilName = service.ToLower().Split('_')[0];
                            }
                            service = service.Split('_')[1];
                        } 
                        String forwardingEmailAddress = await GetSovereignEmailFromDynamoAsync(sovereignCouncilName,service);
                        UpdateCase("sovereign-council", sovereignLocation.SovereignCouncilName);
                        await TransitionCaseAsync("close-case");
                        String emailBody = await FormatEmailAsync(caseDetails, "email-sovereign-acknowledge.txt");
                        if (!String.IsNullOrEmpty(emailBody))
                        {
                            if (!await SendMessageAsync("Northampton Borough Council: Your Call Number is " + caseReference, caseDetails.customerEmail, emailBody, caseDetails, supporessResponse))
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
                       
                        emailBody = await FormatEmailAsync(caseDetails, "email-sovereign-forward.txt");
                        if (!String.IsNullOrEmpty(emailBody))
                        {
                            String subjectPrefix = "";
                            if (!liveInstance)
                            {
                                subjectPrefix = "(" + sovereignCouncilName + "-" + service + ") ";
                            }
                            if (!await SendMessageAsync(subjectPrefix + "Hub case reference number is " + caseReference, forwardingEmailAddress.ToLower(), emailBody, caseDetails, supporessResponse))
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
                        if (caseDetails.customerHasUpdated)
                        {
                            await TransitionCaseAsync("unitary-awaiting-review");
                        }
                        else
                        {
                            String emailBody = await FormatEmailAsync(caseDetails, "email-location-request.txt");
                            if (!String.IsNullOrEmpty(emailBody))
                            {
                                if (!await SendMessageAsync("Northampton Borough Council: Your Call Number is " + caseReference, caseDetails.customerEmail, emailBody, caseDetails, supporessResponse))
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
                    }

                }
                else
                {
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
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/api/service-api/norbert/case/" + caseReference + "/transition/" + transitionTo + "?" + requestParameters);
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

        private async Task<String> FormatEmailAsync(CaseDetails caseDetails, String fileName)
        {
            String emailBody = "";
            IAmazonS3 client = new AmazonS3Client(bucketRegion);
            try
            {
                GetObjectRequest objectRequest = new GetObjectRequest
                {
                    BucketName = templateBucket,
                    Key = fileName
                };
                using (GetObjectResponse objectResponse = await client.GetObjectAsync(objectRequest))
                using (Stream responseStream = objectResponse.ResponseStream)
                using (StreamReader reader = new StreamReader(responseStream))
                {
                    emailBody = reader.ReadToEnd();
                }
                emailBody = emailBody.Replace("AAA", caseReference);
                emailBody = emailBody.Replace("FFF", HttpUtility.HtmlEncode(originalEmail));
                emailBody = emailBody.Replace("KKK", caseDetails.customerEmail);
            }
            catch (Exception error)
            {
                await SendFailureAsync(" Reading Response Template", error.Message);
                Console.WriteLine("ERROR : FormatEmailAsync : Reading Response Template : " + error.Message);
                Console.WriteLine("ERROR : FormatEmailAsync : " + error.StackTrace);
            }
            return emailBody;
        }

        private async Task<Boolean> SendMessageAsync(String emailSubject, String emailTo, String emailBody, CaseDetails caseDetails, Boolean suppressResponse)
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
                        messageTypeAttribute2.StringValue = emailTo;
                        MessageAttributes.Add("To", messageTypeAttribute2);
                        MessageAttributeValue messageTypeAttribute3 = new MessageAttributeValue();
                        messageTypeAttribute3.DataType = "String";
                        messageTypeAttribute3.StringValue = emailSubject;
                        MessageAttributes.Add("Subject", messageTypeAttribute3);
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

        private async Task<Location> CheckForLocationAsync(String emailBody)
        {

            Location sovereignLocation = new Location();

            String[] regArray = new string[2];

            regArray[0] = @"^([Gg][Ii][Rr] 0[Aa]{2})|((([A-Za-z][0-9]{1,2})|(([A-Za-z][A-Ha-hJ-Yj-y][0-9]{1,2})|(([AZa-z][0-9][A-Za-z])|([A-Za-z][A-Ha-hJ-Yj-y][0-9][A-Za-z]?)))) [0-9][A-Za-z]{2})$";
            regArray[1] = @"^([Gg][Ii][Rr] 0[Aa]{2})|((([A-Za-z][0-9]{1,2})|(([A-Za-z][A-Ha-hJ-Yj-y][0-9]{1,2})|(([AZa-z][0-9][A-Za-z])|([A-Za-z][A-Ha-hJ-Yj-y][0-9][A-Za-z]?))))[0-9][A-Za-z]{2})$";

            foreach( String regString in regArray)
            {
                MatchCollection matches = Regex.Matches(emailBody, regString);

                foreach (Match match in matches)
                {
                    GroupCollection groups = match.Groups;
                    String sovereign = await checkPostcode(groups[0].Value);
                    try
                    {
                        if (!String.IsNullOrEmpty(sovereign))
                        {
                            sovereignLocation.SovereignCouncilName = sovereign;
                            sovereignLocation.Success = true;
                            return sovereignLocation;
                        }
                    }
                    catch (Exception) { }
                }
            }
            
            if(emailBody.ToLower().Contains("northampton"))
            {
                sovereignLocation.SovereignCouncilName = "Northampton";
                sovereignLocation.Success = true;
            } else
            if (emailBody.ToLower().Contains("towcester"))
            {
                sovereignLocation.SovereignCouncilName = "south_northants";
                sovereignLocation.Success = true;
            } else
            if (emailBody.ToLower().Contains("daventry")) 
            {
                sovereignLocation.SovereignCouncilName = "Daventry";
                sovereignLocation.Success = true;
            } else
            if (emailBody.ToLower().Contains("wellingborough"))
            {
                sovereignLocation.SovereignCouncilName = "Wellingborough";
                sovereignLocation.Success = true;
            } else
            if (emailBody.ToLower().Contains("kettering"))
            {
                sovereignLocation.SovereignCouncilName = "Kettering";
                sovereignLocation.Success = true;
            } else
            if (emailBody.ToLower().Contains("corby"))
            {
                sovereignLocation.SovereignCouncilName = "Corby";
                sovereignLocation.Success = true;
            } else
            if (emailBody.ToLower().Contains("rushden"))
            {
                sovereignLocation.SovereignCouncilName = "east_northants";
                sovereignLocation.Success = true;
            }
            return sovereignLocation;
        }

        private static async Task<String> checkPostcode(String postcode)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, postCodeURL+postcode);

            HttpClient httpClient = new HttpClient();

            using HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    String responseString = await response.Content.ReadAsStringAsync();
                    JObject caseSearch = JObject.Parse(responseString);
                    try
                    {
                        return (String)caseSearch.SelectToken("sovereign");
                    }
                    catch (Exception) { }
                }
                catch (NotSupportedException) 
                {
                    Console.WriteLine("The content type is not supported.");
                }
                catch (JsonException) 
                {
                    Console.WriteLine("Invalid JSON.");
                }
            }

            return null;
        }

        private Boolean UpdateCase(String fieldName, String fieldValue)
        {
            Boolean success = true;

            String data = "{\"" + fieldName + "\":\"" + fieldValue + "\"" +
                          "}";

            Console.WriteLine($"PATCH payload : " + data);

            String url = cxmEndPoint + "/api/service-api/norbert/case/" + caseReference + "/edit?key=" + cxmAPIKey;
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

        private async Task<String> GetContactFromDynamoAsync(String caseReference)
        {
            try
            {
                AmazonDynamoDBClient dynamoDBClient = new AmazonDynamoDBClient(primaryRegion);
                Table dynamoTable = Table.LoadTable(dynamoDBClient, caseTable);
                GetItemOperationConfig config = new GetItemOperationConfig
                {
                    AttributesToGet = new List<String> { "InitialContact" },
                    ConsistentRead = true
                };
                Document document = await dynamoTable.GetItemAsync(caseReference, config);
                return document["InitialContact"].AsPrimitive().Value.ToString();
            }
            catch (Exception error)
            {
                Console.WriteLine("ERROR : GetContactFromDynamoAsync : " + error.Message);
                Console.WriteLine(error.StackTrace);
                return "";
            }
        }

        private async Task<string> GetIntentFromLexAsync(String customerContact)
        {
            try
            {
                AmazonLexClient lexClient = new AmazonLexClient(primaryRegion);
                PostTextRequest textRequest = new PostTextRequest();
                textRequest.UserId = "MailBot";
                textRequest.BotAlias = lexAlias;
                textRequest.BotName = "UnitaryServices";
                textRequest.InputText = customerContact;
                PostTextResponse textResponse = await lexClient.PostTextAsync(textRequest);
                HttpStatusCode temp = textResponse.HttpStatusCode;
                String intentName = textResponse.IntentName;
                if (String.IsNullOrEmpty(intentName))
                {
                    intentName = "default";
                    await SendToTrello(caseReference,secrets.trelloBoardTrainingLabelUnitaryService,secrets.trelloBoardTrainingLabelAWSLexUnitary);
                }
                return intentName;
            }
            catch (Exception error)
            {
                await SendFailureAsync("Getting Intent", error.Message);
                Console.WriteLine("ERROR : GetIntentFromLexAsync : " + error.StackTrace);
                return "GeneralEnquiries";
            }
        }

        private async Task<String> GetSovereignEmailFromDynamoAsync(String sovereignName, String service)
        {
          try
            {
                AmazonDynamoDBClient dynamoDBClient = new AmazonDynamoDBClient(primaryRegion);
                GetItemRequest request = new GetItemRequest
                {
                    TableName = sovereignEmailTable,
                    Key = new Dictionary<string, AttributeValue>() {
                                                                    { "name", new AttributeValue { S = sovereignName } },
                                                                    { "service", new AttributeValue { S = service } }
                                                                   }
                };
                GetItemResponse response = await dynamoDBClient.GetItemAsync(request);

                Dictionary<String,AttributeValue> attributeMap = response.Item; 
                AttributeValue sovereignEmailAttribute;
                attributeMap.TryGetValue("email", out sovereignEmailAttribute);
                String sovereignEmail = sovereignEmailAttribute.S;
                return sovereignEmail;
            }
            catch (Exception error)
            {
                Console.WriteLine("ERROR : GetSovereignEmailFromDynamoAsync : " + error.Message);
                Console.WriteLine(error.StackTrace);
                return "";
            }
        }

        private async Task<Boolean> SendToTrello(String caseReference, String fieldLabel, String techLabel)
        {
            try
            {
                HttpClient cxmClient = new HttpClient();
                cxmClient.BaseAddress = new Uri("https://api.trello.com");
                String requestParameters = "key=" + secrets.trelloAPIKey;
                requestParameters += "&token=" + secrets.trelloAPIToken;
                requestParameters += "&idList=" + secrets.trelloBoardTrainingListPending;
                requestParameters += "&name=" + caseReference + " - No Unitary Service Found";             
                requestParameters += "&desc=**[Full Case Details](" + myAccountEndPoint + "/q/case/" + caseReference + "/timeline)**";
                requestParameters += "&pos=" + "bottom";
                requestParameters += "&idLabels=" + fieldLabel + "," + techLabel;
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "1/cards?" + requestParameters);
                try
                {
                    HttpResponseMessage response = cxmClient.SendAsync(request).Result;
                    if (!response.IsSuccessStatusCode)
                    {
                        await SendFailureAsync("Getting case details for " + caseReference, response.StatusCode.ToString());
                        Console.WriteLine(caseReference + " : ERROR : GetStaffResponseAsync : " + request.ToString());
                        Console.WriteLine(caseReference + " : ERROR : GetStaffResponseAsync : " + response.StatusCode.ToString());
                    }
                }
                catch (Exception error)
                {
                    await SendFailureAsync("SentToTrello : " + caseReference, error.Message);
                    Console.WriteLine(caseReference + " : ERROR : SentToTrello : " + error.StackTrace);
                }
                return false;
            }
            catch (Exception error)
            {
                Console.WriteLine(caseReference + " : ERROR : Creating Trello Card : " + error.Message);
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
        public String customerEmail { get; set; } = "";
        public String enquiryDetails { get; set; } = "";
        public Boolean customerHasUpdated { get; set; } = false;
    }

    public class Secrets
    {
        public String cxmEndPointTest { get; set; }
        public String cxmEndPointLive { get; set; }
        public String cxmAPIKeyTest { get; set; }
        public String cxmAPIKeyLive { get; set; }
        public String sqsEmailURLLive { get; set; }
        public String sqsEmailURLTest { get; set; }
        public String templateBucketTest { get; set; }
        public String templateBucketLive { get; set; }
        public String postcodeURLLive { get; set; }
        public String postcodeURLTest { get; set; }
        public String trelloAPIKey { get; set; }
        public String trelloAPIToken { get; set; }
        public String trelloBoardTrainingListPending { get; set; }
        public String myAccountEndPointLive { get; set; }
        public String myAccountEndPointTest { get; set; }
        public String trelloBoardTrainingLabelAWSLexUnitary { get; set; }
        public String trelloBoardTrainingLabelUnitaryService { get; set; }
    }

    public class Location
    {
        public Boolean Success = false;
        public String SovereignCouncilName = "";
    }
}