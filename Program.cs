using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

// Entity base class with EF Core support
abstract class Entity
{
    [Key]
    public int Id { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public char Symbol { get; set; }
    public string Name { get; set; }
    public bool BlocksMovement { get; set; }
    public bool BlocksVision { get; set; }
    
    [ForeignKey("GameWorld")]
    public int GameWorldId { get; set; }
    
    protected Entity() { }
    
    protected Entity(int x, int y, char symbol, string name, bool blocksMovement = false, bool blocksVision = false)
    {
        X = x;
        Y = y;
        Symbol = symbol;
        Name = name;
        BlocksMovement = blocksMovement;
        BlocksVision = blocksVision;
    }
}

class Player : Entity
{
    public int Health { get; set; }
    public int MaxHealth { get; set; }
    public int Level { get; set; }
    public int Experience { get; set; }
    
    public Player() : base() { }
    
    public Player(int x, int y) : base(x, y, '@', "Player", true, false)
    {
        Health = 100;
        MaxHealth = 100;
        Level = 1;
        Experience = 0;
    }
}

class Enemy : Entity
{
    public int Health { get; set; }
    public int Damage { get; set; }
    
    public Enemy() : base() { }
    
    public Enemy(int x, int y, string name) : base(x, y, 'E', name, true, false)
    {
        Health = 30;
        Damage = 10;
    }
}

class Item : Entity
{
    public string ItemType { get; set; }
    public int Value { get; set; }
    
    public Item() : base() { }
    
    public Item(int x, int y, char symbol, string name, string itemType, int value = 0) 
        : base(x, y, symbol, name, false, false)
    {
        ItemType = itemType;
        Value = value;
    }
}

class Wall : Entity
{
    public string WallType { get; set; }
    
    public Wall() : base() { }
    
    public Wall(int x, int y, string wallType = "Stone") 
        : base(x, y, '#', $"{wallType} Wall", true, true)
    {
        WallType = wallType;
    }
}

class Door : Entity
{
    public bool IsOpen { get; set; }
    
    public Door() : base() { }
    
    public Door(int x, int y, bool isOpen = false) 
        : base(x, y, isOpen ? '/' : '+', isOpen ? "Open Door" : "Closed Door", !isOpen, false)
    {
        IsOpen = isOpen;
    }
    
    public void Toggle()
    {
        IsOpen = !IsOpen;
        Symbol = IsOpen ? '/' : '+';
        Name = IsOpen ? "Open Door" : "Closed Door";
        BlocksMovement = !IsOpen;
    }
}

class Tile
{
    [Key]
    public int Id { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public char Symbol { get; set; }
    public bool IsWalkable { get; set; }
    public string TileType { get; set; }
    
    [ForeignKey("GameWorld")]
    public int GameWorldId { get; set; }
    
    public Tile() { }
    
    public Tile(int x, int y, char symbol, bool isWalkable, string tileType)
    {
        X = x;
        Y = y;
        Symbol = symbol;
        IsWalkable = isWalkable;
        TileType = tileType;
    }
}

class GameWorldData
{
    [Key]
    public int Id { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string Name { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastSavedAt { get; set; }
    
    public virtual ICollection<Tile> Tiles { get; set; }
    public virtual ICollection<Entity> Entities { get; set; }
    
    public GameWorldData()
    {
        Tiles = new List<Tile>();
        Entities = new List<Entity>();
        CreatedAt = DateTime.Now;
        LastSavedAt = DateTime.Now;
    }
}

class GameDbContext : DbContext
{
    public DbSet<GameWorldData> GameWorlds { get; set; }
    public DbSet<Tile> Tiles { get; set; }
    public DbSet<Entity> Entities { get; set; }
    public DbSet<Player> Players { get; set; }
    public DbSet<Enemy> Enemies { get; set; }
    public DbSet<Item> Items { get; set; }
    public DbSet<Wall> Walls { get; set; }
    public DbSet<Door> Doors { get; set; }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=roguelike.db");
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Entity>()
            .HasDiscriminator<string>("EntityType")
            .HasValue<Player>("Player")
            .HasValue<Enemy>("Enemy")
            .HasValue<Item>("Item")
            .HasValue<Wall>("Wall")
            .HasValue<Door>("Door");
    }
}

class GameWorld
{
    public int Width { get; private set; }
    public int Height { get; private set; }
    public int WorldId { get; private set; }
    
