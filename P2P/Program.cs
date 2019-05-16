using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace P2P
{
    class Program
    {
        private static void DeletePrevConsoleLine()
        {
            if (Console.CursorTop == 0) return;
            Console.SetCursorPosition(0, Console.CursorTop - 1);
            Console.Write(new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, Console.CursorTop - 1);
        }

        static void Main(string[] args)
        {
            try
            {

                Console.Write("Enter your name: ");
                string userName = Console.ReadLine();
                Chat chat = new Chat(userName);
                chat.SendMessage();



                Thread ListenThread = new Thread(new ThreadStart(chat.Listen));
                ListenThread.Start();

                Console.WriteLine();

                Thread ListenThread2 = new Thread(new ThreadStart(chat.TCPListen));
                ListenThread2.Start();

                



                while (true)
                {
                    var message = Console.ReadLine();
                    DeletePrevConsoleLine();
                    var msg = $"[{DateTime.Now.ToLongTimeString()}] {userName}: {message}";
                    Console.WriteLine(msg);

                    
                    chat.SendTCPMessage(message);
                    
                }


            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

        }
    }

    public class Chat
    {
        const int TCPMessagePort = 8888;
        private UdpClient UdpClient;
        private string ClientName;
        
        private IPAddress MulticastAddress;
        private IPEndPoint RemoteEP;
        private TcpListener tcpListener { get; set; }
        private List<UDPUser> ConnectedUsersList { get; set; }
        public Chat(string name)
        {
            
            ClientName = name;
            MulticastAddress = IPAddress.Parse("239.0.0.222");
            UdpClient = new UdpClient();
            ConnectedUsersList = new List<UDPUser>();
            UdpClient.JoinMulticastGroup(MulticastAddress);
            RemoteEP = new IPEndPoint(MulticastAddress, 2222);

        }
        public void SendMessage()
        {
            Byte[] buffer = Encoding.UTF8.GetBytes(ClientName);
            UdpClient.Send(buffer, buffer.Length, RemoteEP);

        }

        public void Listen()
        {
            UdpClient client = new UdpClient();

            client.ExclusiveAddressUse = false; //one port for many users
            IPEndPoint localEp = new IPEndPoint(IPAddress.Any, 2222);

            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true); //Позволяет ограничить сокет, разрешив только тот адрес, который уже используется.
            

            client.Client.Bind(localEp);

            client.JoinMulticastGroup(MulticastAddress);

            Console.WriteLine("\tListening started");

            string formatted_data;

            while (true)
            {
                Byte[] data = client.Receive(ref localEp);
                if (ConnectedUsersList.Find(x => x.ipAddress.ToString() == localEp.Address.ToString()) == null)
                {
                    formatted_data = Encoding.UTF8.GetString(data);

                    ConnectedUsersList.Add(new UDPUser()
                    {
                        chatConnection = null,
                        username = formatted_data,
                        ipAddress = localEp.Address,
                        IsConnected = true
                    });
                    int number = ConnectedUsersList.FindIndex(x => x.ipAddress.ToString() == localEp.Address.ToString());
                    Console.WriteLine("USER " + ConnectedUsersList[number].username + " Connected");

                    

                    initTCP(number);


                    
                }
            }
        }
        private void initTCP(int index)
        {
            var newtcpConnect = new TcpClient();
            newtcpConnect.Connect(new IPEndPoint(ConnectedUsersList[index].ipAddress, TCPMessagePort)); 
            ConnectedUsersList[index].chatConnection = newtcpConnect;
            Thread tcp = new Thread(() => TcpMessage(newtcpConnect, ConnectedUsersList[index].username, true));
            tcp.Start();
            SendTCPMessage(ClientName);
        }
        public void TCPListen()
        {
            tcpListener = new TcpListener(IPAddress.Any, 8888);
            tcpListener.Start();

            while (true)
            {
                TcpClient tcpClient = tcpListener.AcceptTcpClient();
                IPAddress address1 = ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address;
                string Name;
                if (ConnectedUsersList.FindIndex(x => x.ipAddress.ToString() == address1.ToString()) == -1)
                {
                    ConnectedUsersList.Add(new UDPUser()
                    {
                        chatConnection = tcpClient,
                        username = "unknown",
                        ipAddress = address1,
                        IsConnected = true

                    });
                    Name = "unknown";

                }
                else
                {
                    Name = ConnectedUsersList.Find(x => x.ipAddress.ToString() == address1.ToString()).username;
                }

                Thread tcp = new Thread(() => TcpMessage(tcpClient, Name, true));
                tcp.Start();

            }

        }
        private void TcpMessage(TcpClient connection, string username, bool IsLocalConnection)
        {
            NetworkStream stream = connection.GetStream();
            try
            {
                while (IsLocalConnection)
                {

                    byte[] data = new byte[64]; // буфер для получаемых данных
                    StringBuilder builder = new StringBuilder();
                    string message;
                    int bytes = 0;
                    do
                    {
                        bytes = stream.Read(data, 0, data.Length);
                        builder.Append(Encoding.UTF8.GetString(data, 0, bytes));
                    }
                    while (stream.DataAvailable);
                    message = builder.ToString();
                    if (username == "unknown")
                    {
                        username = message;
                        Console.WriteLine("User " + username + " Connected");
                        ConnectedUsersList[(ConnectedUsersList.FindIndex(x => x.ipAddress.ToString() == ((IPEndPoint)connection.Client.RemoteEndPoint).Address.ToString()))].username = message;
                    }
                    else
                    {
                        var msg = $"[{DateTime.Now.ToLongTimeString()}] {username}: {message}";
                        Console.WriteLine(msg);
                    }

                }
            }
            catch
            {
                Console.WriteLine(username + " покинул чат"); //соединение было прервано
                
                var address = ((IPEndPoint)connection.Client.RemoteEndPoint).Address;
                ConnectedUsersList.RemoveAll(X => X.ipAddress.ToString() == address.ToString());
                Console.WriteLine(address);
                if (stream != null)
                    stream.Close();//отключение потока
                if (connection != null)
                    connection.Close();//отключение клиента

            }

        }
        protected internal void SendTCPMessage(string message)
        {
            var messageBytes = Encoding.UTF8.GetBytes(message);
            var msg = $"[{DateTime.Now.ToLongTimeString()}] {ClientName}: {message}";
            

            ConnectedUsersList.ForEach(client =>
            {
                var clientStream = client.chatConnection.GetStream();

                clientStream.Write(messageBytes, 0, messageBytes.Length);
            });




        }
        
    }

    public class UDPUser
    {
        public TcpClient chatConnection;
        public string username;
        public IPAddress ipAddress;
        public bool IsConnected;
    }

    
 }


