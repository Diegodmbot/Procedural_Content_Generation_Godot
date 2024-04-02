extends Node

var door_scene = preload("res://scenes/door.tscn")

@onready var map_structure = $MapStructure
@onready var player = $Player
@onready var doors_manager = $DoorsManager

func _ready():
	doors_manager.connect("player_exit_door", on_player_exit_door)
	map_structure.GenerateMapStructure()
	set_player()
	set_doors()

func set_player():
	var ground_structure: Array = map_structure.GetRoom(1)
	var player_starting_position = ground_structure.pick_random()
	player.position = player_starting_position * 16 + Vector2(8,8)


func set_doors():
	var doors_positions: Array = map_structure.GetDoors()
	var spawns: Array = map_structure.GetSpawnsPositions()
	for i in doors_positions.size():
		var door_instance: Door = door_scene.instantiate()
		door_instance.id = i
		door_instance.position = doors_positions[i] * 16 + Vector2(8,8)
		door_instance.spawn_position = spawns[i] * 16 + Vector2(8,8)
		doors_manager.add_door(door_instance)

func on_player_exit_door(position: Vector2 ):
	player.position = position