    private Tile[,] tiles;
    private List<Entity> entities;
    public Player Player { get; private set; }
    
    private List<string> messageBuffer;
    private const int MaxMessages = 5;
    
    public IReadOnlyList<string> Messages => messageBuffer.AsReadOnly();
    
    public GameWorld(int width, int height)
    {
        Width = width;
        Height = height;
        tiles = new Tile[height, width];
        entities = new List<Entity>();
        messageBuffer = new List<string>();
        
        AddMessage("Welcome to the dungeon!");
        
        InitializeWorld();
    }
    
    private GameWorld(GameWorldData data)
    {
        Width = data.Width;
        Height = data.Height;
        WorldId = data.Id;
        tiles = new Tile[data.Height, data.Width];
        entities = new List<Entity>();
        messageBuffer = new List<string>();
        
        AddMessage("Game loaded.");
        
        foreach (var tile in data.Tiles)
        {
            tiles[tile.Y, tile.X] = tile;
        }
        
        entities.AddRange(data.Entities);
        Player = entities.OfType<Player>().FirstOrDefault();
    }
    
    public void AddMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;
            
        messageBuffer.Insert(0, message);
        
        if (messageBuffer.Count > MaxMessages)
        {
            messageBuffer.RemoveAt(messageBuffer.Count - 1);
        }
    }
    
    private void InitializeWorld()
    {
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                tiles[y, x] = new Tile(x, y, '.', true, "Floor");
            }
        }
        
        CreateWallBox(0, 0, Width, Height);
        
        CreateRoom(5, 3, 10, 6);
        CreateRoom(20, 8, 15, 8);
        CreateRoom(10, 12, 8, 5);
        CreateRoom(35, 5, 12, 10);
        CreateRoom(50, 15, 10, 8);
        
        AddDoor(14, 5);
        AddDoor(20, 12);
        AddDoor(35, 9);
        
        // Add a cluster of doors for testing
        AddDoor(15, 10);
        AddDoor(16, 10);
        AddDoor(17, 10);
        AddDoor(15, 11);
        AddDoor(17, 11);
        AddDoor(15, 12);
        AddDoor(16, 12);
        AddDoor(17, 12);
        
        Player = new Player(16, 11);
        entities.Add(Player);
        
        entities.Add(new Enemy(25, 12, "Goblin"));
        entities.Add(new Enemy(28, 10, "Orc"));
        entities.Add(new Enemy(38, 8, "Skeleton"));
        entities.Add(new Enemy(53, 18, "Troll"));
        
        entities.Add(new Item(30, 10, 'T', "Gold Coins", "Treasure", 50));
        entities.Add(new Item(13, 14, '$', "Silver Key", "Key", 0));
        entities.Add(new Item(7, 5, '!', "Health Potion", "Potion", 25));
        entities.Add(new Item(40, 7, '*', "Magic Scroll", "Scroll", 100));
        entities.Add(new Item(55, 19, '&', "Ancient Artifact", "Artifact", 500));
    }
    
    private void CreateWallBox(int x, int y, int width, int height)
    {
        for (int dy = 0; dy < height; dy++)
        {
            for (int dx = 0; dx < width; dx++)
            {
                int px = x + dx;
                int py = y + dy;
                
                if (px < 0 || px >= Width || py < 0 || py >= Height)
                    continue;
                
                if (dx == 0 || dx == width - 1 || dy == 0 || dy == height - 1)
                {
                    entities.Add(new Wall(px, py));
                }
            }
        }
    }
    
