using System.Reflection;
using FluffyVoid.Logging;
using MoonSharp.Interpreter;

namespace FluffyVoid.Lua;

/// <summary>
///     Lua manager that can load and manage Lua files at run-time
/// </summary>
/// <typeparam name="TLuaScript">The type of Lua script to manage</typeparam>
public abstract class LuaManager<TLuaScript>
    where TLuaScript : LuaScript
{
    /// <summary>
    ///     Category string for use with the LogManager
    /// </summary>
    private const string LuaCategory = "Lua";
    /// <summary>
    ///     Color code for Yellow
    /// </summary>
    private const string Yellow = "FFFF00";

    /// <summary>
    ///     List of paths to use for finding required modules that are needed for the loaded lua scripts to compile
    /// </summary>
    protected readonly HashSet<string> ModulePaths;
    /// <summary>
    ///     The root directory to find all lua scripts to load
    /// </summary>
    protected readonly string RootDirectory;

    /// <summary>
    ///     Predefined empty arguments table that can be reused to ensure arguments are always non-null
    /// </summary>
    protected Dictionary<string, object> EmptyArguments { get; }
    /// <summary>
    ///     Lookup table of lua scripts that have currently been loaded
    /// </summary>
    protected Dictionary<string, TLuaScript> ScriptsTable { get; }

    /// <summary>
    ///     Event used to notify listeners that a new script has been loaded into memory
    /// </summary>
    public event Action<TLuaScript>? ScriptLoaded;
    /// <summary>
    ///     Event used to notify listeners that a script has been unloaded from memory
    /// </summary>
    public event Action<TLuaScript>? ScriptUnloaded;

    /// <summary>
    ///     Constructor used to build the proper full filepath
    /// </summary>
    /// <param name="rootDirectory">The full root directory path to load lua files from</param>
    /// <param name="initializeDependencies">
    ///     Initializes any other Lua dependencies that need to be injected into the Lua
    ///     framework
    /// </param>
    protected LuaManager(string rootDirectory,
                         Action? initializeDependencies = null)
    {
        RootDirectory = rootDirectory;
        ScriptsTable = new Dictionary<string, TLuaScript>();
        EmptyArguments = new Dictionary<string, object>();
        ModulePaths = new HashSet<string>();
        UserData.RegisterType<Debug>();
        UserData.RegisterType<LuaApplication>();
        UserData.RegisterAssembly(Assembly.GetEntryAssembly());
        initializeDependencies?.Invoke();
    }

    /// <summary>
    ///     Loads a Lua script by name from file to be stored within the manager
    /// </summary>
    /// <param name="scriptName">The name of the lua script to load</param>
    public void LoadScript(string scriptName)
    {
        if (string.IsNullOrEmpty(scriptName))
        {
            return;
        }

        scriptName = scriptName.ToLower();
        if (ScriptsTable.TryGetValue(scriptName, out TLuaScript? script))
        {
            LogManager.LogWarning($"{scriptName} is already loaded!",
                                  LuaCategory);

            return;
        }

        PopulateModulePaths();
        if (TryFindLuaFile(RootDirectory, scriptName,
                           out string fullFilePath) &&
            TryParseLuaFile(fullFilePath, out script) && script != null)
        {
            ScriptsTable.Add(scriptName, script);
            ScriptLoaded?.Invoke(script);
        }
        else
        {
            LogManager
                .LogInfo($"{scriptName}.lua was not found within {Path.GetFullPath(RootDirectory)} or any of its sub folders",
                         LuaCategory);
        }
    }
    /// <summary>
    ///     Loads and initializes all the Lua files found within the directories into Lua Scripts
    /// </summary>
    public virtual void LoadScripts()
    {
        LoadLuaFiles();
    }
    /// <summary>
    ///     Reloads a Lua script to allow making updates to the Lua script at run-time
    /// </summary>
    /// <param name="scriptName">The name of the lua script to reload</param>
    public virtual void ReloadScript(string scriptName)
    {
        UnloadScript(scriptName);
        LoadScript(scriptName);
    }
    /// <summary>
    ///     Clears out the currently loaded lua scripts and reloads all from the default directory of the manager
    /// </summary>
    public virtual void ReloadScripts()
    {
        foreach (TLuaScript script in ScriptsTable.Values)
        {
            ScriptUnloaded?.Invoke(script);
        }

        ScriptsTable.Clear();
        LoadLuaFiles();
    }
    /// <summary>
    ///     Attempts to make a call to an event within the desired Lua script
    /// </summary>
    /// <param name="scriptName">The name of the Lua script to call into</param>
    /// <param name="eventName">The name of the event to call</param>
    /// <param name="arguments">Arguments table to pass into the event</param>
    /// <param name="output">Any return value from the lua event</param>
    /// <returns>True if the event was successfully called without errors, otherwise false</returns>
    public bool TryCallEvent(string scriptName, string eventName,
                             Dictionary<string, object>? arguments,
                             out DynValue output)
    {
        if (string.IsNullOrEmpty(scriptName))
        {
            output = DynValue.Nil;
            return false;
        }

        scriptName = scriptName.ToLower();
        if (!TryRetrieveScript(scriptName, out TLuaScript? script) ||
            script == null)
        {
            output = DynValue.Nil;
            return false;
        }

        if (arguments == null)
        {
            EmptyArguments.Clear();
            arguments = EmptyArguments;
        }

        return script.TryCallEvent(eventName,
                                   PrepareArgumentsTable(script, arguments),
                                   out output);
    }
    /// <summary>
    ///     Attempts to make a call to an event within the desired Lua script
    /// </summary>
    /// <param name="scriptName">The name of the Lua script to call into</param>
    /// <param name="eventName">The name of the event to call</param>
    /// <param name="output">Any return value from the lua event</param>
    /// <returns>True if the event was successfully called without errors, otherwise false</returns>
    public bool TryCallEvent(string scriptName, string eventName,
                             out DynValue output)
    {
        EmptyArguments.Clear();
        return TryCallEvent(scriptName, eventName, EmptyArguments, out output);
    }
    /// <summary>
    ///     Attempts to make a call to an event within the desired Lua script
    /// </summary>
    /// <param name="scriptName">The name of the Lua script to call into</param>
    /// <param name="eventName">The name of the event to call</param>
    /// <param name="arguments">Arguments table to pass into the event</param>
    /// <returns>True if the event was successfully called without errors, otherwise false</returns>
    public bool TryCallEvent(string scriptName, string eventName,
                             Dictionary<string, object> arguments)
    {
        return TryCallEvent(scriptName, eventName, arguments, out _);
    }
    /// <summary>
    ///     Attempts to make a call to an event within the desired Lua script
    /// </summary>
    /// <param name="scriptName">The name of the Lua script to call into</param>
    /// <param name="eventName">The name of the event to call</param>
    /// <returns>True if the event was successfully called without errors, otherwise false</returns>
    public bool TryCallEvent(string scriptName, string eventName)
    {
        EmptyArguments.Clear();
        return TryCallEvent(scriptName, eventName, EmptyArguments, out _);
    }
    /// <summary>
    ///     Attempts to get a Lua script by name and cast to the desired script type
    /// </summary>
    /// <param name="scriptName">The name of the Lua script to call into</param>
    /// <param name="script">The desired Lua script if found and of the type desired</param>
    /// <typeparam name="TCustomLua">The type of Lua script to attempt to retrieve from the manager</typeparam>
    /// <returns>True if the Lua script existed and is of the desired type, otherwise false</returns>
    public bool TryGetScript<TCustomLua>(string scriptName,
                                         out TCustomLua? script)
        where TCustomLua : TLuaScript
    {
        if (TryGetScript(scriptName, out TLuaScript? luaScript) &&
            luaScript is TCustomLua convertedScript)
        {
            script = convertedScript;
            return true;
        }

        script = null;
        return false;
    }
    /// <summary>
    ///     Attempts to get a Lua script by name
    /// </summary>
    /// <param name="scriptName">The name of the Lua script to call into</param>
    /// <param name="script">The desired Lua script if found</param>
    /// <returns>True if the Lua script existed, otherwise false</returns>
    public bool TryGetScript(string scriptName, out TLuaScript? script)
    {
        scriptName = scriptName.ToLower();
        if (TryRetrieveScript(scriptName, out TLuaScript? luaScript))
        {
            script = luaScript;
            return script != null;
        }

        script = null;
        return false;
    }
    /// <summary>
    ///     Unloads a Lua script from the manager
    /// </summary>
    /// <param name="scriptName">The name of the lua script to unload</param>
    public void UnloadScript(string scriptName)
    {
        if (string.IsNullOrEmpty(scriptName))
        {
            return;
        }

        scriptName = scriptName.ToLower();
        if (ScriptsTable.TryGetValue(scriptName, out TLuaScript? script) &&
            ScriptsTable.Remove(scriptName))
        {
            ScriptUnloaded?.Invoke(script);
        }
        else
        {
            LogManager
                .LogWarning($"Unable to unload {scriptName}, it is either not loaded or failed to be removed!",
                            LuaCategory);
        }
    }
    /// <summary>
    ///     Creates a new instance of a Lua script class as defined by the manager
    /// </summary>
    /// <param name="filePath">The file path to the lua script to create</param>
    /// <returns>The newly created Lua script class</returns>
    protected abstract TLuaScript? CreateLuaScript(string filePath);
    /// <summary>
    ///     Loads all Lua files from the base directory, and then any directories below
    /// </summary>
    protected void LoadLuaFiles()
    {
        PopulateModulePaths();
        LoadLuaFiles(RootDirectory);
    }
    /// <summary>
    ///     Allows for additional logic/elements to be added to the arguments table prior to dispatching the Lua function call
    /// </summary>
    /// <param name="script">The Lua script that we are preparing the arguments table for</param>
    /// <param name="arguments">Arguments table to pass into the event</param>
    protected virtual Dictionary<string, object> PrepareArgumentsTable(
        TLuaScript script, Dictionary<string, object> arguments)
    {
        return arguments;
    }
    /// <summary>
    ///     Attempts to find a Lua file by digging recursively through the directories within the root until the file is found
    ///     or not
    /// </summary>
    /// <param name="currentDirectory">The current directory to search for a Lua file</param>
    /// <param name="scriptName">The name of the Lua script</param>
    /// <param name="filePath">The file path the Lua script was found at if one was found</param>
    /// <returns>True if the Lua script was found, otherwise false</returns>
    protected bool TryFindLuaFile(string currentDirectory, string scriptName,
                                  out string filePath)
    {
        filePath = string.Empty;
        string currentFilePath = $"{currentDirectory}/{scriptName}.lua";
        string libraryModules = Path.Combine(currentDirectory, "Libs", "?.lua");
        ModulePaths.Add(libraryModules);
        if (File.Exists(currentFilePath))
        {
            filePath = currentFilePath;
            return true;
        }

        foreach (string directory in Directory.GetDirectories(currentDirectory)
                                              .Where(name => !name.ToLower()
                                                         .Contains("libs")))
        {
            if (TryFindLuaFile(directory, scriptName, out filePath))
            {
                return true;
            }
        }

        ModulePaths.Remove(libraryModules);
        return false;
    }
    /// <summary>
    ///     Attempts to parse the contents of a Lua script file into the Lua script class
    /// </summary>
    /// <param name="filePath">The filepath to the Lua script file</param>
    /// <param name="script">The populated Lua script class</param>
    /// <returns>True if the Lua script was successfully loaded and populated, otherwise false</returns>
    protected bool TryParseLuaFile(string filePath, out TLuaScript? script)
    {
        try
        {
            script = CreateLuaScript(filePath);
            return script != null && script.LoadScript(ModulePaths);
        }
        catch (Exception ex)
        {
            LogManager
                .LogException($"Unable to create lua script from {Path.GetFullPath(filePath)} path",
                              LuaCategory, ex: ex);

            script = null;
        }

        return false;
    }

    /// <summary>
    ///     Attempts to retrieve a script from the scripts table
    /// </summary>
    /// <param name="scriptName">The name of the script to retrieve</param>
    /// <param name="script">The retrieved script is it exists</param>
    /// <returns>True if the script was retrieved, otherwise false</returns>
    protected virtual bool TryRetrieveScript(string scriptName,
                                             out TLuaScript? script)
    {
        return ScriptsTable.TryGetValue(scriptName, out script);
    }

    /// <summary>
    ///     Recursive helper function used to dig through the directories to load all the Lua files within into Lua script
    ///     classes
    /// </summary>
    /// <param name="directory">The current directory to search for Lua files</param>
    private void LoadLuaFiles(string directory)
    {
        LoadLuaFilesFromDirectory(directory);
        foreach (string subDirectory in Directory.GetDirectories(directory)
                                                 .Where(name => !name.ToLower()
                                                     .Contains("libs")))
        {
            LogManager
                .LogInfo($"Loading Lua files from {Path.GetFullPath(subDirectory)}",
                         LuaCategory, Yellow);

            LoadLuaFiles(subDirectory);
        }

        ModulePaths.Remove(Path.Combine(directory, "Libs", "?.lua"));
    }
    /// <summary>
    ///     Helper function used to load all the Lua files within the desired directory
    /// </summary>
    /// <param name="directory">The directory to load Lua files from</param>
    private void LoadLuaFilesFromDirectory(string directory)
    {
        ModulePaths.Add(Path.Combine(directory, "Libs", "?.lua"));
        foreach (string filePath in Directory.GetFiles(directory)
                                             .Where(name =>
                                                        name.EndsWith(".lua")))
        {
            if (TryParseLuaFile(filePath, out TLuaScript? script) &&
                script != null)
            {
                ScriptsTable[script.Name] = script;
                ScriptLoaded?.Invoke(script);
            }
        }
    }
    /// <summary>
    ///     Helper function used to load Lua modules needed for the Lua scripts
    /// </summary>
    private void PopulateModulePaths()
    {
        ModulePaths.Clear();
        ModulePaths.Add(Path.Combine(RootDirectory, "Libs", "?.lua"));
    }
}