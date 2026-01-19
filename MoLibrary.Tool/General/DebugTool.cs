using System.Collections;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using JetBrains.Annotations;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Tool.General
{
    /// <summary>
    /// For easily debug
    /// </summary>
    public static class DebugTool
    {
        /// <summary>
        /// When enable this, Debug method will function.
        /// </summary>
        public static bool IsDebugging { get; set; }

      
        /// <summary>
        /// Do given action given times.
        /// </summary>
        /// <param name="times"></param>
        /// <param name="action"></param>
        public static void Do(int times, Action action)
        {
            while (times-- > 0)
            {
                action.Invoke();
            }
        }
        /// <summary>
        /// ToString() and Console.WriteLine() this obj.
        /// </summary>
        /// <param name="s"></param>
        public static void PrintLn(this object? s) => Console.WriteLine(s?.ToString());
        /// <summary>
        /// ToString() and Console.WriteLine() this obj.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="format">Description of given obj to print.</param>
        public static void PrintLn(this object? s, string format) => Console.WriteLine(format + s);
        /// <summary>
        /// string.Format() and Console.WriteLine() this obj.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="format">format string to print</param>
        /// <param name="useFormat">Placeholder, either true or false will still use string.Format</param>
        [StringFormatMethod("format")]
        public static void PrintLn(this object? s, string format, bool useFormat) => Console.WriteLine(format, s);
        /// <summary>
        /// Use string.join() and ToString() to format like an array, and finally Console.WriteLine() this obj.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="objList"></param>
        public static void PrintLn<T>(this ICollection<T> objList) => Console.WriteLine($"[{objList.StringJoin(",")}]");
        /// <summary>
        /// Format dictionary into Console.
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="dictionary"></param>
        public static void PrintLn<TKey, TValue>(this IDictionary<TKey, TValue> dictionary)
        {
            foreach (var pair in dictionary)
            {
                Console.WriteLine($"{pair.Key} —— {pair.Value}");
            }
        }

        /// <summary>
        /// ToString() and Console.WriteLine() this obj. Only function when <see cref="IsDebugging"/> is true.
        /// </summary>
        /// <param name="s"></param>
        public static void DebugPrintLn(this object? s)
        {
            if (IsDebugging) PrintLn(s);
        }

        /// <summary>
        /// ToString() and Console.WriteLine() this obj. Only function when <see cref="IsDebugging"/> is true.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="format">Description of given obj to print.</param>
        public static void DebugPrintLn(this object? s, string format)
        {
            if (IsDebugging) PrintLn(s, format);
        }

        /// <summary>
        /// string.Format() and Console.WriteLine() this obj. Only function when <see cref="IsDebugging"/> is true.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="format">format string to print</param>
        /// <param name="useFormat">Placeholder, either true or false will still use string.Format</param>
        [StringFormatMethod("format")]
        public static void DebugPrintLn(this object? s, string format, bool useFormat)
        {
            if (IsDebugging) PrintLn(s, format, useFormat);
        }

        /// <summary>
        /// Use string.join() and ToString() to format like an array, and finally Console.WriteLine() this obj. Only function when <see cref="IsDebugging"/> is true.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="objList"></param>
        public static void DebugPrintLn<T>(this ICollection<T> objList)
        {
            if (IsDebugging) PrintLn(objList);
        }
        /// <summary>
        /// Format dictionary into Console. Only function when <see cref="IsDebugging"/> is true.
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="dictionary"></param>
        public static void DebugPrintLn<TKey, TValue>(this IDictionary<TKey, TValue> dictionary)
        {
            if (IsDebugging) PrintLn(dictionary);
        }
    }
    
    /// <summary>
    /// Update the display without flicker
    /// </summary>
    public class ConsoleWriteUpdater
    {
        private (int, int)? _previousCursorPosition;

        /// <summary>
        /// Update use given string builder. (Will make the cursor fixed where the first time you use the update method)
        /// The effect depends on your console window size.
        /// </summary>
        /// <param name="stringBuilder"></param>
        public void Update(StringBuilder stringBuilder) => Update(stringBuilder.ToString());
        /// <summary>
        /// Update use given string. (Will make the cursor fixed where the first time you use the update method)
        /// The effect depends on your console window size.
        /// </summary>
        /// <param name="data"></param>
        public void Update(string data)
        {
            if (_previousCursorPosition == null)
            {
                _previousCursorPosition = (Console.CursorLeft, Console.CursorTop);//The cursor position depends on your console window size.
            }
            else
            {
                Console.SetCursorPosition(_previousCursorPosition.Value.Item1, _previousCursorPosition.Value.Item2);
            }
            Console.Write(data);
        }

        // can't support \n
        // private int _previousDataLength = 0;
        // public void Update(string data)
        // {
        //     data ??= "";
        //     var backup = new string('\b', _previousDataLength);
        //     Console.Write(backup);
        //     Console.Write(data);
        //     _previousDataLength = data.Length;
        // }
    }
}