    private void CreateRoom(int x, int y, int width, int height)
    {
        CreateWallBox(x, y, width, height);
        
        for (int dy = 1; dy < height - 1; dy++)
        {
            for (int dx = 1; dx < width - 1; dx++)
            {
                int px = x + dx;
                int py = y + dy;
                
                if (px >= 0 && px < Width && py >= 0 && py < Height)
                {
                    tiles[py, px] = new Tile(px, py, '.', true, "Floor");
                }
            }
        }
    }
    
    private void AddDoor(int x, int y)
    {
        var wall = entities.OfType<Wall>().FirstOrDefault(w => w.X == x && w.Y == y);
        if (wall != null)
        {
            entities.Remove(wall);
        }
        entities.Add(new Door(x, y, false));
    }
    
    public Tile GetTile(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return null;
        return tiles[y, x];
    }
    
    public IEnumerable<Entity> GetEntitiesAt(int x, int y)
    {
        return entities.Where(e => e.X == x && e.Y == y);
    }
    
    public IEnumerable<Entity> GetAllEntities()
    {
        return entities.AsReadOnly();
    }
    
    public IEnumerable<Entity> GetEntitiesInArea(int x, int y, int width, int height)
    {
        return entities.Where(e => 
            e.X >= x && e.X < x + width && 
            e.Y >= y && e.Y < y + height);
    }
    
    public bool CanMoveTo(int x, int y)
    {
        Tile tile = GetTile(x, y);
        if (tile == null || !tile.IsWalkable)
            return false;
        
        return !entities.Any(e => e.X == x && e.Y == y && e.BlocksMovement);
    }
    
    public void ToggleDoorInDirection(int dx, int dy)
    {
        int checkX = Player.X + dx;
        int checkY = Player.Y + dy;
        
        var door = GetEntitiesAt(checkX, checkY).OfType<Door>().FirstOrDefault();
        if (door != null)
        {
            door.Toggle();
            AddMessage(door.IsOpen ? "You open the door." : "You close the door.");
        }
        else
        {
            AddMessage("There is no door in that direction.");
        }
    }
    
    public void MovePlayer(int dx, int dy)
    {
        int newX = Player.X + dx;
        int newY = Player.Y + dy;
        
        var entitiesAtTarget = GetEntitiesAt(newX, newY).ToList();
        
        // Check for closed door - open it but don't move
        var door = entitiesAtTarget.OfType<Door>().FirstOrDefault();
        if (door != null && !door.IsOpen)
        {
            door.Toggle();
            AddMessage("You open the door.");
            return;
        }
        
        // Check for enemy - attack instead of moving
        var enemy = entitiesAtTarget.OfType<Enemy>().FirstOrDefault();
        if (enemy != null)
        {
            enemy.Health -= 20;
            AddMessage($"You attack the {enemy.Name}! Dealt 20 damage.");
            if (enemy.Health <= 0)
            {
                entities.Remove(enemy);
                AddMessage($"The {enemy.Name} is defeated!");
                Player.Experience += 10;
            }
            return;
        }
        
        // Try to move to the new position
        if (CanMoveTo(newX, newY))
        {
            Player.X = newX;
            Player.Y = newY;
            
            // Pick up items after moving
            var item = entitiesAtTarget.OfType<Item>().FirstOrDefault();
            if (item != null)
            {
                entities.Remove(item);
                AddMessage($"You picked up {item.Name}.");
                if (item.ItemType == "Potion")
                {
                    Player.Health = Math.Min(Player.Health + item.Value, Player.MaxHealth);
                    AddMessage($"Restored {item.Value} health!");
                }
            }
        }
        else
        {
            AddMessage("You cannot move there.");
        }
    }
    
