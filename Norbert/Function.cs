using Amazon.Lambda.Core;
using Amazon.Lambda.LexV2Events;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
//[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace Norbert;

public class Function
{

    /// <summary>
    /// A simple function that takes a string and does a ToUpper
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public LexV2Response FunctionHandler(LexEventV2 lexEvent, ILambdaContext context)
    {
        IIntentProcessor process;

        try
        {
            Console.WriteLine("------------");
            Console.WriteLine("Bot       : " + lexEvent.Bot.Name);
            Console.WriteLine("Alias     : " + lexEvent.Bot.AliasId);
            Console.WriteLine("Version   : " + lexEvent.Bot.Version);
            Console.WriteLine("Intent    : " + lexEvent.Interpretations[0].Intent.Name);
        }
        catch(Exception) { }

        try
        {
            if (lexEvent.Interpretations[0].Intent.Name == "Debug")
            {
                process = new DebugIntentProcessor();
            }
            else
            {
                throw new Exception($"Intent with name {lexEvent.Interpretations[0].Intent.Name} not supported");
            }
         }
        catch(Exception)
        {
            process = new DefaultIntentProcessor();
        }
        return process.Process(lexEvent, context);
    }
}
