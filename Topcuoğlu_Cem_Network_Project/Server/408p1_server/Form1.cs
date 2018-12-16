using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.Sockets;
using System.Threading;
using System.Net;

namespace _408p1_server
{
    public partial class Form1 : Form
    {
        // initiating server, socket and online users variables
        static bool accept = true;
        static Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        static List<Socket> socketList = new List<Socket>();
        static List<User> activeList = new List<User>();  // online users list
        static List<Game> gameList = new List<Game>(); // active and possible games list

        public Form1()
        {
            InitializeComponent();
            this.Text = "Server";
            button2.Enabled = false;
            button3.Enabled = false;
        }

        // 'Game' struct keeps a pair of players who either trying to maintain a connection to play or already playing
        public class Game
        {
            public User inviting;
            public User invited;
            public int rand;
            public List<int> invitingGuess;
            public List<int> invitedGuess;
            public int invitingScore;
            public int invitedScore;

            public Game(User u1, User u2, int r)
            {
                inviting = u1;
                invited = u2;
                rand = r;
                invitingGuess = new List<int>();
                invitedGuess = new List<int>();
                invitingScore = 0;
                invitedScore = 0;
            }
        };

        // 'User' class keeps username and socket information for each player
        public class User
        {
            public string username;
            public Socket socket;
            public bool isPlaying;
            public int score;

            public User(string un, Socket n)
            {
                username = un;
                socket = n;
                isPlaying = false;
                score = 0;
            }
        };

        // 'Start' button
        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                int serverPort = Convert.ToInt32(textBox1.Text);

                // initiating the interface
                Thread thrAccept;
                button1.Enabled = false;
                button2.Enabled = true;
                button3.Enabled = true;
                textBox1.ReadOnly = true;

                try
                {
                    server.Bind(new IPEndPoint(IPAddress.Any, serverPort)); // binding the server listener to the given port number

                    richTextBox1.AppendText("Started listening for incoming connections.\r\n");
                    accept = true;
                    server.Listen(10); // maximum length of pending connections. 
                    thrAccept = new Thread(new ThreadStart(Accept));
                    thrAccept.IsBackground = true; // with this option, threads will automatically shut down
                    thrAccept.Start();
                }

                catch
                {
                    richTextBox1.AppendText("Cannot create a server with the specified port number. Check the port number and try again.\n");
                    richTextBox1.AppendText("Terminating...\r\n");
                }
            }