    public void SaveToDatabase(string worldName)
    {
        using (var context = new GameDbContext())
        {
            context.Database.EnsureCreated();
            
            GameWorldData worldData;
            
            if (WorldId > 0)
            {
                worldData = context.GameWorlds
                    .Include(w => w.Tiles)
                    .Include(w => w.Entities)
                    .FirstOrDefault(w => w.Id == WorldId);
                
                if (worldData != null)
                {
                    context.Tiles.RemoveRange(worldData.Tiles);
                    context.Entities.RemoveRange(worldData.Entities);
                }
            }
            else
            {
                worldData = new GameWorldData
                {
                    Name = worldName,
                    Width = Width,
                    Height = Height
                };
                context.GameWorlds.Add(worldData);
            }
            
            worldData.LastSavedAt = DateTime.Now;
            
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    var tile = tiles[y, x];
                    tile.GameWorldId = worldData.Id;
                    worldData.Tiles.Add(tile);
                }
            }
            
            foreach (var entity in entities)
            {
                entity.GameWorldId = worldData.Id;
                worldData.Entities.Add(entity);
            }
            
            context.SaveChanges();
            WorldId = worldData.Id;
        }
    }
    
    public static GameWorld LoadFromDatabase(int worldId)
    {
        using (var context = new GameDbContext())
        {
            var worldData = context.GameWorlds
                .Include(w => w.Tiles)
                .Include(w => w.Entities)
                .FirstOrDefault(w => w.Id == worldId);
            
            if (worldData == null)
                return null;
            
            return new GameWorld(worldData);
        }
    }
    
    public static List<(int Id, string Name, DateTime LastSaved)> GetSavedWorlds()
    {
        using (var context = new GameDbContext())
        {
            return context.GameWorlds
                .Select(w => ValueTuple.Create(w.Id, w.Name, w.LastSavedAt))
                .ToList();
        }
    }
}

class Camera
{
    public int ViewWidth { get; private set; }
    public int ViewHeight { get; private set; }
    public int CenterX { get; set; }
    public int CenterY { get; set; }
    
    public Camera(int viewWidth, int viewHeight)
    {
        ViewWidth = viewWidth;
        ViewHeight = viewHeight;
    }
    
    public void CenterOn(int x, int y)
    {
        CenterX = x;
        CenterY = y;
    }
    
    public (int x, int y) GetTopLeft()
    {
        int x = CenterX - ViewWidth / 2;
        int y = CenterY - ViewHeight / 2;
        return (x, y);
    }
}

class RoguelikeRenderer
{
    private char[,] buffer;
    private int width;
    private int height;
    private int uiHeight;
    
    public RoguelikeRenderer(int width, int height, int uiHeight)
    {
        this.width = width;
        this.height = height;
        this.uiHeight = uiHeight;
        buffer = new char[height, width];
    }
    
