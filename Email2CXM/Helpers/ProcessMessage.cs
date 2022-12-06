using Amazon;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2;
using Amazon.S3;
using Amazon.S3.Model;
using MimeKit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Amazon.DynamoDBv2.Model;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using System.Linq;
using HtmlAgilityPack;
using System.Text.Encodings.Web;

namespace Email2CXM.Helpers
{
    class ProcessMessage
    {
        private static readonly RegionEndpoint primaryRegion = RegionEndpoint.EUWest2;
        private static readonly RegionEndpoint bucketRegion = RegionEndpoint.EUWest1;

        private static readonly String secretName = "nbcGlobal";
        private static readonly String secretAlias = "AWSCURRENT";

        private static String cxmEndPoint;
        private static String cxmAPIKey;
        private static String cxmAPIName;
        private static String cxmAPICaseType;

        private static String tableName = "";

        private Secrets secrets = null;

        private static IAmazonS3 client;
        private string EmailFrom { get; set; } = null;
        private string EmailTo { get; set; } = null;
        private string Subject { get; set; } = null;
        private string serviceArea { get; set; } = null;
        private string AutoResponseTable = null;
        public string firstName { get; set; } = null;
        public string lastName { get; set; } = null;
        public string emailBody { get; set; } = null;
        public string caseReference { get; set; } = null;
        public string emailContents { get; set; } = null;
        public string telNo { get; set; } = null;
        public string address { get; set; } = null;
        public string ContactUsTableMapping { get; set; } = null;

        private string MyCouncilEndPoint = "";

        private Boolean create = true;
        private Boolean unitary = false;
        private Boolean contactUs = false;
        private Boolean district = true;
        private Boolean fixMyStreet = false;
        private Boolean bundlerFound = false;
        private Boolean west = true;
        private Boolean useSigParser = true;

        private MimeMessage message = null;

        MyCouncilCase myCouncilCase = null;

        public ProcessMessage()
        {
            client = new AmazonS3Client(bucketRegion);
        }

        public Boolean Process(String bucketName, String keyName, Boolean liveInstance)
        {
            create = true;
            unitary = false;
            contactUs = false;
            district = true;
            fixMyStreet = false;
            bundlerFound = false;
            west = true;
            useSigParser = true;
            message = null;

            if (bucketName.ToLower().Contains("incoming"))
            {
                client = new AmazonS3Client(primaryRegion);
            }
            return ReadObjectDataAsync(bucketName, keyName, liveInstance).Result;
        }

