using Microsoft.ApplicationInsights;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace proxy.App_Start
{
    public class ApiKeyHandler : DelegatingHandler
    {
        TelemetryClient telemetry = new TelemetryClient();

        public ApiKeyHandler()
        {
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!Authorized(request))
            {
                var response = new HttpResponseMessage(HttpStatusCode.Forbidden);
                var tsc = new TaskCompletionSource<HttpResponseMessage>();
                tsc.SetResult(response);
                return tsc.Task;
            }
            return base.SendAsync(request, cancellationToken);
        }


        /// <summary>
        /// Authorize the message based on header content
        /// </summary>
        /// <param name="message"></param>
        /// <returns>true if authorization succeeds, false otherwise</returns>
        private bool Authorized(HttpRequestMessage message)
        {

            // we always want to fail closed. we're not passing back
            // any indication of why failure occured to user as that's info
            // leakage (though maybe timing attack could tell if id is good?).
            // We do log ALL info to trace however, if that's enabled..
            bool result = false;

            try
            {
                if (ValidateId(message.Headers))
                {
                    if (ValidateOTP(message.Headers))
                    {
                        result = true;
                    }
                }
            }
            catch (Exception ex)
            {
                telemetry.TrackTrace("Exception in validation routine " + ex.Message);
                result = false;
            }

            return result;
        }

        /// <summary>
        /// Validate if Id in header is acceptable
        /// </summary>
        /// <param name="headers">HttpHeaders from HttpRequestMessage</param>
        /// <returns>true if acceptable, false otherwise</returns>
        private bool ValidateId(HttpHeaders headers)
        {
            bool result = false;

            try
            {
                IEnumerable<string> headerValues = headers.GetValues("id");
                var incomingId = headerValues.FirstOrDefault();
                telemetry.TrackTrace("ID in header is : " + incomingId); // icky - log corruption could happen? does telemtry class protect us? dunno.

                if (incomingId.Equals(ConfigurationManager.AppSettings["expectedID"])) // in a "real" system, we'd go to a user database to look incomingId's existence
                {
                    telemetry.TrackTrace("ID in header is valid.");
                    result = true;
                }
                else
                {
                    telemetry.TrackTrace("ID in header is not valid.");
                    result = false;
                }
            }
            catch (Exception ex)
            {
                telemetry.TrackTrace("Exception when validating ID in header: " + ex.Message);
                result = false;
            }

            return result;
        }

        /// <summary>
        /// Validate if OTP in header is acceptable. This routine could use some refactoring.
        /// </summary>
        /// <param name="headers">HttpHeaders from HttpRequestMessage</param>
        /// <returns>true if acceptable, false otherwise</returns>
        private bool ValidateOTP(HttpHeaders headers)
        {
            bool result = false;

            // in a "real" system, we'd go to a user database to get a key corresponding with the Id we've already validated
            byte[] secretKey = proxy.Controllers.Utility.ConvertToByteArray(ConfigurationManager.AppSettings["OTPKey"]);

            try
            {
                IEnumerable<string> headerValues = headers.GetValues("key");
                var incomingOTP = headerValues.FirstOrDefault();

                // for the sake of sanity in testing...I allow myself to define a static value for
                // the OTP that will always validate successfully. :/ I'm queasy just writing this comment.
                if (!String.IsNullOrEmpty(ConfigurationManager.AppSettings["expectedOTP"]))
                {
                    if (ConfigurationManager.AppSettings["expectedOTP"].Equals(incomingOTP))
                    {
                        result = true;
                    }
                }
                else
                {
                    // Calculate what the key should be, compare it what we got in the header
                    string expectedOTP = OTP.GetOTP(secretKey);
                    telemetry.TrackTrace("Incoming key is " + incomingOTP + ", Expected key is " + expectedOTP);
                    if (expectedOTP.Equals(incomingOTP))
                    {
                        telemetry.TrackTrace("Key in header is valid.");
                        result = true;
                    }
                    else
                    {
                        expectedOTP = OTP.GetLastOTP(secretKey);
                        telemetry.TrackTrace("Incoming key is " + incomingOTP + ", Prior key is " + expectedOTP);
                        if (expectedOTP.Equals(incomingOTP))
                        {
                            telemetry.TrackTrace("Prior key matches, key considered to be valid.");
                            result = true;
                        }
                        else
                        {
                            expectedOTP = OTP.GetNextOTP(secretKey);
                            telemetry.TrackTrace("Incoming key is " + incomingOTP + ", Next key is " + expectedOTP);
                            if (expectedOTP.Equals(incomingOTP))
                            {
                                telemetry.TrackTrace("Next key matches, key considered to be valid");
                                result = true;
                            }
                            else
                            {
                                telemetry.TrackTrace("Key in header is not valid.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                telemetry.TrackTrace("Exception when validating key in header: " + ex.Message);
                result = false;
            }

            return result;
            // NOTE: In our design, we'd never expect more than one request per 30 second OTP window.
            // Given that, we should never allow a given OTP key to be matched twice within  
            // Now() +/- 30 seconds, assuming the OTP algorithn would never generate a sequence of keys
            // in which a key repeated within a 3-window range. So, we *should* be keeping track and
            // noting what key values have been matched in the last few requests, disallowing any
            // repeated matches. That's just complexity I don't care about for my uses here so not
            // pursuing it. Caring at that level would imply we also care about rate-limiting requests, etc.
            // to defeat brute-force auth attempts. This project is a hobby, not a job. 
        }
    }
}