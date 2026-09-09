namespace Terrain.App.Rendering.OpenGl;

// Paired with SunPath.SkyColor: same pixel centres, aspect correction and palette.
internal static class SkyShader
{
    public const string Vertex = """
        #version 150
        void main() {
            vec2 p = vec2(float((gl_VertexID << 1) & 2), float(gl_VertexID & 2));
            gl_Position = vec4(p * 2.0 - 1.0, 0.0, 1.0);
        }
        """;
    public const string Fragment = """
        #version 150
        uniform vec3 viewport;
        uniform vec3 light;
        out vec4 outputColor;
        void main() {
            vec2 uv = vec2(gl_FragCoord.x / viewport.x, 1.0 - gl_FragCoord.y / viewport.y);
            float elevation = clamp(light.y, 0.0, 1.0);
            vec3 top = mix(vec3(.12, .10, .22), vec3(.07, .19, .34), elevation);
            vec3 horizon = mix(vec3(.72, .30, .17), vec3(.49, .67, .76), elevation);
            float blend = clamp(uv.y / .85, 0.0, 1.0);
            vec3 sky = mix(top, horizon, blend * blend);
            vec2 delta = uv - vec2(.5 + .42 * light.x, .65 - .53 * light.y);
            delta.x *= viewport.x / viewport.y;
            float distance = length(delta);
            vec3 warm = mix(vec3(1.0, .42, .12), vec3(1.0, .88, .56), elevation);
            sky += warm * (.24 * exp(-distance * distance / .018));
            float edge = clamp((distance - .024) / max(1.0 / viewport.y, 1e-6), 0.0, 1.0);
            float disk = 1.0 - edge * edge * (3.0 - 2.0 * edge);
            outputColor = vec4(clamp(mix(sky, mix(warm, vec3(1.0), .5), disk), 0.0, 1.0), 1.0);
        }
        """;
}
