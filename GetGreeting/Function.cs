using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using System.Text.Json.Nodes;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace GetGreeting;

public class GreetingResponse
{
    public String? Greeting { get; set; }
}
public class Function
{
    private static readonly RegionEndpoint primaryRegion = RegionEndpoint.EUWest2;
    public static GreetingResponse FunctionHandler(Object input, ILambdaContext context)
    {
        String continuationTableName = Environment.GetEnvironmentVariable("continuationTableNameTest")!;
        try
        {
            if (context.InvokedFunctionArn.ToLower().Contains("prod"))
            {
                continuationTableName = Environment.GetEnvironmentVariable("continuationTableNameLive")!;
            }
        }
        catch (Exception) { }
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
            Console.WriteLine("GreetingType : " + connectJSON["Details"]!["ContactData"]!["Attributes"]!["greetingType"]!.GetValue<String>().ToLower());
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
                Greeting = "Hello. I am " + personaName + ", your virtual contact centre agent. How can I help you today? Type 'help' if you're not sure, if I can't help you one of my colleagues will join the chat to assist you.",
            };
            return greeting;
        }
        else
        {
            GreetingResponse greeting = new()
            {
                Greeting = GetContinuationMessage(continuationTableName)
        };
            return greeting;
        }
    }
    private static String GetContinuationMessage(String continuationTableName) 
    {
        try
        {
            AmazonDynamoDBClient dynamoDBClient = new(primaryRegion);

            Table productCatalogTable = Table.LoadTable(dynamoDBClient, continuationTableName);
            ScanFilter scanFilter = new();
            ScanOperationConfig config = new()
            {
                Filter = scanFilter,
                Select = SelectValues.SpecificAttributes,
                AttributesToGet = new List<string> { "Message" }
            };
            Search search = productCatalogTable.Scan(config);
            List<Document> allTheMessages = new();
            do
            {
                List<Document> batchOfRecords = search.GetNextSetAsync().Result;

                foreach (Document message in batchOfRecords)
                {
                   allTheMessages.Add(message); 
                }
            } while (!search.IsDone);
            Random random = new();         
            Document[] arrayOfAllTheMessages = allTheMessages.ToArray();
            arrayOfAllTheMessages[random.Next(allTheMessages.Count)].TryGetValue("Message", out DynamoDBEntry response);
            return response;
        }
        catch(Exception error)
        {
            Console.WriteLine("ERROR Getting Continuation Responses : " + error.Message);
            return "Anything else??";
        }
    }
}
