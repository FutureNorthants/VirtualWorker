using Amazon.Lambda.Core;
using Amazon.Lambda.LexV2Events;
using System.Text.Json;

namespace Norbert;

public class HandoverIntentProcessor : AbstractIntentProcessor
{
    public override LexV2Response Process(LexEventV2 lexEvent, ILambdaContext context, IDictionary<String, String> requestAttributes, IDictionary<String, String> sessionAttributes, IDictionary<String, LexV2.LexIntentV2.LexSlotV2> slots)
    {
        Console.WriteLine(" ");
        Console.WriteLine("HandoverIntentProcessor Started");

        String[] responseMessages;

        switch (lexEvent.InputTranscript)
        {
            case "End Chat":
                String[] EndChatMessages = { "Laters, dude" };
                responseMessages = EndChatMessages;
                sessionAttributes.Add("HandOverTo", "EndChat");
                return Close("Handover","Fulfilled",responseMessages,requestAttributes,sessionAttributes);
            case "Chat with a real person":
                String[] RealPersonMessages = { "Please wait whilst we connect you to one of my colleagues" };
                responseMessages = RealPersonMessages;
                sessionAttributes.Add("HandOverTo", "WebChat");
                return Close("Handover","Fulfilled", responseMessages, requestAttributes, sessionAttributes);
            case "Leave a message":
                return Ellicit("LeaveAMessage", "CustomerEmail", requestAttributes, sessionAttributes, "PlainText", "Please provide an email address for us to respond to");
            default:
                String[] DefaultMessages = { "Handover has finished" };
                responseMessages = DefaultMessages;
                break;

        }

        Console.WriteLine("HandoverIntentProcessor Ended");

        return Close(
                    "Handover",
                    "Fulfilled",
                    responseMessages,
                    requestAttributes,
                    sessionAttributes
                );
    }
}