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

namespace _408p1_client
{
    public partial class Form1 : Form
    {
        // initiating the client socket
        static bool connected = false;
        static Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        public Form1()
        {
            InitializeComponent();
            this.Text = "Client";
            button2.Enabled = false;
            button6.Enabled = false;
            button4.Enabled = false;
            button3.Enabled = false;
            button5.Enabled = false;
            button7.Enabled = false;
            numericUpDown1.Enabled = false;
            this.FormClosing += new FormClosingEventHandler(Form1_Closing); // so that Form1_Closing function works properly
        }

        // 'Connect' button
        private void button1_Click(object sender, EventArgs e)
        {
            // get the necessary input to connect
            string serverIP = textBox1.Text;
            int serverPort = Convert.ToInt32(textBox2.Text);
            string username = textBox3.Text;

            // check if the username is valid
            if (string.IsNullOrWhiteSpace(username))
            {
                richTextBox1.AppendText("You need a username.\r\n");
            }

            else if (username.Contains(" "))
            {
                richTextBox1.AppendText("You cannot have space in your username.\r\n");
            }

            else
            {
                try
                {
                    /* connect to the server */
                    client.Connect(serverIP, serverPort);
                    richTextBox1.AppendText("Connecting to " + serverIP + "/" + serverPort + "\r\n");
                    sendMessage("Username " + username);

                    Thread thrReceive;
                    thrReceive = new Thread(new ThreadStart(Receive)); // started to receiving the data
                    thrReceive.IsBackground = true; // with this option, threads will automatically shut down
                    thrReceive.Start();
                }

                catch
                {
                    client.Dispose(); // dispose the current socket and get a new one in case the attempt to connect fails
                    client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    richTextBox1.AppendText("Connection has failed.\r\n");
                }
            }
        }
        
        // 'Disconnect' button
        private void button2_Click(object sender, EventArgs e)
        {
            disconnect();
        }

        // 'Invite' button
        private void button6_Click(object sender, EventArgs e)
        {   
            string invitation = "Invite ";

            try
            {
                string userInvited = listBox1.GetItemText(listBox1.SelectedItem);
                userInvited = userInvited.Split(' ')[0];
                invitation += userInvited;

                if (userInvited == "")
                {
                    richTextBox1.AppendText("Please select a valid user from the list.\r\n");
                }

                else if (userInvited == textBox3.Text)
                {
                    richTextBox1.AppendText("You cannot invite yourself.\r\n");
                }

                else
                {
                    sendMessage(invitation);
                    richTextBox1.AppendText("You have invited " + userInvited + " to play.\r\n");
                    button6.Enabled = false;
                }
            }
            catch
            {
                richTextBox1.AppendText("Your invitation could not be sent.\r\n");
            }
        }

        // 'Surrender' button
        private void button3_Click(object sender, EventArgs e)
        {
            button3.Enabled = false;
            button7.Enabled = false;
            numericUpDown1.Enabled = false;
            button6.Enabled = true;
            richTextBox1.AppendText("You have surrendered.\r\n");
            sendMessage("Lost");
        }

        // 'Send' button
        private void button4_Click(object sender, EventArgs e)
        {
            string message = textBox4.Text;

            if (!string.IsNullOrWhiteSpace(message)) // do not send empty messages
            {
                try
                {
                    sendMessage("Message " + message);
                    richTextBox1.AppendText(textBox3.Text + ": " + message + "\r\n");
                }

                catch
                {
                    richTextBox1.AppendText("Your message could not be sent.\r\n");
                }
            }

            textBox4.Clear();
        }

        // 'Refresh' button
        private void button5_Click(object sender, EventArgs e)
        {
            sendMessage("List");
        }

        // 'Guess' button
        private void button7_Click(object sender, EventArgs e)
        {
            string toSend = "Guess " + numericUpDown1.Value;
            sendMessage(toSend);
            this.Invoke((MethodInvoker)delegate ()
            {
                button7.Enabled = false;
                richTextBox1.AppendText("You have made your guess Waiting for the opponent's turn.\r\n");
            });
        }

        // form closing event: disconnect properly from the server
        private void Form1_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            disconnect();
        }