        private async Task<Boolean> ReadObjectDataAsync(String bucketName, String keyName, Boolean liveInstance)
        {
            Console.WriteLine(" ");
            try
            {
                GetObjectRequest request = new GetObjectRequest
                {
                    BucketName = bucketName,
                    Key = keyName
                };
                using (GetObjectResponse response = await client.GetObjectAsync(request))
                {
                    message = MimeMessage.Load(response.ResponseStream);
                    MailAddressCollection mailFromAddresses = (MailAddressCollection)message.From;
                    MailAddressCollection mailToAddresses = (MailAddressCollection)message.To;

                    try
                    {
                        EmailTo = mailToAddresses[0].Address.ToString().ToLower();
                        EmailFrom = mailFromAddresses[0].Address.ToString().ToLower();
                        Console.WriteLine(EmailFrom + " - Processing email sent to this address : " + EmailTo);
                        if (EmailTo.Contains("update"))
                        {
                            Console.WriteLine(EmailFrom + " - Update Case");
                            create = false;
                        }
                        else
                        {
                            Console.WriteLine(EmailFrom + " - Create Case");
                        }
                        if (EmailTo.ToLower().Equals(await GetStringFieldFromDynamoAsync(EmailTo.ToLower(), "email", "UnitaryEmailAddresses")))
                        {
                            unitary = true;
                        }
                        if (EmailTo.ToLower().Contains("northnorthants"))
                        {
                            west = false;
                        }
                        if (EmailFrom.ToLower().Contains("noreply@northamptonshire.gov.uk") && message.Subject.ToLower().Contains("northamptonshire council form has been submitted"))
                        {
                            contactUs = true;
                            useSigParser = false;
                        }
                        if (EmailFrom.ToLower().Contains("fixmystreet.com"))
                        {
                            fixMyStreet = true;
                            useSigParser = false;
                        }
                        if (mailToAddresses[0].Address.ToLower().Contains("document"))
                        {
                            bundlerFound = true;
                        }

                    }
                    catch (Exception)
                    {
                    }
                    Subject = message.Subject;

                    if (String.IsNullOrWhiteSpace(Subject))
                    {
                        Subject = " ";
                    }
                    List<String> names = message.From[0].Name.Split(' ').ToList();
                    firstName = names.First();
                    names.RemoveAt(0);
                    lastName = String.Join(" ", names.ToArray());
                    emailBody = message.HtmlBody;
                    Console.WriteLine(EmailFrom + " - Email Contents : " + message.TextBody);
                    if (String.IsNullOrEmpty(message.TextBody))
                    {
                        if (String.IsNullOrEmpty(message.HtmlBody))
                        {
                            emailContents = getBodyFromBase64(message);
                        }
                        else
                        {
                            HtmlDocument emailHTML = new HtmlDocument();
                            String htmlBody = message.HtmlBody.Replace("<br/>", "\r\n");
                            emailHTML.LoadHtml(htmlBody);
                            emailContents = emailHTML.DocumentNode.InnerText;
                        }
                    }
                    else
                    {
                        emailContents = message.TextBody;
                    }

                    String person = "";

                    String responseFileName = "";
                    String parsedEmailEncoded = "";
                    String parsedEmailUnencoded = "";

                    String emailFromName = "";

                    if (await GetSecrets())
                    {
                        if (liveInstance)
                        {
                            MyCouncilEndPoint = secrets.MyCouncilLiveEndPoint;
                            if (west)
                            {
                                cxmEndPoint = secrets.cxmEndPointLive;
                                cxmAPIKey = secrets.cxmAPIKeyLive;
                                cxmAPIName = secrets.cxmAPINameWest;
                                cxmAPICaseType = secrets.cxmAPICaseTypeWestLive;
                                tableName = secrets.wncEMACasesLive;
                                ContactUsTableMapping = secrets.WNCContactUsMappingTable;
                                AutoResponseTable = secrets.AutoResponseTableLive;
                            }
                            else
                            {
                                cxmEndPoint = secrets.cxmEndPointLiveNorth;
                                cxmAPIKey = secrets.cxmAPIKeyLiveNorth;
                                cxmAPIName = secrets.cxmAPINameNorth;
                                cxmAPICaseType = secrets.cxmAPICaseTypeNorthLive;
                                tableName = secrets.nncEMNCasesLive;
                                ContactUsTableMapping = secrets.NNCContactUsMappingTable;
                                AutoResponseTable = secrets.AutoResponseTableLive;
                            }

                        }
                        else  
                        {
                            MyCouncilEndPoint = secrets.MyCouncilTestEndPoint;
                            if (west)
                            {
                                cxmEndPoint = secrets.cxmEndPointTest;
                                cxmAPIKey = secrets.cxmAPIKeyTest;
                                cxmAPIName = secrets.cxmAPINameWest;
                                cxmAPICaseType = secrets.cxmAPICaseTypeWest;
                                tableName = secrets.wncEMACasesTest;
                                ContactUsTableMapping = secrets.WNCContactUsMappingTable;
                                AutoResponseTable = secrets.AutoResponseTableTest;
                            }
                            else
                            {
                                cxmEndPoint = secrets.cxmEndPointTestNorth;
                                cxmAPIKey = secrets.cxmAPIKeyTestNorth;
                                cxmAPIName = secrets.cxmAPINameNorth;
                                cxmAPICaseType = secrets.cxmAPICaseTypeNorth;
                                tableName = secrets.nncEMNCasesTest;
                                ContactUsTableMapping = secrets.NNCContactUsMappingTable;
                                AutoResponseTable = secrets.AutoResponseTableTest;
                            }
                        }

                        Random rand = new Random();
                        if (rand.Next(0, 2) == 0)
                        {
                            emailFromName = secrets.botPersona1;
                        }
                        else
                        {
                            emailFromName = secrets.botPersona2;
                        }
                    }
                    else
                    {
                        Console.WriteLine("ERROR : Unable to retrieve secrets");
                        return false;
                    }

                    try
                    {
                        await GetCommonSignaturesAsync(emailContents);
                        String corporateSignature = await GetSignatureFromDynamoAsync(secrets.homeDomain, "");
                        int corporateSignatureLocation = emailContents.IndexOf(corporateSignature);
                        if (corporateSignatureLocation > 0)
                        {
                            Console.WriteLine("Home Domain Corporate Signature Found");
                            emailContents = emailContents.Remove(corporateSignatureLocation);
                        }
                        else
                        {
                            Console.WriteLine("Home Domain Corporate Signature Not Found");
                        }
                        for (int currentAddress = 0; currentAddress < mailFromAddresses.Count; currentAddress++)
                        {
                            int domainLocation = mailFromAddresses[currentAddress].Address.IndexOf("@");
                            domainLocation++;
                            if (mailFromAddresses[currentAddress].Address.ToLower().Equals("customerservices@northamptonshire.gov.uk"))
                            {
                                corporateSignature = await GetSignatureFromDynamoAsync(mailFromAddresses[currentAddress].Address.ToLower().Substring(domainLocation), "2");
                            }
                            else
                            {
                                corporateSignature = await GetSignatureFromDynamoAsync(mailFromAddresses[currentAddress].Address.ToLower().Substring(domainLocation), "");
                            }
                            if (mailFromAddresses[currentAddress].Address.ToLower().Equals("noreply@northamptonshire.gov.uk"))
                            {
                                int nccSignatureLocation = emailContents.IndexOf("Any views expressed in this email are those of the individual sender");
                                if (nccSignatureLocation > 0)
                                {
                                    emailContents = emailContents.Substring(0, nccSignatureLocation);
                                    emailContents = emailContents.Trim();
                                }
                            }
                            corporateSignatureLocation = emailContents.IndexOf(corporateSignature);
                            if (corporateSignatureLocation > 0)
                            {
                                Console.WriteLine("Corporate Signature Found " + currentAddress);
                                emailContents = emailContents.Remove(corporateSignatureLocation);
                            }
                            else
                            {
                                Console.WriteLine("Corporate Signature Not Found " + currentAddress);
                            }
                        }

                        if (useSigParser)
                        {
                            SigParser.Client sigParserClient = new SigParser.Client(secrets.sigParseKey);
                            SigParser.EmailParseRequest sigParserRequest = new SigParser.EmailParseRequest { plainbody = emailContents, from_name = firstName + " " + lastName, from_address = EmailFrom };
                            parsedEmailUnencoded = sigParserClient.Parse(sigParserRequest).cleanedemailbody_plain;
                            if ((parsedEmailUnencoded == null || parsedEmailUnencoded.Contains("___")) && !bundlerFound)
                            {
                                Console.WriteLine($"No message found, checking for forwarded message");
                                parsedEmailUnencoded = sigParserClient.Parse(sigParserRequest).emails[1].cleanedBodyPlain;
                                EmailFrom = sigParserClient.Parse(sigParserRequest).emails[1].from_EmailAddress;
                                names = sigParserClient.Parse(sigParserRequest).emails[1].from_Name.Split(' ').ToList();
                                firstName = names.First();
                                names.RemoveAt(0);
                                lastName = String.Join(" ", names.ToArray());
                            }
                            Console.WriteLine($"Cleaned email body is : {parsedEmailUnencoded}");
                            parsedEmailEncoded = HttpUtility.UrlEncode(parsedEmailUnencoded);
                            Console.WriteLine($"Encoded email body is : {parsedEmailEncoded}");

                        }
                        if(contactUs)
                        {
                            try
                            {
                                int serviceAreaStarts = emailContents.ToLower().IndexOf("service: ") + 9;
                                int serviceAreaEnds = emailContents.ToLower().IndexOf("enquiry details:");
                                serviceArea = (emailContents.Substring(serviceAreaStarts, serviceAreaEnds - serviceAreaStarts).TrimEnd('\r', '\n')).ToLower();
                            }
                            catch { }
                            try
                            {
                                int emailAddressStarts = emailContents.ToLower().IndexOf("email address:") + 15;
                                int emailAddressEnds = emailContents.ToLower().IndexOf("telephone number:");
                                EmailFrom = emailContents.Substring(emailAddressStarts, emailAddressEnds - emailAddressStarts).TrimEnd('\r', '\n');
                            }
                            catch { }
                            try
                            {
                                int firstNameStarts = emailContents.ToLower().IndexOf("first name: ") + 12;
                                int firstNameEnds = emailContents.ToLower().IndexOf("last name:");
                                firstName = emailContents.Substring(firstNameStarts, firstNameEnds - firstNameStarts).TrimEnd('\r', '\n');
                            }
                            catch { }
                            try
                            {
                                int lastNameStarts = emailContents.ToLower().IndexOf("last name: ") + 11;
                                int lastNameEnds = emailContents.ToLower().IndexOf("email address:");
                                lastName = emailContents.Substring(lastNameStarts, lastNameEnds - lastNameStarts).TrimEnd('\r', '\n');
                            }
                            catch { }
                            try
                            {
                                int telNoStarts = emailContents.ToLower().IndexOf("telephone number: ") + 18;
                                int telNoEnds = emailContents.ToLower().IndexOf("address line 1:");
                                telNo = emailContents.Substring(telNoStarts, telNoEnds - telNoStarts).TrimEnd('\r', '\n');
                            }
                            catch { }
                            try
                            {
                                int address1Starts = emailContents.ToLower().IndexOf("address line 1: ") + 16;
                                int address1Ends = emailContents.ToLower().IndexOf("address line 2:");
                                address = emailContents.Substring(address1Starts, address1Ends - address1Starts).TrimEnd('\r', '\n') + ", ";
                            }
                            catch { }
                            try
                            {
                                int address2Starts = emailContents.ToLower().IndexOf("address line 2: ") + 16;
                                int address2Ends = emailContents.ToLower().IndexOf("address line 3:");
                                if (address2Ends < 0)
                                {
                                    address2Ends = emailContents.ToLower().IndexOf("postcode:");
                                }
                                address += emailContents.Substring(address2Starts, address2Ends - address2Starts).TrimEnd('\r', '\n') + ", ";
                            }
                            catch { }
                            try
                            {
                                int address3Starts = emailContents.ToLower().IndexOf("address line 3: ") + 16;
                                int address3Ends = emailContents.ToLower().IndexOf("postcode:");
                                if (address3Starts > 16)
                                {
                                    address += emailContents.Substring(address3Starts, address3Ends - address3Starts).TrimEnd('\r', '\n');
                                }
                            }
                            catch { }
                            try
                            {
                                int postcodeStarts = emailContents.ToLower().IndexOf("postcode: ") + 10;
                                int postcodeEnds = emailContents.Length;
                                if (postcodeEnds - postcodeStarts < 10)
                                {
                                    address += ", " + emailContents.Substring(postcodeStarts, postcodeEnds - postcodeStarts).TrimEnd('\r', '\n');
                                }
                            }
                            catch { }
                            try
                            {
                                int startOfContact = emailContents.ToLower().IndexOf("enquiry details: ") + 17;
                                int endOfContact = emailContents.ToLower().IndexOf("about you");
                                emailContents = emailContents.Substring(startOfContact, endOfContact - startOfContact).TrimEnd('\r', '\n');
                                parsedEmailUnencoded = emailContents;
                                parsedEmailEncoded = HttpUtility.UrlEncode(emailContents);
                            }
                            catch { }
                        }

                        if (fixMyStreet)
                        {
                            firstName = FMSDeserializeFirstName(emailContents);
                            lastName = FMSDeserializeLastName(emailContents);
                            String description = FMSDeserializeIssueTitle(emailContents);
                            String lat = FMSDeserializeLat(emailContents);
                            String lng = FMSDeserializeLng(emailContents);
                            String northing = FMSDeserializeNorthing(emailContents);
                            String easting = FMSDeserializeEasting(emailContents);
                            EmailFrom = FMSDeserializeEmail(emailContents);
                            //TODO Temp Code!!!!
                            //EmailFrom = "kevin.white@clubpit.com";
                            telNo = FMSDeserializePhone(emailContents);
                            String road = FMSDeserializeStreet(emailContents);
                            String postcode = FMSDeserializePostcode(emailContents);
                            String details = FMSDeserializeDetails(message.HtmlBody);
                            String usrn = await GetUSRN(lat, lng);
                            String type = await GetStringFieldFromDynamoAsync(FMSDeserializeCategory(message.HtmlBody).ToLower(), "Classification", "FixMyStreetNorthampton");
                            myCouncilCase = await CreateMyCouncilCase(type, lat, lng, description + " - " + details, road, usrn, EmailFrom, telNo, firstName + " " + lastName); 
                            //TODO Transition to with-digital if no type match
                            //TODO transition to with-digital if no usrn
                        }
                    }
                    catch (Exception error)
                    {
                        parsedEmailUnencoded = emailContents;
                        parsedEmailEncoded = HttpUtility.UrlEncode(emailContents);
                        Console.WriteLine("ERROR : An Unknown error encountered : {0}' when reading email", error.Message);
                        Console.WriteLine(error.StackTrace);
                    }

                    using (var client = new HttpClient())
                    {
                        try
                        {
                            HttpResponseMessage responseMessage = await client.GetAsync(
                            cxmEndPoint + "/api/service-api/norbert/user/" + EmailFrom + "?key=" + cxmAPIKey);
                            responseMessage.EnsureSuccessStatusCode();
                            String responseBody = await responseMessage.Content.ReadAsStringAsync();
                            dynamic jsonResponse = JObject.Parse(responseBody);
                            person = jsonResponse.person.reference;
                            Console.WriteLine("Person found");
                        }
                        catch (Exception error)
                        {
                            Console.WriteLine("Person NOT found : " + error.Message);
                            person = "";
                        }
                    }
                    parsedEmailEncoded = JsonConvert.ToString(parsedEmailUnencoded);

                    if (String.IsNullOrEmpty(parsedEmailUnencoded))
                    {
                        if (bundlerFound)
                        {
                            emailBody = "PDF Bundler";
                            parsedEmailUnencoded = "PDF Bundler";
                        }
                        else
                        {
                            emailBody = "Empty message";
                            parsedEmailUnencoded = "Empty message";
                        }
                    }


                    if (EmailFrom.Contains(secrets.loopPreventIdentifier) || (EmailTo.ToLower().Contains("update") && (EmailFrom.ToLower().Contains("westnorthants.gov.uk") || EmailFrom.ToLower().Contains("northnorthants.gov.uk"))))
                    {
                        Console.WriteLine(EmailFrom + " - Loop identifier found - no case created or updated : " + keyName);
                    }
                    else
                    {
                        if (Subject.Contains("EMA") || parsedEmailUnencoded.Contains("EMA") || Subject.Contains("EMN") || parsedEmailUnencoded.Contains("EMN"))
                        {
                            HttpClient client = new HttpClient();
                            String caseNumber = "";
                            if (west)
                            {
                                if (Subject.Contains("EMA"))
                                {
                                    int refLocation = Subject.IndexOf("EMA");
                                    caseNumber = Subject.Substring(refLocation, 9);
                                }
                                else
                                {
                                    int refLocation = parsedEmailUnencoded.IndexOf("EMA");
                                    caseNumber = parsedEmailUnencoded.Substring(refLocation, 9);
                                }
                            }
                            else
                            {
                                if (Subject.Contains("EMN"))
                                {
                                    int refLocation = Subject.IndexOf("EMN");
                                    caseNumber = Subject.Substring(refLocation, 9);
                                }
                                else
                                {
                                    int refLocation = parsedEmailUnencoded.IndexOf("EMN");
                                    caseNumber = parsedEmailUnencoded.Substring(refLocation, 9);
                                }
                            }

                            caseReference = caseNumber;

                            if (await IsAutoResponse(parsedEmailUnencoded))
                            {
                                Console.WriteLine(caseReference + " : " + EmailFrom + " - Autoresponder Text Found : " + keyName);
                            }
                            else
                            {
                                String fieldName = "enquiry-details";

                                String data = "{\"" + fieldName + "\":" + parsedEmailEncoded + "," +
                                              "\"" + "customer-has-updated" + "\":" + "true" +
                                              "}";

                                Console.WriteLine($"PATCH payload : " + data);

                                String url = cxmEndPoint + "/api/service-api/" + cxmAPIName + "/case/" + caseNumber + "/edit?key=" + cxmAPIKey;
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
                                    Console.WriteLine(caseNumber + " : " + error.ToString());
                                    Console.WriteLine(caseNumber + " : Error updating CXM field " + fieldName + " with message : " + message);
                                }

                                String unitary = await GetStringFieldFromDynamoAsync(caseReference, "Unitary", tableName);

                                if (unitary.Equals("true"))
                                {
                                    await TransitionCaseAsync("awaiting-location-confirmation");
                                }
                                else
                                {
                                    await TransitionCaseAsync("awaiting-review");
                                }
                            }
                        }
                        else
                        {
                            if (create)
                            {
                                String cxmSovereignServiceArea = "";
                                if (contactUs)
                                {
                                    if (west)
                                    {
                                        cxmSovereignServiceArea = await GetStringFieldFromDynamoAsync(serviceArea, "LexService", ContactUsTableMapping);
                                        if (cxmSovereignServiceArea.ToLower().Contains("county"))
                                        {
                                            district = false;
                                        }
                                        try
                                        {                                        
                                            cxmSovereignServiceArea = cxmSovereignServiceArea.Substring(cxmSovereignServiceArea.IndexOf("_") + 1);
                                        }
                                        catch (Exception) { }
                                    }
                                    else
                                    {
                                        cxmSovereignServiceArea = await GetStringFieldFromDynamoAsync(serviceArea, "LexService", ContactUsTableMapping);
                                        if (cxmSovereignServiceArea.ToLower().Contains("county"))
                                        {
                                            district = false;
                                        }
                                        try
                                        {
                                            if (cxmSovereignServiceArea.Equals("district_waste"))
                                            {
                                                cxmSovereignServiceArea = cxmSovereignServiceArea.Replace("_", "");
                                            }
                                            else
                                            {
                                                cxmSovereignServiceArea = cxmSovereignServiceArea.Substring(cxmSovereignServiceArea.IndexOf("_") + 1);
                                            }
                                        }
                                        catch (Exception) { }
                                    }
                                }

                                Boolean success = true;

                                String telNoField = "";

                                if (west)
                                {
                                    telNoField = "telephone-number";
                                }
                                else
                                {
                                    telNoField = "customer-telephone-number";
                                }
                                String comments = "";
                                if (fixMyStreet)
                                {
                                    if (myCouncilCase.Success)
                                    {
                                        comments = "Report It case raised : " + myCouncilCase.CaseReference;
                                    }
                                    else
                                    {
                                        comments = "Unable to raise Report It case : " + "???";
                                    }
                                }
                                   

                                if (contactUs)
                                {
                                    Dictionary<String, Object> values = new Dictionary<String, Object>
                                    {
                                        { "first-name", firstName },
                                        { "surname", lastName },
                                        { "email", EmailFrom },
                                        { "subject", Subject },
                                        { "enquiry-details", await TrimEmailContents(parsedEmailUnencoded)},
                                        { "customer-has-updated", false },
                                        { "unitary", unitary },
                                        { "contact-us", contactUs },
                                        { "district", district },
                                        { telNoField, telNo },
                                        { "customer-address", address },
                                        { "email-id", keyName},
                                        { "sovereign-service-area", cxmSovereignServiceArea },
                                        { "original-email", await TrimEmailContents(parsedEmailUnencoded) }
                                    };
                                    success = await CreateCXMCase(values, parsedEmailUnencoded, message, person, comments, bundlerFound);
                                    if (!success)
                                    {
                                        Console.WriteLine("ERROR - Retrying case without cxmSovereignServiceArea of : " + cxmSovereignServiceArea);
                                    }
                                }
                                if (!contactUs || (contactUs && !success))
                                {
                                    Dictionary<String, Object> values = new Dictionary<String, Object>
                                    {
                                        { "first-name", firstName },
                                        { "surname", lastName },
                                        { "email", EmailFrom },
                                        { "subject", Subject },
                                        { "enquiry-details", await TrimEmailContents(parsedEmailUnencoded) },
                                        { "customer-has-updated", false },
                                        { "unitary", unitary },
                                        { "contact-us", contactUs },
                                        { "district", district },
                                        { telNoField, telNo },
                                        { "customer-address", address },
                                        { "email-id", keyName},
                                        { "original-email", await TrimEmailContents(message.TextBody) }
                                    };
                                    await CreateCXMCase(values, parsedEmailUnencoded, message, person, comments, bundlerFound);
                                }

                                responseFileName = "email-no-faq.txt";

                                await StoreContactToDynamoAsync(caseReference, parsedEmailUnencoded, unitary);

                                if (bundlerFound)
                                {
                                    await TransitionCaseAsync("awaiting-bundling");
                                }

                                if (fixMyStreet&&myCouncilCase.Success)
                                {
                                    UpdateCaseString("case-update-details", "Case created as a result of : " + caseReference,myCouncilCase.CaseReference);
                                    await TransitionCaseAsync("close-case");
                                }
                            }

                            String attachmentBucketName = "";
                            if (west)
                            {
                                if (liveInstance)
                                {
                                    attachmentBucketName = secrets.wncAttachmentBucketLive;
                                }
                                else
                                {
                                    attachmentBucketName = secrets.wncAttachmentBucketTest;
                                }
                            }
                            else
                            {
                                if (liveInstance)
                                {
                                    attachmentBucketName = secrets.nncAttachmentBucketLive;
                                }
                                else
                                {
                                    attachmentBucketName = secrets.nncAttachmentBucketTest;
                                }
                            }

                            Console.WriteLine($"responseFileName {responseFileName}");
                            ProcessAttachments attachmentProcessor = new ProcessAttachments();
                            attachmentProcessor.Process(caseReference, message, client, attachmentBucketName);
                            await StoreAttachmentCountToDynamoAsync(caseReference, attachmentProcessor.numOfAttachments);
                        }
                    }
                }
            }
            catch (AmazonS3Exception error)
            {
                Console.WriteLine("ERROR : Reading Email : '{0}' when reading email", error.Message);
                Console.WriteLine(error.StackTrace);
            }
            catch (Exception error)
            {
                Console.WriteLine("ERROR : An Unknown error encountered : {0}' when reading email", error.Message);
                Console.WriteLine(error.StackTrace);
            }
            return true;
        }

