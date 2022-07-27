using Amazon.Lambda.Core;
using Amazon.Lambda.LexV2Events;
//using Newtonsoft.Json.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Web;

namespace Norbert;

public class CollectionDayIntentProcessor : AbstractIntentProcessor
{
    public override LexV2Response Process(LexEventV2 lexEvent, ILambdaContext context, IDictionary<String, String> sessionAttributes, IDictionary<String, String> requestAttributes, IDictionary<String, LexV2.LexIntentV2.LexSlotV2> slots)
    {
        try
        {
            LexV2.LexIntentV2.LexSlotValueV2 slotValue = slots["Postcode"].Value;
            String[] responseMessages = {
               getBinCollectionDetails(slotValue.InterpretedValue)
            };
            return Close(
                        "Default",
                        "Fulfilled",
                        responseMessages,
                        requestAttributes,
                        sessionAttributes
                    );
        }
        catch(ApplicationException error)
        {
            String[] responseMessages = {
               "Please wait whilst we connect you to a member of staff to help with this query"
            };
            Console.WriteLine("Error : " + error.Message);
            Console.WriteLine(error.StackTrace);
            return Close(lexEvent.Interpretations[0].Intent.Name, "Failed", responseMessages, requestAttributes,sessionAttributes);
        }
        catch(Exception)
        {
            return Delegate(lexEvent.ProposedNextState.Intent.Name,requestAttributes, sessionAttributes);
        }      
    }

    private String getBinCollectionDetails(String postCode)
    {
        //TODO parameterise!
        String collectionDetailsURL = "https://mycouncil-test.northampton.digital/BinRoundFinder?postcode=";
        try
        {
            HttpClient collectionClient = new HttpClient();
            HttpResponseMessage responseMessage = collectionClient.GetAsync(collectionDetailsURL+postCode).Result;
            responseMessage.EnsureSuccessStatusCode();
            String responseBody = responseMessage.Content.ReadAsStringAsync().Result;
            JsonNode jsonResponse = JsonNode.Parse(responseBody)!;
            if (jsonResponse["result"]!.GetValue<String>().ToLower().Equals("success")&&
                jsonResponse["rounds"]!.GetValue<String>().ToLower().Equals("single"))
            {
                return "Your collection day is " + jsonResponse["day"]!.GetValue<String>() +
                       ", please put out your " + jsonResponse["type"]!.GetValue<String>() + " bin by 6am";
            }
            else
            {
                throw new ApplicationException("Multi Round postcode");
            }
        }
        catch (Exception error)
        {
            throw new ApplicationException("Postcode API Error : " +  error.Message);
        }
    }
}