            catch
            {
                richTextBox1.AppendText("Please enter a valid port number!\r\n");
            }

        }
        
        // 'Stop' button
        private void button2_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        // 'Send' button
        private void button3_Click(object sender, EventArgs e)
        {
            string message = textBox2.Text;

            if (!string.IsNullOrWhiteSpace(message)) // cannot send empty message
            {
                BroadCastMessage(message);
            }

            textBox2.Clear();
        }

        // accepting the connection
        private void Accept()
        {
            accept = true;
            while (accept)
            {
                try
                {
                    socketList.Add(server.Accept());
                    Thread thrReceive;
                    // a client is succesfuly connected to the server
                    thrReceive = new Thread(new ThreadStart(Receive)); // started to receiving the data
                    thrReceive.IsBackground = true; // with this option, threads will automatically shut down
                    thrReceive.Start();
                }

                catch
                {
                    accept = false;
                    this.Invoke((MethodInvoker)delegate ()
                    {
                        richTextBox1.AppendText("This connection could not be accepted.\r\n");
                    });
                }
            }
        }

        // receiving data from the user
        private void Receive()
        {
            bool connected = true;
            Socket n = socketList[socketList.Count - 1];
            
            while (connected)
            {
                try
                {
                    byte[] buffer = new byte[64]; // buffer to process received commands

                    if (n.Receive(buffer) <= 0)
                    {
                        throw new SocketException();
                    }

                    // processing the received buffer into an array of strings
                    string bufferMessage = Encoding.Default.GetString(buffer);
                    //bufferMessage += "\0";
                    bufferMessage = bufferMessage.Substring(0, bufferMessage.IndexOf("\0"));
                    string[] arrayBuffer = bufferMessage.Split(' ');

                    if (arrayBuffer[0] == "Username") // user sends his/her username
                    {
                        string username = arrayBuffer[1];

                        if (activeList.FindIndex(User => User.username == username) >= 0) // do not accept if the username already exists
                        {
                            connected = false;
                            richTextBox1.AppendText("A client could not connect due to an existing username.\r\n");

                            SendMessage("Namefail", n); // notify user accordingly
                            socketList.Remove(n);
                        }

                        else // otherwise accept and add new user to the 'actives list'
                        {
                            User temp = new User(username, n);
                            activeList.Add(temp);

                            this.Invoke((MethodInvoker)delegate ()
                            {
                                richTextBox1.AppendText(username + " has connected.\r\n");
                            });

                            SendMessage("Success", n); // notify user accordingly

                        }
                    }

                    else if (arrayBuffer[0] == "Disconnect") // user wants to disconnect
                    {
                        connected = false;

                        this.Invoke((MethodInvoker)delegate ()
                        {
                            richTextBox1.AppendText(arrayBuffer[1] + " has disconnected.\r\n");
                        });

                        disconnect(n);
                    }

                    else if (arrayBuffer[0] == "Message") // user sends a message
                    {
                        User sender = activeList.Find(User => User.socket == n); // find out the user's nick
                        string message = "";

                        for (int i = 1; i < arrayBuffer.Length; i++) // construct the message from the array of strings 
                        {
                            message += " " + arrayBuffer[i];
                        }

                        this.Invoke((MethodInvoker)delegate ()
                        {
                            richTextBox1.AppendText(sender.username + ":" + message + "\r\n");
                        });
                    }
                    
                    else if (arrayBuffer[0] == "List") // client wants to refresh the lobby list
                    {
                        RefreshLobby();
                    }

                    else if (arrayBuffer[0] == "Invite") // client invites another client to play
                    {
                        // find the users
                        int i = activeList.FindIndex(User => User.socket == n);
                        int j = activeList.FindIndex(User => User.username == arrayBuffer[1]);
                        string sending;

                        try
                        {
                            if (j == -1) // the invited user went offline
                            {
                                sending = "Offline " + arrayBuffer[1];
                                SendMessage(sending, n);

                                this.Invoke((MethodInvoker)delegate()
                                {
                                    richTextBox1.AppendText(activeList[i].username + " has invited an user that disconnected from the server.\r\n");
                                });
                            }

                            else // fine for now
                            {
                                User inviting = activeList[i];
                                User invited = activeList[j];
                                
                                if (invited.isPlaying == true) // the invited player is already in a game
                                {
                                    sending = "BusyPlaying " + invited.username;
                                    SendMessage(sending, n);
                                }
                     
                                else if (gameList.FindIndex(Game => Game.inviting.username == invited.username) >= 0 || gameList.FindIndex(Game => Game.invited.username == invited.username) >= 0)
                                { // the invited player is already processing an invitation
                                    sending = "BusyInvited " + invited.username;
                                    SendMessage(sending, n);
                                }

                                else // okay to send the invitation
                                {
                                    string toInvited = "Invited " + inviting.username;
                                    SendMessage(toInvited, invited.socket); // notify the user

                                    this.Invoke((MethodInvoker)delegate()
                                    {
                                        richTextBox1.AppendText(inviting.username + " has invited " + invited.username + " to play.\r\n");
                                    });

                                    Random r = new Random();
                                    int rInt = r.Next(0, 100);
                                    gameList.Add(new Game(inviting, invited, rInt)); // add the possible game to the list
                                }
                            }
                        }

                        catch
                        {
                            this.Invoke((MethodInvoker)delegate()
                            {
                                richTextBox1.AppendText("The invitation has failed.\r\n");
                            });
                        }
                    }

                    else if (arrayBuffer[0] == "Accept") // the invited player accepts
                    {
                        // find the users
                        int i = activeList.FindIndex(User => User.socket == n);
                        int j = activeList.FindIndex(User => User.username == arrayBuffer[1]);

                        try
                        {
                            User invited = activeList[i];
                            User inviting = activeList[j];

                            this.Invoke((MethodInvoker)delegate ()
                            {
                                richTextBox1.AppendText(invited.username + " has accepted " + inviting.username + "'s invitation to play.\r\n");
                            });

                            // initiate the game
                            activeList[i].isPlaying = true;
                            activeList[j].isPlaying = true;
                            string toInviting = "Accepted " + invited.username;
                            SendMessage(toInviting, inviting.socket);
                        }

                        catch // the inviting player went offline
                        {
                            string username = arrayBuffer[1];
                            int itemIndex = gameList.FindIndex(Game => Game.inviting.username == username);

                            if (itemIndex != -1)
                            {
                                gameList.Remove(gameList[itemIndex]);
                                activeList[i].isPlaying = false;

                                string toInvited = "Offline " + arrayBuffer[1];
                                SendMessage(toInvited, n); // notify the user
                            }    
                        }

                    }

                    else if (arrayBuffer[0] == "Reject") // the invited player rejects
                    {
                        // find the users
                        int i = activeList.FindIndex(User => User.socket == n);
                        int j = activeList.FindIndex(User => User.username == arrayBuffer[1]);

                        try
                        {
                            User invited = activeList[i];
                            User inviting = activeList[j];

                            // clean up for future invitations
                            Game item = gameList.Find(Game => Game.inviting.username == inviting.username);
                            gameList.Remove(item);

                            this.Invoke((MethodInvoker)delegate ()
                            {
                                richTextBox1.AppendText(invited.username + " has rejected " + inviting.username + "'s invitation to play.\r\n");
                            });

                            string toInviting = "Rejected " + invited.username;
                            SendMessage(toInviting, inviting.socket); // notify the user

                        }

                        catch // the invited player went offline
                        {
                            string username = arrayBuffer[1];
                            int itemIndex = gameList.FindIndex(Game => Game.inviting.username == username);

                            if (itemIndex != -1)
                            {
                                gameList.Remove(gameList[itemIndex]);
                                string toInvited = "Offline " + arrayBuffer[1];
                                SendMessage(toInvited, n); // notify the user
                            }                      
                        }
                    }

                    else if (arrayBuffer[0] == "Game") // initiate the game
                    {
                        int i = activeList.FindIndex(User => User.socket == n);
                        int j = activeList.FindIndex(User => User.username == arrayBuffer[1]);

                        try // notify the players
                        {
                            SendMessage("Game " + activeList[i].username, activeList[j].socket);
                            SendMessage("Game " + activeList[j].username, activeList[i].socket);

                            Game item = gameList.Find(Game => (Game.inviting.username == activeList[i].username || Game.invited.username == activeList[i].username));

                            this.Invoke((MethodInvoker)delegate ()
                            {
                                richTextBox1.AppendText("Game has started between " + item.inviting.username + " and " + item.invited.username + " with random number " + item.rand + ".\r\n");
                            });
                        }
                        
                        catch
                        {
                            this.Invoke((MethodInvoker)delegate ()
                            {
                                richTextBox1.AppendText("The game could not be initiated.\r\n");
                            });
                        }
                    }

                    else if (arrayBuffer[0] == "Lost")
                    {
                        // find the users and the game
                        int i = activeList.FindIndex(User => User.socket == n);
                        Game item = gameList.Find(Game => (Game.inviting.username == activeList[i].username || Game.invited.username == activeList[i].username));

                        int j;

                        if (item.invited.username != activeList[i].username)
                        {
                            j = activeList.FindIndex(User => User.username == item.invited.username);
                        }

                        else
                        {
                            j = activeList.FindIndex(User => User.username == item.inviting.username);
                        }
                     
                        try // finish up the game and make the players available again
                        {
                            activeList[i].isPlaying = false;
                            activeList[j].isPlaying = false;
                            activeList[j].score = activeList[j].score + 1;

                            gameList.Remove(item);

                            User loser = activeList[i];
                            User winner = activeList[j];

                            string toWinner = "Won " + loser.username;
                            SendMessage(toWinner, winner.socket); // notify the winner

                            this.Invoke((MethodInvoker)delegate()
                            {
                                richTextBox1.AppendText(winner.username + " has won a game against " + loser.username + ".\r\n");
                            });
                        }
                        catch
                        {
                            this.Invoke((MethodInvoker)delegate()
                            {
                                richTextBox1.AppendText("Something went wrong during the game.\r\n");
                            });
                        }
                    }

                    else if (arrayBuffer[0] == "Guess")
                    {
                        try
                        {
                            // find the users and the game
                            int i = activeList.FindIndex(User => User.socket == n);
                            int g = gameList.FindIndex(Game => (Game.inviting.username == activeList[i].username || Game.invited.username == activeList[i].username));
                            int guess = Convert.ToInt32(arrayBuffer[1]);
                            int rand = gameList[g].rand;
                            int j;
                            bool flag; // to pair i and j with inviting and invited users

                            if (gameList[g].inviting.username == activeList[i].username)
                            {
                                gameList[g].invitingGuess.Add(guess);
                                j = activeList.FindIndex(User => User.username == gameList[g].invited.username);
                                flag = true;
                            }

                            else
                            {
                                gameList[g].invitedGuess.Add(guess);
                                j = activeList.FindIndex(User => User.username == gameList[g].inviting.username);
                                flag = false;
                            }
                            
                            int count = gameList[g].invitingGuess.Count;

                            if (count == gameList[g].invitedGuess.Count)
                            {
                                // compute who made the closer guess
                                int deltaInviting = Math.Abs(rand - gameList[g].invitingGuess[count - 1]);
                                int deltaInvited = Math.Abs(rand - gameList[g].invitedGuess[count - 1]);

                                if (deltaInviting == deltaInvited) // tie
                                {
                                    SendMessage("Tie " + activeList[j].username, n);
                                    SendMessage("Tie " + activeList[i].username, activeList[j].socket);
                                }

                                else if (deltaInviting < deltaInvited) // inviting user wins the round
                                {
                                    gameList[g].invitingScore++;

                                    if (flag) // inviting == user at index i
                                    {
                                        SendMessage("SmallWin " + activeList[j].username + " " + gameList[g].invitingScore + " " + gameList[g].invitedScore, n);
                                        SendMessage("SmallLose " + activeList[i].username + " " + gameList[g].invitedScore + " " + gameList[g].invitingScore, activeList[j].socket);
                                    }

                                    else
                                    {
                                        SendMessage("SmallLose " + activeList[j].username + " " + gameList[g].invitedScore + " " + gameList[g].invitingScore, n);
                                        SendMessage("SmallWin " + activeList[i].username + " " + gameList[g].invitingScore + " " + gameList[g].invitedScore, activeList[j].socket);
                                    }
                                }

                                else // invited user wins the round
                                {
                                    gameList[g].invitedScore++;

                                    if (flag) // inviting == user at index i
                                    {
                                        SendMessage("SmallLose " + activeList[j].username + " " + gameList[g].invitingScore.ToString() + " " + gameList[g].invitedScore.ToString(), n);
                                        SendMessage("SmallWin " + activeList[i].username + " " + gameList[g].invitedScore.ToString() + " " + gameList[g].invitingScore.ToString(), activeList[j].socket);
                                    }

                                    else
                                    {
                                        SendMessage("SmallWin " + activeList[j].username + " " + gameList[g].invitedScore.ToString() + " " + gameList[g].invitingScore.ToString(), n);
                                        SendMessage("SmallLose " + activeList[i].username + " " + gameList[g].invitingScore.ToString() + " " + gameList[g].invitedScore.ToString(), activeList[j].socket);
                                    }
                                }

                                // end the game if one of the players has won two rounds
                                if (gameList[g].invitingScore == 2)
                                {
                                    if (flag) // inviting == user at index i
                                    {
                                        SendMessage("Lost " + activeList[i].username, activeList[j].socket);
                                    }
                                    
                                    else // inviting == user at index j
                                    {
                                        SendMessage("Lost " + activeList[j].username, n);
                                    }
                                }

                                else if (gameList[g].invitedScore == 2)
                                {
                                    if (flag) // invited == user at index j
                                    {
                                        SendMessage("Lost " + activeList[j].username, n);
                                    }

                                    else // invited == user at index i
                                    {
                                        SendMessage("Lost " + activeList[i].username, activeList[j].socket);
                                    }
                                }

                            }
                        }
                        
                        catch
                        {
                            this.Invoke((MethodInvoker)delegate ()
                            {
                                richTextBox1.AppendText("Something went wrong during the game.\r\n");
                            });
                        }
                    }
                }

                catch // disconnect the user in case anything fails
                {
                    User userToDisconnect = activeList.Find(User => User.socket == n);

                    connected = false;
                    disconnect(n);

                    this.Invoke((MethodInvoker)delegate ()
                    {
                        richTextBox1.AppendText(userToDisconnect.username + " has disconnected.\r\n");
                    });
                }
            }
        }

        void BroadCastMessage(string message)
        {
            try
            {
                string sending = "Broadcast " + message;
                byte[] buffer = Encoding.Default.GetBytes(sending);

                // broadcast the message to all clients
                foreach (Socket s in socketList)
                {
                    s.Send(buffer);
                }

                richTextBox1.AppendText("Server: " + message + "\r\n");
            }
            catch
            {
                this.Invoke((MethodInvoker)delegate ()
                {
                    richTextBox1.AppendText("Your message could not be sent.\r\n");
                });
            }
        }

        void SendMessage(string message, Socket n) // send the message to the client with the given socket
        {
            try
            {
                byte[] buffer = Encoding.Default.GetBytes(message);
                n.Send(buffer);
            }

            catch        
            {
                this.Invoke((MethodInvoker)delegate ()
                {
                    richTextBox1.AppendText("Your message could not be sent.\r\n");
                });
            }
            
        }

        void RefreshLobby() // refresh the lobby list for all active users
        {
            try
            {
                string listMessage = "List";

                foreach (User u in activeList)
                {
                    listMessage += " " + u.username +" (" + u.score + ")"; // get the active users
                }

                foreach (Socket s in socketList)
                {
                    SendMessage(listMessage, s); // send it to the users
                }
            }

            catch
            {
                this.Invoke((MethodInvoker)delegate ()
                {
                    richTextBox1.AppendText("Lobby list could not be refreshed.\r\n");
                });
            }
        }

        void disconnect(Socket n)
        {
            // find the user
            int i = activeList.FindIndex(User => User.socket == n);
            User userToDisconnect = activeList[i];

            // find the game and the opponent, if applicable
            int j;
            int k = gameList.FindIndex(Game => (Game.inviting.username == userToDisconnect.username || Game.invited.username == userToDisconnect.username));

            if (k != -1)
            {
                Game item = gameList[k];

                if (item.invited.username != activeList[i].username)
                {
                    j = activeList.FindIndex(User => User.username == item.invited.username);
                }

                else
                {
                    j = activeList.FindIndex(User => User.username == item.inviting.username);
                }

                gameList.Remove(item); // remove the game that the disconnecting user is in

                User against = activeList[j];

                if (against.isPlaying)
                {
                    SendMessage("Won " + userToDisconnect.username, against.socket); // notify the opponent
                    against.isPlaying = false;
                    against.score++;

                    this.Invoke((MethodInvoker)delegate()
                    {
                        richTextBox1.AppendText(against.username + " has won a game against " + userToDisconnect.username + ".\r\n");
                    });
                }

                else
                {
                    SendMessage("Offline " + userToDisconnect.username, against.socket); // notify the possible opponent
                }
            }

            // find and remove the user from the actives list
            activeList.Remove(userToDisconnect);
            socketList.Remove(n);

            SendMessage("Disconnect", n); // let user it is okay to disconnect
        }
    }
}