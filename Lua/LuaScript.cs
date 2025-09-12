using System.Text;
using System.Text.RegularExpressions;
using FluffyVoid.Logging;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Debugging;
using MoonSharp.Interpreter.Loaders;

namespace FluffyVoid.Lua;

/// <summary>
///     Base class for all Lua scripts that can be compiled and managed by the Lua manager
/// </summary>
public abstract class LuaScript
{
    /// <summary>
    ///     Category string for use with the LogManager
    /// </summary>
    private const string LuaCategory = "Lua";
    /// <summary>
    ///     The file path that the Lua script resides at
    /// </summary>
    protected readonly string FilePath;

    /// <summary>
    ///     The name of the lua script
    /// </summary>
    public string Name { get; protected set; }

    /// <summary>
    ///     Application class exposed to the Lua script that holds shared application specific functionality
    /// </summary>
    protected LuaApplication Application { get; }
    /// <summary>
    ///     Debug class exposed to the Lua script that holds debugging functionality
    /// </summary>
    protected Debug Debug { get; }
    /// <summary>
    ///     Lookup table for all events registered from the Lua script
    /// </summary>
    protected Dictionary<string, Closure?> EventTable { get; }
    /// <summary>
    ///     The Moonsharp script object for use with the lua system
    /// </summary>
    protected Script? Lua { get; private set; }

    /// <summary>
    ///     Constructor used to initialize the lua script
    /// </summary>
    /// <param name="filePath">The file path that the Lua script resides at</param>
    protected LuaScript(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            LogManager
                .LogError("Empty or null filepath provided during Lua script creation",
                          LuaCategory);

            FilePath = string.Empty;
            Name = "invalid";
        }
        else
        {
            FilePath = filePath;
            Name = Path.GetFileNameWithoutExtension(FilePath).ToLower();
        }

        Debug = new Debug(Name);
        Application = new LuaApplication();
        Application.RegisterEvent += RegisterEvent;
        EventTable = new Dictionary<string, Closure?>();
    }

    /// <summary>
    ///     Loads the Lua script form file into memory
    /// </summary>
    /// <param name="modulePaths">Collection of module paths to load with the Lua script</param>
    /// <returns>True if the script loaded successfully, otherwise false</returns>
    public bool LoadScript(IEnumerable<string> modulePaths)
    {
        if (!File.Exists(FilePath))
        {
            return false;
        }

        Lua = new Script
        {
            Options =
            {
                ScriptLoader = new FileSystemScriptLoader()
            }
        };

        ((ScriptLoaderBase)Lua.Options.ScriptLoader).ModulePaths =
            modulePaths.ToArray();

        Lua.Globals["Debug"] = Debug;
        Lua.Globals["Application"] = Application;
        LoadGlobals();
        return LoadLuaString();
    }
    /// <summary>
    ///     Attempts to make a call to an event within the desired Lua script
    /// </summary>
    /// <param name="eventName">The name of the event to call</param>
    /// <param name="arguments">Arguments table to pass into the event</param>
    /// <param name="output">Any return value from the lua event</param>
    /// <returns>True if the event was successfully called without errors, otherwise false</returns>
    public bool TryCallEvent(string eventName,
                             Dictionary<string, object> arguments,
                             out DynValue output)
    {
        if (!TryGetEvent(eventName, out Closure? function) || function == null)
        {
            output = DynValue.Nil;
            return false;
        }

        try
        {
            output = function.Call(arguments);
        }
        catch (ScriptRuntimeException ex)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(ex.CallStack[0].Name);
            for (int index = 1; index < ex.CallStack.Count; index++)
            {
                WatchItem item = ex.CallStack[index];
                builder.Append($"::{item.Name}");
            }

            LogManager
                .LogException($"Lua run-time exception occurred while calling {eventName} in {Name}.lua. Error occurred in {builder} at Line # {ex.CallStack[0].Location.FromLine}",
                              LuaCategory, ex: ex);

            output = DynValue.Nil;
            return false;
        }
        catch (Exception ex)
        {
            LogManager
                .LogException($"Unhandled Lua run-time exception while calling {eventName} in {Name}.lua.",
                              LuaCategory, ex: ex);

            output = DynValue.Nil;
            return false;
        }

        return true;
    }
    /// <summary>
    ///     Attempts to make a call to an event within the desired Lua script
    /// </summary>
    /// <param name="eventName">The name of the event to call</param>
    /// <param name="arguments">Arguments table to pass into the event</param>
    /// <returns>True if the event was successfully called without errors, otherwise false</returns>
    public bool TryCallEvent(string eventName,
                             Dictionary<string, object> arguments)
    {
        return TryCallEvent(eventName, arguments, out _);
    }
    /// <summary>
    ///     Attempts to retrieve an event by name that has been registered by the Lua script
    /// </summary>
    /// <param name="eventName">The name of the event to retrieve</param>
    /// <param name="function">The function registered to the event</param>
    /// <returns>True if the event was found with a valid function, otherwise false</returns>
    public bool TryGetEvent(string eventName, out Closure? function)
    {
        if (string.IsNullOrEmpty(eventName))
        {
            function = null;
            return false;
        }

        return EventTable.TryGetValue(eventName, out function) &&
               function != null;
    }
    /// <summary>
    ///     Loads any other Lua.Globals needed for the Lua script to run
    /// </summary>
    protected abstract void LoadGlobals();
    /// <summary>
    ///     Registers a function from the Lua script into the event table
    /// </summary>
    /// <param name="eventName">The name of the event</param>
    /// <param name="function">The function to register to the event</param>
    protected void RegisterEvent(string eventName, DynValue function)
    {
        if (function.Type == DataType.Function)
        {
            EventTable[eventName] = function.Function;
        }
    }

    /// <summary>
    ///     Helper function used to load the file into the Lua script, handling any loading exceptions
    /// </summary>
    /// <returns>True if the Lua was loaded successfully, otherwise false</returns>
    private bool LoadLuaString()
    {
        if (Lua == null)
        {
            LogManager
                .LogError("Core Lua class has not been created. Please call LoadScript to create this object.",
                          LuaCategory);

            return false;
        }

        try
        {
            Lua.DoString(File.ReadAllText(FilePath));
        }
        catch (ArgumentNullException ex)
        {
            LogManager
                .LogException("The path to the Lua script was null or empty, unable to load the Lua script.",
                              LuaCategory, ex: ex);

            return false;
        }
        catch (IOException ex)
        {
            LogManager
                .LogException($"Unable to read the contents of the Lua file at {FilePath}.",
                              LuaCategory, ex: ex);

            return false;
        }
        catch (SyntaxErrorException ex)
        {
            string exceptionInformation = ex.Message;
            if (!string.IsNullOrEmpty(ex.DecoratedMessage))
            {
                Match lineInformation =
                    Regex.Match(ex.DecoratedMessage, @"\((\d+),");

                if (lineInformation is { Success: true, Groups.Count: > 1 })
                {
                    exceptionInformation +=
                        $" around Line #{lineInformation.Groups[1]}";
                }
            }

            LogManager
                .LogException($"Lua syntax error detected while loading the contents of the Lua file -- {exceptionInformation}.",
                              LuaCategory, ex: ex);

            return false;
        }
        catch (Exception ex)
        {
            LogManager
                .LogException("Unhandled exception occurred while loading the contents of the Lua file.",
                              LuaCategory, ex: ex);

            return false;
        }

        return true;
    }
}