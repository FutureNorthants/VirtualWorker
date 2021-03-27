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
        private String emailFrom { get; set; } = null;
        private String emailTo { get; set; } = null;
        private String subject { get; set; } = null;
        public String firstName { get; set; } = null;
        public String lastName { get; set; } = null;
        public String emailBody { get; set; } = null;
        public String caseReference { get; set; } = null;
        public String emailContents { get; set; } = null;

        public Boolean create = true;
        public Boolean unitary = false;
        public Boolean contactUs = false;

        private Boolean west = true;

        public ProcessMessage()
        {
            client = new AmazonS3Client(bucketRegion);
        }

        public Boolean Process(String bucketName, String keyName, Boolean liveInstance)
        {
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
                    MimeMessage message = MimeMessage.Load(response.ResponseStream);
                    MailAddressCollection mailFromAddresses = (MailAddressCollection)message.From;
                    MailAddressCollection mailToAddresses = (MailAddressCollection)message.To;

                    try
                    {
                        emailTo = mailToAddresses[0].Address.ToString().ToLower();
                        emailFrom = mailFromAddresses[0].Address.ToString().ToLower();
                        Console.WriteLine(emailFrom + " - Processing email sent to this address : " + emailTo);
                        if (emailTo.Contains("update"))
                        {
                            Console.WriteLine(emailFrom + " - Update Case");
                            create = false;
                        }
                        else
                        {
                            Console.WriteLine(emailFrom + " - Create Case");
                        }
                        if (emailTo.ToLower().Contains("unitary") || emailTo.ToLower().Contains("westnorthants") || emailTo.ToLower().Contains("northnorthants") || emailFrom.ToLower().Contains("noreply@northamptonshire.gov.uk"))
                        {
                            unitary = true;
                        }
                        if (emailTo.ToLower().Contains("northnorthants"))
                        {
                            west = false;
                        }
                        if (emailFrom.ToLower().Contains("noreply@northamptonshire.gov.uk")&&message.Subject.ToLower().Contains("northamptonshire council form has been submitted"))
                        {
                            contactUs = true;
                        }
                    }
                    catch (Exception)
                    {
                    }
                    subject = message.Subject;

                    if (String.IsNullOrWhiteSpace(subject))
                    {
                        subject = " ";
                    }
                    List<String> names = message.From[0].Name.Split(' ').ToList();
                    firstName = names.First();
                    names.RemoveAt(0);
                    lastName = String.Join(" ", names.ToArray());
                    emailBody = message.HtmlBody;
                    Console.WriteLine(emailFrom + " - Email Contents : " + message.TextBody);
                    if (String.IsNullOrEmpty(message.TextBody))
                    {
                        if (String.IsNullOrEmpty(message.HtmlBody))
                        {
                            emailContents = getBodyFromBase64(message);
                        }
                        else
                        {
                            HtmlDocument emailHTML = new HtmlDocument();
                            emailHTML.LoadHtml(message.HtmlBody);
                            emailContents =  emailHTML.DocumentNode.InnerText;
                        }                     
                        try
                        {
                            int emailAddressStarts = emailContents.ToLower().IndexOf("email address:") + 15;
                            int emailAddressEnds = emailContents.ToLower().IndexOf("telephone number:") - 2;
                            emailFrom = emailContents.Substring(emailAddressStarts, emailAddressEnds-emailAddressStarts);
                        }
                        catch { }
                    }
                    else
                    {
                        emailContents = message.TextBody;
                    } 

                    String person = "";
                    Boolean bundlerFound = false;
                    String responseFileName = "";
                    String parsedEmailEncoded = "";
                    String parsedEmailUnencoded = "";

                    String emailFromName = "";

                    if (await GetSecrets())
                    {
                        if (liveInstance)
                        {
                            if (west)
                            {
                                cxmEndPoint = secrets.cxmEndPointLive;
                                cxmAPIKey = secrets.cxmAPIKeyLive;
                                cxmAPIName = secrets.cxmAPINameWest;
                                cxmAPICaseType = secrets.cxmAPICaseTypeWest;
                                tableName = secrets.wncEMACasesLive;
                            }
                            else
                            {
                                cxmEndPoint = secrets.cxmEndPointLiveNorth;
                                cxmAPIKey = secrets.cxmAPIKeyLiveNorth;
                                cxmAPIName = secrets.cxmAPINameNorth;
                                cxmAPICaseType = secrets.cxmAPICaseTypeNorth;
                                tableName = secrets.nncEMNCasesLive;
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
                                tableName = secrets.wncEMACasesTest;
                            }
                            else
                            {
                                cxmEndPoint = secrets.cxmEndPointTestNorth;
                                cxmAPIKey = secrets.cxmAPIKeyTestNorth;
                                cxmAPIName = secrets.cxmAPINameNorth;
                                cxmAPICaseType = secrets.cxmAPICaseTypeNorth;
                                tableName = secrets.nncEMNCasesTest;
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
                        if (mailToAddresses[0].Address.ToLower().Contains("document"))
                        {
                            bundlerFound = true;
                        }
                        SigParser.Client sigParserClient = new SigParser.Client(secrets.sigParseKey);
                        String corporateSignature = await GetSignatureFromDynamoAsync(secrets.homeDomain);
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
                            corporateSignature = await GetSignatureFromDynamoAsync(mailFromAddresses[currentAddress].Address.Substring(domainLocation));
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

                        if (contactUs)
                        {
                            parsedEmailUnencoded = emailContents;
                            parsedEmailEncoded = HttpUtility.UrlEncode(emailContents);
                        }
                        else 
                        { 
                            SigParser.EmailParseRequest sigParserRequest = new SigParser.EmailParseRequest { plainbody = emailContents, from_name = firstName + " " + lastName, from_address = emailFrom };
                            parsedEmailUnencoded = sigParserClient.Parse(sigParserRequest).cleanedemailbody_plain;
                            //var temp = sigParserClient.Parse(sigParserRequest);
                            if ((parsedEmailUnencoded == null || parsedEmailUnencoded.Contains("___")) && !bundlerFound)
                            {
                                Console.WriteLine($"No message found, checking for forwarded message");
                                parsedEmailUnencoded = sigParserClient.Parse(sigParserRequest).emails[1].cleanedBodyPlain;
                                emailFrom = sigParserClient.Parse(sigParserRequest).emails[1].from_EmailAddress;
                                names = sigParserClient.Parse(sigParserRequest).emails[1].from_Name.Split(' ').ToList();
                                firstName = names.First();
                                names.RemoveAt(0);
                                lastName = String.Join(" ", names.ToArray());
                            }
                            Console.WriteLine($"Cleaned email body is : {parsedEmailUnencoded}");
                            parsedEmailEncoded = HttpUtility.UrlEncode(parsedEmailUnencoded);
                            Console.WriteLine($"Encoded email body is : {parsedEmailEncoded}");
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
                            cxmEndPoint + "/api/service-api/norbert/user/" + emailFrom + "?key=" + cxmAPIKey);
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

                    if (emailFrom.Contains(secrets.loopPreventIdentifier))
                    {
                        Console.WriteLine(emailFrom + " - Loop identifier found - no case created or updated : " + secrets.loopPreventIdentifier);
                    }
                    else
                    {
                        if (subject.Contains("EMA") || parsedEmailUnencoded.Contains("EMA") || subject.Contains("EMN") || parsedEmailUnencoded.Contains("EMN"))
                        {
                            HttpClient client = new HttpClient();
                            String caseNumber = "";
                            if (west)
                            {
                                if (subject.Contains("EMA"))
                                {
                                    int refLocation = subject.IndexOf("EMA");
                                    caseNumber = subject.Substring(refLocation, 9);
                                }
                                else
                                {
                                    int refLocation = parsedEmailUnencoded.IndexOf("EMA");
                                    caseNumber = parsedEmailUnencoded.Substring(refLocation, 9);
                                }
                            }
                            else
                            {
                                if (subject.Contains("EMN"))
                                {
                                    int refLocation = subject.IndexOf("EMN");
                                    caseNumber = subject.Substring(refLocation, 9);
                                }
                                else
                                {
                                    int refLocation = parsedEmailUnencoded.IndexOf("EMN");
                                    caseNumber = parsedEmailUnencoded.Substring(refLocation, 9);
                                }
                            }

                            caseReference = caseNumber;

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

                            String unitary = await GetStringFieldFromDynamoAsync(caseReference, "Unitary");

                            if (unitary.Equals("true"))
                            {

                                await TransitionCaseAsync("awaiting-location-confirmation");
                            }
                            else
                            {
                                await TransitionCaseAsync("awaiting-review");
                            }
                        }
                        else
                        {
                            if (create)
                            {
                                try
                                {
                                    HttpClient client = new HttpClient();
                                    Dictionary<String, Object> values;
                                    values = new Dictionary<String, Object>
                                    {
                                            { "first-name", firstName },
                                            { "surname", lastName },
                                            { "email", emailFrom },
                                            { "subject", subject },
                                            { "enquiry-details", parsedEmailUnencoded },
                                            { "customer-has-updated", false },
                                            { "unitary", unitary },
                                            { "original-email", await TrimEmailContents(message.TextBody) }
                                    };
                                    if (!person.Equals(""))
                                    {
                                        values.Add("person", person);
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

                                    dynamic jsonResponse = JObject.Parse(responseString);

                                    caseReference = jsonResponse.reference;

                                    Console.WriteLine($"Case Reference >>>{caseReference}<<<");
                                }
                                catch (Exception error)
                                {
                                    Console.WriteLine($"Error Response from Jadu {error.ToString()}");
                                }

                                responseFileName = "email-no-faq.txt";

                                await StoreContactToDynamoAsync(caseReference, parsedEmailUnencoded, unitary);

                                if (bundlerFound)
                                {
                                    await TransitionCaseAsync("awaiting-bundling");
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

        private async Task<String> GetSignatureFromDynamoAsync(String domain)
        {
            Console.WriteLine("Checking for known email signature for : " + domain);
            try
            {
                AmazonDynamoDBClient dynamoDBClient = new AmazonDynamoDBClient(primaryRegion);
                Table table = Table.LoadTable(dynamoDBClient, "EmailSignatures");
                GetItemOperationConfig config = new GetItemOperationConfig
                {
                    AttributesToGet = new List<String> { "signature" },
                    ConsistentRead = true
                };
                Document document = await table.GetItemAsync(domain, config);
                Console.WriteLine("SUCCESS : GetSignatureFromDynamoAsync : " + document["signature"].AsPrimitive().Value.ToString());
                return document["signature"].AsPrimitive().Value.ToString();
            }
            catch (Exception error)
            {
                Console.WriteLine("ERROR : GetSignatureFromDynamoAsync : " + error.Message);
                Console.WriteLine(error.StackTrace);
                return "";
            }

        }

        private async Task<String> GetStringFieldFromDynamoAsync(String caseReference, String field)
        {
            try
            {
                AmazonDynamoDBClient dynamoDBClient = new AmazonDynamoDBClient(primaryRegion);
                Table productCatalog = Table.LoadTable(dynamoDBClient, tableName);
                GetItemOperationConfig config = new GetItemOperationConfig
                {
                    AttributesToGet = new List<String> { field },
                    ConsistentRead = true
                };
                Document document = await productCatalog.GetItemAsync(caseReference, config);
                return document[field].AsPrimitive().Value.ToString();
            }
            catch (Exception error)
            {
                Console.WriteLine("ERROR : GetContactFromDynamoAsync : " + error.Message);
                Console.WriteLine(error.StackTrace);
                return "";
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
                Console.WriteLine(emailFrom + " - ERROR Email Contents from Base64: " + error.ToString());
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
    }
}