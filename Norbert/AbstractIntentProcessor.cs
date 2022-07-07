using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.LexV2Events;

namespace Norbert;

/// <summary>
/// Base class for intent processors.
/// </summary>
public abstract class AbstractIntentProcessor : IIntentProcessor
{

    internal const string MESSAGE_CONTENT_TYPE = "PlainText";

    /// <summary>
    /// Main method for proccessing the lex event for the intent.
    /// </summary>
    /// <param name="lexEvent"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public abstract LexV2Response Process(LexEventV2 lexEvent, ILambdaContext context, IDictionary<String, String> sessionAttributes, IDictionary<String, String> requestAttributes, IDictionary<String, LexV2.LexIntentV2.LexSlotV2> slots);

    //protected string SerializeReservation(FlowerOrder order)
    //{
    //    return JsonSerializer.Serialize(order, new JsonSerializerOptions
    //    {
    //        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    //    });
    //}

    //protected FlowerOrder DeserializeReservation(string json)
    //{
    //    return JsonSerializer.Deserialize<FlowerOrder>(json) ?? new FlowerOrder()   ;
    //}

    protected LexV2Response Close(String intent, String fulfillmentState, String responseMessage, IDictionary<String, String> requestAttributes, IDictionary<String, String> sessionAttributes)
    {
        Console.WriteLine("Closing");
        LexV2SessionState sessionState = new()
        {
            DialogAction = new LexV2DialogAction
            {
                Type = "Close"
            },
            Intent = new LexV2Intent 
            { 
                Name = intent,
                State = fulfillmentState 
            },
            SessionAttributes = new Dictionary<String, String>(sessionAttributes)
    };
        LexV2Message[] messages =  new LexV2Message[1];
            messages[0] = new LexV2Message
            {
                ContentType = "PlainText",
                Content = responseMessage
            };
  
        return new LexV2Response
        {
            SessionState = sessionState,
            Messages = messages,
            RequestAttributes = requestAttributes   
        };
    }

    protected LexV2Response Delegate(String intent, IDictionary<String, String> requestAttributes)
    {
        Console.WriteLine("Delegating");
        LexV2SessionState sessionState = new()
        {
            DialogAction = new LexV2DialogAction
            {
                Type = "Delegate"
            },
            Intent = new LexV2Intent
            {
                Name = intent,
                State = "ReadyForFulfillment"
            }
        };

        return new LexV2Response
        {
            SessionState = sessionState,
            RequestAttributes = requestAttributes
        };
    }

    //protected LexV2Response ElicitSlot(IDictionary<string, string> sessionAttributes, string intentName, IDictionary<string, string?> slots, string? slotToElicit, LexResponse.LexMessage? message)
    //{
    //    return new LexV2Response
    //    {
    //        SessionAttributes = sessionAttributes,
    //        DialogAction = new LexResponse.LexDialogAction
    //        {
    //            Type = "ElicitSlot",
    //            IntentName = intentName,
    //            Slots = slots,
    //            SlotToElicit = slotToElicit,
    //            Message = message
    //        }
    //    };
    //}

    //protected LexV2Response ConfirmIntent(IDictionary<string, string> sessionAttributes, string intentName, IDictionary<string, string?> slots, LexResponse.LexMessage? message)
    //{
    //    return new LexResponse
    //    {
    //        SessionAttributes = sessionAttributes,
    //        DialogAction = new LexResponse.LexDialogAction
    //        {
    //            Type = "ConfirmIntent",
    //            IntentName = intentName,
    //            Slots = slots,
    //            Message = message
    //        }
    //    };
    //}
}