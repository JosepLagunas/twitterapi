﻿/**
 * Created by Josep on 18/12/14.
 */
/**
 * 	@author: Josep Lagunas
 *  @date: 02/04/2014
 */

(function () {

    var clientApi = (function () {

        var instance = "";

        function init() {

            //Initialize baseURL for serverConnector
            serverConnector.setBaseURL("http://localhost:51500");
            //Singleton

            //Private Methods and Variables goes here

            var connection;

            return {

                requestWSToken: function (isAsync, getAsJson, successCallback, errorCallback,
                    beforeSendCallback, completedCallback) {
                    var settings = {
                        verb: "GET",
                        async: isAsync,
                        getAsJon: getAsJson,
                        method: "api/request-ws-token",
                        pathParameters: [],
                        callback: successCallback,
                        errorCallback: errorCallback,
                        beforeSendCallback: beforeSendCallback,
                        completedCallback: completedCallback
                    };
                    
                    serverConnector.getInstance().callRESTServerMethod(settings);
                },
                openWebSocketConnection: function (clientId, wsToken) {
                    /*
                    var settings = {
                        verb: "GET",
                        async: false,
                        getAsJon: getAsJson,
                        method: "api/connect-websocket",
                        headers: { 'clientid': clientId, 'wstoken': wsToken }
                    };

                    serverConnector.getInstance().callRESTServerMethod(settings);
                    */
                    var settings = {
                        verb: "GET",
                        method: "api/connect-websocket"                      
                    };
                    
                    return serverConnector.getInstance().callWebSocketMethod(settings);
                }
            };
        };

        return {

            getInstance: function () {

                if (!instance) {
                    instance = init();
                }

                return instance;
            }
        };

    })();

    window["clientApi"] = clientApi;

})();


