using Amazon.Lambda.Core;
using Amazon.Lambda.LexV2Events;
using System.Text.Json;

namespace Norbert;

public class EndChatIntentProcessor : AbstractIntentProcessor
{
    public override LexV2Response Process(LexEventV2 lexEvent, ILambdaContext context, IDictionary<String, String> requestAttributes, IDictionary<String, String> sessionAttributes, IDictionary<String, LexV2.LexIntentV2.LexSlotV2> slots)
    {
        Console.WriteLine(" ");
        Console.WriteLine("EndChatIntentProcessor Started");
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
            "Laters, dude"
        };
        Console.WriteLine("EndChatIntentProcessor Ended");

        return Close(
                    "EndChat",
                    "Fulfilled",
                    responseMessages,
                    requestAttributes,
                    sessionAttributes
                );
    }
}