
using System;
using System.Diagnostics;
using Tekla.Structures.Model;

class Program
{
    static void Main()
    {
        Console.WriteLine("=== TEKLA 2026 CONNECTION TEST ===\n");

        // 1. Check application architecture
        Console.WriteLine("64-bit: " +
            Environment.Is64BitProcess);

        // 2. Check running Tekla processes
        var processes =
            Process.GetProcessesByName("TeklaStructures");

        Console.WriteLine("Tekla running: " +
            (processes.Length > 0));

        Console.WriteLine("Tekla process count: " +
            processes.Length);

        // 3. Check loaded Open API DLL
        var dll = typeof(Model).Assembly;

        Console.WriteLine("API version: " +
            dll.GetName().Version);

        Console.WriteLine("DLL file version: " +
            FileVersionInfo.GetVersionInfo(
                dll.Location).FileVersion);

        Console.WriteLine("DLL path: " +
            dll.Location);

        // 4. Test connection
        try
        {
            var model = new Model();

            if (model.GetConnectionStatus())
            {
                Console.WriteLine("SUDAH CONNECTED NI");
                Console.WriteLine("\nCONNECTED!");

                var info = model.GetInfo();

                Console.WriteLine("Model: " +
                    info.ModelName);

                Console.WriteLine("Path: " +
                    info.ModelPath);
            }
            else
            {
                Console.WriteLine(
                    "\nFAILED: No connection.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "\nERROR: " + ex.ToString());
        }

        Console.WriteLine("\nPress Enter to exit...");
        Console.ReadLine();
    }
}