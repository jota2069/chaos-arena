// Шейдер мигания при уроне: подмешивает красный к спрайту по flash_intensity.
// Используется LocalPlayer (ShaderMaterial на AnimatedSprite2D тела).
shader_type canvas_item;

uniform float flash_intensity : hint_range(0.0, 1.0) = 0.0;

void fragment() {
    vec4 tex = texture(TEXTURE, UV);
    COLOR = mix(tex, vec4(1.0, 0.0, 0.0, tex.a), flash_intensity);
    COLOR.a = tex.a;
}
