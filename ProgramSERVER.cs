using System.Net.Sockets;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.IO;
using System.Threading.Tasks;

await ConsoleManager.Main();
public static class ConsoleManager {
    private static bool running = true;
    private static string request;
    private static string[] rules = ["","","","","","","","","",];
    private static readonly string[] configStrings = ["Open UDP Port (self)", 
    "Open TCP Port (self)", 
    "Bandwidth (self)", 
    "Open UDP Port (server)", 
    "Open TCP Port (server)", 
    "Server IP Address", 
    "Bandwidth (server) [NA for non-applicable]", 
    "Greeting (to client) [NA for non-applicable]",
    "Greeting (to server) [NA for non-applicable]"];

    private static readonly string[] configStringsStandby = ["Open UDP Port (self)", 
    "Open TCP Port (self)", 
    "Bandwidth (self)", 
    "Repository path from which files will be taken/added to", 
    "Allow clients to request and download contents from this server's repository [Y/N]", 
    "Allow clients to upload files to this server's repository [Y/N]", 
    "Password for this server [NA for non-applicable]"];
    
    public static async Task Main()
    {
        while(running)
        {
            request = ReadUserRes(1, ["Query"])[0];
            await Decission(request);
        }
    }

    private static string[] ReadUserRes(int times, string[] messages)
    {
        string[] res = new string[times];
        for(int i = 0; i < times; i++)
        {
            Console.Write($"\n{messages[i]}: ");
            res[i] = Console.ReadLine().Trim();
        }
        return res;
    }

    private static async Task Decission(string query)
    {
        switch (query.ToLower())
        {
            case "config": 
                rules = ReadUserRes(9, configStrings);
                Config config = new Config();
                config.SetUp(rules);
                ServerManager.SetUp(config, false);
                break;
            case "connect-server":
                await ServerManager.Connect(false, false); 
                break;
            case "connect-client": 
                await ServerManager.Connect(true, false);
                break; 
            case "connect-client-server": 
                await ServerManager.Connect(true, true); 
                break;
            case "send": 
                await ServerManager.SendTo(ReadUserRes(1, ["Filename"])[0]);
                break;
            case "disconnect":
                await ServerManager.Disconnect();
                break;
            case "recieve":
                await ServerManager.RecieveFrom();
                break;
            case "close":
                Console.Write("\nClosing app");
                await ServerManager.Disconnect();
                running = false;
                break;
            case "recreate":
                await ServerManager.Recreate(ReadUserRes(1, ["Name for recreation [NA for non-applicable]"])[0]);
                break;
            case "connectstatus":
                ServerManager.ConnectionStatus();
                break;
            case "listconfig":
                for(int i = 0; i < rules.Length; i++)
                    Console.Write($"\n{configStrings[i]}: {rules[i]}");
                break;
            case "clear":
                Console.Clear();
                break;
            case "config-standby-server":
                rules = ReadUserRes(7, configStringsStandby);
                Config configSBS = new Config();
                configSBS.SetUpStandby(rules);
                ServerManager.SetUp(configSBS, true);
                break;
            case "vent-sbs":
                ServerManager.SwitchSBServerState();
                break;
            case "request-file":
                await ServerManager.ProcessFile(false, ReadUserRes(1, ["File name: "])[0]);
                break;
            case "upload-file":
                await ServerManager.ProcessFile(true, ReadUserRes(1, ["File path: "])[0]);
                break;
            case "start-server":
                await ServerManager.Connect(false, true);
                break;
            case "end-server":
                DriveTransmition.active = false;
                break;    
        }
    }
}



public static class ServerManager
{
    private static Socket tcpSocket;
    private static Socket udpSocket;

    public static IPEndPoint[] EndPoints(int clientUDPPort, int clientTCPPort, IPAddress ip = null)
    {
        IPEndPoint serverUDP = new IPEndPoint(ip != null ? ip : SelfIpAddress(), clientUDPPort);
        IPEndPoint serverTCP = new IPEndPoint(ip != null ? ip : SelfIpAddress(), clientTCPPort);
        IPEndPoint[] vals = new IPEndPoint[2];
        vals[0] = serverUDP;
        vals[1] = serverTCP;
        return vals;
    }

    private static void Initialize(int udpPort, int tcpPort)
    {
        IPEndPoint[] clientEPS = EndPoints(udpPort, tcpPort);

        tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        IPEndPoint clientUDPEP = clientEPS[0];
        IPEndPoint clientTCPEP = clientEPS[1];
        tcpSocket.Bind(clientTCPEP);
        udpSocket.Bind(clientUDPEP);
    }

