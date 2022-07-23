using Amazon.Lambda.Core;
using Amazon.Lambda.LexV2Events;
//using Newtonsoft.Json.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Web;

namespace Norbert;

public class DefaultIntentProcessor : AbstractIntentProcessor
{
    public override LexV2Response Process(LexEventV2 lexEvent, ILambdaContext context, IDictionary<String, String> requestAttributes, IDictionary<String, String> sessionAttributes, IDictionary<String, LexV2.LexIntentV2.LexSlotV2> slots)
    {
        try
        {
            String[] responseMessages = {
                 getFAQResponseAsync(lexEvent.InputTranscript)
            };
            return Close(
             "Default",
             "Fulfilled",
             responseMessages,
             requestAttributes,
             sessionAttributes
             );
        }
        catch (ApplicationException error)
        {
            return Handover(requestAttributes, sessionAttributes);
            //responseMessages[0] = "Please wait whilst we connect you to a member of staff to help with this query";
            //if (error is not ApplicationException)
            //{
            //    Console.WriteLine("Error : " + error.Message);
            //    Console.WriteLine(error.StackTrace);
            //}
            //return Close(lexEvent.Interpretations[0].Intent.Name, "Failed", responseMessages, requestAttributes, sessionAttributes);
        }
        catch (Exception error)
        {
            return Handover(requestAttributes, sessionAttributes);
            //responseMessages[0] = "Please wait whilst we connect you to a member of staff to help with this query";
            //if (error is not ApplicationException)
            //{
            //    Console.WriteLine("Error : " + error.Message);
            //    Console.WriteLine(error.StackTrace);
            //}
            //return Close(lexEvent.Interpretations[0].Intent.Name, "Failed", responseMessages, requestAttributes, sessionAttributes);
        }
 
    }

    private String getFAQResponseAsync(String query)
    {
        //TODO Move to secrets or params
        String qnaAuthorization = "EndpointKey d77cb979-073f-4427-8d10-c672fec9e5cd";
        String qnaURL = "https://nbcwebservice.azurewebsites.net/qnamaker/knowledgebases/9e0449aa-e598-4c5f-a848-de61d123f91e/generateAnswer";
        try
        {
            HttpClient qnaClient = new HttpClient();
            qnaClient.DefaultRequestHeaders.Add("Authorization", qnaAuthorization);
            HttpResponseMessage responseMessage = qnaClient.PostAsync(qnaURL,new StringContent("{'question':'" + HttpUtility.UrlEncode(query) + "'}", Encoding.UTF8, "application/json")).Result;
            responseMessage.EnsureSuccessStatusCode();
            String responseBody = responseMessage.Content.ReadAsStringAsync().Result;
            JsonNode jsonResponse = JsonNode.Parse(responseBody)!;
            JsonArray answers = jsonResponse!["answers"]!.AsArray()!;
            float score = answers[0]!["score"]!.GetValue<float>();
            //TODO parameterise!
            if(score > 50)
            {
                return answers[0]!["answer"]!.GetValue<String>();
            }
            else
            {
                throw new ApplicationException("");
            }          
        }
        catch (Exception error)
        {
            throw new Exception(error.Message,error);
        }
    }

    private LexV2Response Handover(IDictionary<String, String> requestAttributes, IDictionary<String, String> sessionAttributes)
    {
        Element[] elements = new Element[4];
        elements[0] = new()
        {
            title = "No Thanks, I'm good",
            subtitle = "Nope",
            imageType = "URL",
            imageData = "https://wnclogo.s3.eu-west-2.amazonaws.com/thumb-down-basic-symbol-outline.png",
            imageDescription = "Thumbs Down"
        };

        elements[1] = new()
        {
            title = "I want to leave a message",
            subtitle = "Message",
            imageType = "URL",
            imageData = "https://wnclogo.s3.eu-west-2.amazonaws.com/message.png",
            imageDescription = "Leave a message"
        };

        elements[2] = new()
        {
            title = "I want a callback",
            subtitle = "Callback",
            imageType = "URL",
            imageData = "https://wnclogo.s3.eu-west-2.amazonaws.com/incoming-call.png",
            imageDescription = "Request a callback"
        };

        elements[3] = new()
        {
            title = "I want to chat with a real person",
            subtitle = "Handover to staff",
            imageType = "URL",
            imageData = "https://wnclogo.s3.eu-west-2.amazonaws.com/hi-face-speech-bubble.png",
            imageDescription = "Request a callback"
        };

        Content content = new()
        {
            title = "Sorry, but I'm not able to resolve your query myself. What would you like to do?",
            subtitle = "Tap to select option",
            imageType = "URL",
            imageData = "https://wnclogo.s3.eu-west-2.amazonaws.com/Oops.jpg",
            imageDescription = "Select an option",
            elements = elements
        };

        Replymessage replymessage = new()
        {
            title = "Thanks for selecting!",
            subtitle = "Produce selected",
            imageType = "URL",
            imageData = "https://interactive-msg.s3-us-west-2.amazonaws.com/fruit_34.3kb.jpg",
            imageDescription = "Select a produce to buy"
        };

        Data data = new()
        {
            replyMessage = replymessage,
            content = content
        };

        ListPicker listPicker = new()
        {
            data = data,
        };

        //return listPicker;

        return Ellicit("Handover", "HandoverSelection", requestAttributes, sessionAttributes, "CustomPayload", JsonSerializer.Serialize(listPicker));
    }
}