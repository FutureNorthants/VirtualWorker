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

        String[] responseMessages = {
            context.FunctionName + "( " + instance + " " + context.FunctionVersion + " ) @ " + currentTime.ToString("dddd, dd MMMM yyyy HH:mm:ss")
        };

        return Close(
                    "Debug",
                    "Fulfilled",
                    responseMessages,
                    requestAttributes,
                    sessionAttributes
                );
    }
}