    private static IPAddress SelfIpAddress()
    {
        foreach(NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            if(ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                    if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        Console.Write($"\nAUTO IP DISCOVER: {ip.Address}\n");
                        return ip.Address;
                    }

        Console.Write("\nCould not identify host machine IP address, please enter it manually: ");
        return IPAddress.Parse(Console.ReadLine());
    }

    public static void SetUp(Config config, bool standbyServer)
    {
        tcpSocket = null;
        udpSocket = null;

        
        Initialize(config.clientUDPPort, config.clientTCPPort);
        while(true)
            if(tcpSocket != null && udpSocket != null)
                break;

        DriveTransmition.SetSockets(tcpSocket, udpSocket);
        
        
        if(standbyServer)
            DriveTransmition.SetInfoStandbyServer(config.key2Client, config.bandwidthClient, config.allowDownload, config.allowUpload, config.repository);
        else
        {
            IPEndPoint[] serverEPS = EndPoints(config.serverUDPPort, config.serverTCPPort, IPAddress.Parse(config.serverIP));
            DriveTransmition.SetEP(serverEPS[0], serverEPS[1]);
            DriveTransmition.SetInfo(config.key2Server, config.key2Client, config.bandwidthClient, config.bandwidthServer);
        }

    } 

    public static async Task SendTo(string filePath)
    {
        if (Path.Exists(filePath))
        {
            Console.Write($"\nSending file {filePath}");
            await DriveTransmition.SendFile(filePath);
            Console.Write($"\nFinished sending file {filePath}");
        } else
        {
            Console.Write($"\nFilepath doesn't exist {filePath}");
        }
    }

    public static async Task RecieveFrom()
    {
        Console.Write("\nAwaiting file");
        await DriveTransmition.RecieveFile();
        Console.Write("\nFinished receiving and constructing file, command 'recreate' to rebuild a file");
    }

    public static async Task Connect(bool client, bool toSBServer)
    {
        
        if (client)
        {
            Console.Write("\nConnecting you to dest server");
            if (toSBServer)
                await DriveTransmition.QueryServer(true);
            else
                await DriveTransmition.ClientConnect();
            Console.Write("\nFinished connection sequence");
        } else
        {
            Console.Write("\nAwaiting connection from client");
            if (toSBServer)
            {
                DriveTransmition.active = true; 
                DriveTransmition.running = true;
                DriveTransmition.ServerLoop();
                Console.Write("\nServer loop started");
            }
            else
            {
                await DriveTransmition.ServerConnect();
                Console.Write("\nConnected to client, ready to send or recieve files");
            }
        }
    }

    public static async Task Disconnect()
    {
        Console.Write("\nBeginning process of disconnection");
        await DriveTransmition.Disconnect();
        Console.Write("\nConnected to client, ready to send or recieve files");
    }

    public static async Task Recreate(string name = null)
    {
        Console.Write($"\nRecreating last recieved file");
        DriveTransmition.CreateFile(name);
        Console.Write($"\nFinished recreating last recieved file");
    }

    public static void ConnectionStatus()
    {
        Console.Write("\nFetching info\n");
        foreach(string info in DriveTransmition.ConnectionStatus())
        {
            Console.Write("\n" + info);
        }
    }

    public static void SwitchSBServerState()
    {
        Console.Write($"\nActive status: {DriveTransmition.active} \nSwitching from RUNNING: {DriveTransmition.running} to RUNNING: {!DriveTransmition.running}");
        DriveTransmition.running = !DriveTransmition.running;
        Console.Write($"\nSwitched successfully");
    }

    public static async Task ProcessFile(bool request, string fileReq = null)
    {
        if (request)
            await DriveTransmition.QueryServer(true, fileReq);
        else
            await DriveTransmition.QueryServer(false, fileReq);
    }
}


public class Config
{
    public int clientUDPPort;
    public int clientTCPPort;
    public int bandwidthClient;
    public string key2Client;
    //Client only
    public string key2Server;
    public string serverIP;
    public int bandwidthServer;
    public int serverUDPPort;
    public int serverTCPPort;
    //Server only
    public string repository;
    public bool allowDownload;
    public bool allowUpload;
    public void SetUp(string[] rules)
    {
        int.TryParse(rules[0], out clientUDPPort);
        int.TryParse(rules[1], out clientTCPPort);
        int.TryParse(rules[2], out bandwidthClient);
        int.TryParse(rules[3], out serverUDPPort);
        int.TryParse(rules[4], out serverTCPPort);
        serverIP = rules[5];
        if(rules[6].ToLower() != "na")
            int.TryParse(rules[6], out bandwidthServer);
        else 
            bandwidthServer = 0;
        key2Client = rules[7].ToLower() != "na" ? rules[7] : null;
        key2Server = rules[8].ToLower() != "na" ? rules[8] : null;
    }

    public void SetUpStandby(string[] rules)
    {
        int.TryParse(rules[0], out clientUDPPort);
        int.TryParse(rules[1], out clientTCPPort);
        int.TryParse(rules[2], out bandwidthClient);
        repository = rules[3];
        allowDownload = rules[4].ToLower() == "y";
        allowUpload = rules[5].ToLower() == "y";
        key2Client = rules[6].ToLower() != "na" ? rules[6] : null;
    }
}