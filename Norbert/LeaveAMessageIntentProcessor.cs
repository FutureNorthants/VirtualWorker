using Amazon.Lambda.Core;
using Amazon.Lambda.LexV2Events;
using System.Text.Json;
using static Norbert.LexV2.LexIntentV2;

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
                lexEvent.SessionState.Intent.Slots.TryGetValue("CustomerEmail", out LexSlotV2 customerEmail);
                try
                {
                    if (lexEvent.ProposedNextState.DialogAction.SlotToElicit.ToLower().Equals("message") && 
                        !customerEmail.Value.ResolvedValues[0].Equals(lexEvent.InputTranscript))
                    {
                        String[] responseMessages1 = { "Write me a letter3" };
                        return Close("LeaveAMessage",
                             "Fulfilled",
                             responseMessages1,
                             requestAttributes,
                             sessionAttributes
                             );
                    }
                    else
                    {                       
                        return Delegate2(lexEvent);
                    }
                }
                catch (Exception)
                {
                    String[] responseMessages2 = { "Write me a letter2" };
                    return Close("LeaveAMessage",
                             "Fulfilled",
                             responseMessages2,
                             requestAttributes,
                             sessionAttributes
                             );
                }
                
                
            case "FulfillmentCodeHook":
                Console.WriteLine("FulfillmentCodeHook");
                String[] responseMessages = { "Write me a letter1" };             
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