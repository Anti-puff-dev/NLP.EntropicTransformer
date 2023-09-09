
using System.Runtime.CompilerServices;


namespace NLP
{
    internal class Trace
    {
        public static void Message(string message, [CallerMemberName] string callingMethod = "", [CallerFilePath] string callingFilePath = "", [CallerLineNumber] int callingFileLineNumber = 0)
        {
            Console.WriteLine($"Line: {callingFileLineNumber} / Method: {callingMethod} / {message}");
        }
    }
}
