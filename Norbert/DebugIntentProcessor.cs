using Amazon.Lambda.Core;
using Amazon.Lambda.LexV2Events;
using System.Text.Json;

namespace Norbert;

public class DebugIntentProcessor : AbstractIntentProcessor
{
    public override LexV2Response Process(LexEventV2 lexEvent, ILambdaContext context, IDictionary<String, String> requestAttributes, IDictionary<String, String> sessionAttributes, IDictionary<String, LexV2.LexIntentV2.LexSlotV2> slots)
    {
        String instance = "Beta";
        try
        {
            if (context.InvokedFunctionArn.ToLower().Contains("prod"))
            {
                instance = " Prod";
            }
        }
        catch (Exception){}
        DateTime currentTime = DateTime.UtcNow.ToLocalTime();
        try
        {
            TimeZoneInfo curTimeZone = TimeZoneInfo.Local;
            if (curTimeZone.IsDaylightSavingTime(DateTime.Now))
            {
                currentTime.AddHours(1);
            }
        }
        catch (Exception error)
        {
            Console.WriteLine("Error : " + error.Message);
        }
        //LexV2Button[] buttons = new LexV2Button[4];
        //buttons[0] = new LexV2Button { Text = "Leave a message", Value = "message"};
        //buttons[1] = new LexV2Button { Text = "Request a callback", Value = "callback"};
        //buttons[2] = new LexV2Button { Text = "Chat with Staff", Value = "handoff"};
        //buttons[3] = new LexV2Button { Text = "End the chat", Value = "stop" };
        //return CloseWithResponseCard(
        //        "Debug",
        //        "Fulfilled",
        //        context.FunctionName + "( " + instance + " " + context.FunctionVersion + " ) @ " + currentTime.ToString("dddd, dd MMMM yyyy HH:mm:ss"),
        //        "I think we need the human touch here",
        //        "What would you like to do?",
        //        buttons,
        //        requestAttributes,
        //        sessionAttributes
        //);

        String[] responseMessages = {
            context.FunctionName + "( " + instance + " " + context.FunctionVersion + " ) @ " + currentTime.ToString("dddd, dd MMMM yyyy HH:mm:ss"),
            "wibble2"
        };

        Element[] elements = new Element[4];
        elements[0] = new()
        {
            title = "No Thanks, I'm good",
            subtitle = "Nope",
            imageType = "URL",
            imageData = "https://wnclogo.s3.eu-west-2.amazonaws.com/thumb-down-basic-symbol-outline.png",
            imageDescription = "Thumbs Down"
        };

        elements[1] = new()
        {
            title = "I want to leave a message",
            subtitle = "Message",
            imageType = "URL",
            imageData = "https://wnclogo.s3.eu-west-2.amazonaws.com/message.png",
            imageDescription = "Leave a message"
        };

        elements[2] = new()
        {
            title = "I want a callback",
            subtitle = "Callback",
            imageType = "URL",
            imageData = "https://wnclogo.s3.eu-west-2.amazonaws.com/incoming-call.png",
            imageDescription = "Request a callback"
        };

        elements[3] = new()
        {
            title = "I want to chat with a real person",
            subtitle = "Handover to staff",
            imageType = "URL",
            imageData = "https://wnclogo.s3.eu-west-2.amazonaws.com/hi-face-speech-bubble.png",
            imageDescription = "Request a callback"
        };

        Content content = new()
        {
            title = "Sorry, but I'm not able to resolve your query myself. What would you like to do?",
            subtitle = "Tap to select option",
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

        //return listPicker;

        return Ellicit("Debug", "Handover", requestAttributes, sessionAttributes, "CustomPayload", JsonSerializer.Serialize(listPicker));

        //return Close(
        //            "Debug",
        //            "Fulfilled",
        //            responseMessages,
        //            requestAttributes,
        //            sessionAttributes
        //        );
    }
}