using Amazon.Lambda.Core;
using Amazon.Lambda.LexV2Events;

using static Norbert.FlowerOrder;

namespace Norbert;

public class DebugIntentProcessor : AbstractIntentProcessor
{
    public const string TYPE_SLOT = "FlowerType";
    public const string PICK_UP_DATE_SLOT = "PickupDate";
    public const string PICK_UP_TIME_SLOT = "PickupTime";
    public const string INVOCATION_SOURCE = "invocationSource";
    FlowerTypes _chosenFlowerType = FlowerTypes.Null;

    /// <summary>
    /// Performs dialog management and fulfillment for ordering flowers.
    /// 
    /// Beyond fulfillment, the implementation for this intent demonstrates the following:
    /// 1) Use of elicitSlot in slot validation and re-prompting
    /// 2) Use of sessionAttributes to pass information that can be used to guide the conversation
    /// </summary>
    /// <param name="lexEvent"></param>
    /// <param name="context"></param>
    /// <returns></returns>
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
        return Close(
                    "Debug",
                    "Fulfilled",
                    context.FunctionName + "( " + instance + " " + context.FunctionVersion + " ) @ " + currentTime.ToString("dddd, dd MMMM yyyy HH:mm:ss"),
                    requestAttributes,
                    sessionAttributes
                );
    }
}