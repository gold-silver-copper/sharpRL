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
    
    [ForeignKey("GameWorld")]
    public int GameWorldId { get; set; }
    
    protected Entity() { }
    
    protected Entity(int x, int y, char symbol, string name, bool blocksMovement = false)
    {
        X = x;
        Y = y;
        Symbol = symbol;
        Name = name;
        BlocksMovement = blocksMovement;
    }
}

class Player : Entity
{
    public int Health { get; set; }
    public int MaxHealth { get; set; }
    
    public Player() : base() { }
    
    public Player(int x, int y) : base(x, y, '@', "Player", true)
    {
        Health = 100;
        MaxHealth = 100;
    }
}

class Enemy : Entity
{
    public int Damage { get; set; }
    
    public Enemy() : base() { }
    
    public Enemy(int x, int y, string name) : base(x, y, 'E', name, true)
    {
        Damage = 10;
    }
}

class Item : Entity
{
    public string ItemType { get; set; }
    
    public Item() : base() { }
    
    public Item(int x, int y, char symbol, string name, string itemType) 
        : base(x, y, symbol, name, false)
    {
        ItemType = itemType;
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
            .HasValue<Item>("Item");
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
    
    public GameWorld(int width, int height)
    {
        Width = width;
        Height = height;
        tiles = new Tile[height, width];
        entities = new List<Entity>();
        
        InitializeWorld();
    }
    
    private GameWorld(GameWorldData data)
    {
        Width = data.Width;
        Height = data.Height;
        WorldId = data.Id;
        tiles = new Tile[data.Height, data.Width];
        entities = new List<Entity>();
        
        // Load tiles into array
        foreach (var tile in data.Tiles)
        {
            tiles[tile.Y, tile.X] = tile;
        }
        
        // Load entities
        entities.AddRange(data.Entities);
        Player = entities.OfType<Player>().FirstOrDefault();
    }
    
    private void InitializeWorld()
    {
        // Initialize all tiles as floor
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                tiles[y, x] = new Tile(x, y, '.', true, "Floor");
            }
        }
        
        // Create outer walls
        CreateBox(0, 0, Width, Height, '#', false, "Wall");
        
        // Create rooms
        CreateBox(5, 3, 10, 6, '#', false, "Wall");
        FillBox(6, 4, 8, 4, '.', true, "Floor");
        
        CreateBox(20, 8, 15, 8, '#', false, "Wall");
        FillBox(21, 9, 13, 6, '.', true, "Floor");
        
        CreateBox(10, 12, 8, 5, '#', false, "Wall");
        FillBox(11, 13, 6, 3, '.', true, "Floor");
        
        // Create player
        Player = new Player(8, 5);
        entities.Add(Player);
        
        // Create enemies
        entities.Add(new Enemy(25, 12, "Goblin"));
        entities.Add(new Enemy(28, 10, "Orc"));
        
        // Create items
        entities.Add(new Item(30, 10, 'T', "Gold Coins", "Treasure"));
        entities.Add(new Item(13, 14, '$', "Silver Key", "Key"));
        entities.Add(new Item(7, 5, '!', "Health Potion", "Potion"));
    }
    
    private void CreateBox(int x, int y, int width, int height, char symbol, bool walkable, string tileType)
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
                    tiles[py, px] = new Tile(px, py, symbol, walkable, tileType);
                }
            }
        }
    }
    
    private void FillBox(int x, int y, int width, int height, char symbol, bool walkable, string tileType)
    {
        for (int dy = 0; dy < height; dy++)
        {
            for (int dx = 0; dx < width; dx++)
            {
                int px = x + dx;
                int py = y + dy;
                
                if (px < 0 || px >= Width || py < 0 || py >= Height)
                    continue;
                
                tiles[py, px] = new Tile(px, py, symbol, walkable, tileType);
            }
        }
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
    
    public void MovePlayer(int dx, int dy)
    {
        int newX = Player.X + dx;
        int newY = Player.Y + dy;
        
        if (CanMoveTo(newX, newY))
        {
            Player.X = newX;
            Player.Y = newY;
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
                // Update existing world
                worldData = context.GameWorlds
                    .Include(w => w.Tiles)
                    .Include(w => w.Entities)
                    .FirstOrDefault(w => w.Id == WorldId);
                
                if (worldData != null)
                {
                    // Clear old data
                    context.Tiles.RemoveRange(worldData.Tiles);
                    context.Entities.RemoveRange(worldData.Entities);
                }
            }
            else
            {
                // Create new world
                worldData = new GameWorldData
                {
                    Name = worldName,
                    Width = Width,
                    Height = Height
                };
                context.GameWorlds.Add(worldData);
            }
            
            worldData.LastSavedAt = DateTime.Now;
            
            // Save tiles
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    var tile = tiles[y, x];
                    tile.GameWorldId = worldData.Id;
                    worldData.Tiles.Add(tile);
                }
            }
            
            // Save entities
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
    
    public RoguelikeRenderer(int width, int height)
    {
        this.width = width;
        this.height = height;
        buffer = new char[height, width];
    }
    
    public void RenderWorld(GameWorld world, Camera camera)
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
        
        Camera camera = new Camera(40, 20);
        RoguelikeRenderer renderer = new RoguelikeRenderer(40, 20);
        
        bool running = true;
        
        while (running)
        {
            renderer.RenderWorld(world, camera);
            
            Console.WriteLine($"\nHealth: {world.Player.Health}/{world.Player.MaxHealth} | Position: ({world.Player.X}, {world.Player.Y})");
            Console.WriteLine("Arrow Keys: Move | S: Save | ESC: Exit");
            
            if (Console.KeyAvailable)
            {
                ConsoleKeyInfo key = Console.ReadKey(true);
                
                switch (key.Key)
                {
                    case ConsoleKey.UpArrow:
                        world.MovePlayer(0, -1);
                        break;
                    case ConsoleKey.DownArrow:
                        world.MovePlayer(0, 1);
                        break;
                    case ConsoleKey.LeftArrow:
                        world.MovePlayer(-1, 0);
                        break;
                    case ConsoleKey.RightArrow:
                        world.MovePlayer(1, 0);
                        break;
                    case ConsoleKey.S:
                        Console.SetCursorPosition(0, 22);
                        Console.Write("Enter save name: ");
                        Console.CursorVisible = true;
                        string saveName = Console.ReadLine();
                        Console.CursorVisible = false;
                        world.SaveToDatabase(saveName);
                        Console.SetCursorPosition(0, 22);
                        Console.WriteLine("Game saved!                    ");
                        System.Threading.Thread.Sleep(1000);
                        break;
                    case ConsoleKey.Escape:
                        running = false;
                        break;
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
            Console.WriteLine("=== ROGUELIKE GAME ===\n");
            Console.WriteLine("1. New Game");
            Console.WriteLine("2. Load Game");
            Console.WriteLine("3. Exit\n");
            Console.Write("Choose an option: ");
            
            string choice = Console.ReadLine();
            
            switch (choice)
            {
                case "1":
                    return new GameWorld(80, 40);
                    
                case "2":
                    var saves = GameWorld.GetSavedWorlds();
                    if (saves.Count == 0)
                    {
                        Console.WriteLine("\nNo saved games found. Press any key...");
                        Console.ReadKey();
                        continue;
                    }
                    
                    Console.Clear();
                    Console.WriteLine("=== LOAD GAME ===\n");
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
