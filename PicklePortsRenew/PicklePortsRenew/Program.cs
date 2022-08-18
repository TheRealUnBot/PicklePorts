using System.ServiceProcess;
using System;
using Open.Nat;
using System.Threading;
using System.Diagnostics;


public static class Program
{
    public static string MRPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\MappingRenew.csv";
    public static string[] MRFile;
    public static List<List<string>> MRList = new List<List<string>>();
    public static Process process = Process.GetCurrentProcess();  

    static void Main(string[] args)
    {   
        while (true)
        {
            Console.WriteLine("Start Loop");
            TryRouter();
            Thread.Sleep(1800000);
        }
    }
    public static async void TryRouter()
    {
        try
        {
            Console.WriteLine("Searching Router");
            NatDiscoverer discoverer = new NatDiscoverer();
            CancellationTokenSource cts = new CancellationTokenSource(10000);
            NatDevice device = await discoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts);
            Console.WriteLine("Router Found");
            if (File.Exists(MRPath))
            {

                MRFile = File.ReadAllLines(MRPath);
                if (File.ReadAllText(MRPath) != "")
                {
                    for (var x = 0; x < MRFile.Length; x++)
                    {
                        if (MRFile[x].Split("│")[5] == (await device.GetExternalIPAsync()).ToString())
                        {
                            bool MappingFound = false;
                            foreach (var mapping in await device.GetAllMappingsAsync())
                            {
                                if (MRFile[x].ToString() == mapping.Protocol.ToString() + "│" + mapping.PrivateIP.ToString() + "│" + mapping.PrivatePort.ToString() + "│" + mapping.PublicPort.ToString() + "│" + mapping.Description.ToString() + "│" + (await device.GetExternalIPAsync()).ToString())
                                {
                                    MappingFound = true;
                                }
                            }
                            if (MappingFound == false)
                            {
                                await device.CreatePortMapAsync(new Mapping((MRFile[x].Split("│")[0] == "Tcp") ? Protocol.Tcp : Protocol.Udp, System.Net.IPAddress.Parse(MRFile[x].Split("│")[1]), Int32.Parse(MRFile[x].Split("│")[2]), Int32.Parse(MRFile[x].Split("│")[3]), 0, MRFile[x].Split("│")[4]));
                            }
                        }
                    }

                }
            }
            else
            {

                File.Create(MRPath);
            }
        }
        catch (NatDeviceNotFoundException e)
        {
            Console.WriteLine("No Upnp Device Found!");
        }
    }
}