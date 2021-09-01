# CK.DeviceModel.Configuration package

This small packages reacts to changes in the application's [configuration](https://docs.microsoft.com/en-us/dotnet/core/extensions/configuration)
section named **"CK-DeviceModel"** and can create, reconfigure or destroy devices automatically.

The "CK-DeviceModel" configuration contains:
- A "Daemon" section (optional).

  - A "StoppedBehavior" (optional) that can be "None" (the default), "ClearAllHosts" or "ClearAllHostsAndWaitForDevicesDestroyed".
 
- Multiple sections named with the "[IDeviceHost](../CK.DeviceModel/Host/IDeviceHost.cs)`.DeviceHostName`".

  - Each [DeviceHostConfiguration](../CK.DeviceModel/Host/DeviceHostConfiguration.cs) can be specialized (and can totally control their 
  configuration handling), but by default they have:

    - The optional "IsPartialConfiguration" that defaults to true (this is the safest option).
    - The "Items" section that defines named devices associated to their [DeviceConfiguration](../CK.DeviceModel/DeviceConfiguration.cs).

Below a commented JSON configuration sample:

```jsonc
{
  // The root section must be CK-DeviceModel.
  "CK-DeviceModel": {
    // The Daemon is a special (optional) host.
    "Daemon": {
      // Can be "None" (the default) or ""ClearAllHosts".
      "StoppedBehavior": "ClearAllHostsAndWaitForDevicesDestroyed" 
    },
    // Hosts are found thanks to their name. 
    "FlashBulbHost": {
      // Optionally, hosts can have their own configuration if needed,
      // but by default only the Items (the devices) are handled.
      "Items": {
        // Devices are named objects.
        "FlashBulb n°1":
        {
          "Status": "AlwaysRunning",
          "FlashColor": 45,
          "FlashRate": 100
        },
        "FlashBulb n°2":
        {
          "Status": "RunnableStarted",
          "ControllerKey": "WebAPI"
          "FlashColor": 12,
        }
      }
    },
    "AnotherHost": {
      "IsPartialConfiguration": false, // Only D1 and D2 devices will exist.
                                       // Others will be destroyed: this configuration
                                       // fully drives the AnotherHost's devices.
      "Items": {
        "D1":
        {
          "Status": "Disabled",
          "Powers": [45, 23, 17]
        },
        "D2":
        {
          "Status": "Runnable",
          "ControllerKey": "WebAPI-Only", // Commands with any other controller key
                                          // will be ignored by default.
          "BaseImmediateCommandLimit": 1  // Defaults to 10. Here we want to execute
                                          // one regular command after each immediate one.
        }
      }
    }
  }
}
```


