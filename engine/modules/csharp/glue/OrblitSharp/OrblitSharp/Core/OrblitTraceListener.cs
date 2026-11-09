using System.Diagnostics;

namespace Orblit
{
    internal class OrblitTraceListener : TraceListener
    {
        public override void Write(string message)
        {
            Orb.PrintRaw(message);
        }

        public override void WriteLine(string message)
        {
            Orb.Print(message);
        }

        public override void Fail(string message, string detailMessage)
        {
            Orb.PrintErr("Assertion failed: ", message);
            Orb.PrintErr("  Details: ", detailMessage);

            try
            {
                string stackTrace = new StackTrace(true).ToString();
                Orb.PrintErr(stackTrace);
            }
            catch
            {
                // ignored
            }
        }
    }
}
