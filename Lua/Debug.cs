using FluffyVoid.Logging;
using MoonSharp.Interpreter;

namespace FluffyVoid.Lua;

/// <summary>
///     Debug class that is exposed to the lua scripts by default, allowing debug tools for lua script debugging
/// </summary>
[MoonSharpUserData]
[Serializable]
public class Debug
{
    /// <summary>
    ///     Color code for Dark Magenta
    /// </summary>
    private const string DarkMagenta = "8b008b";
    /// <summary>
    ///     The name of the lua script
    /// </summary>
    [MoonSharpHidden]
    public string Name { get; protected set; }

    /// <summary>
    ///     Category string for use with the LogManager
    /// </summary>
    [MoonSharpHidden]
    protected string LuaScriptCategory => $"{Name}.lua";

    /// <summary>
    ///     Constructor used to initialize the Debug object
    /// </summary>
    /// <param name="name">The name of the lua script that is initializing the debug object</param>
    [MoonSharpHidden]
    internal Debug(string name)
    {
        Name = name;
    }

    /// <summary>
    ///     Logs a lua message to the LogManager
    /// </summary>
    /// <param name="logMessage">The message to log out</param>
    public void Log(string logMessage)
    {
        LogManager.Log(logMessage, LuaScriptCategory, DarkMagenta);
    }
    /// <summary>
    ///     Logs an error message to the LogManager
    /// </summary>
    /// <param name="logMessage">The message to log out</param>
    public void LogError(string logMessage)
    {
        LogManager.LogError(logMessage, LuaScriptCategory);
    }
    /// <summary>
    ///     Logs a warning message to the LogManager
    /// </summary>
    /// <param name="logMessage">The message to log out</param>
    public void LogWarning(string logMessage)
    {
        LogManager.LogWarning(logMessage, LuaScriptCategory);
    }
}