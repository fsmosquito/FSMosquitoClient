# FSMosquitoClient

Client application that connects to FS2020 via SimConnect and relays data to the FSMosquito Server via MQTT

![Build FSMosquito Client](https://github.com/fsmosquito/FSMosquitoClient/workflows/Build%20FSMosquito%20Client/badge.svg)

## Getting Started

1. Download the latest FSMosquito Client release on the releases tab and unzip into a folder of your choosing.
2. Register for an account at www.fsmosquito.com - Currently in Alpha
3. After email verification, once logged in, obtain the authentication token from the 'account' page (UI/XP needs to be improved)
4. Within the folder that you unzipped the FSMosquito Client, with a text editor such as notepad, edit the appsettings.json and place your username and the authentication token in the appropriate fields and save the file.
5. Launch the client by 2x clicking FSMosquitoClient.exe
6. Start FS2020 and Fly!

## Developing

Prerequisites (For just the client):
 - FS2020
 - Git
 - Visual Studio 2019 (or other IDE that supports .net core development)

Pretty straight forward, this is a .Net Core 3.1 application using Windows Forms.

The necessary FS2020 SDK files are included in the /lib folder (these will probably need to be updated over time as FS2020 is updated)

I'm using VS2019 for development, but there shouldn't be anything that prevents VSCode from being the IDE of choice during the
development process except for it's annoyances with C#. Maybe JetBrains Rider is a good alternative that doesn't require a full
VS2019 license.

With VS2019, just open the .sln file and run.

There are four main classes involved:

 - FsSimConnect - Basically a wrapper around SimConnect that makes it easier to be controlled
 - FsMqtt - Provides the communication layer to a MQTT broker
 - SimConnectMqttAdapter - Performs the functional adaptation of the comms between SimConnect and Mqtt.
 - MainForm - Provides a minimalistic UI - and importantly a WinHandle that SimConnect posts messages to.

 ### Design
 
 How SimConnect works is that it uses [Windows Procedures](https://docs.microsoft.com/en-us/windows/win32/winmsg/using-window-procedures) to communicate
 with applications out-of-process
 
 Within the SimConnect constructor, one passes the handle of the window that should be notified by FS2020 when a message is available.
 
 This mechanism happens within Forms/MainForm.cs class with the WndProc override.

 When that message is recieved by the MainForm, it raises an event which is handled by the SimConnectMqttAdapter which then requests the actual data associated with the message from SimConnect.

The adapter then then serializes this data into a MQTT message and publishes it to the broker.

The metrics that are transmitted are very much like individual data points that an IoT device would be relaying to an IoT edge server which can be analyzed. In FSMosquito's case this data is subscribed by various server components which notify the web client of position updates and react to events such as taking off and landing at airports.

 --- 
 
 SimConnect requires a client app to register 'subscriptions' to various events that occur in FS2020 prior to having those events be
 raised by the instance of a SimConnect class. The FSMosquitoClient doesn't statically register these events, but subscribes to requests
 for registration events coming from the MQTT broker (FSMosquitoServer) and then correspondingly registers the event with SimConnect.

 This allows for the data points that are utilized by the FSMosquitoServer to change and grow over time. Effectively, the FSMosquitoClient
 is just a "dumb pipe" that facilitates communication between FS2020 and the FSMosquitoServer.

 ---

 The client currently also accepts SimVars to be set via MQTT messages as well, this opens up some interesting possibilities for automation.

 TODO: System variables, invoking various SimConnect functions

### Logs

Currently the FSMosquitoClient creates log files located in the ./logs folder relative to where the FSMosquitoClient.exe is located.

## Releasing

This project contains a publishing profile that will bundle the application (including all .net dependencies) into a single file.

 - In VS2019, right click the FSMosquitoClient project and select "Publish".
 - Choose the 'Create FSMosquitoClient Release' publishing profile
 - Press the 'Publish' button. A new single-file build will be located in $(ProjectLocation)\bin\x64\Release\netcoreapp3.1\win-x64\publish


 A GitHub action is included that will automatically build and publish new releases to GitHub

  - On Push to Master or on PR - Builds the project and stores the result as an artifact (except for changes to README.md and /docs/*)
  - On tag that starts with 'v' (ex: ```git tag -a v0.2.0-alpha01 -m "Release Description" && git push --tags```) Builds the project and creates a release that corresponds to the tag.


  ## Resources

  https://csharp.hotexamples.com/examples/-/SimConnect/RequestDataOnSimObject/php-simconnect-requestdataonsimobject-method-examples.html
