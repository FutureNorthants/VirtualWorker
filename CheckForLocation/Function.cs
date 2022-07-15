using Amazon;
using Amazon.DynamoDBv2;
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
using System.Threading.Tasks;
using System.Web;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace CheckForLocation
{
    public class Messages
    {
        public const String missingEmailFile = "This enquiry is more than 30 days old and has been archived. Please copy the original message and updates and forward onto the Sovereign Council via Outlook, quoting the email reference number";
    }


    public class Function
    {
        private static readonly RegionEndpoint primaryRegion = RegionEndpoint.EUWest2;
        private static readonly RegionEndpoint bucketRegion = RegionEndpoint.EUWest2;
        private static RegionEndpoint emailsRegion = RegionEndpoint.EUWest1;
        private static readonly String secretName = "nbcGlobal";
        private static readonly String secretAlias = "AWSCURRENT";

        private static String caseReference;
        private static String taskToken;
        private static String cxmEndPoint;
        private static String cxmAPIKey;
        private static String templateBucket;
        private static String postCodeURL;
        private static String sovereignEmailTable;
        private static String lexAlias;
        private static String myAccountEndPoint;
        private static String cxmAPIName;
        private static String orgName;
        private static String nncSovereignEmailTable;
        private static String norbertSendFrom;
        private static String emailBucket;
        private static String bccEmailAddress;
        private static String persona;
        private static String SubjectServiceMinConfidence;

        private Boolean liveInstance = false;
        private Boolean district = true;
        private Boolean west = true;
        private Boolean preventOutOfArea = true;
        private Boolean defaultRouting = false;
        private Boolean outOfArea = false;
        private Boolean reopened = false;
        private Secrets secrets = null;

        private Location sovereignLocation;
        readonly MemoryStream memoryStream = new MemoryStream();

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
                reopened = false;
                outOfArea = false;

                templateBucket = secrets.templateBucketTest;
                postCodeURL = secrets.postcodeURLTest;
                myAccountEndPoint = secrets.myAccountEndPointTest;
                orgName = secrets.organisationName;

                JObject inputJSON = JObject.Parse(input.ToString());
                caseReference = (String)inputJSON.SelectToken("CaseReference");
                taskToken = (String)inputJSON.SelectToken("TaskToken");

                Random randonNumber = new Random();
                if (randonNumber.Next(0, 2) == 0)
                {
                    persona = secrets.botPersona1;
                }
                else
                {
                    persona = secrets.botPersona2;
                }

                try
                {
                    if (((String)inputJSON.SelectToken("FromStatus")).ToString().ToLower().Equals("case-closed"))
                    {
                        reopened = true;
                    }
                }
                catch (Exception) { }

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
                    postCodeURL = secrets.postcodeURLLive;
                    lexAlias = "LIVE";
                    myAccountEndPoint = secrets.myAccountEndPointLive;
                    SubjectServiceMinConfidence = secrets.SubjectServiceMinConfidenceLive;
                    if (caseReference.ToLower().Contains("ema"))
                    {
                        sovereignEmailTable = "MailBotCouncilsLive";
                        cxmEndPoint = secrets.cxmEndPointLive;
                        cxmAPIKey = secrets.cxmAPIKeyLive;
                        templateBucket = secrets.templateBucketLive;
                        cxmAPIName = secrets.cxmAPINameWest;
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
                        emailsRegion = RegionEndpoint.EUWest2;
                        sovereignEmailTable = "MailBotCouncilsLive";
                        cxmEndPoint = secrets.cxmEndPointLiveNorth;
                        cxmAPIKey = secrets.cxmAPIKeyLiveNorth;
                        templateBucket = secrets.nncTemplateBucketLive;
                        cxmAPIName = secrets.cxmAPINameNorth;
                        nncSovereignEmailTable = secrets.nncSovereignEmailTableLive;
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
                    postCodeURL = secrets.postcodeURLTest;
                    lexAlias = "UAT";
                    myAccountEndPoint = secrets.myAccountEndPointTest;
                    SubjectServiceMinConfidence = secrets.SubjectServiceMinConfidenceTest;
                    if (caseReference.ToLower().Contains("ema"))
                    {
                        sovereignEmailTable = "MailBotCouncilsTest";
                        cxmEndPoint = secrets.cxmEndPointTest;
                        cxmAPIKey = secrets.cxmAPIKeyTest;
                        templateBucket = secrets.templateBucketTest;
                        cxmAPIName = secrets.cxmAPINameWest;
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
                        emailsRegion = RegionEndpoint.EUWest2;
                        sovereignEmailTable = "MailBotCouncilsTest";
                        cxmEndPoint = secrets.cxmEndPointTestNorth;
                        cxmAPIKey = secrets.cxmAPIKeyTestNorth;
                        templateBucket = secrets.nncTemplateBucketTest;
                        cxmAPIName = secrets.cxmAPINameNorth;
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
                try
                {
                    await ProcessCaseAsync(caseDetails);
                    await SendSuccessAsync();
                }
                catch (ApplicationException)
                {
                    //await SendFailureAsync(caseReference + " : ApplicationException : " + error.Message, "ProcessCaseAsync");
                    await SendSuccessAsync();
                }
            }

            Console.WriteLine("Completed");
        }

        private String FormatLinks(CaseDetails caseDetails)
        {
            String links = "";
            String instanceText = "test";
            if (liveInstance)
            {
                instanceText = "prod";
            }
            if (west)
            {
                if (!caseDetails.sovereignCouncil.ToLower().Equals("northampton"))
                {
                    links += "<a href=\"" + secrets.RedirectURI + "?instance=" + instanceText + "&reference=" + caseReference + "&transfercaseto=" + "northampton" + "&persona=" + persona + "\">Redirect to Guildhall Hub</a><BR>";
                }

                if (!caseDetails.sovereignCouncil.ToLower().Equals("daventry"))
                {
                    links += "<a href=\"" + secrets.RedirectURI + "?instance=" + instanceText + "&reference=" + caseReference + "&transfercaseto=" + "daventry" + "&persona=" + persona + "\">Redirect to Lodge Road Hub</a><BR>";
                }

                if (!caseDetails.sovereignCouncil.ToLower().Equals("northamptonshire"))
                {
                    links += "<a href=\"" + secrets.RedirectURI + "?instance=" + instanceText + "&reference=" + caseReference + "&transfercaseto=" + "northamptonshire" + "&persona=" + persona + "\">Redirect to One Angel Square Hub</a><BR>";
                }

                if (!caseDetails.sovereignCouncil.ToLower().Equals("south_northants"))
                {
                    links += "<a href=\"" + secrets.RedirectURI + "?instance=" + instanceText + "&reference=" + caseReference + "&transfercaseto=" + "south_northants" + "&persona=" + persona + "\">Redirect to The Forum Hub</a><BR>";
                }
            }
            else
            {
                if (!caseDetails.sovereignCouncil.ToLower().Equals("wellingborough"))
                {
                    links += "<a href=\"" + secrets.RedirectURI + "?instance=" + instanceText + "&reference=" + caseReference + "&transfercaseto=" + "wellingborough" + "&persona=" + persona + "\">Redirect to Wellingborough</a><BR>";
                }

                if (!caseDetails.sovereignCouncil.ToLower().Equals("corby"))
                {
                    links += "<a href=\"" + secrets.RedirectURI + "?instance=" + instanceText + "&reference=" + caseReference + "&transfercaseto=" + "corby" + "&persona=" + persona + "\">Redirect to Corby</a><BR>";
                }

                if (!caseDetails.sovereignCouncil.ToLower().Equals("east_northants"))
                {
                    links += "<a href=\"" + secrets.RedirectURI + "?instance=" + instanceText + "&reference=" + caseReference + "&transfercaseto=" + "east_northants" + "&persona=" + persona + "\">Redirect to East Northants</a><BR>";
                }

                if (!caseDetails.sovereignCouncil.ToLower().Equals("kettering"))
                {
                    links += "<a href=\"" + secrets.RedirectURI + "?instance=" + instanceText + "&reference=" + caseReference + "&transfercaseto=" + "kettering" + "&persona=" + persona + "\">Redirect to Kettering</a><BR>";
                }

                if (!caseDetails.sovereignCouncil.ToLower().Equals("northamptonshire"))
                {
                    links += "<a href=\"" + secrets.RedirectURI + "?instance=" + instanceText + "&reference=" + caseReference + "&transfercaseto=" + "northamptonshire" + "&persona=" + persona + "\">Redirect to County</a><BR>";
                }
            }
            return links;
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
                    caseDetails.customerName = (String)caseSearch.SelectToken("values.first_name") + " " + (String)caseSearch.SelectToken("values.surname");
                    try
                    {
                        caseDetails.manualReview = (Boolean)caseSearch.SelectToken("values.manual_review");
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
                    caseDetails.Subject = RemoveSuppressionList(secrets.SuppressWording, GetStringValueFromJSON(caseSearch, "values.subject"));
                    caseDetails.enquiryDetails = RemoveSuppressionList(secrets.SuppressWording, GetStringValueFromJSON(caseSearch, "values.enquiry_details"));
                    caseDetails.customerHasUpdated = (Boolean)caseSearch.SelectToken("values.customer_has_updated");
                    caseDetails.sovereignCouncil = GetStringValueFromJSON(caseSearch, "values.sovereign_council");
                    caseDetails.sovereignServiceArea = GetStringValueFromJSON(caseSearch, "values.sovereign_service_area");
                    caseDetails.Redirected = GetBooleanValueFromJSON(caseSearch, "values.redirected");
                    caseDetails.fullEmail = RemoveSuppressionList(secrets.SuppressWording, GetStringValueFromJSON(caseSearch, "values.original_email"));
                    if (caseReference.ToLower().Contains("emn"))
                    {
                        caseDetails.customerEmail = (String)caseSearch.SelectToken("values.email_1");
                        caseDetails.telephoneNumber = (String)caseSearch.SelectToken("values.customer_telephone_number");
                        caseDetails.nncForwardEMailTo = GetStringValueFromJSON(caseSearch, "values.forward_email_to");
                        caseDetails.contactUs = (Boolean)caseSearch.SelectToken("values.emn_contact_us");
                        try
                        {
                            caseDetails.forward = (String)caseSearch.SelectToken("values.emn_fwd_to_sovereign_council");
                            if (!caseDetails.Redirected&&!String.IsNullOrEmpty(caseDetails.forward))
                            {
                                caseDetails.sovereignCouncil = caseDetails.forward;
                            }
                        }
                        catch (Exception) { }
                    }
                    if (String.IsNullOrEmpty(caseDetails.sovereignCouncil))
                    {
                        caseDetails.sovereignCouncil = "";
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

        private async Task<Boolean> ProcessCaseAsync(CaseDetails caseDetails)
        {
            Boolean success = true;
            try
            {
                if (String.IsNullOrEmpty(caseDetails.enquiryDetails))
                {
                    return false;
                }

                if (reopened && !caseDetails.Redirected)
                {
                    return await UpdateClosedCaseAsync();
                }

                if (caseDetails.manualReview && west)
                {
                    String forwardingEmailAddress = await GetSovereignEmailFromDynamoAsync(caseDetails.sovereignCouncil, caseDetails.sovereignServiceArea);
                    if (String.IsNullOrEmpty(forwardingEmailAddress))
                    {
                        forwardingEmailAddress = await GetSovereignEmailFromDynamoAsync(caseDetails.sovereignCouncil, "default");
                        defaultRouting = true;
                    }
                    success = await SendEmails(caseDetails, forwardingEmailAddress, true);
                    if (success)
                    {
                        caseDetails.forward = caseDetails.sovereignCouncil + "-" + caseDetails.sovereignServiceArea;
                        if (caseDetails.sovereignCouncil.ToLower().Equals("northampton") && defaultRouting)
                        {
                            await TransitionCaseAsync("awaiting-review");
                        }
                        else
                        {
                            await TransitionCaseAsync("close-case");
                        }
                    }
                }
                else if (caseDetails.manualReview)
                {
                    if (caseDetails.Redirected)
                    {
                        String forwardingEmailAddress = await GetSovereignEmailFromDynamoAsync(caseDetails.sovereignCouncil, caseDetails.sovereignServiceArea);
                        if (String.IsNullOrEmpty(forwardingEmailAddress))
                        {
                            forwardingEmailAddress = await GetSovereignEmailFromDynamoAsync(caseDetails.sovereignCouncil, "default");
                            defaultRouting = true;
                        }
                        success = await SendEmails(caseDetails, forwardingEmailAddress, true);
                        if (success)
                        {
                            caseDetails.forward = caseDetails.sovereignCouncil + "-" + caseDetails.sovereignServiceArea;
                            await TransitionCaseAsync("close-case");
                        }
                    }
                    else
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
                    }
                    if (success)
                    {
                        await TransitionCaseAsync("close-case");
                    }
                }
                else
                {
                    String searchText = caseDetails.enquiryDetails;
                    if (caseDetails.customerHasUpdated)
                    {
                        searchText = caseDetails.fullEmail + " " + caseDetails.enquiryDetails;
                    }
                    sovereignLocation = await CheckForLocationAsync(caseDetails.Subject + " " + searchText);
                    if (caseDetails.contactUs && !sovereignLocation.Success)
                    {
                        //TODO Not finding location on occasion for NNC
                        Console.WriteLine("INFO : Checking for Location Using customerAddress : " + caseDetails.customerAddress);
                        sovereignLocation = await CheckForLocationAsync(caseDetails.customerAddress);
                    }
                    String service = "";
                    district = caseDetails.District;

                    if (caseDetails.contactUs && !String.IsNullOrEmpty(caseDetails.sovereignServiceArea))
                    {
                        Console.WriteLine(caseReference + " : SovereignServiceArea set using  : " + caseDetails.sovereignServiceArea);
                        service = caseDetails.sovereignServiceArea;
                    }
                    else
                    {
                        Console.WriteLine(caseReference + " : SovereignServiceArea not set using Lex ");
                        //TODO use subject then fullemail
                        if(!caseDetails.Subject.ToLower().Contains("council form has been submitted"))
                        {
                            service = await GetServiceAsync(caseDetails.Subject,true);
                        }
                        if (service.Equals(""))
                        {
                            service = await GetServiceAsync(caseDetails.fullEmail,false);
                        }                                             
                    }

                    if (sovereignLocation.Success)
                    {
                        Console.WriteLine(caseReference + " : Location Found : " + sovereignLocation.SovereignCouncilName.ToLower());
                        String sovereignCouncilName = sovereignLocation.SovereignCouncilName.ToLower();
                        if (!district)
                        {
                            if (west)
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
                        caseDetails.sovereignCouncil = sovereignLocation.SovereignCouncilName;
                        if (preventOutOfArea && west && !sovereignLocation.sovereignWest)
                        {
                            outOfArea = true;
                            UpdateCaseString("email-comments", "Contact destination out of area");
                            await TransitionCaseAsync("unitary-awaiting-review");
                        }
                        else
                        if (preventOutOfArea && !west && sovereignLocation.sovereignWest)
                        {
                            outOfArea = true;
                            UpdateCaseString("email-comments", "Contact destination out of area");
                            await TransitionCaseAsync("hub-awaiting-review");
                        }
                        else
                        {
                            if ((west && !sovereignLocation.sovereignWest) || (!west && sovereignLocation.sovereignWest))
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
                            if (west && sovereignLocation.SovereignCouncilName.ToLower().Equals("northampton") && defaultRouting)
                            {
                                UpdateCaseBoolean("unitary", false);
                                UpdateCaseString("email-comments", "Transitioning case to local process");
                                await TransitionCaseAsync("awaiting-review");
                            }
                            else
                            {
                                success = await SendEmails(caseDetails, forwardingEmailAddress, true);
                                UpdateCaseString("email-comments", "Closing case");
                                await TransitionCaseAsync("close-case");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine(caseReference + " : Location Not Found");
                        Console.WriteLine(caseReference + " : Customer Has Updated : " + caseDetails.customerHasUpdated);
                        if (caseDetails.customerHasUpdated || sovereignLocation.PostcodeFound)
                        {
                            if (west)
                            {
                                Console.WriteLine(caseReference + " : West Transition");
                                await TransitionCaseAsync("unitary-awaiting-review");
                            }
                            else
                            {
                                //TODO Drops here if out of area postcode - need to send confirmation #75

                                Console.WriteLine(caseReference + " : North Transition");
                                await TransitionCaseAsync("hub-awaiting-review");
                            }

                        }
                        else
                        {
                            String emailBody = await FormatEmailAsync(caseDetails, "email-location-request.txt");
                            if (!String.IsNullOrEmpty(emailBody))
                            {
                                if (await SendEmailAsync(secrets.OrganisationNameShort, norbertSendFrom, caseDetails.customerEmail, bccEmailAddress, orgName + " : Your Call Number is " + caseReference, caseDetails.emailID, emailBody, "", false))
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
            catch (ApplicationException error)
            {
                throw new ApplicationException(error.Message);
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
            HttpClient cxmClient = new HttpClient
            {
                BaseAddress = new Uri(cxmEndPoint)
            };
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
                emailBody = emailBody.Replace("NNN", persona);

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
                if (outOfArea)
                {
                    emailBody = emailBody.Replace("YYY", "");
                }
                else
                {
                    emailBody = emailBody.Replace("YYY", FormatLinks(caseDetails));
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

        private async Task<Location> CheckForLocationAsync(String emailBody)
        {
            Console.WriteLine("INFO : Checking for Location");
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
                    Console.WriteLine(caseReference + " : INFO : CheckPostcode input : " + groups[0].Value);
                    Postcode postCodeData = await CheckPostcode(groups[0].Value);
                    Console.WriteLine(caseReference + " : INFO : CheckPostcode response : " + postCodeData.success);
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
                Console.WriteLine("INFO : Found Postcode");
                return sovereignLocation;
            }

            if (west)
            {
                Console.WriteLine("INFO : Checking for West Locations");
                if (emailBody.ToLower().Contains("northampton") || await IsInArea(emailBody.ToLower(), "LocationsNorthampton"))
                {
                    sovereignLocation.SovereignCouncilName = "Northampton";
                    sovereignLocation.sovereignWest = true;
                    sovereignLocation.Success = true;
                    return sovereignLocation;
                }

                if (emailBody.ToLower().Contains("towcester") || await IsInArea(emailBody.ToLower(), "LocationsSouthNorthants"))
                {
                    sovereignLocation.SovereignCouncilName = "south_northants";
                    sovereignLocation.sovereignWest = true;
                    sovereignLocation.Success = true;
                    return sovereignLocation;
                }

                if (emailBody.ToLower().Contains("daventry") || await IsInArea(emailBody.ToLower(), "LocationsDaventry"))
                {
                    sovereignLocation.SovereignCouncilName = "Daventry";
                    sovereignLocation.sovereignWest = true;
                    sovereignLocation.Success = true;
                    return sovereignLocation;
                }

                if (emailBody.ToLower().Contains("wellingborough") || await IsInArea(emailBody.ToLower(), "LocationsWellingborough"))
                {
                    sovereignLocation.SovereignCouncilName = "Wellingborough";
                    sovereignLocation.sovereignWest = false;
                    sovereignLocation.Success = true;
                    return sovereignLocation;
                }

                if (emailBody.ToLower().Contains("kettering") || await IsInArea(emailBody.ToLower(), "LocationsKettering"))
                {
                    sovereignLocation.SovereignCouncilName = "Kettering";
                    sovereignLocation.sovereignWest = false;
                    sovereignLocation.Success = true;
                    return sovereignLocation;
                }

                if (emailBody.ToLower().Contains("corby") || await IsInArea(emailBody.ToLower(), "LocationsCorby"))
                {
                    sovereignLocation.SovereignCouncilName = "Corby";
                    sovereignLocation.sovereignWest = false;
                    sovereignLocation.Success = true;
                    return sovereignLocation;
                }

                if (emailBody.ToLower().Contains("rushden") || await IsInArea(emailBody.ToLower(), "LocationsEastNorthants"))
                {
                    sovereignLocation.SovereignCouncilName = "east_northants";
                    sovereignLocation.sovereignWest = false;
                    sovereignLocation.Success = true;
                    return sovereignLocation;
                }
            }
            else
            {
                Console.WriteLine("INFO : Checking for North Locations");
                if (emailBody.ToLower().Contains("wellingborough") || await IsInArea(emailBody.ToLower(), "LocationsWellingborough"))
                {
                    sovereignLocation.SovereignCouncilName = "Wellingborough";
                    sovereignLocation.sovereignWest = false;
                    sovereignLocation.Success = true;
                    return sovereignLocation;
                }

                if (emailBody.ToLower().Contains("kettering") || await IsInArea(emailBody.ToLower(), "LocationsKettering"))
                {
                    sovereignLocation.SovereignCouncilName = "Kettering";
                    sovereignLocation.sovereignWest = false;
                    sovereignLocation.Success = true;
                    return sovereignLocation;
                }

                if (emailBody.ToLower().Contains("corby") || await IsInArea(emailBody.ToLower(), "LocationsCorby"))
                {
                    sovereignLocation.SovereignCouncilName = "Corby";
                    sovereignLocation.sovereignWest = false;
                    sovereignLocation.Success = true;
                    return sovereignLocation;
                }

                if (emailBody.ToLower().Contains("rushden") || await IsInArea(emailBody.ToLower(), "LocationsEastNorthants"))
                {
                    sovereignLocation.SovereignCouncilName = "east_northants";
                    sovereignLocation.sovereignWest = false;
                    sovereignLocation.Success = true;
                    return sovereignLocation;
                }

                if (emailBody.ToLower().Contains("towcester") || await IsInArea(emailBody.ToLower(), "LocationsSouthNorthants"))
                {
                    sovereignLocation.SovereignCouncilName = "south_northants";
                    sovereignLocation.sovereignWest = true;
                    sovereignLocation.Success = true;
                    return sovereignLocation;
                }

                if (emailBody.ToLower().Contains("daventry") || await IsInArea(emailBody.ToLower(), "LocationsDaventry"))
                {
                    sovereignLocation.SovereignCouncilName = "Daventry";
                    sovereignLocation.sovereignWest = true;
                    sovereignLocation.Success = true;
                    return sovereignLocation;
                }

                if (emailBody.ToLower().Contains("northampton") || await IsInArea(emailBody.ToLower(), "LocationsNorthampton"))
                {
                    sovereignLocation.SovereignCouncilName = "Northampton";
                    sovereignLocation.sovereignWest = true;
                    sovereignLocation.Success = true;
                    return sovereignLocation;
                }
            }
            return sovereignLocation;
        }

        private async Task<Postcode> CheckPostcode(String postcode)
        {
            Postcode postCodeData = new Postcode();

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, postCodeURL + postcode.Replace(" ", ""));

            HttpClient httpClient = new HttpClient();

            using HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    String responseString = await response.Content.ReadAsStringAsync();
                    Console.WriteLine(caseReference + " : INFO : APIResponse : " + responseString);
                    JObject caseSearch = JObject.Parse(responseString);
                    try
                    {
                        JArray sovereignArray = (JArray)caseSearch.SelectToken("sovereigns");
                        if (sovereignArray.Count == 1)
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
                        JArray unitariesArray = (JArray)caseSearch.SelectToken("unitaries");
                        if (unitariesArray.Count == 1)
                        {
                            postCodeData.singleUni = true;
                            if (((String)caseSearch.SelectToken("unitaries[0].name")).ToLower().Equals("west"))
                            {
                                postCodeData.west = true;
                            }
                        }
                        else
                        {
                            UpdateCaseString("email-comments", "Postcode spans both WNC and NNC");
                        }
  
                    }
                    catch (Exception) { }
                    try
                    {
                        postCodeData.SovereignCouncilName = (String)caseSearch.SelectToken("sovereigns[0].name").ToString().ToLower();
                    }
                    catch (Exception) { }
                    try
                    {
                        if (postCodeData.SovereignCouncilName.Equals("south northamptonshire"))
                        {
                            postCodeData.SovereignCouncilName = "south_northants";
                        }
                    }
                    catch (Exception) { }
                    try
                    {
                        if (postCodeData.SovereignCouncilName.Equals("east northamptonshire"))
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
                UpdateCaseString("email-comments",  "Postcode API failed - assigned to staff");
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

        private Boolean UpdateCaseBoolean(String fieldName, Boolean fieldValue)
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
                using StreamReader reader = new StreamReader(patchResponse.GetResponseStream(), Encoding.Default);
                result = reader.ReadToEnd();
            }
            catch (Exception error)
            {
                Console.WriteLine(caseReference + " : " + error.ToString());
                return false;
            }
            return true; ;
        }

        private async Task<string> GetServiceAsync(String customerContact, Boolean usingSubject)
        {
            try
            {
                AmazonLexClient lexClient = new AmazonLexClient(primaryRegion);
                PostTextRequest textRequest = new PostTextRequest
                {
                    UserId = "MailBot",
                    BotAlias = lexAlias,
                    BotName = "UnitaryServices",
                    InputText = customerContact
                };
                PostTextResponse textResponse = await lexClient.PostTextAsync(textRequest);
                HttpStatusCode temp = textResponse.HttpStatusCode;
                String intentName = textResponse.IntentName;
                if (usingSubject)
                {
                    if(long.TryParse(SubjectServiceMinConfidence, out long minConfidence))
                    {
                        if (String.IsNullOrEmpty(intentName) || textResponse.NluIntentConfidence.Score < (minConfidence/100))
                        {
                            return "";
                        }
                    }
                }
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
                attributeMap.TryGetValue("email", out AttributeValue sovereignEmailAttribute);
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
                attributeMap.TryGetValue("email", out AttributeValue sovereignEmailAttribute);
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

        private Boolean GetBooleanValueFromJSON(JObject json, String fieldName)
        {
            Boolean returnValue = false;
            try
            {
                returnValue = (Boolean)json.SelectToken(fieldName);
            }
            catch (Exception) { }
            return returnValue;
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
            try
            {
                Console.WriteLine(caseReference + " : SendEmails Started");
                String emailBody = "";
                if (replyToCustomer && !caseDetails.ConfirmationSent)
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
                        if (!await SendEmailAsync(secrets.OrganisationNameShort, norbertSendFrom, caseDetails.customerEmail, bccEmailAddress, subject, caseDetails.emailID, emailBody, "", false))
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
                    if (UpdateCaseBoolean("confirmation-sent", true)) { }
                    else
                    {
                        Console.WriteLine(caseReference + " : ERROR : Failed to update confirmation-sent");
                        UpdateCaseString("email-comments", "Failed to update confirmation-sent");
                    }
                }

                Console.WriteLine(caseReference + " : Sending forward email");

                if (west && caseDetails.sovereignCouncil.ToLower().Equals("northampton") && defaultRouting)
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
                        if (!await SendEmailAsync(secrets.OrganisationNameShort, norbertSendFrom, forwardingEmailAddress.ToLower(), bccEmailAddress, subjectPrefix + "Hub case reference number is " + caseReference, caseDetails.emailID, emailBody, "", true))
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
            catch (ApplicationException error)
            {
                throw new ApplicationException(error.Message);
            }

        }

        private async Task<Boolean> SendToTrello(String caseReference, String fieldLabel, String techLabel)
        {
            try
            {
                HttpClient cxmClient = new HttpClient
                {
                    BaseAddress = new Uri("https://api.trello.com")
                };
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
            SendTaskSuccessRequest successRequest = new SendTaskSuccessRequest
            {
                TaskToken = taskToken
            };
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
            SendTaskFailureRequest failureRequest = new SendTaskFailureRequest
            {
                Cause = failureCause,
                Error = failureError,
                TaskToken = taskToken
            };

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

        private async Task<Boolean> IsInArea(String emailContents, String tableName)
        {
            List<String> locations = new List<String>();
            Dictionary<string, AttributeValue> lastKeyEvaluated = null;
            try
            {
                AmazonDynamoDBClient dynamoDBClient = new AmazonDynamoDBClient(primaryRegion);
                do
                {
                    ScanRequest request = new ScanRequest
                    {
                        TableName = tableName,
                        Limit = 10,
                        ExclusiveStartKey = lastKeyEvaluated,
                        ProjectionExpression = "LocationText"
                    };

                    ScanResponse response = await dynamoDBClient.ScanAsync(request);
                    foreach (Dictionary<string, AttributeValue> item
                         in response.Items)
                    {
                        item.TryGetValue("LocationText", out AttributeValue value);
                        locations.Add(value.S.ToLower());
                    }
                    lastKeyEvaluated = response.LastEvaluatedKey;
                } while (lastKeyEvaluated != null && lastKeyEvaluated.Count != 0);
                foreach (String location in locations)
                {
                    if (emailContents.ToLower().Contains(location))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception error)
            {
                Console.WriteLine("ERROR : ISAutoResponse :" + error.Message);
                Console.WriteLine(error.StackTrace);
                return false;
            }
        }

        public async Task<Boolean> SendEmailAsync(String from, String fromAddress, String toAddress, String bccAddress, String subject, String emailID, String htmlBody, String textBody, Boolean includeOriginalEmail)
        {
            using AmazonSimpleEmailServiceClient client = new AmazonSimpleEmailServiceClient(RegionEndpoint.EUWest1);
            try
            {
                SendRawEmailRequest sendRequest = new SendRawEmailRequest { RawMessage = new RawMessage(await GetMessageStreamAsync(from, fromAddress, toAddress, subject, emailID, htmlBody, bccAddress, includeOriginalEmail)) };
                SendRawEmailResponse response = await client.SendRawEmailAsync(sendRequest);
                return true;
            }
            catch (ApplicationException error)
            {
                throw new ApplicationException(error.Message);
            }
            catch (Exception error)
            {
                Console.WriteLine(caseReference + " : Error Sending Raw Email : " + error.Message);
                return false;
            }
        }

        private async Task<MemoryStream> GetMessageStreamAsync(String from, String fromAddress, String toAddress, String subject, String emailID, String htmlBody, String bccAddress, Boolean includeOriginalEmail)
        {
            MemoryStream stream = new MemoryStream();
            try
            {
                MimeMessage message = await GetMessageAsync(from, fromAddress, toAddress, subject, emailID, htmlBody, bccAddress, includeOriginalEmail);
                message.WriteTo(stream);
                return stream;
            }
            catch (ApplicationException error)
            {
                throw new ApplicationException(error.Message);
            }
        }

        private async Task<MimeMessage> GetMessageAsync(String from, String fromAddress, String toAddress, String subject, String emailID, String htmlBody, String bccAddress, Boolean includeOriginalEmail)
        {
            MimeMessage message = new MimeMessage();
            message.From.Add(new MailboxAddress(from, fromAddress));
            message.To.Add(new MailboxAddress(string.Empty, toAddress));
            message.Bcc.Add(new MailboxAddress(string.Empty, bccAddress));
            message.Subject = subject;

            try
            {
                message = await GetMessageBodyAsync(message, emailID, htmlBody, includeOriginalEmail);
                return message;
            }
            catch (ApplicationException error)
            {
                throw new ApplicationException(error.Message);
            }
        }

        private async Task<MimeMessage> GetMessageBodyAsync(MimeMessage message, String emailID, String htmlBody, Boolean includeOriginalEmail)
        {
            byte[] textBodyBytes = Encoding.UTF8.GetBytes("Test");
            byte[] htmlBodyBytes = Encoding.UTF8.GetBytes(htmlBody);
            TextPart plain = new TextPart();
            TextPart html = new TextPart("html");
            plain.ContentTransferEncoding = ContentEncoding.Base64;
            html.ContentTransferEncoding = ContentEncoding.Base64;
            plain.SetText(Encoding.UTF8, Encoding.Default.GetString(textBodyBytes));
            html.SetText(Encoding.UTF8, Encoding.Default.GetString(htmlBodyBytes));
            MultipartAlternative alternative = new MultipartAlternative
            {
                plain,
                html
            };
            Multipart multipart = new Multipart("mixed")
            {
                alternative
            };

            if (includeOriginalEmail)
            {
                try
                {
                    AmazonS3Client s3 = new AmazonS3Client(emailsRegion);
                    GetObjectResponse image = await s3.GetObjectAsync(emailBucket, emailID);
                    byte[] imageBytes = new byte[image.ContentLength];
                    int read;
                    byte[] buffer = new byte[16 * 1024];
                    while ((read = image.ResponseStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        memoryStream.Write(buffer, 0, read);
                    }
                    imageBytes = memoryStream.ToArray();
                    MimePart attachment = new MimePart("message", "rfc822")
                    {
                        Content = new MimeContent(memoryStream, ContentEncoding.Default),
                        ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                        ContentTransferEncoding = ContentEncoding.Base64,
                        FileName = caseReference + ".eml"
                    };
                    multipart.Add(attachment);
                }
                catch (Exception error)
                {
                    UpdateCaseString("email-comments", Messages.missingEmailFile);
                    if (west)
                    {
                        await TransitionCaseAsync("unitary-awaiting-review");
                    }
                    else
                    {
                        await TransitionCaseAsync("hub-awaiting-review");
                    }
                    Console.WriteLine(caseReference + " : Error Attaching original email : " + error.Message);
                    throw new ApplicationException("Error Attaching original email");
                }

            }
            message.Body = multipart;
            return message;
        }

        private static String RemoveSuppressionList(String suppressionlist, String content)
        {
            String[] words = suppressionlist.Split(',');

            foreach (String word in words)
            {
                try
                {
                    content = content.ToLower().Replace(word.ToLower(), "");
                }
                catch (Exception) { }
            }
            return content;
        }

        private async Task<bool> UpdateClosedCaseAsync()
        {
            Console.WriteLine(caseReference + " : Transitioning to staff review due to case being closed");
            if (west)
            {
                return await TransitionCaseAsync("unitary-awaiting-review");
            }
            else
            {
                return await TransitionCaseAsync("hub-awaiting-review");
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
    public String Subject { get; set; } = "";
    public Boolean customerHasUpdated { get; set; } = false;
    public Boolean manualReview { get; set; } = false;
    public Boolean contactUs { get; set; } = false;
    public Boolean District { get; set; } = false;
    public Boolean ConfirmationSent { get; set; } = false;
    public Boolean Redirected { get; set; } = false;
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
    public String cxmEndPointTestNorth { get; set; }
    public String cxmEndPointLiveNorth { get; set; }
    public String cxmAPIKeyTestNorth { get; set; }
    public String cxmAPIKeyLiveNorth { get; set; }
    public String cxmAPINameNorth { get; set; }
    public String cxmAPINameWest { get; set; }
    public String organisationName { get; set; }
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
    public String SuppressWording { get; set; }
    public String OrganisationNameShort { get; set; }
    public String botPersona1 { get; set; }
    public String botPersona2 { get; set; }
    public String RedirectURI { get; set; }
    public String SubjectServiceMinConfidenceTest { get; set; }
    public String SubjectServiceMinConfidenceLive { get; set; }
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