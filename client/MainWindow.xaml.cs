﻿using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.IO.Ports;

namespace TicTacToeWPF
{
    public partial class MainWindow : Window
    {
        private SerialPort serialPort;
        // true = X / false = 0
        public static bool TURN = true;
        // true = hot seat / false = AI
        public static int MODE = Constants.HOT_SEAT_MODE;

        private List<Button> gameButtons;

        public MainWindow()
        {
            InitializeComponent();

            var comPortSelectionWindow = new ComPortSelectionWindow();
            bool? dialogResult = comPortSelectionWindow.ShowDialog();

            if (dialogResult == true)
            {
                string selectedPort = comPortSelectionWindow.SelectedPort;

                gameButtons = new List<Button>();

                gameButtons.Add(A1);
                gameButtons.Add(A2);
                gameButtons.Add(A3);

                gameButtons.Add(B1);
                gameButtons.Add(B2);
                gameButtons.Add(B3);

                gameButtons.Add(C1);
                gameButtons.Add(C2);
                gameButtons.Add(C3);

                SetupCOM(selectedPort);
            }
            else
            {
                Application.Current.Shutdown();
            }

        }

        private void SetupCOM(string portName)
        {
            serialPort = new SerialPort(portName, 9600);
            serialPort.DataReceived += SerialPort_DataReceived;

            try
            {
                serialPort.Open();
                MessageBox.Show("Serial port " + portName + " opened successfully.");
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show("Access to the port is denied: " + ex.Message);
            }
            catch (IOException ex)
            {
                MessageBox.Show("The port is in an invalid state: " + ex.Message);
            }
            catch (ArgumentException ex)
            {
                MessageBox.Show("The port name does not begin with 'COM' or the file type of the port is not supported: " + ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show("The specified port is already open: " + ex.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error opening COM port: " + ex.Message);
            }

            this.Closed += MainWindow_Closed;
        }

        private void sendStatusToArduino(string message)
        {
            try
            {
                if (serialPort.IsOpen)
                {
                    serialPort.WriteLine(message);
                }
                else
                {
                    MessageBox.Show("Serial port is not open.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error while sending message to Arduino: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            string receivedMessage = serialPort.ReadLine();

            Dispatcher.Invoke(() =>
            {
                MessageBox.Show("Arduino says: " + receivedMessage);
            });
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                serialPort.Close();
            }
        }


        private void gameAction_Click(object sender, RoutedEventArgs e)
        {
            // Y move
            Button pressedButton = (Button)sender;
            if (TURN)
            {
                pressedButton.Content = Constants.X_SYMBOL;
                pressedButton.IsEnabled = false;
                TURN = false;
            }
            else
            {
                pressedButton.Content = Constants.O_SYMBOL;
                pressedButton.IsEnabled = false;
                TURN = true;
            }

            if (checkGameStatus())
            {
                return;
            }

            // Move AI if necessary
            if (MODE == Constants.AI_EASY_MODE || MODE == Constants.AI_HARD_MODE)
            {
                performAiMoveAsync();
                TURN = true;
            }

            checkGameStatus();
        }

        private void onRestartButton_Click(object sender, EventArgs e)
        {
            // Restart the game
            // Activate all buttons and reset their texts
            foreach (Button button in gameButtons)
            {
                button.IsEnabled = true;
                button.Content = "";
            }

            // Reset member variables
            TURN = true;
        }

        private void gameModeComboBox_Click(object sender, SelectionChangedEventArgs e)
        {
            // Get clicked item
            int selection = gameModeComboBox.SelectedIndex;
            MODE = selection;
            if (startButton != null && gameModeComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                startButton.IsEnabled = selectedItem.Content.ToString() == "AI vs AI";
            }
        }

        private async Task performAiMoveAsync()
        {
            if (MODE == Constants.AI_EASY_MODE)
            {
                ArtificialIntelligence.performEasyMove(gameButtons);
            }
            else if (MODE == Constants.AI_HARD_MODE)
            {
                ArtificialIntelligence.performHardMove(gameButtons);
            }
            else if (MODE == Constants.AI_VS_AI_MODE)
            {
                while (MODE == Constants.AI_VS_AI_MODE && !checkGameStatus())
                {
                    if (TURN)
                    {
                        // AI's turn as X
                        ArtificialIntelligence.performMove(gameButtons, Constants.X_SYMBOL);
                        TURN = false;
                    }
                    else
                    {
                        // AI's turn as O
                        ArtificialIntelligence.performMove(gameButtons, Constants.O_SYMBOL);
                        TURN = true;
                    }
                    if (checkGameStatus()) break;
                    await Task.Delay(500);
                }
            }
        }

        private bool checkGameStatus()
        {
            GameStatus status = checkHorizontal();
            if (status.isGameOver())
            {
                disableGame();
                updateStats(status);
                sendStatusToArduino("Player " + status.winner + " has won the game!");
                return true;
            }

            status = checkVertical();
            if (status.isGameOver())
            {
                disableGame();
                updateStats(status);
                sendStatusToArduino("Player " + status.winner + " has won the game!");
                return true;
            }

            status = checkDiagonal();
            if (status.isGameOver())
            {
                disableGame();
                updateStats(status);
                sendStatusToArduino("Player " + status.winner + " has won the game!");
                return true;
            }

            if (checkForTie())
            {
                disableGame();
                updateStats(new GameStatus(true, "", true));
                sendStatusToArduino("The game ended in a tie!");
                return true;
            }

            return false;
        }

        private void disableGame()
        {
            foreach (Button button in gameButtons)
            {
                button.IsEnabled = false;
            }
        }

        private void updateStats(GameStatus status)
        {
            if (status.isGameOver())
            {
                if (status.getWinner().Equals(Constants.X_SYMBOL))
                {
                    int currentWins = Convert.ToInt32(winsX.Content);
                    winsX.Content = "" + (currentWins + 1);
                }
                else if (status.getWinner().Equals(Constants.O_SYMBOL))
                {
                    int currentWins = Convert.ToInt32(winsO.Content);
                    winsO.Content = "" + (currentWins + 1);
                }
                else if (status.isTie())
                {
                    int currentTies = Convert.ToInt32(ties.Content);
                    ties.Content = "" + (currentTies + 1);
                }
            }
        }

        // HELPER
        private GameStatus checkHorizontal()
        {
            bool gameOver = false;
            string winner = "";
            // Horizontal check
            if (A1.Content.Equals(A2.Content)
                && A1.Content.Equals(A3.Content)
                && A2.Content.Equals(A3.Content)
                && !A1.Content.Equals(""))
            {
                // Top row won
                //MessageBox.Show("top row");
                gameOver = true;
                winner = Convert.ToString(A1.Content);
            }
            else if (B1.Content.Equals(B2.Content)
                    && B1.Content.Equals(B3.Content)
                    && B2.Content.Equals(B3.Content)
                    && !B1.Content.Equals(""))
            {
                // Middle row won
                //MessageBox.Show("middle row");
                gameOver = true;
                winner = Convert.ToString(B1.Content);
            }
            else if (C1.Content.Equals(C2.Content)
                    && C1.Content.Equals(C3.Content)
                    && C2.Content.Equals(C3.Content)
                    && !C1.Content.Equals(""))
            {
                // Bottom row won
                //MessageBox.Show("bottom row");
                gameOver = true;
                winner = Convert.ToString(C1.Content);
            }

            return new GameStatus(gameOver, winner, false);
        }

        private GameStatus checkVertical()
        {
            bool gameOver = false;
            string winner = "";
            // Vertical check
            if (A1.Content.Equals(B1.Content)
                && A1.Content.Equals(C1.Content)
                && B1.Content.Equals(C1.Content)
                && !A1.Content.Equals(""))
            {
                // Left column won
                //MessageBox.Show("left column");
                gameOver = true;
                winner = Convert.ToString(A1.Content);
            }
            else if (A2.Content.Equals(B2.Content)
                    && A2.Content.Equals(C2.Content)
                    && B2.Content.Equals(C2.Content)
                    && !A2.Content.Equals(""))
            {
                // Middle column won
                //MessageBox.Show("middle column");
                gameOver = true;
                winner = Convert.ToString(A2.Content);
            }
            else if (A3.Content.Equals(B3.Content)
                    && A3.Content.Equals(C3.Content)
                    && B3.Content.Equals(C3.Content)
                    && !A3.Content.Equals(""))
            {
                // Right column won
                //MessageBox.Show("right column");
                gameOver = true;
                winner = Convert.ToString(A3.Content);
            }

            return new GameStatus(gameOver, winner, false);
        }

        private GameStatus checkDiagonal()
        {
            bool gameOver = false;
            string winner = "";
            // Diagonal check
            if (A1.Content.Equals(B2.Content)
                && A1.Content.Equals(C3.Content)
                && B2.Content.Equals(C3.Content)
                && !A1.Content.Equals(""))
            {
                // Top left to bottom right won
                //MessageBox.Show("tl-br");
                gameOver = true;
                winner = Convert.ToString(A1.Content);
            }
            else if (C1.Content.Equals(B2.Content)
                    && C1.Content.Equals(A3.Content)
                    && B2.Content.Equals(A3.Content)
                    && !C1.Content.Equals(""))
            {
                // Bottom left to top right won
                //MessageBox.Show("bl-tr");
                gameOver = true;
                winner = Convert.ToString(C1.Content);
            }

            return new GameStatus(gameOver, winner, false);
        }

        private bool checkForTie()
        {
            bool tie = true;
            foreach (Button button in gameButtons)
            {
                if (button.IsEnabled == true)
                {
                    tie = false;
                    break;
                }
            }

            return tie;
        }

        private void newButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (Button button in gameButtons)
            {
                button.IsEnabled = true;
                button.Content = "";
            }

            winsX.Content = "0";
            winsO.Content = "0";
            ties.Content = "0";

            TURN = true;
        }

        private void saveButton_Click(object sender, RoutedEventArgs e)
        {

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[Game]");
            sb.AppendLine("Turn=" + (TURN ? "X" : "O"));
            sb.AppendLine("Mode=" + MODE);
            sb.AppendLine("[Board]");
            foreach (Button button in gameButtons)
            {
                sb.AppendLine(button.Name + "=" + button.Content);
            }
            sb.AppendLine("[Stats]");
            sb.AppendLine("WinsX=" + winsX.Content);
            sb.AppendLine("WinsO=" + winsO.Content);
            sb.AppendLine("Ties=" + ties.Content);

            File.WriteAllText("gameState.ini", sb.ToString());
            MessageBox.Show("Game state saved!");
        }

        private void loadButton_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists("gameState.ini"))
            {
                var parser = new IniParser.FileIniDataParser();
                var data = parser.ReadFile("gameState.ini");

                TURN = data["Game"]["Turn"] == "X";
                MODE = int.Parse(data["Game"]["Mode"]);

                foreach (Button button in gameButtons)
                {
                    button.Content = data["Board"][button.Name];
                    button.IsEnabled = string.IsNullOrEmpty(button.Content.ToString());
                }

                winsX.Content = data["Stats"]["WinsX"];
                winsO.Content = data["Stats"]["WinsO"];
                ties.Content = data["Stats"]["Ties"];

                MessageBox.Show("Game state loaded!");
            }
            else
            {
                MessageBox.Show("No save file found!");
            }
        }

        private void startButton_Click(object sender, RoutedEventArgs e)
        {
            if (MODE == Constants.AI_VS_AI_MODE)
            {
                performAiMoveAsync();
            }

        }
    }
}