    public void Render(GameWorld world, Camera camera)
    {
        camera.CenterOn(world.Player.X, world.Player.Y);
        var (topLeftX, topLeftY) = camera.GetTopLeft();
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                buffer[y, x] = ' ';
            }
        }
        
        for (int screenY = 0; screenY < height; screenY++)
        {
            for (int screenX = 0; screenX < width; screenX++)
            {
                int worldX = topLeftX + screenX;
                int worldY = topLeftY + screenY;
                
                Tile tile = world.GetTile(worldX, worldY);
                if (tile != null)
                {
                    buffer[screenY, screenX] = tile.Symbol;
                }
            }
        }
        
        var visibleEntities = world.GetEntitiesInArea(
            topLeftX, topLeftY, 
            camera.ViewWidth, camera.ViewHeight);
        
        foreach (var entity in visibleEntities)
        {
            int screenX = entity.X - topLeftX;
            int screenY = entity.Y - topLeftY;
            
            if (screenX >= 0 && screenX < width && screenY >= 0 && screenY < height)
            {
                buffer[screenY, screenX] = entity.Symbol;
            }
        }
        
        Console.SetCursorPosition(0, 0);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Console.Write(buffer[y, x]);
            }
            Console.WriteLine();
        }
        
        RenderUI(world);
    }
    
    private void RenderUI(GameWorld world)
    {
        Console.SetCursorPosition(0, height);
        Console.WriteLine(new string('=', width));
        
        string healthBar = $"Health: {world.Player.Health}/{world.Player.MaxHealth} ";
        string levelInfo = $"Level: {world.Player.Level} XP: {world.Player.Experience} ";
        string posInfo = $"Position: ({world.Player.X}, {world.Player.Y})";
        Console.WriteLine(healthBar + levelInfo + posInfo);
        
        Console.WriteLine(new string('-', width));
        
        // Display message buffer (newest to oldest)
        Console.WriteLine("Messages:");
        for (int i = 0; i < world.Messages.Count; i++)
        {
            string prefix = i == 0 ? "> " : "  ";
            Console.WriteLine(prefix + world.Messages[i]);
        }
        
        // Fill remaining message lines with blank space
        for (int i = world.Messages.Count; i < 5; i++)
        {
            Console.WriteLine();
        }
        
        Console.WriteLine(new string('-', width));
        Console.WriteLine("Commands: [hjkl/↑↓←→] Move | [yubn] Diagonal Move | [O] Open/Close Door | [I] Inventory | [S] Save | [ESC] Exit");
        Console.WriteLine("Combat: Walk into enemies to attack | Items: Walk over items to pick up | Doors: Press O then direction");
    }
}

class Program
{
    static void Main()
    {
        Console.CursorVisible = false;
        
        GameWorld world = ShowMainMenu();
        if (world == null)
        {
            Console.Clear();
            Console.WriteLine("No world loaded. Exiting.");
            return;
        }
        
        int viewWidth = 100;
        int viewHeight = 35;
        int uiHeight = 12;
        
        Camera camera = new Camera(viewWidth, viewHeight);
        RoguelikeRenderer renderer = new RoguelikeRenderer(viewWidth, viewHeight, uiHeight);
        
        bool running = true;
        bool awaitingDoorDirection = false;
        
        while (running)
        {
            Console.Clear();
            renderer.Render(world, camera);
            
            if (awaitingDoorDirection)
            {
                Console.SetCursorPosition(0, viewHeight + viewHeight);
                Console.Write("Choose direction to open/close door (hjkl/arrows or ESC to cancel): ");
            }
            
            if (Console.KeyAvailable)
            {
                ConsoleKeyInfo key = Console.ReadKey(true);
                
                if (awaitingDoorDirection)
                {
                    switch (key.Key)
                    {
                        case ConsoleKey.H:
                        case ConsoleKey.LeftArrow:
                            world.ToggleDoorInDirection(-1, 0);
                            awaitingDoorDirection = false;
                            break;
                        case ConsoleKey.J:
                        case ConsoleKey.DownArrow:
                            world.ToggleDoorInDirection(0, 1);
                            awaitingDoorDirection = false;
                            break;
                        case ConsoleKey.K:
                        case ConsoleKey.UpArrow:
                            world.ToggleDoorInDirection(0, -1);
                            awaitingDoorDirection = false;
                            break;
                        case ConsoleKey.L:
                        case ConsoleKey.RightArrow:
                            world.ToggleDoorInDirection(1, 0);
                            awaitingDoorDirection = false;
                            break;
                        case ConsoleKey.Y:
                            world.ToggleDoorInDirection(-1, -1);
                            awaitingDoorDirection = false;
                            break;
                        case ConsoleKey.U:
                            world.ToggleDoorInDirection(1, -1);
                            awaitingDoorDirection = false;
                            break;
                        case ConsoleKey.B:
                            world.ToggleDoorInDirection(-1, 1);
                            awaitingDoorDirection = false;
                            break;
                        case ConsoleKey.N:
                            world.ToggleDoorInDirection(1, 1);
                            awaitingDoorDirection = false;
                            break;
                        case ConsoleKey.Escape:
                            world.AddMessage("Cancelled.");
                            awaitingDoorDirection = false;
                            break;
                    }
                }
                else
                {
                    switch (key.Key)
                    {
                        case ConsoleKey.K:
                        case ConsoleKey.UpArrow:
                            world.MovePlayer(0, -1);
                            break;
                        case ConsoleKey.J:
                        case ConsoleKey.DownArrow:
                            world.MovePlayer(0, 1);
                            break;
                        case ConsoleKey.H:
                        case ConsoleKey.LeftArrow:
                            world.MovePlayer(-1, 0);
                            break;
                        case ConsoleKey.L:
                        case ConsoleKey.RightArrow:
                            world.MovePlayer(1, 0);
                            break;
                        case ConsoleKey.Y:
                            world.MovePlayer(-1, -1);
                            break;
                        case ConsoleKey.U:
                            world.MovePlayer(1, -1);
                            break;
                        case ConsoleKey.B:
                            world.MovePlayer(-1, 1);
                            break;
                        case ConsoleKey.N:
                            world.MovePlayer(1, 1);
                            break;
                        case ConsoleKey.O:
                            world.AddMessage("Choose a direction to open/close a door...");
                            awaitingDoorDirection = true;
                            break;
                        case ConsoleKey.S:
                            Console.SetCursorPosition(0, viewHeight + uiHeight);
                            Console.Write("Enter save name: ");
                            Console.CursorVisible = true;
                            string saveName = Console.ReadLine();
                            Console.CursorVisible = false;
                            world.SaveToDatabase(saveName);
                            world.AddMessage("Game saved successfully!");
                            break;
                        case ConsoleKey.Escape:
                            running = false;
                            break;
                    }
                }
            }
            
            System.Threading.Thread.Sleep(50);
        }
        
        Console.Clear();
        Console.CursorVisible = true;
        Console.WriteLine("Thanks for playing!");
    }
    
