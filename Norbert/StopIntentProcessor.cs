using Amazon.Lambda.Core;
using Amazon.Lambda.LexV2Events;
using System.Text.Json;

namespace Norbert;

public class StopIntentProcessor : AbstractIntentProcessor
{
    public override LexV2Response Process(LexEventV2 lexEvent, ILambdaContext context, IDictionary<String, String> requestAttributes, IDictionary<String, String> sessionAttributes, IDictionary<String, LexV2.LexIntentV2.LexSlotV2> slots)
    {
        Console.WriteLine(" ");
        Console.WriteLine("StopIntentProcessor Started");
        String instance = "Beta";
        try
        {
            if (context.InvokedFunctionArn.ToLower().Contains("prod"))
            {
                instance = " Prod";
            }
        }
        catch (Exception){}

        String[] responses = { "Goodbye", "Sorry", "Haters gonna hate" };
        Random random = new();

        String[] responseMessages = {
            responses[random.Next(responses.Length)]
        };
        Console.WriteLine("StopIntentProcessor Ended");

        return Close(
                    "Stop",
                    "Fulfilled",
                    responseMessages,
                    requestAttributes,
                    sessionAttributes
                );
    }
}