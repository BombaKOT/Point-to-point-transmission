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
    "Server bandwidth", 
    "Greeting (to client) [NA for non-applicable]",
    "Greeting (to server) [NA for non-applicable]"];
    private static string repository;
    public static async Task Main()
    {
        while(running)
        {
            for(int i = 0; i < rules.Length; i++)
                Console.Write($"\n{configStrings[i]}: {rules[i]}");
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
                ServerManager.SetUp(config);
                break;
            case "connects":
                await ServerManager.Connect(false); 
                break;
            case "connectc": 
                await ServerManager.Connect(true); 
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
                running = false;
                break;
            case "recreate":
                await ServerManager.Recreate(ReadUserRes(1, ["Name for recreation [NA for non-applicable]"])[0]);
                break;
            case "standby":
                /*
                    TODO:
                        RENAME previous server names to listening device 
                        Config lite - No dest EPs, ask if completely public or restricted to a couple of IP addresses, repo directory
                        Create functions: RequestServerFile, UploadServerFile, StandbyConnect, StandbyDisconnect/Stop
                */
                repository = ReadUserRes(1, ["Repository of your files"])[0];
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
        //Returns  EPs for future usage 
        IPEndPoint serverUDP = new IPEndPoint(ip != null ? ip : SelfIpAddress(), clientUDPPort);
        IPEndPoint serverTCP = new IPEndPoint(ip != null ? ip : SelfIpAddress(), clientTCPPort);
        IPEndPoint[] vals = new IPEndPoint[2];
        vals[0] = serverUDP;
        vals[1] = serverTCP;
        return vals;
    }

    private static void Initialize(int udpPort, int tcpPort)
    {
        //Creates the sockets and binds them to client EPs
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
        //THIS STILL FUCKS
        foreach(NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if(ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
            {
                foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        return ip.Address;
                    }
                }
            }  
        }

        Console.Write("\nCould not identify host machine IP address, please enter it manually: ");
        return IPAddress.Parse(Console.ReadLine());
    }

    public static void SetUp(Config config)
    {
        tcpSocket = null;
        udpSocket = null;
        
        Initialize(config.clientUDPPort, config.clientTCPPort);
        while(true)
            if(tcpSocket != null && udpSocket != null)
                break;

        DriveTransmition.SetSockets(tcpSocket, udpSocket);
        
        IPEndPoint[] serverEPS = EndPoints(config.serverUDPPort, config.serverTCPPort, IPAddress.Parse(config.serverIP));
        DriveTransmition.SetEP(serverEPS[0], serverEPS[1]);
        
        DriveTransmition.SetInfo(config.key2Server, config.key2Client, config.bandwidthClient, config.bandwidthServer);
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

    public static async Task Connect(bool client)
    {
        if (client)
        {
            Console.Write("\nConnecting you to dest server");
            await DriveTransmition.ClientConnect();
            Console.Write("\nFinished connecting, ready to send and recieve files");
        } else
        {
            Console.Write("\nAwaiting connection from client");
            await DriveTransmition.ServerConnect();
            Console.Write("\nConnected to client, ready to send or recieve files");
        }
    }

    public static async Task Disconnect()
    {
        Console.Write("\nBeginning process of disconnection");
        await DriveTransmition.ServerConnect();
        Console.Write("\nConnected to client, ready to send or recieve files");
    }

    public static async Task Recreate(string name = null)
    {
        Console.Write($"\nRecreating last recieved file");
        DriveTransmition.CreateFile(name);
        Console.Write($"\nFinished recreating last recieved file");
    }

    public static async Task StandbyServer(string repo)
    {
        
    }
}


public class Config
{
    public int clientUDPPort;
    public int clientTCPPort;
    public int bandwidthClient;
    public int serverUDPPort;
    public int serverTCPPort;
    public string serverIP;
    public int bandwidthServer;
    public string key2Server;
    public string key2Client;
    public void SetUp(string[] rules)
    {
        int.TryParse(rules[0], out clientUDPPort);
        int.TryParse(rules[1], out clientTCPPort);
        int.TryParse(rules[2], out bandwidthClient);
        int.TryParse(rules[3], out serverUDPPort);
        int.TryParse(rules[4], out serverTCPPort);
        serverIP = rules[5];
        if(rules[6] != "NA")
            int.TryParse(rules[6], out bandwidthServer);
        else 
            bandwidthServer = 0;
        key2Client = rules[7] != "NA" ? rules[7] : null;
        key2Server = rules[8] != "NA" ? rules[8] : null;
    }
}
