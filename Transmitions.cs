using System.Net.Sockets;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.IO;
using System.Threading.Tasks;
//METHOD FILE SEND/RECIEVE/DISCONNECT: FIX CONNECTION SOCKET ONLY FOR THE SERVER, MAKE A 3RD PARTY VARIABLE THAT USES THE CORRECT SOCKET
//LIKE BANDWIDTH
//ALSO CLEAR BIN
public static class DriveTransmition
{
    private static Socket connectionTCPSocket;
    private static Socket socketTCP;
    private static Socket socketUDP;
    private static int bandwidth;
    private static int bandwidthServer;
    private static IPEndPoint udpEP;
    private static IPEndPoint tcpEP;
    private static string serverGreeting;
    private static string selfPasskey;
    private static string binPath = "bin.bin";
    private static string lastName;
    private static string lastExtension;
    private static bool server;

    //CONFIG
    public static void SetSockets(Socket tcpSocketNew, Socket udpSocketNew)
    {
        socketTCP = tcpSocketNew;
        socketUDP = udpSocketNew;
    }

    public static void SetEP(IPEndPoint desEPUDP, IPEndPoint desEPTCP)
    {
        udpEP = desEPUDP;
        tcpEP = desEPTCP;
    }

    public static void SetInfo(string greeting, string key, int transBandwidth, int serverBandwidth = 0)
    {
        serverGreeting = greeting;
        selfPasskey = key;
        bandwidth = transBandwidth;
        bandwidthServer = serverBandwidth;
    }

    private static async Task<string> RecieveMessageUDP(int width)
    {
        byte[] buffer = new byte[width];
        Memory<byte> mem = new Memory<byte>(buffer);
        
        var res = await socketUDP.ReceiveMessageFromAsync(mem, udpEP);
        int length = res.ReceivedBytes;
        return Encoding.ASCII.GetString(buffer, 0, length);
    }

    private static async Task SendMessageUDP(string contents)
    {
        await socketUDP.SendToAsync(Encoding.ASCII.GetBytes(Path.GetFileName(contents)), udpEP);
    }

    //TRANSMITIONS
    public async static Task SendFile(string filePath)
    {
        byte[] fileBytes = File.ReadAllBytes(filePath);
        int length = fileBytes.Length;

        await SendMessageUDP(Path.GetFileName(filePath));
        await SendMessageUDP(length.ToString());

        int leftOver = length % bandwidth;
        int whole = (length - leftOver) / bandwidth;

        byte[] tempArray = new byte[bandwidth];

        for(int i = 0; i < whole; i++)
        {
            int o = bandwidth * i;

            for(int k = 0; k < bandwidth; k++)
                tempArray[k] = fileBytes[k + o];

            await connectionTCPSocket.SendAsync(tempArray);
        }

        tempArray = new byte[leftOver];

        for(int i = whole * 16384; i < leftOver; i++)
            tempArray[i] = fileBytes[i];
        
        await connectionTCPSocket.SendAsync(tempArray);
    }

    public async static Task RecieveFile()
    {
        int length;
        int currentLength = 0;
        int totalLength;

        byte[] buffer;
        Memory<byte> mem;
        
        int width = bandwidthServer != 0 ? bandwidthServer : bandwidth;
        
        string fileName = await RecieveMessageUDP(width);
        
        string fileLength = await RecieveMessageUDP(width);
        int.TryParse(fileLength, out totalLength);

        if(!Path.Exists(binPath))
            File.Create(binPath).Close();

        using(var stream = new FileStream(binPath, FileMode.Append, FileAccess.Write, FileShare.None, 16384, useAsync: true))
        {
            while(currentLength < totalLength)
            {
                buffer = new byte[width];
                mem = new Memory<byte>(buffer);

                length = await connectionTCPSocket.ReceiveAsync(mem);
                currentLength += length;
                await stream.WriteAsync(buffer, 0, length);
            }
            await stream.FlushAsync();
            stream.Close();
        }
        CreateFile(fileName);  
    }

    public async static Task Disconnect()
    {
        await connectionTCPSocket.DisconnectAsync(!server);
        
        if (server)
        {
            connectionTCPSocket.Close();
            connectionTCPSocket = null;
        }

        if(socketTCP.Connected)
            await socketTCP.DisconnectAsync(true);
    }

    public async static Task ClientConnect()
    {
        server = false;

        if(serverGreeting != null)
        {
            byte[] greeting = Encoding.ASCII.GetBytes(serverGreeting);
            await socketUDP.SendToAsync(greeting, udpEP);
        }
        
        Console.Write($"\nAttemping to establish connection to {tcpEP.Address} on port {tcpEP.Port}");
        
        await socketTCP.ConnectAsync(tcpEP);
        
        connectionTCPSocket = socketTCP;
    }

    public async static Task ServerConnect()
    {
        server = true;
        
        if (selfPasskey != null)
        {
            bool passed = false;
            int keyByteLength = Encoding.ASCII.GetByteCount(selfPasskey);
            string attempt;

            while (!passed)
            {
                attempt = await RecieveMessageUDP(keyByteLength);

                if(attempt.Equals(selfPasskey)) break;
            }
        }

        socketTCP.Listen();

        connectionTCPSocket = await socketTCP.AcceptAsync();
    }

    //OTHER
    private static void AddLog(string info, string logPath)
    {
        File.AppendAllText(logPath, $"{DateTime.Today}\n{info}\n\n");
    }

    public static void CreateFile(string file = null)
    {
        if(file != null)
        {
            lastName = Path.GetFileName(file);
            lastExtension = Path.GetExtension(file); 
        }

        string path;
        int index = 1;

        do {path = lastName + index++ + lastExtension;} while (Path.Exists(path));
        
        Console.Write($"Building {path}");

        File.Copy(binPath, path);
        
        File.WriteAllText(binPath, "");

        Console.Write($"Finished building {path}");
    }

}
