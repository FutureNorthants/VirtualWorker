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
         return Close(
                    "Debug",
                    "Fulfilled",
                    "Hello World2!",
                    requestAttributes,
                    sessionAttributes
                );
    }
}