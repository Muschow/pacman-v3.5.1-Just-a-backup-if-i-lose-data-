using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public class MazeGenerator : TileMap
{
    const int path = 0; //maybe make these constants global variables?
    const int wall = 1;
    const int node = 0; //0 blank 1 green //2 green 3 blank
    [Export] public int width = 31;
    [Export] public int height = 38; //was originally 19, maybe do 19*3?
    //[Export] private float tickDuration = 0.0f; //was 0.03f
    [Export] public int mazeOriginY = 0; //maybe make this or mazeStartLoc a global variable
    private Node2D gameScr;
    private TileMap nodeTilemap;
    private bool generationComplete = false;
    //public int mazesOnScreen = 0; //have the ghost maze wall decrease this number when passing a maze chunk mazeOnScreen -= 1; //maybe make public

    //int[,] mazeArray = new int[height*maxNumMazes,width];
    static Vector2 north = new Vector2(0, -1); //maybe turn these NESW vectors into global variables or just replace with Vector2.Up etc
    static Vector2 east = new Vector2(1, 0);
    static Vector2 south = new Vector2(0, 1);
    static Vector2 west = new Vector2(-1, 0);
    Vector2[] directions = new Vector2[] { north, east, south, west };
    List<Vector2> visited = new List<Vector2>();
    Stack<Vector2> rdfStack = new Stack<Vector2>();

    List<Vector2> wallEdgeList = new List<Vector2>();
    private int backtrackCount = 0;
    private void CorrectMazeSize()
    {
        if (width % 2 != 1)
        {
            width -= 1;
        }
        if (height % 2 != 1)
        {
            height -= 1;
        }
        GD.Print("width " + width);
        GD.Print("height " + height);
    }

    private void CreateStartingGrid()
    {
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                //wall tile edges
                if ((i == 0 && j != 0) || (i == width - 1 && j != 0) || (j == height - 1)) //j != 0 stuff removes the entire top layer
                {
                    SetCell(i, j + mazeOriginY, wall);

                    Vector2 wallEdge = new Vector2(i, j + mazeOriginY);
                    wallEdgeList.Add(wallEdge);
                }
                //alternating wall tiles
                else if ((i % 2 == 0 || j % 2 == 0) && (j != 0)) //again, j!=0 removes the top layer so that the next maze can slot into it
                {
                    SetCell(i, j + mazeOriginY, wall);
                }
                //path tiles
                else
                {
                    SetCell(i, j + mazeOriginY, path);
                }
            }
        }
    }

    private void FixDeadEnds(Vector2 currentV)
    {
        bool complete = false;

        for (int i = 0; i < directions.Length; i++)
        {
            if (!complete)
            {
                Vector2 newCell = new Vector2(currentV + directions[i]);
                if ((GetCellv(newCell) == wall) && (!wallEdgeList.Contains(newCell)) && (!visited.Contains(newCell)))
                {
                    SetCellv(newCell, path);

                    if (GetCellv(currentV + (directions[i] * 3)) != path)
                    {
                        AddNode(currentV + (directions[i] * 2));
                    }
                    if (GetCellv(currentV + (directions[i] * -1)) != path)
                    {
                        AddNode(currentV);
                    }
                    complete = true;
                }
            }
        }
    }

    private void PrepMazeForJoin(int numHoles) //dependancy on gameScr.Get(mazesOnTheScreen)
    {
        Random rnd = new Random();
        List<Vector2> usedCells = new List<Vector2>();

        for (int i = 1; i < width - 1; i++) //this loop sets the top row of the maze into just paths so it can join with the bottom of another maze
        {
            Vector2 removeCell = new Vector2(i, mazeOriginY);

            if (GetCellv(removeCell + south) == wall)
            {
                SetCellv(removeCell + south, path);
                //GD.Print("set cell+south path");
            }
        }

        GD.Print(mazeOriginY); //debug

        if ((int)gameScr.Get("mazesOnTheScreen") > 0) //If its not the first maze, Add paths to the floor so that you can join to the maze below
        {
            while (usedCells.Count < numHoles * 3) //Maybe change to Math.Round(width/4) <-- [must be >3]
            {
                int cellX = rnd.Next(1, width - 1);
                Vector2 cell = new Vector2(cellX, mazeOriginY + height - 1);
                if ((!usedCells.Contains(cell)) && GetCellv(cell + north) == path)
                {
                    SetCellv(cell, path);

                    //I deliberately made it so there are no nodes joining the 2 mazes. This is as a ghost is instanced on its own maze; if pacman goes between mazes, 
                    //it switches to being chased by the ghosts on that maze. This way, ghosts arent going between mazes and leaving 1 maze empty and 1 maze full.
                    //This also means pacman could exploit the game by just staying in between mazes, however, the ghost maze wall will stop that, forcing pacman to move up.

                    usedCells.Add(cell);
                    usedCells.Add(cell + east);
                    usedCells.Add(cell + west);
                }

            }
        }

    }

    public Vector2 SetSpawn(bool spawnPacman)
    {
        int x = 0;
        int y = 0;

        Random rnd = new Random();
        if (spawnPacman)
        {
            y = height - 2;
            while ((GetCell(x, y) == wall) || (GetCell(x, y + 1) == path))
            {
                x = rnd.Next(1, width);
            }
        }
        else
        {
            while (GetCell(x, y) == wall)
            {
                x = rnd.Next(1, width);
                y = rnd.Next(1, height - 2);
            }
        }
        Vector2 spawnLoc = new Vector2(x, y);
        //GD.Print("spawn" + spawnLoc); //debug

        spawnLoc = new Vector2((spawnLoc * CellSize) + (CellSize / 2));

        //GD.Print("MTWspawnLoc: " + spawnLoc); //debug
        return spawnLoc;
    }

    private void AddNode(Vector2 nodeLocation)
    {
        SetCellv(nodeLocation, -1); //deletes tile so will remove wall node that collides
        nodeTilemap.SetCellv(nodeLocation, node); //turns it into an actual path node tile
    }

    private void rdfInit()
    {
        generationComplete = false;

        CorrectMazeSize();
        CreateStartingGrid();

        //startVector x and y must be odd, between 1+mazeOriginX/Y & height-1 / width-1 
        Vector2 startVector = new Vector2(1, mazeOriginY + 1); //Choose the initial cell,
        //GD.Print("StartV: " + startVector); //debug

        visited.Add(startVector); //Mark initial cell as visited,
        rdfStack.Push(startVector); //and push it to the stack,

        rdfStep();
    }

    private void rdfStep()
    {
        Vector2 prev = new Vector2(0, 0);
        while (!generationComplete)
        {
            Vector2 curr = rdfStack.Pop(); //Pop a cell from the stack and make it a current cell.
            Vector2 next = new Vector2(0, 0);

            bool found = false;

            //check neighbours in random order //N,E,S,W walls instead of their paths, so *2
            Random rnd = new Random();
            var rndDirections = directions.OrderBy(_ => rnd.Next()).ToList(); //found this online, randomly shuffle the list.

            for (int i = 0; i < rndDirections.Count; i++)
            {
                next = 2 * rndDirections[i];
                if (GetCellv(curr + next) == path && (!visited.Contains(curr + next)))
                { //If the current cell has any neighbours which have not been visited,
                    found = true;
                    break; //Choose one of the unvisited neighbours (next),
                }
            }

            if (found)
            {
                if (prev != next)
                {
                    AddNode(curr);
                }
                prev = next;

                rdfStack.Push(curr); //Push the current cell to the stack,
                SetCellv(curr + (next / 2), path); // Remove the wall between the current cell and the chosen cell,
                visited.Add(curr + next); //Mark the chosen cell as visited,
                rdfStack.Push(curr + next); //and push it to the stack.  

                backtrackCount = 0;
            }
            else
            {
                backtrackCount++;
                if (backtrackCount == 1)
                {
                    FixDeadEnds(curr);
                }
            }

            if (rdfStack.Count <= 0)
            { //While stack is not empty, (if stack is empty)
                FixDeadEnds(curr);
                PrepMazeForJoin(7); //dependancy on gameScr.Get(mazesOnTheScreen)
                generationComplete = true;

                GD.Print("Maze Generation Complete!"); //debug
                return;
            }
        }

    }

    //Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {

        nodeTilemap = GetParent().GetNode<Godot.TileMap>("NodeTilemap");
        gameScr = GetNode<Node2D>("/root/Game");
        mazeOriginY = (int)gameScr.Get("mazeStartLoc"); //dependancy, maybe make mazeStartLoc a global variable instead?

        GD.Randomize();
        rdfInit();
        //UpdateDirtyQuadrants(); //maybe get rid of this tbh, not sure if its doing anything. Supposed to force and update to the tilemap if tiles arent updating

    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(float delta)
    {

    }
}
