using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Newtonsoft.Json;
using static Amazon.Lambda.SQSEvents.SQSEvent;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace SendEmail
{
    public class Function
    {
        private static readonly RegionEndpoint primaryRegion = RegionEndpoint.EUWest2;
        private static readonly String secretName = "nbcGlobal";
        private static readonly String secretAlias = "AWSCURRENT";
        private Secrets secrets = null;
        private String norbertSendFrom = "";
        private Boolean live = false;

        public Function()
        {
        }

        public async Task FunctionHandler(SQSEvent evnt, ILambdaContext context)
        {
            context.Logger.LogLine("Version : 2");

            if (await GetSecrets())
            {
                try
                {
                    if (context.InvokedFunctionArn.ToLower().Contains("prod"))
                    {
                        live = true;
                    }
                }
                catch (Exception)
                {
                }

                context.Logger.LogLine("NorbertSendFrom : " + norbertSendFrom);

                foreach (var message in evnt.Records)
                {
                    await ProcessMessageAsync(message, context);
                }
            }
        }

        private async Task ProcessMessageAsync(SQSEvent.SQSMessage message, ILambdaContext context)
        {
            message.MessageAttributes.TryGetValue("To", out MessageAttribute toEmail);
            message.MessageAttributes.TryGetValue("Subject", out MessageAttribute subject);
            norbertSendFrom = secrets.norbertSendFromTest;
            context.Logger.LogLine("Finding SendFrom");
            if (subject.StringValue.ToLower().Contains("ema"))
            {
                if (live)
                {
                    norbertSendFrom = secrets.norbertSendFromLive;
                    context.Logger.LogLine("Sending from WNC Live : " + norbertSendFrom);
                }
                else
                {
                    norbertSendFrom = secrets.norbertSendFromTest;
                    context.Logger.LogLine("Sending from WNC Test : " + norbertSendFrom);
                }
            }
            if (subject.StringValue.ToLower().Contains("emn"))
            {
                if (live)
                {
                    norbertSendFrom = secrets.nncSendFromLive;
                    context.Logger.LogLine("Sending from NNC Live : " + norbertSendFrom);
                }
                else
                {
                    norbertSendFrom = secrets.nncSendFromTest;
                    context.Logger.LogLine("Sending from NNC Test : " + norbertSendFrom);
                }
            }
            context.Logger.LogLine("Sending email to " + toEmail.StringValue + " with subject of : " + subject.StringValue);
            String messageBody = message.Body;
            Random rand = new Random();
            if (rand.Next(0, 2) == 0)
            {
                messageBody = messageBody.Replace("NNN", secrets.botPersona1);
            }
            else
            {
                messageBody = messageBody.Replace("NNN", secrets.botPersona2);
            }
            using (AmazonSimpleEmailServiceClient client = new AmazonSimpleEmailServiceClient(RegionEndpoint.EUWest1))
            {
                SendEmailRequest sendRequest = new SendEmailRequest
                {

                    Source = norbertSendFrom,
                    Destination = new Destination
                    {
                        ToAddresses = new List<string> { toEmail.StringValue },
                        BccAddresses = new List<string> { "kwhite@northampton.gov.uk", "kevin.white@futurenorthants.org" }
                    },
                    Message = new Message
                    {
                        Subject = new Content(subject.StringValue),
                        Body = new Body
                        {
                            Html = new Content
                            {
                                Charset = "UTF-8",
                                Data = messageBody
                            },
                        }
                    },
                    ConfigurationSetName = "AllMail"
                };
                try
                {
                    SendEmailResponse sesresponse = await client.SendEmailAsync(sendRequest);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error message: " + ex.Message);
                }
            }
            await Task.CompletedTask;
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
        public string norbertSendFromLive { get; set; }
        public string norbertSendFromTest { get; set; }
        public string nncSendFromLive { get; set; }
        public string nncSendFromTest { get; set; }
        public string botPersona1 { get; set; }
        public string botPersona2 { get; set; }
    }
}