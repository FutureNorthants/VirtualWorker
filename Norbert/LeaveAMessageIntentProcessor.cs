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

        switch (lexEvent.InvocationSource)
        {
            case "DialogCodeHook":
                Console.WriteLine("DialogCodeHook");
                return Delegate("LeaveAMessage",
                                requestAttributes,
                                sessionAttributes
                                );
            case "FulfillmentCodeHook":
                Console.WriteLine("FulfillmentCodeHook");
                String[] responseMessages = { "Write me a letter" };             
                return Close("LeaveAMessage",
                             "Fulfilled",
                             responseMessages,
                             requestAttributes,
                             sessionAttributes
                             );
            default:
                Console.WriteLine("ERROR Unknown InvocationSource : " + lexEvent.InvocationSource);
                return Delegate("LeaveAMessage",
                                requestAttributes,
                                sessionAttributes
                                );
        }
        Console.WriteLine("LeaveAMessageIntentProcessor Ended");
    }

    private static void ValidateMessage()
    {

    }

    private static void CreateCase()
    {

    }
}