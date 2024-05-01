extends Node2D

var settings_scene = preload("res://scenes/settings.tscn")

func _on_play_pressed():
	get_tree().change_scene_to_file("res://scenes/map.tscn")

func _on_settings_pressed():
	add_child(settings_scene.instantiate())

func _on_quit_pressed():
	get_tree().quit()

