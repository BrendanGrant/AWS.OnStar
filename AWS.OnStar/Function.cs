using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Alexa.NET.Response;
using Alexa.NET.Request;
using Alexa.NET.Request.Type;
using Alexa.NET;
using NOnStar;
using NOnStar.CommandAndControl;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AWS.OnStar
{
    public class Function
    {

        private string username = "Your_OnStar_UserName";
        private string password = "Your_OnStar_Password";
        private string pin = "Your_OnStar_Pin";
        public SkillResponse CommandVehical(SkillRequest input, ILambdaContext context)
        {
            SkillResponse skillResponse = null;
            try
            {
                context.Logger.LogLine("Hello from lambda");

                context.Logger.LogLine($"input is null: {input == null}");
                context.Logger.LogLine($"Request type: {input?.GetRequestType()}");

                if ( input?.GetRequestType() == typeof(LaunchRequest))
                {
                    var speech = new PlainTextOutputSpeech() { Text = "Launch request complete" };
                    skillResponse = ResponseBuilder.Tell(speech);
                }
                else
                {
                    var intentRequest = input.Request as IntentRequest;
                    if (intentRequest != null)
                    {
                        context.Logger.LogLine($"ConfirmationStatus: {intentRequest.Intent?.ConfirmationStatus}");
                        context.Logger.LogLine($"Name: {intentRequest.Intent?.Name}");
                        context.Logger.LogLine($"Signature: {intentRequest.Intent?.Signature}");
                        context.Logger.LogLine($"Slots: {intentRequest.Intent?.Slots}");
                    }

                    //Normally we wouldn't hard code creds here, however storing them in the AWS Secret Store would cost money.
                    var c = new OnStarClient(username, password, pin);
                    
                    //Ensures client logs show up in Lambda logs.
                    c.SetupLogging(context.Logger.LogLine);

                    //Amazon already has a stop intent, for consistency, apply a different string for later use
                    if (intentRequest.Intent.Name == "AMAZON.StopIntent")
                    {
                        intentRequest.Intent.Name = "stop";
                    }

                    var taskList = new List<Task>();
                    switch (intentRequest.Intent.Name.ToLower())
                    {
                        case "start":
                            taskList.Add(c.StartVehical());
                            break;
                        case "stop":
                            taskList.Add(c.StopVehical());
                            break;
                        case "lock":
                            taskList.Add(c.LockVehical());
                            break;
                        case "unlock":
                            taskList.Add(c.UnlockVehical());
                            break;
                    }

                    var timeout = context.RemainingTime.Subtract(new TimeSpan(0, 0, 0, 5));
                    Task.WaitAll(taskList.ToArray(), timeout);
                    var convertedTask = (Task<CommandRequestStatus>)taskList[0];
                    if (convertedTask.IsCompletedSuccessfully && convertedTask.Result.Successful)
                    {
                        var speech = new PlainTextOutputSpeech() { Text = $"{intentRequest.Intent.Name} successful" };
                        skillResponse = ResponseBuilder.Tell(speech);
                    }
                    else if (convertedTask.IsFaulted)
                    {
                        var speech = new PlainTextOutputSpeech() { Text = $"Something went wrong {convertedTask.Result.ErrorMessage}" };
                        skillResponse = ResponseBuilder.Tell(speech);
                    }
                }

                context.Logger.LogLine($"RequestType: {input?.Version}");
                context.Logger.LogLine($"RequestType: {input?.Request?.Type}");

                context.Logger.LogLine("Done executing");
            }
            catch(Exception ex)
            {
                context.Logger.LogLine("Failure during execution: " + ex.ToString());
            }

            return skillResponse;
        }
    }
}
