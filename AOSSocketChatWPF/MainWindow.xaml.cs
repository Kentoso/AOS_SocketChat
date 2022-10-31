using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AOSSocketChatWPF
{
    public partial class MainWindow : Window
    {
        private Socket Listener;
        private List<Socket> Clients;
        private List<string> Nicknames;
        private IPEndPoint _endPoint;
        public MainWindow()
        {
            IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
            int variant = 4;
            _endPoint = new(ipAddress, 1025 + variant);
            Listener = new Socket(_endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            Clients = new List<Socket>();
            Nicknames = new List<string>();
            InitializeComponent();
        }

        private void SendButton_OnClick(object sender, RoutedEventArgs e)
        {
            string messageBoxText = MessageBox.Text;
            PrintToChat(messageBoxText);
        }

        private async void StartServerButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!Listener.IsBound)
            {
                Listener.Bind(_endPoint);
                Listener.Listen(100);
            }
            await StartServer();
        }
        
        private async Task Listen()
        {
            var buffer = new byte[256];
            var tasks = Clients.Select(c => c.ReceiveAsync(buffer, SocketFlags.None)).ToList();
            while (true)
            {
                for (int i = 0; i < tasks.Count(); i++)
                {
                    var task = tasks[i];
                    if (task.IsCompleted)
                    {
                        tasks[i] = Clients[i].ReceiveAsync(buffer, SocketFlags.None);
                    }
                }
                var received = await Task.WhenAny(tasks);
                var response = Encoding.UTF8.GetString(buffer, 0, received.Result);
                var message = $"{Nicknames[tasks.IndexOf(received)]}: {response}";
                // PrintToChat(response);
                var messageBytes = Encoding.UTF8.GetBytes(message, 0, message.Length);
                var sendMessagesTasks = Clients.Select(c => c.SendAsync(messageBytes, SocketFlags.None));
                await Task.WhenAll(sendMessagesTasks);
            }
        }

        private async Task StartServer()
        {
            int clientNumber = 0;
            if (!Int32.TryParse(ClientNumberBox.Text, out clientNumber)) return;
            AnnounceToChat("Waiting for connections...");
            for (int i = 0; i < clientNumber; i++)
            {
                var client = await Listener.AcceptAsync();
                var buffer = new byte[256];
                var received = await client.ReceiveAsync(buffer, SocketFlags.None);
                var clientNickname = Encoding.UTF8.GetString(buffer, 0, received);
                Clients.Add(client);
                Nicknames.Add(clientNickname);
                PrintToChat($"{i + 1} / {clientNumber}");
            }
            AnnounceToChat("Listening...");
            await Listen();
        }

        private void PrintToChat(string message)
        {
            ChatBox.Text += message + '\n';
        }

        private void AnnounceToChat(string message)
        {
            ChatBox.Text += "____________________________________________" + '\n';
            ChatBox.Text += message + '\n';
            ChatBox.Text += "____________________________________________" + '\n';
        }
    }
}