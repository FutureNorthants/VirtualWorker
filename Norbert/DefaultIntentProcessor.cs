using Amazon.Lambda.Core;
using Amazon.Lambda.LexV2Events;

namespace Norbert;

public class DefaultIntentProcessor : AbstractIntentProcessor
{
    public override LexV2Response Process(LexEventV2 lexEvent, ILambdaContext context)
    {
        IDictionary<string, string> sessionAttributes = lexEvent.RequestAttributes ?? new Dictionary<string, string>();

        return Close(
                    "Default",
                    "Fulfilled",
                    "I should be reading the FAQ right now",
                    sessionAttributes
                );
    }
}