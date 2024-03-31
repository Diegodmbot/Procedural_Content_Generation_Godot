using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class MapStructure : Node2D
{
	public readonly PackedScene DoorScene = ResourceLoader.Load<PackedScene>("res://scenes/door.tscn");


	private enum MapType
	{
		AREA = 0,
		WALLS = 1,
		DOORS = 2,
		GROUND = 3
	}

	private enum NeighborType
	{
		NEIGHBOORS = 1,
		COUNTED = 2,
		DOORS = 3
	}


	[Export] Godot.Vector2 ExportedBorders = new(100, 100);

	readonly System.Numerics.Vector2[] Directions = [new(0, 1), new(0, -1), new(1, 0), new(-1, 0)];
	const double MinimumGroundPerRoom = 0.3;

	VoronoiDiagram VoronoiDiagram;
	TileMap DungeonTileMap;
	CharacterBody2D Player;
	private System.Numerics.Vector2 _borders;
	List<byte[,]> Structure { get; set; } = [];
	// Guarda las habitaciones conectadas los índices de los array representa el id de las habitaciones
	// Si el número en la posición [i,j] es diferente a 0 significa que las habitaciones i y j están conectadas
	byte[,] Neighborhood;
	// Guarda la posición de las puertas de cada habitación
	List<System.Numerics.Vector2>[] DoorsPositions;
	(int area, int ground)[] Surfaces;


	public override void _Ready()
	{
		// Guardar nodos hijos
		VoronoiDiagram = GetNode<VoronoiDiagram>("VoronoiDiagram");
		DungeonTileMap = GetNode<TileMap>("DungeonTileMap");
		DungeonTileMap.Clear();
		Player = GetNode<CharacterBody2D>("Player");
		// Número de puntos en el mapa
		int pointsCount = VoronoiDiagram.PointsLimit + 1;
		_borders = new(ExportedBorders.X, ExportedBorders.Y);
		Structure.Add(new byte[(int)_borders.X, (int)_borders.Y]);
		Neighborhood = new byte[pointsCount, pointsCount];
		DoorsPositions = Enumerable.Range(0, pointsCount)
														.Select(_ => new List<System.Numerics.Vector2>())
														.ToArray();
		Surfaces = new (int, int)[pointsCount];
		GenerateRooms();
		GenerateBorders();
		SetNeighborsConnections();
		SetDoors();
		RunRandomWalker();
		DrawMap();
		SetPlayer();
	}


	private void GenerateRooms()
	{
		var map = VoronoiDiagram.BuildVoronoiDiagram(_borders);
		Structure[(int)MapType.AREA] = map;
	}

	private void GenerateBorders()
	{
		Structure.Add(new byte[(int)_borders.X, (int)_borders.Y]);
		for (int i = 0; i < _borders.X; i++)
		{
			for (int j = 0; j < _borders.Y; j++)
			{
				// Si es un límete del mapa
				if (i == 0 || i == _borders.X - 1 || j == 0 || j == _borders.Y - 1)
				{
					Structure[(int)MapType.WALLS][i, j] = 1;
				}
				else
				{
					foreach (var direction in Directions)
					{
						byte neighbor = Structure[(int)MapType.AREA][i + (int)direction.X, j + (int)direction.Y];
						if (neighbor != Structure[(int)MapType.AREA][i, j] && neighbor != 0)
						{
							// El muro guarda el id de la habitación adyacente
							Structure[(int)MapType.WALLS][i, j] = neighbor;
							Neighborhood[Structure[(int)MapType.AREA][i, j], neighbor] = (byte)NeighborType.NEIGHBOORS;
							break;
						}
					}
					if (Structure[(int)MapType.WALLS][i, j] == 0)
					{
						Surfaces[Structure[(int)MapType.AREA][i, j]].area += 1;
					}
				}
			}
		}
	}

	private void SetNeighborsConnections()
	{
		for (int i = 1; i < Neighborhood.GetLength(0); i++)
		{
			for (int j = 1; j < i; j++)
			{
				if (Neighborhood[i, j] == 1)
				{
					Neighborhood[i, j] = (byte)NeighborType.COUNTED;
				}
			}
		}
	}

	// En Structure[mapType.Doors] se guardan las puertas en la posicion correspondiente y la casilla de suelo desde la que se entra. 
	// Las puertas se generan en uno de los muros (A) de las habitaciones donde haya
	// una habitación adyacente (B) y otra habitación diferente (C) a 2 casillas de distancia en
	// la misma dirección y sentido contrario. Además se guarda el muro opuesto de la habitación vecina (D).
	// Cada puerta (A y D) tendrá un valor identico y único. Las entradas (B y C) tendrá el identificador de la habitación.
	private void SetDoors()
	{
		Structure.Add(new byte[(int)_borders.X, (int)_borders.Y]);
		byte doorId = 0;
		bool allDoorsSet = false;
		Random random = new();
		while (allDoorsSet == false)
		{
			int doorX = random.Next(1, (int)_borders.X - 1);
			int doorY = random.Next(1, (int)_borders.Y - 1);
			// La casilla tiener que ser un muro
			if (Structure[(int)MapType.WALLS][doorX, doorY] != 0)
			{
				foreach (var direction in Directions)
				{
					int adjacentRoomX = doorX + (int)direction.X;
					int adjacentRoomY = doorY + (int)direction.Y;
					// La casilla adyacente no puede ser un muro y tiene que ser de la misma habitación que la puerta
					if (Structure[(int)MapType.WALLS][adjacentRoomX, adjacentRoomY] == 0)
					{
						int oppositeRoomX = doorX - (int)direction.X * 2;
						int oppositeRoomY = doorY - (int)direction.Y * 2;
						// La casilla de la habitación opuesta no pueder ser un muro
						if (oppositeRoomX > 0 && oppositeRoomX < _borders.X && oppositeRoomY > 0 && oppositeRoomY < _borders.Y && Structure[(int)MapType.WALLS][oppositeRoomX, oppositeRoomY] == 0)
						{
							if (Neighborhood[Structure[(int)MapType.AREA][adjacentRoomX, adjacentRoomY], Structure[(int)MapType.AREA][oppositeRoomX, oppositeRoomY]] == 2)
							{
								Structure[(int)MapType.DOORS][doorX, doorY] = doorId++;
								Structure[(int)MapType.DOORS][doorX - (int)direction.X, doorY - (int)direction.Y] = doorId++;
								// Guardar la vencidad
								Neighborhood[Structure[(int)MapType.AREA][adjacentRoomX, adjacentRoomY], Structure[(int)MapType.AREA][oppositeRoomX, oppositeRoomY]] = (byte)NeighborType.DOORS;
								Neighborhood[Structure[(int)MapType.AREA][oppositeRoomX, oppositeRoomY], Structure[(int)MapType.AREA][adjacentRoomX, adjacentRoomY]] = (byte)NeighborType.DOORS;
								// Guardar la posicion de las puertas
								DoorsPositions[Structure[(int)MapType.AREA][adjacentRoomX, adjacentRoomY]].Add(new System.Numerics.Vector2(doorX, doorY));
								DoorsPositions[Structure[(int)MapType.AREA][oppositeRoomX, oppositeRoomY]].Add(new System.Numerics.Vector2(doorX - (int)direction.X, doorY - (int)direction.Y));
								break;
							}
						}
					}
				}
				for (int i = 0; i < Neighborhood.GetLength(0); i++)
				{
					for (int j = 0; j < Neighborhood.GetLength(1); j++)
					{
						if (Neighborhood[i, j] == (byte)NeighborType.COUNTED)
						{
							allDoorsSet = false;
							break;
						}
						else
						{
							allDoorsSet = true;
						}
					}
				}
			}
		}
	}

	private void RunRandomWalker()
	{
		Structure.Add(new byte[(int)_borders.X, (int)_borders.Y]);
		List<System.Numerics.Vector2> automatas;
		bool roomCreated;
		for (int i = 1; i < DoorsPositions.Length; i++)
		{
			automatas = new(DoorsPositions[i]);
			roomCreated = false;
			while (!roomCreated)
			{
				// Comprobar con un BFS que todas las casillas están conectadas
				roomCreated = PathConnected(automatas[0], i) && (double)Surfaces[i].ground / Surfaces[i].area > MinimumGroundPerRoom;
				// mover cada automata
				for (int j = 0; j < DoorsPositions[i].Count; j++)
				{
					if (Structure[(int)MapType.GROUND][(int)automatas[j].X, (int)automatas[j].Y] == 0)
					{
						Surfaces[i].ground += 1;
					}
					Structure[(int)MapType.GROUND][(int)automatas[j].X, (int)automatas[j].Y] = (byte)i;
					automatas[j] = MoveAutomata(automatas[j]);
				}
			}
		}
	}

	private System.Numerics.Vector2 MoveAutomata(System.Numerics.Vector2 automata)
	{
		Random random = new();
		int randomNumber = random.Next(0, 4);
		System.Numerics.Vector2 targetPosition = automata + Directions[randomNumber];
		while (Structure[(int)MapType.WALLS][(int)targetPosition.X, (int)targetPosition.Y] != 0)
		{
			randomNumber = random.Next(0, 4);
			targetPosition = automata + Directions[randomNumber];
		}
		return targetPosition;
	}

	private bool PathConnected(System.Numerics.Vector2 initialPosition, int roomId)
	{
		Queue<System.Numerics.Vector2> queue = new();
		bool[,] visited = new bool[(int)_borders.X, (int)_borders.Y];
		queue.Enqueue(initialPosition);
		visited[(int)initialPosition.X, (int)initialPosition.Y] = true;
		List<System.Numerics.Vector2> doors = new(DoorsPositions[roomId]);
		while (queue.Count > 0)
		{
			System.Numerics.Vector2 current = queue.Dequeue();
			foreach (var direction in Directions)
			{
				System.Numerics.Vector2 neighbor = current + direction;
				if (neighbor.X >= 0 && neighbor.X < _borders.X && neighbor.Y >= 0 && neighbor.Y < _borders.Y)
				{
					if (Structure[(int)MapType.GROUND][(int)neighbor.X, (int)neighbor.Y] != 0 && !visited[(int)neighbor.X, (int)neighbor.Y])
					{
						queue.Enqueue(neighbor);
						visited[(int)neighbor.X, (int)neighbor.Y] = true;
						if (doors.Contains(neighbor))
						{
							doors.Remove(neighbor);
						}
					}
				}
			}
		}
		return doors.Count == 0;
	}

	private void DrawMap()
	{
		List<System.Numerics.Vector2> groundTiles = [];
		List<System.Numerics.Vector2> wallTiles = [];
		for (int i = 0; i < _borders.X; i++)
		{
			for (int j = 0; j < _borders.Y; j++)
			{
				// 	if (Structure[(int)MapType.DOORS][i, j] != 0)
				// 	{
				// 		Node2D DoorInstance = (Node2D)DoorScene.Instantiate();
				// 		AddChild(DoorInstance);
				// 		DoorInstance.Position = new Vector2(i * 16 + 8, j * 16 + 8);
				// 	}
				if (Structure[(int)MapType.GROUND][i, j] == 0)
				{
					DungeonTileMap.SetCell(0, new Vector2I(i, j), 2, Vector2I.Zero);
				}
				else
				{
					groundTiles.Add(new System.Numerics.Vector2(i, j));
				}
			}
		}
		Array<Vector2I> groundTilesArray = new(groundTiles.Select(v => new Vector2I((int)v.X, (int)v.Y)).ToArray());
		DungeonTileMap.SetCellsTerrainConnect(0, groundTilesArray, 0, 0);
	}

	private void SetPlayer()
	{
		Random random = new();
		while (true)
		{
			System.Numerics.Vector2 playerPosition = new(random.Next(1, (int)_borders.X), random.Next(1, (int)_borders.Y));
			if (Structure[(int)MapType.GROUND][(int)playerPosition.X, (int)playerPosition.Y] != 0)
			{
				Player.Position = new Vector2(playerPosition.X, playerPosition.Y) * DungeonTileMap.TileSet.TileSize;
				break;
			}
		}
	}
}