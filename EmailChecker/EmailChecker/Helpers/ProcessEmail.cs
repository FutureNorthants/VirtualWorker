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
        private static readonly RegionEndpoint primaryRegion = RegionEndpoint.EUWest2;
        private static IAmazonS3 s3EmailsClient;
        private static IAmazonS3 s3TemplatesClient;
        private static IAmazonS3 s3Client;
        public static Boolean emailPassedChecks { get; set; } = false;
        public static Boolean spamCheckPass { get; set; } = false;
        public static Boolean virusCheckPass { get; set; } = false;
        public static Boolean imageCheckPass { get; set; } = false;
        public String emailTo { get; set; } = null;
        public String subject { get; set; } = null;
        public String name { get; set; } = null;
        public String emailBody { get; set; } = null;
        public Boolean west = true;

        public ProcessEmail()
        {
        }

        public Boolean Process(String bucketName, String keyName, float confidence, String pendingimagesbucket, String quarantinedimagesbucket, String templatesBucket, RegionEndpoint s3EmailsRegion, RegionEndpoint s3TemplatesRegion)
        {
            Console.WriteLine("Pending Images Bucket : " + pendingimagesbucket);
            Console.WriteLine("Quarantined Images Bucket : " + quarantinedimagesbucket);
            Console.WriteLine("Templates Bucket : " + templatesBucket);
            s3EmailsClient = new AmazonS3Client(s3EmailsRegion);
            s3TemplatesClient = new AmazonS3Client(s3TemplatesRegion);
            s3Client = new AmazonS3Client(primaryRegion);
            return ReadObjectDataAsync(bucketName, keyName, confidence, pendingimagesbucket, quarantinedimagesbucket, templatesBucket).Result;
        }

        private async Task<Boolean> ReadObjectDataAsync(String bucketName, String keyName, float confidence, String pendingimagesbucket, String quarantinedimagesbucket, String templatesBucket)
        {
            try
            {
                GetObjectRequest request = new GetObjectRequest
                {
                    BucketName = bucketName,
                    Key = keyName
                };
                using (GetObjectResponse response = await s3EmailsClient.GetObjectAsync(request))
                {
                    MimeMessage message = MimeMessage.Load(response.ResponseStream);
                    MailAddressCollection mailAddresses = (MailAddressCollection)message.From;
                    MailAddressCollection sendToAddresses = (MailAddressCollection)message.To;
                    emailTo = mailAddresses[0].Address;
                    if (sendToAddresses[0].ToString().ToLower().Contains("northnorthants"))
                    {
                        west = false;
                        Console.WriteLine("Processing email for North Northants from : " + emailTo);
                    }
                    else
                    {
                        Console.WriteLine("Processing email for West Northants from  : " + emailTo);
                    }
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
                                            BucketName = pendingimagesbucket,
                                            Key = fileName,
                                        };
                                        putRequest.Headers.ContentLength = attachmentLength;
                                        await s3Client.PutObjectAsync(putRequest);
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
                                            Bucket = pendingimagesbucket
                                        },
                                    },
                                    MinConfidence = 60F
                                };

                                try
                                {
                                    DetectModerationLabelsResponse detectModerationLabelsResponse = await rekognitionClient.DetectModerationLabelsAsync(detectModerationLabelsRequest);
                                    Console.WriteLine("Detected labels");
                                    foreach (ModerationLabel label in detectModerationLabelsResponse.ModerationLabels)
                                    {
                                        if (!String.IsNullOrEmpty(label.ParentName))
                                        {
                                            Console.WriteLine("Found - Label: {0}\n Confidence: {1}\n Parent: {2}",
                                            label.Name, label.Confidence, label.ParentName);
                                            if (label.Confidence > confidence)
                                            {
                                                Console.WriteLine("Rejected - Label: {0}\n Confidence: {1}", label.Name, label.Confidence);
                                                imageCheckPass = false;
                                            }
                                        }
                                    }
                                           
                                    try
                                    {
                                        if (imageCheckPass)
                                        {
                                            DeleteObjectRequest deleteRequest = new DeleteObjectRequest
                                            {
                                                BucketName = pendingimagesbucket,
                                                Key = fileName
                                            };
                                            await s3Client.DeleteObjectAsync(deleteRequest);
                                        }
                                        else
                                        {
                                            Console.WriteLine("Image Rejection Started");
                                            try
                                            {
                                                Console.WriteLine("Clearing : Deleting " + fileName + " from : " + quarantinedimagesbucket);
                                                DeleteObjectRequest clearRequest = new DeleteObjectRequest
                                                {
                                                    BucketName = quarantinedimagesbucket,
                                                    Key = fileName
                                                };
                                                await s3Client.DeleteObjectAsync(clearRequest);
                                            }
                                            catch(Exception){}
                                            Console.WriteLine("Quarantining : Copying " + fileName + " to : " + quarantinedimagesbucket);
                                            CopyObjectRequest copyRequest = new CopyObjectRequest
                                            {
                                                SourceBucket = pendingimagesbucket,
                                                SourceKey = fileName,
                                                DestinationBucket = quarantinedimagesbucket,
                                                DestinationKey = fileName
                                            };
                                            await s3Client.CopyObjectAsync(copyRequest);
                                            Console.WriteLine("Deleting : Deleting " + fileName + " from : " + pendingimagesbucket);
                                            DeleteObjectRequest deleteRequest = new DeleteObjectRequest
                                            {
                                                BucketName = pendingimagesbucket,
                                                Key = fileName
                                            };
                                            await s3Client.DeleteObjectAsync(deleteRequest);
                                            Console.WriteLine("Deleting : Deleting " + fileName + " from : " + pendingimagesbucket);
                                            Console.WriteLine("Image Rejection Ended");
                                        }
                                    }
                                    catch (Exception error)
                                    {
                                        Console.WriteLine("ERROR : Moving/Deleting Image : " + error.Message);
                                        Console.WriteLine(error.StackTrace);
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
                                BucketName = templatesBucket,
                                Key = "email-unsafe-rejection.txt"
                            };
                            using (GetObjectResponse objectResponse = await s3TemplatesClient.GetObjectAsync(objectRequest))
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
