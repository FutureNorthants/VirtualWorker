using Amazon.Lambda.Core;
using System.Text.Json.Nodes;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace GetGreeting;

public class GreetingResponse
{
    public String? greeting { get; set; }
}
public class Function
{
    public GreetingResponse FunctionHandler(Object input, ILambdaContext context)
    {
        Console.WriteLine("Payload : " + input);
        String personaName = "";
        try
        {
            JsonNode connectJSON = JsonNode.Parse(input.ToString()!)!;
            if (connectJSON["Details"]!["ContactData"]!["Attributes"]!["persona"]!.GetValue<String>().ToLower().Equals("male"))
            {
                personaName = Environment.GetEnvironmentVariable("malePersona")!;
            }
            else
            {
                personaName = Environment.GetEnvironmentVariable("femalePersona")!;
            }
        }
        catch (Exception) { }
        Boolean initialGreeting = false;
        try
        {
            JsonNode connectJSON = JsonNode.Parse(input.ToString()!)!;
            Console.WriteLine("gREETINGtYPE : " + connectJSON["Details"]!["ContactData"]!["Attributes"]!["greetingType"]!.GetValue<String>().ToLower());
            if (connectJSON["Details"]!["ContactData"]!["Attributes"]!["greetingType"]!.GetValue<String>().ToLower().Equals("initial"))
            {
                initialGreeting = true;
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
        catch(Exception error) 
        {
            Console.WriteLine("Error : " + error.Message);
        }

        if (initialGreeting)
        {
            GreetingResponse greeting = new()
            {
                greeting = "Hello. I am " + personaName + ", your virtual contact centre agent. How can I help you today? Type 'help' if you're not sure, if I can't help you one of my colleagues will join the chat to assist you.",
            };
            return greeting;
        }
        else
        {
            GreetingResponse greeting = new()
            {
                greeting = "Is there anything else I can help you with today?",
            };
            return greeting;
        }
 
    }
}
