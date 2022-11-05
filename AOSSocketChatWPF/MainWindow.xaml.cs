using System;
using System.Collections.Generic;
using System.Globalization;
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
            // IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
            IPAddress ipAddress = IPAddress.Parse("26.43.116.76");
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
                await StartServer();
            }
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
                if (response.Length == 0) return;
                if (response[0] == 'c')
                {
                    var messageCountResponse = response;
                    int messageCount;
                    if (!Int32.TryParse(new string(messageCountResponse.Skip(1).ToArray()), out messageCount)) continue;
                    var userIndex = tasks.IndexOf(received);
                    List<byte> nicknameBytes = new List<byte>() {(byte) 'n'};
                    nicknameBytes.AddRange(Encoding.UTF8.GetBytes(Nicknames[userIndex]));
                    var sendMessageCountTasks = Clients.Select(c => c.SendAsync(buffer, SocketFlags.None));
                    await Task.WhenAll(sendMessageCountTasks);
                    
                    //var sTask = Clients[userIndex].SendAsync(nicknameBytesArr, SocketFlags.None);
                    //AnnounceToChat($"SENDING NICKNAME: {DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)}");
                    //await sTask;
                    //AnnounceToChat($"SENT NICKNAME: {DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)}");
                    // await Task.Delay(1);
                    
                    // var confirmationBuffer = new byte[20];
                    // var nicknameConfirmationTasks = Clients.Select(c => c.ReceiveAsync(confirmationBuffer, SocketFlags.None));
                    // var a = await Task.WhenAll(nicknameConfirmationTasks);

                    // foreach (var client in Clients)   
                    // {
                    //     var confirmationBuffer = new byte[20];
                    //     var a = await client.ReceiveAsync(confirmationBuffer, SocketFlags.None);
                    //     PrintToChat(Encoding.UTF8.GetString(confirmationBuffer, 0, a));
                    // }
                    for (int i = 0; i < messageCount; i++)
                    {
                        var messageContentBytes = new byte[21];
                        var n = await Clients[userIndex].ReceiveAsync(messageContentBytes, SocketFlags.None);
                        // var messageContent = Encoding.UTF8.GetString(messageContentBytes, 0, n);
                        // var message = $"{Nicknames[userIndex]}: {messageContent}";
                        // var messageBytes = Encoding.UTF8.GetBytes(message, 0, message.Length);
                        
                        var sendMessagesTasks = Clients.Select(c => c.SendAsync(messageContentBytes, SocketFlags.None));
                        await Task.WhenAll(sendMessagesTasks);
                        // sTask = Clients[userIndex].SendAsync(messageContentBytes, SocketFlags.None);
                        // AnnounceToChat(
                        //     $"SENDING MESSAGE: {DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)}");
                        // await sTask;
                        // AnnounceToChat(
                        //     $"SENT MESSAGE: {DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)}");
                        // var message = $"{Nicknames[userIndex]}: {response}";
                    }
                    var nicknameBytesArr = nicknameBytes.ToArray();
                    var sendNicknameTasks = Clients.Select(c => c.SendAsync(nicknameBytesArr, SocketFlags.None));
                    await Task.WhenAll(sendNicknameTasks);
                }
                
                // var userIndex = tasks.IndexOf(received);
                // var message = $"{Nicknames[userIndex]}: {response}";
                // PrintToChat(response);
                
                // var messageBytes = Encoding.UTF8.GetBytes(message, 0, message.Length);
                // var sendMessagesTasks = Clients.Select(c => c.SendAsync(messageBytes, SocketFlags.None));
                // await Task.WhenAll(sendMessagesTasks);
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
                var clientNickname = Encoding.UTF8.GetString(buffer.Skip(1).ToArray(), 0, received - 1);
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