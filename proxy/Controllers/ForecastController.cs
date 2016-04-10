using ForecastIO;
using Microsoft.ApplicationInsights;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Web.Http;

namespace proxy.Controllers
{
    public class ForecastController : ApiController
    {

        private double[] intensityBins = new double[5] {
                                                        0.010,
                                                        0.075,
                                                        0.150,
                                                        0.300,
                                                        0.750
                                                       };

        public string Get(Double latitude, Double longitude)
        {
            TelemetryClient telemetry = new TelemetryClient();

            string returnVal = "";

            if (latitude<-90 || latitude>90 || longitude<-180 || longitude>180)
            {
                telemetry.TrackTrace("Failed geo location validation");
                return ""; // not a very friendly error, #TODO make errors nicer
            }

            try
            {

                // to help with testing, we're going to make it possible to override
                // the incoming lat/lon with one of our choosing.
                if (!String.IsNullOrEmpty(ConfigurationManager.AppSettings["latitude"]))
                {
                    latitude = Double.Parse(ConfigurationManager.AppSettings["latitude"]);
                }
                if (!String.IsNullOrEmpty(ConfigurationManager.AppSettings["longitude"]))
                {
                    longitude = Double.Parse(ConfigurationManager.AppSettings["longitude"]);
                }

                // make the call to our weather provider
                string apiKey = ConfigurationManager.AppSettings["ForecastIO.apikey"];
                var request = new ForecastIORequest(apiKey, (float)latitude, (float)longitude, Unit.us);
                var response = request.Get();

                // the api response is HUGE, we're only interested in 
                // the "minutely" data. The forecast.io api doco
                // says this data may not always be returned :( I have not addressed that case yet
                returnVal = convertToBinnedData(response.minutely.data);            

            }
            catch (Exception ex)
            {
                telemetry.TrackTrace("Exception during forecast.io call: " + ex.Message);
                return ""; // lazy with problems again :(
            }

            telemetry.TrackTrace("value returned to caller: " + returnVal);
            return returnVal;

        }

        /// <summary>
        /// Given the Minutely portion of the forecast.io response,
        /// return a string of hex values mapping to the appropriate
        /// precipitation type and intensity.
        /// </summary>
        /// <param name="obj">Forecast.io Minutely data</param>
        /// <returns></returns>
        private string convertToBinnedData(List<MinuteForecast> obj)
        {
            string binnedData = "";
            int multiplier=0;

            for (int i = 0; i < 60; i++)
            {
                switch (obj[i].precipType)
                {
                    case "rain":
                        multiplier = 1;
                        break;
                    case "snow":
                        multiplier = 2;
                        break;
                    case "sleet":
                    case "hail":
                        multiplier = 3;
                        break;
                    default:
                        break;
                }
                binnedData = binnedData + String.Format("{0:X}", (multiplier * getBin(obj[i].precipIntensity)));
            }

            return binnedData;

        }


        /// <summary>
        /// Given an intensity value, return what bin number the intensity falls into
        /// </summary>
        /// <param name="intensity"></param>
        /// <returns>bin number</returns>
        private int getBin(double intensity)
        {
            int bin = 0;

            while (bin < intensityBins.Length && intensity >= intensityBins[bin])
            {
                bin++;
            }
                
            return bin;
        }
    }
}
