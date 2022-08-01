using Amazon.Lambda.Core;
using Amazon.Lambda.LexV2Events;
using System.Text.Json;

namespace Norbert;

public class LeaveAMessageIntentProcessor : AbstractIntentProcessor
{
    public override LexV2Response Process(LexEventV2 lexEvent, ILambdaContext context, IDictionary<String, String> requestAttributes, IDictionary<String, String> sessionAttributes, IDictionary<String, LexV2.LexIntentV2.LexSlotV2> slots)
    {
        Console.WriteLine(" ");
        Console.WriteLine("LeaveAMessageIntentProcessor Started");
        String instance = "Beta";
        try
        {
            if (context.InvokedFunctionArn.ToLower().Contains("prod"))
            {
                instance = " Prod";
            }
        }
        catch (Exception){}

        String[] responseMessages = {
            "Write me a letter"
        };
        Console.WriteLine("LeaveAMessageIntentProcessor Ended");

        return Close(
                    "LeaveAMessage",
                    "Fulfilled",
                    responseMessages,
                    requestAttributes,
                    sessionAttributes
                );
    }
}