        private async Task<Boolean> CreateCXMCase(Dictionary<String, Object> values, String parsedEmailUnencoded, MimeMessage message, String person, String comments, Boolean bundlerFound)
        {
            try
            {
                HttpClient client = new HttpClient();
                if (!person.Equals(""))
                {
                    values.Add("person", person);
                }
                if (!String.IsNullOrEmpty(comments))
                {
                    values.Add("email-comments", comments);
                }
                if (bundlerFound)
                {
                    values.Add("merge-into-pdf", "yes");
                }
                else
                {
                    values.Add("merge-into-pdf", "no");
                }
                String jsonContent = JsonConvert.SerializeObject(values);
                HttpContent content = new StringContent(jsonContent);
                Console.WriteLine($"Form Data2  : {jsonContent}");
                HttpResponseMessage responseFromJadu = await client.PostAsync(cxmEndPoint + "/api/service-api/" + cxmAPIName + "/" + cxmAPICaseType + "/case/create?key=" + cxmAPIKey, content);
                String responseString = await responseFromJadu.Content.ReadAsStringAsync();
                Console.WriteLine($"Response from Jadu {responseString}");

                if (responseFromJadu.IsSuccessStatusCode)
                {
                    dynamic jsonResponse = JObject.Parse(responseString);
                    caseReference = jsonResponse.reference;
                    Console.WriteLine($"Case Reference >>>{caseReference}<<<");
                }
                else
                {
                    Console.WriteLine($"ERROR - No case created : " + responseFromJadu.StatusCode);
                    return false;
                }
            }
            catch (Exception error)
            {
                Console.WriteLine($"Error Response from Jadu {error.ToString()}");
                return false;
            }
            return true;
        }

