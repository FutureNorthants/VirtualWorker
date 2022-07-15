using Amazon.Lambda.Core;
using Amazon.Lambda.LexV2Events;

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

        return Close(
                    "Debug",
                    "Fulfilled",
                    context.FunctionName + "( " + instance + " " + context.FunctionVersion + " ) @ " + currentTime.ToString("dddd, dd MMMM yyyy HH:mm:ss"),
                    requestAttributes,
                    sessionAttributes
                );
    }
}