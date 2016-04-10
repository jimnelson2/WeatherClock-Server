 using proxy.App_Start;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;

namespace proxy
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            config.MessageHandlers.Add(new ApiKeyHandler());

            config.Routes.MapHttpRoute(
                name:"LatLon",
                routeTemplate: "api/{controller}/{latitude}/{longitude}/",
                defaults: new {controller="Forecast"}
            );

            config.Formatters.JsonFormatter.SupportedMediaTypes.Add(new System.Net.Http.Headers.MediaTypeHeaderValue("text/html"));

            // Uncomment the following line of code to enable query support for actions with an IQueryable or IQueryable<T> return type.
            // To avoid processing unexpected or malicious queries, use the validation settings on QueryableAttribute to validate incoming queries.
            // For more information, visit http://go.microsoft.com/fwlink/?LinkId=279712.
            //config.EnableQuerySupport();

            // To disable tracing in your application, please comment out or remove the following line of code
            // For more information, refer to: http://www.asp.net/web-api
            config.EnableSystemDiagnosticsTracing();

        }
    }
}
