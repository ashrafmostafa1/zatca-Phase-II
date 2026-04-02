using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serilog;

namespace Zatca_Phase_II.Helpers
{
    public static class LogsFile
    {
        public static void MessagePrinter(string message)
        {
            try
            {
                // Get the log file path
                string logDirectoryPath = Path.Combine("Logs", "Printer");
                string logFilePath = Path.Combine(
                    logDirectoryPath,
                    DateTime.Now.ToString("yyyy-MM-dd") + ".txt"
                );

                // Create the directory if it doesn't exist
                if (!Directory.Exists(logDirectoryPath))
                {
                    Directory.CreateDirectory(logDirectoryPath);
                }

                using (StreamWriter sw = new StreamWriter(logFilePath, true))
                {
                    sw.WriteLine($"{DateTime.Now}: {message}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error Logs Message : {ex.Message}");
            }
        }

        public static void MessageZatca(string message)
        {
            try
            {
                // Get the log file path
                string logDirectoryPath = Path.Combine("Logs", "Zatca");
                string logFilePath = Path.Combine(
                    logDirectoryPath,
                    $"{DateTime.Now.ToString("yyyy-MM-dd")}.txt"
                );

                // Create the directory if it doesn't exist
                if (!Directory.Exists(logDirectoryPath))
                {
                    Directory.CreateDirectory(logDirectoryPath);
                }

                using (StreamWriter sw = new StreamWriter(logFilePath, true))
                {
                    sw.WriteLine($"{DateTime.Now}: {message}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error Logs Message : {ex.Message}");
            }
        }
    }
}
