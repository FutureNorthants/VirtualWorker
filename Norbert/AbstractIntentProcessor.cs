using Amazon.Lambda.Core;
using Amazon.Lambda.LexV2Events;
using System.Text.Json;
using static Norbert.LexResponseV2;

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

    protected LexV2Response Close(String intent, String fulfillmentState, String[] responseMessages, IDictionary<String, String> requestAttributes, IDictionary<String, String> sessionAttributes)
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
        LexV2Message[] messages = new LexV2Message[responseMessages.Length];

        for (int currentMessage = 0; currentMessage < responseMessages.Length; currentMessage++)
        {
            messages[currentMessage] = new LexV2Message
            {
                ContentType = "PlainText",
                Content = responseMessages[currentMessage]
            };
        }

        return new LexV2Response
        {
            SessionState = sessionState,
            Messages = messages,
            RequestAttributes = requestAttributes
        };
    }
    protected static LexV2Response CloseWithResponseCard(String intent, String fulfillmentState, String responseMessage, String title, String subtitle, LexV2Button[] responseButtons, IDictionary<String, String> requestAttributes, IDictionary<String, String> sessionAttributes)
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
        LexV2Message[] messages = new LexV2Message[1];
        messages[0] = new LexV2Message
        {
            ContentType = "ImageResponseCard",
            Content = responseMessage,
            ImageResponseCard = new LexV2ImageResponseCard
            {
                Title = title,
                Subtitle = subtitle,
                Buttons = responseButtons
            }
        };

        return new LexV2Response
        {
            SessionState = sessionState,
            Messages = messages,
            RequestAttributes = requestAttributes
        };
    }

    protected LexV2Response Delegate(String intent, IDictionary<String, String> requestAttributes, IDictionary<String, String> sessionAttributes)
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
            },
            SessionAttributes = new Dictionary<String, String>(sessionAttributes)
        };

        return new LexV2Response
        {
            SessionState = sessionState,
            RequestAttributes = requestAttributes
        };
    }

    protected LexV2Response Delegate2(LexEventV2 lexEvent)
    {
        Console.WriteLine("Delegating2");
        JsonSerializerOptions options = new JsonSerializerOptions { WriteIndented = true };
        String jsonString = JsonSerializer.Serialize(lexEvent.SessionState);

        LexV2SessionState? sessionState = JsonSerializer.Deserialize<LexV2SessionState>(jsonString);
        sessionState.DialogAction = new LexV2DialogAction
        {
            Type = "Delegate"
        };

        LexV2Response response = new LexV2Response
        {
            SessionState = sessionState
        };

        Console.WriteLine("Response as JSON : " + JsonSerializer.Serialize(response));

        return response;
    }

    protected LexV2Response Ellicit(String intent, String slotName, IDictionary<String, String> requestAttributes, IDictionary<String, String> sessionAttributes, String messageContentType, String message)
    {
        Console.WriteLine("Elliciting");
        LexV2SessionState sessionState = new()
        {
            DialogAction = new LexV2DialogAction
            {
                Type = "ElicitSlot",
                SlotElicitationStyle = "Default",
                SlotToElicit = slotName
            },
            Intent = new LexV2Intent
            {
                Name = intent
            },
            SessionAttributes = new Dictionary<String, String>(sessionAttributes)
        };

        LexV2Message[] messages = new LexV2Message[1];
            messages[0] = new LexV2Message
            {
                ContentType = messageContentType,
                Content = message
            };

        return new LexV2Response
        {
            SessionState = sessionState,
            Messages = messages,
            RequestAttributes = requestAttributes
        };
    }

    protected LexV2Response Handover(IDictionary<String, String> requestAttributes, IDictionary<String, String> sessionAttributes)
    {
        Element[] elements = new Element[4];
        elements[0] = new()
        {
            title = "End Chat",
            subtitle = " ",
            imageType = "URL",
            imageData = "https://wnclogo.s3.eu-west-2.amazonaws.com/thumb-down-basic-symbol-outline.png",
            imageDescription = "End Chat"
        };

        elements[1] = new()
        {
            title = "Leave a message",
            subtitle = " ",
            imageType = "URL",
            imageData = "https://wnclogo.s3.eu-west-2.amazonaws.com/message.png",
            imageDescription = "Leave a message"
        };

        elements[2] = new()
        {
            title = "Request a callback",
            subtitle = " ",
            imageType = "URL",
            imageData = "https://wnclogo.s3.eu-west-2.amazonaws.com/incoming-call.png",
            imageDescription = "Request a callback"
        };

        elements[3] = new()
        {
            title = "Chat with a real person",
            subtitle = " ",
            imageType = "URL",
            imageData = "https://wnclogo.s3.eu-west-2.amazonaws.com/hi-face-speech-bubble.png",
            imageDescription = "Chat with a real person"
        };

        Content content = new()
        {
            title = "Sorry, but I'm not able to resolve your query myself. What would you like to do?",
            subtitle = "Please select option",
            imageType = "URL",
            imageData = "https://wnclogo.s3.eu-west-2.amazonaws.com/Oops.jpg",
            imageDescription = "Select an option",
            elements = elements
        };

        Replymessage replymessage = new()
        {
            title = "Thanks for selecting!",
            subtitle = "Produce selected",
            imageType = "URL",
            imageData = "https://interactive-msg.s3-us-west-2.amazonaws.com/fruit_34.3kb.jpg",
            imageDescription = "Select a produce to buy"
        };

        Data data = new()
        {
            replyMessage = replymessage,
            content = content
        };

        ListPicker listPicker = new()
        {
            data = data,
        };

        return Ellicit("Handover", "HandoverSelection", requestAttributes, sessionAttributes, "CustomPayload", JsonSerializer.Serialize(listPicker));
    }

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