        private async Task<MyCouncilCase> CreateMyCouncilCase(String problemType, String problemLat, String problemLng, String problemDescription, String problemLocation, String problemStreet, String problemEmail, String problemText, String problemName)
        {
            MyCouncilCase mycouncilCase = new MyCouncilCase();
            HttpClient client = new HttpClient();

            client.DefaultRequestHeaders.Add("Accept", "application/json");
            //TODO
            //client.DefaultRequestHeaders.Add("dataSource", "fixmystreet");
            client.DefaultRequestHeaders.Add("dataSource", "xamarin");
            client.DefaultRequestHeaders.Add("DeviceID", "FixMyStreet");
            client.DefaultRequestHeaders.Add("ProblemNumber", problemType);
            client.DefaultRequestHeaders.Add("ProblemLatitude", problemLat);
            client.DefaultRequestHeaders.Add("ProblemLongitude", problemLng);
            client.DefaultRequestHeaders.Add("ProblemDescription", JavaScriptEncoder.Default.Encode(problemDescription));
            client.DefaultRequestHeaders.Add("ProblemLocation", JavaScriptEncoder.Default.Encode(problemLocation));
            client.DefaultRequestHeaders.Add("ProblemStreet", JavaScriptEncoder.Default.Encode(problemStreet));
            client.DefaultRequestHeaders.Add("ProblemEmail", problemEmail);
            client.DefaultRequestHeaders.Add("ProblemPhone", problemText);
            client.DefaultRequestHeaders.Add("ProblemName", JavaScriptEncoder.Default.Encode(problemName));
            client.DefaultRequestHeaders.Add("ProblemUsedGPS", "true");
            client.DefaultRequestHeaders.Add("postref", "");
            HttpContent content = null;
            //if (Application.Current.Properties["ProblemUsedImage"].ToString().Equals("true"))
            //{
            //    client.DefaultRequestHeaders.Add("includesImage", "true");
            //    MediaFile imageData = Application.Current.Properties["ProblemImage"] as MediaFile;
            //    content = new StreamContent(imageData.GetStream());
            //    content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            //}
            //else
            //{
                client.DefaultRequestHeaders.Add("includesImage", "false");
            //}

            //TODO Live and secrets 
            client.BaseAddress = new Uri("https://api.northampton.digital/vcc-test/mycouncil");
 
            try
            {
                //HttpResponseMessage response = await client.PostAsync("", content);
                HttpResponseMessage response = await client.PostAsync("", null);
                String jsonResult = await response.Content.ReadAsStringAsync();
                if (jsonResult.Contains("HTTP Status "))
                {
                    int errorIndex = jsonResult.IndexOf("HTTP Status ", StringComparison.Ordinal);
                    Console.WriteLine("ERROR - MyCouncil Case Not Created : " + jsonResult.Substring(errorIndex + 12, 3));
                    return mycouncilCase;
                }
                else
                {
                    JObject crmJSONobject = JObject.Parse(jsonResult);
                    try
                    {
                        if (((String)crmJSONobject.SelectToken("result")).Equals("success"))
                        {
                            //caseReference = ((String)crmJSONobject.SelectToken("callNumber"));
                            mycouncilCase.CaseReference = ((String)crmJSONobject.SelectToken("callNumber"));
                            if (String.IsNullOrEmpty(problemType))
                            {
                                UpdateCaseString("email-comments", "Unexpected FixMyStreet classification of '" + FMSDeserializeCategory(message.HtmlBody) + "' found.", caseReference);
                                await TransitionCaseAsync("with-digital");
                            }
                            if (String.IsNullOrEmpty(problemStreet))
                            {
                                await TransitionCaseAsync("with-digital");
                            }
                            if (problemType.ToLower().Equals("unknown"))
                            {
                                UpdateCaseString("case-update-details", "No mapping of FixMyStreet classification of '" + FMSDeserializeCategory(message.HtmlBody) + "' found.", caseReference);
                                await TransitionCaseAsync("with-digital");
                            }
                            mycouncilCase.Success = true;
                            return mycouncilCase;
                        }
                        else
                        {
                            Console.WriteLine("ERROR : Unexpected response from MyCouncil :" + ((String)crmJSONobject.SelectToken("result")));
                            return mycouncilCase;
                        }
                    }
                    catch (Exception error)
                    {
                        Console.WriteLine("ERROR : Parsing MyCouncil JSON Response :" + error.Message);
                        Console.WriteLine("ERROR : " + error.StackTrace);
                        return mycouncilCase;
                    }
                } 
            }
            catch (Exception error)
            {
                Console.WriteLine("ERROR : Sending case to MyCouncil :" + error.Message);
                Console.WriteLine("ERROR : " + error.StackTrace);
                return mycouncilCase;
            }
        }
        // TODO common signatures 
        private async Task<String> GetCommonSignaturesAsync(String emailContents)
        {

            string tableName = "Thread";
            AmazonDynamoDBClient dynamoDBClient = new AmazonDynamoDBClient(primaryRegion);
            Table ThreadTable = Table.LoadTable(dynamoDBClient, "CommonSignaturesTest");

            ScanFilter scanFilter = new ScanFilter();
            ScanOperationConfig config = new ScanOperationConfig()
            {
                Filter = scanFilter,
                Select = SelectValues.SpecificAttributes,
                AttributesToGet = new List<string> {"signature"}
            };

            Search search = ThreadTable.Scan(config);
            int signatureLocation = emailContents.ToLower().IndexOf("sent from my iphone");
            if (signatureLocation > 0)
            {
                emailContents = emailContents.Remove(signatureLocation);       
            }
            return emailContents;
        }

