using Amazon;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.S3;
using Amazon.S3.Model;
using MimeKit;
using System;
using System.IO;
using System.Net.Mail;
using System.Threading.Tasks;

namespace EmailChecker.Helpers
{
    class ProcessEmail
    {
        private static readonly RegionEndpoint bucketRegion = RegionEndpoint.EUWest1;
        private static IAmazonS3 client;
        public static Boolean emailPassedChecks { get; set; } = false;
        public static Boolean spamCheckPass { get; set; } = false;
        public static Boolean virusCheckPass { get; set; } = false;
        public static Boolean imageCheckPass { get; set; } = false;
        public String emailTo { get; set; } = null;
        public String subject { get; set; } = null;
        public String name { get; set; } = null;
        public String emailBody { get; set; } = null;

        public ProcessEmail()
        {
            client = new AmazonS3Client(bucketRegion);
        }

        public Boolean Process(String bucketName, String keyName)
        {
            return ReadObjectDataAsync(bucketName, keyName).Result;
        }

        private async Task<Boolean> ReadObjectDataAsync(String bucketName, String keyName)
        {
            try
            {
                GetObjectRequest request = new GetObjectRequest
                {
                    BucketName = bucketName,
                    Key = keyName
                };
                using (GetObjectResponse response = await client.GetObjectAsync(request))
                {
                    MimeMessage message = MimeMessage.Load(response.ResponseStream);
                    MailAddressCollection mailAddresses = (MailAddressCollection)message.From;
                    emailTo = mailAddresses[0].Address;
                    subject = message.Subject;
                    name = message.From[0].Name;
                    int numOfAttachments = 0;
                    imageCheckPass = true;
                    foreach (MimeEntity attachment in message.Attachments)
                    {
                        if(!(attachment is MessagePart))
                        {
                            numOfAttachments++;
                            MimePart part = (MimePart)attachment;
                            String fileName = part.FileName;
                            if (!fileName.ToLower().Contains("pdf"))
                            {
                                try
                                {
                                    Stream objectStream = new MemoryStream();
                                    part.Content.DecodeTo(objectStream);
                                    byte[] attachmentArray = new byte[objectStream.Length];

                                    long attachmentLength = objectStream.Length;


                                    using (var imageStream = part.Content.Open())
                                    {
                                        PutObjectRequest putRequest = new PutObjectRequest()
                                        {
                                            InputStream = imageStream,
                                            BucketName = "nbc-pending-images",
                                            Key = fileName,
                                        };
                                        putRequest.Headers.ContentLength = attachmentLength;
                                        await client.PutObjectAsync(putRequest);
                                        Console.WriteLine("Written to S3 : {0}", fileName);
                                    }

                                }
                                catch (Exception error)
                                {
                                    Console.WriteLine("ERROR : Processing Image : '{0}' ", error.Message);
                                    Console.WriteLine(error.StackTrace);
                                }

                                AmazonRekognitionClient rekognitionClient = new AmazonRekognitionClient();

                                DetectModerationLabelsRequest detectModerationLabelsRequest = new DetectModerationLabelsRequest()
                                {
                                    Image = new Image()
                                    {
                                        S3Object = new Amazon.Rekognition.Model.S3Object()
                                        {
                                            Name = fileName,
                                            Bucket = "nbc-pending-images"
                                        },
                                    },
                                    MinConfidence = 60F
                                };

                                try
                                {
                                    DetectModerationLabelsResponse detectModerationLabelsResponse = await rekognitionClient.DetectModerationLabelsAsync(detectModerationLabelsRequest);
                                    Console.WriteLine("Detected labels for " + "EMA000146-image.jpg");
                                    foreach (ModerationLabel label in detectModerationLabelsResponse.ModerationLabels)
                                    {
                                        if (!String.IsNullOrEmpty(label.ParentName))
                                        {
                                            Console.WriteLine("Label: {0}\n Confidence: {1}\n Parent: {2}",
                                            label.Name, label.Confidence, label.ParentName);
                                            switch (label.Name)
                                            {
                                                case "Female Swimwear Or Underwear":
                                                    imageCheckPass = false;
                                                    break;
                                                default:
                                                    Console.WriteLine("Error : Unknown label for '" + fileName + "' : " + label.Name);
                                                    break;
                                            }
                                            try
                                            {
                                                if (imageCheckPass)
                                                {
                                                    DeleteObjectRequest deleteRequest = new DeleteObjectRequest
                                                    {
                                                        BucketName = "nbc-pending-images",
                                                        Key = fileName
                                                    };
                                                    await client.DeleteObjectAsync(deleteRequest);
                                                }
                                                else
                                                {
                                                    CopyObjectRequest copyRequest = new CopyObjectRequest
                                                    {
                                                        SourceBucket = "nbc-pending-images",
                                                        SourceKey = fileName,
                                                        DestinationBucket = "nbc-quarantined-image",
                                                        DestinationKey = fileName
                                                    };
                                                    await client.CopyObjectAsync(copyRequest);

                                                    DeleteObjectRequest deleteRequest = new DeleteObjectRequest
                                                    {
                                                        BucketName = "nbc-pending-images",
                                                        Key = fileName
                                                    };
                                                    await client.DeleteObjectAsync(deleteRequest);
                                                }
                                            }
                                            catch (Exception error)
                                            {
                                                Console.WriteLine("ERROR : Moving/Deleting Image : " + error.Message);
                                                Console.WriteLine(error.StackTrace);
                                            }

                                        }
                                    }

                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine(e.Message);
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine("Ignoring attachment");
                        }
                    }
                    Console.WriteLine("Num of attachments : {0}", numOfAttachments);


                    emailBody = message.HtmlBody;
                    for (int currentHeader = 0; currentHeader < message.Headers.Count; currentHeader++)
                    {
                        if (message.Headers[currentHeader].Field.ToString().Equals("X-SES-Spam-Verdict") && message.Headers[currentHeader].Value.ToString().Equals("PASS"))
                        {
                            spamCheckPass = true;
                        }
                        if (message.Headers[currentHeader].Field.ToString().Equals("X-SES-Virus-Verdict") && message.Headers[currentHeader].Value.ToString().Equals("PASS"))
                        {
                            virusCheckPass = true;
                        }
                    }
                    if (spamCheckPass && virusCheckPass && imageCheckPass)
                    {
                        emailPassedChecks = true;
                    }
                    else
                    {
                        emailBody = "Failed text";
                        try
                        {
                            GetObjectRequest objectRequest = new GetObjectRequest
                            {
                                BucketName = "norbert.templates",
                                Key = "email-unsafe-rejection.txt"
                            };
                            using (GetObjectResponse objectResponse = await client.GetObjectAsync(objectRequest))
                            using (Stream responseStream = objectResponse.ResponseStream)
                            using (StreamReader reader = new StreamReader(responseStream))
                            {
                                  emailBody = reader.ReadToEnd(); 
                            }
                            if(!virusCheckPass){
                                emailBody = emailBody.Replace("AAA", "a virus");
                            }
                            else if(!spamCheckPass)
                            {
                                emailBody = emailBody.Replace("AAA", "spam");
                            }
                            else if(!imageCheckPass)
                            {
                                emailBody = emailBody.Replace("AAA", "inappropriate content in attachments");
                            }
                            else
                            {
                                emailBody = emailBody.Replace("AAA", "unknown");
                            }
                            emailBody = emailBody.Replace("DDD", name);

                        }
                        catch (AmazonS3Exception e)
                        {
                            Console.WriteLine("ERROR : Reading Email : '{0}' when reading rejection template", e.Message);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("ERROR : An Unknown encountered : '{0}' when reading rejection template", e.Message);
                        }
                    }
                }
            }
            catch (AmazonS3Exception error)
            {
                Console.WriteLine("ERROR : Reading Email : '{0}' when reading email", error.Message);
                Console.WriteLine(error.StackTrace);
            }
            catch (Exception error)
            {
                Console.WriteLine("ERROR : An Unknown encountered : {0}' when reading email", error.Message);
                Console.WriteLine(error.StackTrace);
            }
            return emailPassedChecks;
        }
    }
}
