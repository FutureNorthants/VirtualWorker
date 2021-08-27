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
using Amazon.S3.Model;
using Amazon.S3;
using System.IO;
using System.Web;
using MimeKit;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace TransferCaseViaEmail
{
    public class Function
    {
        private static readonly RegionEndpoint primaryRegion = RegionEndpoint.EUWest2;
        private static readonly RegionEndpoint bucketRegion = RegionEndpoint.EUWest2;
        private static RegionEndpoint sqsRegion = RegionEndpoint.EUWest1;
        private static RegionEndpoint emailsRegion = RegionEndpoint.EUWest1;
        private static readonly String secretName = "nbcGlobal";
        private static readonly String secretAlias = "AWSCURRENT";

        private static String caseReference;
        private static String taskToken;
        private static String cxmEndPoint;
        private static String cxmAPIKey;
        private static String templateBucketName;
        private static String emailBucket;
        private static String norbertSendFrom;
        private static String bccEmailAddress;
        private Secrets secrets;
        private CaseDetails caseDetails;


        public async Task FunctionHandler(object input, ILambdaContext context)
        {
            if (await GetSecrets())
            {
                Boolean liveInstance = false;
                JObject inputJson = JObject.Parse(input.ToString());
                caseReference = (String)inputJson.SelectToken("CaseReference");
                if (caseReference.ToLower().Contains("emn"))
                {
                    sqsRegion = RegionEndpoint.EUWest2;
                }
                taskToken = (String)inputJson.SelectToken("TaskToken");
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

                if (caseReference.ToLower().Contains("ema"))
                {
                }
                else
                {
                    emailsRegion = RegionEndpoint.EUWest2;
                }

                if (liveInstance)
                {
                    cxmEndPoint = secrets.cxmEndPointLive;
                    cxmAPIKey = secrets.cxmAPIKeyLive;
                    templateBucketName = secrets.TemplateBucketLive;
                    if (caseReference.ToLower().Contains("ema"))
                    {
                        emailBucket = secrets.WncEmailBucketLive;
                        norbertSendFrom = secrets.NorbertSendFromLive;
                        bccEmailAddress = secrets.WncBccAddressLive;
                    }
                    else
                    {
                        emailBucket = secrets.NncEmailBucketLive;
                        norbertSendFrom = secrets.NncSendFromLive;
                        bccEmailAddress = secrets.NncBccAddressLive;
                    }
                }
                else
                {
                    cxmEndPoint = secrets.cxmEndPointTest;
                    cxmAPIKey = secrets.cxmAPIKeyTest;
                    templateBucketName = secrets.TemplateBucketTest;
                    if (caseReference.ToLower().Contains("ema"))
                    {
                        emailBucket = secrets.WncEmailBucketTest;
                        norbertSendFrom = secrets.NorbertSendFromTest;
                        bccEmailAddress = secrets.WncBccAddressTest;
                    }
                    else
                    {
                        emailBucket = secrets.NncEmailBucketTest;
                        norbertSendFrom = secrets.NncSendFromTest;
                        bccEmailAddress = secrets.NncBccAddressTest;
                    }
                }
                caseDetails = await GetCustomerContactAsync();
                String emailContents = await FormatEmailAsync();
                Boolean includeOriginalEmail = true;
                if (caseDetails.XfpContactUs)
                {
                    includeOriginalEmail = false;
                }

                if (await SendEmailAsync(secrets.OrganisationNameShort, norbertSendFrom, caseDetails.emailTo, bccEmailAddress, "West Northants Council: Your Call Number is " + caseReference, caseDetails.emailID, emailContents, "", includeOriginalEmail))
                {
                    await TransitionCaseAsync("close-case");
                    await SendSuccessAsync();
                }
            }
        }

        private async Task<Boolean> GetSecrets()
        {
            IAmazonSecretsManager client = new AmazonSecretsManagerClient(primaryRegion);

            GetSecretValueRequest request = new GetSecretValueRequest
            {
                SecretId = secretName,
                VersionStage = secretAlias
            };

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

        private async Task<String> FormatEmailAsync()
        {
            String emailBody = "";
            IAmazonS3 client = new AmazonS3Client(bucketRegion);
            try
            {
                GetObjectRequest objectRequest = new GetObjectRequest
                {
                    BucketName = templateBucketName,
                    Key = "email-non-CXM-service.txt"
                };
                using (GetObjectResponse objectResponse = await client.GetObjectAsync(objectRequest))
                using (Stream responseStream = objectResponse.ResponseStream)
                using (StreamReader reader = new StreamReader(responseStream))
                {
                    emailBody = reader.ReadToEnd();
                }

                emailBody = emailBody.Replace("AAA", caseReference);
                emailBody = emailBody.Replace("KKK", HttpUtility.HtmlEncode(caseDetails.emailFrom));

                String tempDetails = "";
                if (caseDetails.CustomerHasUpdated)
                {
                    tempDetails = HttpUtility.HtmlEncode(caseDetails.enquiryDetails) + "<BR><BR>";
                }
                if(caseDetails.XfpContactUs)
                {
                    emailBody = emailBody.Replace("FFF", tempDetails + HttpUtility.HtmlEncode(caseDetails.enquiryDetails));
                }
                else
                {
                    emailBody = emailBody.Replace("FFF", tempDetails + HttpUtility.HtmlEncode(caseDetails.FullEmail));
                }
                
            }
            catch (Exception error)
            {
                await SendFailureAsync(" Reading Response Template", error.Message);
                Console.WriteLine("ERROR : FormatEmailAsync : Reading Response Template : " + error.Message);
                Console.WriteLine("ERROR : FormatEmailAsync : " + error.StackTrace);
            }
            return emailBody;
        }

        private async Task<CaseDetails> GetCustomerContactAsync()
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
                    try
                    {
                        caseDetails.CustomerHasUpdated = (Boolean)caseSearch.SelectToken("values.customer_has_updated");
                    }
                    catch (Exception) { }
  
                    caseDetails.FullEmail = GetStringValueFromJSON(caseSearch, "values.original_email");
                    caseDetails.emailTo = (String)caseSearch.SelectToken("values.forward_email_to");
                    caseDetails.emailFrom = (String)caseSearch.SelectToken("values.email");
                    caseDetails.enquiryDetails = (String)caseSearch.SelectToken("values.enquiry_details");
                    try
                    {
                        if (GetStringValueFromJSON(caseSearch, "values.xfp_case_type").ToLower().Equals("contact us"))
                        {
                            caseDetails.XfpContactUs = true;
                        }
                    }
                    catch (Exception) { }
                    try
                    {
                        caseDetails.emailID = (String)caseSearch.SelectToken("values.email_id");
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

        private String GetStringValueFromJSON(JObject json, String fieldName)
        {
            String returnValue = "";
            try
            {
                returnValue = (String)json.SelectToken(fieldName);
            }
            catch (Exception) { }
            return returnValue;
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

        public async Task<Boolean> SendEmailAsync(String from, String fromAddress, String toAddress, String bccAddress, String subject, String emailID, String htmlBody, String textBody, Boolean includeOriginalEmail)
        {
            using (AmazonSimpleEmailServiceClient client = new AmazonSimpleEmailServiceClient(RegionEndpoint.EUWest1))
            {
                SendRawEmailRequest sendRequest = new SendRawEmailRequest { RawMessage = new RawMessage(await GetMessageStreamAsync(from, fromAddress, toAddress, subject, emailID, htmlBody, textBody, bccAddress, includeOriginalEmail)) };
                try
                {
                    SendRawEmailResponse response = await client.SendRawEmailAsync(sendRequest);
                    return true;
                }
                catch (Exception error)
                {
                    Console.WriteLine(caseReference + " : Error Sending Raw Email : " + error.Message);
                    return false;
                }
            }
        }

        private static async Task<MemoryStream> GetMessageStreamAsync(String from, String fromAddress, String toAddress, String subject, String emailID, String htmlBody, String textBody, String bccAddress, Boolean includeOriginalEmail)
        {
            MemoryStream stream = new MemoryStream();
            MimeMessage message = await GetMessageAsync(from, fromAddress, toAddress, subject, emailID, htmlBody, textBody, bccAddress, includeOriginalEmail);
            message.WriteTo(stream);
            return stream;
        }

        private static async Task<MimeMessage> GetMessageAsync(String from, String fromAddress, String toAddress, String subject, String emailID, String htmlBody, String textBody, String bccAddress, Boolean includeOriginalEmail)
        {
            MimeMessage message = new MimeMessage();
            message.From.Add(new MailboxAddress(from, fromAddress));
            message.To.Add(new MailboxAddress(string.Empty, toAddress));
            message.Bcc.Add(new MailboxAddress(string.Empty, bccAddress));
            message.Subject = subject;
            BodyBuilder bodyBuilder = await GetMessageBodyAsync(emailID, htmlBody, textBody, includeOriginalEmail);
            message.Body = bodyBuilder.ToMessageBody();
            return message;
        }

        private static async Task<BodyBuilder> GetMessageBodyAsync(String emailID, String htmlBody, String textBody, Boolean includeOriginalEmail)
        {
            BodyBuilder body = new BodyBuilder()
            {
                HtmlBody = @htmlBody,
                TextBody = textBody
            };

            if (includeOriginalEmail)
            {
                AmazonS3Client s3 = new AmazonS3Client(emailsRegion);
                GetObjectResponse image = await s3.GetObjectAsync(emailBucket, emailID);
                byte[] imageBytes = new byte[image.ContentLength];
                using (MemoryStream ms = new MemoryStream())
                {
                    int read;
                    byte[] buffer = new byte[16 * 1024];
                    while ((read = image.ResponseStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        ms.Write(buffer, 0, read);
                    }
                    try
                    {
                        imageBytes = ms.ToArray();
                        body.Attachments.Add(caseReference + ".eml", imageBytes);
                    }
                    catch (Exception error)
                    {
                        Console.WriteLine(caseReference + " : Error Attaching original email : " + error.Message);
                    }
                }
            }
            return body;

        }

    }
    class CaseDetails
    {
        public String emailTo { get; set; } = "";
        public String emailFrom { get; set; } = "";
        public String enquiryDetails { get; set; } = "";
        public String FullEmail { get; set; } = "";
        public String emailID { get; set; } = "";
        public Boolean CustomerHasUpdated { get; set; } = false;
        public Boolean XfpContactUs { get; set; } = false;
    }

    public class Sentiment
    {
        public Boolean success { get; set; }
        public String sentimentRating { get; set; }
        public String sentimentMixed { get; set; }
        public String sentimentNegative { get; set; }
        public String sentimentNeutral { get; set; }
        public String sentimentPositive { get; set; }
    }

    public class Secrets
    {
        public String cxmEndPointTest { get; set; }
        public String cxmEndPointLive { get; set; }
        public String cxmAPIKeyTest { get; set; }
        public String cxmAPIKeyLive { get; set; }
        public String sqsEmailURL { get; set; }
        public String TemplateBucketTest { get; set; }
        public String TemplateBucketLive { get; set; }
        public String WncEmailBucketLive { get; set; }
        public String WncEmailBucketTest { get; set; }
        public String NncEmailBucketLive { get; set; }
        public String NncEmailBucketTest { get; set; }
        public String OrganisationNameShort { get; set; }
        public String NorbertSendFromLive { get; set; }
        public String NorbertSendFromTest { get; set; }
        public String NncSendFromLive { get; set; }
        public String NncSendFromTest { get; set; }
        public String WncBccAddressTest { get; set; }
        public String WncBccAddressLive { get; set; }
        public String NncBccAddressTest { get; set; }
        public String NncBccAddressLive { get; set; }
    }
}