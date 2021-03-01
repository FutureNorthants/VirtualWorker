using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon;
using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.S3;
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
        private static readonly String secretName = "nbcGlobal";
        private static readonly String secretAlias = "AWSCURRENT";
        private Secrets secrets = null;

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
                try
                {
                    var response = await this.S3Client.GetObjectMetadataAsync(s3Event.Bucket.Name, s3Event.Object.Key);
                    AmazonSQSClient amazonSQSClient = new AmazonSQSClient();
                    ProcessEmail emailProcessor = new ProcessEmail();
                    Console.WriteLine("Image Moderation Confidence Parameter = " + secrets.imageModerationConfidence);
                    long imageModerationLevel = long.Parse(secrets.imageModerationConfidence);
                    if (emailProcessor.Process(s3Event.Bucket.Name, s3Event.Object.Key, imageModerationLevel))
                    {
                        String sqsFAQURL = secrets.sqsFAQURLbeta;
                        try
                        {
                            if (context.InvokedFunctionArn.ToLower().Contains("prod"))
                            {
                                Console.WriteLine("Prod version");
                                sqsFAQURL = secrets.sqsFAQURLprod;
                            }
                        }
                        catch (Exception)
                        {
                            Console.WriteLine("Beta version");
                        }
                        
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
                        SendMessageRequest sendMessageRequest = new SendMessageRequest
                        {
                            QueueUrl = secrets.sqsEmailURL
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
        public String sqsEmailURL { get; set; }
        public String imageModerationConfidence { get; set; }
    }
}
