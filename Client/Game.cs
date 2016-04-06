using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Client;
using System.Collections.Generic;
using System.Threading;

public class Game
{
    private List<Player>[] Board = new List<Player> [20];    //game board

    private Player winner;                            //winner of the game
    public int BoardSize { get{ return Board.Length; } }
    public int MaxPlayers {  get;  set; }

    public Timer TurnTimer { get; set; }

    private const int TIMER_START_TIME = 16;
    public int TimerTime { get; private set; }

    public bool Over
    {
        get { return Winner != null; }
    }

    public Player Winner
    {
        get { return winner; }
        private set
        {
            if (!Over)
            {
                winner = value;
            }
        }
    }

    object timerLock = new object();
    object timeChangeLock = new object();

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="players">list of all players</param>
    public Game(List<PeerInfo> players)
    {
        ResetTime();
        //initilize board
        for (int i = 0; i < Board.Length; i++)
        {
            Board[i] = new List<Player>();
        }

        //set each player location to 0
        // and add all players to first space on board
        foreach (PeerInfo p in players)
        {
            Player player = p.PlayerInfo;
            Board[0].Add(player);
        }

        //set maxplayers
        MaxPlayers = players.Count;

        winner = null;
        Display();
    }

    public int RollDice()
    {
        Random rnd = new Random();
        return rnd.Next(1, 7);
    }

    /// <summary>
    /// Move player to spot
    /// 
    /// </summary>
    /// <param name="current_player"></param>
    /// <param name="diceRolled"></param>
    public void MovePlayer(Player current_player, int diceRolled)
    {
        //get current and new locations
        int cur_loc = current_player.Position;
        int new_loc = cur_loc + diceRolled;
        if (new_loc >= Board.Length - 1 )
        {
            new_loc = Board.Length - 1;
            Winner = current_player;
        }

        
           
        //remove player from board space
        Board[cur_loc].Remove(current_player);
        //update player loction, move and turn
        current_player.Position = new_loc;
        
        Board[new_loc].Add(current_player);

        Display();
    }
    /// <summary>
    /// Update turn number for players by decrementing turn number
    /// </summary>
    public void UpdateTurn()
    {
        foreach (List<Player> players in Board)
        {
            foreach (Player p in players)
            {
                if (p.Turn == 0)
                    p.Turn = MaxPlayers - 1;
                //otherwise decrement turn
                else
                    p.Turn -= 1;
            }

        }
    }

    /// <summary>
    ///  updated player is added to correct place on board, use for reconnecting/syncing game state
    /// </summary>
    /// <param name="p"></param>
    public void UpdatePlayer(Player p)
    {
        foreach(List<Player> players in Board)
        {
            if (players.Remove(p))
            {
                break;
            }
        }

        Board[p.Position].Add(p);
    }

       
    /// <summary>
    /// Remove player from game board
    /// </summary>
    /// <param name="pToBeRemoved"></param>
    public void RemovePlayer(Player pToBeRemoved)
    {
        int removedPlayerTurnNum = pToBeRemoved.Turn;

        if (removedPlayerTurnNum == 0)
        {
            ResetTime();
        }

        Board[pToBeRemoved.Position].Remove(pToBeRemoved);

        foreach (List<Player> players in Board)
        {
            foreach (Player p in players)
            {
                if (p.Turn > removedPlayerTurnNum)
                {
                    p.Turn -= 1;
                }

            }
        }
        MaxPlayers--;

        Display();
    }

    /// <summary>
    /// Display game board
    /// </summary>
    public void Display()
    {
        string display = "\n-------------------------------\n";

        foreach (List<Player> players in Board)
        {
            display += "[" + (Array.IndexOf(Board, players) + 1) + "] ";
            foreach (Player p in players)
            {
                display += "(" + p.PlayerId + ")" + p.Name + " ";
            }
            display += "\n";
        }
        display += "-------------------------------\n";

       

        Console.WriteLine(display);

        if (Over)
        {
            Console.WriteLine("\n---------------------------------");
            Console.WriteLine("The Winner is (" + winner.PlayerId + ")" + winner.Name);
            Console.WriteLine("---------------------------------");
        }

    }
    /// <summary>
    /// Pause timer
    /// </summary>
    public void PauseTimer()
    {
        lock (timerLock)
        {
            try { 
                TurnTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
            catch (ObjectDisposedException)
            {
                Console.Write("TIMER DISPOSED");
            }
        }
       
    }
    /// <summary>
    /// Start timer
    /// </summary>
    public void StartTimer()
    {
        lock (timerLock)
        {
            try
            {
                TurnTimer.Change(1000, 1000);
            }
            catch (ObjectDisposedException)
            {
                Console.Write("TIMER DISPOSED");
            }
        }
    }
    /// <summary>
    /// Reset time back to MAX time
    /// </summary>
    public void ResetTime()
    {
        SetTime(TIMER_START_TIME);
    }

    /// <summary>
    /// Set timer to specific value
    /// </summary>
    /// <param name="t"></param>
    public void SetTime(int t)
    {
        lock (timeChangeLock)
        {
            TimerTime = t;
        }
    }


}

