// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

'use strict';

var fs = require('fs');
var Protocol = require('azure-iot-device-mqtt').Mqtt;
var MqttBase = require('./dist/x509_hsm_mqtt_base.js').X509HsmMqttBase;
var X509AuthenticationProvider = require('azure-iot-device').X509AuthenticationProvider;
var Client = require('azure-iot-device').Client;
var Message = require('azure-iot-device').Message;

var iotHubHostname = process.env.IOT_HUB_HOSTNAME;
var deviceId = process.env.DEVICE_ID;
var certFile = process.env.PATH_TO_CERTIFICATE_FILE;
var keyIdentifier = process.env.KEY_IDENTIFIER;

var authProvider = new X509AuthenticationProvider({
  host: iotHubHostname,
  deviceId: deviceId
});

var client = Client.fromAuthenticationProvider(authProvider, function (authProvider) {
  return new Protocol(authProvider, new MqttBase());
});

var connectCallback = function (err) {
  if (err) {
    console.error('Could not connect: ' + err.message);
  } else {
    console.log('Client connected');
    client.on('message', function (msg) {
      console.log('Id: ' + msg.messageId + ' Body: ' + msg.data);
      // When using MQTT the following line is a no-op.
      client.complete(msg, printResultFor('completed'));
    });

    // Create a message and send it to the IoT Hub every second
    var sendInterval = setInterval(function () {
      var windSpeed = 10 + (Math.random() * 4); // range: [10, 14]
      var temperature = 20 + (Math.random() * 10); // range: [20, 30]
      var humidity = 60 + (Math.random() * 20); // range: [60, 80]
      var data = JSON.stringify({ deviceId: deviceId, windSpeed: windSpeed, temperature: temperature, humidity: humidity });
      var message = new Message(data);
      message.properties.add('temperatureAlert', (temperature > 28) ? 'true' : 'false');
      console.log('Sending message: ' + message.getData());
      client.sendEvent(message, printResultFor('send'));
    }, 2000);

    client.on('error', function (err) {
      console.error(err.message);
    });

    client.on('disconnect', function () {
      clearInterval(sendInterval);
      client.removeAllListeners();
      client.open(connectCallback);
    });
  }
};

var options = {
  cert: fs.readFileSync(certFile, 'utf-8').toString(),
  privateKeyEngine: "pkcs11",
  privateKeyIdentifier: keyIdentifier
};

client.setOptions(options);
client.open(connectCallback);

// Helper function to print results in the console
function printResultFor(op) {
  return function printResult(err, res) {
    if (err) console.log(op + ' error: ' + err.toString());
    if (res) console.log(op + ' status: ' + res.constructor.name);
  };
}
