namespace WorldlineHost
{
    internal static class Logger
    {
        public static void Info(string message)
        {
            Console.Error.WriteLine($"[INFO] {DateTime.Now:HH:mm:ss.fff} {message}");
        }

        public static void Error(string message, Exception? ex = null)
        {
            Console.Error.WriteLine($"[ERROR] {DateTime.Now:HH:mm:ss.fff} {message}");
            if (ex != null)
            {
                Console.Error.WriteLine(ex.ToString());
            }
        }
    }
}