            private async Task<String> GetSignatureFromDynamoAsync(String domain, String sigSuffix)
        {
            Console.WriteLine("GetSignatureFromDynamoAsync : Checking for known email signature for : " + domain + " " + sigSuffix);
            try
            {
                AmazonDynamoDBClient dynamoDBClient = new AmazonDynamoDBClient(primaryRegion);
                Table table = Table.LoadTable(dynamoDBClient, "EmailSignatures");
                GetItemOperationConfig config = new GetItemOperationConfig
                {
                    AttributesToGet = new List<String> { "signature" + sigSuffix },
                    ConsistentRead = true
                };
                Document document = await table.GetItemAsync(domain, config);
                Console.WriteLine("SUCCESS : GetSignatureFromDynamoAsync : " + document["signature" + sigSuffix].AsPrimitive().Value.ToString());
                return document["signature" + sigSuffix].AsPrimitive().Value.ToString();
            }
            catch (Exception error)
            {
                Console.WriteLine("ERROR : GetSignatureFromDynamoAsync : " + error.Message);
                Console.WriteLine(error.StackTrace);
                return "";
            }

        }

        private async Task<String> GetStringFieldFromDynamoAsync(String key, String field, String tableName)
        {
            try
            {
                AmazonDynamoDBClient dynamoDBClient = new AmazonDynamoDBClient(primaryRegion);
                Table table = Table.LoadTable(dynamoDBClient, tableName);
                GetItemOperationConfig config = new GetItemOperationConfig
                {
                    AttributesToGet = new List<String> { field },
                    ConsistentRead = true
                };
                Document document = await table.GetItemAsync(key, config);
                return document[field].AsPrimitive().Value.ToString();
            }
            catch (Exception error)
            {
                Console.WriteLine("ERROR : GetStringFieldFromDynamoAsync : " + error.Message);
                Console.WriteLine(error.StackTrace);
                return "";
            }
        }

