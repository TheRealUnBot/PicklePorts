using System;
using System.Diagnostics;
using Open.Nat;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;

namespace PicklePorts;

public partial class MainPage : ContentPage
{
    public NatDevice device;
    public string MRPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\MappingRenew.csv";
    public string[] MRFile;
    public List<List<string>> MRList = new List<List<string>>();
    public MainPage()
    {
        InitializeComponent();
    }
    public void OnConnectClicked(object sender, EventArgs e)
    {
        Status.Text = "Status: Initializing Connection...";
        GetNetworkInfo();
    }
    public async void AddNewMapping(object sender, EventArgs e)
    {

        string Description = await DisplayPromptAsync("What is the Port Mapping Name?", "Remember, keep it short and simple. Don't leave it blank!");
        if (Description == null)
        {
            return;
        }
        else if (Description == "")
        {
            Description = "My Mapping";
        }

        string PrivateIP = await DisplayPromptAsync("What's the Internal IP?", "Press 'Use Localhost' to use curent device IP. No IPv6 Addresses! Doing so will result in an error.", cancel: "Use Localhost");
        if (PrivateIP == null)
        {
            PrivateIP = GetLocalIpAddress();
        }
        IPAddress address;
        try
        {
            address = IPAddress.Parse(PrivateIP);
        }
        catch (FormatException exception)
        {
            await DisplayAlert("Wrong Format!", "Remember, IPv4 looks like x.x.x.x, and IPv6 is not supported. For now we'll set it to localhost for ya. It can be edited later. \n Data: " + exception.Source + "; " + exception.Message, "OK");
            address = IPAddress.Parse(GetLocalIpAddress());
            return;
        }
        string PrivatePort = await DisplayPromptAsync("What's the Private Port?", " Type the port you want to forward from the internal network. Not the port visble to the Internet. Remeber there is only 65,535 ports avalible and 0 is unavailable.", maxLength: 5, keyboard: Keyboard.Numeric);
        int PrivateInt = 0;
        if (PrivatePort == "")
        {
            await DisplayAlert("Can you read?", "Don't mean to seem rude, but this is supposed to be an number only situation, which means no charcters or blankness. So for now, we'll set it to 80.", "Whoops");
            PrivateInt = 80;
        }
        else if (PrivatePort.All(char.IsDigit))
        {
            PrivateInt = Int32.Parse(PrivatePort);
        }
        else
        {
            await DisplayAlert("Can you read?", "Don't mean to seem rude, but this is supposed to be an number only situation. So for now, we'll set it to 80.", "Whoops");
            PrivateInt = 80;
        }
        if (PrivateInt == 0)
        {
            await DisplayAlert("Can you read?", "Don't mean to seem rude, but I literally just said 0 is unavailable, I'll just set it to 80 instead.", "Whoops");
            PrivateInt = 80;
        }
        else if (PrivateInt > 65535)
        {
            PrivateInt = 65535;
        }
        string PublicPort = await DisplayPromptAsync("What's the Public Port?", " Type the port you want to forward to the internet. Not the port from the internal network. Remeber there is only 65,535 ports available and 0 is unavailable.", cancel: "Same as Private Port", maxLength: 5, keyboard: Keyboard.Numeric);
        if (PublicPort == null)
        {
            PublicPort = PrivateInt.ToString();
        }
        int PublicInt;
        if (PublicPort.All(char.IsDigit))
        {
            PublicInt = Int32.Parse(PublicPort);
        }
        else
        {
            await DisplayAlert("Can you read?", "Don't mean to seem rude, but this is supposed to be an number only situation. So for now, we'll set it to 80, you can edit it later.", "Whoops");
            PublicInt = 80;
        }
        if (PublicInt == 0)
        {
            await DisplayAlert("Can you read?", "Don't mean to seem rude, but I literally just said 0 is unavailable, I'll just set it to 80 instead.", "Whoops");
            PublicInt = 80;
        }
        if (PublicInt > 65535)
        {
            PublicInt = 65535;
        }
        bool boolProtocol = await DisplayAlert("What is the Port Mapping's Internet Protocol?", "Tcp is for secured and regulated packet sending while Udp is for fast unsecured packet sending.", "Tcp", "Udp");
        Protocol Protocol = Protocol.Udp;
        if (boolProtocol)
        {
            Protocol = Protocol.Tcp;
        }
        string LifeTime = await DisplayPromptAsync("How long will the Port Mapping last?", "Type the value in minutes, or 0 for infinite. Or just click Absolute Infinite, which will constantly make sure that the mapping exists, which will bypass any limits talked about in the warning. WARNING: Some routers will only support temporary values, permanent values or even 24 hour only, such as xFinity, except if you use absolute infinity.", cancel: "Absolute Infinite", maxLength: 10, keyboard: Keyboard.Numeric);
        Status.Text = "Status: Creating New...";
        bool Ai = false;
        if (LifeTime == null)
        {
            Ai = true;
            LifeTime = "0";
        }
        int LifeInt = Int32.Parse(LifeTime);
        try
        {
            await device.CreatePortMapAsync(new Mapping(Protocol, address, PrivateInt, PublicInt, LifeInt, Description));
            if (Ai)
            {
                File.AppendAllText(MRPath, Protocol + "│" + address + "│" + PrivateInt + "│" + PublicInt + "│" + Description + "│" + (await device.GetExternalIPAsync()).ToString() + Environment.NewLine);
            }
        }
        catch (MappingException me)
        {
            switch (me.ErrorCode)
            {
                case 718:
                    Console.WriteLine("The external port already in use.");
                    break;
                case 728:
                    Console.WriteLine("The router's mapping table is full.");
                    break;
            }
        }
        Status.Text = "Status: Refreshing...";
        var process = Process.GetProcessesByName("PicklePortsRenew")[0];
        process.Kill();
        GetNetworkInfo();
    }
    public async void RemoveAllMappings(object sender, EventArgs e)
    {
        if (await DisplayAlert("Are you sure you want to delete ALL Mappings?", "Continuing will delete the ALL Port Mappings, and may cause unwanted issues, such as a service no longer accessable outside of the Network.", "Yes", "No"))
        {
            Status.Text = "Status: Deleting...";
            foreach (var mapping in await device.GetAllMappingsAsync())
            {
                await device.DeletePortMapAsync(mapping);
                for (var x = 0; x < MRFile.Length; x++)
                {
                    if (MRFile[x].Split("│")[5].ToString() == (await device.GetExternalIPAsync()).ToString())
                    {
                        Debug.WriteLine("Match!");
                        File.WriteAllLines(MRPath, File.ReadLines(MRPath).Where(l => l != MRFile[x]).ToList());
                    }
                }
            }
        }
        Status.Text = "Status: Refreshing...";
        var process = Process.GetProcessesByName("PicklePortsRenew")[0];
        process.Kill();
        GetNetworkInfo();
    }
    public async void OnEditMapping(object sender, EventArgs e)
    {
        Debug.WriteLine("Edit!");
        Button EditMapping = sender as Button;
        foreach (var mapping in await device.GetAllMappingsAsync())
        {
            if (mapping.ToString() == EditMapping.ClassId)
            {
                string Description = await DisplayPromptAsync("What is the Port Mapping Name?", "Remember, keep it short and simple. Don't leave it blank!", initialValue: mapping.Description.ToString());
                if (Description == null)
                {
                    return;
                }
                string PrivateIP = await DisplayPromptAsync("What's the Internal IP?", "Press 'Use Localhost' to use curent device IP. No IPv6 Addresses! Doing so will result in an error.", cancel: "Use Localhost", initialValue: mapping.PrivateIP.ToString());
                if (PrivateIP == null)
                {
                    PrivateIP = GetLocalIpAddress();
                }
                IPAddress address;
                try
                {
                    address = IPAddress.Parse(PrivateIP);
                }
                catch (FormatException exception)
                {
                    await DisplayAlert("Wrong Format!", "Remember, IPv4 looks like x.x.x.x, and IPv6 is not supported. For now we'll set it to localhost for ya. It can be edited later. \n Data: " + exception.Source + "; " + exception.Message, "OK");
                    address = IPAddress.Parse(GetLocalIpAddress());
                    return;
                }
                string PrivatePort = await DisplayPromptAsync("What's the Private Port?", " Type the port you want to forward from the internal network. Not the port visble to the Internet. Remeber there is only 65,535 ports avalible and 0 is unavailable.", initialValue: mapping.PrivatePort.ToString(), maxLength: 5, keyboard: Keyboard.Numeric);
                int PrivateInt = 0;
                if (PrivateInt == 0)
                {
                    await DisplayAlert("Can you read?", "Don't mean to seem rude, but I literally just said 0 is unavailable, I'll just set it to 80 instead.", "Whoops");
                    PrivateInt = 80;
                }
                else if (PrivatePort.All(char.IsDigit))
                {
                    PrivateInt = Int32.Parse(PrivatePort);
                }
                else
                {
                    await DisplayAlert("Can you read?", "Don't mean to seem rude, but this is supposed to be an number only situation. So for now, we'll set it to 80.", "Whoops");
                    PrivateInt = 80;
                }
                if (PrivateInt == 0)
                {
                    await DisplayAlert("Can you read?", "Don't mean to seem rude, but I literally just said 0 is unavailable, I'll just set it to 80 instead.", "Whoops");
                    PrivateInt = 80;
                }
                else if (PrivateInt > 65535)
                {
                    PrivateInt = 65535;
                }
                string PublicPort = await DisplayPromptAsync("What's the Public Port?", " Type the port you want to forward from the internal network. Not the port visble to the Internet. Remeber there is only 65,535 ports avalible and 0 is unavailable.", cancel: "Same as Private Port", maxLength: 5, keyboard: Keyboard.Numeric, initialValue: mapping.PublicPort.ToString());
                if (PublicPort == null)
                {
                    PublicPort = PrivatePort;
                }
                int PublicInt;
                if (PublicPort.All(char.IsDigit))
                {
                    PublicInt = Int32.Parse(PublicPort);
                }
                else
                {
                    await DisplayAlert("Can you read?", "Don't mean to seem rude, but this is supposed to be an number only situation. So for now, we'll set it to 80, you can edit it later.", "Whoops");
                    PublicInt = 80;
                }
                if (PublicInt == 0)
                {
                    await DisplayAlert("Can you read?", "Don't mean to seem rude, but I literally just said 0 is unavailable, I'll just set it to 80 instead.", "Whoops");
                    PublicInt = 80;
                }
                if (PublicInt > 65535)
                {
                    PublicInt = 65535;
                }
                bool boolProtocol = await DisplayAlert("What is the Port Mapping's Internet Protocol?", "Tcp is for secured and regulated packet sending while Udp is for fast un secured packet sending.", (mapping.Protocol.ToString() == "Tcp" ? "Current: " : "") + "Tcp", (mapping.Protocol.ToString() == "Udp" ? "Current: " : "") + "Udp");
                Protocol Protocol = Protocol.Udp;
                if (boolProtocol)
                {
                    Protocol = Protocol.Tcp;
                }
                string yes = "";
                for (var x = 0; x < MRFile.Length; x++)
                {
                    if (MRFile[x].ToString() == mapping.Protocol.ToString() + "│" + mapping.PrivateIP.ToString() + "│" + mapping.PrivatePort.ToString() + "│" + mapping.PublicPort.ToString() + "│" + mapping.Description.ToString() + "│" + (await device.GetExternalIPAsync()).ToString())
                    {
                        yes = "Current: ";
                    }
                }
                string LifeTime = await DisplayPromptAsync("How long will the Port Mapping last?", "Type the value in minutes, or 0 for infinite. Or just click Absolute Infinite, which will constantly make sure that the mapping exists, which will bypass any limits talked about in the warning. WARNING: Some routers will only support temporary values, permanent values or even 24 hour only, such as xFinity, except if you use absolute infinity.", initialValue: mapping.Lifetime.ToString(), cancel: yes + "Absolute Infinite", maxLength: 10, keyboard: Keyboard.Numeric);
                Status.Text = "Status: Creating New...";
                bool Ai = false;
                if (LifeTime == null)
                {
                    Ai = true;
                    LifeTime = "0";
                }
                int LifeInt = Int32.Parse(LifeTime);
                if (await DisplayAlert("Are you sure you want to continue?", "Continuing will reset the Mapping to the bottom and it will take a few min to sync.", "Yes", "No (We don't Judge)") == false)
                {
                    return;
                }
                Status.Text = "Status: Editing Mapping...";
                await device.DeletePortMapAsync(mapping);
                await device.CreatePortMapAsync(new Mapping(Protocol, address, PrivateInt, PublicInt, LifeInt, Description));
                for (var x = 0; x < MRFile.Length; x++)
                {
                    if (MRFile[x].ToString() == mapping.Protocol.ToString() + "│" + mapping.PrivateIP.ToString() + "│" + mapping.PrivatePort.ToString() + "│" + mapping.PublicPort.ToString() + "│" + mapping.Description.ToString() + "│" + (await device.GetExternalIPAsync()).ToString())
                    {
                        File.WriteAllLines(MRPath, File.ReadLines(MRPath).Where(l => l != MRFile[x]).ToList());
                        File.AppendAllText(MRPath, Protocol.ToString() + "│" + address.ToString() + "│" + PrivateInt.ToString() + "│" + PublicInt.ToString() + "│" + Description.ToString() + "│" + (await device.GetExternalIPAsync()).ToString() + Environment.NewLine);
                    }
                }
                Status.Text = "Status: Refreshing...";
                var process = Process.GetProcessesByName("PicklePortsRenew")[0];
                process.Kill();
                GetNetworkInfo();

            }
        }
    }
    public async void OnDeleteMapping(object sender, EventArgs e)
    {
        Debug.WriteLine("Delete!");
        Button DeleteMapping = sender as Button;
        foreach (Mapping mapping in await device.GetAllMappingsAsync())
        {
            if (mapping.ToString() == DeleteMapping.ClassId)
            {
                if (await DisplayAlert("Are you sure you want to delete this Mapping?", "Continuing will delete the seleted Port Mapping, and may cause unwanted issues, such as a service no longer accessable outside of the Network.", "Yes", "No") == false)
                {
                    return;
                }
                Status.Text = "Status: Deleting...";
                await device.DeletePortMapAsync(mapping);
                for (var x = 0; x < MRFile.Length; x++)
                {
                    if (MRFile[x].ToString() == mapping.Protocol.ToString() + "│" + mapping.PrivateIP.ToString() + "│" + mapping.PrivatePort.ToString() + "│" + mapping.PublicPort.ToString() + "│" + mapping.Description.ToString() + "│" + (await device.GetExternalIPAsync()).ToString())
                    {
                        Debug.WriteLine("Match!");
                        File.WriteAllLines(MRPath, File.ReadLines(MRPath).Where(l => l != MRFile[x]).ToList());
                    }
                }
                Status.Text = "Status: Refreshing...";
                var process = Process.GetProcessesByName("PicklePortsRenew")[0];
                process.Kill();
                GetNetworkInfo();
            }
        }
    }
    public async void OnRenewMapping(object sender, EventArgs e)
    {
        Status.Text = "Status: Renewing...";
        Button RenewMapping = sender as Button;
        foreach (var mapping in await device.GetAllMappingsAsync())
        {
            if (mapping.ToString() == RenewMapping.ClassId)
            {
                int LifeTime = Int32.Parse(await DisplayPromptAsync("How long will the Renewed Port Mapping last?", "Type the value in minutes, or 0 for infinite. Or just click Infinite. WARNING: Some routers will only support temporary values or even 24 hour only!! *cough* xFinity *cough*", initialValue: (mapping.Lifetime / 60).ToString(), maxLength: 10, keyboard: Keyboard.Numeric)) * 60;
                await device.DeletePortMapAsync(mapping);
                await device.CreatePortMapAsync(new Mapping(mapping.Protocol, mapping.PrivateIP, mapping.PrivatePort, mapping.PublicPort, LifeTime, mapping.Description));
                Status.Text = "Status: Refreshing...";
                GetNetworkInfo();
            }
        }
    }
    public async void GetNetworkInfo()
    {
        Connect.Text = "Resync";
        MappingGrid.Clear();
        Status.Text = "Status: Connecting to Gateway...";
        try
        {
            var nat = new NatDiscoverer();
            var cts = new CancellationTokenSource(5000);
            device = await nat.DiscoverDeviceAsync(PortMapper.Upnp, cts);
            NatDiscoverer.TraceSource.Switch.Level = SourceLevels.Information;
        }     
        catch (NatDeviceNotFoundException e)
        {
            await DisplayAlert("Router Not Found", "Looks like there are no Upnp Routers around, make sure you are connected to the internet, and that the router supports Upnp and has it enabled. \n Data: " + e.Source + "; " + e.Message, "OK");
            return;
        }
        Status.Text = "Status: Acquiring IPs...";
        IPAddress[] IPS = Dns.GetHostAddresses(Dns.GetHostName());
        InternalIP.Text = "Network Interface IP Address: \n" + GetLocalIpAddress();
        ExternalIP.Text = "External Router IP Address: \n" + await device.GetExternalIPAsync();
        Status.Text = "Starting Renewal Service...";
        Process[] processes = Process.GetProcessesByName("PicklePortsRenew");
        if (processes.Length > 0)
        {
            processes[0].Kill();
        }
        Process.Start(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "\\PicklePortsRenew.exe");
        Status.Text = "Connnecting to Renewal Service...";
        if (File.Exists(MRPath))
        {
            MRFile = File.ReadAllLines(MRPath);
            if (File.ReadAllText(MRPath) != "")
            {
                for (var x = 0; x < MRFile.Length; x++)
                {
                    List<string> MRListSub = new List<string>();
                    for (var y = 0; y < MRFile[x].Split("│").Length; y++)
                    {
                        MRListSub.Add(MRFile[x].Split("│")[y]);
                    }
                    MRList.Add(MRListSub);
                }
            }
        }
        else
        {
            File.Create(MRPath);
        }
        Status.Text = "Status: Setting Up Mapping Grid...";
        Button AddNew = new Button { Text = "Add New", HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, FontSize = 10 };
        AddNew.Clicked += new System.EventHandler(AddNewMapping);
        Button RemoveAll = new Button { Text = "Remove All", HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, FontSize = 10 };
        RemoveAll.Clicked += new System.EventHandler(RemoveAllMappings);
        MappingGrid.Add(AddNew, 1, 0);
        MappingGrid.Add(RemoveAll, 2, 0);
        MappingGrid.Add(new Label { Text = "#", TextColor = Colors.Black, BackgroundColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center }, 0, 1);
        MappingGrid.Add(new Label { Text = "Description", TextColor = Colors.Black, BackgroundColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center }, 1, 1);
        MappingGrid.Add(new Label { Text = "Private IP", TextColor = Colors.Black, BackgroundColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center }, 2, 1);
        MappingGrid.Add(new Label { Text = "Private Port", TextColor = Colors.Black, BackgroundColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center }, 3, 1);
        MappingGrid.Add(new Label { Text = "Public Port", TextColor = Colors.Black, BackgroundColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center }, 4, 1);
        MappingGrid.Add(new Label { Text = "Protocol", TextColor = Colors.Black, BackgroundColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center }, 5, 1);
        MappingGrid.Add(new Label { Text = "Lifetime", TextColor = Colors.Black, BackgroundColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center }, 6, 1);
        MappingGrid.Add(new Label { Text = "Expiration", TextColor = Colors.Black, BackgroundColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center }, 7, 1);
        Label EditButtons = new Label { Text = "Editor Buttons", TextColor = Colors.Black, BackgroundColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center };
        Grid.SetColumnSpan(EditButtons, 3);
        MappingGrid.Add(EditButtons, 8, 1);
        Status.Text = "Status: Acquiring Mappings...";
        int MappingNumber = 1;

        try 
        { 
            foreach (var mapping in await device.GetAllMappingsAsync())
            {
                MappingNumber++;
                Button Edit = new Button { Text = "Edit", TextColor = Colors.Black, BackgroundColor = Colors.Gray, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, FontSize = 10, ClassId = mapping.ToString() };
                Edit.Clicked += new System.EventHandler(OnEditMapping);
                Button Delete = new Button { Text = "Delete", TextColor = Colors.Black, BackgroundColor = Colors.Gray, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, FontSize = 10, ClassId = mapping.ToString() };
                Delete.Clicked += new System.EventHandler(OnDeleteMapping);
                Button Renew = new Button { Text = "Renew", TextColor = Colors.Black, BackgroundColor = Colors.Gray, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, FontSize = 10, ClassId = mapping.ToString() };
                Renew.Clicked += new System.EventHandler(OnRenewMapping);
                MappingGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
                MappingGrid.Add(new Label { Text = (MappingNumber - 1).ToString(), TextColor = Colors.Black, BackgroundColor = Colors.Gray, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center }, 0, MappingNumber);
                MappingGrid.Add(new Label { Text = mapping.Description, TextColor = Colors.Black, BackgroundColor = Colors.Gray, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center }, 1, MappingNumber);
                MappingGrid.Add(new Label { Text = mapping.PrivateIP.ToString(), TextColor = Colors.Black, BackgroundColor = Colors.Gray, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center }, 2, MappingNumber);
                MappingGrid.Add(new Label { Text = mapping.PrivatePort.ToString(), TextColor = Colors.Black, BackgroundColor = Colors.Gray, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center }, 3, MappingNumber);
                MappingGrid.Add(new Label { Text = mapping.PublicPort.ToString(), TextColor = Colors.Black, BackgroundColor = Colors.Gray, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center }, 4, MappingNumber);
                MappingGrid.Add(new Label { Text = mapping.Protocol.ToString(), TextColor = Colors.Black, BackgroundColor = Colors.Gray, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center }, 5, MappingNumber);
                string Lifetime = (mapping.Lifetime / 60).ToString() + " Minutes or\n" + (mapping.Lifetime / 3600).ToString() + " Hours";
                string Expiration = mapping.Expiration.ToLocalTime().ToString();
                for (var x = 0; x < MRFile.Length; x++)
                {
                    if (MRFile[x].ToString() == mapping.Protocol.ToString() + "│" + mapping.PrivateIP.ToString() + "│" + mapping.PrivatePort.ToString() + "│" + mapping.PublicPort.ToString() + "│" + mapping.Description.ToString() + "│" + (await device.GetExternalIPAsync()).ToString())
                    {
                        Lifetime = "Absolute Infinite";
                        Expiration = "Technically None";
                    }
                }
                MappingGrid.Add(new Label { Text = Lifetime, TextColor = Colors.Black, BackgroundColor = Colors.Gray, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center }, 6, MappingNumber);
                MappingGrid.Add(new Label { Text = Expiration, TextColor = Colors.Black, BackgroundColor = Colors.Gray, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center }, 7, MappingNumber);
                MappingGrid.Add(Edit, 8, MappingNumber);
                MappingGrid.Add(Delete, 9, MappingNumber);
                MappingGrid.Add(Renew, 10, MappingNumber);
            }
        }
        catch
        { 
            Debug.WriteLine("Problem!!");
        }

        if (MappingNumber == 1)
        {
            Label NoneFound = new Label { Text = "No Mappings Found", TextColor = Colors.Black, BackgroundColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center };
            Grid.SetColumnSpan(NoneFound, 11);
            MappingGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
            MappingGrid.Add(NoneFound, 0, 2);
        }
        Status.Text = "Status: Connected and Mappings Acquired";
    }
    public static string GetLocalIpAddress()
    {
        UnicastIPAddressInformation mostSuitableIp = null;

        var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

        foreach (var network in networkInterfaces)
        {
            if (network.OperationalStatus != OperationalStatus.Up)
                continue;

            var properties = network.GetIPProperties();

            if (properties.GatewayAddresses.Count == 0)
                continue;

            foreach (var address in properties.UnicastAddresses)
            {
                if (address.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;

                if (IPAddress.IsLoopback(address.Address))
                    continue;

                if (!address.IsDnsEligible)
                {
                    if (mostSuitableIp == null)
                        mostSuitableIp = address;
                    continue;
                }

                // The best IP is the IP got from DHCP server
                if (address.PrefixOrigin != PrefixOrigin.Dhcp)
                {
                    if (mostSuitableIp == null || !mostSuitableIp.IsDnsEligible)
                        mostSuitableIp = address;
                    continue;
                }

                return address.Address.ToString();
            }
        }

        return mostSuitableIp != null
            ? mostSuitableIp.Address.ToString()
            : "";
    }
    public async void ShowWelcome(object sender, EventArgs e)
    {
        Debug.WriteLine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "\\PicklePortsRenew.exe");
        if (!File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\FirstStart.txt"))
        {
            await DisplayAlert("Welcome!", "Hey there, and welcome to Pickle Ports! The wackiest and easiest Port Mapper around! We take pride in doing everthing in the background, while you have the best experience. We first need to setup in order to have to maximum experience, this should be quick...", "Let's Go!");
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "\\RegisterRenewal.exe",
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            };
            proc.Start();
            File.Create(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\FirstStart.txt");
        }
    }
    public async void ShowInfo(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new NewPage1());
    }
}

