A good name would be `AppConfig`, `SettingsProvider`, or `RuntimeSettings`.

My vote: **`AppConfig`**. It reads clearly at call sites:

```csharp
AppConfig.GetEnvironmentVariable("SomeVariable");
AppConfig.GetAppSetting("SomeSetting");
```

A couple of important notes first:

* **Environment variables** can be read and written at runtime.
* **`AppSettings`** can be read easily, but writing back to `App.config` / `web.config` is more limited and depends on your app type.
* In C#, `ConfigurationManager.AppSettings` uses **square brackets**, not parentheses:

```csharp
ConfigurationManager.AppSettings["SomeSetting"]
```

## User vs System environment variables

That part is built into C# already through `EnvironmentVariableTarget`:

* `Process` = current process only
* `User` = current Windows user
* `Machine` = system-wide

Examples:

```csharp
Environment.GetEnvironmentVariable("SomeVariable", EnvironmentVariableTarget.User);
Environment.GetEnvironmentVariable("SomeVariable", EnvironmentVariableTarget.Machine);
Environment.SetEnvironmentVariable("SomeVariable", "abc", EnvironmentVariableTarget.User);
Environment.SetEnvironmentVariable("SomeVariable", "abc", EnvironmentVariableTarget.Machine);
```

So that is how you distinguish between user and system variables.

## Suggested class

Here is a practical version for a .NET Framework style app using `ConfigurationManager`:

```csharp
using System;
using System.Collections.Generic;
using System.Configuration;

public static class AppConfig
{
    private static readonly Dictionary<string, string> DefaultEnvironmentVariables = new()
    {
        { "SomeVariable", "DefaultEnvValue" },
        { "AnotherVariable", "AnotherDefault" }
    };

    private static readonly Dictionary<string, string> DefaultAppSettings = new()
    {
        { "SomeSetting", "DefaultSettingValue" },
        { "AnotherSetting", "AnotherDefaultSetting" }
    };

    public static string GetEnvironmentVariable(
        string name,
        EnvironmentVariableTarget target = EnvironmentVariableTarget.Process,
        bool createIfMissing = false)
    {
        var value = Environment.GetEnvironmentVariable(name, target);

        if (!string.IsNullOrWhiteSpace(value))
            return value;

        if (createIfMissing && DefaultEnvironmentVariables.TryGetValue(name, out var defaultValue))
        {
            Environment.SetEnvironmentVariable(name, defaultValue, target);
            return defaultValue;
        }

        return string.Empty;
    }

    public static void SetEnvironmentVariable(
        string name,
        string value,
        EnvironmentVariableTarget target = EnvironmentVariableTarget.Process)
    {
        Environment.SetEnvironmentVariable(name, value, target);
    }

    public static string GetAppSetting(string key, bool createIfMissing = false)
    {
        var value = ConfigurationManager.AppSettings[key];

        if (!string.IsNullOrWhiteSpace(value))
            return value;

        if (createIfMissing && DefaultAppSettings.TryGetValue(key, out var defaultValue))
        {
            SetAppSetting(key, defaultValue);
            return defaultValue;
        }

        return string.Empty;
    }

    public static void SetAppSetting(string key, string value)
    {
        var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

        if (config.AppSettings.Settings[key] == null)
        {
            config.AppSettings.Settings.Add(key, value);
        }
        else
        {
            config.AppSettings.Settings[key].Value = value;
        }

        config.Save(ConfigurationSaveMode.Modified);
        ConfigurationManager.RefreshSection("appSettings");
    }
}
```

## Usage

```csharp
string env1 = AppConfig.GetEnvironmentVariable("SomeVariable");
string env2 = AppConfig.GetEnvironmentVariable("SomeVariable", EnvironmentVariableTarget.User);
string env3 = AppConfig.GetEnvironmentVariable("SomeVariable", EnvironmentVariableTarget.Machine, createIfMissing: true);

string setting1 = AppConfig.GetAppSetting("SomeSetting");
string setting2 = AppConfig.GetAppSetting("SomeSetting", createIfMissing: true);

AppConfig.SetEnvironmentVariable("SomeVariable", "Hello", EnvironmentVariableTarget.User);
AppConfig.SetAppSetting("SomeSetting", "World");
```

## A few real-world cautions

### 1. Writing machine-level environment variables

This usually requires elevated permissions.

```csharp
EnvironmentVariableTarget.Machine
```

If the app is not running as admin, this may fail.

### 2. Writing `AppSettings`

This works best in classic desktop/console apps with `App.config`.

It is not something I would rely on heavily for:

* ASP.NET apps
* locked-down production servers
* modern `.NET` apps using `appsettings.json`

### 3. Constructor-based defaults

Since this kind of class is usually static, a constructor is not the best fit unless you want an instance-based service.

If you do want constructor-based defaults, then this is a better shape:

```csharp
public class AppConfigService
{
    private readonly Dictionary<string, string> _defaultEnv;
    private readonly Dictionary<string, string> _defaultSettings;

    public AppConfigService(
        Dictionary<string, string> defaultEnv,
        Dictionary<string, string> defaultSettings)
    {
        _defaultEnv = defaultEnv;
        _defaultSettings = defaultSettings;
    }

    // methods here...
}
```

That is cleaner if you want different defaults in different environments.

## My recommendation

For your case, I would do this:

* Use a class named **`AppConfig`**
* Make it **static**
* Add:

  * `GetEnvironmentVariable`
  * `SetEnvironmentVariable`
  * `GetAppSetting`
  * `SetAppSetting`
* Support `EnvironmentVariableTarget` so you can choose:

  * `User`
  * `Machine`
  * `Process`
* Keep defaults in dictionaries inside the class unless you truly need instance-based behavior

## Even better API shape

I would slightly improve your names to this:

```csharp
AppConfig.GetEnv("SomeVariable");
AppConfig.GetSetting("SomeSetting");
AppConfig.SetEnv("SomeVariable", "abc", EnvironmentVariableTarget.User);
AppConfig.SetSetting("SomeSetting", "xyz");
```

That reads nicely and keeps the calls short.

If you want, I can give you a polished version with:

* typed helpers like `GetInt`, `GetBool`
* fallback order like `Env first, AppSettings second`
* logging when defaults are auto-created