        private async Task<String> GetUSRN(String lat, String lng)
        {
            HttpClient client = new HttpClient();
            //TODO Live and secrets 
            client.BaseAddress = new Uri("https://api.northampton.digital/vcc/getstreetbylatlng");

            try
            {
                //HttpResponseMessage response = await client.PostAsync("", content);
                HttpResponseMessage response = await client.GetAsync("?lat=" + lat + "&lng=" + lng);
                String jsonResult = await response.Content.ReadAsStringAsync();

                 Console.WriteLine($"Response from API {jsonResult}");

                if (response.IsSuccessStatusCode)
                {
                    dynamic jsonResponse = JObject.Parse(jsonResult);
                    String temp = jsonResponse.results[0][0];
                    return temp;
                }
                else
                {
                    Console.WriteLine($"ERROR - GetUSRN : Error Code from API : " + response.StatusCode);
                    throw new ApplicationException();
                }
            }
            catch (Exception error)
            {
                Console.WriteLine("ERROR : GetUSRN :" + error.Message);
                Console.WriteLine("ERROR : " + error.StackTrace);
                throw new ApplicationException();
            }
        }

        private async Task<Boolean> StoreContactToDynamoAsync(String caseReference, String contact, Boolean unitary)
        {
            String unitaryString = "false";

            if (unitary)
            {
                unitaryString = "true";
            }

            try
            {
                AmazonDynamoDBClient dynamoDBClient = new AmazonDynamoDBClient(primaryRegion);
                PutItemRequest dynamoRequest = new PutItemRequest
                {
                    TableName = tableName,
                    Item = new Dictionary<String, AttributeValue>
                        {
                              { "CaseReference", new AttributeValue { S = caseReference }},
                              { "InitialContact", new AttributeValue { S = contact }},
                              { "Unitary", new AttributeValue { S = unitaryString }}
                        }
                };
                await dynamoDBClient.PutItemAsync(dynamoRequest);
                return true;
            }
            catch (Exception error)
            {
                Console.WriteLine("ERROR : StoreContactToDynamoDB :" + error.Message);
                Console.WriteLine(error.StackTrace);
                return false;
            }
        }