    static GameWorld ShowMainMenu()
    {
        while (true)
        {
            Console.Clear();
            Console.WriteLine("╔════════════════════════════════════╗");
            Console.WriteLine("║      ROGUELIKE DUNGEON GAME        ║");
            Console.WriteLine("╚════════════════════════════════════╝\n");
            Console.WriteLine("1. New Game");
            Console.WriteLine("2. Load Game");
            Console.WriteLine("3. Exit\n");
            Console.Write("Choose an option: ");
            
            string choice = Console.ReadLine();
            
            switch (choice)
            {
                case "1":
                    return new GameWorld(120, 60);
                    
                case "2":
                    var saves = GameWorld.GetSavedWorlds();
                    if (saves.Count == 0)
                    {
                        Console.WriteLine("\nNo saved games found. Press any key...");
                        Console.ReadKey();
                        continue;
                    }
                    
                    Console.Clear();
                    Console.WriteLine("╔════════════════════════════════════╗");
                    Console.WriteLine("║          LOAD SAVED GAME           ║");
                    Console.WriteLine("╚════════════════════════════════════╝\n");
                    for (int i = 0; i < saves.Count; i++)
                    {
                        Console.WriteLine($"{i + 1}. {saves[i].Name} (Last saved: {saves[i].LastSaved})");
                    }
                    Console.WriteLine($"{saves.Count + 1}. Back\n");
                    Console.Write("Choose a save: ");
                    
                    if (int.TryParse(Console.ReadLine(), out int saveChoice) && 
                        saveChoice > 0 && saveChoice <= saves.Count)
                    {
                        return GameWorld.LoadFromDatabase(saves[saveChoice - 1].Id);
                    }
                    break;
                    
                case "3":
                    return null;
            }
        }
    }
}
