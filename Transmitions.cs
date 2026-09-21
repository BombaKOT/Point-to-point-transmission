using System.Net.Sockets;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.IO;
using System.Threading.Tasks;
/*
    TODO
    Finish server loop
    Add udp connect
    create a code system to verify contents
*/
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
    //Determines if the server is still passively running or not
    public static bool active;
    //Determines if the server is taking requests
    public static bool running;
    //Determines if the server supports downloading files
    private static bool download;
    //Determines if the server supports uploading files
    private static bool upload;
    //Where the server will upload and download files from/to
    private static string repo;

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

    public static void SetInfoStandbyServer(string key, int transBandwidth, bool aDownload, bool aUpload, string repository)
    {
        selfPasskey = key;
        bandwidth = transBandwidth;
        download = aDownload;
        upload = aUpload;
        repo = repository;
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

        if (!connectionTCPSocket.Poll(200, SelectMode.SelectWrite))
        {
            Console.Write("\nServer device is not ready to recieve file\n");
            return;
        }

        for(int i = 0; i < whole; i++)
        {
            int o = bandwidth * i;

            for(int k = 0; k < bandwidth; k++)
                tempArray[k] = fileBytes[k + o];

            await connectionTCPSocket.SendAsync(tempArray);
        }

        if(leftOver <= 0) return;

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
        else 
            File.WriteAllText(binPath, "");

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
        if(!(connectionTCPSocket.Connected || connectionTCPSocket.Poll(100, SelectMode.SelectRead))) return;
        
        await socketUDP.DisconnectAsync(true);
        if (server)
        {
            await connectionTCPSocket.DisconnectAsync(false);
            connectionTCPSocket.Close();
        }
         else
            await connectionTCPSocket.DisconnectAsync(true);
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
        await socketUDP.ConnectAsync(udpEP);
        
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
        await socketUDP.ConnectAsync(udpEP);
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

        Console.Write($"Finished building {path}");
    }

    public static string[] ConnectionStatus()
    {
        List<string> info = new List<string>();
        info.Add($"IP Address {(socketTCP.LocalEndPoint as IPEndPoint).Address}");
        info.Add($"TCP Socket port: {(socketTCP.LocalEndPoint as IPEndPoint).Port}");
        info.Add($"UDP Socket port: {(socketUDP.LocalEndPoint as IPEndPoint).Port}");
        if (connectionTCPSocket.Connected)
        {
            info.Add("SOCKET STATUS: Connected");
            info.Add($"SOCKET DES IP: {(connectionTCPSocket.RemoteEndPoint as IPEndPoint).Address}");
            info.Add($"SOCKET READ MODE: {connectionTCPSocket.Poll(100, SelectMode.SelectRead)}");
            info.Add($"SOCKET READ MODE: {connectionTCPSocket.Poll(100, SelectMode.SelectRead)}");
            info.Add($"SOCKET WRITE MODE: {connectionTCPSocket.Poll(100, SelectMode.SelectWrite)}");
            info.Add($"SOCKET ERRORS: {connectionTCPSocket.Poll(100, SelectMode.SelectError)}");
        } else
        {
            info.Add("SOCKET STATUS: Disconnected");
        }

        return info.ToArray();
    }

    //SERVER

    public static async Task ServerLoop()
    {
        Console.Write("\n\nSERVER LOOP BEGUN\n\n");

        string query;

        AddLog("Server Loop Start", "Log.txt");

        while (active)
        {
            while (running)
            {
                query = (await RecieveMessageUDP(bandwidth)).ToLower();
                AddLog($"Recieved query: {query}", "Log.txt");

                switch (query)
                {
                    case "upload":
                        if (!upload)
                        {
                            AddLog("Client turned away from uploading to server", "Log.txt");
                            await SendStatus(CODES.INVALID, "SERVER DOES NOT SUPPORT UPLOAD");
                        }
                        else
                        {
                            AddLog("Recieving file by client", "Log.txt");
                            await SendStatus(CODES.OK);
                            await RecieveFile();
                        }
                        continue;
                    case "download":
                        if (!download)
                        {
                            AddLog("Client turned away from downloading to server", "Log.txt");
                            await SendStatus(CODES.INVALID, "SERVER DOES NOT SUPPORT DOWNLOAD");
                        }
                        else
                        {
                            string fileQ = await RecieveMessageUDP(bandwidth);
                            fileQ = Path.Combine(repo, fileQ);
                            AddLog($"Recieved file request: {fileQ} | matching to: {fileQ}", "Log.txt");

                            if(File.Exists(fileQ))
                            {
                                await SendStatus(CODES.OK);
                                AddLog("File found, sending to client", "Log.txt");
                                await SendFile(fileQ);
                            }
                            else
                            {
                                AddLog("File not found", "Log.txt");
                                await SendStatus(CODES.ERROR, $"SERVER DID NOT FIND FILE: {fileQ}");
                            }
                        }
                        continue;
                    case "connect":
                        await SendStatus(CODES.OK);
                        await ServerConnect();
                        continue;
                    case "disconnect":
                        await SendStatus(CODES.OK);
                        await Disconnect();
                        continue;
                }

                await SendStatus(CODES.INVALID, "SERVER DOES NOT RECOGNISE THE REQUEST");
            }
            Thread.Sleep(1000);
        }

        await Disconnect();

    }

    public static async Task QueryServer(bool upload, string file = null)
    {
        // CONNECT/DISCONNECT
        if(file == null)
        {
            await SendMessageUDP(upload ? "Connect" : "Disconnect");
            if(await GetStatus()) 
                if (upload)
                    await ClientConnect();
                else
                    await Disconnect();
            return;
        }

        // UPLOAD/DOWNLOAD
        await SendMessageUDP(upload ? "Upload" : "Download");
        bool goo = GetStatus().Result;
        if (goo)
        {
            if (upload)
                await SendFile(file);
            else
            {
                await SendMessageUDP(file);
                await RecieveFile();
            }
        } else
            Console.Write("\nOpperation terminated\n");
    }

    private static async Task SendStatus(CODES code, string extra = null)
    {
        await SendMessageUDP($"{code.ToString()}:{extra}");
    }

    private static async Task<bool> GetStatus()
    {
        string code = await RecieveMessageUDP(bandwidth);
        Console.Write(code.Substring(code.IndexOf(":")));
        return code == "OK";
    }

    private enum CODES {
        OK,
        INVALID,
        ERROR
    }
}