        private async Task<Boolean> StoreAttachmentCountToDynamoAsync(String caseReference, int numOfAttachments)
        {
            try
            {
                AmazonDynamoDBClient dynamoDBClient = new AmazonDynamoDBClient(primaryRegion);
                UpdateItemRequest dynamoRequest = new UpdateItemRequest
                {
                    TableName = tableName,
                    Key = new Dictionary<string, AttributeValue>() { { "CaseReference", new AttributeValue { S = caseReference } } },
                    ExpressionAttributeNames = new Dictionary<string, string>()
                    {
                        {"#A", "AttachmentCount"},
                    },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>()
                    {
                        {":numofdocs",new AttributeValue {N = numOfAttachments.ToString() }},
                    },

                    UpdateExpression = "ADD #A :numofdocs "
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

        private String getBodyFromBase64(MimeMessage message)
        {
            String content = "";
            try
            {
                foreach (var bodyPart in message.BodyParts)
                {
                    MemoryStream memory = new MemoryStream();
                    MimePart emailBody = (MimePart)bodyPart;
                    emailBody.Content.DecodeTo(memory);
                    byte[] memoryArray = memory.ToArray();
                    String emailBodyString = Encoding.Default.GetString(memoryArray);
                    HtmlDocument emailHTML = new HtmlDocument();
                    emailHTML.LoadHtml(emailBodyString);
                    return emailHTML.DocumentNode.InnerText;
                }
            }
            catch (Exception error)
            {
                Console.WriteLine(EmailFrom + " - ERROR Email Contents from Base64: " + error.ToString());
            }

            return content;
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
                Console.WriteLine("ERROR : GetSecretValue : " + error.Message);
                Console.WriteLine("ERROR : GetSecretValue : " + error.StackTrace);
                return false;
            }
        }

        private Boolean UpdateCaseString(String fieldName, String fieldValue, String caseReference)
        {
            String data = "{\"" + fieldName + "\":\"" + fieldValue + "\"" +
                "}";

            if (UpdateCase(data, caseReference))
            {
                return true;
            }
            else
            {
                Console.WriteLine(caseReference + " : Error updating CXM field " + fieldName + " with message : " + fieldValue);
                return false;
            }
        }

        private Boolean UpdateCase(String data, String caseReference)
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

        private async Task<Boolean> TransitionCaseAsync(String transitionTo)
        {
            Console.WriteLine(caseReference + " : transitioning to : " + transitionTo);
            Boolean success = false;
            HttpClient cxmClient = new HttpClient();
            cxmClient.BaseAddress = new Uri(cxmEndPoint);
            String requestParameters = "key=" + cxmAPIKey;
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/api/service-api/" + cxmAPIName + "/case/" + caseReference + "/transition/" + transitionTo + "?" + requestParameters);
            HttpResponseMessage response = cxmClient.SendAsync(request).Result;
            if (response.IsSuccessStatusCode)
            {
                success = true;
                Console.WriteLine(caseReference + " : transitioned to : " + transitionTo);
            }
            else
            {
                Console.WriteLine("ERROR CXM Failed to transiton : " + caseReference + " to " + transitionTo);
            }
            return success;
        }

        private async Task<String> TrimEmailContents(String emailContents)
        {
            return emailContents;
        }

        private async Task<Boolean> IsAutoResponse(String emailContents)
        {
            List<String> autoResponseText = new List<String>();
            Dictionary<string, AttributeValue> lastKeyEvaluated = null;
            try
            {
                AmazonDynamoDBClient dynamoDBClient = new AmazonDynamoDBClient(primaryRegion);
                do
                {
                    ScanRequest request = new ScanRequest
                    {
                        TableName = AutoResponseTable,
                        Limit = 10,
                        ExclusiveStartKey = lastKeyEvaluated,
                        ProjectionExpression = "AutoResponseText"
                    };

                    ScanResponse response = await dynamoDBClient.ScanAsync(request);
                    foreach (Dictionary<string, AttributeValue> item
                         in response.Items)
                    {
                        item.TryGetValue("AutoResponseText", out AttributeValue value);
                        autoResponseText.Add(value.S.ToLower());
                    }
                    lastKeyEvaluated = response.LastEvaluatedKey;
                } while (lastKeyEvaluated != null && lastKeyEvaluated.Count != 0);
                foreach (String text in autoResponseText)
                {
                    if (emailContents.ToLower().Contains(text))
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

        private String FMSDeserializeFirstName(String Email)
        {
            try
            {
                int fieldStarts = Email.IndexOf("Name") + 4;
                int fieldEnds = Email.IndexOf("Email", fieldStarts);
                String name = Email.Substring(fieldStarts, fieldEnds - fieldStarts).TrimEnd('\r', '\n');
                List<String> names = name.Split(' ').ToList();
                return names.First();
            }
            catch
            {
                Console.WriteLine("WARNING : Unable to deserialize Customer First Name");
                return "";
            }
        }

        private String FMSDeserializeLastName(String Email)
        {
            try
            {
                int fieldStarts = Email.IndexOf("Name") + 4;
                int fieldEnds = Email.IndexOf("Email", fieldStarts);
                String name = Email.Substring(fieldStarts, fieldEnds - fieldStarts).TrimEnd('\r', '\n');
                List<String> names = name.Split(' ').ToList();
                names.RemoveAt(0);
                return String.Join(" ", names.ToArray());
            }
            catch
            {
                Console.WriteLine("WARNING : Unable to deserialize Customer Last Name");
                return "";
            }
        }

        private String FMSDeserializeIssueTitle(String Email)
        {
            try
            {
                int fieldStarts = Email.IndexOf("the user who reported the problem.") + 34;
                int fieldEnds = Email.IndexOf("Category:", fieldStarts);
                return Email.Substring(fieldStarts, fieldEnds - fieldStarts).TrimEnd('\r', '\n');
            }
            catch
            {
                Console.WriteLine("WARNING : Unable to deserialize Customer Issue Title");
                return "";
            }
        }

        private String FMSDeserializeEmail(String Email)
        {
            try
            {
                int fieldStarts = Email.IndexOf("Email") + 5;
                int fieldEnds = Email.IndexOf("Phone", fieldStarts);
                return Email.Substring(fieldStarts, fieldEnds - fieldStarts).TrimEnd('\r', '\n');
            }
            catch
            {
                Console.WriteLine("WARNING : Unable to deserialize Customer Email");
                return "";
            }
        }

        private String FMSDeserializePhone(String Email)
        {
            try
            {
                int fieldStarts = Email.IndexOf("Phone") + 5;
                if (fieldStarts == 4)
                {
                    return "";
                }
                int fieldEnds = Email.IndexOf("Replies to this", fieldStarts);
                return Email.Substring(fieldStarts, fieldEnds - fieldStarts).TrimEnd('\r', '\n');
            }
            catch
            {
                Console.WriteLine("WARNING : Unable to deserialize FMS Phone");
                return "";
            }
        }

        private String FMSDeserializeNorthing(String Email)
        {
            try
            {
                int fieldStarts = Email.IndexOf("Easting/Northing: ") + 25;
                return Email.Substring(fieldStarts, 6).TrimEnd('\r', '\n');
            }
            catch
            {
                Console.WriteLine("WARNING : Unable to deserialize FMS Northing");
                return "";
            }
        }

        private String FMSDeserializeEasting(String Email)
        {
            try
            {
                int fieldStarts = Email.IndexOf("Easting/Northing: ") + 18;
                return Email.Substring(fieldStarts, 6).TrimEnd('\r', '\n');
            }
            catch
            {
                Console.WriteLine("WARNING : Unable to deserialize FMS Easting");
                return "";
            }
        }

        private String FMSDeserializeLat(String Email)
        {
            try
            {
                int approxStart = Email.IndexOf("Easting/Northing");
                int fieldStarts = Email.IndexOf(" (", approxStart) + 2;
                int fieldEnds = Email.IndexOf(", ", fieldStarts);
                return Email.Substring(fieldStarts, fieldEnds - fieldStarts).TrimEnd('\r', '\n');
            }
            catch
            {
                Console.WriteLine("WARNING : Unable to deserialize FMS Latitude");
                return "";
            }
        }

        private String FMSDeserializeLng(String Email)
        {
            try
            {
                int approxStart = Email.IndexOf("Easting/Northing");
                int fieldStarts = Email.IndexOf(", ", approxStart) + 2;
                int fieldEnds = Email.IndexOf(")", fieldStarts);
                return Email.Substring(fieldStarts, fieldEnds - fieldStarts).TrimEnd('\r', '\n');
            }
            catch
            {
                Console.WriteLine("WARNING : Unable to deserialize FMS Longtitude");
                return "";
            }
        }

        private String FMSDeserializeStreet(String Email)
        {
            try
            {
                int approxStart = Email.IndexOf("Nearest road to the pin placed on the map");
                int fieldStarts = Email.IndexOf(": ", approxStart) + 2;
                int fieldEnds = Email.IndexOf("Nearest postcode", fieldStarts);
                return Email.Substring(fieldStarts, fieldEnds - fieldStarts).TrimEnd('\r', '\n');
            }
            catch
            {
                Console.WriteLine("WARNING : Unable to deserialize FMS Street");
                return "";
            }
        }

        private String FMSDeserializePostcode(String Email)
        {
            try
            {
                int approxStart = Email.IndexOf("Nearest postcode to the pin placed on the map");
                int fieldStarts = Email.IndexOf(": ", approxStart) + 2;
                int fieldEnds = Email.IndexOf("(", fieldStarts);
                return Email.Substring(fieldStarts, fieldEnds - fieldStarts).TrimEnd('\r', '\n');
            }
            catch
            {
                Console.WriteLine("WARNING : Unable to deserialize FMS Postcode");
                return "";
            }
        }

        private String FMSDeserializeCategory(String Email)
        {
            try
            {
                int fieldStarts = Email.IndexOf("<strong>Category:</strong> ") + 27;
                int fieldEnds = Email.IndexOf("</p>", fieldStarts);
                return Email.Substring(fieldStarts, fieldEnds - fieldStarts).TrimEnd('\r', '\n');
            }
            catch
            {
                Console.WriteLine("WARNING : Unable to deserialize FMS Category");
                return "";
            }
        }

        private String FMSDeserializeDetails(String Email)
        {
            try
            {
                int fieldStarts = Email.IndexOf("font-size: 14px; line-height: 20px; margin: 0 0 0.8em 0;") + 80;
                fieldStarts = Email.IndexOf("font-size: 14px; line-height: 20px; margin: 0 0 0.8em 0;",fieldStarts) + 58;
                int fieldEnds = Email.IndexOf("</p>", fieldStarts);
                return Email.Substring(fieldStarts, fieldEnds - fieldStarts).TrimEnd('\r', '\n');
            }
            catch
            {
                Console.WriteLine("WARNING : Unable to deserialize FMS Category");
                return "";
            }
        }
    }

    public class MyCouncilCase
    {
        public Boolean Success { get; set; }
        public String CaseReference { get; set; }

        public MyCouncilCase()
        {
            Success = false;
            CaseReference = "";
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
        public String AutoResponseTableLive { get; set; }
        public String AutoResponseTableTest { get; set; }
        public String MyCouncilTestEndPoint { get; set; }
        public String MyCouncilLiveEndPoint { get; set; }
    }
}