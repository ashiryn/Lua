using FluffyVoid.FileUtilities;
using MoonSharp.Interpreter;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FluffyVoid.Lua;

/// <summary>
///     Exposed Actions that Lua scripts can use to directly interact with Lua or the Lua Manager
/// </summary>
[MoonSharpUserData]
public class LuaApplication
{
    /// <summary>
    ///     Function used to register an event from a Lua script with the C# Lua Script class
    /// </summary>
    public Action<string, DynValue>? RegisterEvent;

    /// <summary>
    ///     Appends text to a text file
    /// </summary>
    /// <param name="fileName">The file to append text to</param>
    /// <param name="text">The text to append to the file</param>
    public void AppendToTextFile(string fileName, string text)
    {
        DataLoader.SaveTextFile(fileName, text, append: true);
    }
    /// <summary>
    ///     Loads the text from a text file
    /// </summary>
    /// <param name="fileName">The file to read text from</param>
    /// <returns>The string of text that was loaded from the file</returns>
    public string? LoadTextFile(string fileName)
    {
        if (DataLoader.LoadTextFile(fileName, out string result))
        {
            return !string.IsNullOrEmpty(result) ? result : null;
        }

        return null;
    }
    /// <summary>
    ///     Write the data to a json file
    /// </summary>
    /// <param name="fileName">The file to write text to</param>
    /// <param name="contents">The data to write to the file</param>
    public void WriteToJsonFile(string fileName, string contents)
    {
        contents = JToken.Parse(contents).ToString(Formatting.Indented);
        DataLoader.SaveTextFile(fileName, contents);
    }
    /// <summary>
    ///     Writes the text to a text file
    /// </summary>
    /// <param name="fileName">The file to write text to</param>
    /// <param name="text">The text to write to the file</param>
    public void WriteToTextFile(string fileName, string text)
    {
        DataLoader.SaveTextFile(fileName, text);
    }
}