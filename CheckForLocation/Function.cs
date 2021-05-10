using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Amazon.Lex;
using Amazon.Lex.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using MimeKit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace CheckForLocation
{
    public class Function
    {
        private static readonly RegionEndpoint primaryRegion = RegionEndpoint.EUWest2;
        private static readonly RegionEndpoint bucketRegion = RegionEndpoint.EUWest2;
        private static readonly RegionEndpoint sqsRegion = RegionEndpoint.EUWest1;
        private static readonly RegionEndpoint emailsRegion = RegionEndpoint.EUWest1;
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
        private static String lexAlias;
        private static String originalEmail = "";
        private static String myAccountEndPoint;
        private static String cxmAPIName;
        private static String orgName;
        private static String nncSovereignEmailTable;
        private static String norbertSendFrom;
        private static String emailBucket;
        private static String bccEmailAddress;

        private Boolean liveInstance = false;
        private Boolean district = true;
        private Boolean west = true;
        private Boolean preventOutOfArea = true;
        private Boolean defaultRouting = false;
        private Boolean outOfArea = false;

        private Secrets secrets = null;

        private Location sovereignLocation;

        public async Task FunctionHandler(object input, ILambdaContext context)
        {
            if (await GetSecrets())
            {
                liveInstance = false;
                district = true;
                west = true;
                preventOutOfArea = true;
                defaultRouting = false;
                outOfArea = false;

                templateBucket = secrets.templateBucketTest;
                sqsEmailURL = secrets.sqsEmailURLTest;
                postCodeURL = secrets.postcodeURLTest;
                myAccountEndPoint = secrets.myAccountEndPointTest;

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
                    sqsEmailURL = secrets.sqsEmailURLLive;
                    postCodeURL = secrets.postcodeURLLive;
                    lexAlias = "LIVE";
                    myAccountEndPoint = secrets.myAccountEndPointLive;
                    if (caseReference.ToLower().Contains("ema"))
                    {
                        caseTable = secrets.wncEMACasesLive;
                        sovereignEmailTable = "MailBotCouncilsLive";
                        cxmEndPoint = secrets.cxmEndPointLive;
                        cxmAPIKey = secrets.cxmAPIKeyLive;
                        templateBucket = secrets.templateBucketLive;
                        cxmAPIName = secrets.cxmAPINameWest;
                        orgName = secrets.wncOrgName;
                        norbertSendFrom = secrets.norbertSendFromLive;
                        emailBucket = secrets.WncEmailBucketLive;
                        bccEmailAddress = secrets.WncBccAddressLive;
                        try
                        {
                            if (secrets.wncPreventOutOfAreaLive.ToLower().Equals("false"))
                            {
                                preventOutOfArea = false;
                            }
                        }
                        catch (Exception) { }
                    }
                    if (caseReference.ToLower().Contains("emn"))
                    {
                        west = false;
                        caseTable = secrets.nncEMNCasesLive;
                        sovereignEmailTable = "MailBotCouncilsLive";
                        cxmEndPoint = secrets.cxmEndPointLiveNorth;
                        cxmAPIKey = secrets.cxmAPIKeyLiveNorth;
                        templateBucket = secrets.nncTemplateBucketLive;
                        cxmAPIName = secrets.cxmAPINameNorth;
                        nncSovereignEmailTable = secrets.nncSovereignEmailTableLive;
                        orgName = secrets.nncOrgName;
                        norbertSendFrom = secrets.nncSendFromLive;
                        emailBucket = secrets.NncEmailBucketLive;
                        bccEmailAddress = secrets.NncBccAddressLive;
                        try
                        {
                            if (secrets.nncPreventOutOfAreaLive.ToLower().Equals("false"))
                            {
                                preventOutOfArea = false;
                            }
                        }
                        catch (Exception) { }
                    }
                }
                else
                {
                    cxmEndPoint = secrets.cxmEndPointTest;
                    cxmAPIKey = secrets.cxmAPIKeyTest;
                    templateBucket = secrets.templateBucketTest;
                    sqsEmailURL = secrets.sqsEmailURLTest;
                    postCodeURL = secrets.postcodeURLTest;
                    lexAlias = "UAT";
                    myAccountEndPoint = secrets.myAccountEndPointLive;
                    if (caseReference.ToLower().Contains("ema"))
                    {
                        caseTable = secrets.wncEMACasesTest;
                        sovereignEmailTable = "MailBotCouncilsTest";
                        cxmEndPoint = secrets.cxmEndPointTest;
                        cxmAPIKey = secrets.cxmAPIKeyTest;
                        templateBucket = secrets.templateBucketTest;
                        cxmAPIName = secrets.cxmAPINameWest;
                        orgName = secrets.wncOrgName;
                        norbertSendFrom = secrets.norbertSendFromTest;
                        emailBucket = secrets.WncEmailBucketTest;
                        bccEmailAddress = secrets.WncBccAddressTest;
                        try
                        {
                            if (secrets.wncPreventOutOfAreaTest.ToLower().Equals("false"))
                            {
                                preventOutOfArea = false;
                            }
                        }
                        catch (Exception) { }
                    }
                    if (caseReference.ToLower().Contains("emn"))
                    {
                        west = false;
                        caseTable = secrets.nncEMNCasesTest;
                        sovereignEmailTable = "MailBotCouncilsTest";
                        cxmEndPoint = secrets.cxmEndPointTestNorth;
                        cxmAPIKey = secrets.cxmAPIKeyTestNorth;
                        templateBucket = secrets.nncTemplateBucketTest;
                        cxmAPIName = secrets.cxmAPINameNorth;
                        orgName = secrets.nncOrgName;
                        nncSovereignEmailTable = secrets.nncSovereignEmailTableTest;
                        norbertSendFrom = secrets.nncSendFromTest;
                        emailBucket = secrets.NncEmailBucketTest;
                        bccEmailAddress = secrets.NncBccAddressTest;
                        try
                        {
                            if (secrets.nncPreventOutOfAreaTest.ToLower().Equals("false"))
                            {
                                preventOutOfArea = false;
                            }
                        }
                        catch (Exception) { }
                    }
                }
                Console.WriteLine(caseReference + " : Prevent Out of Area : " + preventOutOfArea);
                CaseDetails caseDetails = await GetCaseDetailsAsync();
                await ProcessCaseAsync(caseDetails);
                await SendSuccessAsync();
            }

            Console.WriteLine("Completed");
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

        private async Task<CaseDetails> GetCaseDetailsAsync()
        {
            CaseDetails caseDetails = new CaseDetails();
            HttpClient cxmClient = new HttpClient
            {
                BaseAddress = new Uri(cxmEndPoint)
            };
            string requestParameters = "key=" + cxmAPIKey;
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "/api/service-api/" + cxmAPIName + "/case/" + caseReference + "?" + requestParameters);
            try
            {
                HttpResponseMessage response = cxmClient.SendAsync(request).Result;
                if (response.IsSuccessStatusCode)
                {
                    HttpContent responseContent = response.Content;
                    String responseString = responseContent.ReadAsStringAsync().Result;
                    JObject caseSearch = JObject.Parse(responseString);
                    caseDetails.customerName = (String)caseSearch.SelectToken("values.first-name") + " " + (String)caseSearch.SelectToken("values.surname");
                    try
                    {
                        caseDetails.manualReview = (Boolean)caseSearch.SelectToken("values.manual_review");
                    }
                    catch (Exception) { }
                    try
                    {
                        caseDetails.forward = (String)caseSearch.SelectToken("values.emn_fwd_to_sovereign_council");
                    }
                    catch (Exception) { }
                    if (caseReference.ToLower().Contains("ema"))
                    {
                        caseDetails.customerEmail = (String)caseSearch.SelectToken("values.email");
                    }
                    try
                    {
                        caseDetails.contactUs = (Boolean)caseSearch.SelectToken("values.contact_us");
                    }
                    catch (Exception) { }
                    try
                    {
                        caseDetails.District = (Boolean)caseSearch.SelectToken("values.district");
                    }
                    catch (Exception) { }
                    try
                    {
                        caseDetails.customerAddress = (String)caseSearch.SelectToken("values.customer_address");
                    }
                    catch (Exception) { }
                    try
                    {
                        caseDetails.telephoneNumber = (String)caseSearch.SelectToken("values.telephone_number");
                    }
                    catch (Exception) { }
                    try
                    {
                        caseDetails.emailID = (String)caseSearch.SelectToken("values.email_id");
                    }
                    catch (Exception) { }
                    try
                    {
                        caseDetails.ConfirmationSent = (Boolean)caseSearch.SelectToken("values.confirmation_sent");
                    }
                    catch (Exception) { }
                    if (caseReference.ToLower().Contains("emn"))
                    {
                        caseDetails.customerEmail = (String)caseSearch.SelectToken("values.email_1");
                        caseDetails.nncForwardEMailTo = GetStringValueFromJSON(caseSearch, "values.forward_email_to");
                        caseDetails.contactUs = (Boolean)caseSearch.SelectToken("values.emn_contact_us");
                    }
                    caseDetails.enquiryDetails = (String)caseSearch.SelectToken("values.enquiry_details");
                    caseDetails.customerHasUpdated = (Boolean)caseSearch.SelectToken("values.customer_has_updated");
                    caseDetails.sovereignCouncil = GetStringValueFromJSON(caseSearch, "values.sovereign_council");
                    caseDetails.sovereignServiceArea = GetStringValueFromJSON(caseSearch, "values.sovereign_service_area");
                    caseDetails.fullEmail = GetStringValueFromJSON(caseSearch, "values.original_email");
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

        private async Task<Boolean> ProcessCaseAsync(CaseDetails caseDetails)
        {
            Boolean success = true;
            try
            {
                if (!String.IsNullOrEmpty(caseDetails.enquiryDetails))
                {

                    originalEmail = await GetContactFromDynamoAsync(caseReference);
                    if (caseDetails.manualReview && west)
                    {
                        String forwardingEmailAddress = await GetSovereignEmailFromDynamoAsync(caseDetails.sovereignCouncil, caseDetails.sovereignServiceArea);
                        if (String.IsNullOrEmpty(forwardingEmailAddress))
                        {
                            forwardingEmailAddress = await GetSovereignEmailFromDynamoAsync(caseDetails.sovereignCouncil, "default");
                            defaultRouting = true;
                        }
                        success = await SendEmails(caseDetails, forwardingEmailAddress, true);
                        caseDetails.forward = caseDetails.sovereignCouncil + "-" + caseDetails.sovereignServiceArea;
                        if (caseDetails.sovereignCouncil.ToLower().Equals("northampton")&&!defaultRouting)
                        {
                            await TransitionCaseAsync("awaiting-review");
                        }
                        else
                        {
                            await TransitionCaseAsync("close-case");
                        }
                    }
                    else if (caseDetails.manualReview)
                    {
                        Boolean replyToCustomer = true;
                        if (!String.IsNullOrEmpty(caseDetails.forward))
                        {
                            String forwardingEmailAddress = await NNCGetSovereignEmailFromDynamoAsync(caseDetails.forward);
                            success = await SendEmails(caseDetails, forwardingEmailAddress, replyToCustomer);
                            replyToCustomer = false;
                        }
                        if (!String.IsNullOrEmpty(caseDetails.nncForwardEMailTo))
                        {
                            success = await SendEmails(caseDetails, caseDetails.nncForwardEMailTo, replyToCustomer);
                        }
                        await TransitionCaseAsync("close-case");
                    }
                    else 
                    {
                       sovereignLocation = await CheckForLocationAsync(caseDetails.enquiryDetails);
                        if (caseDetails.contactUs && !sovereignLocation.Success)
                        {
                            sovereignLocation = await CheckForLocationAsync(caseDetails.customerAddress);
                        }
                        String service = "";
                        district = caseDetails.District;

                        if (caseDetails.contactUs&&!String.IsNullOrEmpty(caseDetails.sovereignServiceArea))
                        {
                            Console.WriteLine(caseReference + " : SovereignServiceArea set using  : " + caseDetails.sovereignServiceArea);                           
                            service = caseDetails.sovereignServiceArea;
                        }
                        else
                        {
                            Console.WriteLine(caseReference + " : SovereignServiceArea not set using Lex ");
                            service = await GetServiceAsync(originalEmail);
                        }                      

                        if (sovereignLocation.Success)
                        {
                            Console.WriteLine(caseReference + " : Location Found : " + sovereignLocation.SovereignCouncilName.ToLower());
                            String sovereignCouncilName = sovereignLocation.SovereignCouncilName.ToLower();
                            if (!district)
                            {
                                if(west)
                                {
                                    sovereignCouncilName = "county_west";
                                }
                                else
                                {
                                    sovereignCouncilName = "county_north";
                                }                              
                                sovereignLocation.SovereignCouncilName = "northamptonshire";
                            }
                            String forwardingEmailAddress = await GetSovereignEmailFromDynamoAsync(sovereignCouncilName, service);
                            if (String.IsNullOrEmpty(forwardingEmailAddress))
                            {
                                forwardingEmailAddress = await GetSovereignEmailFromDynamoAsync(sovereignCouncilName, "default");
                                defaultRouting = true;
                            }
                            UpdateCaseString("sovereign-council", sovereignLocation.SovereignCouncilName);
                            if (preventOutOfArea && west && !sovereignLocation.sovereignWest)
                            {
                                UpdateCaseString("email-comments", "Contact destination out of area");
                                await TransitionCaseAsync("unitary-awaiting-review");
                            } 
                            else
                            if (preventOutOfArea && !west && sovereignLocation.sovereignWest)
                            {
                                UpdateCaseString("email-comments", "Contact destination out of area");
                                await TransitionCaseAsync("hub-awaiting-review");
                            } else
                            {
                                if((west && !sovereignLocation.sovereignWest)||(!west && sovereignLocation.sovereignWest))
                                {
                                    UpdateCaseString("email-comments", "Contact destination out of area");
                                    if (west)
                                    {
                                        forwardingEmailAddress = await GetSovereignEmailFromDynamoAsync("nnc", "default");
                                    }
                                    else
                                    {
                                        forwardingEmailAddress = await GetSovereignEmailFromDynamoAsync("wnc", "default");
                                    }                                  
                                    outOfArea = true;
                                }
                                success = await SendEmails(caseDetails, forwardingEmailAddress, true);                               
                                if (west && sovereignLocation.SovereignCouncilName.ToLower().Equals("northampton")&&defaultRouting)
                                {
                                    UpdateCaseString("email-comments", "Transitioning case to local process");
                                    await TransitionCaseAsync("awaiting-review");
                                }
                                else
                                {
                                    UpdateCaseString("email-comments", "Closing case");
                                    await TransitionCaseAsync("close-case");
                                }
                            }                        
                        }
                        else
                        {
                            Console.WriteLine(caseReference + " : Location Not Found");
                            Console.WriteLine(caseReference + " : Customer Has Updated : " + caseDetails.customerHasUpdated);
                            if (caseDetails.customerHasUpdated||sovereignLocation.PostcodeFound)
                            {
                                if (west)
                                {
                                    Console.WriteLine(caseReference + " : West Transition");
                                    await TransitionCaseAsync("unitary-awaiting-review");
                                }
                                else
                                {
                                    Console.WriteLine(caseReference + " : North Transition");
                                    await TransitionCaseAsync("hub-awaiting-review");
                                }

                            }
                            else
                            {
                                String emailBody = await FormatEmailAsync(caseDetails, "email-location-request.txt");
                                if (!String.IsNullOrEmpty(emailBody))
                                {
                                    if (await SendMessageAsync(orgName + " : Your Call Number is " + caseReference, caseDetails.customerEmail, caseDetails.customerEmail, emailBody, caseDetails))
                                    {
                                        UpdateCaseString("email-comments", "email requesting location details sent to " + caseDetails.customerEmail);                                       
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
                emailBody = emailBody.Replace("ZZZ", caseDetails.enquiryDetails);
                emailBody = emailBody.Replace("GGG", caseDetails.customerName);

                if (String.IsNullOrEmpty(caseDetails.fullEmail))
                {
                    emailBody = emailBody.Replace("OOO", HttpUtility.HtmlEncode(caseDetails.enquiryDetails));
                }
                else
                {
                    String tempDetails = "";
                    if (caseDetails.customerHasUpdated)
                    {
                        tempDetails = HttpUtility.HtmlEncode(caseDetails.enquiryDetails) + "<BR><BR>";
                    }
                    emailBody = emailBody.Replace("OOO", tempDetails + HttpUtility.HtmlEncode(caseDetails.fullEmail));
                }

                if (caseDetails.contactUs)
                {
                    if (!String.IsNullOrEmpty(caseDetails.customerEmail))
                    {
                        emailBody = emailBody.Replace("MMM", "The customer's email address is - <b>" + caseDetails.customerEmail + "</b><br><br>");
                    }
                    else
                    {
                        emailBody = emailBody.Replace("MMM", "");
                    }
                    if (!String.IsNullOrEmpty(caseDetails.customerAddress))
                    {
                        emailBody = emailBody.Replace("PPP", "The customer's address is - <b>" + caseDetails.customerAddress + "</b><br><br>");
                    }
                    else
                    {
                        emailBody = emailBody.Replace("PPP", "");
                    }
                    if (!String.IsNullOrEmpty(caseDetails.telephoneNumber))
                    {
                        emailBody = emailBody.Replace("TTT", "You can contact the customer on - <b>" + caseDetails.telephoneNumber + "</b><br><br>");
                    }
                    else
                    {
                        emailBody = emailBody.Replace("TTT", "");
                    }
                }
                else
                {
                    emailBody = emailBody.Replace("KKK", caseDetails.customerEmail);
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

        private async Task<Boolean> SendMessageAsync(String emailSubject, String emailTo, String replyTo, String emailBody, CaseDetails caseDetails)
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
                    MessageAttributeValue messageTypeAttribute4 = new MessageAttributeValue();
                    messageTypeAttribute4.DataType = "String";
                    messageTypeAttribute4.StringValue = replyTo;
                    MessageAttributes.Add("ReplyTo", messageTypeAttribute4);
                    sendMessageRequest.MessageAttributes = MessageAttributes;
                    SendMessageResponse sendMessageResponse = await amazonSQSClient.SendMessageAsync(sendMessageRequest);
                }
                catch (Exception error)
                {
                    await SendFailureAsync("Error sending SQS message", error.Message);
                    Console.WriteLine("ERROR : SendMessageAsync : Error sending SQS message : '{0}'", error.Message);
                    Console.WriteLine("ERROR : SendMessageAsync : " + caseReference + " : " + emailTo + " : " + emailBody);
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
            return true;
        }

        private async Task<Location> CheckForLocationAsync(String emailBody)
        {
            emailBody = emailBody.Replace("\n", " ");
            emailBody = emailBody.Trim();
            Location sovereignLocation = new Location();

            String[] regArray = new string[4];

            regArray[0] = @"^([Gg][Ii][Rr] 0[Aa]{2})|((([A-Za-z][0-9]{1,2})|(([A-Za-z][A-Ha-hJ-Yj-y][0-9]{1,2})|(([AZa-z][0-9][A-Za-z])|([A-Za-z][A-Ha-hJ-Yj-y][0-9][A-Za-z]?)))) [0-9][A-Za-z]{2})$";
            regArray[1] = @"^([Gg][Ii][Rr] 0[Aa]{2})|((([A-Za-z][0-9]{1,2})|(([A-Za-z][A-Ha-hJ-Yj-y][0-9]{1,2})|(([AZa-z][0-9][A-Za-z])|([A-Za-z][A-Ha-hJ-Yj-y][0-9][A-Za-z]?))))[0-9][A-Za-z]{2})$";
            regArray[2] = @"^([Gg][Ii][Rr] 0[Aa]{2})|((([A-Za-z][0-9]{1,2})|(([A-Za-z][A-Ha-hJ-Yj-y][0-9]{1,2})|(([AZa-z][0-9][A-Za-z])|([A-Za-z][A-Ha-hJ-Yj-y][0-9][A-Za-z]?)))) [0-9][A-Za-z]{2}).";
            regArray[3] = @"^([Gg][Ii][Rr] 0[Aa]{2})|((([A-Za-z][0-9]{1,2})|(([A-Za-z][A-Ha-hJ-Yj-y][0-9]{1,2})|(([AZa-z][0-9][A-Za-z])|([A-Za-z][A-Ha-hJ-Yj-y][0-9][A-Za-z]?))))[0-9][A-Za-z]{2}).";

            foreach (String regString in regArray)
            {
                MatchCollection matches = Regex.Matches(emailBody, regString);

                foreach (Match match in matches)
                {
                    sovereignLocation.PostcodeFound = true;
                    GroupCollection groups = match.Groups;
                    Postcode postCodeData = await CheckPostcode(groups[0].Value);
                    try
                    {
                        if (postCodeData.success)
                        {
                            sovereignLocation.SovereignCouncilName = postCodeData.SovereignCouncilName;
                            sovereignLocation.sovereignWest = postCodeData.west;
                            sovereignLocation.Success = true;
                            return sovereignLocation;
                        }
                    }
                    catch (Exception) { }
                }
            }

            if (sovereignLocation.PostcodeFound)
            {
                return sovereignLocation;
            }

            if (emailBody.ToLower().Contains("northampton"))
            {
                //if((emailBody.ToLower().inde)
                sovereignLocation.SovereignCouncilName = "Northampton";
                sovereignLocation.sovereignWest = true;
                sovereignLocation.Success = true;
            }
            else
            if (emailBody.ToLower().Contains("towcester") || emailBody.ToLower().Contains("cogenhoe"))
            {
                sovereignLocation.SovereignCouncilName = "south_northants";
                sovereignLocation.sovereignWest = true;
                sovereignLocation.Success = true;
            }
            else
            if (emailBody.ToLower().Contains("daventry"))
            {
                sovereignLocation.SovereignCouncilName = "Daventry";
                sovereignLocation.sovereignWest = true;
                sovereignLocation.Success = true;
            }
            else
            if (emailBody.ToLower().Contains("wellingborough"))
            {
                sovereignLocation.SovereignCouncilName = "Wellingborough";
                sovereignLocation.sovereignWest = false;
                sovereignLocation.Success = true;
            }
            else
            if (emailBody.ToLower().Contains("kettering"))
            {
                sovereignLocation.SovereignCouncilName = "Kettering";
                sovereignLocation.sovereignWest = false;
                sovereignLocation.Success = true;
            }
            else
            if (emailBody.ToLower().Contains("corby"))
            {
                sovereignLocation.SovereignCouncilName = "Corby";
                sovereignLocation.sovereignWest = false;
                sovereignLocation.Success = true;
            }
            else
            if (emailBody.ToLower().Contains("rushden"))
            {
                sovereignLocation.SovereignCouncilName = "east_northants";
                sovereignLocation.sovereignWest = false;
                sovereignLocation.Success = true;
            }
            return sovereignLocation;
        }

        private async Task<Postcode> CheckPostcode(String postcode)
        {
            Postcode postCodeData = new Postcode();

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, postCodeURL + postcode);

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
                        if((int)caseSearch.SelectToken("numOfSovereign") == 1)
                        {
                            postCodeData.singleSov = true;
                        }
                        else
                        {
                            UpdateCaseString("email-comments", "Postcode spans multiple sovereign councils");
                        }
                    }
                    catch (Exception) { }
                    try
                    {
                        if ((int)caseSearch.SelectToken("numOfUnitary") == 1)
                        {
                            postCodeData.singleUni = true;
                        }
                        else
                        {
                            UpdateCaseString("email-comments", "Postcode spans both WNC and NNC");
                        }
                    }
                    catch (Exception) { }
                    try
                    {
                        if ((int)caseSearch.SelectToken("unitary[0].unitaryCode") == 2)
                        {
                            postCodeData.west = true;
                        }
                    }
                    catch (Exception) { }
                    try
                    {
                        postCodeData.SovereignCouncilName = (String)caseSearch.SelectToken("sovereign[0].sovereignName").ToString().ToLower();
                    }
                    catch (Exception) { }
                    try
                    {
                        if (postCodeData.SovereignCouncilName.Equals("south northants"))
                        {
                            postCodeData.SovereignCouncilName = "south_northants";
                        }
                    }
                    catch (Exception) { }
                    try
                    {
                        if (postCodeData.SovereignCouncilName.Equals("east northants"))
                        {
                            postCodeData.SovereignCouncilName = "east_northants";
                        }
                    }
                    catch (Exception) { }
                }
                catch (NotSupportedException)
                {
                    Console.WriteLine("The content type is not supported.");
                    postCodeData.success = false;
                }
                catch (JsonException)
                {
                    Console.WriteLine("Invalid JSON.");
                    postCodeData.success = false;
                }
            }
            else
            {
                postCodeData.success = false;
            }
            return postCodeData;
        }

        private Boolean UpdateCaseString(String fieldName, String fieldValue)
        {
            if (fieldName.Equals("sovereign-service-area"))
            {
                if (fieldValue.ToLower().Equals("waste"))
                {
                    if (district)
                    {
                        fieldValue = "districtwaste";
                    }
                    else
                    {
                        fieldValue = "countywaste";
                    }
                }
            }

            String data = "{\"" + fieldName + "\":\"" + fieldValue.ToLower() + "\"" +
                "}";

            if (UpdateCase(data))
            {
                return true;
            }
            else
            {
                Console.WriteLine(caseReference + " : Error updating CXM field " + fieldName + " with message : " + fieldValue);
                return false;
            }
        }

        private Boolean UpdateCaseBoolean(String fieldName, Boolean  fieldValue)
        {
 
            String data = "{\"" + fieldName + "\":\"" + fieldValue + "\"" +
                "}";

            if (UpdateCase(data))
            {
                return true;
            }
            else
            {
                Console.WriteLine(caseReference + " : Error updating CXM field " + fieldName + " with message : " + fieldValue);
                return false;
            }
        }

        private Boolean UpdateCase(String data)
        {
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
                Console.WriteLine(caseReference + " : " + error.ToString());               
                return false;
            }
            return true; ;
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

        private async Task<string> GetServiceAsync(String customerContact)
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
                    await SendToTrello(caseReference, secrets.trelloBoardTrainingLabelUnitaryService, secrets.trelloBoardTrainingLabelAWSLexUnitary);
                    UpdateCaseString("sovereign-service-area", "notfound");
                }
                else
                {
                    if (intentName.ToLower().Contains("county"))
                    {
                        district = false;
                    }
                    if (intentName.ToLower().Contains("_"))
                    {
                        intentName = intentName.Split('_')[1];
                        UpdateCaseString("sovereign-service-area", intentName);
                    }
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
                                                                    { "service", new AttributeValue { S = service.ToLower() } }
                                                                   }
                };
                GetItemResponse response = await dynamoDBClient.GetItemAsync(request);

                Dictionary<String, AttributeValue> attributeMap = response.Item;
                AttributeValue sovereignEmailAttribute;
                attributeMap.TryGetValue("email", out sovereignEmailAttribute);
                String sovereignEmail = "";
                try
                {
                    sovereignEmail = sovereignEmailAttribute.S;
                }
                catch (Exception) { }
                return sovereignEmail;
            }
            catch (Exception error)
            {
                Console.WriteLine("ERROR : GetSovereignEmailFromDynamoAsync : " + error.Message);
                Console.WriteLine(error.StackTrace);
                return "";
            }
        }

        private async Task<String> NNCGetSovereignEmailFromDynamoAsync(String sovereignName)
        {
            try
            {
                AmazonDynamoDBClient dynamoDBClient = new AmazonDynamoDBClient(primaryRegion);
                GetItemRequest request = new GetItemRequest
                {
                    TableName = nncSovereignEmailTable,
                    Key = new Dictionary<string, AttributeValue>() {
                                                                    { "name", new AttributeValue { S = sovereignName }  }
                                                                   }
                };
                GetItemResponse response = await dynamoDBClient.GetItemAsync(request);

                Dictionary<String, AttributeValue> attributeMap = response.Item;
                AttributeValue sovereignEmailAttribute;
                attributeMap.TryGetValue("email", out sovereignEmailAttribute);
                String sovereignEmail = "";
                try
                {
                    sovereignEmail = sovereignEmailAttribute.S;
                }
                catch (Exception) { }
                return sovereignEmail;
            }
            catch (Exception error)
            {
                Console.WriteLine("ERROR : NNCGetSovereignEmailFromDynamoAsync : " + error.Message);
                Console.WriteLine(error.StackTrace);
                return "";
            }
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

        private async Task<Boolean> SendEmails(CaseDetails caseDetails, String forwardingEmailAddress, Boolean replyToCustomer)
        {
            Console.WriteLine(caseReference + " : SendEmails Started");
            String emailBody = "";
            if (replyToCustomer&&!caseDetails.ConfirmationSent)
            {
                Console.WriteLine(caseReference + " : Sending confirmation email");
                String subject = "";
                if (outOfArea)
                {
                    emailBody = await FormatEmailAsync(caseDetails, "sovereign-misdirect-acknowledge.txt");
                    subject = orgName + " : We have redirected your enquiry";
                }
                else
                {
                    emailBody = await FormatEmailAsync(caseDetails, "email-sovereign-acknowledge.txt");
                    subject = orgName + " : Your Call Number is " + caseReference;
                }
                
                if (!String.IsNullOrEmpty(emailBody))
                {
                    if (!await SendMessageAsync(subject, caseDetails.customerEmail, norbertSendFrom, emailBody, caseDetails))
                    {
                        Console.WriteLine(caseReference + " : ERROR : Failed to send confirmation email");
                        UpdateCaseString("email-comments", "Failed to send confirmation email to " + caseDetails.customerEmail);
                        return false;
                    }
                    Console.WriteLine(caseReference + " : Sent confirmation email");
                    UpdateCaseString("email-comments", "Confirmation email sent to " + caseDetails.customerEmail);
                }
                else
                {
                    Console.WriteLine(caseReference + " : ERROR : Failed to send confirmation email");
                    UpdateCaseString("email-comments", "Failed to send confirmation email to " + caseDetails.customerEmail);
                    await SendFailureAsync("Empty Message Body : " + caseReference, "ProcessCaseAsync");
                    return false;
                }
                if (UpdateCaseBoolean("confirmation-sent", true)){}
                else
                {
                    Console.WriteLine(caseReference + " : ERROR : Failed to update confirmation-sent");
                    UpdateCaseString("email-comments", "Failed to update confirmation-sent");
                }
            }

            Console.WriteLine(caseReference + " : Sending forward email");
 

            if (sovereignLocation.SovereignCouncilName.ToLower().Equals("northampton")&&defaultRouting)
            {
                Console.WriteLine(caseReference + " : Local default case no forward necessary");
            }
            else
            {
                Console.WriteLine(caseReference + " : Preparing to forward email");
                String forwardFileName = "";
                if (caseDetails.contactUs) 
                {
                    Console.WriteLine(caseReference + " : ContactUs case");
                    forwardFileName = "email-sovereign-forward-contactus.txt";
                }
                else
                {
                    Console.WriteLine(caseReference + " : Email case");
                    forwardFileName = "email-sovereign-forward.txt";
                }                    
                emailBody = await FormatEmailAsync(caseDetails, forwardFileName);
                Console.WriteLine(caseReference + " : Email contents set");
                if (!String.IsNullOrEmpty(emailBody))
                {
                    String subjectPrefix = "";
                    if (!liveInstance)
                    {
                        if (!String.IsNullOrEmpty(caseDetails.forward))
                        {
                            subjectPrefix = "(" + caseDetails.forward + ") ";
                        }
                        subjectPrefix += "TEST - ";
                    }
                    if (!await SendEmailAsync(orgName,norbertSendFrom, forwardingEmailAddress.ToLower(), bccEmailAddress,subjectPrefix + "Hub case reference number is " + caseReference, caseDetails.emailID, emailBody, ""))
                    {
                        Console.WriteLine(caseReference + " : ERROR : Failed to forward email");
                        UpdateCaseString("email-comments", "Failed to forward email to " + forwardingEmailAddress.ToLower());
                        return false;
                    }
                    Console.WriteLine(caseReference + " : Forwarded email");
                    UpdateCaseString("email-comments", "Forwarded email to " + forwardingEmailAddress.ToLower());
                }
                else
                {
                    Console.WriteLine(caseReference + "ERROR : ProcessCaseAsyn : Empty Message Body : " + caseReference);
                    UpdateCaseString("email-comments", "Failed to forward email to " + forwardingEmailAddress.ToLower());
                    await SendFailureAsync("Empty Message Body : " + caseReference, "ProcessCaseAsync");                   
                    return false;
                }
            }
            Console.WriteLine(caseReference + " : SendEmails Ended");
            return true;
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

        public async Task<Boolean> SendEmailAsync(String from, String fromAddress, String toAddress, String bccAddress, String subject, String emailID, String htmlBody, String textBody)
        {
            using (var client = new AmazonSimpleEmailServiceClient(RegionEndpoint.EUWest1))
            {
                var sendRequest = new SendRawEmailRequest { RawMessage = new RawMessage(await GetMessageStreamAsync(from, fromAddress, toAddress, subject, emailID, htmlBody, textBody, bccAddress)) };
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

        private static async Task<MemoryStream> GetMessageStreamAsync(String from, String fromAddress, String toAddress, String subject,String emailID, String htmlBody, String textBody, String bccAddress)
        {
            MemoryStream stream = new MemoryStream();
            MimeMessage message = await GetMessageAsync(from,fromAddress,toAddress,subject,emailID, htmlBody, textBody, bccAddress);
            message.WriteTo(stream);
            return stream;
        }

        private static async Task<MimeMessage> GetMessageAsync(String from, String fromAddress, String toAddress, String subject, String emailID, String htmlBody, String textBody, String bccAddress)
        {
            MimeMessage message = new MimeMessage();
            message.From.Add(new MailboxAddress(from,fromAddress));
            message.To.Add(new MailboxAddress(string.Empty, toAddress));
            message.Bcc.Add(new MailboxAddress(string.Empty, bccAddress));
            message.Subject = subject;
            BodyBuilder bodyBuilder = await GetMessageBodyAsync(emailID, htmlBody, textBody);
            message.Body = bodyBuilder.ToMessageBody();
            return message;
        }

        private static async Task<BodyBuilder> GetMessageBodyAsync(String emailID, String htmlBody, String textBody)
        {
            var body = new BodyBuilder()
            {
                HtmlBody = @htmlBody,
                TextBody = textBody
            };

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
                    body.Attachments.Add(caseReference + ".eml",imageBytes);
                }
                catch (Exception error)
                {

                }
                return body;
            }
        }
    }

}

    public class CaseDetails
    {
        public String customerName { get; set; } = "";
        public String customerEmail { get; set; } = "";
        public String enquiryDetails { get; set; } = "";
        public String fullEmail { get; set; } = "";
        public String forward { get; set; } = "";
        public String sovereignCouncil { get; set; } = "";
        public String sovereignServiceArea { get; set; } = "";
        public String nncForwardEMailTo { get; set; } = "";
        public String customerAddress { get; set; } = "";
        public String telephoneNumber { get; set; } = "";
        public String emailID { get; set; } = "";
        public Boolean customerHasUpdated { get; set; } = false;
        public Boolean manualReview { get; set; } = false;
        public Boolean contactUs { get; set; } = false;
        public Boolean District { get; set; } = false;
        public Boolean ConfirmationSent { get; set; } = false;
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
        public String wncEMACasesLive { get; set; }
        public String wncEMACasesTest { get; set; }
        public String nncEMNCasesLive { get; set; }
        public String nncEMNCasesTest { get; set; }
        public String cxmEndPointTestNorth { get; set; }
        public String cxmEndPointLiveNorth { get; set; }
        public String cxmAPIKeyTestNorth { get; set; }
        public String cxmAPIKeyLiveNorth { get; set; }
        public String cxmAPINameNorth { get; set; }
        public String cxmAPINameWest { get; set; }
        public String organisationName { get; set; }
        public String wncOrgName { get; set; }
        public String nncOrgName { get; set; }
        public String nncTemplateBucketLive { get; set; }
        public String nncTemplateBucketTest { get; set; }
        public String nncSovereignEmailTableLive { get; set; }
        public String nncSovereignEmailTableTest { get; set; }
        public String norbertSendFromLive { get; set; }
        public String norbertSendFromTest { get; set; }
        public String nncSendFromLive { get; set; }
        public String nncSendFromTest { get; set; }
        public String nncPreventOutOfAreaLive { get; set; }
        public String nncPreventOutOfAreaTest { get; set; }
        public String wncPreventOutOfAreaLive { get; set; }
        public String wncPreventOutOfAreaTest { get; set; }
        public String WncEmailBucketLive { get; set; }
        public String WncEmailBucketTest { get; set; }
        public String NncEmailBucketLive { get; set; }
        public String NncEmailBucketTest { get; set; }
        public String WncBccAddressTest { get; set; }
        public String WncBccAddressLive { get; set; }
        public String NncBccAddressTest { get; set; }
        public String NncBccAddressLive { get; set; }
    }

    public class Location
    {
        public Boolean Success = false;
        public Boolean PostcodeFound = false;
        public Boolean sovereignWest = false;
        public String SovereignCouncilName = "";
    }

    public class Postcode
    {
        public Boolean success = true;
        public Boolean singleUni = false;
        public Boolean singleSov = false;
        public Boolean west = false;
        public String SovereignCouncilName = "";
    }