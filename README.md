# AlwaysTooLate.Logging

AlwaysTooLate Logging module, a simple, customizable, one-class, thread-safe logging solution, that supports enabling/disabling logging, backups, compression of the older log files and many other functionalities.

# Installation

Before installing this module, be sure to have installed these:

- [AlwaysTooLate.Core](https://github.com/AlwaysTooLate/AlwaysTooLate.Core)

Open your target project in Unity and use the Unity Package Manager (`Window` -> `Package Manager` -> `+` -> `Add package from git URL`) and paste the following URL:
https://github.com/AlwaysTooLate/AlwaysTooLate.Logging.git

# Setup

After succesfull installation, open a scene that is loaded first when starting your game (we recommend having an entry scene called Main that is only used for initializing core systems and utilities, which then loads the next scene, that is supposed to start the game - like a Main Menu). In that scene, create an empty GameObject and attach the LoggingManager component to it. You can now use the Inspector window values to configure the LoggingManager to your needs (hover your mouse pointer over the value for a tooltip to appear). It is also recommended to disable the `Use Player Log` option in `Player Settings` of your project.

# Basic Usage

For logging, just use the standard `UnityEngine.Debug.Log` method, LoggingManager will handle stuff in the background.

# Contribution

We do accept PR. Feel free to contribute. If you want to find us, look for the ATL staff on the official [AlwaysTooLate Discord Server](https://discord.alwaystoolate.com/)

*AlwaysTooLate.Logging (c) 2018-2020 Always Too Late.*
