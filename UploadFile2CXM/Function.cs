using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Amazon;
using Newtonsoft.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace UploadFile2CXM
{
    public class Function
    {
        private static readonly RegionEndpoint primaryRegion = RegionEndpoint.EUWest2;
        private static readonly String secretName = "nbcGlobal";
        private static readonly String secretAlias = "AWSCURRENT";
        private Secrets secrets = null;

        public async Task FunctionHandler(S3Event evnt, ILambdaContext context)
        {
            if (await GetSecrets())
            {
                S3EventNotification.S3Entity s3Event = evnt.Records?[0].S3;
                if (s3Event != null)
                {
                    try
                    {
                        s3Event.Object.Key = HttpUtility.UrlDecode(s3Event.Object.Key); ;
                        context.Logger.LogLine("Processing " + s3Event.Object.Key + " from " + s3Event.Bucket.Name);
                        String cxmDomain = null;
                        String cxmServiceAPI = null;
                        String cxmServiceAPIKey = null;
                        String sqsURL = null;

                        switch (s3Event.Bucket.Name)
                        {
                            case "reportit.test":
                            case "nbc-email-attachments.test":
                                cxmDomain = secrets.cxmEndPointTest;
                                cxmServiceAPI = secrets.cxmAPINameWest;
                                cxmServiceAPIKey = secrets.cxmAPIKeyTest;
                                sqsURL = secrets.sqsPostAttachmentUploadTest;
                                break;
                            case "reportit.live":
                            case "nbc-email-attachments.live":
                                cxmDomain = secrets.cxmEndPointLive;
                                cxmServiceAPI = secrets.cxmAPINameWest;
                                cxmServiceAPIKey = secrets.cxmAPIKeyLive;
                                sqsURL = secrets.sqsPostAttachmentUploadLive;
                                break;
                            case "nnc-email-attachments.test":
                            case "nnc.incoming.attachments.test":
                                cxmDomain = secrets.cxmEndPointTestNorth;
                                cxmServiceAPI = secrets.cxmAPINameNorth;
                                cxmServiceAPIKey = secrets.cxmAPIKeyTestNorth;
                                sqsURL = secrets.sqsPostAttachmentUploadTest;
                                break;
                            case "nnc-email-attachments.live":
                            case "nnc.incoming.attachments.live":
                                cxmDomain = secrets.cxmEndPointLiveNorth;
                                cxmServiceAPI = secrets.cxmAPINameNorth;
                                cxmServiceAPIKey = secrets.cxmAPIKeyLiveNorth;
                                sqsURL = secrets.sqsPostAttachmentUploadLive;
                                break;
                            default:
                                context.Logger.LogLine("ERROR - Unexpected bucket name : " + s3Event.Bucket.Name);
                                break;
                        }
                        byte[] imageBytes = new byte[s3Event.Object.Size];

                        AmazonS3Client s3 = new AmazonS3Client();
                        GetObjectResponse image = await s3.GetObjectAsync(s3Event.Bucket.Name, s3Event.Object.Key);
                        using (MemoryStream ms = new MemoryStream())
                        {
                            int read;
                            byte[] buffer = new byte[16 * 1024];
                            while ((read = image.ResponseStream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                ms.Write(buffer, 0, read);
                            }
                            imageBytes = ms.ToArray();
                        }

                        HttpClient httpClient = new HttpClient();
                        MultipartFormDataContent form = new MultipartFormDataContent();
                        if (s3Event.Object.Key.Contains("-map.jpg"))
                        {
                            form.Add(new StringContent("{\"type\":\"photo\",\"name\":\" \",\"description\":\"Location of the reported issue\"}"), "json");
                            form.Add(new ByteArrayContent(imageBytes, 0, imageBytes.Length), "file", "map.jpg");
                        }
                        else if (s3Event.Object.Key.Contains("-image.jpg"))
                        {
                            form.Add(new StringContent("{\"type\":\"photo\",\"name\":\" \",\"description\":\"Photo of the reported issue\"}"), "json");
                            form.Add(new ByteArrayContent(imageBytes, 0, imageBytes.Length), "file", "photo.jpg");
                        }
                        else if (s3Event.Object.Key.StartsWith("EMA") || s3Event.Object.Key.StartsWith("SDU") || s3Event.Object.Key.StartsWith("EMN"))
                        {
                            form.Add(new StringContent("{\"type\":\"photo\",\"name\":\" \",\"description\":\"Attachment\"}"), "json");
                            form.Add(new ByteArrayContent(imageBytes, 0, imageBytes.Length), "file", s3Event.Object.Key);
                        }
                        else
                        {
                            context.Logger.LogLine("ERROR - Unexpected image type found : " + s3Event.Object.Key);
                        }

                        HttpResponseMessage response = await httpClient.PostAsync(cxmDomain + "/api/service-api/" + cxmServiceAPI + "/case/" + s3Event.Object.Key.Substring(0, 9) + "/attach?key=" + cxmServiceAPIKey, form);
                        response.EnsureSuccessStatusCode();
                        httpClient.Dispose();

                        AmazonSQSClient amazonSQSClient = new AmazonSQSClient();
                        SendMessageRequest sendMessageRequest = new SendMessageRequest();
                        sendMessageRequest.QueueUrl = sqsURL;
                        sendMessageRequest.MessageBody = "Processing " + s3Event.Object.Key + " from " + s3Event.Bucket.Name + " has been uploaded to CXM.";
                        Dictionary<string, MessageAttributeValue> MessageAttributes = new Dictionary<string, MessageAttributeValue>();
                        MessageAttributeValue messageTypeAttribute1 = new MessageAttributeValue();
                        messageTypeAttribute1.DataType = "String";
                        messageTypeAttribute1.StringValue = s3Event.Bucket.Name;
                        MessageAttributes.Add("bucket", messageTypeAttribute1);
                        MessageAttributeValue messageTypeAttribute2 = new MessageAttributeValue();
                        messageTypeAttribute2.DataType = "String";
                        messageTypeAttribute2.StringValue = s3Event.Object.Key;
                        MessageAttributes.Add("file", messageTypeAttribute2);
                        sendMessageRequest.MessageAttributes = MessageAttributes;

                        SendMessageResponse sendMessageResponse = await amazonSQSClient.SendMessageAsync(sendMessageRequest);
                    }
                    catch (Exception e)
                    {
                        context.Logger.LogLine("ERROR Processing " + s3Event.Object.Key + " from " + s3Event.Bucket.Name);
                        context.Logger.LogLine(e.Message);
                        context.Logger.LogLine(e.StackTrace);
                        throw;
                    }
                }
            }
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
    }

    public class Secrets
    {
        public String cxmEndPointTest { get; set; }
        public String cxmEndPointLive { get; set; }
        public String cxmEndPointTestNorth { get; set; }
        public String cxmEndPointLiveNorth { get; set; }
        public String cxmAPINameWest { get; set; }
        public String cxmAPINameNorth { get; set; }
        public String cxmAPIKeyTest { get; set; }
        public String cxmAPIKeyLive { get; set; }
        public String cxmAPIKeyTestNorth { get; set; }
        public String cxmAPIKeyLiveNorth { get; set; }
        public String sqsPostAttachmentUploadLive { get; set; }
        public String sqsPostAttachmentUploadTest { get; set; }
    }
}
