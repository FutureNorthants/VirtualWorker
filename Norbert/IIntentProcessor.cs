using Amazon.Lambda.Core;
using Amazon.Lambda.LexV2Events;

namespace Norbert;

/// <summary>
/// Represents an intent processor that the Lambda function will invoke to process the event.
/// </summary>
public interface IIntentProcessor
{
    /// <summary>
    /// Main method for processing the Lex event for the intent.
    /// </summary>
    /// <param name="lexEvent"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    LexV2Response Process(LexEventV2 lexEvent, ILambdaContext context, IDictionary<String, String> sessionAttributes, IDictionary<String, String> requestAttributes, IDictionary<String, LexV2.LexIntentV2.LexSlotV2> slots);
}