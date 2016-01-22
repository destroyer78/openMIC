﻿//******************************************************************************************************
//  Site.js - Gbtc
//
//  Copyright © 2016, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the MIT License (MIT), the "License"; you may not use this
//  file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://opensource.org/licenses/MIT
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  01/15/2016 - J. Ritchie Carroll
//       Generated original version of source code.
//
//******************************************************************************************************

// Declare page scoped SignalR variables
var dataHub, dataHubClient, serviceHub, serviceHubClient;
var hubIsConnecting = false;
var hubIsConnected = false;

function hideErrorMessage() {
    $("#error-msg-block").hide();
}

function hideInfoMessage() {
    $("#info-msg-block").hide();
}

function showErrorMessage(message, timeout) {
    $("#error-msg-text").html(message);
    $("#error-msg-block").show();

    if (timeout != undefined && timeout > 0)
        setTimeout(hideErrorMessage, timeout);
}

function showInfoMessage(message, timeout) {
    $("#info-msg-text").html(message);
    $("#info-msg-block").show();

    if (timeout === undefined)
        timeout = 3000;

    if (timeout > 0)
        setTimeout(hideInfoMessage, timeout);
}

function hubConnected() {
    hideErrorMessage();

    if (hubIsConnecting)
        showInfoMessage("Reconnected to service.");

    hubIsConnecting = false;
    hubIsConnected = true;

    // Call "onHubConnected" function, if page has defined one
    if (typeof onHubConnected == "function")
        onHubConnected();
}

$(function () {
    $(".page-header").css("margin-bottom", "-5px");

    // Initialize proxy references to the SignalR hubs
    dataHub = $.connection.dataHub.server;
    dataHubClient = $.connection.dataHub.client;
    serviceHub = $.connection.serviceHub.server;
    serviceHubClient = $.connection.serviceHub.client;

    $.connection.hub.reconnecting(function () {
        hubIsConnecting = true;
        showInfoMessage("Attempting to reconnect to service&nbsp;&nbsp;<span class='glyphicon glyphicon-refresh glyphicon-spin'></span>", -1);
    });

    $.connection.hub.reconnected(function () {
        hubConnected();
    });

    $.connection.hub.disconnected(function () {
        hubIsConnected = false;

        if (hubIsConnecting)
            showErrorMessage("Disconnected from server");

        // Call "onHubDisconnected" function, if page has defined one
        if (typeof onHubDisconnected == "function")
            onHubDisconnected();

        setTimeout(function () {
            $.connection.hub.start().done(function () {
                hubConnected();
            });
        }, 5000); // Restart connection after 5 seconds
    });

    // Start the connection
    $.connection.hub.start().done(function () {
        hubConnected();
    });

    $(window).on('beforeunload', function () {
        if (!hubIsConnected || hubIsConnecting)
            return "Service is disconnected, web pages are currently unavailable.";

        return undefined;
    });

    // Enable tool-tips on the page
    $("[data-toggle='tooltip']").tooltip();
});