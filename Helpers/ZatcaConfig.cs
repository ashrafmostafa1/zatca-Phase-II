using System.Reflection;
using System.Runtime.InteropServices;
using ZATCA.EInvoice.SDK.Contracts.Models;

namespace Zatca_Phase_II.Helpers;

public static class ZatcaConfig
{
    public static bool ExtractSchematrons()
    {
        try
        {
            Assembly assembly = Assembly.Load("Zatca.EInvoice.SDK");

            string outputFolder;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                outputFolder = Path.Combine("..\\", "..\\", "..\\", "Data", "Rules", "schematrons");
            else
                outputFolder = Path.Combine(Path.GetTempPath(), "zatca", "Data", "Rules", "schematrons");
            Directory.CreateDirectory(outputFolder);

            if (assembly != null)
            {
                string[] resourceNames = assembly.GetManifestResourceNames();
                foreach (string resourceName in resourceNames.Where(x => x.Contains("Schematrons")))
                {
                    string[] resourceNameParts = resourceName.Split('.');
                    string fileName = $"{resourceNameParts[^2]}.{resourceNameParts[^1]}";
                    string outputFile = Path.Combine(outputFolder, fileName);

                    using Stream stream = assembly.GetManifestResourceStream(resourceName)!;
                    if (stream != null)
                    {
                        using FileStream fileStream = new(
                            outputFile,
                            FileMode.Create,
                            FileAccess.Write
                        );
                        stream.CopyTo(fileStream);
                        LogsFile.MessageZatca(
                            $"Resource {resourceName} berhasil diekstrak ke {outputFile}"
                        );
                    }
                }
            }

            return false;
        }
        catch (System.Exception)
        {
            return false;
        }
    }
}
