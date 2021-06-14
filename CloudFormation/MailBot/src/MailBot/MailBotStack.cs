//cdk deploy
using Amazon.CDK;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.S3;

namespace MailBot
{
    public class MailBotStack : Stack
    {
        internal MailBotStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            //Secrets Manager
            //IAM
            //IAM - Role - mailbot
            //IAM - Role - StepFunctions
            //IAM - Role - StepFunctions - CloudWatchFullAccess
            //IAM - Role - StepFunctions - AWS Lambda Role
            //IAM - Role - StepFunctions - TAGS

            Role role = new Role(this, "StepFunctions", new RoleProps
            {
                AssumedBy = new ServicePrincipal("ec2.amazonaws.com"),
                Description = "Role for Step Functions",
            });

            role.AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("CloudWatchFullAccess"));
            //role.AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("AWSLambdaRole"));


            //User user = new User(this, "MyUser", new UserProps { Password = SecretValue.PlainText("1234") });
            //Group group = new Group(this, "MyGroup");

            //Policy policy = new Policy(this, "MyPolicy");
            //policy.AttachToUser(user);
            //group.AttachInlinePolicy(policy);


            ////Register domain
            ////Verify SES

            //Bucket emailsBucket = new Bucket(this, "emails", new BucketProps
            //{
            //});
            //Bucket updatesBucket = new Bucket(this, "updates", new BucketProps
            //{
            //});
            //Bucket attachmentsBucket = new Bucket(this, "attachments", new BucketProps
            //{
            //});
            //Bucket quarantineBucket = new Bucket(this, "quarantine", new BucketProps
            //{
            //});
        }
    }
}
