﻿using Microsoft.Practices.Unity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using TwitterApi;
using WebSocketApi.Controllers;

namespace WebSocketApi
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Configuración y servicios de API web
            var container = new UnityContainer();
            container.RegisterType<ITwitterApiClient, TwitterApiClient>(new ContainerControlledLifetimeManager(),
                new InjectionFactory(c=>
                {
                TwitterApiClient instance = new TwitterApiClient();
                instance.SetCredentials("jkuG56zlta1exJJ3kGi2mlXRM"
                        , "kPHXBkmLqOV9thDnFE4QJpvzND7hkJBp8AYtwcIts9l64LEmt8"
                        , "430727651-vHPtvToq1UK3RHm3tMrQmQA4BW3PdJlxAopL53We"
                        , "rEArJ1vb8Uuh24WTeh9tW8DKFPNWfEvEFte3jdfUkXaPC");
                    return instance;
                }
        ));
            config.DependencyResolver = new UnityResolver(container);

            // Rutas de API web
            config.MapHttpAttributeRoutes();
            
            //config.Routes.MapHttpRoute(
            //    name: "DefaultApi",
            //    routeTemplate: "api/{controller}/{id}",
            //    defaults: new { id = RouteParameter.Optional }
            //);
        }
      
    }
}