        // receiving data from the server
        private void Receive()
        {
            connected = true;

            while (connected)
            {
                try
                {
                    byte[] buffer = new byte[64]; // buffer to process received commands

                    if (client.Receive(buffer) <= 0)
                    {
                        throw new SocketException();
                    }

                    // processing the received buffer into an array of strings
                    string bufferMessage = Encoding.Default.GetString(buffer);
                    bufferMessage = bufferMessage.Substring(0, bufferMessage.IndexOf("\0"));
                    string[] arrayBuffer = bufferMessage.Split(' ');

                    if (arrayBuffer[0] == "Success") // initiate the interface
                    { 
                        this.Invoke((MethodInvoker)delegate ()
                        {
                            textBox1.ReadOnly = true;
                            textBox2.ReadOnly = true;
                            textBox3.ReadOnly = true;
                            button1.Enabled = false;
                            button2.Enabled = true;
                            button6.Enabled = true;
                            button4.Enabled = true;
                            button5.Enabled = true;
                        
                            richTextBox1.AppendText("Connection has succeeded.\r\n");
                        });
                    }

                    else if (arrayBuffer[0] == "Namefail") // username already exists
                    {
                        connected = false;
                        client.Dispose();
                        client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                        this.Invoke((MethodInvoker)delegate ()
                        {
                            richTextBox1.AppendText("This username already exists.\r\n");
                        });
                    }

                    else if (arrayBuffer[0] == "Broadcast") // server broadcasts a message
                    {
                        string message = "";
                        for (int i = 1; i < arrayBuffer.Length; i++) // get the message from the array
                        {
                            message += " " + arrayBuffer[i];
                        }

                        this.Invoke((MethodInvoker)delegate ()
                        {
                            richTextBox1.AppendText("Server:" + message + "\r\n");
                        });
                    }

                    else if (arrayBuffer[0] == "List") // server sends the list of active users
                    {
                        this.Invoke((MethodInvoker)delegate ()
                        {
                            listBox1.Items.Clear();
                            for (int i = 1; i < arrayBuffer.Length; i += 2)
                            {
                                listBox1.Items.Add(arrayBuffer[i] + " " + arrayBuffer[i+1]);
                            }
                        });
                    }

                    else if (arrayBuffer[0] == "Disconnect") // server approves the disconnection
                    {
                        client.Dispose();
                        client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        connected = false;

                        this.Invoke((MethodInvoker)delegate ()
                        {
                            richTextBox1.AppendText("Disconnected from the server.\r\n");
                            listBox1.Items.Clear();
                        });
                    }

                    else if (arrayBuffer[0] == "Invited") // server notifies that the client is invited to play
                    {
                        this.Invoke((MethodInvoker)delegate ()
                        {
                            button6.Enabled = false;
                        });

                        string userInviting = arrayBuffer[1];
                        string question = userInviting + " has invited you to play. Do you accept the challenge?";
                        DialogResult dialogResult = MessageBox.Show(question, "Invitation for " + textBox3.Text, MessageBoxButtons.YesNo); // pop up to ask the client 
                        string toSend = "";

                        if (dialogResult == DialogResult.Yes) // client accepts the invitation
                        {
                            toSend = "Accept " + userInviting;
                            this.Invoke((MethodInvoker)delegate ()
                            {
                                button3.Enabled = true;
                                richTextBox1.AppendText("You have accepted the invitation sent by " + userInviting + " to play.\r\n");
                            });
                        }

                        else if (dialogResult == DialogResult.No) // client rejects the invitation
                        {
                            toSend = "Reject " + userInviting;
                            this.Invoke((MethodInvoker)delegate ()
                            {
                                button6.Enabled = true;
                                richTextBox1.AppendText("You have rejected the invitation sent by " + userInviting + " to play.\r\n");
                            });
                        }

                        sendMessage(toSend); // notify the server
                    }
                    
                    else if (arrayBuffer[0] == "Offline") // the possible opponent went offline
                    {
                        string userInvited = arrayBuffer[1];
                        this.Invoke((MethodInvoker)delegate ()
                        {
                            button6.Enabled = true;
                            richTextBox1.AppendText(userInvited + " is not online.\r\n");
                        });
                    }

                    else if (arrayBuffer[0] == "BusyPlaying") // invited user is playing with someone else
                    {
                        string userInvited = arrayBuffer[1];
                        this.Invoke((MethodInvoker)delegate ()
                        {
                            button6.Enabled = true;
                            richTextBox1.AppendText(userInvited + " is already in a game.\r\n");
                        });
                    }
                   
                    else if (arrayBuffer[0] == "BusyInvited") // invited user is processing another invitation
                    {
                        
                        string userInvited = arrayBuffer[1];
                        this.Invoke((MethodInvoker)delegate()
                        {
                            button6.Enabled = true;
                            richTextBox1.AppendText(userInvited + " is already invited to a game.\r\n");
                        });
                    }

                    else if (arrayBuffer[0] == "Accepted") // the invitation is accepted
                    {
                        string userInvited = arrayBuffer[1];
                        this.Invoke((MethodInvoker)delegate ()
                        {
                            button6.Enabled = false;
                            button3.Enabled = true;
                            richTextBox1.AppendText(userInvited + " has accepted your invitation to play.\r\n");
                        });

                        string toSend = "Game " + userInvited;
                        sendMessage(toSend);
                    }

                    else if (arrayBuffer[0] == "Rejected") // the invitation is rejected
                    {
                        string userInvited = arrayBuffer[1];
                        this.Invoke((MethodInvoker)delegate ()
                        {
                            button6.Enabled = true;
                            richTextBox1.AppendText(userInvited + " has rejected your invitation to play.\r\n");
                        });
                    }

                    else if (arrayBuffer[0] == "Game") // the game has started
                    {
                        string userInvited = arrayBuffer[1];
                        this.Invoke((MethodInvoker)delegate ()
                        {
                            numericUpDown1.Enabled = true;
                            button7.Enabled = true;
                            richTextBox1.AppendText("The game is on against " + userInvited + ".\r\n");
                        });
                    }

                    else if (arrayBuffer[0] == "Won") // the client wins the game
                    {
                        string against = arrayBuffer[1];
                        this.Invoke((MethodInvoker)delegate ()
                        {
                            richTextBox1.AppendText("You have won the game against " + against + ".\r\n");
                            button3.Enabled = false;
                            button6.Enabled = true;
                            button7.Enabled = false;
                            numericUpDown1.Enabled = false;
                        });
                    }

                    else if (arrayBuffer[0] == "Lost") // the client loses the game
                    {
                        sendMessage("Lost");
                        string against = arrayBuffer[1];
                        this.Invoke((MethodInvoker)delegate ()
                        {
                            richTextBox1.AppendText("You have lost the game against " + against + ".\r\n");
                            button3.Enabled = false;
                            button6.Enabled = true;
                            button7.Enabled = false;
                            numericUpDown1.Enabled = false;
                        });
                    }

                    else if (arrayBuffer[0] == "SmallWin") // the client wins a round
                    {
                        string against = arrayBuffer[1];
                        string yourScore = arrayBuffer[2];
                        string oppScore = arrayBuffer[3];
                        
                        this.Invoke((MethodInvoker)delegate ()
                        {
                            richTextBox1.AppendText("You made a closer guess. You " + yourScore + ":" + oppScore + " " + against + "\r\n");
                            button7.Enabled = true;
                            numericUpDown1.Enabled = true;
                        });
                    }

                    else if (arrayBuffer[0] == "SmallLose") // the client loses a round
                    {
                        string against = arrayBuffer[1];
                        string yourScore = arrayBuffer[2];
                        string oppScore = arrayBuffer[3];

                        this.Invoke((MethodInvoker)delegate ()
                        {
                            richTextBox1.AppendText("Your opponent made a closer guess. You " + yourScore + ":" + oppScore + " " + against + "\r\n");
                            button7.Enabled = true;
                            numericUpDown1.Enabled = true;
                        });
                    }

                    else if (arrayBuffer[0] == "Tie") // the game results in tie
                    {
                        string against = arrayBuffer[1];

                        this.Invoke((MethodInvoker)delegate ()
                        {
                            richTextBox1.AppendText("Your opponent's guess is as good as yours. Tie.\r\n");
                            button7.Enabled = true;
                            numericUpDown1.Enabled = true;
                        });
                    }
                }

                catch // in case server side terminates the connection
                {
                    // dispose the current socket and get a new one in case the attempt to connect fails
                    disconnect();
                    client.Dispose();
                    client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    connected = false;
                }
            }
        }
        
        private void sendMessage(string message) // send the given string to the server
        {
            byte[] buffer = Encoding.Default.GetBytes(message);
            client.Send(buffer);
        }
        
        private void disconnect()
        {
            try
            {
                // change the interface to the disconnected state
                textBox1.ReadOnly = false;
                textBox2.ReadOnly = false;
                textBox3.ReadOnly = false;
                button1.Enabled = true;
                button2.Enabled = false;
                button6.Enabled = false;
                button4.Enabled = false;
                button5.Enabled = false;
                button3.Enabled = false;
                button7.Enabled = false;
                numericUpDown1.Enabled = false;
                listBox1.Items.Clear();
                sendMessage("Disconnect " + textBox3.Text); // notify server of disconnection
            }

            catch
            {
                listBox1.Items.Clear();
                richTextBox1.AppendText("Connection has been terminated.\r\n");
            }
        }
    }
}
