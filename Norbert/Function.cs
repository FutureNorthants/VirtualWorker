using Amazon.Lambda.Core;
using Amazon.Lambda.LexEvents;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Norbert;

public class Function
{

    /// <summary>
    /// A simple function that takes a string and does a ToUpper
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public LexResponse FunctionHandler(LexEvent lexEvent, ILambdaContext context)
    {
        IIntentProcessor process;

        if (lexEvent.CurrentIntent.Name == "Debug")
        {
            process = new DebugIntentProcessor();
        }
        else
        {
            throw new Exception($"Intent with name {lexEvent.CurrentIntent.Name} not supported");
        }


        return process.Process(lexEvent, context);
    }
}
