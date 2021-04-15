using System;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Email2CXM.Helpers;
using static Amazon.Lambda.SQSEvents.SQSEvent;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace Email2CXM
{
    public class Function
    {

        public async Task FunctionHandler(SQSEvent evnt, ILambdaContext context)
        {
            Boolean liveInstance = false;

            try
            {
                if (context.InvokedFunctionArn.ToLower().Contains("prod"))
                {
                    Console.WriteLine(">>>Prod Case<<<");
                    liveInstance = true;
                }
                else
                {
                    Console.WriteLine(">>>Test Case<<<");
                }
            }
            catch (Exception)
            {
                Console.WriteLine(">>>Test Case<<<");
            }

            foreach (SQSMessage message in evnt.Records)
            {
                await ProcessMessageAsync(message, liveInstance);
            }
        }

        private async Task ProcessMessageAsync(SQSEvent.SQSMessage message, Boolean liveInstance)
        {
            String bucket;
            String file;
            try
            {
                message.MessageAttributes.TryGetValue("Bucket", out MessageAttribute messageBucket);
                message.MessageAttributes.TryGetValue("Object", out MessageAttribute messageObject);
                bucket = messageBucket.StringValue;
                file = messageObject.StringValue;
            }
            catch
            {
                //Test Code
                bucket = "norbert.emails.test";
                //bucket = "nnc.emails.test";
                file = "2463hir6cm6qut3kjheqs8pj430obmk4105pn981";
            }
 

            ProcessMessage messageProcessor = new ProcessMessage();

            messageProcessor.Process(bucket, file, liveInstance);

            await Task.CompletedTask;
        }
    }
}
