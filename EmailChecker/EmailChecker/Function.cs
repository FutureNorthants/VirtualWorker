using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon;
using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using EmailChecker.Helpers;
using Newtonsoft.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace EmailChecker
{
    public class Function
    {
        IAmazonS3 S3Client { get; set; }
        private static readonly RegionEndpoint primaryRegion = RegionEndpoint.EUWest2;
        private static RegionEndpoint sqsRegion;
        private static RegionEndpoint s3EmailsRegion;
        private static RegionEndpoint s3TemplatesRegion;
        private static readonly String secretName = "nbcGlobal";
        private static readonly String secretAlias = "AWSCURRENT";
        private Secrets secrets = null;
        private static String pendingimagesbucket;
        private static String quarantinedimagesbucket;
        private static String templatesbucket;
        private static String sqsEmailURL;

        public Function()
        {
            S3Client = new AmazonS3Client();
        }

        public Function(IAmazonS3 s3Client)
        {
            this.S3Client = s3Client;
        }

        public async Task<string> FunctionHandler(S3Event evnt, ILambdaContext context)
        {
            var s3Event = evnt.Records?[0].S3;
            if (s3Event == null)
            {
                return null;
            }


            if (await GetSecrets())
            {
                
                if (s3Event.Bucket.Name.Contains("nnc"))
                {
                    sqsRegion = RegionEndpoint.EUWest2;
                    s3EmailsRegion = RegionEndpoint.EUWest2;
                    s3TemplatesRegion = RegionEndpoint.EUWest2;
                    pendingimagesbucket = secrets.nncpendingimagesbucket;
                    quarantinedimagesbucket = secrets.nncquarantinedimagesbucket;
                    templatesbucket = secrets.nncTemplateBucketTest;
                }
                else
                {
                    sqsRegion = RegionEndpoint.EUWest1;
                    s3EmailsRegion = RegionEndpoint.EUWest1;
                    s3TemplatesRegion = RegionEndpoint.EUWest2;
                    pendingimagesbucket = secrets.wncpendingimagesbucket;
                    quarantinedimagesbucket = secrets.wncquarantinedimagesbucket;
                    templatesbucket = secrets.templateBucketTest;
                }
                try
                {
                    GetObjectMetadataResponse response = await this.S3Client.GetObjectMetadataAsync(s3Event.Bucket.Name, s3Event.Object.Key);

                    AmazonSQSClient amazonSQSClient = new AmazonSQSClient(sqsRegion);
                    
                    ProcessEmail emailProcessor = new ProcessEmail();
                    Console.WriteLine("Image Moderation Confidence Parameter = " + secrets.imageModerationConfidence);
                    long imageModerationLevel = long.Parse(secrets.imageModerationConfidence);
                    String sqsFAQURL = secrets.sqsFAQURLbeta;
                    sqsEmailURL = secrets.sqsEmailURLTest;
                    try
                    {
                        if (context.InvokedFunctionArn.ToLower().Contains("prod"))
                        {
                            Console.WriteLine("Prod version");
                            sqsFAQURL = secrets.sqsFAQURLprod;
                            sqsEmailURL = secrets.sqsEmailURLLive;
                            if (s3Event.Bucket.Name.Contains("nnc"))
                            {
                                templatesbucket = secrets.nncTemplateBucketLive;
                            }
                            else
                            {
                                 templatesbucket = secrets.templateBucketLive;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Beta version");
                    }
                    if (emailProcessor.Process(s3Event.Bucket.Name, s3Event.Object.Key, imageModerationLevel,pendingimagesbucket,quarantinedimagesbucket,templatesbucket,s3EmailsRegion,s3TemplatesRegion))
                    {                    
                        SendMessageRequest sendMessageRequest = new SendMessageRequest
                        {
                            QueueUrl = sqsFAQURL
                        };
                        sendMessageRequest.MessageBody = "Email has passed validation. Waiting to be checked against FAQ database";
                        Dictionary<string, MessageAttributeValue> MessageAttributes = new Dictionary<string, MessageAttributeValue>();
                        MessageAttributeValue messageTypeAttribute1 = new MessageAttributeValue
                        {
                            DataType = "String",
                            StringValue = "email"
                        };
                        MessageAttributes.Add("Type", messageTypeAttribute1);
                        MessageAttributeValue messageTypeAttribute2 = new MessageAttributeValue
                        {
                            DataType = "String",
                            StringValue = s3Event.Bucket.Name
                        };
                        MessageAttributes.Add("Bucket", messageTypeAttribute2);
                        MessageAttributeValue messageTypeAttribute3 = new MessageAttributeValue
                        {
                            DataType = "String",
                            StringValue = s3Event.Object.Key
                        };
                        MessageAttributes.Add("Object", messageTypeAttribute3);
                        String unitaryCouncil = "";
                        if (emailProcessor.west)
                        {
                            unitaryCouncil = "west";
                        }
                        else
                        {
                            unitaryCouncil = "north";
                        }
                        MessageAttributeValue messageTypeAttribute4 = new MessageAttributeValue
                        {
                            DataType = "String",
                            StringValue = unitaryCouncil
                        };
                        MessageAttributes.Add("UnitaryCouncil", messageTypeAttribute4);
                        sendMessageRequest.MessageAttributes = MessageAttributes;
                        SendMessageResponse sendMessageResponse = await amazonSQSClient.SendMessageAsync(sendMessageRequest);
                        return "{\"Message\":\"Email passed checks\",\"lambdaResult\":\"Success\"}";
                    }
                    else
                    {
                        try
                        {
                            SendMessageRequest sendMessageRequest = new SendMessageRequest
                            {
                                QueueUrl = sqsEmailURL
                            };
                            sendMessageRequest.MessageBody = emailProcessor.emailBody;
                            Dictionary<string, MessageAttributeValue> MessageAttributes = new Dictionary<string, MessageAttributeValue>();
                            MessageAttributeValue messageTypeAttribute1 = new MessageAttributeValue
                            {
                                DataType = "String",
                                StringValue = emailProcessor.name
                            };
                            MessageAttributes.Add("Name", messageTypeAttribute1);
                            MessageAttributeValue messageTypeAttribute2 = new MessageAttributeValue
                            {
                                DataType = "String",
                                StringValue = emailProcessor.emailTo
                            };
                            MessageAttributes.Add("To", messageTypeAttribute2);
                            MessageAttributeValue messageTypeAttribute3 = new MessageAttributeValue
                            {
                                DataType = "String",
                                StringValue = "Re - " + emailProcessor.subject
                            };
                            MessageAttributes.Add("Subject", messageTypeAttribute3);
                            sendMessageRequest.MessageAttributes = MessageAttributes;
                            SendMessageResponse sendMessageResponse = await amazonSQSClient.SendMessageAsync(sendMessageRequest);
                            return "{\"Message\":\"Email did not pass checks\",\"lambdaResult\":\"Success\"}";
                        }
                        catch(Exception error)
                        {
                            context.Logger.LogLine($"Error writing to queue {sqsEmailURL}.");
                            context.Logger.LogLine(error.Message);
                            context.Logger.LogLine(error.StackTrace);
                            return "{\"Message\":\"Error writing to queue\",\"lambdaResult\":\"Success\"}";
                        }
                       
                    }
                }
                catch (Exception e)
                {
                    context.Logger.LogLine($"Error getting object {s3Event.Object.Key} from bucket {s3Event.Bucket.Name}.");
                    context.Logger.LogLine(e.Message);
                    context.Logger.LogLine(e.StackTrace);
                    return "{\"Message\":\"Error getting object\",\"lambdaResult\":\"Success\"}";
                }
            }
            else
            {
                return "{\"Message\":\"Error getting secrets\",\"lambdaResult\":\"Success\"}";
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
        public String sqsFAQURLbeta { get; set; }
        public String sqsFAQURLprod { get; set; }
        public String sqsEmailURLLive { get; set; }
        public String sqsEmailURLTest { get; set; }
        public String imageModerationConfidence { get; set; }
        public String wncpendingimagesbucket { get; set; }
        public String wncquarantinedimagesbucket { get; set; }
        public String nncpendingimagesbucket { get; set; }
        public String nncquarantinedimagesbucket { get; set; }
        public String templateBucketLive { get; set; }
        public String templateBucketTest { get; set; }
        public String nncTemplateBucketLive { get; set; }
        public String nncTemplateBucketTest { get; set; }
    }
}
