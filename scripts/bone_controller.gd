extends Node

const base_damage = 1
const base_scope = 5

@export var bone_scene: PackedScene
@onready var base_speed_attack: float = $Timer.wait_time

var actual_attack_scope: int

func _ready():
	actual_attack_scope = base_scope * PositionFixer.tile_size

func _on_timer_timeout():
	# updates position
	# Añadir una instancia del hueso que se mueva hacia el jugador
	var player = get_tree().get_first_node_in_group("player") as Node2D
	if player == null:
		return
	var bone_instance = bone_scene.instantiate()
	add_child(bone_instance)
	bone_instance.hitbox_component.damage = base_damage
	var skeleton_position = get_parent().global_position
	bone_instance.global_position = skeleton_position
	var player_angle = skeleton_position.angle_to_point(player.global_position)
	bone_instance.target_position = skeleton_position + Vector2(actual_attack_scope,0).rotated(player_angle)
	# Si el hueso choca contra un muro desaparece
		# mandar una señal si detecta un muro, activa una animación que lo hace más pequeño y desaparece
		# misma anim que cuando no toca nada
	# Si choca contra un jugador le hace daño y desaparece