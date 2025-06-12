using Microsoft.VisualBasic;
using Squirrel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace High_5
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string username;
        public static bool showplayers = false;
        public static bool downlow = false;
        public static Image img = new Image();
        public static Image img2 = new Image();
        public static TextBlock text = new TextBlock();
        static TcpClient client = new TcpClient();
        static string appdatadir = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        //static string folderPath = System.IO.Path.Combine(appdatadir, "JimboINC", "High5");
        static string folderPath = Environment.CurrentDirectory;
        static bool listener = true;
        static string clickedUser = "";
        static List<string> onlineusers = new List<string>();
        MediaPlayer player = new MediaPlayer();
        int skin = 0;

        public MainWindow()
        {
            InitializeComponent();
            CheckForUpdates();

            string filePath = System.IO.Path.Combine(folderPath, "user.txt");

            if (File.Exists(filePath))
            {
                username = File.ReadAllText(filePath);
            }
            else
            {
                Directory.CreateDirectory(folderPath);

                using (FileStream fs = File.Create(filePath)) { }
                username = Interaction.InputBox("Username:", "Enter Username");
                File.WriteAllText(filePath, username);
            }

            player.Open(new Uri(System.IO.Path.Combine(folderPath, "high5.wav")));
            LoadHUD();
            ConnectToServer();
        }

        private async void CheckForUpdates()
        {
            try
            {
                using (var mgr = await UpdateManager.GitHubUpdateManager("https://github.com/jimberss/High-5"))
                {
                    await mgr.UpdateApp();
                }
            }
            catch
            {

            }
        }
        public void LoadHUD()
        {
            img.Source = new BitmapImage(new Uri(System.IO.Path.Combine(folderPath, "skin1.png")));
            img.RenderTransformOrigin = new Point(0.5, 0.5);
            GameCanvas.Children.Add(img);
            Canvas.SetLeft(img, 0);
            Canvas.SetTop(img, 0);
    
            img2.Source = new BitmapImage(new Uri(System.IO.Path.Combine(folderPath, "skin1.png")));
            GameCanvas.Children.Add(img2);
            Canvas.SetRight(img2, 0);
            Canvas.SetTop(img2, 0);
            img2.RenderTransform = new ScaleTransform(-1, 1); // Flip horizontally
            img2.RenderTransformOrigin = new Point(0.5, 0.5); // Flip around center

            GameCanvas.Children.Add(text);
            text.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            text.VerticalAlignment = VerticalAlignment.Top;
            text.Margin = new Thickness(10, 10, 0, 0);
        }

        public async void ConnectToServer()
        {
            while (!client.Connected)
            {
                try
                {
                    client = new TcpClient();
                    await client.ConnectAsync("ip", port);
                    NetworkStream stream = client.GetStream();

                    //Check username is unique
                    await RequestPlayers();
                    string newusername = "";
                    string filePath = System.IO.Path.Combine(folderPath, "user.txt");
                    foreach (var x in onlineusers)
                    {
                        if (x == username)
                        {
                            newusername = Interaction.InputBox("Username taken. Please enter a new username:", "Enter Username");

                            while (onlineusers.Contains(newusername))
                            {
                                newusername = Interaction.InputBox("Username taken. Please enter a new username:", "Enter Username");
                            }
                            username = newusername;
                        }
                    }
                    File.WriteAllText(filePath, username);
                    byte[] message = Encoding.UTF8.GetBytes(username);
                    await stream.WriteAsync(message, 0, message.Length);
                    _ = Task.Run(() => ListenForRequests());
                }
                catch
                {
                    await Task.Delay(5000);
                }
            }
        }

        private async Task ListenForRequests()
        {
            if (listener == true)
            {
                try
                {
                    NetworkStream stream = client.GetStream();
                    byte[] buffer = new byte[1024];

                    while (listener == true)
                    {
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            if (response.StartsWith("*"))
                            {
                                string requestingUser = response.Substring(3);
                                
                                await System.Windows.Application.Current.Dispatcher.Invoke(async () =>
                                {
                                    DialogResult result = System.Windows.Forms.MessageBox.Show(
                                        $"Connection request from {requestingUser}. Accept?",
                                        "Connection Request",
                                        MessageBoxButtons.YesNo,
                                        MessageBoxIcon.Question);

                                    byte[] replyMessage;

                                    if (result == System.Windows.Forms.DialogResult.Yes)
                                    {
                                        replyMessage = Encoding.UTF8.GetBytes("Yes");
                                        stream.Write(replyMessage, 0, replyMessage.Length);
                                        _ = HighFive();
                                    }
                                    else
                                    {
                                        replyMessage = Encoding.UTF8.GetBytes("No");
                                        stream.Write(replyMessage, 0, replyMessage.Length);
                                    }
                                });
                            }
                            else if (response == "Yes" || response == "No")
                            {
                                if (response == "Yes")
                                {
                                    int skinnum = (int)response[1];
                                    string downlow = response[2].ToString();
                                    string skinname = "skin" + skin.ToString();

                                    await SwitchOtherSkin(skinname);
                                    if(downlow == "Y")
                                    {
                                        await FlipOtherSkin();
                                    }
                                    _ = HighFive();
                                }
                                else
                                {
                                    System.Windows.MessageBox.Show($"{clickedUser} did not accept High Five");
                                }
                            }
                            else if (response.StartsWith("#"))
                            {
                                response = response.Substring(1);

                                foreach (var x in response.Split(';'))
                                {
                                    string trimmed = x.Trim();
                                    if (!string.IsNullOrEmpty(trimmed))
                                    {
                                        onlineusers.Add(trimmed);
                                    }
                                }
                            }
                        }
                    }
                }
                catch
                {
                    System.Windows.MessageBox.Show("Disconnected, retrying connection...");
                    ConnectToServer();
                }
            }
        }

        private async Task FlipOtherSkin()
        {
            var currentTransform = img2.RenderTransform as ScaleTransform;

            if (currentTransform == null)
            {
                img2.RenderTransform = new ScaleTransform(1, 1);
                currentTransform = (ScaleTransform)img2.RenderTransform;
            }

            currentTransform.ScaleY *= -1;
        }

        private async Task SwitchOtherSkin(string skinname)
        {
            img2.Source = new BitmapImage(new Uri(System.IO.Path.Combine(folderPath, "skins", skinname + ".png")));
        }


        private async void GameWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                if (showplayers == false)
                {
                    await ShowClients();
                }
                else
                {
                    text.Inlines.Clear();
                }
                showplayers = !showplayers;
            }
            else if(e.Key == Key.L)
            {
                if (!downlow)
                {
                    img.RenderTransform = new ScaleTransform(1, -1);
                }
                else
                {
                    img.RenderTransform = new ScaleTransform(1, 1);
                }
                downlow = !downlow;
            }
        }

        private async Task ShowClients()
        {
            text.Inlines.Clear();
            text.Inlines.Add(new Run("Online users:\n")
            {
                FontSize = 24,
                Foreground = Brushes.Chartreuse
            });
            await RequestPlayers();
            
            while(onlineusers.Count == 0)
            {
                await Task.Delay(500);
            }

            foreach (string x in onlineusers)
            {
                if(onlineusers.Count == 1)
                {
                    if(x == username)
                    {
                        text.Inlines.Add(new Run("No Online Users")
                        {
                            FontSize = 16,
                            Foreground = Brushes.Gray,
                        });
                    }
                }
                if (x == "Not Connected")
                {
                    text.Inlines.Add(new Run(x)
                    {
                        FontSize = 16,
                        Foreground = Brushes.Gray,
                    });
                }
                else
                {
                    if (username != x)
                    {
                        Hyperlink userLink = new Hyperlink(new Run(x))
                        {
                            FontSize = 16,
                            Foreground = Brushes.Gray,
                            TextDecorations = null
                        };

                        userLink.Click += (sender, args) =>
                        {
                            clickedUser = x;
                            System.Windows.MessageBox.Show($"Requesting to connect to {clickedUser}");
                            RequestConnection(clickedUser);
                        };
                        text.Inlines.Add(userLink);
                        text.Inlines.Add("\n");
                    }
                }
            }
        }

        private async Task RequestConnection(string clickedUser)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                string dl = "";
                if (downlow)
                {
                    dl = "Y";
                }
                else
                {
                    dl = "N";
                }
                    byte[] message = Encoding.UTF8.GetBytes(skin + dl + clickedUser);
                await stream.WriteAsync(message, 0, message.Length);
            }
            catch
            {
            }
        }
        private async Task RequestPlayers()
        {
            onlineusers.Clear();
            try
            {
                NetworkStream stream = client.GetStream();
                byte[] message = Encoding.UTF8.GetBytes("P");
                await stream.WriteAsync(message, 0, message.Length);
            }
            catch
            {
                onlineusers.Add("Not Connected");
            }
        }
        private async Task HighFive()
        {
            double left = 0;
            double right = GameCanvas.ActualWidth - img2.ActualWidth;
            double center = GameCanvas.ActualWidth / 2;
            double acceleration = 2;
            double speed = 0;

            player.Play();
            await Task.Delay(300);
            while (left + img.ActualWidth < center + 10 && right > center - 10)
            {
                await Task.Delay(16);

                speed += acceleration;

                left += speed;
                right -= speed;

                Canvas.SetLeft(img, left);
                Canvas.SetLeft(img2, right);
            }
            await Task.Delay(1000);
            player.Stop();

            Canvas.SetLeft(img, 0);
            Canvas.SetLeft(img2, GameCanvas.ActualWidth - img2.ActualWidth);
            await FlipOtherSkin();
            await SwitchOtherSkin("skin1");
        }


    }
}
